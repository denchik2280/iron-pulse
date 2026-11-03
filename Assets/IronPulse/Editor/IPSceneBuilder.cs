using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;

namespace IronPulse.Editor
{
    public static class IPSceneBuilder
    {
        public const string ScenePath="Assets/IronPulse/Scenes/IronPulse_Arena.unity";
        [MenuItem("Iron Pulse/Upgrade Arena Lighting")]
        public static void Upgrade()
        {
            if(EditorApplication.isPlaying)return;
            var scene=EditorSceneManager.GetActiveScene();
            if(scene.path!=ScenePath)scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
            var game=Object.FindFirstObjectByType<IPGame>();
            if(!game)throw new System.InvalidOperationException("IRON PULSE game root is missing.");
            IPWorld.AddDetail(game.transform.Find("World"));
            foreach(string guid in AssetDatabase.FindAssets("t:Material",new[]{"Assets/IronPulse/Materials"}))
            {
                var material=AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if(material){IPArt.ApplySurface(material);EditorUtility.SetDirty(material);}
            }
            foreach(var renderer in game.GetComponentsInChildren<Renderer>(true))
            {
                bool solid=renderer.sharedMaterial&&renderer.sharedMaterial.shader&&renderer.sharedMaterial.shader.name=="HDRP/Lit";
                renderer.shadowCastingMode=solid?ShadowCastingMode.On:ShadowCastingMode.Off;renderer.receiveShadows=solid;
            }
            IPGame.ConfigureAtmosphere();IPEnvironment.Configure();
            foreach(var camera in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))IPGame.ConfigureCamera(camera);
            foreach(var volume in Object.FindObjectsByType<Volume>(FindObjectsSortMode.None))
            {
                if(!volume.sharedProfile)continue;
                if(!AssetDatabase.Contains(volume.sharedProfile))AssetDatabase.CreateAsset(volume.sharedProfile,AssetDatabase.GenerateUniqueAssetPath("Assets/IronPulse/Materials/ReactorAtmosphere.asset"));
                foreach(var component in volume.sharedProfile.components)
                    if(component&&!AssetDatabase.Contains(component))AssetDatabase.AddObjectToAsset(component,volume.sharedProfile);
                EditorUtility.SetDirty(volume.sharedProfile);
            }
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            Debug.Log("IRON PULSE v3 arena upgraded: PBR surfaces, industrial details, shadowed furnace lighting.");
        }
        [MenuItem("Iron Pulse/Build Arena")]
        public static void Build()
        {
            if(EditorApplication.isPlaying) return;
            Directory.CreateDirectory("Assets/IronPulse/Scenes");Directory.CreateDirectory("Assets/IronPulse/Meshes");Directory.CreateDirectory("Assets/IronPulse/Materials");
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var root=new GameObject("IRON PULSE // Game");root.AddComponent<IPGame>();
            var world=new GameObject("World");world.transform.SetParent(root.transform);IPWorld.Build(world.transform);
            IPGame.ConfigureAtmosphere();
            var preview=new GameObject("Arena Preview Camera");var camera=preview.AddComponent<Camera>();camera.transform.position=IPWorld.PlayerSpawn+Vector3.up*.7f;camera.transform.rotation=Quaternion.Euler(3,0,0);camera.fieldOfView=92;IPGame.ConfigureCamera(camera);
            var meshes=new System.Collections.Generic.Dictionary<Mesh,Mesh>();
            foreach(var filter in world.GetComponentsInChildren<MeshFilter>())
            {
                var mesh=filter.sharedMesh;if(!mesh||AssetDatabase.Contains(mesh))continue;
                if(!meshes.ContainsKey(mesh))
                {
                    string path=AssetDatabase.GenerateUniqueAssetPath("Assets/IronPulse/Meshes/"+filter.gameObject.name.Replace("/","_").Replace(":","_")+".asset");
                    AssetDatabase.CreateAsset(mesh,path);meshes[mesh]=mesh;
                }
                var collider=filter.GetComponent<MeshCollider>();if(collider)collider.sharedMesh=filter.sharedMesh;
            }
            foreach(var volume in Object.FindObjectsByType<Volume>(FindObjectsSortMode.None))
            {
                if(volume.sharedProfile&&!AssetDatabase.Contains(volume.sharedProfile))
                {
                    AssetDatabase.CreateAsset(volume.sharedProfile,AssetDatabase.GenerateUniqueAssetPath("Assets/IronPulse/Materials/ReactorAtmosphere.asset"));
                    foreach(var component in volume.sharedProfile.components)
                        if(component&&!AssetDatabase.Contains(component))AssetDatabase.AddObjectToAsset(component,volume.sharedProfile);
                    EditorUtility.SetDirty(volume.sharedProfile);
                }
            }
            EditorSceneManager.SaveScene(scene,ScenePath);
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(ScenePath,true)};
            PlayerSettings.productName="IRON PULSE";PlayerSettings.companyName="Reactor Nine";
            AssetDatabase.SaveAssets();
            Selection.activeGameObject=root;
            var sceneView=SceneView.lastActiveSceneView;
            if(sceneView){sceneView.LookAt(new Vector3(0,2,0),Quaternion.Euler(18,0,0),42);sceneView.Repaint();}
            Debug.Log("IRON PULSE arena saved. Press Play, then Enter to breach. "+world.GetComponentsInChildren<Renderer>().Length+" world renderers.");
        }
    }
}
