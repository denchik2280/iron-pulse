using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace IronPulse
{
    /// <summary>Human first person hands, three mechanical firearms and movement driven sword finishers.</summary>
    public sealed class IPViewWeapon : MonoBehaviour
    {
        sealed class WeaponRig
        {
            public Transform root, mechanism, magazine, supportHand, triggerHand;
            public Vector3 supportHome, triggerHome, magazineHome, supportOnMechanism;
            public Quaternion supportRotation;
            public Transform[] chambers, muzzles;
            public GameObject[] flashes;
        }
        sealed class CubicArm
        {
            public Transform hand,wrist,forearm,sleeve,hem;
            public Transform[] fingers,bends;
            public Transform thumb;
            public Vector3 elbowLocal;
            public int elbowFrame=-1;
            public bool elbowInitialized;
            public float side;
        }
        readonly List<CubicArm> handArms = new List<CubicArm>();
        readonly WeaponRig[] rigs = new WeaponRig[3];
        readonly int[] ammunition = { 12, 30, 2 };
        readonly List<Mesh> ownedMeshes = new List<Mesh>();
        Camera viewCamera;
        IPPlayer owner;
        Transform viewRoot, swordRoot, swordRightHand, swordLeftHand, bladeTip;
        TrailRenderer bladeTrail;
        Light muzzleLight;
        Material metal, dark, walnut, brass, shellRed, glove, skin, skinShadow, skinLight,
            edge, polymer, rubber, clothStitch, bladeSteel, bladeEdge, bladeHamon, tracerMaterial, muzzleMaterial;
        float shotCooldown, reloadTime = -1f, recoil, recoilVelocity, walkPhase, flashTime,
            executionProgress = -1f, switchTime;
        Vector2 sway, swayVelocity, delayedSway;
        float movementBlend, airborneBlend, dashBlend, breathPhase, recoilRoll, recoilRollVelocity,
            recoilPitch, recoilPitchVelocity, punchProgress=-1f, ultimateProgress=-1f;
        Vector3 executionShoulder,executionChest,executionHead,freeHandStart;
        Quaternion freeHandStartRotation;
        bool hasExecutionVictim;
        bool hasPunchVictim;
        Vector3 punchVictimPoint;
        CubicArm combatArm;
        float actionRecovery;
        Vector3 specialRigPosition,specialMechanismPosition,recoveryRigPosition;
        Quaternion specialRigRotation,specialMechanismRotation,recoveryRigRotation;
        readonly float[] handTimes={0f,.105f,.23f,.285f,.355f,.425f,.60f,.68f,.755f,.855f,1f};
        readonly SwordPose[] handPoses=new SwordPose[11];
        readonly float[] brutalityHandTimes={0f,.095f,.20f,.275f,.32f,.37f,.42f,.475f,.52f,.61f,.68f,.735f,.82f,.91f,1f};
        readonly SwordPose[] brutalityHandPoses=new SwordPose[15];
        readonly float[] punchTimes={0f,.15f,.235f,.34f,.39f,.53f,.76f,1f};
        readonly SwordPose[] punchPoses=new SwordPose[8];
        int lastBarrel, pendingWeapon = -1;
        bool mechanismReleased, shellsEjected, shellsInserted;
        readonly bool[] spentShells = new bool[2];
        Transform[] reloadShells;
        Quaternion executionViewStartRotation;
        Vector3 executionViewStartPosition;
        Mesh smokeMesh;
        Material smokeMaterial;
        const float SwitchDuration = .34f;
        IPExecutionStyle executionStyle;
        SwordPose[] swordPoses;
        float[] swordTimes;
        Quaternion[] swordTangents;
        readonly Vector3 viewHome = new Vector3(.185f, -.235f, .405f);
        readonly Vector3 swordRightHome = new Vector3(.012f, -.026f, -.115f);
        readonly Vector3 swordLeftHome = new Vector3(-.012f, -.033f, -.245f);

        public IPWeaponKind CurrentWeapon { get; private set; } = IPWeaponKind.Shotgun;
        public int Capacity => CurrentWeapon == IPWeaponKind.Pistol ? 12 : CurrentWeapon == IPWeaponKind.Rifle ? 30 : 2;
        public int LoadedShells => UsesReload ? ammunition[2] : Capacity;
        public string WeaponName => CurrentWeapon == IPWeaponKind.Pistol ? "M-12 // SERVICE PISTOL" :
            CurrentWeapon == IPWeaponKind.Rifle ? "AR-30 // ASSAULT RIFLE" : "BREAKER // DOUBLE BARREL";
        public bool IsReloading => reloadTime >= 0f;
        public bool IsMeleeAttacking => punchProgress>=0f||ultimateProgress>=0f;
        public bool IsPunching => punchProgress>=0f;
        public bool IsUltimateActive => ultimateProgress>=0f;
        public bool UsesReload => CurrentWeapon == IPWeaponKind.Shotgun;
        public float ShotgunReloadSeconds => .92f;
        public Transform[] ReloadShells => reloadShells;
        public int ShotsFired { get; private set; }
        public string ReloadPhase => !IsReloading ? "Ready" : ReloadProgress < .28f ? "Open" :
            ReloadProgress < .42f ? "Eject" : ReloadProgress < .75f ? "Insert" : "Close";
        public float ReloadProgress => IsReloading ? Mathf.Clamp01(reloadTime / ReloadDuration) : 0f;
        public float ShotPulse { get; private set; }
        float ReloadDuration => ShotgunReloadSeconds;
        WeaponRig ActiveRig => rigs[(int)CurrentWeapon];

        public void Initialize(Camera camera, IPPlayer player)
        {
            viewCamera = camera; owner = player;
            metal = Surface("Weapon // satin gunmetal steel", new Color(.18f,.20f,.22f), .72f, .40f);
            dark = Surface("Weapon // recessed bore", new Color(.013f,.016f,.019f), .35f, .66f);
            edge = Surface("Weapon // worn machined steel", new Color(.33f,.355f,.375f), .92f, .23f);
            walnut = Surface("Shotgun // dark oiled walnut", new Color(.26f,.105f,.044f), 0f, .45f);
            brass = Surface("Weapon // cartridge brass", new Color(.53f,.36f,.15f), .88f, .26f);
            shellRed = Surface("Shotgun // red paper shell", new Color(.43f,.055f,.029f), 0f, .7f);
            polymer = Surface("Weapon // graphite polymer", new Color(.07f,.077f,.08f), .05f, .74f);
            rubber = Surface("Weapon // grip rubber", new Color(.025f,.029f,.03f), 0f, .91f);
            glove = Surface("Hands // fingerless black glove fabric", new Color(.043f,.048f,.045f), 0f, .94f);
            clothStitch = Surface("Hands // glove cloth seam", new Color(.12f,.13f,.115f), 0f, .97f);
            skin = Surface("Hands // warm forearm skin", new Color(.62f,.37f,.25f), 0f, .66f);
            skinShadow = Surface("Hands // skin creases", new Color(.39f,.205f,.13f), 0f, .79f);
            skinLight = Surface("Hands // knuckle skin highlight", new Color(.70f,.44f,.31f), 0f, .64f);
            bladeSteel = Surface("Katana // folded high carbon steel", new Color(.47f,.5f,.52f), .97f, .19f);
            bladeEdge = Surface("Katana // polished steel cutting bevel", new Color(.72f,.75f,.76f), 1f, .12f);
            bladeHamon = Surface("Katana // cloudy steel temper line", new Color(.57f,.6f,.61f), .92f, .26f);
            tracerMaterial = IPArt.Mat("Weapon // short ballistic tracer", new Color(1f,.72f,.32f), 2f);
            muzzleMaterial = IPArt.Mat("Weapon // flash core", new Color(1f,.79f,.39f), 7f);
            viewRoot = Node("Human first person arsenal", camera.transform, viewHome);
            rigs[0] = BuildPistol(); rigs[1] = BuildRifle(); rigs[2] = BuildShotgun();
            BuildKatana();
            var lightObject = Node("Weapon muzzle illumination", viewRoot, new Vector3(0f,.02f,.75f));
            muzzleLight = lightObject.gameObject.AddComponent<Light>(); muzzleLight.color = new Color(1f,.6f,.3f);
            muzzleLight.range = 7f; muzzleLight.type = LightType.Point; muzzleLight.lightUnit = LightUnit.Candela;
            muzzleLight.intensity = 0f; muzzleLight.shadows = LightShadows.None;
            var hdLight = lightObject.gameObject.AddComponent<HDAdditionalLightData>();
            hdLight.affectsVolumetric = false; muzzleLight.cullingMask = ~0; muzzleLight.renderingLayerMask = -1;
            BuildSmoke();
            // A broad, restrained camera key reveals the gun's machining and walnut under
            // dark arena lighting. Its exclusive rendering layer keeps the nearby world dark.
            var fillObject = Node("Viewmodel // soft neutral warm fill",camera.transform,new Vector3(-.34f,.21f,.15f));
            var fill = fillObject.gameObject.AddComponent<Light>();
            fill.type = LightType.Point;fill.color = new Color(1f,.935f,.86f);fill.range = 2.2f;
            fill.shadows = LightShadows.None;
            var fillHD = fillObject.gameObject.AddComponent<HDAdditionalLightData>();
            fillHD.affectsVolumetric = false;fillHD.affectDiffuse = true;fillHD.affectSpecular = true;fillHD.shapeRadius = .20f;
            // Assign physical units after HD data creation, so default-light initialization
            // cannot replace the intended candela value or rendering layer.
            fill.lightUnit = LightUnit.Candela;fill.intensity = 220f;fill.cullingMask = 1<<2;
            var pipeline=GraphicsSettings.currentRenderPipeline as HDRenderPipelineAsset;
            bool supportsLayers=pipeline&&pipeline.currentPlatformRenderPipelineSettings.supportLightLayers;
            fill.renderingLayerMask=supportsLayers?1<<7:-1;
            if(supportsLayers)
            {
                var cameraHD=camera.GetComponent<HDAdditionalCameraData>();
                if(!cameraHD)cameraHD=camera.gameObject.AddComponent<HDAdditionalCameraData>();
                cameraHD.customRenderingSettings=true;
                cameraHD.renderingPathCustomFrameSettings.SetEnabled(FrameSettingsField.LightLayers,true);
                cameraHD.renderingPathCustomFrameSettingsOverrideMask.mask[(int)FrameSettingsField.LightLayers]=true;
            }
            SetWeaponVisibility();
            foreach (var renderer in viewRoot.GetComponentsInChildren<Renderer>(true))
            { renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = true; renderer.renderingLayerMask |= 1u<<7; }
        }

        Material Surface(string name, Color color, float metallic, float roughness)
        {
            var result = IPArt.Mat(name, color);
            if (result.HasProperty("_Metallic")) result.SetFloat("_Metallic", metallic);
            if (result.HasProperty("_Roughness")) result.SetFloat("_Roughness", roughness);
            if (result.HasProperty("_Smoothness")) result.SetFloat("_Smoothness", 1f-roughness);
            return result;
        }

        Transform Node(string name, Transform parent, Vector3 position)
        {
            var node = new GameObject(name).transform; node.SetParent(parent,false);
            node.localPosition = position; node.gameObject.layer = 2; return node;
        }

        GameObject MeshPart(string name, Transform parent, Mesh mesh, Material material)
        {
            var part = Node(name,parent,Vector3.zero).gameObject;
            part.AddComponent<MeshFilter>().sharedMesh = mesh;
            part.AddComponent<MeshRenderer>().sharedMaterial = material;
            ownedMeshes.Add(mesh); return part;
        }

        // Octagonal profiles replace the old rectangular blocks. Geometry is built once at initialization.
        GameObject Bevel(string name, Transform parent, Vector3 position, Vector3 size, Material material, float chamfer=.12f)
        {
            float x=size.x*.5f, y=size.y*.5f, z=size.z*.5f, c=Mathf.Min(size.x,size.y)*Mathf.Clamp(chamfer,.005f,.45f);
            var outline = new[] { new Vector2(-x+c,-y),new Vector2(x-c,-y),new Vector2(x,-y+c),new Vector2(x,y-c),
                new Vector2(x-c,y),new Vector2(-x+c,y),new Vector2(-x,y-c),new Vector2(-x,-y+c) };
            var vertices=new List<Vector3>(); var triangles=new List<int>(); var uv=new List<Vector2>();
            for(int i=0;i<outline.Length;i++)
            {
                int j=(i+1)%outline.Length;
                Quad(vertices,triangles,uv,new Vector3(outline[i].x,outline[i].y,-z),new Vector3(outline[j].x,outline[j].y,-z),
                    new Vector3(outline[j].x,outline[j].y,z),new Vector3(outline[i].x,outline[i].y,z));
                Triangle(vertices,triangles,uv,new Vector3(0f,0f,-z),new Vector3(outline[j].x,outline[j].y,-z),new Vector3(outline[i].x,outline[i].y,-z));
                Triangle(vertices,triangles,uv,new Vector3(0f,0f,z),new Vector3(outline[i].x,outline[i].y,z),new Vector3(outline[j].x,outline[j].y,z));
            }
            var result=MeshPart(name,parent,MakeMesh(name,vertices,triangles,uv),material);
            result.transform.localPosition=position; return result;
        }

        GameObject Tube(string name, Transform parent, Vector3 position, float radius, float length, Material material, float bore=0f, int sides=12)
        {
            var vertices=new List<Vector3>();var triangles=new List<int>();var uv=new List<Vector2>();
            float half=length*.5f;
            for(int i=0;i<sides;i++)
            {
                float a=i*Mathf.PI*2f/sides,b=(i+1)*Mathf.PI*2f/sides;
                Vector3 a0=new Vector3(Mathf.Cos(a)*radius,Mathf.Sin(a)*radius,-half),b0=new Vector3(Mathf.Cos(b)*radius,Mathf.Sin(b)*radius,-half);
                Vector3 a1=a0+Vector3.forward*length,b1=b0+Vector3.forward*length;
                Quad(vertices,triangles,uv,a0,b0,b1,a1);
                if(bore>0f)
                {
                    Vector3 ai=new Vector3(Mathf.Cos(a)*bore,Mathf.Sin(a)*bore,half),bi=new Vector3(Mathf.Cos(b)*bore,Mathf.Sin(b)*bore,half);
                    Quad(vertices,triangles,uv,a1,b1,bi,ai);
                    Quad(vertices,triangles,uv,ai,bi,new Vector3(bi.x,bi.y,-half),new Vector3(ai.x,ai.y,-half));
                    Quad(vertices,triangles,uv,b0,a0,new Vector3(ai.x,ai.y,-half),new Vector3(bi.x,bi.y,-half));
                }
                else
                {
                    Triangle(vertices,triangles,uv,new Vector3(0f,0f,half),a1,b1);
                    Triangle(vertices,triangles,uv,new Vector3(0f,0f,-half),b0,a0);
                }
            }
            var result=MeshPart(name,parent,MakeMesh(name,vertices,triangles,uv),material);result.transform.localPosition=position;return result;
        }

        void Stock(string name,Transform parent,Vector2[] outline,float width,Material material)
        {
            var vertices=new List<Vector3>();var triangles=new List<int>();var uv=new List<Vector2>();int n=outline.Length;
            for(int i=1;i<n-1;i++)
            {
                Triangle(vertices,triangles,uv,new Vector3(-width*.5f,outline[0].y,outline[0].x),new Vector3(-width*.5f,outline[i+1].y,outline[i+1].x),new Vector3(-width*.5f,outline[i].y,outline[i].x));
                Triangle(vertices,triangles,uv,new Vector3(width*.5f,outline[0].y,outline[0].x),new Vector3(width*.5f,outline[i].y,outline[i].x),new Vector3(width*.5f,outline[i+1].y,outline[i+1].x));
            }
            for(int i=0;i<n;i++)
            {
                int j=(i+1)%n;Quad(vertices,triangles,uv,new Vector3(-width*.5f,outline[i].y,outline[i].x),new Vector3(-width*.5f,outline[j].y,outline[j].x),new Vector3(width*.5f,outline[j].y,outline[j].x),new Vector3(width*.5f,outline[i].y,outline[i].x));
            }
            MeshPart(name,parent,MakeMesh(name,vertices,triangles,uv),material);
        }

        static void Quad(List<Vector3> v,List<int> t,List<Vector2> uv,Vector3 a,Vector3 b,Vector3 c,Vector3 d)
        {int k=v.Count;v.Add(a);v.Add(b);v.Add(c);v.Add(d);uv.Add(Vector2.zero);uv.Add(Vector2.right);uv.Add(Vector2.one);uv.Add(Vector2.up);t.Add(k);t.Add(k+1);t.Add(k+2);t.Add(k);t.Add(k+2);t.Add(k+3);}
        static void Triangle(List<Vector3> v,List<int> t,List<Vector2> uv,Vector3 a,Vector3 b,Vector3 c)
        {int k=v.Count;v.Add(a);v.Add(b);v.Add(c);uv.Add(Vector2.zero);uv.Add(Vector2.right);uv.Add(Vector2.up);t.Add(k);t.Add(k+1);t.Add(k+2);}
        static Mesh MakeMesh(string name,List<Vector3> vertices,List<int> triangles,List<Vector2> uv)
        {var mesh=new Mesh{name=name};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.SetUVs(0,uv);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;}

        void Screw(Transform parent,Vector3 position,float radius=.006f)
        {
            var screw=Tube("Recessed steel fastener",parent,position,radius,.003f,edge,0f,10);screw.transform.localRotation=Quaternion.Euler(0f,90f,0f);
            Bevel("Fastener slot",screw.transform,new Vector3(0f,0f,.0017f),new Vector3(radius*1.3f,.0015f,.001f),dark,.03f);
        }

        WeaponRig CreateRig(string name)=>new WeaponRig{root=Node(name,viewRoot,Vector3.zero)};

        void Hands(WeaponRig rig,Vector3 right,Vector3 left,Vector3 leftEuler)
        {
            rig.triggerHome=right;rig.supportHome=left;
            rig.triggerHand=Node("Human right trigger hand",rig.root,right);BuildHumanHand(rig.triggerHand,false);
            rig.supportHand=Node("Human bare left support hand",rig.root,left);BuildHumanHand(rig.supportHand,true,true);
            rig.supportHand.localRotation=Quaternion.Euler(leftEuler);
            rig.supportRotation=rig.supportHand.localRotation;
            if(rig.mechanism)rig.supportOnMechanism=rig.mechanism.InverseTransformPoint(rig.supportHand.position);
        }

        WeaponRig BuildPistol()
        {
            var rig=CreateRig("M-12 // full size service pistol");
            Bevel("Polymer dust cover",rig.root,new Vector3(0f,-.025f,.055f),new Vector3(.047f,.048f,.205f),polymer);
            var grip=Bevel("Anatomical pistol grip",rig.root,new Vector3(0f,-.112f,-.018f),new Vector3(.048f,.155f,.07f),rubber,.2f);grip.transform.localRotation=Quaternion.Euler(-16f,0f,0f);
            for(int i=0;i<7;i++)Bevel("Grip front strap stippling",grip.transform,new Vector3(0f,-.059f+i*.019f,.0355f),new Vector3(.036f,.0035f,.002f),polymer,.05f);
            for(int side=-1;side<=1;side+=2)
            {
                Bevel("Grip side inset",grip.transform,new Vector3(side*.0245f,-.005f,0f),new Vector3(.002f,.098f,.044f),polymer);
                Screw(rig.root,new Vector3(side*.026f,-.057f,-.026f),.004f);
            }
            rig.mechanism=Node("Reciprocating pistol slide",rig.root,Vector3.zero);
            Bevel("Machined steel slide",rig.mechanism,new Vector3(0f,.018f,.054f),new Vector3(.048f,.061f,.23f),metal,.19f);
            Bevel("Slide polished chamfer",rig.mechanism,new Vector3(0f,.0488f,.049f),new Vector3(.032f,.002f,.215f),edge,.08f);
            Bevel("Ejection port recess",rig.mechanism,new Vector3(.0245f,.024f,.035f),new Vector3(.002f,.023f,.049f),dark);
            Bevel("Barrel hood in port",rig.mechanism,new Vector3(.0257f,.025f,.037f),new Vector3(.001f,.015f,.035f),edge);
            for(int i=0;i<7;i++)for(int side=-1;side<=1;side+=2)
                Bevel("Slide rear cocking serration",rig.mechanism,new Vector3(side*.0245f,.018f,-.043f+i*.006f),new Vector3(.002f,.043f,.0025f),dark,.03f);
            Tube("Pistol recessed muzzle crown",rig.mechanism,new Vector3(0f,.022f,.171f),.015f,.014f,edge,.0105f);
            Tube("Pistol dark rifled bore",rig.mechanism,new Vector3(0f,.022f,.163f),.0108f,.022f,dark,.008f);
            Tube("Recoil spring guide",rig.root,new Vector3(0f,-.007f,.162f),.006f,.025f,metal);
            Sight(rig.mechanism,new Vector3(0f,.058f,-.049f),true);Sight(rig.mechanism,new Vector3(0f,.058f,.14f),false);
            TriggerGuard(rig.root,new Vector3(0f,-.057f,.035f),.041f,.066f,.063f);
            Bevel("Pistol curved trigger",rig.root,new Vector3(0f,-.063f,.024f),new Vector3(.008f,.035f,.013f),edge).transform.localRotation=Quaternion.Euler(-15f,0f,0f);
            Bevel("Slide release paddle",rig.root,new Vector3(-.027f,-.012f,-.026f),new Vector3(.007f,.008f,.032f),metal);
            rig.magazine=Node("Removable pistol magazine",rig.root,new Vector3(0f,-.123f,-.02f));rig.magazineHome=rig.magazine.localPosition;
            Bevel("Pistol magazine body",rig.magazine,Vector3.zero,new Vector3(.033f,.121f,.047f),metal);
            Bevel("Pistol magazine base",rig.magazine,new Vector3(0f,-.065f,0f),new Vector3(.05f,.012f,.064f),rubber);
            rig.muzzles=new[]{Node("Pistol muzzle",rig.root,new Vector3(0f,.022f,.184f))};rig.flashes=new[]{BuildFlash(rig.muzzles[0],.6f)};
            Hands(rig,new Vector3(.012f,-.094f,-.016f),new Vector3(-.05f,-.105f,-.002f),new Vector3(9f,-22f,15f));
            return rig;
        }

        WeaponRig BuildRifle()
        {
            var rig=CreateRig("AR-30 // compact gas operated assault rifle");
            Bevel("Forged upper receiver",rig.root,new Vector3(0f,.012f,.022f),new Vector3(.067f,.085f,.264f),metal,.18f);
            Bevel("Lower receiver",rig.root,new Vector3(0f,-.055f,-.022f),new Vector3(.06f,.074f,.18f),polymer,.14f);
            var grip=Bevel("Textured rifle pistol grip",rig.root,new Vector3(0f,-.15f,-.085f),new Vector3(.048f,.145f,.067f),rubber,.2f);grip.transform.localRotation=Quaternion.Euler(-18f,0f,0f);
            for(int i=0;i<5;i++)Bevel("Grip texture ridge",grip.transform,new Vector3(0f,-.044f+i*.02f,.034f),new Vector3(.036f,.003f,.002f),polymer);
            Tube("Stock buffer tube",rig.root,new Vector3(0f,.005f,-.205f),.022f,.2f,metal);
            Stock("Adjustable skeletal shoulder stock",rig.root,new[]{new Vector2(-.11f,.027f),new Vector2(-.31f,.017f),new Vector2(-.355f,.042f),new Vector2(-.39f,.018f),new Vector2(-.39f,-.13f),new Vector2(-.345f,-.13f),new Vector2(-.29f,-.047f),new Vector2(-.11f,-.035f)},.058f,polymer);
            Bevel("Rifle rubber butt pad",rig.root,new Vector3(0f,-.045f,-.39f),new Vector3(.064f,.18f,.019f),rubber);
            for(int i=0;i<5;i++)Bevel("Stock adjustment tooth",rig.root,new Vector3(.025f,-.018f,-.16f-i*.028f),new Vector3(.004f,.007f,.011f),edge);
            Bevel("Vented free float handguard",rig.root,new Vector3(0f,.005f,.285f),new Vector3(.069f,.08f,.283f),polymer,.25f);
            for(int i=0;i<7;i++)
            {
                float z=.175f+i*.035f;
                for(int side=-1;side<=1;side+=2)
                {Bevel("M-LOK cooling slot",rig.root,new Vector3(side*.0347f,.005f,z),new Vector3(.001f,.018f,.023f),dark,.35f);Bevel("Handguard upper vent",rig.root,new Vector3(side*.024f,.0451f,z),new Vector3(.012f,.001f,.017f),dark,.15f);}
            }
            Bevel("Picatinny upper rail",rig.root,new Vector3(0f,.062f,.135f),new Vector3(.033f,.013f,.56f),metal);
            for(int i=0;i<21;i++)Bevel("Picatinny transverse rail tooth",rig.root,new Vector3(0f,.071f,-.117f+i*.026f),new Vector3(.041f,.009f,.014f),edge,.11f);
            Tube("Rifle barrel",rig.root,new Vector3(0f,.013f,.46f),.013f,.16f,metal);
            Tube("Muzzle brake external sleeve",rig.root,new Vector3(0f,.013f,.56f),.0205f,.075f,metal,.0115f);
            Tube("Machined muzzle crown",rig.root,new Vector3(0f,.013f,.598f),.0205f,.006f,edge,.0115f);
            for(int i=0;i<3;i++)Bevel("Muzzle brake gas port",rig.root,new Vector3(.0205f,.013f,.537f+i*.019f),new Vector3(.001f,.013f,.009f),dark);
            Sight(rig.root,new Vector3(0f,.092f,-.077f),true);Sight(rig.root,new Vector3(0f,.092f,.377f),false);
            rig.mechanism=Node("Animated rifle bolt carrier",rig.root,Vector3.zero);
            Bevel("Rifle side ejection port",rig.root,new Vector3(.034f,.017f,.048f),new Vector3(.001f,.035f,.087f),dark);
            Bevel("Exposed steel bolt",rig.mechanism,new Vector3(.035f,.017f,.049f),new Vector3(.003f,.023f,.071f),edge);
            Bevel("Charging handle",rig.mechanism,new Vector3(0f,.04f,-.123f),new Vector3(.098f,.014f,.025f),metal);
            Bevel("Fire selector lever",rig.root,new Vector3(-.033f,-.054f,-.055f),new Vector3(.007f,.01f,.032f),edge).transform.localRotation=Quaternion.Euler(0f,0f,-25f);
            Screw(rig.root,new Vector3(.034f,-.032f,-.062f));Screw(rig.root,new Vector3(.034f,-.025f,.093f));
            TriggerGuard(rig.root,new Vector3(0f,-.105f,-.038f),.045f,.068f,.066f);
            Bevel("Rifle trigger",rig.root,new Vector3(0f,-.111f,-.045f),new Vector3(.008f,.041f,.013f),edge).transform.localRotation=Quaternion.Euler(-20f,0f,0f);
            rig.magazine=Node("Curved detachable rifle magazine",rig.root,new Vector3(0f,-.165f,.068f));rig.magazineHome=rig.magazine.localPosition;
            Stock("Thirty round curved magazine",rig.magazine,new[]{new Vector2(-.035f,.084f),new Vector2(.035f,.084f),new Vector2(.039f,.012f),new Vector2(.063f,-.115f),new Vector2(-.019f,-.115f),new Vector2(-.035f,-.015f)},.053f,polymer);
            for(int i=0;i<4;i++)for(int side=-1;side<=1;side+=2)
                Bevel("Magazine reinforcing rib",rig.magazine,new Vector3(side*.027f,-.014f,-.022f+i*.018f),new Vector3(.003f,.14f,.006f),metal,.18f).transform.localRotation=Quaternion.Euler(-7f,0f,0f);
            Bevel("Magazine steel floorplate",rig.magazine,new Vector3(0f,-.116f,.022f),new Vector3(.061f,.014f,.091f),metal);
            rig.muzzles=new[]{Node("Rifle muzzle",rig.root,new Vector3(0f,.013f,.604f))};rig.flashes=new[]{BuildFlash(rig.muzzles[0],.75f)};
            Hands(rig,new Vector3(.017f,-.143f,-.073f),new Vector3(-.032f,-.062f,.27f),new Vector3(0f,0f,-28f));
            return rig;
        }

        WeaponRig BuildShotgun()
        {
            var rig=CreateRig("BREAKER // walnut side by side double barrel");
            Bevel("Rounded boxlock receiver",rig.root,new Vector3(0f,-.014f,-.024f),new Vector3(.11f,.091f,.19f),metal,.2f);
            Bevel("Receiver polished upper spine",rig.root,new Vector3(0f,.032f,-.019f),new Vector3(.056f,.01f,.177f),edge);
            Bevel("Standing breech",rig.root,new Vector3(0f,.005f,.069f),new Vector3(.112f,.101f,.014f),edge,.16f);
            Stock("Shaped hand finished walnut stock",rig.root,new[]{new Vector2(-.48f,-.22f),new Vector2(-.48f,-.065f),new Vector2(-.32f,-.045f),new Vector2(-.15f,.007f),new Vector2(-.105f,-.032f),new Vector2(-.17f,-.10f),new Vector2(-.245f,-.147f),new Vector2(-.31f,-.13f)},.085f,walnut);
            Bevel("Black rubber shotgun recoil pad",rig.root,new Vector3(0f,-.145f,-.482f),new Vector3(.093f,.174f,.018f),rubber).transform.localRotation=Quaternion.Euler(7f,0f,0f);
            for(int i=0;i<8;i++)
            {
                var cut=Bevel("Walnut grip fine checkering",rig.root,new Vector3(.043f,-.067f-i*.004f,-.154f-i*.007f),new Vector3(.0015f,.037f,.002f),dark,.04f);cut.transform.localRotation=Quaternion.Euler(29f,0f,0f);
            }
            Bevel("Top break lever",rig.root,new Vector3(.013f,.052f,-.068f),new Vector3(.022f,.013f,.073f),metal).transform.localRotation=Quaternion.Euler(0f,-21f,0f);
            TriggerGuard(rig.root,new Vector3(0f,-.085f,-.07f),.035f,.052f,.094f);
            for(int i=0;i<2;i++)
            {
                Bevel("Twin curved trigger",rig.root,new Vector3(i==0?-.009f:.009f,-.083f,-.056f-i*.027f),new Vector3(.006f,.035f,.012f),metal).transform.localRotation=Quaternion.Euler(-22f,0f,0f);
                Screw(rig.root,new Vector3(i==0?-.056f:.056f,-.012f,.043f),.009f);
                for(int k=0;k<4;k++)Bevel("Engraved receiver scroll",rig.root,new Vector3(i==0?-.0555f:.0555f,-.005f+Mathf.Sin(k)*.009f,-.06f+k*.025f),new Vector3(.001f,.013f,.004f),edge,.2f).transform.localRotation=Quaternion.Euler(25f+k*20f,0f,0f);
            }
            rig.mechanism=Node("Break action barrel hinge",rig.root,new Vector3(0f,-.013f,.075f));
            Bevel("Barrel monoblock",rig.mechanism,new Vector3(0f,.023f,.033f),new Vector3(.105f,.079f,.078f),edge,.18f);
            rig.chambers=new Transform[2];rig.muzzles=new Transform[2];rig.flashes=new GameObject[2];
            for(int i=0;i<2;i++)
            {
                float x=i==0?-.024f:.024f;
                Tube("Long blued steel barrel",rig.mechanism,new Vector3(x,.025f,.337f),.023f,.64f,metal,.018f,16);
                Tube("Polished barrel muzzle crown",rig.mechanism,new Vector3(x,.025f,.659f),.0235f,.007f,edge,.0185f,16);
                Tube("Deep twelve gauge bore",rig.mechanism,new Vector3(x,.025f,.64f),.0184f,.033f,dark,.016f,16);
                Tube("Open breech chamber",rig.mechanism,new Vector3(x,.025f,-.009f),.021f,.012f,dark,.017f,16);
                var shell=Node("Live twelve gauge cartridge",rig.mechanism,new Vector3(x,.025f,.021f));
                Tube("Red shell hull",shell,Vector3.zero,.018f,.063f,shellRed);
                Tube("Brass shell rim",shell,new Vector3(0f,0f,-.035f),.0205f,.01f,brass);
                Tube("Shell primer",shell,new Vector3(0f,0f,-.0405f),.006f,.0015f,edge);
                rig.chambers[i]=shell;
                rig.muzzles[i]=Node("Double barrel muzzle "+i,rig.mechanism,new Vector3(x,.025f,.667f));rig.flashes[i]=BuildFlash(rig.muzzles[i],1f);
            }
            Bevel("Raised sight rib",rig.mechanism,new Vector3(0f,.053f,.341f),new Vector3(.013f,.009f,.631f),metal);
            for(int i=0;i<8;i++)Bevel("Sight rib support",rig.mechanism,new Vector3(0f,.046f,.10f+i*.068f),new Vector3(.012f,.012f,.012f),metal);
            Tube("Small brass bead sight",rig.mechanism,new Vector3(0f,.065f,.62f),.004f,.008f,brass,0f,8).transform.localRotation=Quaternion.Euler(90f,0f,0f);
            Stock("Rounded walnut fore end",rig.mechanism,new[]{new Vector2(.12f,-.012f),new Vector2(.43f,-.012f),new Vector2(.458f,-.04f),new Vector2(.413f,-.081f),new Vector2(.16f,-.087f),new Vector2(.104f,-.055f)},.091f,walnut);
            for(int i=0;i<11;i++)for(int side=-1;side<=1;side+=2)
                Bevel("Walnut fore end checkering",rig.mechanism,new Vector3(side*.0459f,-.048f,.17f+i*.021f),new Vector3(.001f,.033f,.002f),dark,.05f).transform.localRotation=Quaternion.Euler(side*25f,0f,0f);
            Hands(rig,new Vector3(.026f,-.09f,-.155f),new Vector3(-.029f,-.067f,.32f),new Vector3(-7f,0f,-21f));
            reloadShells=new Transform[2];
            for(int i=0;i<2;i++)
            {
                reloadShells[i]=Cartridge("Fresh twelve gauge in left hand "+i,rig.root,new Vector3(i==0?-.024f:.024f,-.4f,-.15f));
                reloadShells[i].gameObject.SetActive(false);
            }
            return rig;
        }

        void Sight(Transform parent,Vector3 position,bool rear)
        {
            var sight=Node(rear?"Rear iron sight":"Front iron post",parent,position);
            Bevel("Sight pedestal",sight,Vector3.zero,new Vector3(.033f,.013f,.019f),metal);
            if(rear)
            {
                Bevel("Rear sight left ear",sight,new Vector3(-.012f,.012f,0f),new Vector3(.008f,.025f,.013f),dark);
                Bevel("Rear sight right ear",sight,new Vector3(.012f,.012f,0f),new Vector3(.008f,.025f,.013f),dark);
                Bevel("Rear sight left white dot",sight,new Vector3(-.012f,.017f,-.007f),new Vector3(.003f,.003f,.001f),skinLight);
                Bevel("Rear sight right white dot",sight,new Vector3(.012f,.017f,-.007f),new Vector3(.003f,.003f,.001f),skinLight);
            }
            else Bevel("Front sight blade",sight,new Vector3(0f,.014f,0f),new Vector3(.007f,.029f,.012f),dark);
        }

        void TriggerGuard(Transform parent,Vector3 center,float width,float height,float length)
        {
            Bevel("Trigger guard forward curve",parent,center+new Vector3(0f,0f,length*.5f),new Vector3(width,height,.009f),metal,.2f).transform.localRotation=Quaternion.Euler(12f,0f,0f);
            Bevel("Trigger guard lower bow",parent,center+new Vector3(0f,-height*.5f,0f),new Vector3(width,.009f,length),metal,.2f);
            Bevel("Trigger guard rear return",parent,center+new Vector3(0f,0f,-length*.5f),new Vector3(width,height,.009f),metal,.2f).transform.localRotation=Quaternion.Euler(-14f,0f,0f);
        }

        // Deliberately cuboid human hands: broad skin planes and simple tactical gloves,
        // without a sculpted anatomical silhouette, veins, nails or rounded finger meshes.
        CubicArm BuildHumanHand(Transform parent,bool left,bool bare=false,bool broadFist=false)
        {
            float side=left?-1f:1f;
            Transform palmRoot=parent;
            if(broadFist)
            {
                // Enlarge only palm, thumb and fingers. The wrist/forearm retain human
                // dimensions and the fist has a readable silhouette at full extension.
                palmRoot=Node("Bare combat hand // broad cubic fist",parent,Vector3.zero);
                palmRoot.localScale=Vector3.one*1.2f;
            }
            Material palmMaterial=bare?skin:glove;
            Block("Cuboid human palm",palmRoot,new Vector3(0f,-.002f,.002f),new Vector3(.082f,.05f,.092f),palmMaterial);
            Block(bare?"Broad cubic bare knuckles":"Flat glove knuckle panel",palmRoot,new Vector3(0f,.026f,.005f),new Vector3(.077f,.008f,.075f),bare?skinLight:glove);
            if(!bare)
            {
                Block("Glove wrist cuff",parent,new Vector3(0f,0f,-.052f),new Vector3(.089f,.064f,.032f),glove);
                Block("Wrist cuff tab",parent,new Vector3(side*.035f,.035f,-.05f),new Vector3(.025f,.009f,.026f),clothStitch);
            }
            var arm=new CubicArm{hand=parent,side=side,fingers=new Transform[4],bends=new Transform[4]};
            for(int i=0;i<4;i++)
            {
                float x=(i-1.5f)*.0205f;
                var finger=Node("Block human finger "+i,palmRoot,new Vector3(x,-.001f,.049f));
                finger.localRotation=Quaternion.Euler(32f,0f,0f);
                Block("Square human finger base",finger,new Vector3(0f,0f,.012f),new Vector3(.018f,.022f,.029f),palmMaterial);
                var knuckle=Node("Cuboid finger bend",finger,new Vector3(0f,0f,.027f));knuckle.localRotation=Quaternion.Euler(54f,0f,0f);
                Block("Exposed block fingertip",knuckle,new Vector3(0f,0f,.012f),new Vector3(.018f,.021f,.03f),skin);
                arm.fingers[i]=finger;arm.bends[i]=knuckle;
            }
            var thumb=Node("Block human thumb",palmRoot,new Vector3(-side*.047f,-.003f,.012f));
            thumb.localRotation=Quaternion.Euler(-15f,-side*46f,side*22f);
            Block("Square human thumb base",thumb,new Vector3(0f,0f,.014f),new Vector3(.029f,.028f,.033f),palmMaterial);
            Block("Exposed cubic thumb",thumb,new Vector3(0f,-.008f,.038f),new Vector3(.026f,.025f,.031f),skin);
            arm.thumb=thumb;
            arm.wrist=Block("Cuboid bare human wrist",parent,Vector3.zero,Vector3.one,skin).transform;
            arm.forearm=Block("Cubic human forearm",parent,Vector3.zero,Vector3.one,skin).transform;
            arm.sleeve=Block("Block charcoal rolled sleeve",parent,Vector3.zero,Vector3.one,glove).transform;
            arm.hem=Block("Square cloth sleeve hem",parent,Vector3.zero,Vector3.one,clothStitch).transform;
            handArms.Add(arm);
            return arm;
        }

        void SetCombatGrip(float grip,float fist=0f)
        {
            if(combatArm==null)return;
            for(int i=0;i<combatArm.fingers.Length;i++)
            {
                // Fingers close successively rather than collapsing as one rigid mitten.
                float curl=Smooth((grip-i*.055f)/.80f);
                combatArm.fingers[i].localRotation=Quaternion.Euler(Mathf.Lerp(-9f,Mathf.Lerp(47f,84f,fist),curl),0f,(i-1.5f)*(1f-curl)*5f);
                combatArm.bends[i].localRotation=Quaternion.Euler(Mathf.Lerp(7f,Mathf.Lerp(66f,101f,fist),curl),0f,0f);
            }
            combatArm.thumb.localRotation=Quaternion.Euler(Mathf.Lerp(-25f,18f,grip),Mathf.Lerp(68f,34f,grip),Mathf.Lerp(-39f,-11f,grip));
        }

        GameObject Block(string name,Transform parent,Vector3 position,Vector3 size,Material material)
        {
            // The weapon parts retain machining bevels; all hand geometry has exactly six faces.
            var vertices=new List<Vector3>();var triangles=new List<int>();var uv=new List<Vector2>();
            Vector3 a=size*.5f;
            Quad(vertices,triangles,uv,new Vector3(-a.x,-a.y,-a.z),new Vector3(-a.x,a.y,-a.z),new Vector3(a.x,a.y,-a.z),new Vector3(a.x,-a.y,-a.z));
            Quad(vertices,triangles,uv,new Vector3(a.x,-a.y,a.z),new Vector3(a.x,a.y,a.z),new Vector3(-a.x,a.y,a.z),new Vector3(-a.x,-a.y,a.z));
            Quad(vertices,triangles,uv,new Vector3(-a.x,-a.y,a.z),new Vector3(-a.x,a.y,a.z),new Vector3(-a.x,a.y,-a.z),new Vector3(-a.x,-a.y,-a.z));
            Quad(vertices,triangles,uv,new Vector3(a.x,-a.y,-a.z),new Vector3(a.x,a.y,-a.z),new Vector3(a.x,a.y,a.z),new Vector3(a.x,-a.y,a.z));
            Quad(vertices,triangles,uv,new Vector3(-a.x,a.y,-a.z),new Vector3(-a.x,a.y,a.z),new Vector3(a.x,a.y,a.z),new Vector3(a.x,a.y,-a.z));
            Quad(vertices,triangles,uv,new Vector3(-a.x,-a.y,a.z),new Vector3(-a.x,-a.y,-a.z),new Vector3(a.x,-a.y,-a.z),new Vector3(a.x,-a.y,a.z));
            var part=MeshPart(name,parent,MakeMesh(name,vertices,triangles,uv),material);part.transform.localPosition=position;return part;
        }

        void UpdateArms()
        {
            if(!viewCamera)return;
            foreach(var arm in handArms)
            {
                if(!arm.hand||!arm.hand.gameObject.activeInHierarchy)continue;
                Vector3 wrist=arm.hand.TransformPoint(new Vector3(0f,-.002f,-.055f));
                // Elbows stay below and behind the eye. Rotating the supporting palm must not
                // swing a rigid forearm through the camera, especially while loading shells.
                Vector3 wristLocal=viewCamera.transform.InverseTransformPoint(wrist);
                Vector3 elbowTarget=new Vector3(arm.side*.36f+Mathf.Clamp(wristLocal.x-arm.side*.18f,-.5f,.5f)*.13f,
                    -.42f+Mathf.Clamp(wristLocal.y+.20f,0f,.8f)*.12f,
                    -.11f+Mathf.Clamp(wristLocal.z-.3f,0f,.9f)*.16f);
                if(!arm.elbowInitialized){arm.elbowLocal=elbowTarget;arm.elbowInitialized=true;}
                if(arm.elbowFrame!=Time.frameCount)
                {
                    // The elbow follows the wrist with a small, frame-stable delay while staying
                    // below the camera. Do not integrate twice when a progress marker is applied.
                    arm.elbowLocal=Vector3.Lerp(arm.elbowLocal,elbowTarget,1f-Mathf.Exp(-Time.deltaTime*11f));
                    arm.elbowFrame=Time.frameCount;
                }
                Vector3 elbow=viewCamera.transform.TransformPoint(arm.elbowLocal);
                Vector3 foreStart=Vector3.Lerp(wrist,elbow,.15f),foreEnd=Vector3.Lerp(wrist,elbow,.88f);
                ArmSegment(arm.wrist,wrist,foreStart,.061f,.049f);
                ArmSegment(arm.forearm,foreStart,foreEnd,.080f,.067f);
                ArmSegment(arm.sleeve,Vector3.Lerp(wrist,elbow,.79f),elbow,.099f,.087f);
                ArmSegment(arm.hem,Vector3.Lerp(wrist,elbow,.78f),Vector3.Lerp(wrist,elbow,.84f),.104f,.091f);
            }
        }

        void ArmSegment(Transform part,Vector3 start,Vector3 end,float width,float height)
        {
            Vector3 direction=end-start;
            part.SetPositionAndRotation((start+end)*.5f,Quaternion.LookRotation(direction,viewCamera.transform.up));
            part.localScale=new Vector3(width,height,Mathf.Max(.005f,direction.magnitude));
        }

        GameObject BuildFlash(Transform parent,float scale)
        {
            var flash=Node("Short pressure cone muzzle flash",parent,Vector3.zero);
            Flame("White hot pressure diamond",flash,Vector3.forward*.07f*scale,.022f*scale,.21f*scale,muzzleMaterial);
            for(int i=0;i<5;i++)
            {
                float a=i*Mathf.PI*2f/5f;
                var petal=Flame("Thin combustion petal",flash,new Vector3(Mathf.Cos(a)*.031f,Mathf.Sin(a)*.031f,.055f)*scale,.014f*scale,.14f*scale,tracerMaterial);
                petal.localRotation=Quaternion.Euler(-Mathf.Sin(a)*24f,Mathf.Cos(a)*24f,a*Mathf.Rad2Deg);
            }
            flash.gameObject.SetActive(false);return flash.gameObject;
        }

        Transform Flame(string name,Transform parent,Vector3 position,float radius,float length,Material material)
        {
            var vertices=new List<Vector3>();var triangles=new List<int>();var uv=new List<Vector2>();
            for(int i=0;i<4;i++)
            {
                float a=i*Mathf.PI*.5f,b=(i+1)*Mathf.PI*.5f;
                var p0=new Vector3(Mathf.Cos(a)*radius,Mathf.Sin(a)*radius,0f);
                var p1=new Vector3(Mathf.Cos(b)*radius,Mathf.Sin(b)*radius,0f);
                Triangle(vertices,triangles,uv,p1,p0,Vector3.back*length*.3f);
                Triangle(vertices,triangles,uv,p0,p1,Vector3.forward*length*.7f);
            }
            var part=MeshPart(name,parent,MakeMesh(name,vertices,triangles,uv),material);part.transform.localPosition=position;return part.transform;
        }

        Transform Cartridge(string name,Transform parent,Vector3 position)
        {
            var round=Node(name,parent,position);
            Tube("Red cartridge hull",round,Vector3.zero,.018f,.063f,shellRed,0f,12);
            Tube("Brass twelve gauge rim",round,new Vector3(0f,0f,-.035f),.0205f,.01f,brass,0f,12);
            Tube("Recessed primer",round,new Vector3(0f,0f,-.0405f),.006f,.0015f,edge,0f,8);
            return round;
        }

        void BuildSmoke()
        {
            smokeMaterial=new Material(Shader.Find("HDRP/Unlit"));smokeMaterial.name="Weapon smoke // translucent wisps";
            smokeMaterial.SetColor("_UnlitColor",new Color(.33f,.35f,.37f,.13f));
            smokeMaterial.SetFloat("_SurfaceType",1f);smokeMaterial.SetFloat("_BlendMode",0f);
            smokeMaterial.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);smokeMaterial.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);
            smokeMaterial.SetFloat("_AlphaSrcBlend",(float)BlendMode.One);smokeMaterial.SetFloat("_AlphaDstBlend",(float)BlendMode.OneMinusSrcAlpha);
            smokeMaterial.SetFloat("_ZWrite",0f);smokeMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");smokeMaterial.renderQueue=3000;
            var v=new List<Vector3>();var tr=new List<int>();var uv=new List<Vector2>();
            for(int i=0;i<6;i++)
            {
                float a=i*Mathf.PI/3f,b=(i+1)*Mathf.PI/3f;
                Vector3 p0=new Vector3(Mathf.Cos(a),0f,Mathf.Sin(a)),p1=new Vector3(Mathf.Cos(b),0f,Mathf.Sin(b));
                Triangle(v,tr,uv,Vector3.up,p1,p0);Triangle(v,tr,uv,Vector3.down,p0,p1);
            }
            smokeMesh=MakeMesh("Small drifting pressure haze",v,tr,uv);ownedMeshes.Add(smokeMesh);
        }

        void MuzzleSmoke(Transform muzzle,bool shotgun)
        {
            var puff=new GameObject("Short drifting gun smoke");puff.layer=2;puff.transform.position=muzzle.position+muzzle.forward*.11f;
            puff.transform.localScale=Vector3.one*(shotgun?.032f:.019f);
            puff.AddComponent<MeshFilter>().sharedMesh=smokeMesh;
            var renderer=puff.AddComponent<MeshRenderer>();renderer.sharedMaterial=smokeMaterial;renderer.shadowCastingMode=ShadowCastingMode.Off;
            var drift=puff.AddComponent<SmokeWisp>();drift.velocity=muzzle.forward*.46f+Vector3.up*.23f;drift.size=shotgun?.032f:.019f;
        }

        public sealed class SmokeWisp : MonoBehaviour
        {
            public Vector3 velocity;public float size;float age;
            Renderer target;MaterialPropertyBlock properties;
            void Awake()
            {
                target=GetComponent<Renderer>();properties=new MaterialPropertyBlock();
                // Lifetime cleanup is independent of the renderer and animation update path.
                Destroy(gameObject,.26f);
            }
            void Update()
            {
                age+=Time.deltaTime;float t=Mathf.Clamp01(age/.24f);
                if(t>=1f){Destroy(gameObject);return;}
                transform.position+=velocity*Time.deltaTime;transform.localScale=Vector3.one*size*(1f+t*2.1f);
                if(!target||properties==null)return;
                properties.SetColor("_UnlitColor",new Color(.33f,.35f,.37f,.13f*(1f-t)));target.SetPropertyBlock(properties);
            }
        }

        void BuildKatana()
        {
            swordRoot=Node("KATANA // human two hand execution rig",viewRoot,Vector3.zero);
            Tube("Black samegawa handle core",swordRoot,new Vector3(0f,0f,-.13f),.021f,.278f,rubber,0f,12);
            for(int i=0;i<12;i++)
            {
                var a=Bevel("Crossed black silk tsuka wrap",swordRoot,new Vector3(0f,.019f,-.252f+i*.022f),new Vector3(.038f,.005f,.012f),glove,.14f);a.transform.localRotation=Quaternion.Euler(0f,31f,0f);
                var b=Bevel("Reverse silk wrap",swordRoot,new Vector3(0f,-.019f,-.252f+i*.022f),new Vector3(.038f,.005f,.012f),clothStitch,.14f);b.transform.localRotation=Quaternion.Euler(0f,-31f,0f);
            }
            Tube("Brass kashira pommel",swordRoot,new Vector3(0f,0f,-.278f),.022f,.014f,brass,0f,12);
            Tube("Brass fuchi collar",swordRoot,new Vector3(0f,0f,.003f),.023f,.019f,brass,0f,12);
            var guard=Tube("Dark iron oval tsuba",swordRoot,new Vector3(0f,0f,.023f),.059f,.008f,metal,.018f,16);guard.transform.localScale=new Vector3(1f,.72f,1f);
            var rim=Tube("Tsuba fine brass rim",swordRoot,new Vector3(0f,0f,.024f),.06f,.009f,brass,.055f,16);rim.transform.localScale=new Vector3(1f,.72f,1f);
            Bevel("Brass habaki blade collar",swordRoot,new Vector3(.003f,0f,.052f),new Vector3(.045f,.016f,.045f),brass,.13f);
            BuildBlade();
            bladeTip=Node("Kissaki // trail tip",swordRoot,new Vector3(.087f,0f,1.06f));
            bladeTrail=bladeTip.gameObject.AddComponent<TrailRenderer>();bladeTrail.sharedMaterial=IPArt.Mat("Katana // restrained silver motion edge",new Color(.63f,.72f,.76f),.7f);
            bladeTrail.time=.065f;bladeTrail.minVertexDistance=.025f;bladeTrail.startWidth=.065f;bladeTrail.endWidth=.002f;
            bladeTrail.startColor=new Color(.76f,.83f,.85f,.27f);bladeTrail.endColor=new Color(.49f,.6f,.65f,0f);
            bladeTrail.numCornerVertices=2;bladeTrail.numCapVertices=2;bladeTrail.shadowCastingMode=ShadowCastingMode.Off;bladeTrail.emitting=false;
            swordRightHand=Node("Katana right human hand",swordRoot,swordRightHome);BuildHumanHand(swordRightHand,false);swordRightHand.localRotation=Quaternion.Euler(0f,0f,-12f);
            // The bare left hand is independent of the sword. It can hold a victim in world space,
            // open its fingers, punch, or return to the handle without inheriting blade rotation.
            swordLeftHand=Node("Independent bare left combat hand",viewRoot,Vector3.zero);
            combatArm=BuildHumanHand(swordLeftHand,true,true,true);
            swordLeftHand.gameObject.SetActive(false);
            swordRoot.gameObject.SetActive(false);
        }

        void BuildBlade()
        {
            const int divisions=13;
            var flatV=new List<Vector3>();var flatT=new List<int>();var flatUv=new List<Vector2>();
            var edgeV=new List<Vector3>();var edgeT=new List<int>();var edgeUv=new List<Vector2>();
            var lineV=new List<Vector3>();var lineT=new List<int>();var lineUv=new List<Vector2>();
            for(int i=0;i<divisions;i++)
            {
                float t0=(float)i/divisions,t1=(float)(i+1)/divisions;
                Vector3 c0=BladeCenter(t0),c1=BladeCenter(t1);float w0=BladeWidth(t0),w1=BladeWidth(t1);
                for(int side=-1;side<=1;side+=2)
                {
                    Vector3 a=c0+new Vector3(-w0*.42f,side*.004f,0f),b=c1+new Vector3(-w1*.42f,side*.004f,0f);
                    Vector3 c=c1+new Vector3(w1*.22f,side*.004f,0f),d=c0+new Vector3(w0*.22f,side*.004f,0f);
                    if(side>0)Quad(flatV,flatT,flatUv,a,b,c,d);else Quad(flatV,flatT,flatUv,d,c,b,a);
                    Vector3 sharp0=c0+new Vector3(w0*.58f,0f,0f),sharp1=c1+new Vector3(w1*.58f,0f,0f);
                    if(side>0)Quad(edgeV,edgeT,edgeUv,d,c,sharp1,sharp0);else Quad(edgeV,edgeT,edgeUv,sharp0,sharp1,c,d);
                    float wave0=Mathf.Sin(t0*52f)*.0019f,wave1=Mathf.Sin(t1*52f)*.0019f;
                    Vector3 h0=c0+new Vector3(w0*.16f+wave0,side*.0042f,0f),h1=c1+new Vector3(w1*.16f+wave1,side*.0042f,0f);
                    if(side>0)Quad(lineV,lineT,lineUv,h0,h1,h1+Vector3.right*.003f,h0+Vector3.right*.003f);
                    else Quad(lineV,lineT,lineUv,h0+Vector3.right*.003f,h1+Vector3.right*.003f,h1,h0);
                }
                Quad(flatV,flatT,flatUv,c0+new Vector3(-w0*.42f,-.004f,0f),c1+new Vector3(-w1*.42f,-.004f,0f),c1+new Vector3(-w1*.42f,.004f,0f),c0+new Vector3(-w0*.42f,.004f,0f));
            }
            MeshPart("Curved forged katana blade",swordRoot,MakeMesh("Katana blade",flatV,flatT,flatUv),bladeSteel);
            MeshPart("Continuous sharpened cutting bevel",swordRoot,MakeMesh("Katana bevel",edgeV,edgeT,edgeUv),bladeEdge);
            MeshPart("Subtle wavy hamon temper line",swordRoot,MakeMesh("Katana hamon",lineV,lineT,lineUv),bladeHamon);
        }
        static Vector3 BladeCenter(float t)=>new Vector3(.005f+t*t*.082f,0f,.077f+t*.983f);
        static float BladeWidth(float t)=>.045f*(t>.86f?Mathf.Max(.015f,(1f-t)/.14f):1f-t*.17f);

        public void Tick(float dt,Vector2 look,float movement,bool grounded,bool dashing)
        {
            if(viewRoot==null)return;
            dt=Mathf.Max(0f,dt);
            shotCooldown=Mathf.Max(0f,shotCooldown-dt);
            if(switchTime>0f)
            {
                switchTime=Mathf.Max(0f,switchTime-dt);
                if(pendingWeapon>=0&&switchTime<=SwitchDuration*.5f)
                {
                    reloadTime=-1f;HideReloadShells();CurrentWeapon=(IPWeaponKind)pendingWeapon;pendingWeapon=-1;SetWeaponVisibility();RefreshChambers();
                }
            }
            ShotPulse=Mathf.MoveTowards(ShotPulse,0f,dt*7f);flashTime=Mathf.Max(0f,flashTime-dt);
            for(int r=0;r<rigs.Length;r++)for(int i=0;i<rigs[r].flashes.Length;i++)
                rigs[r].flashes[i].SetActive(executionProgress<0f&&!IsMeleeAttacking&&r==(int)CurrentWeapon&&flashTime>0f&&i==lastBarrel);
            if(muzzleLight)
            {
                Transform muzzle=ActiveRig.muzzles[Mathf.Min(lastBarrel,ActiveRig.muzzles.Length-1)];
                muzzleLight.transform.position=muzzle.position-muzzle.forward*.04f;
                float duration=UsesReload?.055f:CurrentWeapon==IPWeaponKind.Pistol?.038f:.032f;
                muzzleLight.intensity=executionProgress<0f&&!IsMeleeAttacking?(UsesReload?1800f:CurrentWeapon==IPWeaponKind.Pistol?650f:900f)*Mathf.Clamp01(flashTime/duration):0f;
            }
            // Exact solution of a critically damped recoil spring. It stays stable at any frame rate
            // and an impulse changes velocity, never the weapon's position in a single frame.
            float omega=UsesReload?22f:CurrentWeapon==IPWeaponKind.Pistol?26f:29f;
            float j=recoilVelocity+omega*recoil,e=Mathf.Exp(-omega*dt);
            recoil=(recoil+j*dt)*e;recoilVelocity=(recoilVelocity-omega*j*dt)*e;
            DampedSpring(ref recoilPitch,ref recoilPitchVelocity,0f,27f,.72f,dt);
            DampedSpring(ref recoilRoll,ref recoilRollVelocity,0f,19f,.67f,dt);
            // Position, aim, wrist roll and breathing follow separate clocks. This overlap prevents
            // the viewmodel from translating like a single rigid object bolted to the camera.
            Vector2 aimTarget=Vector2.ClampMagnitude(look,28f)*.025f;
            float sx=sway.x,sy=sway.y,vx=swayVelocity.x,vy=swayVelocity.y;
            DampedSpring(ref sx,ref vx,aimTarget.x,13f,.83f,dt);
            DampedSpring(ref sy,ref vy,aimTarget.y,13f,.83f,dt);
            sway=new Vector2(sx,sy);swayVelocity=new Vector2(vx,vy);
            delayedSway=Vector2.Lerp(delayedSway,sway,1f-Mathf.Exp(-dt*6f));
            movementBlend=Mathf.Lerp(movementBlend,movement,1f-Mathf.Exp(-dt*8f));
            airborneBlend=Mathf.Lerp(airborneBlend,grounded?0f:1f,1f-Mathf.Exp(-dt*7f));
            dashBlend=Mathf.Lerp(dashBlend,dashing?1f:0f,1f-Mathf.Exp(-dt*14f));
            walkPhase+=dt*Mathf.Lerp(3.1f,9.8f,movementBlend)*(1f-airborneBlend*.62f);
            breathPhase+=dt*1.95f;
            float footfall=Mathf.Sin(walkPhase*2f+.3f);
            Vector3 bob=new Vector3(Mathf.Sin(walkPhase)*.0075f,(footfall+Mathf.Sin(walkPhase*4f)*.16f)*.004f,
                -Mathf.Cos(walkPhase*2f)*.003f)*movementBlend*(1f-airborneBlend*.72f);
            bob+=new Vector3(Mathf.Sin(breathPhase*.5f)*.0013f,Mathf.Sin(breathPhase)*.0022f,Mathf.Cos(breathPhase)*.001f)*(1f-movementBlend*.6f);
            float drawDip=switchTime>0f?Mathf.Pow(Mathf.Sin(Mathf.Clamp01(1f-switchTime/SwitchDuration)*Mathf.PI),2f):0f;
            Vector3 viewTarget=viewHome+bob+new Vector3(-sway.x*.006f,-sway.y*.006f-drawDip*.65f,-drawDip*.09f);
            Quaternion viewRotation=Quaternion.Euler(sway.y*.8f-dashBlend*5f+drawDip*30f+airborneBlend*1.6f,
                -sway.x*.7f-3f,(delayedSway.x-sway.x)*3.6f+Mathf.Sin(walkPhase+.24f)*movementBlend*.65f);
            viewRoot.localPosition=Vector3.Lerp(viewRoot.localPosition,viewTarget,1f-Mathf.Exp(-dt*24f));
            viewRoot.localRotation=Quaternion.Slerp(viewRoot.localRotation,viewRotation,1f-Mathf.Exp(-dt*20f));
            if(executionProgress>=0f){ApplyExecutionPose(executionProgress);return;}
            if(punchProgress>=0f){ApplyPunchPose(punchProgress);return;}
            if(ultimateProgress>=0f){ApplyUltimatePose(ultimateProgress);return;}
            WeaponRig rig=ActiveRig;
            float t=IsReloading?Mathf.Clamp01((reloadTime+dt)/ReloadDuration):0f;
            float reloadPose=IsReloading?Smooth(t/.13f)*(1f-Smooth((t-.89f)/.11f)):0f;
            float open=IsReloading?Smooth((t-.10f)/.21f)*(1f-Smooth((t-.77f)/.155f)):0f;
            // A tiny delayed latch compression gives the fast close physical weight, without shake.
            if(IsReloading)open+=Mathf.Sin(Mathf.Clamp01((t-.90f)/.10f)*Mathf.PI*2f)*.017f*(1f-Smooth((t-.90f)/.10f));
            rig.mechanism.localPosition=UsesReload?new Vector3(0f,-.013f,.075f):Vector3.back*recoil*(CurrentWeapon==IPWeaponKind.Pistol?.047f:.034f);
            rig.mechanism.localRotation=Quaternion.Euler(open*53f,0f,0f);
            rig.supportHand.localPosition=UsesReload?rig.root.InverseTransformPoint(rig.mechanism.TransformPoint(rig.supportOnMechanism)):rig.supportHome;
            rig.supportHand.localRotation=UsesReload?rig.mechanism.localRotation*rig.supportRotation:rig.supportRotation;
            rig.triggerHand.localPosition=rig.triggerHome+new Vector3(0f,-.004f,0f)*recoil;
            rig.triggerHand.localRotation=Quaternion.Euler(-recoil*1.2f-recoilPitch*.08f,0f,recoilRoll*.08f);
            if(rig.magazine!=null){rig.magazine.localPosition=rig.magazineHome;rig.magazine.localRotation=Quaternion.identity;rig.magazine.gameObject.SetActive(true);}
            rig.root.localPosition=new Vector3(-.082f*reloadPose,.026f*reloadPose,-recoil*(UsesReload?.11f:.049f)-.078f*reloadPose);
            float kick=UsesReload?12f:CurrentWeapon==IPWeaponKind.Pistol?14f:7f;
            rig.root.localRotation=Quaternion.Euler(-recoil*kick-recoilPitch-9f*reloadPose,-18f*reloadPose+recoilRoll*.14f,
                -24f*reloadPose+recoilRoll);
            if(actionRecovery>0f)
            {
                actionRecovery=Mathf.Max(0f,actionRecovery-dt);
                float restore=Smooth(1f-actionRecovery/.24f);
                rig.root.localPosition=Vector3.Lerp(recoveryRigPosition,rig.root.localPosition,restore);
                rig.root.localRotation=Quaternion.Slerp(recoveryRigRotation,rig.root.localRotation,restore);
            }
            if(IsReloading)
            {
                reloadTime+=dt;AnimateShotgunReload(rig,t);
                if(t>=1f)
                {
                    reloadTime=-1f;ammunition[2]=2;spentShells[0]=spentShells[1]=false;HideReloadShells();RefreshChambers();
                }
            }
            if(UsesReload&&LoadedShells==0&&!IsReloading&&shotCooldown<=0f&&owner!=null&&!owner.IsDead)Reload();
            UpdateArms();
        }

        static float Smooth(float value)
        {
            float t=Mathf.Clamp01(value);
            return t*t*t*(t*(t*6f-15f)+10f); // C2 continuous at both ends.
        }
        static void DampedSpring(ref float value,ref float velocity,float target,float frequency,float damping,float dt)
        {
            float w=frequency*Mathf.Sqrt(1f-damping*damping),d=damping*frequency;
            float a=value-target,b=(velocity+d*a)/w,e=Mathf.Exp(-d*dt),c=Mathf.Cos(w*dt),s=Mathf.Sin(w*dt);
            value=target+e*(a*c+b*s);
            velocity=e*((w*b-d*a)*c+(-w*a-d*b)*s);
        }
        static float Window(float t,float start,float end,float fade)
        {return Smooth((t-start)/fade)*(1f-Smooth((t-end)/fade));}
        static Vector3 Bezier(Vector3 a,Vector3 b,Vector3 c,float t)
        {float u=1f-t;return u*u*a+2f*u*t*b+t*t*c;}

        void AnimateShotgunReload(WeaponRig rig,float t)
        {
            Vector3 foregrip=rig.root.InverseTransformPoint(rig.mechanism.TransformPoint(rig.supportOnMechanism));
            Quaternion foreRotation=rig.mechanism.localRotation*rig.supportRotation;
            Vector3 belt=new Vector3(-.21f,-.40f,-.18f);
            Vector3 chamber=(rig.chambers[0].localPosition+rig.chambers[1].localPosition)*.5f;
            Vector3 seated=rig.mechanism.localPosition+rig.mechanism.localRotation*chamber;
            Vector3 approach=seated+rig.mechanism.localRotation*(Vector3.back*.155f);
            Vector3 roundCenter=t<.56f?Bezier(belt,new Vector3(-.20f,-.11f,-.19f),approach,Smooth((t-.37f)/.19f)):
                Vector3.Lerp(approach,seated,Smooth((t-.56f)/.18f));
            Quaternion roundRotation=Quaternion.Slerp(Quaternion.Euler(62f,-12f,-18f),rig.mechanism.localRotation,Smooth((t-.40f)/.16f));
            if(!shellsEjected&&t>=.28f)
            {
                shellsEjected=true;
                for(int i=0;i<2;i++){EjectShell(i);rig.chambers[i].gameObject.SetActive(false);}
                IPGame.Audio("reload_open",viewCamera.transform.position,.68f);
            }
            for(int i=0;i<2;i++)
            {
                bool show=t>=.34f&&t<.74f;
                reloadShells[i].gameObject.SetActive(show);
                if(show)
                {
                    reloadShells[i].localPosition=roundCenter+roundRotation*new Vector3(i==0?-.024f:.024f,0f,0f);
                    reloadShells[i].localRotation=roundRotation;
                }
            }
            Quaternion carryRotation=Quaternion.Slerp(Quaternion.Euler(-34f,13f,24f),rig.mechanism.localRotation*Quaternion.Euler(-8f,0f,-20f),Smooth((t-.40f)/.19f));
            Vector3 carry=roundCenter+roundRotation*new Vector3(-.047f,-.031f,-.052f);
            if(t<.37f)
            {
                float reach=Smooth((t-.25f)/.12f);
                Vector3 beltGrip=belt+Quaternion.Euler(62f,-12f,-18f)*new Vector3(-.047f,-.031f,-.052f);
                rig.supportHand.localPosition=Vector3.Lerp(foregrip,beltGrip,reach);
                rig.supportHand.localRotation=Quaternion.Slerp(foreRotation,Quaternion.Euler(-34f,13f,24f),reach);
            }
            else
            {
                float returnGrip=Smooth((t-.76f)/.20f);
                rig.supportHand.localPosition=Vector3.Lerp(carry,foregrip,returnGrip);
                rig.supportHand.localRotation=Quaternion.Slerp(carryRotation,foreRotation,returnGrip);
            }
            // Fresh models meet the chamber models at exactly the same pose before changing ownership.
            if(!shellsInserted&&t>=.74f)
            {
                shellsInserted=true;ammunition[2]=2;spentShells[0]=spentShells[1]=false;RefreshChambers();
                IPGame.Audio("reload_shells",viewCamera.transform.position,.75f);
            }
            if(!mechanismReleased&&t>=.92f)
            {mechanismReleased=true;IPGame.Audio("reload_close",viewCamera.transform.position,.72f);}
        }

        void HideReloadShells()
        {if(reloadShells!=null)foreach(var shell in reloadShells)if(shell)shell.gameObject.SetActive(false);}

        public void TryFire(bool force=false)
        {
            if(owner==null||owner.IsDead||owner.IsExecuting||IPGame.IsPaused||IsMeleeAttacking||IsReloading||shotCooldown>0f||LoadedShells<=0||switchTime>0f||executionProgress>=0f)return;
            if(CurrentWeapon==IPWeaponKind.Pistol&&!force&&(Mouse.current==null||!Mouse.current.leftButton.wasPressedThisFrame))return;
            WeaponRig rig=ActiveRig;bool shotgun=CurrentWeapon==IPWeaponKind.Shotgun;bool pistol=CurrentWeapon==IPWeaponKind.Pistol;
            int barrel=shotgun?(LoadedShells==2?0:1):0;
            if(shotgun){ammunition[2]--;spentShells[barrel]=true;}
            RefreshChambers();lastBarrel=barrel;ShotsFired++;
            shotCooldown=shotgun? .31f:pistol? .195f:.091f;
            recoilVelocity=Mathf.Min(94f,recoilVelocity+(shotgun?65f:pistol?47f:26f));
            recoilPitchVelocity+=shotgun?34f:pistol?30f:14f;
            recoilRollVelocity+=(lastBarrel==0?1f:-1f)*(shotgun?12f:pistol?8f:4f);
            ShotPulse=shotgun?1f:pistol? .72f:.48f;flashTime=shotgun?.055f:pistol?.038f:.032f;
            rig.flashes[barrel].transform.localRotation=Quaternion.Euler(0f,0f,Random.Range(0f,180f));
            // Tick precedes input/fire on the player. Show the pressure flash now so a shot is
            // visible on its firing frame even when the next frame exceeds the flash lifetime.
            for(int r=0;r<rigs.Length;r++)for(int i=0;i<rigs[r].flashes.Length;i++)
                rigs[r].flashes[i].SetActive(r==(int)CurrentWeapon&&i==barrel);
            if(muzzleLight)
            {
                muzzleLight.transform.position=rig.muzzles[barrel].position-rig.muzzles[barrel].forward*.04f;
                muzzleLight.intensity=shotgun?1800f:pistol?650f:900f;
            }
            owner.KickCamera(shotgun?3.9f:pistol?1.6f:.78f);
            IPGame.ShotFeedback(CurrentWeapon);
            IPGame.Audio(shotgun?"shotgun":pistol?"pistol_fire":"rifle_fire",viewCamera.transform.position,shotgun? .9f:pistol? .65f:.52f);
            if(!shotgun)EjectBrass(pistol);
            MuzzleSmoke(rig.muzzles[barrel],shotgun);
            int pellets=shotgun?14:1;
            var hitEnemies=new Dictionary<IPEnemy,ShotDamage>();bool confirmed=false;
            for(int i=0;i<pellets;i++)
            {
                Vector2 spread=Vector2.zero;
                if(shotgun){float radius=Mathf.Sqrt((i+.25f)/pellets)*.048f,angle=i*2.399963f+Random.Range(-.1f,.1f);spread=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*radius;}
                else if(!pistol)spread=Random.insideUnitCircle*(owner.IsGrounded? .0065f:.012f);
                Vector3 direction=(viewCamera.transform.forward+viewCamera.transform.right*spread.x+viewCamera.transform.up*spread.y).normalized;
                Vector3 end=viewCamera.transform.position+direction*100f;
                if(Physics.Raycast(viewCamera.transform.position,direction,out var hit,100f,~(1<<2),QueryTriggerInteraction.Ignore))
                {
                    end=hit.point;var enemy=hit.collider.GetComponentInParent<IPEnemy>();
                    if(enemy!=null)
                    {
                        float damage=shotgun?Mathf.Lerp(10f,3.3f,Mathf.InverseLerp(6f,30f,hit.distance)):pistol?29f:15f;
                        if(hitEnemies.TryGetValue(enemy,out var batch)){batch.damage+=damage;hitEnemies[enemy]=batch;}
                        else hitEnemies.Add(enemy,new ShotDamage{damage=damage,point=hit.point,direction=direction});confirmed=true;
                    }
                    if(i%4==0)IPGame.Burst(hit.point+hit.normal*.02f,enemy!=null?new Color(1f,.47f,.12f):new Color(.58f,.61f,.62f),shotgun?3:2,2.7f);
                }
                if(i%4==0)DrawTracer(rig.muzzles[barrel].position,end,shotgun);
            }
            foreach(var entry in hitEnemies)if(entry.Key!=null)entry.Key.ReceiveShot(entry.Value.damage,entry.Value.point,entry.Value.direction);
            if(confirmed)owner.ConfirmHit();
        }
        struct ShotDamage{public float damage;public Vector3 point,direction;}

        void DrawTracer(Vector3 start,Vector3 end,bool shotgun)
        {
            var trace=new GameObject("Momentary ballistic streak");var line=trace.AddComponent<LineRenderer>();line.sharedMaterial=tracerMaterial;
            line.positionCount=2;line.SetPosition(0,start);line.SetPosition(1,end);line.startWidth=shotgun? .009f:.005f;line.endWidth=.001f;
            line.startColor=new Color(1f,.81f,.52f,.6f);line.endColor=new Color(1f,.5f,.17f,0f);line.shadowCastingMode=ShadowCastingMode.Off;Destroy(trace,.027f);
        }

        public void Reload()
        {
            if(!UsesReload||owner==null||IsMeleeAttacking||IsReloading||LoadedShells>=2||executionProgress>=0f||owner.IsDead||IPGame.IsPaused||switchTime>0f)return;
            reloadTime=0f;shellsEjected=shellsInserted=mechanismReleased=false;IPGame.Audio("reload",viewCamera.transform.position,.48f);
        }

        void EjectBrass(bool pistol)
        {
            var casing=new GameObject(pistol?"Ejected 9 mm brass":"Ejected rifle brass");casing.layer=2;casing.transform.position=ActiveRig.root.TransformPoint(new Vector3(.057f,.02f,.035f));
            casing.transform.rotation=viewCamera.transform.rotation*Quaternion.Euler(0f,0f,90f);
            var cylinder=IPArt.Cylinder("Spent cartridge",casing.transform,Vector3.zero,new Vector3(pistol? .014f:.012f,pistol? .012f:.022f,pistol? .014f:.012f),brass);
            cylinder.layer=2;var body=casing.AddComponent<Rigidbody>();body.mass=.009f;body.linearVelocity=viewCamera.transform.right*2.6f+viewCamera.transform.up*1.6f-viewCamera.transform.forward*.5f;
            body.angularVelocity=Random.insideUnitSphere*25f;Destroy(casing,2f);
        }

        void EjectShell(int index)
        {
            var source=rigs[2].chambers[index];
            // Reuse the exact chamber meshes, avoiding new mesh allocations every reload.
            var shell=Instantiate(source.gameObject,source.position,source.rotation);
            shell.name=spentShells[index]?"Ejected spent twelve gauge shell":"Extracted twelve gauge cartridge";shell.layer=2;shell.SetActive(true);
            foreach(var renderer in shell.GetComponentsInChildren<Renderer>())renderer.shadowCastingMode=ShadowCastingMode.Off;
            var body=shell.AddComponent<Rigidbody>();body.mass=.025f;
            // The extractor propels each case backwards along its opened bore, then gravity takes over.
            body.linearVelocity=-source.forward*2.8f+viewCamera.transform.up*.7f+viewCamera.transform.right*(index==0?-.62f:.62f);
            body.angularVelocity=viewCamera.transform.right*(index==0?13f:-11f)+viewCamera.transform.up*7f;Destroy(shell,2.2f);
        }

        public void AddShells(int amount)
        {if(!UsesReload)return;ammunition[2]=Mathf.Clamp(ammunition[2]+amount,0,2);RefreshChambers();}

        public void ResetAmmo()
        {
            ammunition[0]=12;ammunition[1]=30;ammunition[2]=2;reloadTime=-1f;shotCooldown=0f;executionProgress=-1f;switchTime=0f;recoil=0f;recoilVelocity=0f;
            punchProgress=ultimateProgress=-1f;actionRecovery=0f;hasExecutionVictim=hasPunchVictim=false;
            recoilPitch=recoilPitchVelocity=recoilRoll=recoilRollVelocity=0f;
            pendingWeapon=-1;ShotsFired=0;spentShells[0]=spentShells[1]=false;HideReloadShells();
            CurrentWeapon=IPWeaponKind.Shotgun;if(viewRoot!=null){SetWeaponVisibility();RefreshChambers();if(bladeTrail){bladeTrail.emitting=false;bladeTrail.Clear();}}
        }

        public void SwitchWeapon(int index)
        {
            if(index<0||index>2||index==(int)CurrentWeapon||executionProgress>=0f||IsMeleeAttacking||owner==null||owner.IsDead||IPGame.IsPaused)return;
            if(switchTime>0f)return;
            pendingWeapon=index;shotCooldown=SwitchDuration;switchTime=SwitchDuration;flashTime=0f;
            IPGame.Audio("weapon_switch",viewCamera.transform.position,.34f);
        }
        public void CycleWeapon(int direction)=>SwitchWeapon(((int)CurrentWeapon+(direction>=0?1:2))%3);

        void RefreshChambers()
        {if(rigs[2]?.chambers==null)return;for(int i=0;i<2;i++)rigs[2].chambers[i].gameObject.SetActive(!IsReloading||!shellsEjected||shellsInserted);}

        void SetWeaponVisibility()
        {
            for(int i=0;i<rigs.Length;i++)if(rigs[i]!=null)
            {
                rigs[i].root.gameObject.SetActive(executionProgress<0f&&ultimateProgress<0f&&i==(int)CurrentWeapon);
                rigs[i].supportHand.gameObject.SetActive(punchProgress<0f);
            }
            if(swordRoot)swordRoot.gameObject.SetActive(executionProgress>=0f||ultimateProgress>=0f);
            if(swordLeftHand)swordLeftHand.gameObject.SetActive(executionProgress>=0f||IsMeleeAttacking);
            UpdateArms();
        }

        public void StartExecution()=>StartExecution(IPExecutionStyle.Frontal);
        public void StartExecution(IPExecutionStyle style)
        {
            CaptureCombatStart();punchProgress=ultimateProgress=-1f;actionRecovery=0f;
            executionStyle=style;executionProgress=0f;reloadTime=-1f;flashTime=0f;switchTime=0f;pendingWeapon=-1;
            executionViewStartPosition=viewRoot.localPosition;executionViewStartRotation=viewRoot.localRotation;
            HideReloadShells();BuildExecutionTrack();SetWeaponVisibility();RefreshChambers();
            if(bladeTrail){bladeTrail.emitting=false;bladeTrail.Clear();}IPGame.Audio("katana_draw",viewCamera.transform.position,.65f);
            ApplyExecutionPose(0f);
        }
        public void SetExecutionProgress(float progress)
        {
            float previous=executionProgress;executionProgress=Mathf.Clamp01(progress);
            bool backstep=executionStyle==IPExecutionStyle.BackstepCrush,twin=executionStyle==IPExecutionStyle.TwinArmBreak;
            float strike=executionStyle==IPExecutionStyle.Frontal? .49f:twin?.465f:.435f;
            if(previous<strike&&progress>=strike)IPGame.Audio("katana_swing",viewCamera.transform.position,.8f);
            if(backstep)
            {
                if(previous<.22f&&progress>=.22f)IPGame.Audio("execute_grab",viewCamera.transform.position,.38f);
                if(previous<.675f&&progress>=.675f)IPGame.Audio("melee_swing",viewCamera.transform.position,.48f);
            }
            else
            {
                if(previous<.285f&&progress>=.285f)IPGame.Audio("melee_swing",viewCamera.transform.position,.45f);
                if(twin&&previous<.375f&&progress>=.375f)IPGame.Audio("melee_swing",viewCamera.transform.position,.5f);
            }
            if(previous<.715f&&progress>=.715f)
                IPGame.Audio("execute_rip",viewCamera.transform.position,.65f);
            // Root calls this after its camera/victim step. Apply now as well as Tick so
            // hand contact and the victim pose are evaluated on the same rendered frame.
            ApplyExecutionPose(executionProgress);
        }
        public void SetExecutionVictim(Vector3 shoulder,Vector3 chest,Vector3 head)
        {executionShoulder=shoulder;executionChest=chest;executionHead=head;hasExecutionVictim=true;}
        public void EndExecution()
        {
            executionProgress=-1f;hasExecutionVictim=false;shotCooldown=Mathf.Max(shotCooldown,.19f);switchTime=0f;pendingWeapon=-1;
            BeginActionRecovery(true);SetWeaponVisibility();
            if(bladeTrail){bladeTrail.emitting=false;bladeTrail.Clear();}
        }

        void CaptureCombatStart()
        {
            WeaponRig rig=ActiveRig;
            freeHandStart=viewRoot.InverseTransformPoint(rig.supportHand.position);
            freeHandStartRotation=Quaternion.Inverse(viewRoot.rotation)*rig.supportHand.rotation;
            specialRigPosition=rig.root.localPosition;specialRigRotation=rig.root.localRotation;
            specialMechanismPosition=rig.mechanism.localPosition;specialMechanismRotation=rig.mechanism.localRotation;
            executionViewStartPosition=viewRoot.localPosition;executionViewStartRotation=viewRoot.localRotation;
        }
        void CancelFirearmAction()
        {
            reloadTime=-1f;flashTime=0f;switchTime=0f;pendingWeapon=-1;
            HideReloadShells();RefreshChambers();
            foreach(var rig in rigs)foreach(var flash in rig.flashes)flash.SetActive(false);
            if(muzzleLight)muzzleLight.intensity=0f;
        }
        void BeginActionRecovery(bool fromSword)
        {
            WeaponRig rig=ActiveRig;actionRecovery=.24f;
            recoveryRigPosition=fromSword?new Vector3(.035f,-.56f,-.12f):rig.root.localPosition;
            recoveryRigRotation=fromSword?Quaternion.Euler(26f,7f,-15f):rig.root.localRotation;
            rig.root.localPosition=recoveryRigPosition;rig.root.localRotation=recoveryRigRotation;
        }
        public void StartPunch()
        {
            if(viewRoot==null||executionProgress>=0f||IsMeleeAttacking)return;
            CaptureCombatStart();CancelFirearmAction();punchProgress=0f;actionRecovery=0f;hasPunchVictim=false;
            SetWeaponVisibility();ApplyPunchPose(0f);
        }
        public void SetPunchVictim(Vector3 worldPoint)
        {punchVictimPoint=worldPoint;hasPunchVictim=true;}
        public void SetPunchProgress(float progress)
        {
            if(punchProgress<0f)return;
            if(punchProgress<.22f&&progress>=.22f)IPGame.Audio("melee_swing",viewCamera.transform.position,.7f);
            punchProgress=Mathf.Clamp01(progress);ApplyPunchPose(punchProgress);
        }
        public void EndPunch()
        {
            if(punchProgress<0f)return;
            punchProgress=-1f;hasPunchVictim=false;shotCooldown=Mathf.Max(shotCooldown,.075f);BeginActionRecovery(false);SetWeaponVisibility();
        }
        public void StartUltimate()
        {
            if(viewRoot==null||executionProgress>=0f||IsMeleeAttacking)return;
            CaptureCombatStart();CancelFirearmAction();ultimateProgress=0f;actionRecovery=0f;
            BuildUltimateTrack();SetWeaponVisibility();
            if(bladeTrail){bladeTrail.emitting=false;bladeTrail.Clear();}
            IPGame.Audio("katana_draw",viewCamera.transform.position,.85f);ApplyUltimatePose(0f);
        }
        public void SetUltimateProgress(float progress)
        {
            if(ultimateProgress<0f)return;
            float previous=ultimateProgress;ultimateProgress=Mathf.Clamp01(progress);
            if((previous<.205f&&progress>=.205f)||(previous<.415f&&progress>=.415f)||(previous<.625f&&progress>=.625f))
                IPGame.Audio("katana_swing",viewCamera.transform.position,.95f);
            ApplyUltimatePose(ultimateProgress);
        }
        public void EndUltimate()
        {
            if(ultimateProgress<0f)return;
            ultimateProgress=-1f;shotCooldown=Mathf.Max(shotCooldown,.15f);BeginActionRecovery(true);SetWeaponVisibility();
            if(bladeTrail){bladeTrail.emitting=false;bladeTrail.Clear();}
        }

        struct SwordPose
        {
            public Vector3 position;public Quaternion rotation;
            public SwordPose(Vector3 p,Vector3 e){position=p;rotation=Quaternion.Euler(e);}
        }
        static SwordPose Mix(SwordPose a,SwordPose b,float t)
        {return new SwordPose{position=Vector3.LerpUnclamped(a.position,b.position,t),rotation=Quaternion.SlerpUnclamped(a.rotation,b.rotation,t)};}
        static SwordPose P(float x,float y,float z,float pitch,float yaw,float roll)=>new SwordPose(new Vector3(x,y,z),new Vector3(pitch,yaw,roll));

        // Tracks are built once per finisher, then evaluated without frame allocations. Position uses
        // time-aware Hermite tangents; orientation uses spherical cubic interpolation rather than Euler lerps.
        void BuildExecutionTrack()
        {
            SwordPose enter=P(.25f,-.64f,-.15f,-15f,-18f,-12f);
            SwordPose recovery=P(.27f,-.29f,.01f,-20f,22f,-32f);
            SwordPose exit=P(.28f,-.70f,-.17f,-20f,24f,-38f);
            SwordPose wind,contact,follow,withdraw;
            switch(executionStyle)
            {
                case IPExecutionStyle.BackstepCrush:
                    // The edge stays low through the knee sweep. Only after the cut does
                    // the sword clear the victim, leaving the bare hand to drive the skull down.
                    swordTimes=new[]{0f,.10f,.22f,.34f,.42f,.475f,.52f,.565f,.635f,.69f,.735f,.82f,.92f,1f};
                    swordPoses=new[]{enter,P(.29f,-.18f,.01f,-21f,36f,-23f),P(.39f,-.33f,.02f,24f,69f,-60f),
                        P(.42f,-.43f,-.035f,34f,75f,-71f),P(.40f,-.45f,.035f,29f,66f,-72f),
                        P(.27f,-.50f,.27f,25f,36f,-83f),P(-.10f,-.60f,.64f,30f,-12f,-96f),
                        P(-.49f,-.53f,.25f,38f,-98f,-112f),P(-.38f,-.34f,.06f,8f,-72f,-63f),
                        P(.29f,-.19f,.075f,-10f,35f,24f),P(.28f,-.12f,.11f,12f,34f,54f),
                        P(.31f,-.18f,.015f,-10f,28f,-23f),recovery,exit};
                    BuildSwordTangents();return;
                case IPExecutionStyle.TwinArmBreak:
                    // Two short guard-leverage arcs precede a separate neck-height cut.
                    // The reversal keys carry angular momentum through each break.
                    swordTimes=new[]{0f,.10f,.20f,.275f,.32f,.37f,.42f,.465f,.52f,.565f,.635f,.68f,.735f,.82f,.91f,1f};
                    swordPoses=new[]{enter,P(.26f,-.12f,.07f,-18f,29f,-28f),P(.31f,-.05f,.22f,13f,38f,-36f),
                        P(.23f,-.08f,.32f,18f,65f,-40f),P(.13f,-.20f,.38f,34f,-9f,-80f),
                        P(-.20f,-.07f,.19f,-14f,-48f,-52f),P(-.35f,-.23f,.41f,28f,-77f,-97f),
                        P(.38f,.26f,-.01f,-65f,50f,65f),P(-.04f,.13f,.64f,12f,-15f,91f),
                        P(-.43f,-.08f,.17f,24f,-95f,113f),P(-.34f,-.10f,.08f,4f,-65f,70f),
                        P(.30f,-.08f,.07f,-24f,49f,-26f),P(.32f,-.14f,.05f,-14f,41f,-36f),
                        P(.31f,-.15f,.02f,-19f,34f,-31f),recovery,exit};
                    BuildSwordTangents();return;
                case IPExecutionStyle.StrafeLeft:
                    wind=P(-.36f,.16f,.08f,-35f,-58f,-69f);contact=P(-.12f,-.04f,.44f,9f,2f,-94f);
                    follow=P(.40f,-.21f,.13f,24f,86f,-115f);withdraw=P(.31f,-.17f,.04f,22f,60f,-72f);break;
                case IPExecutionStyle.StrafeRight:
                    wind=P(.39f,.12f,.08f,-35f,62f,74f);contact=P(.02f,-.02f,.46f,11f,-8f,99f);
                    follow=P(-.42f,-.19f,.12f,22f,-88f,116f);withdraw=P(-.23f,-.13f,.05f,12f,-48f,63f);break;
                case IPExecutionStyle.DashThrust:
                    wind=P(.15f,-.03f,-.15f,0f,-13f,-9f);contact=P(-.10f,.015f,.73f,-1f,-2f,-14f);
                    follow=P(-.10f,-.02f,.61f,2f,-8f,-21f);withdraw=P(.31f,-.12f,.12f,-8f,43f,-50f);break;
                case IPExecutionStyle.RisingCut:
                    wind=P(.23f,-.55f,.04f,53f,19f,-48f);contact=P(-.11f,-.04f,.48f,-29f,-16f,-55f);
                    follow=P(-.26f,.34f,.15f,-111f,-39f,-44f);withdraw=P(.10f,.16f,.03f,-74f,13f,-15f);break;
                case IPExecutionStyle.AerialPlunge:
                    wind=P(.06f,.47f,.08f,-87f,3f,-5f);contact=P(-.13f,-.19f,.63f,67f,-4f,0f);
                    follow=P(-.13f,-.36f,.54f,74f,-2f,-7f);withdraw=P(-.07f,.09f,.17f,21f,7f,-14f);break;
                case IPExecutionStyle.RearCut:
                    wind=P(.37f,.10f,.02f,-8f,76f,85f);contact=P(-.08f,.08f,.53f,2f,-14f,91f);
                    follow=P(-.39f,.05f,.16f,-13f,-92f,96f);withdraw=P(.28f,-.15f,.07f,10f,67f,-65f);break;
                default:
                    // Bare hand tears the forearm first; the sword is held back until the
                    // second, much faster diagonal cut. Distinct wind-up/contact/overrun keys
                    // preserve momentum through contact rather than easing to a stop there.
                    swordTimes=new[]{0f,.11f,.24f,.355f,.405f,.485f,.58f,.625f,.68f,.735f,.80f,.90f,1f};
                    swordPoses=new[]{enter,P(.31f,-.13f,.06f,-24f,38f,38f),P(.32f,-.04f,.10f,-37f,45f,52f),
                        P(.35f,-.09f,.04f,-29f,50f,56f),P(.37f,.13f,-.04f,-63f,58f,69f),
                        P(.34f,.20f,.02f,-69f,48f,74f),P(-.08f,-.08f,.61f,22f,-28f,88f),
                        P(-.39f,-.29f,.18f,43f,-84f,106f),P(-.29f,-.16f,.07f,17f,-57f,59f),
                        P(-.31f,-.09f,.03f,12f,-62f,52f),P(.10f,-.23f,-.06f,-18f,18f,-28f),recovery,exit};
                    BuildSwordTangents();return;
            }
            SwordPose brace=Mix(enter,wind,.38f),loaded=wind;
            loaded.position+=new Vector3(.012f,-.012f,-.035f);
            loaded.rotation=loaded.rotation*Quaternion.Euler(-5f,3f,4f);
            swordTimes=new[]{0f,.12f,.26f,.37f,.435f,.52f,.565f,.625f,.695f,.755f,.84f,.925f,1f};
            swordPoses=new[]{enter,brace,wind,loaded,wind,contact,follow,Mix(follow,withdraw,.62f),
                withdraw,Mix(withdraw,recovery,.15f),Mix(withdraw,recovery,.63f),recovery,exit};
            BuildSwordTangents();
        }

        void BuildUltimateTrack()
        {
            swordTimes=new[]{0f,.10f,.165f,.205f,.25f,.285f,.345f,.385f,.415f,.46f,.50f,.555f,.60f,.625f,.67f,.71f,.77f,.86f,1f};
            swordPoses=new[]{P(.27f,-.66f,-.14f,-22f,-16f,-12f),P(-.23f,.06f,.05f,-36f,-60f,-62f),
                P(-.37f,.19f,.01f,-51f,-72f,-80f),P(-.35f,.17f,.04f,-46f,-67f,-78f),
                P(-.02f,.015f,.63f,13f,-2f,-95f),P(.46f,-.19f,.15f,24f,95f,-112f),
                P(.34f,.07f,-.02f,-36f,74f,67f),P(.42f,.20f,.025f,-52f,73f,82f),
                P(.39f,.18f,.05f,-45f,67f,78f),P(-.04f,-.025f,.63f,8f,-5f,97f),
                P(-.45f,-.24f,.16f,29f,-96f,121f),P(-.15f,.23f,-.03f,-89f,-39f,-48f),
                P(.19f,.39f,.025f,-106f,38f,-15f),P(.16f,.37f,.04f,-101f,32f,-18f),
                P(-.10f,-.10f,.67f,52f,-9f,-5f),P(-.22f,-.42f,.43f,77f,-23f,-23f),
                P(-.26f,-.25f,.04f,16f,-46f,-49f),P(.29f,-.18f,-.02f,-23f,38f,-39f),
                P(.30f,-.70f,-.18f,-23f,27f,-41f)};
            BuildSwordTangents();
        }

        static Quaternion Negate(Quaternion q)=>new Quaternion(-q.x,-q.y,-q.z,-q.w);
        static Vector3 Log(Quaternion q)
        {
            if(q.w<0f)q=Negate(q);float m=Mathf.Sqrt(q.x*q.x+q.y*q.y+q.z*q.z);
            return m<.000001f?Vector3.zero:new Vector3(q.x,q.y,q.z)*(Mathf.Atan2(m,q.w)/m);
        }
        static Quaternion Exp(Vector3 v)
        {
            float m=v.magnitude,k=m<.000001f?1f:Mathf.Sin(m)/m;
            return new Quaternion(v.x*k,v.y*k,v.z*k,Mathf.Cos(m));
        }
        void BuildSwordTangents()
        {
            swordTangents=new Quaternion[swordPoses.Length];
            for(int i=1;i<swordPoses.Length;i++)if(Quaternion.Dot(swordPoses[i-1].rotation,swordPoses[i].rotation)<0f)
                swordPoses[i].rotation=Negate(swordPoses[i].rotation);
            for(int i=0;i<swordPoses.Length;i++)
            {
                var q=swordPoses[i].rotation;var inv=Quaternion.Inverse(q);
                Vector3 control;
                if(i==0)control=Log(inv*swordPoses[1].rotation)*-.5f;
                else if(i==swordPoses.Length-1)control=Log(inv*swordPoses[i-1].rotation)*-.5f;
                else
                {
                    float before=swordTimes[i]-swordTimes[i-1],after=swordTimes[i+1]-swordTimes[i];
                    Vector3 previousLog=Log(inv*swordPoses[i-1].rotation),nextLog=Log(inv*swordPoses[i+1].rotation);
                    // SQUAD's outgoing tangent is (nextLog+2*control)/after, while its
                    // incoming tangent is (-previousLog-2*control)/before. This weighted
                    // control makes those equal in seconds, including unequal key spacing.
                    control=-(previousLog*after+nextLog*before)/(2f*(before+after));
                }
                swordTangents[i]=q*Exp(control);
            }
        }
        SwordPose EvaluateSword(float t)
        {
            int i=0;while(i<swordTimes.Length-2&&t>swordTimes[i+1])i++;
            int j=i+1,prev=Mathf.Max(0,i-1),next=Mathf.Min(swordTimes.Length-1,j+1);
            float span=swordTimes[j]-swordTimes[i],u=Mathf.Clamp01((t-swordTimes[i])/span),u2=u*u,u3=u2*u;
            Vector3 p=swordPoses[i].position,q=swordPoses[j].position;
            Vector3 m0=i==0?Vector3.zero:(q-swordPoses[prev].position)/(swordTimes[j]-swordTimes[prev])*span*.72f;
            Vector3 m1=j==swordPoses.Length-1?Vector3.zero:(swordPoses[next].position-p)/(swordTimes[next]-swordTimes[i])*span*.72f;
            Quaternion a=Quaternion.Slerp(swordPoses[i].rotation,swordPoses[j].rotation,u);
            Quaternion b=Quaternion.Slerp(swordTangents[i],swordTangents[j],u);
            return new SwordPose{position=(2f*u3-3f*u2+1f)*p+(u3-2f*u2+u)*m0+(-2f*u3+3f*u2)*q+(u3-u2)*m1,
                rotation=Quaternion.Slerp(a,b,2f*u*(1f-u))};
        }

        void ApplyExecutionPose(float t)
        {
            if(swordPoses==null)BuildExecutionTrack();
            SwordPose pose=EvaluateSword(t);
            float settle=Smooth(t/.17f);
            viewRoot.localPosition=Vector3.Lerp(executionViewStartPosition,viewHome,settle);
            viewRoot.localRotation=Quaternion.Slerp(executionViewStartRotation,Quaternion.identity,settle);
            float strike=executionStyle==IPExecutionStyle.Frontal?.58f:.52f;
            float contact=ContactRebound(t,strike,.40f),tear=ContactRebound(t,.735f,.55f);
            float shoulderLoad=Window(t,.08f,.37f,.12f);
            viewRoot.localPosition+=new Vector3(-.017f*shoulderLoad-.010f*tear,-.011f*contact,-.014f*contact+.016f*tear);
            viewRoot.localRotation*=Quaternion.Euler(contact*1.6f-tear*1.2f,shoulderLoad*1.5f,shoulderLoad*1.4f-tear*2.1f);
            if(executionStyle==IPExecutionStyle.BackstepCrush||executionStyle==IPExecutionStyle.TwinArmBreak)
            {
                float torque=executionStyle==IPExecutionStyle.TwinArmBreak?
                    ContactRebound(t,.32f,.18f)-ContactRebound(t,.42f,.18f):ContactRebound(t,.735f,.22f);
                viewRoot.localRotation*=Quaternion.Euler(torque*1.1f,torque*1.2f,-torque*1.6f);
            }
            swordRoot.localPosition=pose.position;swordRoot.localRotation=pose.rotation;
            swordRightHand.localPosition=swordRightHome;swordRightHand.localRotation=Quaternion.Euler(0f,0f,-12f);
            Vector3 shoulder=hasExecutionVictim?viewRoot.InverseTransformPoint(executionShoulder):new Vector3(-.30f,.01f,.65f);
            Vector3 chest=hasExecutionVictim?viewRoot.InverseTransformPoint(executionChest):new Vector3(-.18f,-.03f,.70f);
            Vector3 head=hasExecutionVictim?viewRoot.InverseTransformPoint(executionHead):new Vector3(-.16f,.32f,.65f);
            if(executionStyle==IPExecutionStyle.BackstepCrush||executionStyle==IPExecutionStyle.TwinArmBreak)
            {
                ApplyBrutalityHand(t,shoulder,chest,head);
                bool lowSweep=executionStyle==IPExecutionStyle.BackstepCrush;
                if(bladeTrail)bladeTrail.emitting=lowSweep?t>.435f&&t<.60f:t>.465f&&t<.60f;
                UpdateArms();return;
            }
            bool frontal=executionStyle==IPExecutionStyle.Frontal;
            handTimes[4]=frontal?.355f:.31f;
            bool side=executionStyle==IPExecutionStyle.StrafeLeft||executionStyle==IPExecutionStyle.StrafeRight;
            Vector3 firstPull=shoulder+new Vector3(-.28f,-.15f,-.30f);
            Vector3 secondTarget=side||executionStyle==IPExecutionStyle.RearCut?head:chest;
            Vector3 finalPull=secondTarget+new Vector3(side?.31f:-.31f,-.21f,-.35f);
            Quaternion grab=Quaternion.Euler(53f,-8f,-24f),headGrip=Quaternion.Euler(64f,8f,-48f);
            handPoses[0]=new SwordPose{position=freeHandStart,rotation=freeHandStartRotation};
            handPoses[1]=P(-.34f,-.07f,.17f,-3f,-29f,-25f);
            handPoses[2]=new SwordPose{position=shoulder,rotation=grab};
            handPoses[3]=new SwordPose{position=shoulder,rotation=grab*Quaternion.Euler(0f,0f,-5f)};
            // A very short pull follows a long readable grip. The detached forearm travels
            // with this bare hand while the right hand winds the sword independently.
            handPoses[4]=new SwordPose{position=frontal?firstPull:chest+new Vector3(-.02f,-.01f,.025f),rotation=Quaternion.Euler(frontal?18f:8f,-28f,-69f)};
            handPoses[5]=P(-.39f,-.20f,.21f,-13f,-32f,-36f);
            handPoses[6]=new SwordPose{position=secondTarget+new Vector3(-.045f,-.03f,-.02f),rotation=headGrip};
            handPoses[7]=new SwordPose{position=secondTarget,rotation=headGrip*Quaternion.Euler(-4f,-8f,-6f)};
            handPoses[8]=new SwordPose{position=finalPull,rotation=Quaternion.Euler(12f,-46f,-72f)};
            handPoses[9]=P(-.38f,-.18f,.16f,-9f,-26f,-27f);
            handPoses[10]=P(-.24f,-.49f,-.12f,-12f,-18f,-21f);
            SwordPose left=EvaluateHand(handTimes,handPoses,t);
            swordLeftHand.localPosition=left.position;swordLeftHand.localRotation=left.rotation;
            float grip=Window(t,.16f,.37f,.065f)+Window(t,.545f,.78f,.065f);
            float fist=Window(t,.285f,.405f,.035f)*(frontal?0f:1f);
            SetCombatGrip(Mathf.Clamp01(grip+fist),fist);
            // An overhead plunge uses a momentary two-handed brace, with the free hand
            // returning to the victim before the secondary tear.
            if(executionStyle==IPExecutionStyle.AerialPlunge||executionStyle==IPExecutionStyle.DashThrust)
            {
                float brace=Window(t,.39f,.53f,.045f);
                swordLeftHand.position=Vector3.Lerp(swordLeftHand.position,swordRoot.TransformPoint(swordLeftHome),brace);
                swordLeftHand.rotation=Quaternion.Slerp(swordLeftHand.rotation,swordRoot.rotation*Quaternion.Euler(0f,0f,18f),brace);
                SetCombatGrip(Mathf.Max(grip,brace),fist);
            }
            bool slashWindow=t>strike-.105f&&t<strike+.085f;
            if(bladeTrail)bladeTrail.emitting=slashWindow;
            UpdateArms();
        }

        void ApplyBrutalityHand(float t,Vector3 shoulder,Vector3 chest,Vector3 head)
        {
            // Both tracks are evaluated from live skeleton anchors. In particular the hand
            // stays on the descending skull instead of pushing at its old standing position.
            brutalityHandPoses[0]=new SwordPose{position=freeHandStart,rotation=freeHandStartRotation};
            brutalityHandPoses[1]=P(-.32f,-.10f,.16f,-6f,-25f,-29f);
            Vector3 crownUp=head-chest;
            crownUp=crownUp.sqrMagnitude>.001f?crownUp.normalized:Vector3.up;
            if(executionStyle==IPExecutionStyle.BackstepCrush)
            {
                Vector3 shoulderGrip=shoulder+new Vector3(-.025f,.025f,-.13f);
                Vector3 skullGrip=head+crownUp*.17f+Vector3.back*.12f;
                Quaternion crown=Quaternion.Euler(108f,-16f,-31f);
                brutalityHandPoses[2]=new SwordPose{position=shoulderGrip,rotation=Quaternion.Euler(55f,-18f,-30f)};
                brutalityHandPoses[3]=new SwordPose{position=skullGrip+new Vector3(-.025f,.045f,-.035f),rotation=crown};
                brutalityHandPoses[4]=new SwordPose{position=skullGrip,rotation=crown};
                brutalityHandPoses[5]=new SwordPose{position=skullGrip,rotation=crown*Quaternion.Euler(0f,-3f,-3f)};
                brutalityHandPoses[6]=new SwordPose{position=skullGrip+Vector3.back*.012f,rotation=crown*Quaternion.Euler(-2f,-5f,-5f)};
                brutalityHandPoses[7]=new SwordPose{position=skullGrip,rotation=crown*Quaternion.Euler(-4f,-5f,-7f)};
                brutalityHandPoses[8]=new SwordPose{position=skullGrip,rotation=crown*Quaternion.Euler(-6f,-8f,-8f)};
                brutalityHandPoses[9]=new SwordPose{position=skullGrip+Vector3.back*.025f,rotation=crown*Quaternion.Euler(-9f,-12f,-9f)};
                brutalityHandPoses[10]=new SwordPose{position=skullGrip,rotation=crown*Quaternion.Euler(-17f,-14f,-12f)};
                brutalityHandPoses[11]=new SwordPose{position=skullGrip-crownUp*.025f,rotation=crown*Quaternion.Euler(-23f,-15f,-14f)};
                brutalityHandPoses[12]=new SwordPose{position=skullGrip+new Vector3(-.11f,.065f,-.17f),rotation=Quaternion.Euler(36f,-32f,-55f)};
                brutalityHandPoses[13]=P(-.31f,-.28f,.13f,2f,-28f,-31f);
                SetCombatGrip(Window(t,.17f,.77f,.075f));
            }
            else
            {
                Vector3 first=shoulder+new Vector3(-.018f,-.15f,-.13f);
                Vector3 opposite=new Vector3(chest.x*2f-shoulder.x,shoulder.y,shoulder.z);
                Vector3 second=opposite+new Vector3(.025f,-.13f,-.13f);
                Vector3 neck=head+new Vector3(-.045f,-.06f,-.22f);
                brutalityHandPoses[2]=new SwordPose{position=first,rotation=Quaternion.Euler(58f,-9f,-28f)};
                brutalityHandPoses[3]=new SwordPose{position=first,rotation=Quaternion.Euler(52f,-12f,-40f)};
                brutalityHandPoses[4]=new SwordPose{position=first+new Vector3(-.21f,-.11f,-.20f),rotation=Quaternion.Euler(20f,-35f,-78f)};
                brutalityHandPoses[5]=new SwordPose{position=second+new Vector3(.04f,.025f,-.04f),rotation=Quaternion.Euler(57f,25f,15f)};
                brutalityHandPoses[6]=new SwordPose{position=second+new Vector3(.15f,-.13f,-.19f),rotation=Quaternion.Euler(17f,38f,62f)};
                brutalityHandPoses[7]=new SwordPose{position=neck,rotation=Quaternion.Euler(61f,-7f,-43f)};
                brutalityHandPoses[8]=new SwordPose{position=neck,rotation=Quaternion.Euler(58f,-14f,-49f)};
                brutalityHandPoses[9]=new SwordPose{position=neck+new Vector3(-.035f,.06f,-.035f),rotation=Quaternion.Euler(48f,-29f,-58f)};
                brutalityHandPoses[10]=new SwordPose{position=neck+new Vector3(-.07f,.11f,-.055f),rotation=Quaternion.Euler(29f,-43f,-68f)};
                brutalityHandPoses[11]=new SwordPose{position=neck+new Vector3(-.23f,.14f,-.22f),rotation=Quaternion.Euler(13f,-61f,-79f)};
                brutalityHandPoses[12]=P(-.43f,-.12f,.24f,-13f,-35f,-58f);
                brutalityHandPoses[13]=P(-.33f,-.26f,.10f,-9f,-24f,-30f);
                float lockGrip=Window(t,.16f,.455f,.055f),neckGrip=Window(t,.465f,.77f,.045f);
                SetCombatGrip(Mathf.Clamp01(lockGrip+neckGrip));
            }
            brutalityHandPoses[14]=P(-.24f,-.49f,-.12f,-12f,-18f,-21f);
            SwordPose left=EvaluateHand(brutalityHandTimes,brutalityHandPoses,t);
            swordLeftHand.localPosition=left.position;swordLeftHand.localRotation=left.rotation;
        }

        // One controlled, low-frequency recoil lobe. There is no random frame noise or
        // position discontinuity, even when a slow frame crosses an impact marker.
        static float ContactRebound(float t,float contact,float width)
        {
            float age=(t-contact)/width;
            return age<=0f||age>=1f?0f:Mathf.Sin(age*Mathf.PI*2f)*Mathf.Exp(-age*4.7f)*3.1f;
        }
        static SwordPose EvaluateHand(float[] times,SwordPose[] poses,float t)
        {
            int i=0;while(i<times.Length-2&&t>times[i+1])i++;
            int j=i+1,p=Mathf.Max(0,i-1),n=Mathf.Min(times.Length-1,j+1);
            float span=times[j]-times[i],u=Mathf.Clamp01((t-times[i])/span),u2=u*u,u3=u2*u;
            Vector3 a=poses[i].position,b=poses[j].position;
            Vector3 m0=i==0?Vector3.zero:(b-poses[p].position)/(times[j]-times[p])*span*.60f;
            Vector3 m1=j==times.Length-1?Vector3.zero:(poses[n].position-a)/(times[n]-times[i])*span*.60f;
            return new SwordPose{position=(2f*u3-3f*u2+1f)*a+(u3-2f*u2+u)*m0+(-2f*u3+3f*u2)*b+(u3-u2)*m1,
                rotation=Quaternion.Slerp(poses[i].rotation,poses[j].rotation,Smooth(u))};
        }
        void ApplyPunchPose(float t)
        {
            WeaponRig rig=ActiveRig;
            float lower=Smooth(t/.22f),close=Smooth(t/.18f);
            rig.root.localPosition=Vector3.Lerp(specialRigPosition,new Vector3(.065f,-.42f,-.12f),lower);
            rig.root.localRotation=Quaternion.Slerp(specialRigRotation,Quaternion.Euler(25f,10f,-19f),lower);
            rig.mechanism.localPosition=Vector3.Lerp(specialMechanismPosition,UsesReload?new Vector3(0f,-.013f,.075f):Vector3.zero,close);
            rig.mechanism.localRotation=Quaternion.Slerp(specialMechanismRotation,Quaternion.identity,close);
            punchPoses[0]=new SwordPose{position=freeHandStart,rotation=freeHandStartRotation};
            punchPoses[1]=P(-.33f,-.10f,-.025f,12f,-25f,-70f);
            punchPoses[2]=P(-.38f,-.05f,-.08f,15f,-32f,-85f);
            punchPoses[3]=P(-.18f,.14f,.54f,3f,-8f,-87f);
            punchPoses[4]=P(-.14f,.12f,.60f,13f,-4f,-95f);
            if(hasPunchVictim)
            {
                Vector3 towardSurface=(punchVictimPoint-viewCamera.transform.position).normalized;
                Vector3 fistCenter=punchVictimPoint-towardSurface*.08f;
                punchPoses[3].position=viewRoot.InverseTransformPoint(fistCenter);
                // Impact compression stays on the player's side of the collider, then the
                // shoulder and elbow unwind. Never bury the enlarged knuckles in armor.
                punchPoses[4].position=viewRoot.InverseTransformPoint(fistCenter-towardSurface*.012f+viewCamera.transform.up*-.012f);
            }
            punchPoses[5]=P(-.32f,.02f,.30f,12f,-31f,-68f);
            punchPoses[6]=P(-.32f,-.22f,.07f,-10f,0f,-34f);
            punchPoses[7]=new SwordPose{position=viewRoot.InverseTransformPoint(rig.supportHand.position),
                rotation=Quaternion.Inverse(viewRoot.rotation)*rig.supportHand.rotation};
            SwordPose left=EvaluateHand(punchTimes,punchPoses,t);
            swordLeftHand.localPosition=left.position;swordLeftHand.localRotation=left.rotation;
            float settle=Window(t,.0f,.82f,.14f);
            SetCombatGrip(Mathf.Lerp(.75f,1f,settle),settle);
            // Opposite shoulder lags behind the jab rather than moving every mesh together.
            rig.root.localRotation*=Quaternion.Euler(ContactRebound(t,.34f,.35f)*2.5f,0f,-Window(t,.14f,.46f,.12f)*3.5f);
            UpdateArms();
        }
        void ApplyUltimatePose(float t)
        {
            SwordPose pose=EvaluateSword(t);
            float settle=Smooth(t/.13f),impact=ContactRebound(t,.25f,.20f)+ContactRebound(t,.46f,.20f)+ContactRebound(t,.67f,.25f);
            viewRoot.localPosition=Vector3.Lerp(executionViewStartPosition,viewHome,settle)+new Vector3(0f,-impact*.009f,-impact*.013f);
            viewRoot.localRotation=Quaternion.Slerp(executionViewStartRotation,Quaternion.identity,settle)*Quaternion.Euler(impact*1.2f,0f,-impact*1.5f);
            swordRoot.localPosition=pose.position;swordRoot.localRotation=pose.rotation;
            swordRightHand.localPosition=swordRightHome;swordRightHand.localRotation=Quaternion.Euler(0f,0f,-12f);
            Vector3 handle=viewRoot.InverseTransformPoint(swordRoot.TransformPoint(swordLeftHome));
            Quaternion grip=Quaternion.Inverse(viewRoot.rotation)*swordRoot.rotation*Quaternion.Euler(0f,0f,18f);
            // The left hand closes a few frames after the right draws the blade, then
            // releases for the second cross-cut and catches the hilt for the overhead blow.
            float twoHands=Smooth(t/.14f)*(1f-Window(t,.31f,.50f,.06f))*(1f-Smooth((t-.83f)/.14f));
            Vector3 open=Vector3.Lerp(new Vector3(-.36f,-.12f,.22f),new Vector3(-.28f,-.54f,-.14f),Smooth((t-.78f)/.22f));
            if(t<.14f)open=freeHandStart;
            swordLeftHand.localPosition=Vector3.Lerp(open,handle,twoHands);
            swordLeftHand.localRotation=Quaternion.Slerp(Quaternion.Euler(13f,-32f,-37f),grip,twoHands);
            SetCombatGrip(Mathf.Lerp(.35f,.98f,twoHands));
            if(bladeTrail)bladeTrail.emitting=(t>.205f&&t<.305f)||(t>.415f&&t<.515f)||(t>.625f&&t<.74f);
            UpdateArms();
        }

        void OnDestroy()
        {foreach(var mesh in ownedMeshes)if(mesh)Destroy(mesh);ownedMeshes.Clear();if(smokeMaterial)Destroy(smokeMaterial);}
    }
}

