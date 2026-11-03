using System.Collections.Generic;
using UnityEngine;

namespace IronPulse
{
    /// <summary>Original, asset-free industrial arena. All geometry remains editable in the saved scene.</summary>
    public static class IPWorld
    {
        public static Vector3 PlayerSpawn => new Vector3(0f, 1f, -18f);
        public static Vector3[] EnemySpawns => new[]
        {
            new Vector3(-4f, .12f, -8f), new Vector3(4f, .12f, -5f),
            new Vector3(-12f, .12f, -12f), new Vector3(12f, .12f, -12f),
            new Vector3(-4f, .12f, 3f), new Vector3(5f, .12f, 5f),
            new Vector3(-14f, .12f, 4f), new Vector3(14f, .12f, 3f),
            new Vector3(-5f, .12f, 12f), new Vector3(5f, .12f, 12f),
            new Vector3(-14f, .12f, 13f), new Vector3(14f, .12f, 14f)
        };

        static Material concrete, concreteLight, charcoal, steel, edge, rust, orange, cyan, hot, yellow, black, navy;

        static void InitializeMaterials()
        {
            concrete = M("World - blue concrete", .19f, .24f, .28f);
            concreteLight = M("World - concrete face", .28f, .33f, .36f);
            charcoal = M("World - basalt steel", .075f, .105f, .14f);
            steel = M("World - blue steel", .12f, .19f, .23f);
            edge = M("World - edge metal", .37f, .43f, .45f);
            rust = M("World - oxidized iron", .31f, .14f, .10f);
            orange = IPArt.Mat("World - reactor amber", new Color(1f, .22f, .035f), 3f);
            cyan = IPArt.Mat("World - ion cyan", new Color(.08f, .82f, 1f), 2.3f);
            hot = IPArt.Mat("World - furnace core", new Color(1f, .56f, .12f), 4f);
            yellow = M("World - caution enamel", .89f, .54f, .12f);
            black = M("World - near black", .025f, .04f, .055f);
            navy = M("World - distant blue", .09f, .12f, .19f);
        }

