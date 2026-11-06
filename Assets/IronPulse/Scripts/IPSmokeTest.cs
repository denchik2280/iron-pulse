using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace IronPulse
{
    // Optional integration check, invoked explicitly from the Unity editor.
    // It drives the same keyboard/mouse state that the playable controller reads.
    [DefaultExecutionOrder(-1000)]
    public sealed class IPSmokeTest : MonoBehaviour
    {
        [Serializable] public class Entry { public string name;public bool passed;public string detail; }
        [Serializable] public class Report {public string game="IRON PULSE";public string unity;public bool allPassed;public float averageFrameMilliseconds;public int sampledFrames;public Entry[] checks;}
        List<Entry> checks=new List<Entry>();
        Keyboard keyboard,previousKeyboard; Mouse mouse,previousMouse;
        IPGame game;IPPlayer player;
        string output;
        int runtimeErrors;string firstRuntimeError;
        float frameSeconds;int frameSamples;
        InputSettings.BackgroundBehavior oldBackground;
#if UNITY_EDITOR
        InputSettings.EditorInputBehaviorInPlayMode oldEditorInput;
#endif
        public void Run(string outputDirectory){output=outputDirectory;Directory.CreateDirectory(output);Application.logMessageReceived+=TrackRuntimeErrors;StartCoroutine(Check());}
        void TrackRuntimeErrors(string message,string stack,LogType kind)
        {if(kind==LogType.Error||kind==LogType.Exception||kind==LogType.Assert){runtimeErrors++;if(firstRuntimeError==null)firstRuntimeError=message;}}
        void Record(string name,bool passed,string detail)
        {checks.Add(new Entry{name=name,passed=passed,detail=detail});Debug.Log("[IRON PULSE CHECK] "+name+": "+passed+" // "+detail);}
        void Keys(params Key[] keys){InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));}
        IEnumerator Check()
        {
            game=IPGame.Instance;player=game.Player;game.BeginRun();
            previousKeyboard=Keyboard.current;previousMouse=Mouse.current;
            oldBackground=InputSystem.settings.backgroundBehavior;InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            oldEditorInput=InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            keyboard=InputSystem.AddDevice<Keyboard>("IronPulseValidationKeyboard");mouse=InputSystem.AddDevice<Mouse>("IronPulseValidationMouse");
            InputSystem.onAfterUpdate+=PinDevices;PinDevices();
            foreach(var enemy in FindObjectsByType<IPEnemy>(FindObjectsSortMode.None))enemy.enabled=false;
            player.ResetAt(IPWorld.PlayerSpawn);yield return new WaitForSeconds(.6f);
            Vector3 start=player.transform.position;Keys(Key.W);yield return new WaitForSeconds(.3f);Keys();yield return null;
            Record("WASD movement",Vector3.Distance(start,player.transform.position)>1f,"distance="+Vector3.Distance(start,player.transform.position).ToString("F2"));
            player.ResetAt(IPWorld.PlayerSpawn);yield return new WaitForSeconds(.45f);start=player.transform.position;
            Keys(Key.D,Key.LeftShift);yield return new WaitForSeconds(.14f);float dashDistance=Vector3.Distance(start,player.transform.position);float dashCooldown=player.DashCooldown01;Keys();yield return new WaitForSeconds(.15f);
            Record("Directional dash",dashDistance>2.1f&&dashCooldown>0,"distance="+dashDistance.ToString("F2")+", cooldown="+dashCooldown.ToString("F2"));
            yield return CheckDoubleDash();
            player.ResetAt(IPWorld.PlayerSpawn);yield return new WaitForSeconds(.5f);float floor=player.transform.position.y;
            Keys(Key.Space);yield return new WaitForSeconds(.12f);Keys();yield return new WaitForSeconds(.12f);int firstJumps=player.JumpsRemaining;
            Keys(Key.Space);yield return new WaitForSeconds(.12f);Keys();yield return new WaitForSeconds(.15f);
            Record("Double jump",firstJumps==1&&player.JumpsRemaining==0&&player.transform.position.y>floor+2,"remaining="+player.JumpsRemaining+", rise="+(player.transform.position.y-floor).ToString("F2"));
            player.ResetAt(IPWorld.PlayerSpawn);yield return new WaitForSeconds(.5f);
            int ammo=player.Shells;InputSystem.QueueStateEvent(mouse,new MouseState{buttons=1});yield return null;yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(output,"iron-pulse-v5-firing.png"));
            yield return new WaitForSeconds(.1f);InputSystem.QueueStateEvent(mouse,new MouseState());yield return new WaitForSeconds(.2f);
            Record("Shotgun fires",ammo==2&&player.Shells==1,"loaded shells="+player.Shells);
            Keys(Key.R);yield return null;Keys();
            float reloadStart=Time.time;bool reloadBegan=player.Weapon.IsReloading;bool sawFreshShells=false;float shellTravel=0;Vector3 lastShell=Vector3.zero;bool trackedShell=false;float maxReloadCameraStep=0;
            Vector3 previousEye=player.ViewCamera.transform.position;bool reloadPhoto=false;
            while(player.Weapon.IsReloading&&Time.time-reloadStart<1.5f)
            {
                yield return null;
                maxReloadCameraStep=Mathf.Max(maxReloadCameraStep,Vector3.Distance(previousEye,player.ViewCamera.transform.position));previousEye=player.ViewCamera.transform.position;
                var rounds=player.Weapon.ReloadShells;
                if(rounds!=null&&rounds.Length==2&&rounds[0].gameObject.activeInHierarchy)
                {
                    sawFreshShells=true;Vector3 local=rounds[0].localPosition;if(trackedShell)shellTravel+=Vector3.Distance(lastShell,local);lastShell=local;trackedShell=true;
                    if(!reloadPhoto&&player.Weapon.ReloadProgress>.52f){reloadPhoto=true;yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(output,"iron-pulse-v5-reload.png"));}
                }
            }
            Record("Fast break action reload",reloadBegan&&player.Shells==2&&Time.time-reloadStart<1.14f,"seconds="+(Time.time-reloadStart).ToString("F2")+", shells="+player.Shells);
            Record("Physical shell insertion",sawFreshShells&&shellTravel>.10f,"visible paired rounds="+sawFreshShells+", travel="+shellTravel.ToString("F3"));
            Record("Stable reload camera",maxReloadCameraStep<.08f,"maximum eye step="+maxReloadCameraStep.ToString("F3"));
            player.ResetAt(IPWorld.PlayerSpawn);yield return new WaitForSeconds(.4f);
            var fixture=new GameObject("Validation // stagger and execute").AddComponent<IPEnemy>();fixture.Initialize(IPEnemyKind.Creep,player.transform.position+player.transform.forward*2.7f,player);fixture.enabled=false;
            fixture.ReceiveShot(999,fixture.ExecutionPoint,player.transform.forward);
            Record("Temporary orange stagger",fixture.CanExecute&&!fixture.IsDead,"CanExecute="+fixture.CanExecute);
            player.TakeDamage(55,player.transform.position-player.transform.forward);float before=player.Health;int executions=game.Executions;
            player.BeginExecution(fixture);bool began=player.IsExecuting;float executionStart=Time.time;bool wasExecuting=began;Quaternion lastEyeRotation=player.ViewCamera.transform.rotation;float exitRotationStep=0;bool turnedVictim=false;float victimMotion=0;Vector3 initialHead=fixture.ExecutionHeadPoint;int executionPhoto=0;
            while(Time.time-executionStart<1.5f)
            {
                yield return null;
                if(wasExecuting&&!player.IsExecuting)exitRotationStep=Quaternion.Angle(lastEyeRotation,player.ViewCamera.transform.rotation);
                wasExecuting=player.IsExecuting;lastEyeRotation=player.ViewCamera.transform.rotation;
                if(fixture){victimMotion=Mathf.Max(victimMotion,Vector3.Distance(initialHead,fixture.ExecutionHeadPoint));if(fixture.ExecutionFacingProgress>.95f&&Vector3.Dot(fixture.transform.forward,Vector3.ProjectOnPlane(player.transform.position-fixture.transform.position,Vector3.up).normalized)>.8f)turnedVictim=true;}
                if(executionPhoto<6&&Time.time-executionStart>=.13f+executionPhoto*.18f){yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(output,"iron-pulse-v5-execution-"+(++executionPhoto).ToString("00")+".png"));}
            }
            Record("Glory kill + health",began&&!player.IsExecuting&&game.Executions==executions+1&&player.Health>=before+34,"health="+before+" -> "+player.Health+", executions="+game.Executions);
            Record("Smooth execution camera exit",exitRotationStep<4f,"exit rotation step="+exitRotationStep.ToString("F2")+" degrees");
            Record("Physical severed victim sections",FindObjectsByType<Transform>(FindObjectsSortMode.None).Length>0&&HasSeveredParts(),"severed limb/body groups present");
            Record("Victim turns toward player",turnedVictim,"grab turned the rear-facing victim");
            Record("Victim reacts during finisher",victimMotion>.08f,"head displacement="+victimMotion.ToString("F3"));
            player.ResetAt(IPWorld.PlayerSpawn);yield return new WaitForSeconds(.6f);float beforeBolt=player.Health;
            IPProjectile.Fire(player,null,player.ViewCamera.transform.position+player.transform.forward*3,-player.transform.forward*18,11,new Color(1,.5f,.05f));yield return new WaitForSeconds(.4f);
            Record("Enemy projectile damage",player.Health<beforeBolt,"health="+beforeBolt+" -> "+player.Health);
            int expectedHeight=Mathf.Clamp(Mathf.Min(Screen.height,1080),360,1440);
            Record("Smooth retro framebuffer",player.ViewCamera.targetTexture&&player.ViewCamera.targetTexture.filterMode==FilterMode.Bilinear&&player.ViewCamera.targetTexture.height==expectedHeight,"target="+(player.ViewCamera.targetTexture?player.ViewCamera.targetTexture.width+"x"+player.ViewCamera.targetTexture.height:"none"));
            var hd=player.ViewCamera.GetComponent<UnityEngine.Rendering.HighDefinition.HDAdditionalCameraData>();
            Record("SMAA smoothing",hd&&hd.antialiasing==UnityEngine.Rendering.HighDefinition.HDAdditionalCameraData.AntialiasingMode.SubpixelMorphologicalAntiAliasing,"antialiasing="+(hd?hd.antialiasing.ToString():"missing"));
            CheckExecutionSelection();
            player.ResetAt(IPWorld.PlayerSpawn);yield return new WaitForSeconds(.4f);
            Keys(Key.Digit1);yield return new WaitForSeconds(.45f);Keys();yield return null;
            Record("Pistol slot",player.Weapon.CurrentWeapon==IPWeaponKind.Pistol&&player.Weapon.Capacity==12,"kind="+player.Weapon.CurrentWeapon+", capacity="+player.Weapon.Capacity);
            int pistolShots=game.ShotsFired;InputSystem.QueueStateEvent(mouse,new MouseState{buttons=1});yield return new WaitForSeconds(.48f);InputSystem.QueueStateEvent(mouse,new MouseState());yield return new WaitForSeconds(.12f);
            Record("Pistol semiautomatic",game.ShotsFired-pistolShots==1,"held trigger fired "+(game.ShotsFired-pistolShots)+" round(s)");
            yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(output,"iron-pulse-v5-pistol.png"));
            Keys(Key.R);yield return new WaitForSeconds(.12f);Keys();
            Record("Pistol unlimited, reload removed",player.Shells==12&&!player.Weapon.UsesReload&&!player.Weapon.IsReloading,"loaded="+player.Shells+", reload="+player.Weapon.IsReloading);
            Keys(Key.Digit2);yield return new WaitForSeconds(.45f);Keys();yield return null;
            Record("Rifle slot",player.Weapon.CurrentWeapon==IPWeaponKind.Rifle&&player.Weapon.Capacity==30,"kind="+player.Weapon.CurrentWeapon+", capacity="+player.Weapon.Capacity);
            int rifleShots=game.ShotsFired;InputSystem.QueueStateEvent(mouse,new MouseState{buttons=1});yield return new WaitForSeconds(3.4f);InputSystem.QueueStateEvent(mouse,new MouseState());yield return new WaitForSeconds(.15f);
            Record("Rifle sustained unlimited automatic fire",game.ShotsFired-rifleShots>=32&&player.Shells==30,"burst fired "+(game.ShotsFired-rifleShots)+" rounds, reserve="+player.Shells);
            yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(output,"iron-pulse-v5-rifle.png"));
            Keys(Key.Digit3);yield return new WaitForSeconds(.45f);Keys();yield return null;
            Record("Shotgun slot",player.Weapon.CurrentWeapon==IPWeaponKind.Shotgun&&player.Weapon.Capacity==2,"kind="+player.Weapon.CurrentWeapon);
            Keys(Key.Digit2);yield return new WaitForSeconds(.4f);Keys();yield return null;
            Record("Unlimited weapon switching",player.Shells==30&&!player.Weapon.UsesReload,"rifle available="+player.Shells);
            Keys(Key.R);yield return new WaitForSeconds(.12f);Keys();
            Record("Rifle reload removed",!player.Weapon.IsReloading,"reload="+player.Weapon.IsReloading);
            Keys(Key.Digit3);yield return new WaitForSeconds(.4f);Keys();yield return null;
            yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(output,"iron-pulse-v5-shotgun.png"));
            player.transform.rotation=Quaternion.identity;player.ResetAt(IPWorld.PlayerSpawn);yield return new WaitForSeconds(.4f);
            var dashTarget=Fixture(IPEnemyKind.Melee,player.transform.position+Vector3.forward*3.7f);
            player.TakeDamage(50,player.transform.position-Vector3.forward);float dashBefore=player.Health;
            Keys(Key.W,Key.LeftShift);yield return new WaitForSeconds(.045f);Keys(Key.W,Key.LeftShift,Key.E);yield return new WaitForSeconds(.055f);Keys();
            bool dashBegan=player.IsExecuting;yield return new WaitForSeconds(1.55f);
            Record("E during directional dash",dashBegan&&player.LastExecutionStyle==IPExecutionStyle.DashThrust&&player.Health>=dashBefore+34,"style="+player.LastExecutionStyle+", health="+player.Health);
            if(dashTarget&&!dashTarget.IsDead)Destroy(dashTarget.gameObject);
            player.transform.rotation=Quaternion.identity;player.ResetAt(IPWorld.PlayerSpawn+Vector3.up*3.7f);
            var aerialTarget=Fixture(IPEnemyKind.Ranged,new Vector3(0,.08f,-16.5f));
            player.TakeDamage(50,player.transform.position-Vector3.forward);float aerialBefore=player.Health;
            InputSystem.QueueStateEvent(mouse,new MouseState{delta=new Vector2(0,-520)});yield return new WaitForSeconds(.24f);
            Keys(Key.E);yield return new WaitForSeconds(.05f);Keys();bool aerialBegan=player.IsExecuting;
            yield return new WaitForSeconds(.43f);yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(output,"iron-pulse-v5-katana.png"));
            yield return new WaitForSeconds(1.35f);
            Record("E while falling from above",aerialBegan&&player.LastExecutionStyle==IPExecutionStyle.AerialPlunge&&player.Health>=aerialBefore+34,"style="+player.LastExecutionStyle+", health="+player.Health);
            if(aerialTarget&&!aerialTarget.IsDead)Destroy(aerialTarget.gameObject);
            yield return CheckExtraFinishers();
            yield return CheckSpecialCombat();
            IPAudio.Warmup();
            var recordedClips=Resources.LoadAll<AudioClip>("Audio/IronPulse");bool recordedQuality=recordedClips.Length>=20;
            foreach(var clip in recordedClips)if(clip.frequency<44100||clip.samples<100)recordedQuality=false;
            Record("Recorded high quality audio library",recordedQuality&&IPAudio.MissingBankCount==0,"clips="+recordedClips.Length+", missing banks="+IPAudio.MissingBankCount);
            bool loopingMusic=false;foreach(var source in FindObjectsByType<AudioSource>(FindObjectsSortMode.None))if(source.loop&&source.isPlaying)loopingMusic=true;
            Record("Background music removed",!loopingMusic,"looping sources="+loopingMusic);
            int lit=0,shadowed=0;foreach(var lamp in FindObjectsByType<Light>(FindObjectsSortMode.None)){if(lamp.enabled){lit++;if(lamp.shadows!=LightShadows.None)shadowed++;}}
            Record("Real HDRP lighting and shadows",lit>=12&&shadowed>=4,"enabled lights="+lit+", shadow casters="+shadowed);
            var ground=GameObject.Find("World");int pbr=0;if(ground)foreach(var renderer in ground.GetComponentsInChildren<Renderer>())if(renderer.sharedMaterial&&renderer.sharedMaterial.shader.name=="HDRP/Lit"&&renderer.receiveShadows)pbr++;
            Record("PBR arena surfaces",pbr>400,"lit, shadow receiving renderers="+pbr);
            foreach(var debris in FindObjectsByType<IPMechanicalDebris>(FindObjectsSortMode.None))Destroy(debris.gameObject);
            foreach(var enemy in FindObjectsByType<IPEnemy>(FindObjectsSortMode.None))Destroy(enemy.gameObject);
            player.transform.rotation=Quaternion.identity;player.ResetAt(IPWorld.PlayerSpawn);yield return new WaitForSeconds(.4f);
            for(int i=0;i<3;i++)
            {
                var display=new GameObject("Validation // enemy showcase").AddComponent<IPEnemy>();
                display.Initialize((IPEnemyKind)i,player.transform.position+new Vector3((i-1)*2.3f,0,4.3f+Mathf.Abs(i-1)*1.7f),player);
                display.transform.rotation=Quaternion.LookRotation(Vector3.back);display.enabled=false;
            }
            yield return new WaitForSeconds(2.6f);yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(output,"iron-pulse-v5-enemies.png"));
            Record("No runtime errors during live checks",runtimeErrors==0,runtimeErrors==0?"No errors, exceptions or assertions":runtimeErrors+" errors: "+firstRuntimeError);
            Application.logMessageReceived-=TrackRuntimeErrors;
            RestoreDevices();
            var report=new Report{unity=Application.unityVersion,allPassed=checks.TrueForAll(e=>e.passed),averageFrameMilliseconds=frameSamples>0?frameSeconds/frameSamples*1000:0,sampledFrames=frameSamples,checks=checks.ToArray()};
            Directory.CreateDirectory(output);File.WriteAllText(Path.Combine(output,"iron-pulse-v5-validation.json"),JsonUtility.ToJson(report,true));
            game.ReturnToTitle();yield return null;yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(output,"iron-pulse-v5-title.png"));
            Debug.Log("IRON PULSE validation complete: "+report.allPassed+". Report: "+output);
            Destroy(this);
        }
        IEnumerator CheckExtraFinishers()
        {
            foreach(var expected in new[]{IPExecutionStyle.Frontal,IPExecutionStyle.StrafeLeft,IPExecutionStyle.StrafeRight,IPExecutionStyle.RisingCut,IPExecutionStyle.RearCut,IPExecutionStyle.BackstepCrush,IPExecutionStyle.TwinArmBreak})
            {
                Keys();player.transform.rotation=Quaternion.identity;player.ResetAt(IPWorld.PlayerSpawn);yield return new WaitForSeconds(.4f);
                var target=Fixture(IPEnemyKind.Melee,player.transform.position+Vector3.forward*(expected==IPExecutionStyle.TwinArmBreak?1.5f:3f));
                if(expected==IPExecutionStyle.RearCut)target.transform.rotation=Quaternion.identity;
                player.TakeDamage(50,player.transform.position-Vector3.forward);int beforeCount=game.Executions;
                if(expected==IPExecutionStyle.StrafeLeft){Keys(Key.A);yield return new WaitForSeconds(.12f);}
                if(expected==IPExecutionStyle.StrafeRight){Keys(Key.D);yield return new WaitForSeconds(.12f);}
                if(expected==IPExecutionStyle.RisingCut){Keys(Key.Space);yield return new WaitForSeconds(.12f);}
                if(expected==IPExecutionStyle.BackstepCrush){Keys(Key.S);yield return new WaitForSeconds(.12f);}
                var knownDebris=new HashSet<int>();foreach(var part in FindObjectsByType<IPMechanicalDebris>(FindObjectsSortMode.None))knownDebris.Add(part.GetInstanceID());
                player.BeginExecution(target);bool began=player.IsExecuting;Keys();
                bool lethal=false;float beganAt=Time.time;int photos=0;bool novel=expected==IPExecutionStyle.BackstepCrush||expected==IPExecutionStyle.TwinArmBreak;int maxParts=0;bool armsBeforeLethal=false,cappedSections=false;
                while(Time.time-beganAt<1.7f)
                {
                    yield return null;if(target&&target.IsDead)lethal=true;
                    if(novel)
                    {
                        int parts=0;foreach(var part in FindObjectsByType<IPMechanicalDebris>(FindObjectsSortMode.None))if(!knownDebris.Contains(part.GetInstanceID()))
                        {
                            parts++;foreach(var filter in part.GetComponentsInChildren<MeshFilter>())if(filter.sharedMesh&&filter.sharedMesh.subMeshCount>1)cappedSections=true;
                        }
                        maxParts=Mathf.Max(maxParts,parts);if(parts>=2&&target&&!target.IsDead)armsBeforeLethal=true;
                    }
                    if(novel&&photos<3&&Time.time-beganAt>=.28f+photos*.30f){yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(output,"iron-pulse-v5-"+expected.ToString().ToLowerInvariant()+"-"+(++photos)+".png"));}
                }
                Record("Live finisher / "+expected,began&&lethal&&!player.IsExecuting&&player.LastExecutionStyle==expected&&game.Executions==beforeCount+1&&player.Health>=84f,"style="+player.LastExecutionStyle+", lethal="+lethal+", health="+player.Health+", executions="+(game.Executions-beforeCount));
                if(expected==IPExecutionStyle.BackstepCrush)Record("Backstep sweep and capped skull fracture",maxParts>=5&&cappedSections,"physical sections="+maxParts+", capped cuts="+cappedSections);
                if(expected==IPExecutionStyle.TwinArmBreak)Record("Twin arm breaks precede lethal neck cut",armsBeforeLethal&&maxParts>=4,"nonlethal arm breaks="+armsBeforeLethal+", physical sections="+maxParts);
                if(target)Destroy(target.gameObject);
            }
        }
        IEnumerator CheckDoubleDash()
        {
            Keys();player.transform.rotation=Quaternion.identity;player.ResetAt(IPWorld.PlayerSpawn);yield return new WaitForSeconds(.4f);
            Vector3 origin=player.transform.position;
            Keys(Key.D,Key.LeftShift);yield return new WaitForSeconds(.11f);Keys();yield return new WaitForSeconds(.07f);
            bool first=player.DashesPerformed==1&&player.DashCharges==1;float east=player.transform.position.x-origin.x;Vector3 turn=player.transform.position;
            Keys(Key.A,Key.LeftShift);yield return new WaitForSeconds(.11f);Keys();yield return new WaitForSeconds(.02f);
            float west=turn.x-player.transform.position.x;
            Record("Two consecutive directional dashes",first&&player.DashesPerformed==2&&player.DashCharges==0&&east>2f&&west>2f,"accepted="+player.DashesPerformed+", charges="+player.DashCharges+", right="+east.ToString("F2")+", left="+west.ToString("F2"));
            Keys(Key.LeftShift);yield return new WaitForSeconds(.035f);
            Record("Third dash blocked while empty",player.DashesPerformed==2&&player.DashCharges==0,"accepted="+player.DashesPerformed+", charges="+player.DashCharges);
            yield return new WaitForSeconds(.50f);
            Record("Dash charges recharge serially",player.DashCharges==1&&player.DashRechargeProgress01>0f&&player.DashRechargeProgress01<1f,"charges="+player.DashCharges+", progress="+player.DashRechargeProgress01.ToString("F2"));
            Record("Holding Shift does not repeat dash",player.DashesPerformed==2,"accepted="+player.DashesPerformed);
            Keys();yield return null;Keys(Key.W,Key.LeftShift);yield return new WaitForSeconds(.10f);Keys();
            Record("Recharged dash usable immediately",player.DashesPerformed==3&&player.DashCharges==0,"accepted="+player.DashesPerformed+", charges="+player.DashCharges);
            yield return new WaitForSeconds(1.55f);
            Record("Both dash charges fully recover",player.DashCharges==2&&Mathf.Approximately(player.DashRechargeProgress01,1f),"charges="+player.DashCharges);
            player.ResetAt(IPWorld.PlayerSpawn+Vector3.up*3.5f);yield return null;
            Keys(Key.W,Key.LeftShift);yield return new WaitForSeconds(.08f);Keys();yield return new WaitForSeconds(.04f);
            Keys(Key.D,Key.LeftShift);yield return new WaitForSeconds(.08f);Keys();
            Record("Double dash in air preserves jumps",player.DashesPerformed==2&&player.DashCharges==0&&player.JumpsRemaining==2&&player.transform.position.y>2f,"charges="+player.DashCharges+", jumps="+player.JumpsRemaining+", height="+player.transform.position.y.ToString("F2"));
            player.ResetAt(IPWorld.PlayerSpawn);bool spent=player.TryDash(Vector3.right);game.SetPause(true);bool pausedAttempt=player.TryDash(Vector3.left);
            yield return new WaitForSecondsRealtime(.82f);
            Record("Pause freezes dash recharge",spent&&!pausedAttempt&&player.DashCharges==1&&player.DashCooldown01>.8f,"charges="+player.DashCharges+", remaining="+player.DashCooldown01.ToString("F2"));
            game.SetPause(false);player.ResetAt(IPWorld.PlayerSpawn);
            Record("Restart restores both dash charges",player.DashCharges==2&&player.DashesPerformed==0,"charges="+player.DashCharges);
        }
        IEnumerator CheckSpecialCombat()
        {
            Keys();player.transform.rotation=Quaternion.identity;player.ResetAt(IPWorld.PlayerSpawn);yield return new WaitForSeconds(.4f);
            var punchTarget=FreshEnemy(IPEnemyKind.Melee,player.transform.position+Vector3.forward*1.7f);float hp=punchTarget.Health;
            int shots=game.ShotsFired;var selected=player.Weapon.CurrentWeapon;
            Keys(Key.F);yield return new WaitForSeconds(.08f);Keys();bool punchBegan=player.IsPunching&&player.Weapon.IsMeleeAttacking;
            yield return new WaitForSeconds(.13f);yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(output,"iron-pulse-v5-punch.png"));
            InputSystem.QueueStateEvent(mouse,new MouseState{buttons=1});Keys(Key.Digit2,Key.R);yield return new WaitForSeconds(.12f);
            bool weaponBlocked=game.ShotsFired==shots&&!player.Weapon.IsReloading&&player.Weapon.CurrentWeapon==selected;
            InputSystem.QueueStateEvent(mouse,new MouseState());Keys();yield return new WaitForSeconds(.45f);
            Record("F is left fist without charge",punchBegan&&player.PunchesThrown==1&&player.MeleeHits==1&&punchTarget.Health<hp&&!punchTarget.IsDead,"damage="+(hp-punchTarget.Health)+", hits="+player.MeleeHits);
            Record("Weapon lowered and blocked during punch",weaponBlocked&&!player.IsSpecialAttacking&&!player.Weapon.IsMeleeAttacking,"shots="+(game.ShotsFired-shots)+", restored="+!player.Weapon.IsMeleeAttacking);
            Record("Melee builds ultimate charge",player.UltimateCharge>=6f&&player.UltimateCharge<100f,"charge="+player.UltimateCharge);
            Destroy(punchTarget.gameObject);
            player.transform.rotation=Quaternion.identity;player.ResetAt(IPWorld.PlayerSpawn);yield return new WaitForSeconds(.4f);
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.name="Validation wall";wall.transform.position=player.transform.position+new Vector3(0,1.3f,1f);wall.transform.localScale=new Vector3(3,3,.18f);
            var blocked=FreshEnemy(IPEnemyKind.Melee,player.transform.position+Vector3.forward*1.9f);float blockedHp=blocked.Health;Physics.SyncTransforms();
            Keys(Key.F);yield return new WaitForSeconds(.08f);Keys();yield return new WaitForSeconds(.65f);
            Record("F punch respects walls",blocked.Health==blockedHp&&player.MeleeHits==0,"blocked health="+blocked.Health);
            Destroy(wall);Destroy(blocked.gameObject);
            player.transform.rotation=Quaternion.identity;player.ResetAt(IPWorld.PlayerSpawn);yield return new WaitForSeconds(.4f);
            var crowd=new List<IPEnemy>();for(int i=0;i<3;i++)crowd.Add(FreshEnemy((IPEnemyKind)i,player.transform.position+new Vector3((i-1)*1.25f,0,3.1f+(i==1?.5f:0))));
            var shield=GameObject.CreatePrimitive(PrimitiveType.Cube);shield.name="Validation ultimate barrier";shield.transform.position=player.transform.position+new Vector3(0,1.5f,5f);shield.transform.localScale=new Vector3(3.8f,4,.25f);
            var protectedEnemy=FreshEnemy(IPEnemyKind.Ranged,player.transform.position+Vector3.forward*6f);float protectedHp=protectedEnemy.Health;Physics.SyncTransforms();
            player.AddUltimateCharge(100f);Record("Ultimate charge reaches ready",player.UltimateReady,"charge="+player.UltimateCharge);
            Keys(Key.F);yield return new WaitForSeconds(.08f);Keys();bool ultBegan=player.IsUltimateActive&&player.UltimateCharge==0f;
            player.TakeDamage(20,player.transform.position-Vector3.forward);float ultHp=player.Health;player.AddUltimateCharge(100);
            InputSystem.QueueStateEvent(mouse,new MouseState{buttons=1});Keys(Key.Digit1,Key.R);int beforeShot=game.ShotsFired;yield return new WaitForSeconds(.2f);
            bool ultBlocked=game.ShotsFired==beforeShot&&player.Weapon.CurrentWeapon==selected&&!player.Weapon.IsReloading;
            InputSystem.QueueStateEvent(mouse,new MouseState());Keys();yield return new WaitForSeconds(.39f);yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(output,"iron-pulse-v5-ultimate.png"));
            float maxFov=player.ViewCamera.fieldOfView;float started=Time.time;
            while(player.IsUltimateActive&&Time.time-started<2f){yield return null;maxFov=Mathf.Max(maxFov,player.ViewCamera.fieldOfView);}
            int dead=0;foreach(var target in crowd)if(target.IsDead)dead++;
            Record("F charged ultimate cuts the crowd",ultBegan&&dead==3&&player.UltimateKills>=3&&player.UltimateActivations==1,"crowd killed="+dead+", total="+player.UltimateKills);
            Record("Ultimate consumes charge without self refill",player.UltimateCharge==0f,"charge="+player.UltimateCharge);
            Record("Ultimate obeys solid barriers",!protectedEnemy.IsDead&&protectedEnemy.Health==protectedHp,"protected health="+protectedEnemy.Health);
            Record("Ultimate blocks guns and protects player",ultBlocked&&ultHp==100f,"blocked="+ultBlocked+", health="+ultHp);
            Record("Ultimate restores weapon and bounded camera",!player.IsSpecialAttacking&&!player.Weapon.IsMeleeAttacking&&maxFov<115f,"max fov="+maxFov.ToString("F2"));
            Destroy(shield);if(protectedEnemy)Destroy(protectedEnemy.gameObject);foreach(var target in crowd)if(target)Destroy(target.gameObject);
            yield return new WaitForSeconds(.25f);Keys(Key.F);yield return new WaitForSeconds(.08f);Keys();bool fallback=player.IsPunching&&!player.IsUltimateActive;yield return new WaitForSeconds(.65f);
            Record("F returns to punch after ultimate",fallback,"fallback punch="+fallback);
        }
        IPEnemy FreshEnemy(IPEnemyKind kind,Vector3 position)
        {
            var enemy=new GameObject("Validation // special combat").AddComponent<IPEnemy>();enemy.Initialize(kind,position,player);enemy.enabled=false;
            enemy.transform.rotation=Quaternion.LookRotation(Vector3.back);Physics.SyncTransforms();return enemy;
        }
        bool HasSeveredParts()
        {
            foreach(var item in FindObjectsByType<IPMechanicalDebris>(FindObjectsSortMode.None))
                if(item.GetComponentsInChildren<MeshFilter>().Length>=3)return true;
            return false;
        }
        IPEnemy Fixture(IPEnemyKind kind,Vector3 position)
        {
            var e=new GameObject("Validation // katana "+kind).AddComponent<IPEnemy>();e.Initialize(kind,position,player);e.enabled=false;
            e.transform.rotation=Quaternion.LookRotation(Vector3.back);e.ReceiveShot(999,e.ExecutionPoint,Vector3.forward);Physics.SyncTransforms();return e;
        }
        void CheckExecutionSelection()
        {
            CheckStyle(IPExecutionStyle.Frontal,new Vector3(0,0,-2),Vector3.zero,Vector3.back,false);
            CheckStyle(IPExecutionStyle.StrafeLeft,new Vector3(0,0,-2),new Vector3(-6,0,0),Vector3.back,false);
            CheckStyle(IPExecutionStyle.StrafeRight,new Vector3(0,0,-2),new Vector3(6,0,0),Vector3.back,false);
            CheckStyle(IPExecutionStyle.DashThrust,new Vector3(0,0,-2),Vector3.forward*25,Vector3.back,true);
            CheckStyle(IPExecutionStyle.RisingCut,new Vector3(0,.7f,-2),Vector3.up*6,Vector3.back,false);
            CheckStyle(IPExecutionStyle.AerialPlunge,new Vector3(0,3,-1),Vector3.down*9,Vector3.back,false);
            CheckStyle(IPExecutionStyle.RearCut,new Vector3(0,0,2),Vector3.zero,Vector3.back,false);
            CheckStyle(IPExecutionStyle.BackstepCrush,new Vector3(0,0,-2),Vector3.back*8,Vector3.back,false);
            CheckStyle(IPExecutionStyle.TwinArmBreak,new Vector3(0,0,-1.5f),Vector3.zero,Vector3.back,false);
        }
        void CheckStyle(IPExecutionStyle expected,Vector3 position,Vector3 velocity,Vector3 targetForward,bool dashing)
        {
            var actual=IPPlayer.ClassifyExecution(position,velocity,Vector3.right,Vector3.forward,Vector3.zero,targetForward,dashing);
            Record("Katana selection / "+expected,actual==expected,"selected="+actual);
        }
        void RestoreDevices()
        {
            InputSystem.onAfterUpdate-=PinDevices;
            if(keyboard!=null){InputSystem.RemoveDevice(keyboard);keyboard=null;}if(mouse!=null){InputSystem.RemoveDevice(mouse);mouse=null;}
            if(previousKeyboard!=null&&previousKeyboard.added)previousKeyboard.MakeCurrent();if(previousMouse!=null&&previousMouse.added)previousMouse.MakeCurrent();
            InputSystem.settings.backgroundBehavior=oldBackground;
#if UNITY_EDITOR
            InputSystem.settings.editorInputBehaviorInPlayMode=oldEditorInput;
#endif
        }
        void PinDevices()
        {
            if(keyboard!=null&&keyboard.added)keyboard.MakeCurrent();
            if(mouse!=null&&mouse.added)mouse.MakeCurrent();
        }
        void Update()
        {
            if(keyboard==null||mouse==null)return;
            frameSeconds+=Time.unscaledDeltaTime;frameSamples++;
            PinDevices();
            if(game&&!IPGame.IsPaused)Cursor.lockState=CursorLockMode.Locked;
        }
        void OnDestroy(){Application.logMessageReceived-=TrackRuntimeErrors;if(keyboard!=null||mouse!=null)RestoreDevices();}
    }
}
