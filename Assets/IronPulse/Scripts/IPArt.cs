using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace IronPulse
{
    public static class IPArt
    {
        static readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
        static readonly Dictionary<string, Texture2D> surfaces = new Dictionary<string, Texture2D>();

        public static Material Mat(string name, Color color, float emission = 0f)
        {
            string key = name + "_" + ColorUtility.ToHtmlStringRGB(color) + "_" + Mathf.RoundToInt(emission * 10);
            if (materials.TryGetValue(key, out var cached) && cached)
            {
                Configure(cached, name, color, emission);
                return cached;
            }
            Shader shader = SurfaceShader(emission);
            Material mat;
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                string path = "Assets/IronPulse/Materials/" + key.Replace("/", "_").Replace(" ", "_") + ".mat";
                mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat) mat.shader = shader;
                else
                {
                    mat = new Material(shader) { name = key, enableInstancing = true };
                    AssetDatabase.CreateAsset(mat, path);
                }
            }
            else
#endif
                mat = new Material(shader) { name = key, enableInstancing = true };
            Configure(mat, name, color, emission);
            materials[key] = mat;
            return mat;
        }

        static void Configure(Material mat, string label, Color color, float emission)
        {
            var shader = SurfaceShader(emission);
            if (shader && mat.shader != shader) mat.shader = shader;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_UnlitColor")) mat.SetColor("_UnlitColor", color);
            if (mat.HasProperty("_Emission")) mat.SetFloat("_Emission", emission);
            if (mat.HasProperty("_EmissiveColor")) mat.SetColor("_EmissiveColor", color * emission);
            ApplySurface(mat, label);
        }

        /// <summary>Refresh an existing scene material's finish without replacing its colour or culling.</summary>
        public static void ApplySurface(Material mat, string label = null)
        {
            if (!mat) return;
            // Real HDRP materials participate in punctual lighting, cascaded shadows,
            // contact shadows and SSAO. The deliberately unlit shell shader is reserved
            // for silhouette outlines and tiny self-lit accents.
            Color color = mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor") : Color.white;
            float emission = mat.HasProperty("_Emission") ? mat.GetFloat("_Emission") : 0f;
            float cull = mat.HasProperty("_Cull") ? mat.GetFloat("_Cull") : 2f;
            var target = SurfaceShader(emission);
            if (target && mat.shader != target) mat.shader = target;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            Set(mat, "_Emission", emission);
            Set(mat, "_Cull", cull);
            string n = (label ?? mat.name).ToLowerInvariant();
            float metallic = .10f, roughness = .72f, grain = .043f, scale = 96f, kind = 0f;
            if (ContainsAny(n, "skin", "forearm", "flesh", "human", "knuckle"))
            {
                metallic = 0f; roughness = .69f; grain = .026f; scale = 150f; kind = 3f;
            }
            else if (ContainsAny(n, "wood", "walnut", "timber"))
            {
                metallic = 0f; roughness = .37f; grain = .036f; scale = 150f; kind = 2f;
            }
            else if (ContainsAny(n, "glove", "cloth", "fabric", "wrap", "sleeve", "leather", "grip"))
            {
                metallic = 0f; roughness = .88f; grain = .065f; scale = 170f; kind = 4f;
            }
            else if (ContainsAny(n, "concrete", "stone", "basalt", "distant", "near black", "recess", "bore"))
            {
                metallic = 0f; roughness = .91f; grain = .075f; scale = 86f;
            }
            else if (ContainsAny(n, "oxidized", "rust"))
            {
                metallic = .22f; roughness = .86f; grain = .085f; scale = 110f; kind = 1f;
            }
            else if (ContainsAny(n, "brass", "blade", "katana edge", "chrome", "polished"))
            {
                metallic = .94f; roughness = .23f; grain = .022f; scale = 170f; kind = 1f;
            }
            else if (ContainsAny(n, "steel", "metal", "barrel", "alloy", "machined", "bolt", "receiver", "graphite", "iron"))
            {
                metallic = .82f; roughness = .34f; grain = .037f; scale = 150f; kind = 1f;
            }
            else if (ContainsAny(n, "armor", "enamel", "paint", "shell"))
            {
                metallic = .28f; roughness = .53f; grain = .034f; scale = 128f;
            }
            if (mat.HasProperty("_Emission") && mat.GetFloat("_Emission") > .1f)
            {
                metallic = 0f; roughness = .66f; grain = .012f;
            }
            Set(mat, "_Metallic", metallic);
            Set(mat, "_Roughness", roughness);
            Set(mat, "_Smoothness", 1f - roughness);
            Set(mat, "_Grain", grain);
            Set(mat, "_TextureScale", scale);
            Set(mat, "_SurfaceKind", kind);
            if (emission <= .001f && mat.shader.name == "HDRP/Lit")
            {
                string finish = kind == 2f ? "Walnut" : kind == 4f ? "Fabric" : kind == 1f ? "Machined" : roughness > .8f ? "Concrete" : "Enamel";
                mat.SetTexture("_BaseColorMap", SurfaceTexture(finish, false));
                mat.SetTexture("_NormalMap", SurfaceTexture(finish, true));
                Set(mat, "_NormalScale", finish == "Concrete" ? .34f : .17f);
                Set(mat, "_UVBase", 5f); // HDRP triplanar mapping also works on procedural meshes without UVs.
                Set(mat, "_ObjectSpaceUVMapping", n.Contains("world") || n.Contains("siege") ? 0f : 1f);
                Set(mat, "_TexWorldScale", finish == "Walnut" ? .8f : 1.15f);
                Set(mat, "_SurfaceType", 0f);
                Set(mat, "_DoubleSidedEnable", 0f);
                Set(mat, "_AlphaCutoffEnable", 0f);
                // Metallic glints stay broad enough to read on angular, block-built forms.
                Set(mat, "_CoatMask", ContainsAny(n, "enamel", "armor", "shell") ? .12f : 0f);
                if (mat.HasProperty("_EmissiveColor")) mat.SetColor("_EmissiveColor", Color.black);
                HDMaterial.ValidateMaterial(mat);
            }
#if UNITY_EDITOR
            if (!Application.isPlaying) EditorUtility.SetDirty(mat);
#endif
        }

        static Shader SurfaceShader(float emission)
        {
            var shader = Shader.Find(emission > .001f ? "IronPulse/PixelSurface" : "HDRP/Lit");
            if (!shader) shader = Shader.Find("HDRP/Unlit");
            return shader;
        }

        // Small, point sampled material textures preserve the pixel/3D language while
        // native-resolution antialiasing and physically lit surfaces remove screen pixels.
        // These are shared by finish instead of allocating one texture per scene object.
        static Texture2D SurfaceTexture(string finish, bool normal)
        {
            string key = "IP_" + finish + (normal ? "_Normal" : "_Albedo");
            if (surfaces.TryGetValue(key, out var cached) && cached) return cached;
#if UNITY_EDITOR
            string path = "Assets/IronPulse/Materials/" + key + ".asset";
            var saved = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (saved) { surfaces[key] = saved; return saved; }
#endif
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true, normal)
            {
                name = key, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat,
                anisoLevel = 4
            };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float h = Height(finish, x, y);
                if (normal)
                {
                    float dx = (h - Height(finish, x + 1, y)) * .65f;
                    float dy = (h - Height(finish, x, y + 1)) * .65f;
                    Vector3 n = new Vector3(dx, dy, 1f).normalized;
                    pixels[y * size + x] = new Color(n.x * .5f + .5f, n.y * .5f + .5f, n.z * .5f + .5f, 1);
                }
                else
                {
                    float v = .86f + h * .14f;
                    pixels[y * size + x] = finish == "Walnut" ? new Color(v, v * .98f, v * .94f, 1) : new Color(v, v, v, 1);
                }
            }
            texture.SetPixels(pixels); texture.Apply(true, false);