        public static void Build(Transform parent)
        {
            InitializeMaterials();

            Transform ground = Group("01 | Reactor court", parent);
            B("Foundation", ground, new Vector3(0f, -.55f, 0f), new Vector3(54f, 1f, 54f), concrete, true);
            // Inset seams and large plates retain their structure at low render resolution.
            for (int x = -3; x <= 3; x++)
                for (int z = -3; z <= 3; z++)
                    B("Cast floor plate", ground, new Vector3(x * 7.45f, -.019f, z * 7.45f), new Vector3(7.32f, .04f, 7.32f), ((x + z) & 1) == 0 ? concrete : steel);
            for (int side = -1; side <= 1; side += 2)
            {
                B("Center lane marker", ground, new Vector3(side * 2.8f, .013f, -4f), new Vector3(.09f, .035f, 29f), edge);
                for (int z = -18; z <= 12; z += 5)
                    B("Cyan route dash", ground, new Vector3(side * 3.05f, .025f, z), new Vector3(.14f, .04f, 1.7f), cyan);
                B("South launch strip", ground, new Vector3(side * 10f, .02f, -21.7f), new Vector3(11f, .045f, .16f), cyan);
                B("Side gutter", ground, new Vector3(side * 16.2f, .03f, 0f), new Vector3(.42f, .07f, 34f), black);
                B("Gutter light", ground, new Vector3(side * 16.2f, .073f, 0f), new Vector3(.09f, .04f, 29f), orange);
            }
            B("Arrival pad", ground, new Vector3(0f, .008f, -18f), new Vector3(5.3f, .05f, 4.6f), charcoal);
            B("Arrival pad cyan edge", ground, new Vector3(0f, .04f, -20.25f), new Vector3(5.3f, .055f, .15f), cyan);
            PaintText("09", ground, new Vector3(-1.1f, .044f, -17f), .2f, edge, Quaternion.Euler(90f, 0f, 0f));

            Transform architecture = Group("02 | Bastion walls", parent);
            B("Northern armored wall", architecture, new Vector3(0f, 7.5f, 26f), new Vector3(54f, 16f, 2f), charcoal, true);
            B("Northern lower plinth", architecture, new Vector3(0f, 1.45f, 24.7f), new Vector3(51f, 3f, 2f), concreteLight, true);
            B("Southern boundary", architecture, new Vector3(0f, 3f, -26f), new Vector3(54f, 6f, 2f), concrete, true);
            for (int side = -1; side <= 1; side += 2)
            {
                B("Lateral retaining wall", architecture, new Vector3(side * 26f, 4f, 0f), new Vector3(2f, 8f, 54f), concrete, true);
                B("Lateral dark belt", architecture, new Vector3(side * 24.95f, 4.7f, 0f), new Vector3(.13f, 1.7f, 49f), charcoal);
                B("Lateral amber seam", architecture, new Vector3(side * 24.84f, 3.6f, 0f), new Vector3(.12f, .12f, 46f), orange);
                for (int z = -21; z <= 21; z += 7)
                {
                    B("Wall buttress", architecture, new Vector3(side * 24.5f, 6f, z), new Vector3(2.1f, 12f, 2f), steel, true);
                    B("Buttress cap", architecture, new Vector3(side * 24.35f, 12.1f, z), new Vector3(2.6f, .6f, 2.6f), edge);
                    B("Recess signal", architecture, new Vector3(side * 23.37f, 7.6f, z), new Vector3(.10f, 2.3f, .32f), z < 0 ? cyan : orange);
                }
                B("Northern pylons", architecture, new Vector3(side * 20f, 10f, 25f), new Vector3(5f, 20f, 5f), steel, true);
                B("Pylon shadow face", architecture, new Vector3(side * 18.4f, 10f, 22.35f), new Vector3(1.4f, 17f, .25f), charcoal);
                B("Pylon amber spine", architecture, new Vector3(side * 20.4f, 12.2f, 22.4f), new Vector3(.35f, 12f, .3f), orange);
                B("Pylon crown", architecture, new Vector3(side * 20f, 20.2f, 25f), new Vector3(6.2f, .7f, 5.8f), concreteLight);
                B("South corner bastion", architecture, new Vector3(side * 23f, 7f, -24f), new Vector3(6f, 14f, 6f), steel, true);
                B("Bastion beacon", architecture, new Vector3(side * 23f, 14.1f, -24f), new Vector3(3f, .3f, 3f), cyan);
                B("South entry jamb", architecture, new Vector3(side * 5.4f, 3f, -24.65f), new Vector3(1.2f, 6f, 1f), steel, true);
                B("Entry lamp", architecture, new Vector3(side * 4.72f, 3.2f, -24.05f), new Vector3(.12f, 3.8f, .1f), cyan);
            }
            B("Entry lintel", architecture, new Vector3(0f, 6f, -24.65f), new Vector3(12f, 1f, 1.6f), edge);
            B("Closed arrival gate", architecture, new Vector3(0f, 2.5f, -24.8f), new Vector3(9f, 5f, .3f), black);
            for (int i = -4; i <= 4; i++)
                B("Entry vertical armor", architecture, new Vector3(i * .9f, 2.5f, -24.5f), new Vector3(.64f, 4.8f, .2f), steel);

            BuildWalkways(parent);
            BuildReactor(parent);
            BuildCovers(parent);
            BuildPipes(parent);
            BuildSkyline(parent);
            AddDetail(parent);
        }

