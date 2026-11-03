using System.Collections.Generic;
using UnityEngine;

namespace IronPulse
{
    /// <summary>Recorded CC0 foley and firearm library. Source and edit credits live beside the clips.</summary>
    public static class IPAudio
    {
        const string Root = "Audio/IronPulse/";
        const int MaxVoices = 48;
        static readonly Dictionary<string, AudioClip[]> banks = new Dictionary<string, AudioClip[]>();
        static readonly Dictionary<string, int> previous = new Dictionary<string, int>();
        static readonly HashSet<string> missing = new HashSet<string>();
        static readonly List<AudioSource> voices = new List<AudioSource>();
        static readonly string[] bankNames = { "pistol", "rifle", "shotgun", "mechanism_open", "mechanism_shell", "mechanism_close", "metal", "plate", "punch", "body", "break", "steps", "swish", "blade_draw", "blade_swing", "bell" };

        public static int PlayCount { get; private set; }
        public static string LastCue { get; private set; }
        public static int MissingBankCount => missing.Count;
        public static int LoadedClipCount
        {
            get { int count = 0; foreach (var bank in banks.Values) count += bank.Length; return count; }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            banks.Clear(); previous.Clear(); missing.Clear(); voices.Clear();
            PlayCount = 0; LastCue = null;
        }

        public static void Warmup()
        {
            foreach (string name in bankNames) Bank(name);
        }

        static AudioClip[] Bank(string name)
        {
            if (banks.TryGetValue(name, out var loaded)) return loaded;
            loaded = Resources.LoadAll<AudioClip>(Root + name);
            banks[name] = loaded;
            if (loaded.Length == 0 && missing.Add(name))
                Debug.LogWarning("IRON PULSE: recorded audio bank missing: " + Root + name);
            return loaded;
        }

        public static void Play(string cue, Vector3 position, float volume = 1f)
        {
            if (string.IsNullOrEmpty(cue) || volume <= 0f || !Application.isPlaying) return;
            PlayCount++; LastCue = cue;
            float gain = Mathf.Clamp01(volume) * .78f;
            switch (cue)
            {
                case "shotgun":
                    Voice("shotgun", position, gain, .96f, 1.015f, true, 20);
                    Voice("mechanism_close", position, gain * .13f, .88f, .94f, true, 55, .045f);
                    break;
                case "pistol_fire":
                    Voice("pistol", position, gain * .94f, .98f, 1.035f, true, 22); break;
                case "rifle_fire":
                    Voice("rifle", position, gain * .92f, .98f, 1.025f, true, 24); break;
                case "reload": case "weapon_switch":
                    Voice("mechanism_open", position, gain * .64f, .98f, 1.06f, true, 70); break;
                case "reload_open":
                    Voice("mechanism_open", position, gain, .96f, 1.03f, true, 38); break;
                case "reload_shells":
                    Voice("mechanism_shell", position, gain, .97f, 1.04f, true, 38); break;
                case "reload_close":
                    Voice("mechanism_close", position, gain, .98f, 1.035f, true, 38); break;
                case "katana_draw":
                    Voice("blade_draw", position, gain, .96f, 1.035f, true, 38); break;
                case "katana_swing": case "ultimate_swing":
                    Voice("blade_swing", position, gain, .91f, 1.06f, true, 32);
                    Voice("swish", position, gain * .28f, .87f, .99f, true, 48); break;
                case "melee_swing":
                    Voice("swish", position, gain * .85f, .83f, .98f, true, 38); break;
                case "melee_hit": case "punchImpact":
                    Voice("punch", position, gain, .87f, 1.01f, false, 32);
                    Voice("metal", position, gain * .35f, .89f, 1.07f, false, 45, .015f); break;
                case "execute_start": case "execute_grab":
                    Voice("body", position, gain * .7f, .88f, 1.0f, true, 42);
                    Voice("metal", position, gain * .26f, .84f, .97f, false, 50); break;
                case "execute": case "execute_hit":
                    Voice("punch", position, gain * .94f, .78f, .92f, false, 24);
                    Voice("plate", position, gain * .45f, .83f, .98f, false, 38);
                    Voice("break", position, gain * .56f, .79f, .92f, false, 36, .018f); break;
                case "execute_rip": case "metalTear":
                    Voice("break", position, gain, .72f, .9f, false, 26);
                    Voice("plate", position, gain * .64f, .69f, .86f, false, 40, .026f);
                    Voice("punch", position, gain * .45f, .78f, .88f, false, 44, .045f); break;
                case "enemyWindup":
                    Voice("mechanism_open", position, gain * .88f, .79f, .94f, false, 65); break;
                case "enemyShot":
                    Voice("rifle", position, gain * .85f, .79f, .91f, false, 46); break;
                case "enemyHit": case "boltImpact":
                    Voice("metal", position, gain * .86f, .89f, 1.09f, false, 55);
                    Voice("body", position, gain * .3f, .9f, 1.02f, false, 65); break;
                case "enemyDeath":
                    Voice("plate", position, gain * .7f, .73f, .95f, false, 52);
                    Voice("body", position, gain * .5f, .76f, .95f, false, 60); break;
                case "damage":
                    Voice("punch", position, gain * .72f, .83f, .97f, true, 36); break;
                case "land":
                    Voice("steps", position, gain, .7f, .83f, true, 58);
                    Voice("body", position, gain * .28f, .88f, 1f, true, 65); break;
                case "footstep":
                    Voice("steps", position, gain * .8f, .91f, 1.07f, true, 72); break;
                case "jump":
                    Voice("steps", position, gain * .48f, 1f, 1.12f, true, 68); break;
                case "doublejump": case "dash":
                    Voice("swish", position, gain * .77f, cue == "dash" ? .78f : .95f, 1.05f, true, 42); break;
                case "stagger":
                    Voice("metal", position, gain * .55f, .66f, .78f, false, 45);
                    Voice("mechanism_close", position, gain * .35f, .8f, .94f, false, 52); break;
                case "ultimate_ready":
                    Voice("blade_draw", position, gain * .7f, .78f, .86f, true, 25);
                    Voice("bell", position, gain * .35f, .75f, .82f, true, 45, .065f); break;
                case "ultimate_start":
                    Voice("blade_draw", position, gain, .9f, 1f, true, 24);
                    Voice("swish", position, gain * .5f, .7f, .82f, true, 40); break;
                case "wave":
                    Voice("mechanism_close", position, gain * .42f, .75f, .82f, true, 65); break;
                default:
                    // Unknown action names remain physical foley; no synthetic or musical fallback.
                    Voice("body", position, gain * .5f, .94f, 1.04f, false, 72); break;
            }
        }