#if UNITY_EDITOR
            if (!Application.isPlaying) AssetDatabase.CreateAsset(texture, path);
#endif
            surfaces[key] = texture;
            return texture;
        }

        static float Height(string finish, int x, int y)
        {
            x = (x + 64) % 64; y = (y + 64) % 64;
            uint v = (uint)(x * 374761393 + y * 668265263 + 8253729);
            v = (v ^ (v >> 13)) * 1274126177; v ^= v >> 16;
            float noise = (v & 65535) / 65535f;
            if (finish == "Walnut") return .5f + Mathf.Sin(x * .78f + Mathf.Sin(y * .09817477f) * 1.5f) * .35f + noise * .12f;
            if (finish == "Fabric") return ((x + y) % 2 == 0 ? .34f : .62f) + noise * .12f;
            if (finish == "Machined") return .5f + Mathf.Sin(y * 1.5707963f) * .16f + noise * .12f;
            if (finish == "Concrete") return noise * .68f + Mathf.PerlinNoise(x * .125f, y * .125f) * .32f;
            return .48f + noise * .2f;
        }

        static bool ContainsAny(string value, params string[] words)
        {
            for (int i = 0; i < words.Length; i++) if (value.Contains(words[i])) return true;
            return false;
        }
        static void Set(Material mat, string property, float value)
        {
            if (mat.HasProperty(property)) mat.SetFloat(property, value);
        }

        public static GameObject Box(string name, Transform parent, Vector3 localPos, Vector3 scale, Material mat, bool collider = false)
            => Shape(PrimitiveType.Cube, name, parent, localPos, scale, mat, collider);
        public static GameObject Cylinder(string name, Transform parent, Vector3 localPos, Vector3 scale, Material mat, bool collider = false)
            => Shape(PrimitiveType.Cylinder, name, parent, localPos, scale, mat, collider);
        static GameObject Shape(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, Material mat, bool collider)
        {
            var go = GameObject.CreatePrimitive(type); go.name = name; go.transform.SetParent(parent, false);
            go.transform.localPosition = pos; go.transform.localScale = scale;
            var r = go.GetComponent<Renderer>(); r.sharedMaterial = mat; r.shadowCastingMode = ShadowCastingMode.On; r.receiveShadows = true;
            if (!collider) RemoveCollider(go);
            return go;
        }
        public static void RemoveCollider(GameObject go)
        {
            var c = go.GetComponent<Collider>(); if (!c) return;
            c.enabled = false;
            if (Application.isPlaying) Object.Destroy(c); else Object.DestroyImmediate(c);
        }
    }
}