        /// <summary>Idempotent visual upgrade for already saved arenas; combat routes and spawns stay clear.</summary>
        public static void AddDetail(Transform world)
        {
            if (!world || world.Find("08 | Siege detail")) return;
            InitializeMaterials();
            Transform p = Group("08 | Siege detail", world);
            var soot = IPArt.Mat("Siege - scorched basalt", new Color(.055f, .045f, .037f));
            var warmPlate = IPArt.Mat("Siege - furnace enamel", new Color(.26f, .19f, .13f));
            var scar = IPArt.Mat("Siege - oxidized scar", new Color(.19f, .055f, .027f));
            var seal = IPArt.Mat("Siege - tar rubber gasket", new Color(.035f, .032f, .025f));
            var white = IPArt.Mat("Siege - faded ivory stencil", new Color(.62f, .58f, .44f));

            // Heavy ribbed furnace arches bring the Doom-like silhouette into the
            // foreground. Their high beams cannot catch a dash or double jump.
            foreach (float z in new[] { -10f, 7f })
            {
                B("Siege arch crossbeam", p, new Vector3(0, 13.5f, z), new Vector3(34, 1.3f, 1.9f), charcoal);
                B("Arch underside heat baffle", p, new Vector3(0, 12.78f, z), new Vector3(28, .2f, 1.4f), rust);
                for (int s = -1; s <= 1; s += 2)
                {
                    B("Reinforced arch pier", p, new Vector3(s * 23.1f, 5.9f, z), new Vector3(1.45f, 11.8f, 2.7f), warmPlate);
                    B("Arch angled shoulder", p, new Vector3(s * 19.8f, 11.8f, z), new Vector3(8.6f, 1.35f, 2.1f), steel, false, new Vector3(0, 0, s * 25));
                    B("Pier recessed heat sink", p, new Vector3(s * 22.33f, 5.5f, z), new Vector3(.12f, 5.6f, 1.9f), soot);
                    for (int row = 0; row < 5; row++)
                        B("Cast pier cooling rib", p, new Vector3(s * 22.2f, 3.4f + row * 1.02f, z), new Vector3(.24f, .2f, 2.15f), edge);
                    for (int y = 0; y < 2; y++)
                        B("Pier caution enamel", p, new Vector3(s * 22.18f, 1.5f + y * .35f, z), new Vector3(.11f, .18f, 1.25f), yellow);
                    B("Flood lamp armored casing", p, new Vector3(s * 13, 11.8f, z), new Vector3(2.1f, .9f, 1.4f), steel, false, new Vector3(22, 0, 0));
                    B("Flood lamp amber lens", p, new Vector3(s * 13, 11.3f, z - .25f), new Vector3(1.6f, .08f, .8f), hot, false, new Vector3(22, 0, 0));
                }
                for (int x = -12; x <= 12; x += 4)
                    B("Crossbeam reinforcement plate", p, new Vector3(x, 13.5f, z - 1.02f), new Vector3(1.3f, 1.55f, .14f), edge);
            }

            // Flush floor inserts, angular scorch marks and inspection seams add
            // scale without creating invisible collision or tripping points.
            for (int side = -1; side <= 1; side += 2)
            {
                foreach (float z in new[] { -14f, -1f, 10f })
                {
                    Vector3 at = new Vector3(side * 6.3f, .058f, z);
                    B("Recessed drainage frame", p, at, new Vector3(1.8f, .04f, 2.5f), rust);
                    B("Drainage black recess", p, at + Vector3.up * .026f, new Vector3(1.5f, .016f, 2.2f), soot);
                    for (int i = 0; i < 7; i++)
                        B("Flush drainage grate", p, at + new Vector3(0, .045f, -.94f + i * .31f), new Vector3(1.51f, .045f, .10f), edge);
                }
                for (int i = 0; i < 8; i++)
                {
                    float z = -19 + i * 5.2f;
                    B("Floor service trench seam", p, new Vector3(side * 12.8f, .063f, z), new Vector3(3.8f, .012f, .035f), seal);
                    if (i % 2 == 0)
                        B("Floor chipped paint marking", p, new Vector3(side * 12.5f, .072f, z + .35f), new Vector3(.65f, .018f, .12f), white);
                }
                // Galvanized utility panels on both flanks are raised clear of the
                // gallery floor. Corner bolts and separate louvers catch real light.
                foreach (float z in new[] { -17f, -3f, 11f })
                {
                    B("Wall recessed utility panel", p, new Vector3(side * 24.78f, 5.75f, z), new Vector3(.18f, 2.6f, 4f), soot);
                    B("Wall access armored door", p, new Vector3(side * 24.61f, 5.75f, z), new Vector3(.15f, 2.34f, 3.72f), warmPlate);
                    for (int i = 0; i < 5; i++)
                        B("Wall intake louver", p, new Vector3(side * 24.47f, 5.05f + i * .32f, z - .6f), new Vector3(.17f, .12f, 1.6f), charcoal, false, new Vector3(0, 0, side * 16));
                    B("Access door locking bar", p, new Vector3(side * 24.44f, 5.7f, z + 1.08f), new Vector3(.23f, 1.2f, .12f), edge);
                    B("Panel warning status", p, new Vector3(side * 24.39f, 6.42f, z + 1.08f), new Vector3(.06f, .13f, .36f), orange);
                    for (int a = -1; a <= 1; a += 2) for (int b = -1; b <= 1; b += 2)
                        B("Square-headed access bolt", p, new Vector3(side * 24.39f, 5.75f + a * .98f, z + b * 1.53f), new Vector3(.12f, .14f, .14f), edge);
                }
                // Suspended angular cable bundles and links, safely above movement.
                for (int chain = 0; chain < 2; chain++)
                {
                    float x = side * (9.2f + chain * 7.5f), z = chain == 0 ? 21 : 7;
                    for (int link = 0; link < 5; link++)
                        B("Hanging segmented chain", p, new Vector3(x, 11.5f - link * .36f, z), new Vector3(.16f, .43f, .12f), link % 2 == 0 ? rust : edge, false, new Vector3(0, 0, link % 2 == 0 ? 12 : -12));
                    B("Suspended industrial seal", p, new Vector3(x, 9.4f, z), new Vector3(.8f, 1.1f, .24f), warmPlate, false, new Vector3(0, 0, side * 8));
                    B("Seal ember groove", p, new Vector3(x, 9.4f, z - .14f), new Vector3(.12f, .72f, .055f), orange, false, new Vector3(0, 0, side * 8));
                }
                // Furnace skirting frames along the rear bridge.
                for (int i = 0; i < 3; i++)
                {
                    float x = side * (10f + i * 5f);
                    B("Furnace skirt dark recess", p, new Vector3(x, 1.7f, 23.55f), new Vector3(3.6f, 2f, .24f), soot);
                    B("Furnace skirt header", p, new Vector3(x, 2.65f, 23.35f), new Vector3(3.9f, .24f, .35f), rust);
                    for (int j = 0; j < 4; j++)
                        B("Furnace skirt vertical fin", p, new Vector3(x - 1.2f + j * .8f, 1.7f, 23.3f), new Vector3(.15f, 1.8f, .38f), steel);
                }
            }

            // A few restrained asymmetrical environmental scars avoid pristine
            // repetition. Thin surface geometry never obstructs arena navigation.
            for (int i = 0; i < 12; i++)
            {
                float x = Mathf.Sin(i * 17.32f) * 13.5f, z = -12f + i * 2.5f;
                B("Angular blast scar", p, new Vector3(x, .083f, z), new Vector3(.8f + i % 3 * .25f, .012f, .24f), scar, false, new Vector3(0, i * 67, 0));
                B("Scorched impact center", p, new Vector3(x + .14f, .092f, z + .06f), new Vector3(.35f, .012f, .26f), soot, false, new Vector3(0, i * 67, 0));
            }
            for (int side = -1; side <= 1; side += 2)
            {
                var coverCenter = new Vector3(side * 9.5f, 0, -4f);
                for (int bolt = 0; bolt < 4; bolt++)
                    B("Cover face recessed bolt", p, coverCenter + new Vector3(-1.7f + bolt * 1.13f, .35f, -1.28f), new Vector3(.12f, .12f, .08f), edge);
                B("Cover worn hazard band", p, coverCenter + new Vector3(0, .18f, -1.3f), new Vector3(3.8f, .12f, .06f), white);
                B("Power trunk horizontal sheath", p, new Vector3(side * 12f, .22f, 13.8f), new Vector3(5.9f, .26f, .32f), seal);
                for (int clip = 0; clip < 4; clip++)
                    B("Power trunk floor clip", p, new Vector3(side * (9.5f + clip * 1.5f), .24f, 13.8f), new Vector3(.15f, .33f, .55f), rust);
            }

            // Only solid architecture needs the shadow atlas. Tiny luminous
            // details remain visible accents without wasting casters.
            foreach (var renderer in p.GetComponentsInChildren<Renderer>())
            {
                bool glow = renderer.sharedMaterial && renderer.sharedMaterial.HasProperty("_Emission") && renderer.sharedMaterial.GetFloat("_Emission") > .001f;
                renderer.shadowCastingMode = glow ? UnityEngine.Rendering.ShadowCastingMode.Off : UnityEngine.Rendering.ShadowCastingMode.On;
                renderer.receiveShadows = !glow;
            }
        }

