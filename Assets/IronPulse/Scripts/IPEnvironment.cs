using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace IronPulse
{
    /// <summary>Physical lighting for the siege arena; no baked scene or external assets required.</summary>
    public sealed class IPEnvironment : MonoBehaviour
    {
        public const float ExposureEV = 8.5f;
        public const string RigName = "IronPulse // Furnace lighting";

        public static void Configure()
        {
            var existing = Object.FindFirstObjectByType<IPEnvironment>();
            var root = existing ? existing.gameObject : new GameObject(RigName);
            if (!root.GetComponent<IPEnvironment>()) root.AddComponent<IPEnvironment>();
            // The generated arena is its own scene. Disable inherited OutdoorsScene
            // lamps so fixed exposure and directional shadow direction remain stable.
            foreach (var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (light.gameObject.scene == root.scene && !light.transform.IsChildOf(root.transform) &&
                    !light.name.Contains("Muzzle") && !light.name.Contains("muzzle")) light.enabled = false;

            var key = Lamp(root.transform, "01 | Furnace skylight", LightType.Directional,
                new Vector3(-10, 20, -12), new Vector3(39, -32, 0), new Color(1f, .78f, .61f), 4200, 100, true);
            key.GetComponent<HDAdditionalLightData>().angularDiameter = 1.8f;
            Lamp(root.transform, "02 | Cool ambient rim", LightType.Directional,
                new Vector3(15, 18, 15), new Vector3(63, 145, 0), new Color(.56f, .65f, .78f), 1100, 100, false);

            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 pos = new Vector3(side * 13f, 11.4f, 8f);
                var spot = Lamp(root.transform, "03 | Armored flood " + side, LightType.Spot,
                    pos, Quaternion.LookRotation(new Vector3(side * 5f, 0f, -4f) - pos).eulerAngles,
                    new Color(1f, .67f, .40f), 85000, 32, true);
                spot.spotAngle = 78;
                spot.GetComponent<HDAdditionalLightData>().SetSpotAngle(78, 66);
                Lamp(root.transform, "04 | Gallery cyan spill " + side, LightType.Point,
                    new Vector3(side * 18.2f, 4.1f, -6), Vector3.zero, new Color(.17f, .52f, .7f), 1300, 11, false);
                Lamp(root.transform, "05 | Generator spill " + side, LightType.Point,
                    new Vector3(side * 14, 2.4f, -15.9f), Vector3.zero, new Color(.21f, .58f, .68f), 800, 8, false);
                Lamp(root.transform, "06 | Furnace sconce " + side, LightType.Point,
                    new Vector3(side * 22.3f, 5.4f, 7), Vector3.zero, new Color(1, .23f, .055f), 2200, 13, false);
                Lamp(root.transform, "07 | Ember trench " + side, LightType.Point,
                    new Vector3(side * 4.5f, 1.5f, 16.4f), Vector3.zero, new Color(1, .27f, .065f), 1400, 9, false);
            }
            Vector3 corePos = new Vector3(0, 9.3f, 20.2f);
            var core = Lamp(root.transform, "08 | Reactor downlight", LightType.Spot,
                corePos, Quaternion.LookRotation(new Vector3(0, 0, 6) - corePos).eulerAngles,
                new Color(1, .44f, .13f), 62000, 29, true);
            core.spotAngle = 105;
            core.GetComponent<HDAdditionalLightData>().SetSpotAngle(105, 74);
            Lamp(root.transform, "09 | Reactor bounce", LightType.Point,
                new Vector3(0, 8.5f, 20.2f), Vector3.zero, new Color(1, .25f, .045f), 8500, 16, false);
            Lamp(root.transform, "10 | Entry safe light", LightType.Point,
                new Vector3(0, 5.8f, -21.8f), Vector3.zero, new Color(.43f, .57f, .63f), 1800, 12, false);
        }

        static Light Lamp(Transform rig, string name, LightType type, Vector3 position, Vector3 euler,
            Color color, float intensity, float range, bool shadows)
        {
            var t = rig.Find(name);
            var obj = t ? t.gameObject : new GameObject(name);
            obj.transform.SetParent(rig, false);
            obj.transform.localPosition = position; obj.transform.localEulerAngles = euler;
            var light = obj.GetComponent<Light>();
            if (!light) light = obj.AddComponent<Light>();
            light.enabled = true; light.type = type; light.color = color;
            light.useColorTemperature = false;
            light.lightUnit = type == LightType.Directional ? LightUnit.Lux : LightUnit.Candela;
            light.intensity = intensity; light.range = range;
            light.shadows = shadows ? LightShadows.Soft : LightShadows.None;
            light.shadowStrength = .94f; light.bounceIntensity = 1;
            var hd = obj.GetComponent<HDAdditionalLightData>();
            if (!hd) hd = obj.AddComponent<HDAdditionalLightData>();
            light.lightUnit = type == LightType.Directional ? LightUnit.Lux : LightUnit.Candela;
            light.intensity = intensity;
            hd.SetShadowResolution(type == LightType.Directional ? 2048 : 1024);
            hd.SetShadowUpdateMode(ShadowUpdateMode.EveryFrame);
            hd.shadowDimmer = .94f; hd.normalBias = .2f; hd.slopeBias = .45f;
            hd.shadowNearPlane = .1f; hd.fadeDistance = range; hd.volumetricDimmer = .35f;
            hd.shapeRadius = .16f;
            hd.useContactShadow.useOverride = true; hd.useContactShadow.@override = shadows;
            return light;
        }

        public static void ConfigureVolume(VolumeProfile profile)
        {
            if (!profile) return;
            if (!profile.TryGet<Exposure>(out var exposure)) exposure = profile.Add<Exposure>(true);
            exposure.mode.Override(ExposureMode.Fixed); exposure.fixedExposure.Override(ExposureEV);
            if (!profile.TryGet<VisualEnvironment>(out var visual)) visual = profile.Add<VisualEnvironment>(true);
            visual.skyType.Override((int)SkyType.Gradient); visual.skyAmbientMode.Override(SkyAmbientMode.Dynamic);
            if (!profile.TryGet<GradientSky>(out var sky)) sky = profile.Add<GradientSky>(true);
            sky.top.Override(new Color(.075f, .10f, .15f));
            sky.middle.Override(new Color(.24f, .085f, .045f));
            sky.bottom.Override(new Color(.08f, .075f, .07f));
            sky.gradientDiffusion.Override(.9f); sky.exposure.Override(9.1f);
            sky.updateMode.Override(EnvironmentUpdateMode.OnChanged);
            if (!profile.TryGet<ScreenSpaceAmbientOcclusion>(out var ao)) ao = profile.Add<ScreenSpaceAmbientOcclusion>(true);
            ao.intensity.Override(1.25f); ao.radius.Override(.6f); ao.directLightingStrength.Override(.12f);
            ao.temporalAccumulation.Override(false); ao.quality.Override(1); ao.fullResolution = true;
            if (!profile.TryGet<ContactShadows>(out var contact)) contact = profile.Add<ContactShadows>(true);
            contact.enable.Override(true); contact.length.Override(.18f); contact.opacity.Override(.7f);
            contact.maxDistance.Override(42); contact.fadeDistance.Override(6); contact.quality.Override(1);
            if (!profile.TryGet<HDShadowSettings>(out var shadow)) shadow = profile.Add<HDShadowSettings>(true);
            shadow.maxShadowDistance.Override(95); shadow.cascadeShadowSplitCount.Override(4);
        }
    }
}
