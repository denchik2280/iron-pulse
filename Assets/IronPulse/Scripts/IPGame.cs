using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace IronPulse
{
    [DefaultExecutionOrder(-100)]
    public class IPGame : MonoBehaviour
    {
        public static IPGame Instance { get; private set; }
        public static bool IsPaused { get; private set; }
        public IPPlayer Player { get; private set; }
        public int Wave { get; private set; }
        public int Kills { get; private set; }
        public int Executions { get; private set; }
        public int Score { get; private set; }
        public bool HasBegun { get; private set; }
        public bool Muted { get; private set; }
        public int Combo { get; private set; }
        public int ShotsFired { get; private set; }
        public IPWeaponKind LastShotKind { get; private set; }
        float shotFlash,reticleImpulse;
        float nextWaveAt=-1, elapsed, comboAt, labelUntil, hitUntil, hurtLastHealth=100, damageFade;
        string banner="", styleAction="SYSTEM READY";
        Color bannerColor=Color.white;
        RenderTexture pixels;
        int pixelHeight=1080;
        Font font;
        GUIStyle text, big, small;
        Texture2D scanlines;
        readonly Color orange=new Color(1,.33f,.09f), cyan=new Color(.22f,.72f,.66f), ink=new Color(.025f,.03f,.025f);

        void Awake()
        {
            Instance=this; Time.timeScale=1; IsPaused=false;
            Application.targetFrameRate=120; Application.runInBackground=true; QualitySettings.vSyncCount=0;
            if(!transform.Find("World"))
            { var world=new GameObject("World"); world.transform.SetParent(transform); IPWorld.Build(world.transform); }
            IPWorld.AddDetail(transform.Find("World"));
            ConfigureAtmosphere();
            IPEnvironment.Configure();
            IPAudio.Warmup();
            var player=new GameObject("Player // R-09"); Player=player.AddComponent<IPPlayer>(); Player.Initialize(IPWorld.PlayerSpawn);
            ConfigureCamera(Player.ViewCamera);
            foreach(var camera in FindObjectsByType<Camera>(FindObjectsSortMode.None))
                if(camera!=Player.ViewCamera&&camera.gameObject.scene==gameObject.scene)camera.enabled=false;
            foreach(var listener in FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
                if(listener.gameObject!=Player.ViewCamera.gameObject) listener.enabled=false;
            if(!Player.ViewCamera.GetComponent<AudioListener>())Player.ViewCamera.gameObject.AddComponent<AudioListener>();
            SpawnWave();
            SetPause(true);
        }

        public static void ConfigureCamera(Camera camera)
        {
            camera.clearFlags=CameraClearFlags.Skybox; camera.backgroundColor=new Color(.025f,.045f,.08f);
            camera.nearClipPlane=.035f; camera.farClipPlane=180;
            var hd=camera.GetComponent<HDAdditionalCameraData>();if(!hd)hd=camera.gameObject.AddComponent<HDAdditionalCameraData>();
            hd.clearColorMode=HDAdditionalCameraData.ClearColorMode.Sky;
            hd.backgroundColorHDR=new Color(.025f,.045f,.08f,1);
            hd.antialiasing=HDAdditionalCameraData.AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            hd.SMAAQuality=HDAdditionalCameraData.SMAAQualityLevel.High;
            hd.dithering=false;
        }
        public static void ConfigureAtmosphere()
        {
            var go=GameObject.Find("IronPulse Atmosphere")??new GameObject("IronPulse Atmosphere");
            var v=go.GetComponent<Volume>();if(!v)v=go.AddComponent<Volume>();v.isGlobal=true;v.priority=100;
            if(!v.sharedProfile)v.sharedProfile=ScriptableObject.CreateInstance<VolumeProfile>();
            var profile=Application.isPlaying?v.profile:v.sharedProfile;
            if(!profile.TryGet<Exposure>(out var exposure))exposure=profile.Add<Exposure>(true);
            exposure.mode.Override(ExposureMode.Fixed);exposure.fixedExposure.Override(0f);
            if(!profile.TryGet<Tonemapping>(out var tone))tone=profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.ACES);
            if(!profile.TryGet<Fog>(out var fog))fog=profile.Add<Fog>(true);fog.enabled.Override(false);
            if(!profile.TryGet<MotionBlur>(out var motion))motion=profile.Add<MotionBlur>(true);motion.intensity.Override(0);
            RenderSettings.fog=false; RenderSettings.ambientLight=new Color(.25f,.3f,.4f);
            IPEnvironment.ConfigureVolume(profile);
        }

        void Update()
        {
            var k=Keyboard.current; var mouse=Mouse.current;
            shotFlash=Mathf.MoveTowards(shotFlash,0,Time.unscaledDeltaTime*12);
            reticleImpulse=Mathf.Lerp(reticleImpulse,0,1-Mathf.Exp(-Time.unscaledDeltaTime*15));
            if(k!=null && k.mKey.wasPressedThisFrame) { Muted=!Muted; AudioListener.volume=Muted?0:1; }
            if(k!=null && k.f2Key.wasPressedThisFrame) { pixelHeight=pixelHeight==1080?1440:pixelHeight==1440?720:1080; RebuildPixels(); }
            if(!HasBegun)
            {
                if((k!=null&&k.enterKey.wasPressedThisFrame)||(mouse!=null&&mouse.leftButton.wasPressedThisFrame))
                { BeginRun(); }
                return;
            }
            if(Player.IsDead)
            { if(k!=null&&(k.rKey.wasPressedThisFrame||k.enterKey.wasPressedThisFrame))Restart(); return; }
            if(k!=null&&k.escapeKey.wasPressedThisFrame)SetPause(!IsPaused);
            if(IsPaused)
            { if(k!=null&&k.enterKey.wasPressedThisFrame)SetPause(false); return; }
            elapsed+=Time.deltaTime;
            if(Combo>0&&Time.time-comboAt>5f) {Combo=0;styleAction="KEEP MOVING";}
            if(nextWaveAt>0 && Time.time>=nextWaveAt) { nextWaveAt=-1;SpawnWave(); }
            if(Player.Health<hurtLastHealth)damageFade=.5f;
            hurtLastHealth=Player.Health; damageFade=Mathf.MoveTowards(damageFade,0,Time.deltaTime);
            if(Player.transform.position.y<-12) { Player.TakeDamage(25,Player.transform.position-Vector3.up); Player.ResetAt(IPWorld.PlayerSpawn); }
            if(!pixels||pixels.height!=TargetHeight||pixels.width!=TargetWidth)RebuildPixels();
        }
        int TargetHeight=>Mathf.Clamp(Mathf.Min(Screen.height,pixelHeight),360,1440);
        int TargetWidth=>Mathf.Clamp(Mathf.RoundToInt(TargetHeight*(float)Screen.width/Mathf.Max(1,Screen.height)),480,3440);
        void RebuildPixels()
        {
            if(pixels){Player.ViewCamera.targetTexture=null;pixels.Release();Destroy(pixels);}
            pixels=new RenderTexture(TargetWidth,TargetHeight,24,RenderTextureFormat.ARGB32){name="IRON PULSE // smooth retro framebuffer",filterMode=FilterMode.Bilinear,antiAliasing=1};
            pixels.Create(); Player.ViewCamera.targetTexture=pixels;
        }
        void LateUpdate(){ if(Player&&Player.ViewCamera&&!pixels)RebuildPixels(); }
        public void SetPause(bool paused)
        { IsPaused=paused; Time.timeScale=paused?0:1; Cursor.lockState=paused?CursorLockMode.None:CursorLockMode.Locked; Cursor.visible=paused; }
        public void BeginRun(){HasBegun=true;SetPause(false);Label("PURGE THE REACTOR",orange);}
        public void ReturnToTitle(){Restart();HasBegun=false;SetPause(true);}
        void SpawnWave()
        {
            Wave++; int count=Mathf.Min(6+Wave*2,18); var spawns=IPWorld.EnemySpawns;
            for(int i=0;i<count;i++)
            {
                var kind=i%4==3?IPEnemyKind.Ranged:i%3==2?IPEnemyKind.Melee:IPEnemyKind.Creep;
                if(Wave>2&&i%4==0)kind=IPEnemyKind.Ranged;
                Vector3 p=spawns[i%spawns.Length]; if(i>=spawns.Length)p+=new Vector3((i%2)*2,0,2);
                var enemy=new GameObject(kind+" // "+(i+1)); enemy.AddComponent<IPEnemy>().Initialize(kind,p,Player,Wave);
            }
            Label("WAVE "+Wave.ToString("00")+"  //  INCOMING",orange);
            Audio("wave",Player.transform.position,.6f);
        }
        public void EnemyKilled(IPEnemy enemy,bool executed)
        {
            Kills++; Combo++; comboAt=Time.time; if(executed)Executions++;
            if(Player)Player.AddUltimateCharge(executed?24f:12f);
            Score+=(executed?500:100)*Mathf.Clamp(Combo,1,8);
            styleAction=executed?"+ EXECUTION / HEALTH RESTORED":"+ " +(enemy.Kind==IPEnemyKind.Ranged?"GUNNER DOWN":enemy.Kind==IPEnemyKind.Melee?"SLASHER DOWN":"ANDROID DOWN");
            Hit(true);
            int live=0; foreach(var e in FindObjectsByType<IPEnemy>(FindObjectsSortMode.None))if(e&&!e.IsDead&&e!=enemy)live++;
            if(live==0) { nextWaveAt=Time.time+4; Label("SECTOR CLEAR  //  NEXT WAVE",cyan); Player.Heal(12); }
        }
        public void Restart()
        {
            foreach(var e in FindObjectsByType<IPEnemy>(FindObjectsSortMode.None))Destroy(e.gameObject);
            foreach(var p in FindObjectsByType<IPProjectile>(FindObjectsSortMode.None))Destroy(p.gameObject);
            Player.ResetAt(IPWorld.PlayerSpawn); Player.Heal(100); Wave=Kills=Executions=Score=Combo=ShotsFired=0; elapsed=0; nextWaveAt=-1;
            SpawnWave();HasBegun=true;SetPause(false);
        }
        public static void Label(string message,Color color)
        { if(!Instance)return;Instance.banner=message;Instance.bannerColor=color;Instance.labelUntil=Time.unscaledTime+2.4f; }
        public static void Hit(bool kill=false)
        { if(!Instance)return;Instance.hitUntil=Time.unscaledTime+(kill ? .18f : .09f); }
        public static void ShotFeedback(IPWeaponKind kind)
        {
            if(!Instance)return;
            Instance.ShotsFired++;Instance.LastShotKind=kind;
            Instance.shotFlash=kind==IPWeaponKind.Shotgun?1f:kind==IPWeaponKind.Pistol?.55f:.3f;
            Instance.reticleImpulse=Mathf.Min(18,Instance.reticleImpulse+(kind==IPWeaponKind.Shotgun?15:kind==IPWeaponKind.Pistol?7:4));
        }
        public static void Burst(Vector3 position,Color color,int count,float speed=6f)
        {
            var mat=IPArt.Mat("Debris",color,1f);
            for(int i=0;i<Mathf.Min(count,40);i++)
            {
                var p=IPArt.Box("Pixel shrapnel",null,position,Vector3.one*UnityEngine.Random.Range(.035f,.13f),mat);
                p.AddComponent<IPFragment>().Launch(UnityEngine.Random.onUnitSphere*UnityEngine.Random.Range(speed*.3f,speed)+Vector3.up*2,UnityEngine.Random.Range(.25f,.8f));
            }
        }

        public static void Audio(string cue,Vector3 position,float volume=1f)
        {
            if(!Instance||Instance.Muted)return;
            IPAudio.Play(cue,position,volume);
        }

        void SetupGUI()
        {
            if(text!=null)return;
            font=Font.CreateDynamicFontFromOSFont(new[]{"Consolas","DejaVu Sans Mono","Arial"},20);
            text=new GUIStyle{font=font,fontSize=24,fontStyle=FontStyle.Bold,normal={textColor=Color.white}};
            big=new GUIStyle(text){fontSize=96};small=new GUIStyle(text){fontSize=15};
            scanlines=new Texture2D(1,4,TextureFormat.RGBA32,false);scanlines.SetPixels(new[]{Color.clear,Color.clear,Color.clear,new Color(0,0,0,.025f)});scanlines.Apply();scanlines.wrapMode=TextureWrapMode.Repeat;
        }
        void Rect(float x,float y,float w,float h,Color color){GUI.color=color;GUI.DrawTexture(new Rect(x,y,w,h),Texture2D.whiteTexture);GUI.color=Color.white;}
        void Text(float x,float y,string value,Color color,int size=24)
        {text.fontSize=size;text.normal.textColor=color;GUI.Label(new Rect(x,y,1100,size*1.5f),value,text);}
        void Center(float y,string value,Color color,int size=24)
        {text.fontSize=size;float w=text.CalcSize(new GUIContent(value)).x;Text((1280-w)*.5f,y,value,color,size);}
        void OnGUI()
        {
            if(!Player)return;SetupGUI(); GUI.depth=-50;
            GUI.matrix=Matrix4x4.TRS(Vector3.zero,Quaternion.identity,new Vector3(Screen.width/1280f,Screen.height/720f,1));
            if(pixels){GUI.color=Color.white;GUI.DrawTexture(new Rect(0,0,1280,720),pixels,ScaleMode.StretchToFill,false);}
            GUI.color=new Color(1,1,1,.15f);GUI.DrawTextureWithTexCoords(new Rect(0,0,1280,720),scanlines,new Rect(0,0,1,180));GUI.color=Color.white;
            if(!HasBegun){DrawTitle();return;}
            DrawHUD();
            if(shotFlash>0)Rect(0,0,1280,720,new Color(1,.5f,.18f,shotFlash*.025f));
            if(damageFade>0){Rect(0,0,1280,20,new Color(1,.05f,0,damageFade));Rect(0,700,1280,20,new Color(1,.05f,0,damageFade));Rect(0,0,12,720,new Color(1,.05f,0,damageFade));Rect(1268,0,12,720,new Color(1,.05f,0,damageFade));}
            if(Player.IsDead)
            { Rect(0,0,1280,720,new Color(.015f,.01f,.02f,.87f));Center(245,"YOU FELL",orange,84);Center(354,"ONE HUMAN. ONE LAST CHANCE.",Color.white,22);Center(420,"[ R / ENTER ]  TRY AGAIN",cyan,26); }
            else if(IsPaused)
            { Rect(0,0,1280,720,new Color(.015f,.025f,.04f,.82f));Center(245,"PAUSED",Color.white,65);Center(350,"[ ENTER / ESC ]  RESUME",cyan,24);Center(405,"M  AUDIO   /   F2  720 / 1080 / 1440",new Color(.5f,.6f,.7f),17); }
        }
        void DrawTitle()
        {
            Rect(0,0,1280,720,new Color(.015f,.025f,.04f,.3f));Rect(0,0,585,720,new Color(.015f,.025f,.04f,.9f));
            Rect(46,54,8,20,orange);Text(70,51,"HUMAN OPERATOR / R-09",cyan,17);
            Text(42,113,"IRON",Color.white,116);Text(42,220,"PULSE",orange,116);
            Text(50,357,"FLESH. STEEL. SURVIVAL.",Color.white,21);
            Rect(50,414,455,2,new Color(.24f,.3f,.34f));
            Text(50,436,"WASD",cyan,17);Text(195,436,"MOVE",Color.white,17);
            Text(50,464,"SHIFT",cyan,17);Text(195,464,"DIRECTIONAL DASH",Color.white,17);
            Text(50,492,"SPACE ×2",cyan,17);Text(195,492,"DOUBLE JUMP",Color.white,17);
            Text(50,520,"LMB / R",cyan,17);Text(195,520,"FIRE / SXS RELOAD",Color.white,17);
            Text(50,548,"1 / 2 / 3",cyan,17);Text(195,548,"PISTOL / RIFLE / SXS",Color.white,16);
            Text(50,576,"E",orange,17);Text(195,576,"KATANA EXECUTION / +35 HP",Color.white,16);
            Text(50,604,"F",orange,17);Text(195,604,"LEFT FIST / CHARGED RAMPAGE",Color.white,16);
            float pulse=.65f+.35f*Mathf.Sin(Time.unscaledTime*4);
            Rect(50,632,455,48,new Color(orange.r,orange.g,orange.b,pulse));Text(77,642,"[ ENTER ]  BREACH THE REACTOR",Color.white,21);
            Text(892,49,"SECTOR 09",Color.white,27);Text(892,83,"THE FURNACE",orange,18);
            Text(905,653,"PIXEL / 3D REALISM",new Color(.6f,.75f,.8f),15);
        }
        void DrawHUD()
        {
            Rect(30,28,5,45,orange);Text(47,25,"SECTOR 09  /  WAVE "+Wave.ToString("00"),Color.white,21);
            Text(48,53,TimeSpan.FromSeconds(elapsed).ToString(@"mm\:ss")+"  •  "+Kills.ToString("000")+" ELIMINATED",new Color(.55f,.7f,.76f),14);
            string rank=Combo>=12?"S":Combo>=8?"A":Combo>=5?"B":Combo>=2?"C":"D";
            Color rankColor=Combo>=8?orange:cyan;Text(941,28,"REACTOR // PURGE",Color.white,23);
            Text(941,62,"COMBAT "+rank+"  /  "+Score.ToString("000000"),rankColor,15);
            if(Combo>0){Text(941,91,Combo+"x  "+(Combo>=8?"RAMPAGE":"CHAIN"),rankColor,19);Text(830,121,styleAction,rankColor,12);Rect(941,148,270*Mathf.Clamp01(1-(Time.time-comboAt)/5),3,rankColor);}
            Rect(30,606,295,88,new Color(ink.r,ink.g,ink.b,.86f));Rect(30,606,5,88,cyan);
            Text(50,613,"VITALS",new Color(.6f,.75f,.8f),13);Text(48,630,Mathf.CeilToInt(Player.Health).ToString("000"),Player.Health<30?orange:Color.white,51);
            Text(183,653,"/ 100",new Color(.45f,.6f,.65f),20);Rect(49,684,255,4,new Color(.1f,.2f,.25f));Rect(49,684,255*Mathf.Clamp01(Player.Health/100),4,cyan);
            Rect(954,606,296,88,new Color(ink.r,ink.g,ink.b,.86f));Rect(1245,606,5,88,orange);
            var weapon=Player.Weapon;
            Text(972,613,weapon.WeaponName.ToUpperInvariant(),new Color(.6f,.75f,.8f),13);
            if(weapon.UsesReload)
            {
                Text(972,632,Player.Shells.ToString("00"),Color.white,46);
                Text(1055,647,"/ 02",new Color(.55f,.67f,.7f),23);
                Text(1154,647,"∞",orange,26);
                for(int i=0;i<2;i++)Rect(975+i*65,682,54,5,i<Player.Shells?orange:new Color(.15f,.19f,.23f));
            }
            else {Text(972,632,"∞",orange,48);Text(1040,651,"UNLIMITED",Color.white,18);Text(1041,681,"NO RELOAD",new Color(.55f,.67f,.7f),11);}
            for(int i=0;i<3;i++){bool active=(int)weapon.CurrentWeapon==i;Rect(957+i*98,576,91,24,active?new Color(.5f,.16f,.05f,.9f):new Color(.02f,.035f,.05f,.8f));Text(963+i*98,579,(i+1)+" "+new[]{"PISTOL","RIFLE","SXS"}[i],active?Color.white:new Color(.5f,.6f,.65f),12);}
            if(weapon.IsReloading){Rect(974,667,210*Mathf.Clamp01(weapon.ReloadProgress),3,cyan);Text(1100,682,weapon.ReloadPhase.ToUpperInvariant(),cyan,10);}
            for(int i=0;i<2;i++)Rect(557+i*91,668,76,5,(i==0?Player.DashCooldown01<=.01f:Player.JumpsRemaining>0)?cyan:new Color(.2f,.3f,.35f));
            Text(552,683,"DASH     AIR",new Color(.5f,.7f,.75f),12);
            Color ultimateColor=Player.UltimateReady?orange:cyan;
            Rect(519,606,242,43,new Color(ink.r,ink.g,ink.b,.84f));
            Text(530,611,Player.IsUltimateActive?"RAMPAGE":Player.UltimateReady?"[ F ]  UNLEASH KATANA":"[ F ]  LEFT FIST",ultimateColor,14);
            Rect(530,635,220,4,new Color(.15f,.20f,.20f));Rect(530,635,220*Player.UltimateCharge/100f,4,ultimateColor);
            if(!Player.IsUltimateActive)Text(719,613,Mathf.FloorToInt(Player.UltimateCharge)+"%",ultimateColor,10);
            float spread=5+Player.Velocity.magnitude*.28f+reticleImpulse;
            Color cross=Time.unscaledTime<hitUntil?orange:Color.white;
            Rect(640-spread-9,359,8,2,cross);Rect(640+spread+1,359,8,2,cross);Rect(639,360-spread-9,2,8,cross);Rect(639,360+spread+1,2,8,cross);Rect(639,359,2,2,cross);
            if(Time.unscaledTime<hitUntil){Text(623,340,"×",orange,33);}
            IPEnemy target=Player.ExecutionCandidate;
            if(target&&!Player.IsExecuting&&!Player.IsSpecialAttacking)
            {var style=IPPlayer.ClassifyExecution(Player.transform.position,Player.Velocity,Player.transform.right,Player.transform.forward,target.transform.position,target.transform.forward,false);Center(420,"[ E ]  "+ExecutionName(style),orange,23);Center(453,"STAGGERED  •  +35 HEALTH",Color.white,13);}
            if(Player.IsExecuting){Rect(0,0,1280,27,Color.black);Rect(0,693,1280,27,Color.black);Center(52,ExecutionName(Player.CurrentExecutionStyle)+" // KATANA",orange,18);}
            if(Time.unscaledTime<labelUntil&&!Player.IsExecuting&&!Player.IsSpecialAttacking){float a=Mathf.Clamp01((labelUntil-Time.unscaledTime)*2);Center(222,banner,new Color(bannerColor.r,bannerColor.g,bannerColor.b,a),26);}
        }
        public static string ExecutionName(IPExecutionStyle style)
        {
            switch(style){case IPExecutionStyle.StrafeLeft:return "LEFT CROSSCUT";case IPExecutionStyle.StrafeRight:return "RIGHT CROSSCUT";case IPExecutionStyle.DashThrust:return "DASH IMPALEMENT";case IPExecutionStyle.RisingCut:return "RISING SEVER";case IPExecutionStyle.AerialPlunge:return "DEATH FROM ABOVE";case IPExecutionStyle.RearCut:return "TURN AND TEAR";default:return "CROSS SLASH";}
        }
        void OnDestroy()
        {Time.timeScale=1;IsPaused=false;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;if(pixels){pixels.Release();Destroy(pixels);}if(Instance==this)Instance=null;}
    }
    public class IPFragment : MonoBehaviour
    {
        Vector3 velocity,spin;float life,initial;Vector3 scale;
        public void Launch(Vector3 v,float duration){velocity=v;spin=UnityEngine.Random.onUnitSphere*700;life=initial=duration;scale=transform.localScale;}
        void Update(){life-=Time.deltaTime;if(life<=0){Destroy(gameObject);return;}velocity+=Vector3.down*12*Time.deltaTime;transform.position+=velocity*Time.deltaTime;transform.Rotate(spin*Time.deltaTime);transform.localScale=scale*Mathf.Clamp01(life/initial*3);}
    }
}