        static void BuildWalkways(Transform parent)
        {
            Transform p = Group("03 | Elevated flanking galleries", parent);
            for (int s = -1; s <= 1; s += 2)
            {
                float x = s * 20.25f;
                B("Armored gallery deck", p, new Vector3(x, 2.7f, 5.6f), new Vector3(7.2f, .6f, 31.2f), steel, true);
                B("Gallery inner edge", p, new Vector3(s * 16.73f, 2.83f, 5.6f), new Vector3(.23f, .5f, 31.4f), edge);
                B("Gallery lit fascia", p, new Vector3(s * 16.57f, 2.55f, 5.6f), new Vector3(.08f, .13f, 29.8f), cyan);
                Ramp("Smooth access ramp", p, x, -21f, -10f, 7.2f, 3f, steel);
                float slope = -Mathf.Atan2(3f, 11f) * Mathf.Rad2Deg;
                for (int edgeSide = -1; edgeSide <= 1; edgeSide += 2)
                {
                    B("Ramp edge rail", p, new Vector3(x + edgeSide * 3.45f, 1.65f, -15.5f), new Vector3(.17f, .18f, 11.4f), yellow, false, new Vector3(slope, 0f, 0f));
                    for (int z = -8; z <= 19; z += 5)
                    {
                        B("Guardrail upright", p, new Vector3(x + edgeSide * 3.4f, 3.5f, z), new Vector3(.15f, 1f, .15f), edge);
                    }
                    B("Gallery guardrail", p, new Vector3(x + edgeSide * 3.4f, 4f, 5.5f), new Vector3(.15f, .13f, 30f), yellow);
                }
                for (int z = -4; z <= 16; z += 10)
                    B("Gallery supporting pier", p, new Vector3(x, 1.1f, z), new Vector3(1.9f, 2.2f, 1.9f), concreteLight, true);
                for (int i = 0; i < 7; i++)
                {
                    float t = (i + .5f) / 7f;
                    B("Ramp caution tread", p, new Vector3(x, 3f * t + .03f, Mathf.Lerp(-21f, -10f, t)), new Vector3(6.8f, .028f, .2f), i % 2 == 0 ? yellow : edge, false, new Vector3(slope, 0f, 0f));
                }
            }
            B("Northern service bridge", p, new Vector3(0f, 2.7f, 21f), new Vector3(35f, .6f, 5f), steel, true);
            B("Bridge luminous front lip", p, new Vector3(0f, 2.6f, 18.43f), new Vector3(33f, .13f, .1f), orange);
            // Deliberately no front railing: readable targets and a clean jump route back to the court.
        }