        static void Voice(string bankName, Vector3 position, float gain, float pitchMin, float pitchMax,
            bool playerLocal, int priority, float delay = 0f)
        {
            var clips = Bank(bankName);
            if (clips.Length == 0) return;
            int index = Random.Range(0, clips.Length);
            if (clips.Length > 1 && previous.TryGetValue(bankName, out int last) && index == last)
                index = (index + 1 + Random.Range(0, clips.Length - 1)) % clips.Length;
            previous[bankName] = index;
            for (int i = voices.Count - 1; i >= 0; --i)
                if (!voices[i] || !voices[i].isPlaying && voices[i].time > .01f) voices.RemoveAt(i);
            if (voices.Count >= MaxVoices)
            {
                int oldest = -1;
                for (int i = 0; i < voices.Count; i++)
                    if (voices[i] && voices[i].priority >= priority) { oldest = i; break; }
                if (oldest < 0) return;
                if (voices[oldest]) Object.Destroy(voices[oldest].gameObject);
                voices.RemoveAt(oldest);
            }
            var obj = new GameObject("Recorded SFX // " + bankName);
            obj.transform.position = position;
            var source = obj.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.clip = clips[index];
            source.volume = gain;
            source.pitch = Random.Range(pitchMin, pitchMax);
            source.spatialBlend = playerLocal ? 0f : 1f;
            source.dopplerLevel = 0f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = 3.5f; source.maxDistance = 48f;
            source.spread = playerLocal ? 0f : 35f;
            source.priority = priority;
            source.reverbZoneMix = .26f;
            voices.Add(source);
            if (delay > 0f) source.PlayDelayed(delay); else source.Play();
            Object.Destroy(obj, source.clip.length / Mathf.Max(.1f, source.pitch) + delay + .15f);
        }
    }
}
