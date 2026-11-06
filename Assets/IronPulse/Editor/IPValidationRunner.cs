using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace IronPulse.Editor
{
    /// <summary>
    /// Fixed-purpose entry point for the gameplay smoke test in an interactive Unity Editor.
    /// A local request file can queue validation when no editor connector is available.
    /// It never compiles or executes code supplied by that request.
    /// </summary>
    [InitializeOnLoad]
    public static class IPValidationRunner
    {
        const string RequestName = "IronPulseValidation.request.json";
        const string StateKey = "IronPulse.ValidationRunner.State";
        const string StatusName = "iron-pulse-editor-validation-status.json";
        static double nextPoll;

        [Serializable]
        sealed class Request
        {
            public string outputDirectory;
            public string reportFileName = "iron-pulse-v5-validation.json";
            public float timeoutSeconds = 180f;
            public bool leavePlayModeRunning;
        }

        [Serializable]
        sealed class RunState
        {
            public Request request;
            public string phase;
            public long startedUtcTicks;
            public long deadlineUtcTicks;
            public long finishAfterUtcTicks;
            public bool wasPlaying;
            public bool wasPaused;
        }

        [Serializable]
        sealed class Status
        {
            public string state;
            public bool passed;
            public int checkCount;
            public string message;
            public string reportPath;
            public string startedUtc;
            public string completedUtc;
        }

        static IPValidationRunner()
        {
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        static string ProjectRoot => Path.GetDirectoryName(Application.dataPath);

        [MenuItem("Iron Pulse/Run Gameplay Validation")]
        public static void RunFromMenu()
        {
            Queue(ReadRequest() ?? new Request());
        }

        // Also usable with -executeMethod in a normal, non-batch Editor launch.
        public static void RunFromCommandLine()
        {
            Queue(ReadRequest() ?? new Request());
        }

        static Request ReadRequest()
        {
            // UserSettings survives Editor restarts; Temp is convenient for a live Editor.
            foreach (string folder in new[] { "UserSettings", "Temp" })
            {
                string path = Path.Combine(ProjectRoot, folder, RequestName);
                if (!File.Exists(path)) continue;
                string json = File.ReadAllText(path);
                Request request = JsonUtility.FromJson<Request>(json);
                if (request == null) throw new InvalidDataException("Invalid gameplay validation request.");
                File.Delete(path); // Consume only the explicit, fixed-name validation request.
                return request;
            }
            return null;
        }

        static void Queue(Request request)
        {
            if (!string.IsNullOrEmpty(SessionState.GetString(StateKey, "")))
            {
                Debug.LogWarning("IRON PULSE validation is already queued or running.");
                return;
            }
            if (Application.isBatchMode)
                throw new InvalidOperationException("Gameplay validation requires a normal Editor Game view; screenshot coroutines do not run in batch mode.");
            request.outputDirectory = string.IsNullOrWhiteSpace(request.outputDirectory)
                ? Path.Combine(ProjectRoot, "Temp", "IronPulseValidation")
                : Path.GetFullPath(request.outputDirectory);
            if (string.IsNullOrWhiteSpace(request.reportFileName)) request.reportFileName = "iron-pulse-v5-validation.json";
            if (Path.GetFileName(request.reportFileName) != request.reportFileName || !request.reportFileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("The report file name must be a JSON file name without directories.");
            request.timeoutSeconds = Mathf.Clamp(request.timeoutSeconds, 60f, 600f);
            Directory.CreateDirectory(request.outputDirectory);
            long now = DateTime.UtcNow.Ticks;
            var state = new RunState
            {
                request = request,
                phase = "Queued",
                wasPlaying = EditorApplication.isPlaying,
                wasPaused = EditorApplication.isPaused,
                startedUtcTicks = now,
                deadlineUtcTicks = now + (long)(request.timeoutSeconds * TimeSpan.TicksPerSecond)
            };
            Save(state);
            WriteStatus(state, "Queued", false, 0, "Gameplay validation queued.");
        }

        static void Save(RunState state) => SessionState.SetString(StateKey, JsonUtility.ToJson(state));

        static void Tick()
        {
            if (EditorApplication.timeSinceStartup < nextPoll) return;
            nextPoll = EditorApplication.timeSinceStartup + .2;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            RunState state = null;
            try
            {
                string stored = SessionState.GetString(StateKey, "");
                if (string.IsNullOrEmpty(stored))
                {
                    Request request = ReadRequest();
                    if (request != null) Queue(request);
                    return;
                }
                state = JsonUtility.FromJson<RunState>(stored);
                if (DateTime.UtcNow.Ticks > state.deadlineUtcTicks)
                {
                    Finish(state, false, 0, "Timed out waiting for gameplay validation to finish.");
                    return;
                }
                if (state.phase == "Queued")
                {
                    if (EditorSceneManager.GetActiveScene().path != IPSceneBuilder.ScenePath)
                    {
                        if (EditorApplication.isPlaying || EditorSceneManager.GetActiveScene().isDirty)
                            throw new InvalidOperationException("Open the arena scene and save other scene changes before validation.");
                        EditorSceneManager.OpenScene(IPSceneBuilder.ScenePath, OpenSceneMode.Single);
                    }
                    state.phase = "AwaitPlay";
                    Save(state); // SessionState survives the Play Mode domain reload.
                    EditorApplication.isPaused = false;
                    EditorApplication.ExecuteMenuItem("Window/General/Game");
                    if (!EditorApplication.isPlaying) EditorApplication.isPlaying = true;
                    return;
                }
                if (state.phase == "AwaitPlay")
                {
                    if (!EditorApplication.isPlaying || IPGame.Instance == null || IPGame.Instance.Player == null) return;
                    if (UnityEngine.Object.FindFirstObjectByType<IPSmokeTest>() != null)
                        throw new InvalidOperationException("Another gameplay smoke test is already running.");
                    state.startedUtcTicks = DateTime.UtcNow.Ticks;
                    state.phase = "Running";
                    Save(state);
                    WriteStatus(state, "Running", false, 0, "Gameplay validation running.");
                    IPGame.Instance.gameObject.AddComponent<IPSmokeTest>().Run(state.request.outputDirectory);
                    return;
                }
                if (!EditorApplication.isPlaying)
                    throw new InvalidOperationException("Play Mode stopped before validation completed.");
                string reportPath = Path.Combine(state.request.outputDirectory, state.request.reportFileName);
                bool freshReport = File.Exists(reportPath) && File.GetLastWriteTimeUtc(reportPath).Ticks >= state.startedUtcTicks;
                if (state.phase == "Running" && freshReport && UnityEngine.Object.FindFirstObjectByType<IPSmokeTest>() == null)
                {
                    // Let the last end-of-frame screenshot finish before restoring Editor state.
                    state.phase = "AwaitFinish";
                    state.finishAfterUtcTicks = DateTime.UtcNow.AddSeconds(.3).Ticks;
                    Save(state);
                }
                if (state.phase == "AwaitFinish" && DateTime.UtcNow.Ticks >= state.finishAfterUtcTicks)
                {
                    var report = JsonUtility.FromJson<IPSmokeTest.Report>(File.ReadAllText(reportPath));
                    if (report == null || report.checks == null) throw new InvalidDataException("Gameplay report is incomplete.");
                    Finish(state, report.allPassed, report.checks.Length, report.allPassed ? "All gameplay checks passed." : "One or more gameplay checks failed; inspect the report.");
                }
            }
            catch (Exception exception)
            {
                if (state != null) Finish(state, false, 0, exception.Message);
                else
                {
                    SessionState.EraseString(StateKey);
                    Debug.LogError("IRON PULSE validation request failed: " + exception.Message);
                }
            }
        }

        static void Finish(RunState state, bool passed, int count, string message)
        {
            SessionState.EraseString(StateKey);
            WriteStatus(state, passed ? "Passed" : "Failed", passed, count, message);
            if (!passed)
            {
                var helper = UnityEngine.Object.FindFirstObjectByType<IPSmokeTest>();
                if (helper != null) UnityEngine.Object.DestroyImmediate(helper);
                Debug.LogWarning("IRON PULSE validation failed: " + message);
            }
            else Debug.Log("IRON PULSE validation passed: " + count + " checks.");
            // A manual stop stays stopped; only restore flags of an Editor still in Play Mode.
            if (EditorApplication.isPlaying)
            {
                if (!state.wasPlaying && !state.request.leavePlayModeRunning) EditorApplication.isPlaying = false;
                else EditorApplication.isPaused = state.wasPaused;
            }
        }

        static void WriteStatus(RunState state, string phase, bool passed, int count, string message)
        {
            File.WriteAllText(Path.Combine(state.request.outputDirectory, StatusName), JsonUtility.ToJson(new Status
            {
                state = phase,
                passed = passed,
                checkCount = count,
                message = message,
                reportPath = Path.Combine(state.request.outputDirectory, state.request.reportFileName),
                startedUtc = new DateTime(state.startedUtcTicks, DateTimeKind.Utc).ToString("O"),
                completedUtc = phase == "Passed" || phase == "Failed" ? DateTime.UtcNow.ToString("O") : null
            }, true));
        }
    }
}