        static void BuildReactor(Transform parent)
        {
            Transform p = Group("04 | HELIOS containment engine", parent);
            Vector3 c = new Vector3(0f, 8.25f, 23.15f);
            B("Reactor facade inset", p, new Vector3(0f, 8f, 24.45f), new Vector3(17.6f, 15f, .8f), black);
            Disc("Dark containment shutter", p, c + new Vector3(0f, 0f, .28f), 5.9f, 24, rust);
            Disc("Glowing pressure chamber", p, c + new Vector3(0f, 0f, -.02f), 4.8f, 24, orange);
            Disc("Molten inner chamber", p, c + new Vector3(0f, 0f, -.045f), 3.85f, 16, hot);
            Disc("Core iris", p, c + new Vector3(0f, 0f, -.08f), 2.4f, 12, rust);
            Disc("Core pupil", p, c + new Vector3(0f, 0f, -.1f), 1.25f, 8, black);
            Disc("Core center glow", p, c + new Vector3(0f, 0f, -.13f), .6f, 8, orange);
            for (int i = 0; i < 32; i++)
            {
                float a = i * Mathf.PI * 2f / 32f;
                Vector3 radial = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                float deg = a * Mathf.Rad2Deg + 90f;
                B("Containment ring segment", p, c + radial * 6.15f, new Vector3(1.32f, .82f, 1.1f), (i % 4 == 0) ? concreteLight : steel, false, new Vector3(0f, 0f, deg));
                B("Amber seal segment", p, c + radial * 5.59f + Vector3.back * .58f, new Vector3(.87f, .15f, .11f), orange, false, new Vector3(0f, 0f, deg));
                if (i % 4 == 0)
                    B("Radial lock tooth", p, c + radial * 4.92f + Vector3.back * .26f, new Vector3(.62f, 1.4f, .8f), charcoal, false, new Vector3(0f, 0f, deg));
            }
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4f;
                Vector3 radial = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                B("Iris radial blade", p, c + radial * 2.9f + Vector3.back * .19f, new Vector3(.2f, 1.25f, .12f), charcoal, false, new Vector3(0f, 0f, a * Mathf.Rad2Deg + 90f));
            }
            for (int s = -1; s <= 1; s += 2)
            {
                B("Reactor vertical support", p, new Vector3(s * 8f, 7f, 23f), new Vector3(1.6f, 14f, 2f), concreteLight, true);
                B("Support face", p, new Vector3(s * 8f, 6.5f, 21.8f), new Vector3(.8f, 11f, .25f), charcoal);
                B("Support amber slash", p, new Vector3(s * 8f, 11.5f, 21.63f), new Vector3(.26f, 3f, .16f), orange);
            }
            B("Reactor crown lintel", p, new Vector3(0f, 15.7f, 23.6f), new Vector3(19.1f, 1.4f, 2.6f), steel);
            B("Title backing", p, new Vector3(0f, 17f, 23.8f), new Vector3(23.2f, 2.7f, .7f), black);
            PaintText("IRON / PULSE", p, new Vector3(-9.23f, 16.02f, 23.36f), .28f, hot, Quaternion.identity);
            PaintText("09", p, new Vector3(11.2f, 8.8f, 24.9f), .39f, cyan, Quaternion.identity);
            // Inset cooling trench: solid floor below the grate, so combat cannot strand the player.
            B("Cooling trench housing", p, new Vector3(0f, .09f, 16.5f), new Vector3(15.5f, .18f, 2.7f), black, true);
            B("Cooling trench energy", p, new Vector3(0f, .2f, 16.5f), new Vector3(14.9f, .025f, 2.2f), orange);
            for (int i = -10; i <= 10; i++)
                B("Cooling grate rib", p, new Vector3(i * .7f, .23f, 16.5f), new Vector3(.25f, .09f, 2.35f), charcoal);
            B("Trench yellow near curb", p, new Vector3(0f, .18f, 15.18f), new Vector3(16f, .34f, .2f), yellow, true);
            B("Trench rear curb", p, new Vector3(0f, .18f, 17.82f), new Vector3(16f, .34f, .2f), yellow, true);
            for (int i = -8; i <= 8; i++)
                B("Curb hazard stripe", p, new Vector3(i * .88f, .36f, 15.18f), new Vector3(.32f, .025f, .22f), black);
        }

        static void BuildCovers(Transform parent)
        {
            Transform p = Group("05 | Combat cover and machinery", parent);
            for (int s = -1; s <= 1; s += 2)
            {
                Cover(p, new Vector3(s * 9.5f, 0f, -4f), new Vector3(4.5f, 1.55f, 2.4f), s);
                Cover(p, new Vector3(s * 10.5f, 0f, 8f), new Vector3(3.4f, 2.35f, 3.4f), s);
                B("Generator footing", p, new Vector3(s * 14f, .3f, -15f), new Vector3(2.2f, .6f, 3f), edge, true);
                B("Generator housing", p, new Vector3(s * 14f, 1.35f, -15f), new Vector3(1.8f, 1.7f, 2.5f), steel, true);
                B("Generator luminous window", p, new Vector3(s * 14f, 1.45f, -16.29f), new Vector3(1.3f, .55f, .07f), cyan);
                for (int i = -1; i <= 1; i++)
                    B("Generator vent", p, new Vector3(s * 14f + i * .4f, 2.24f, -15f), new Vector3(.15f, .16f, 1.4f), charcoal);
                B("Side pressure tank", p, new Vector3(s * 22.2f, 5f, 14f), new Vector3(2.5f, 3.6f, 3.4f), rust, true);
                B("Pressure tank luminous seam", p, new Vector3(s * 22.2f, 5.1f, 12.26f), new Vector3(1.9f, .15f, .1f), orange);
            }
        }

        static void Cover(Transform p, Vector3 at, Vector3 size, int sign)
        {
            B("Armored power block", p, at + Vector3.up * (size.y * .5f), size, steel, true);
            B("Power block lid", p, at + Vector3.up * (size.y + .06f), new Vector3(size.x + .2f, .18f, size.z + .2f), edge);
            B("Power block black face", p, at + new Vector3(0f, size.y * .53f, -size.z * .5f - .025f), new Vector3(size.x * .82f, size.y * .55f, .07f), charcoal);
            B("Power block energized stripe", p, at + new Vector3(0f, size.y * .72f, -size.z * .5f - .08f), new Vector3(size.x * .72f, .11f, .055f), cyan);
            for (int s = -1; s <= 1; s += 2)
                B("Power block corner", p, at + new Vector3(s * (size.x * .5f - .14f), size.y * .5f, -size.z * .5f - .07f), new Vector3(.23f, size.y, .2f), yellow);
        }

        static void BuildPipes(Transform parent)
        {
            Transform p = Group("06 | Service conduits", parent);
            for (int s = -1; s <= 1; s += 2)
            {
                for (int lane = 0; lane < 3; lane++)
                {
                    float y = 8.6f + lane * .65f;
                    Pipe("Lateral service pipe", p, new Vector3(s * 23.25f, y, 2f), .34f, 39f, lane == 1 ? rust : steel, new Vector3(90f, 0f, 0f));
                    for (int z = -15; z <= 19; z += 8)
                        Pipe("Pipe flange", p, new Vector3(s * 23.25f, y, z), .46f, .2f, edge, new Vector3(90f, 0f, 0f));
                }
                Pipe("Reactor downcomer", p, new Vector3(s * 12f, 10f, 23.35f), .85f, 15.5f, rust, Vector3.zero);
                for (int y = 4; y <= 16; y += 4)
                    Pipe("Downcomer collar", p, new Vector3(s * 12f, y, 23.35f), 1.03f, .3f, edge, Vector3.zero);
                Pipe("Header transfer pipe", p, new Vector3(s * 13.1f, 17.4f, 23.35f), .85f, 2.6f, rust, new Vector3(0f, 0f, 90f));
                B("Overhead cable rack", p, new Vector3(s * 15f, 12.9f, 22.6f), new Vector3(3.3f, .6f, 1.4f), charcoal);
                B("Relay warning lamp", p, new Vector3(s * 15f, 13.6f, 22.2f), new Vector3(.45f, .55f, .45f), orange);
            }
        }

        static void BuildSkyline(Transform parent)
        {
            Transform p = Group("07 | Skyline silhouettes", parent);
            // Beyond the playable enclosure: oversized shapes provide a manufactured horizon.
            for (int s = -1; s <= 1; s += 2)
            {
                B("Far cooling tower", p, new Vector3(s * 31f, 13f, 37f), new Vector3(12f, 26f, 11f), navy);
                B("Far tower crown", p, new Vector3(s * 31f, 26.4f, 37f), new Vector3(14f, 1f, 13f), charcoal);
                B("Far tower orange slit", p, new Vector3(s * 31f, 18f, 31.4f), new Vector3(.6f, 12f, .1f), orange);
                B("Offset distant tower", p, new Vector3(s * 45f, 20f, 57f), new Vector3(10f, 40f, 12f), navy);
                B("Distant mast", p, new Vector3(s * 12f, 27f, 37f), new Vector3(2f, 22f, 2f), charcoal);
                B("Distant mast beacon", p, new Vector3(s * 12f, 38.2f, 37f), new Vector3(.8f, .5f, .8f), orange);
            }
            B("Northern overhead gantry", p, new Vector3(0f, 22.6f, 32f), new Vector3(65f, 1.5f, 3f), navy);
            B("Gantry lower ember", p, new Vector3(0f, 21.8f, 30.45f), new Vector3(49f, .16f, .12f), orange);
        }

        static Material M(string name, float r, float g, float b) => IPArt.Mat(name, new Color(r, g, b));
        static Transform Group(string name, Transform parent)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            obj.isStatic = true;
            return obj.transform;
        }
        static GameObject B(string name, Transform parent, Vector3 position, Vector3 scale, Material material, bool collision = false, Vector3 euler = default)
        {
            GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = name;
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = position;
            obj.transform.localScale = scale;
            obj.transform.localEulerAngles = euler;
            obj.GetComponent<Renderer>().sharedMaterial = material;
            if (!collision) IPArt.RemoveCollider(obj);
            obj.isStatic = true;
            return obj;
        }
        static void Pipe(string name, Transform parent, Vector3 pos, float radius, float length, Material mat, Vector3 euler)
        {
            // Eight-sided cylinders keep the pipes faceted and avoid the built-in cylinder's smooth silhouette.
            int count = 8;
            var vertices = new List<Vector3>();
            var tris = new List<int>();
            for (int i = 0; i < count; i++)
            {
                float a = i * Mathf.PI * 2f / count;
                float b = (i + 1) * Mathf.PI * 2f / count;
                int n = vertices.Count;
                vertices.Add(new Vector3(Mathf.Cos(a) * radius, -length * .5f, Mathf.Sin(a) * radius));
                vertices.Add(new Vector3(Mathf.Cos(a) * radius, length * .5f, Mathf.Sin(a) * radius));
                vertices.Add(new Vector3(Mathf.Cos(b) * radius, length * .5f, Mathf.Sin(b) * radius));
                vertices.Add(new Vector3(Mathf.Cos(b) * radius, -length * .5f, Mathf.Sin(b) * radius));
                tris.Add(n); tris.Add(n + 1); tris.Add(n + 2); tris.Add(n); tris.Add(n + 2); tris.Add(n + 3);
            }
            MeshObject(name, parent, vertices.ToArray(), tris.ToArray(), mat, false, pos, Quaternion.Euler(euler));
        }
        static void Disc(string name, Transform parent, Vector3 position, float radius, int segments, Material mat)
        {
            var v = new Vector3[segments + 1];
            var t = new int[segments * 3];
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                v[i + 1] = new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f);
                t[i * 3] = 0; t[i * 3 + 1] = ((i + 1) % segments) + 1; t[i * 3 + 2] = i + 1;
            }
            MeshObject(name, parent, v, t, mat, false, position, Quaternion.identity);
        }
        static void Ramp(string name, Transform parent, float x, float z0, float z1, float width, float h, Material mat)
        {
            float w = width * .5f;
            Vector3[] v = {
                new Vector3(-w, 0, z0), new Vector3(w, 0, z0), new Vector3(-w, h, z1), new Vector3(w, h, z1),
                new Vector3(-w, -.2f, z0), new Vector3(w, -.2f, z0), new Vector3(-w, -.2f, z1), new Vector3(w, -.2f, z1)
            };
            int[] t = { 0,2,1, 1,2,3, 4,5,6, 5,7,6, 0,4,2, 2,4,6, 1,3,5, 3,7,5, 2,6,3, 3,6,7, 0,1,4, 1,5,4 };
            MeshObject(name, parent, v, t, mat, true, new Vector3(x,0f,0f), Quaternion.identity);
        }
        static GameObject MeshObject(string name, Transform parent, Vector3[] vertices, int[] triangles, Material mat, bool collision, Vector3 pos, Quaternion rotation)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = pos;
            obj.transform.localRotation = rotation;
            Mesh mesh = new Mesh { name = name + " mesh" };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            obj.AddComponent<MeshFilter>().sharedMesh = mesh;
            obj.AddComponent<MeshRenderer>().sharedMaterial = mat;
            if (collision) obj.AddComponent<MeshCollider>().sharedMesh = mesh;
            obj.isStatic = true;
            return obj;
        }

        static readonly Dictionary<char, string> Glyph = new Dictionary<char, string>
        {
            {'I', "11111/00100/00100/00100/00100/00100/11111"},
            {'R', "11110/10001/10001/11110/10100/10010/10001"},
            {'O', "01110/10001/10001/10001/10001/10001/01110"},
            {'N', "10001/11001/11001/10101/10011/10011/10001"},
            {'P', "11110/10001/10001/11110/10000/10000/10000"},
            {'U', "10001/10001/10001/10001/10001/10001/01110"},
            {'L', "10000/10000/10000/10000/10000/10000/11111"},
            {'S', "01111/10000/10000/01110/00001/00001/11110"},
            {'E', "11111/10000/10000/11110/10000/10000/11111"},
            {'/', "00001/00001/00010/00100/01000/10000/10000"},
            {'0', "01110/10001/10011/10101/11001/10001/01110"},
            {'9', "01110/10001/10001/01111/00001/00001/01110"}
        };
        static void PaintText(string text, Transform parent, Vector3 pos, float cell, Material mat, Quaternion rotation)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            for (int k = 0; k < text.Length; k++)
            {
                if (!Glyph.TryGetValue(text[k], out string shape)) continue;
                string[] rows = shape.Split('/');
                for (int y = 0; y < 7; y++) for (int x = 0; x < 5; x++)
                {
                    if (rows[y][x] != '1') continue;
                    float px = (k * 6 + x) * cell;
                    float py = (6 - y) * cell;
                    float size = cell * .9f;
                    int n = vertices.Count;
                    vertices.Add(new Vector3(px, py, 0));
                    vertices.Add(new Vector3(px, py + size, 0));
                    vertices.Add(new Vector3(px + size, py + size, 0));
                    vertices.Add(new Vector3(px + size, py, 0));
                    triangles.Add(n); triangles.Add(n + 1); triangles.Add(n + 2);
                    triangles.Add(n); triangles.Add(n + 2); triangles.Add(n + 3);
                }
            }
            MeshObject("Pixel stencil | " + text, parent, vertices.ToArray(), triangles.ToArray(), mat, false, pos, rotation);
        }
    }
}
