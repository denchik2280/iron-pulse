using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace IronPulse
{
    public enum IPEnemyKind { Creep, Melee, Ranged }

    /// <summary>Procedural robots with distinct silhouettes, readable attacks and a timed execution state.</summary>
    public sealed class IPEnemy : MonoBehaviour
    {
        public static readonly List<IPEnemy> Active = new List<IPEnemy>();
        static Material sharedOutlineMaterial;
        public IPEnemyKind Kind { get; private set; }
        public bool IsDead { get; private set; }
        public bool CanExecute { get { return initialized && !IsDead && staggerTime > 0f && !executionLocked; } }
        public Vector3 ExecutionPoint { get { return transform.position + Vector3.up * (Kind == IPEnemyKind.Ranged ? 1.65f : 1.4f); } }
        public float Health { get { return health; } }
        // Live world anchors keep the player's independent gripping hand attached while this skeleton turns.
        public Vector3 ExecutionShoulderPoint { get { return leftArm != null ? leftArm.position : ExecutionPoint; } }
        public Vector3 ExecutionChestPoint { get { return torso != null ? torso.TransformPoint(new Vector3(0, 0, .22f)) : ExecutionPoint; } }
        public Vector3 ExecutionHeadPoint { get { return head != null ? head.position : ExecutionPoint; } }
        public float ExecutionFacingProgress { get; private set; }

        IPPlayer player;
        CharacterController controller;
        Transform visual, leftArm, rightArm, leftLeg, rightLeg, head, torso, muzzle, warningOrb, leftForearm, rightForearm, leftKnee, rightKnee;
        readonly List<Renderer> originals = new List<Renderer>();
        readonly List<GameObject> outlines = new List<GameObject>();
        readonly RaycastHit[] castHits = new RaycastHit[24];
        bool initialized, staggerUsed, executionLocked, attackCommitted;
        float health, maxHealth, staggerTime, staggerProtection, spawnGrace, cooldown, windup, windupDuration, animationTime, yVelocity, hitFlash;
        float modelScale, attackRange, speed, strafeSign, strideBlend, gaitPhase, attackAge = -1f, hitFlinch;
        Vector3 lastDirection, lastHitDirection, lastHitPoint, attackAim;
        Vector3 hitVelocity, executionVisualStart, movementLean;
        Quaternion executionFacingStart, executionFacingTarget, executionVisualRotationStart;
        Quaternion executionHeadStart, executionTorsoStart, executionLeftStart, executionRightStart, executionLeftLegStart, executionRightLegStart, executionLeftElbowStart, executionRightElbowStart;
        Quaternion executionLeftKneeStart, executionRightKneeStart;
        Material eyeMat, shellMat;
        IPExecutionStyle executionStyle;
        Vector3 executionCutDirection;
        bool executionFirstCut, executionSecondCut, executionFinalized, executionPunchSound;
        float stagedExecutionProgress;
        static Material cutFaceMaterial, hydraulicMaterial, sparkMaterial;
        Color eyeColor;

        public void Initialize(IPEnemyKind kind, Vector3 pos, IPPlayer target, int wave = 1)
        {
            if (initialized) return;
            Kind = kind;
            player = target;
            transform.position = pos;
            gameObject.name = kind == IPEnemyKind.Creep ? "HUSK / maintenance android" : kind == IPEnemyKind.Melee ? "RAZOR / assault cyborg" : "CINDER / heavy gunner";
            modelScale = kind == IPEnemyKind.Creep ? .86f : kind == IPEnemyKind.Melee ? 1.04f : 1.18f;
            maxHealth = (kind == IPEnemyKind.Creep ? 48f : kind == IPEnemyKind.Melee ? 112f : 154f) * (1f + Mathf.Min(wave - 1, 8) * .065f);
            health = maxHealth;
            speed = kind == IPEnemyKind.Creep ? 3.5f : kind == IPEnemyKind.Melee ? 5.9f : 2.7f;
            attackRange = kind == IPEnemyKind.Ranged ? 20f : kind == IPEnemyKind.Melee ? 2.15f : 1.65f;
            controller = gameObject.AddComponent<CharacterController>();
            controller.height = 2.02f * modelScale;
            controller.radius = kind == IPEnemyKind.Ranged ? .52f : .38f;
            controller.center = Vector3.up * (controller.height * .5f + .03f);
            controller.stepOffset = .45f;
            controller.slopeLimit = 58f;
            controller.skinWidth = .04f;
            strafeSign = Random.value < .5f ? -1f : 1f;
            spawnGrace = 1.2f;
            cooldown = Random.Range(.7f, 1.5f);
            lastHitDirection = Vector3.forward;
            BuildModel();
            initialized = true;
            Active.Add(this);
            IPGame.Burst(pos + Vector3.up, new Color(.15f, .7f, .95f), 14, 2f);
        }

        GameObject Part(string label, Transform parent, Vector3 p, Vector3 scale, Material material, PrimitiveType shape = PrimitiveType.Cube)
        {
            GameObject go = GameObject.CreatePrimitive(shape);
            go.name = label;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = p;
            go.transform.localScale = scale;
            IPArt.RemoveCollider(go);
            Renderer renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
            originals.Add(renderer);
            return go;
        }

        Material Surface(string name, Color color, float metallic, float smoothness)
        {
            Material material = IPArt.Mat(name, color);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            return material;
        }

        void Strut(string name, Transform parent, Vector3 from, Vector3 to, float radius, Material material)
        {
            Vector3 delta = to - from;
            Transform part = Part(name, parent, (from + to) * .5f, new Vector3(radius, delta.magnitude, radius), material).transform;
            part.localRotation = Quaternion.FromToRotation(Vector3.up, delta.normalized);
        }

        Transform Joint(string label, Transform parent, Vector3 p)
        {
            Transform joint = new GameObject(label).transform;
            joint.SetParent(parent, false);
            joint.localPosition = p;
            return joint;
        }

        void BuildModel()
        {
            visual = Joint("Animated mechanical skeleton", transform, Vector3.zero);
            visual.localScale = Vector3.one * modelScale;
            Material steel = Surface("Robot graphite", new Color(.09f, .115f, .14f), .76f, .34f);
            Material edges = Surface("Robot exposed alloy", new Color(.28f, .32f, .34f), .87f, .49f);
            Material black = IPArt.Mat("Robot recess", new Color(.018f, .024f, .03f));
            eyeColor = Kind == IPEnemyKind.Creep ? new Color(.08f, 1f, .88f) : Kind == IPEnemyKind.Melee ? new Color(1f, .15f, .045f) : new Color(1f, .56f, .05f);
            eyeMat = IPArt.Mat("Robot optical " + Kind, eyeColor, 4.5f);
            shellMat = Surface("Robot armor " + Kind, Kind == IPEnemyKind.Creep ? new Color(.41f, .46f, .38f) : Kind == IPEnemyKind.Melee ? new Color(.36f, .048f, .026f) : new Color(.38f, .20f, .065f), .53f, .28f);
            float broad = Kind == IPEnemyKind.Creep ? .62f : Kind == IPEnemyKind.Melee ? .83f : 1.02f;
            torso = Joint("Torso", visual, new Vector3(0, 1.3f, 0));
            Part("Armoured rib cage", torso, Vector3.zero, new Vector3(broad, .56f, .38f), shellMat);
            Part("Chest vent", torso, new Vector3(0, -.01f, .21f), new Vector3(.26f, .22f, .055f), black);
            Part("Exposed power core", torso, new Vector3(0, -.01f, .25f), new Vector3(.13f, .13f, .055f), eyeMat);
            Part("Spinal hydraulic", visual, new Vector3(0, .96f, 0), new Vector3(.22f, .27f, .23f), steel);
            Part("Pelvis", visual, new Vector3(0, .87f, 0), new Vector3(.48f, .22f, .32f), shellMat);
            head = Joint("Head joint", visual, new Vector3(0, 1.77f, 0));
            Part("Neck piston", head, new Vector3(0, -.13f, 0), new Vector3(.13f, .19f, .13f), edges);
            Part("Head casing", head, Vector3.zero, new Vector3(.37f, .36f, .32f), Kind == IPEnemyKind.Creep ? shellMat : steel);
            Part("Visor inset", head, new Vector3(0, .02f, .171f), new Vector3(.32f, .115f, .045f), black);
            Part("Luminous optical slit", head, new Vector3(0, .025f, .198f), new Vector3(.27f, .052f, .025f), eyeMat);
            Part("Jaw", head, new Vector3(0, -.135f, .12f), new Vector3(.21f, .085f, .12f), edges);
            if (Kind == IPEnemyKind.Creep)
            {
                Part("Asymmetric antenna", head, new Vector3(.14f, .31f, -.05f), new Vector3(.025f, .27f, .025f), edges);
                Part("Antenna beacon", head, new Vector3(.14f, .45f, -.05f), Vector3.one * .055f, eyeMat);
                for (int i = -1; i <= 1; i++) Part("Exposed rib", torso, new Vector3(0, i * .105f, .205f), new Vector3(.44f, .032f, .035f), edges);
            }
            else if (Kind == IPEnemyKind.Melee)
            {
                Part("Crest", head, new Vector3(0, .22f, -.02f), new Vector3(.1f, .16f, .35f), shellMat);
                Part("Chest hazard strip", torso, new Vector3(.23f, .13f, .21f), new Vector3(.12f, .055f, .04f), eyeMat);
            }
            else
            {
                Part("Heavy power backpack", visual, new Vector3(0, 1.4f, -.36f), new Vector3(.83f, .75f, .35f), steel);
                Part("Armored collar", visual, new Vector3(0, 1.65f, -.03f), new Vector3(.8f, .16f, .46f), shellMat);
                for (int side = -1; side <= 1; side += 2)
                {
                    Part("Shoulder battery", visual, new Vector3(side * .61f, 1.51f, 0), new Vector3(.32f, .4f, .49f), shellMat);
                    Part("Battery stripe", visual, new Vector3(side * .61f, 1.5f, .254f), new Vector3(.18f, .06f, .04f), eyeMat);
                    Part("Thigh armor", visual, new Vector3(side * .29f, .73f, .08f), new Vector3(.24f, .32f, .32f), shellMat);
                }
            }
            leftArm = BuildArm(-1, broad, steel, edges);
            rightArm = BuildArm(1, broad, steel, edges);
            leftLeg = BuildLeg(-1, steel, edges);
            rightLeg = BuildLeg(1, steel, edges);
            if (Kind == IPEnemyKind.Melee)
            {
                Transform blade = Part("Energized cleaver", rightForearm, new Vector3(.04f, -.31f, .19f), new Vector3(.13f, .72f, .3f), edges).transform;
                blade.localRotation = Quaternion.Euler(-16, 0, -12);
                Part("Cleaver luminous edge", blade, new Vector3(.52f, -.08f, 0), new Vector3(.13f, .91f, 1.07f), eyeMat);
            }
            if (Kind == IPEnemyKind.Ranged)
            {
                Transform cannon = Joint("Shoulder plasma cannon", visual, new Vector3(.63f, 1.83f, .08f));
                GameObject barrel = Part("Hex plasma barrel", cannon, new Vector3(0, 0, .24f), new Vector3(.25f, .48f, .25f), steel, PrimitiveType.Cylinder);
                barrel.transform.localRotation = Quaternion.Euler(90, 0, 0);
                Part("Cannon armor", cannon, new Vector3(0, .02f, -.03f), new Vector3(.4f, .32f, .42f), shellMat);
                muzzle = Joint("Plasma muzzle", cannon, new Vector3(0, 0, .52f));
                Part("Cannon bright bore", muzzle, Vector3.zero, new Vector3(.14f, .14f, .04f), eyeMat);
            }
            else muzzle = Joint("Attack origin", rightForearm, new Vector3(0, -.24f, .3f));
            BuildArmorDetail(broad, steel, edges, black);
            // Neck and shoulders belong to the moving spine, rather than floating beside an isolated chest box.
            head.SetParent(torso, true);
            leftArm.SetParent(torso, true);
            rightArm.SetParent(torso, true);
            var upperAttachments = new List<Transform>();
            foreach (Transform child in visual)
                if (child.name == "Heavy power backpack" || child.name == "Armored collar" || child.name == "Shoulder battery" ||
                    child.name == "Battery stripe" || child.name == "Shoulder plasma cannon") upperAttachments.Add(child);
            foreach (Transform attachment in upperAttachments) attachment.SetParent(torso, true);
            warningOrb = Part("Attack telegraph", visual, new Vector3(0, 2.2f, .04f), Vector3.one * .14f, IPArt.Mat("Attack warning", new Color(1f, .62f, .05f), 7), PrimitiveType.Sphere).transform;
            warningOrb.gameObject.SetActive(false);
            BuildOutline();
        }

        Transform BuildArm(int side, float broad, Material steel, Material edges)
        {
            Transform joint = Joint(side < 0 ? "Left shoulder" : "Right shoulder", visual, new Vector3(side * (broad * .5f + .09f), 1.52f, 0));
            Part("Shoulder bearing", joint, Vector3.zero, Vector3.one * .19f, edges, PrimitiveType.Sphere);
            Part("Upper arm", joint, new Vector3(0, -.21f, 0), new Vector3(.16f, .39f, .19f), steel);
            Transform elbow = Joint(side < 0 ? "Left elbow articulation" : "Right elbow articulation", joint, new Vector3(0, -.41f, 0));
            if (side < 0) leftForearm = elbow; else rightForearm = elbow;
            Part("Elbow", elbow, Vector3.zero, Vector3.one * .19f, edges, PrimitiveType.Sphere);
            Part("Forearm plate", elbow, new Vector3(0, -.15f, .04f), new Vector3(Kind == IPEnemyKind.Ranged ? .25f : .19f, .31f, .25f), shellMat);
            Part("Mechanical fist", elbow, new Vector3(0, -.35f, .06f), new Vector3(.19f, .16f, .22f), steel);
            Part("Forearm hydraulic piston", elbow, new Vector3(side * .11f, -.14f, 0), new Vector3(.035f, .28f, .035f), edges);
            Strut("Arm power cable", joint, new Vector3(side * .08f, -.08f, -.09f), new Vector3(side * .10f, -.36f, -.11f), .029f, steel);
            return joint;
        }

        Transform BuildLeg(int side, Material steel, Material edges)
        {
            Transform joint = Joint(side < 0 ? "Left hip" : "Right hip", visual, new Vector3(side * .20f, .88f, 0));
            Part("Thigh piston", joint, new Vector3(0, -.19f, 0), new Vector3(.18f, .36f, .21f), steel);
            Transform knee = Joint(side < 0 ? "Left knee articulation" : "Right knee articulation", joint, new Vector3(0, -.39f, 0));
            if (side < 0) leftKnee = knee; else rightKnee = knee;
            Part("Kneecap", knee, new Vector3(0, 0, .045f), new Vector3(.23f, .18f, .24f), shellMat);
            Part("Shin", knee, new Vector3(0, -.21f, 0), new Vector3(.17f, .35f, .18f), edges);
            Part("Heavy boot", knee, new Vector3(0, -.40f, .10f), new Vector3(.26f, .15f, .43f), steel);
            Strut("Exposed shin actuator", knee, new Vector3(side * .11f, -.04f, -.05f), new Vector3(side * .10f, -.35f, -.075f), .036f, edges);
            Part("Boot armored toe", knee, new Vector3(0, -.39f, .26f), new Vector3(.29f, .13f, .12f), shellMat);
            return joint;
        }

        void BuildArmorDetail(float broad, Material steel, Material edges, Material black)
        {
            Material cable = Surface("Cyborg / braided crimson hydraulic lines", new Color(.19f, .024f, .018f), .24f, .26f);
            Material ceramic = Surface("Cyborg / chipped pale ceramic", new Color(.53f, .50f, .39f), .1f, .21f);
            for (int side = -1; side <= 1; side += 2)
            {
                Transform arm = side < 0 ? leftArm : rightArm;
                Transform leg = side < 0 ? leftLeg : rightLeg;
                Part("Angular pectoral armor", torso, new Vector3(side * broad * .28f, .13f, .212f), new Vector3(broad * .31f, .19f, .09f), shellMat).transform.localRotation = Quaternion.Euler(0, side * -11f, side * -10f);
                for (int i = 0; i < 3; i++)
                {
                    Part("Recessed chest cooling louvre", torso, new Vector3(side * broad * .32f, -.10f - i * .042f, .204f), new Vector3(broad * .20f, .021f, .045f), black);
                    Part("Exposed abdominal rib", visual, new Vector3(side * .115f, .94f + i * .055f, .124f), new Vector3(.12f, .025f, .07f), edges).transform.localRotation = Quaternion.Euler(0, 0, side * -15f);
                }
                Part("Recessed shoulder armor", arm, new Vector3(side * .035f, -.01f, -.015f), new Vector3(Kind == IPEnemyKind.Creep ? .23f : .34f, Kind == IPEnemyKind.Creep ? .17f : .27f, .38f), shellMat);
                for (int i = 0; i < 2; i++)
                {
                    Part("Shoulder armor fastener", arm, new Vector3(side * .075f, .05f, .184f - i * .32f), Vector3.one * .045f, edges);
                    Strut("Exposed torso hose", torso, new Vector3(side * .13f, -.11f, -.21f - i * .028f), new Vector3(side * .22f, -.38f, -.12f - i * .03f), .031f, cable);
                }
                Part("Hip rotating bearing", leg, new Vector3(side * .095f, -.045f, 0), new Vector3(.065f, .13f, .17f), edges);
                Part("Thigh armor rivet", leg, new Vector3(0, -.14f, .116f), Vector3.one * .043f, edges);
                Part("Cheek angular respirator", head, new Vector3(side * .155f, -.095f, .126f), new Vector3(.095f, .13f, .105f), shellMat).transform.localRotation = Quaternion.Euler(0, side * 18f, side * -10f);
                Part("Cranial retaining screw", head, new Vector3(side * .15f, .13f, .165f), Vector3.one * .035f, edges);
            }
            for (int i = -1; i <= 1; i++) Part("Jaw grille", head, new Vector3(i * .055f, -.13f, .187f), new Vector3(.023f, .08f, .017f), black);
            for (int i = 0; i < 4; i++)
                Part("Vertebral armored spine", torso, new Vector3(0, -.24f + i * .13f, -.216f), new Vector3(.11f, .07f, .065f), edges);
            if (Kind == IPEnemyKind.Creep)
            {
                Part("Maintenance serial plate", torso, new Vector3(.19f, .13f, .232f), new Vector3(.18f, .07f, .018f), ceramic);
                Part("Exposed battery pack", torso, new Vector3(-.24f, -.12f, -.04f), new Vector3(.14f, .29f, .34f), cable);
            }
            else if (Kind == IPEnemyKind.Melee)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    Transform shoulder = side < 0 ? leftArm : rightArm;
                    Part("Assault shoulder fin", shoulder, new Vector3(side * .10f, .17f, -.02f), new Vector3(.10f, .28f, .22f), edges).transform.localRotation = Quaternion.Euler(0, 0, side * -22f);
                    Strut("Neck hydraulic cable", head, new Vector3(side * .12f, -.15f, -.07f), new Vector3(side * .10f, -.05f, -.16f), .028f, cable);
                }
                Part("Armored jaw teeth", head, new Vector3(0, -.10f, .207f), new Vector3(.21f, .026f, .039f), ceramic);
            }
            else
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    Part("Exhaust armored pipe", torso, new Vector3(side * .30f, .21f, -.40f), new Vector3(.13f, .55f, .14f), edges);
                    Part("Exhaust black aperture", torso, new Vector3(side * .30f, .495f, -.40f), new Vector3(.10f, .025f, .105f), black);
                    for (int i = 0; i < 3; i++) Part("Backpack radiator", torso, new Vector3(side * .25f, -.11f + i * .10f, -.55f), new Vector3(.23f, .038f, .09f), black);
                }
                Part("Heavy frontal armored brow", head, new Vector3(0, .11f, .16f), new Vector3(.43f, .10f, .14f), shellMat);
            }
        }

        void BuildOutline()
        {
            if (sharedOutlineMaterial == null)
            {
                sharedOutlineMaterial = new Material(IPArt.Mat("Execution orange outline", new Color(1f, .32f, .015f), 4f));
                sharedOutlineMaterial.name = "Execution silhouette / inverted hull";
            }
            Material outlineMaterial = sharedOutlineMaterial;
            // HDRP and URP both expose front-face culling under these names.
            if (outlineMaterial.HasProperty("_CullMode")) outlineMaterial.SetFloat("_CullMode", (float)CullMode.Front);
            if (outlineMaterial.HasProperty("_CullModeForward")) outlineMaterial.SetFloat("_CullModeForward", (float)CullMode.Front);
            if (outlineMaterial.HasProperty("_Cull")) outlineMaterial.SetFloat("_Cull", (float)CullMode.Front);
            foreach (Renderer renderer in originals)
            {
                if (renderer.transform == warningOrb) continue;
                MeshFilter source = renderer.GetComponent<MeshFilter>();
                if (source == null) continue;
                GameObject hull = new GameObject("Orange stagger outline");
                hull.transform.SetParent(renderer.transform, false);
                hull.transform.localScale = Vector3.one * 1.13f;
                hull.AddComponent<MeshFilter>().sharedMesh = source.sharedMesh;
                MeshRenderer shell = hull.AddComponent<MeshRenderer>();
                shell.sharedMaterial = outlineMaterial;
                shell.shadowCastingMode = ShadowCastingMode.Off;
                shell.receiveShadows = false;
                hull.SetActive(false);
                outlines.Add(hull);
            }
        }

        void Update()
        {
            if (!initialized || IsDead || player == null) return;
            float dt = Time.deltaTime;
            animationTime += dt;
            if (attackAge >= 0f) attackAge += dt;
            hitFlinch *= Mathf.Exp(-dt * 15f);
            hitVelocity *= Mathf.Exp(-dt * 18f);
            hitFlash = Mathf.Max(0f, hitFlash - dt);
            staggerProtection = Mathf.Max(0f, staggerProtection - dt);
            if (player.IsDead) { Animate(Vector3.zero, dt); return; }
            Vector3 target = player.transform.position - transform.position;
            target.y = 0;
            float distance = target.magnitude;
            Vector3 direction = distance > .02f ? target / distance : transform.forward;
            if (!executionLocked && staggerTime <= 0f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), 1f - Mathf.Exp(-dt * (windup > 0f ? 4f : 9f)));

            if (executionLocked)
            {
                ApplyGravity(Vector3.zero, dt);
                // IPPlayer advances the victim timeline exactly once after moving its controller.
                return;
            }
            if (staggerTime > 0f)
            {
                staggerTime -= dt;
                if (staggerTime <= 0f)
                {
                    SetOutline(false);
                    cooldown = .9f;
                }
                ApplyGravity(Vector3.zero, dt);
                Animate(Vector3.zero, dt);
                return;
            }
            if (spawnGrace > 0f)
            {
                spawnGrace -= dt;
                ApplyGravity(Vector3.zero, dt);
                Animate(Vector3.zero, dt);
                return;
            }
            cooldown -= dt;
            if (windup > 0f)
            {
                windup -= dt;
                float t = 1f - Mathf.Max(0, windup) / windupDuration;
                warningOrb.gameObject.SetActive(true);
                warningOrb.localScale = Vector3.one * (.10f + t * .21f);
                if (windup <= 0f && !attackCommitted)
                {
                    attackCommitted = true;
                    CommitAttack();
                    warningOrb.gameObject.SetActive(false);
                }
                ApplyGravity(Vector3.zero, dt);
                Animate(Vector3.zero, dt);
                return;
            }

            bool canSee = distance < attackRange + 1f && HasLineOfSight();
            if (cooldown <= 0f && distance <= attackRange && canSee && !player.IsExecuting)
            {
                StartAttack();
                ApplyGravity(Vector3.zero, dt);
                Animate(Vector3.zero, dt);
                return;
            }
            Vector3 move = direction;
            if (Kind == IPEnemyKind.Ranged && canSee)
            {
                move = Vector3.Cross(Vector3.up, direction) * strafeSign * .6f;
                if (distance < 7f) move -= direction * .7f;
                else if (distance > 12f) move += direction;
            }
            else if (distance < attackRange * .75f) move = Vector3.zero;
            if (move.sqrMagnitude > 1f) move.Normalize();
            move = AvoidObstacle(move);
            ApplyGravity(move * speed, dt);
            Animate(move * speed, dt);
        }

        void ApplyGravity(Vector3 horizontal, float dt)
        {
            if (controller == null || !controller.enabled) return;
            if (controller.isGrounded && yVelocity < 0f) yVelocity = -2f;
            else yVelocity -= 28f * dt;
            controller.Move((horizontal + hitVelocity + Vector3.up * yVelocity) * dt);
            lastDirection = horizontal;
            if (transform.position.y < -24f) Die(false);
        }

        Vector3 AvoidObstacle(Vector3 movement)
        {
            if (movement.sqrMagnitude < .01f) return movement;
            Vector3 origin = transform.position + Vector3.up * .9f;
            int count = Physics.SphereCastNonAlloc(origin, controller.radius * .7f, movement.normalized, castHits, 1.0f, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider collider = castHits[i].collider;
                if (collider == null || collider.transform.IsChildOf(transform) || collider.GetComponentInParent<IPPlayer>() != null) continue;
                if (castHits[i].normal.y > .55f) continue;
                Vector3 tangent = Vector3.Cross(Vector3.up, castHits[i].normal).normalized * strafeSign;
                return (movement * .23f + tangent * .9f).normalized;
            }
            return movement;
        }

        bool HasLineOfSight()
        {
            Vector3 origin = transform.position + Vector3.up * 1.35f * modelScale;
            Vector3 target = player.ViewCamera != null ? player.ViewCamera.transform.position : player.transform.position + Vector3.up * 1.4f;
            Vector3 delta = target - origin;
            int count = Physics.RaycastNonAlloc(origin, delta.normalized, castHits, delta.magnitude, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider collider = castHits[i].collider;
                if (collider == null || collider.transform.IsChildOf(transform)) continue;
                if (collider.GetComponentInParent<IPPlayer>() != null || collider.GetComponentInParent<IPEnemy>() != null) continue;
                return false;
            }
            return true;
        }

        void StartAttack()
        {
            windupDuration = Kind == IPEnemyKind.Ranged ? .68f : Kind == IPEnemyKind.Melee ? .50f : .62f;
            windup = windupDuration;
            attackAge = 0f;
            attackCommitted = false;
            attackAim = player.ViewCamera != null ? player.ViewCamera.transform.position - Vector3.up * .2f : player.transform.position + Vector3.up;
            cooldown = Kind == IPEnemyKind.Ranged ? 2.3f : Kind == IPEnemyKind.Melee ? 1.25f : 1.6f;
            IPGame.Audio("enemyWindup", transform.position, .32f);
        }

        void CommitAttack()
        {
            if (player == null || player.IsDead || player.IsExecuting) return;
            if (Kind == IPEnemyKind.Ranged)
            {
                Vector3 origin = muzzle.position;
                // Aim is locked when the bright warning appears: lateral movement dodges the burst.
                Vector3 direction = (attackAim - origin).normalized;
                IPProjectile.Fire(player, this, origin + direction * .1f, direction * 18f, 11f, eyeColor);
                IPGame.Burst(origin, eyeColor, 6, 2f);
                IPGame.Audio("enemyShot", origin, .65f);
            }
            else
            {
                float distance = Vector3.Distance(transform.position, player.transform.position);
                if (distance < attackRange + .2f && HasLineOfSight())
                {
                    player.TakeDamage(Kind == IPEnemyKind.Melee ? 15f : 8f, transform.position);
                    IPGame.Burst(player.transform.position + Vector3.up, eyeColor, 5, 2f);
                }
                IPGame.Audio("enemyShot", muzzle.position, .35f);
            }
        }

        void Animate(Vector3 movement, float dt)
        {
            if (visual == null) return;
            bool staggered = staggerTime > 0 || executionLocked;
            float measuredSpeed = controller != null && controller.enabled ? Vector3.ProjectOnPlane(controller.velocity, Vector3.up).magnitude : movement.magnitude;
            float strideTarget = staggered ? 0f : Mathf.Clamp01(Mathf.Min(measuredSpeed, movement.magnitude) / Mathf.Max(speed, .1f));
            strideBlend = Mathf.Lerp(strideBlend, strideTarget, 1f - Mathf.Exp(-dt * 12f));
            // Phase follows travelled distance, so feet stop cycling when the android meets a wall.
            gaitPhase += dt * measuredSpeed * (Kind == IPEnemyKind.Melee ? 2.3f : 2.65f) * strideBlend;
            float gait = Mathf.Sin(gaitPhase), stride = strideBlend;
            // A longer planted phase and a quicker return replace the former metronomic leg pendulum.
            float leftStep = StepCycle(gaitPhase), rightStep = StepCycle(gaitPhase + Mathf.PI);
            float legAmplitude = Kind == IPEnemyKind.Melee ? 31f : 25f;
            float legAngle = leftStep * legAmplitude * stride;
            Vector3 localMovement = transform.InverseTransformDirection(movement) / Mathf.Max(speed, .1f);
            movementLean = Vector3.Lerp(movementLean, localMovement, 1f - Mathf.Exp(-dt * 6.5f));
            float breath = Mathf.Sin(animationTime * 1.8f + (int)Kind * .7f);
            float attackPose = 0f, followThrough = 0f;
            if (!staggered && attackAge >= 0f)
            {
                attackPose = Ease(attackAge / Mathf.Max(.01f, windupDuration));
                // Anticipation takes time; the actual attack crosses its arc in 85 ms and then settles.
                followThrough = Ease((attackAge - windupDuration + .025f) / .085f);
                float recovery = Ease((attackAge - windupDuration - .105f) / .43f);
                attackPose *= 1f - recovery; followThrough *= 1f - recovery;
                if (attackAge > windupDuration + .55f) attackAge = -1f;
            }
            float blend = 1f - Mathf.Exp(-dt * 17f);
            leftLeg.localRotation = Quaternion.Slerp(leftLeg.localRotation, Quaternion.Euler(legAngle + (staggered ? -12f : 0f), movementLean.x * -5f, staggered ? -5f : movementLean.x * -4f), blend);
            rightLeg.localRotation = Quaternion.Slerp(rightLeg.localRotation, Quaternion.Euler(rightStep * legAmplitude * stride + (staggered ? 10f : 0f), movementLean.x * -5f, staggered ? 5f : movementLean.x * -4f), blend);
            leftKnee.localRotation = Quaternion.Slerp(leftKnee.localRotation, Quaternion.Euler(Mathf.Pow(Mathf.Max(0, -Mathf.Sin(gaitPhase - .27f)), 1.6f) * stride * 48f + (staggered ? 14f : 0f), 0, 0), blend);
            rightKnee.localRotation = Quaternion.Slerp(rightKnee.localRotation, Quaternion.Euler(Mathf.Pow(Mathf.Max(0, -Mathf.Sin(gaitPhase + Mathf.PI - .27f)), 1.6f) * stride * 48f + (staggered ? 11f : 0f), 0, 0), blend);
            float rightSwing = Mathf.Sin(gaitPhase - .2f) * 20f * stride - attackPose * 96f + followThrough * 151f;
            float leftSwing = Mathf.Sin(gaitPhase + Mathf.PI - .33f) * 18f * stride - attackPose * (Kind == IPEnemyKind.Ranged ? 42f : 28f) + followThrough * 31f;
            leftArm.localRotation = Quaternion.Slerp(leftArm.localRotation, Quaternion.Euler(staggered ? -22f : leftSwing, 0, staggered ? 24f : 7f + attackPose * 9f), blend);
            rightArm.localRotation = Quaternion.Slerp(rightArm.localRotation, Quaternion.Euler(staggered ? -30f : rightSwing, attackPose * -7f + followThrough * 19f, staggered ? -23f : -7f - attackPose * 12f), blend);
            leftForearm.localRotation = Quaternion.Slerp(leftForearm.localRotation, Quaternion.Euler(-14f - Mathf.Max(0, gait) * stride * 13f - attackPose * 29f + breath * 1.7f, 0, 0), blend);
            rightForearm.localRotation = Quaternion.Slerp(rightForearm.localRotation, Quaternion.Euler(-12f - Mathf.Max(0, -gait) * stride * 13f - attackPose * 48f + followThrough * 38f - breath * 1.3f, 0, 0), blend);
            float bob = (1f - Mathf.Cos(gaitPhase * 2f)) * .016f * stride;
            float staggerSway = staggered ? Mathf.Sin(animationTime * 3.2f) : 0f;
            Vector3 localHit = transform.InverseTransformDirection(lastHitDirection);
            Vector3 targetPosition = Vector3.up * (bob + breath * .0035f + (staggered ? -.13f : 0f)) + Vector3.back * (attackPose * .055f - followThrough * .10f) + Vector3.right * (gait * stride * .013f);
            visual.localPosition = Vector3.Lerp(visual.localPosition, targetPosition, blend);
            visual.localRotation = Quaternion.Slerp(visual.localRotation, Quaternion.Euler(movementLean.z * 3.2f - attackPose * 4f + followThrough * 5f, 0, -movementLean.x * 5f), 1f - Mathf.Exp(-dt * 10f));
            float torsoPitch = (staggered ? 22f : Kind == IPEnemyKind.Creep ? 9f : 1f) - hitFlinch * 17f - attackPose * 9f + followThrough * 17f;
            torso.localRotation = Quaternion.Slerp(torso.localRotation, Quaternion.Euler(torsoPitch + breath * .65f, -Mathf.Sin(gaitPhase - .15f) * stride * 6f - attackPose * 21f + followThrough * 34f + localHit.x * hitFlinch * 9f + movementLean.x * 7f, staggerSway * 2.4f - gait * stride * 2.5f), blend);
            head.localRotation = Quaternion.Slerp(head.localRotation, Quaternion.Euler((staggered ? -17f : breath * .8f) - hitFlinch * 10f + attackPose * 5f - followThrough * 8f, staggerSway * 4f + Mathf.Sin(gaitPhase - .45f) * stride * 2f - movementLean.x * 4f, staggered ? 12f : -localHit.x * hitFlinch * 13f), 1f - Mathf.Exp(-dt * 13f));
        }

        static float StepCycle(float phase)
        {
            float cycle = Mathf.Repeat(phase / (Mathf.PI * 2f), 1f);
            return cycle < .60f ? Mathf.Lerp(1f, -1f, Ease(cycle / .60f)) : Mathf.Lerp(-1f, 1f, Ease((cycle - .60f) / .40f));
        }

        static float Ease(float value) { float t = Mathf.Clamp01(value); return t * t * t * (t * (t * 6f - 15f) + 10f); }

        public void ReceiveShot(float damage, Vector3 hitPoint, Vector3 direction)
        {
            if (!initialized || IsDead || executionLocked || staggerProtection > 0f || damage <= 0f) return;
            health -= damage;
            lastHitPoint = hitPoint;
            lastHitDirection = direction.sqrMagnitude > .001f ? direction.normalized : -transform.forward;
            hitFlash = .12f;
            hitFlinch = Mathf.Min(1.1f, hitFlinch + .24f + damage / maxHealth);
            hitVelocity = Vector3.ClampMagnitude(hitVelocity + Vector3.ProjectOnPlane(lastHitDirection, Vector3.up) * Mathf.Clamp(damage * .055f, .6f, 4.2f), 4.4f);
            IPGame.Burst(hitPoint, Kind == IPEnemyKind.Creep ? new Color(.15f, 1f, .85f) : new Color(1f, .34f, .07f), 4, 4.5f);
            IPGame.Audio("enemyHit", hitPoint, Mathf.Clamp(.34f + damage * .004f, .34f, .72f));
            if (!staggerUsed && health <= maxHealth * .28f)
            {
                health = Mathf.Max(health, maxHealth * .16f);
                staggerUsed = true;
                staggerTime = 5.4f;
                staggerProtection = .08f;
                windup = 0;
                attackAge = -1f;
                warningOrb.gameObject.SetActive(false);
                SetOutline(true);
                IPGame.Audio("stagger", transform.position, .75f);
                IPGame.Label("STAGGERED // [E] KATANA FINISHER", new Color(1f, .49f, .08f));
            }
            else if (health <= 0) Die(false);
        }

        public void ReceiveMelee(float damage, Vector3 hitPoint, Vector3 direction)
        {
            if (!initialized || IsDead || executionLocked || staggerProtection > 0f || damage <= 0f) return;
            ReceiveShot(damage, hitPoint, direction);
            if (IsDead) return;
            // A punch is a physical interruption, not a hidden execution or an unconditional kill.
            Vector3 push = Vector3.ProjectOnPlane(direction, Vector3.up);
            if (push.sqrMagnitude < .001f) push = -transform.forward;
            hitVelocity = Vector3.ClampMagnitude(hitVelocity + push.normalized * 4.8f, 7.2f);
            hitFlinch = Mathf.Max(hitFlinch, .85f);
            windup = 0f; attackAge = -1f; attackCommitted = false;
            cooldown = Mathf.Max(cooldown, .48f);
            warningOrb.gameObject.SetActive(false);
        }

        public void ReceiveUltimate(Vector3 hitPoint, Vector3 cutDirection)
        {
            if (!initialized || IsDead || executionLocked) return;
            Vector3 cut = cutDirection.sqrMagnitude > .001f ? cutDirection.normalized : transform.right;
            IsDead = true; health = 0f; Active.Remove(this);
            if (controller != null) controller.enabled = false;
            SetOutline(false); warningOrb.gameObject.SetActive(false);
            Vector3 centre = transform.position + Vector3.up * 1.13f * modelScale;
            Vector3 normal = Vector3.Cross(cut, transform.forward);
            if (normal.sqrMagnitude < .2f) normal = Vector3.up;
            normal.Normalize();
            Vector3 point = hitPoint;
            point.y = Mathf.Clamp(point.y, transform.position.y + .72f * modelScale, transform.position.y + 1.63f * modelScale);
            Plane bladePlane = new Plane(normal, point);
            DetachGroup("Overdrive / sheared upper cyborg", centre, normal * 3.1f + cut * 3.5f + Vector3.up * 2.2f,
                new Vector3(94, 62, 88), source => true, bladePlane, true, false);
            DetachGroup("Overdrive / sheared lower cyborg", centre, -normal * 1.9f + cut * 1.3f + Vector3.up * 1.2f,
                new Vector3(-59, -43, -47), source => true, bladePlane, false, false);
            foreach (Renderer source in originals) if (source != null) source.enabled = false;
            CutFlash(point, cut, modelScale * 1.4f);
            CutSpray(point, cut, 12, 12);
            IPGame.Audio("execute", point, .95f);
            // Ultimate kills award the ordinary kill event. They neither grant glory-kill health nor refill themselves.
            if (IPGame.Instance != null) IPGame.Instance.EnemyKilled(this, false);
            Destroy(gameObject, .04f);
        }

        void SetOutline(bool state)
        {
            foreach (GameObject hull in outlines) if (hull != null) hull.SetActive(state);
        }

        public void BeginExecution() { BeginExecution(IPExecutionStyle.Frontal); }

        public void BeginExecution(IPExecutionStyle style)
        {
            Vector3 attacker = player != null ? player.transform.position : transform.position + transform.forward;
            BeginExecution(style, attacker);
        }

        public void BeginExecution(IPExecutionStyle style, Vector3 attackerPosition)
        {
            if (!CanExecute) return;
            executionLocked = true;
            executionStyle = style;
            executionFirstCut = false;
            executionSecondCut = executionFinalized = executionPunchSound = false;
            stagedExecutionProgress = 0;
            executionVisualStart = visual.localPosition;
            executionVisualRotationStart = visual.localRotation;
            ConfigureExecutionFacing(attackerPosition);
            executionHeadStart = head.localRotation; executionTorsoStart = torso.localRotation;
            executionLeftStart = leftArm.localRotation; executionRightStart = rightArm.localRotation;
            executionLeftLegStart = leftLeg.localRotation; executionRightLegStart = rightLeg.localRotation;
            executionLeftElbowStart = leftForearm.localRotation; executionRightElbowStart = rightForearm.localRotation;
            executionLeftKneeStart = leftKnee.localRotation; executionRightKneeStart = rightKnee.localRotation;
            staggerTime = 8f;
            windup = 0f;
            attackAge = -1f; hitVelocity = Vector3.zero;
            warningOrb.gameObject.SetActive(false);
            SetOutline(false);
        }

        public void ConfigureExecutionFacing(Vector3 attackerPosition)
        {
            executionFacingStart = transform.rotation;
            Vector3 towards = Vector3.ProjectOnPlane(attackerPosition - transform.position, Vector3.up);
            executionFacingTarget = towards.sqrMagnitude > .001f ? Quaternion.LookRotation(towards.normalized, Vector3.up) : transform.rotation;
            ExecutionFacingProgress = 0f;
        }

        public void CancelExecution()
        {
            if (IsDead || !executionLocked) return;
            executionLocked = false; staggerTime = Mathf.Max(staggerTime, 1.5f);
            SetOutline(true);
        }

        public void StageExecution(float progress)
        {
            if (!executionLocked || executionFinalized) return;
            stagedExecutionProgress = Mathf.Clamp01(progress);
            PoseExecution(progress);
            if (progress >= .31f && !executionPunchSound)
            {
                executionPunchSound = true;
                CutSpray(ExecutionChestPoint, -transform.forward + Vector3.up * .2f, 2, 4);
                IPGame.Audio("punchImpact", ExecutionChestPoint, .88f);
            }
            if (executionStyle == IPExecutionStyle.Frontal && progress > .355f && !executionFirstCut)
            {
                executionFirstCut = true;
                Vector3 at = leftForearm.position;
                SeverLimb(leftForearm, -transform.right * 3.1f + Vector3.up * 1.6f, false);
                CutSpray(at, -transform.right + Vector3.up * .25f, 9, 7);
                CutFlash(at, -transform.right, modelScale * .45f);
                IPGame.Audio("metalTear", at, .82f);
            }
            if (IsDead && progress >= .735f && !executionSecondCut)
            {
                executionSecondCut = true;
                FinalizeExecutionBody();
            }
        }

        void PoseExecution(float t)
        {
            if (visual == null) return;
            float entry = Ease(t / .19f);
            float grab = Ease((t - .075f) / .15f);
            float tension = Ease((t - .21f) / .19f);
            float punch = ImpactEnvelope(t, .295f, .324f, .416f);
            float limbTear = executionStyle == IPExecutionStyle.Frontal ? ImpactEnvelope(t, .345f, .37f, .44f) : 0f;
            float lethal = executionStyle == IPExecutionStyle.Frontal ? .58f : .52f;
            float blade = ImpactEnvelope(t, lethal - .012f, lethal + .024f, lethal + .13f);
            float collapse = Ease((t - lethal) / (.735f - lethal));
            float pull = Ease((t - .645f) / .09f);
            // One resisted twist, followed by two distinct impacts: no looping noise during restraint.
            float struggle = Mathf.Sin(Mathf.Clamp01((t - .19f) / .25f) * Mathf.PI * 2f) * Mathf.Sin(Mathf.Clamp01((t - .19f) / .25f) * Mathf.PI);
            float side = executionStyle == IPExecutionStyle.StrafeLeft ? -1f : 1f;
            ExecutionFacingProgress = Ease((t - .075f) / .205f);
            transform.rotation = Quaternion.Slerp(executionFacingStart, executionFacingTarget, ExecutionFacingProgress);
            Vector3 position = new Vector3(struggle * .015f - punch * .022f * side + pull * .045f * side,
                -.075f * grab - .075f * tension - .17f * collapse,
                .095f * grab - .07f * punch + .065f * tension - .055f * blade + .11f * pull);
            visual.localPosition = Vector3.Lerp(executionVisualStart, position, entry);
            visual.localRotation = Quaternion.Slerp(executionVisualRotationStart,
                Quaternion.Euler(-punch * 5f + collapse * 7f, struggle * 4f - pull * side * 6f, side * (punch * 3f + collapse * 5f)), entry);
            leftLeg.localRotation = Quaternion.Slerp(executionLeftLegStart, Quaternion.Euler(-12f * grab - tension * 8f - collapse * 19f, -4f * grab, -5f * grab - collapse * 8f), entry);
            rightLeg.localRotation = Quaternion.Slerp(executionRightLegStart, Quaternion.Euler(9f * grab + tension * 6f + collapse * 15f, 5f * grab, 4f * grab + collapse * 7f), entry);
            leftKnee.localRotation = Quaternion.Slerp(executionLeftKneeStart, Quaternion.Euler(17f * tension + collapse * 46f + punch * 9f, 0, 0), entry);
            rightKnee.localRotation = Quaternion.Slerp(executionRightKneeStart, Quaternion.Euler(12f * tension + collapse * 32f + blade * 12f, 0, 0), entry);
            Vector3 h, b, l, r;
            if (executionStyle == IPExecutionStyle.AerialPlunge)
            {
                h = new Vector3(-52f * tension - punch * 19f + blade * 31f, struggle * 7f, tension * 8f + blade * 17f);
                b = new Vector3(10f * tension + punch * 18f + collapse * 35f, struggle * -8f, tension * 7f + blade * 12f);
                l = new Vector3(-94f * grab + collapse * 52f, struggle * 7f, 30f * tension + blade * 22f);
                r = new Vector3(-88f * grab + collapse * 45f, struggle * -9f, -36f * tension - blade * 15f);
                visual.localPosition += Vector3.down * (blade * .055f + collapse * .10f);
            }
            else if (executionStyle == IPExecutionStyle.RearCut)
            {
                h = new Vector3(-8f * grab - punch * 31f - pull * 21f, -22f * tension + struggle * 7f + pull * 32f, -17f * tension - blade * 16f - pull * 23f);
                b = new Vector3(-9f * tension + punch * 15f + collapse * 12f, -15f * tension + struggle * 9f + blade * 16f, -5f * grab);
                l = new Vector3(-53f * grab + collapse * 32f, 16f * tension, 24f * tension);
                r = new Vector3(-37f * grab + collapse * 28f, -21f * tension, -33f * tension);
            }
            else if (executionStyle == IPExecutionStyle.DashThrust)
            {
                b = new Vector3(-14f * tension - punch * 18f - blade * 32f + collapse * 27f, struggle * 8f + pull * 14f, -blade * 5f);
                h = new Vector3(-23f * tension - punch * 23f - blade * 21f + collapse * 20f, struggle * -6f, 8f * tension + blade * 9f);
                l = new Vector3(-54f * grab - blade * 19f + collapse * 41f, 10f * grab, 43f * tension + pull * 19f);
                r = new Vector3(-48f * grab - blade * 15f + collapse * 36f, -14f * grab, -45f * tension - pull * 16f);
                visual.localPosition += Vector3.back * (blade * .085f);
            }
            else
            {
                b = new Vector3(15f * tension + punch * 24f - limbTear * 17f - blade * 21f - pull * 21f + collapse * 11f,
                    tension * 13f * side + struggle * 9f - punch * 14f * side + blade * 27f * side + pull * 18f,
                    tension * 11f * side + limbTear * 14f + blade * 17f * side);
                h = new Vector3(-18f * tension - punch * 36f - blade * 17f - pull * 19f,
                    -23f * side * tension - punch * 22f * side + struggle * 9f + pull * 24f,
                    17f * side * tension + punch * 27f * side + blade * 24f * side + pull * 22f);
                l = new Vector3(-63f * grab + limbTear * 25f + collapse * 30f, 17f * tension + struggle * 10f, 43f * tension + limbTear * 18f);
                r = new Vector3(-52f * grab + collapse * 33f - blade * 22f, -20f * tension + struggle * 8f, -39f * tension - punch * 12f);
            }
            if (executionStyle == IPExecutionStyle.RisingCut) { h.x -= blade * 28f; b.x -= blade * 18f; b.y += blade * 16f; }
            head.localRotation = Quaternion.Slerp(executionHeadStart, Quaternion.Euler(h), entry);
            torso.localRotation = Quaternion.Slerp(executionTorsoStart, Quaternion.Euler(b), entry);
            leftArm.localRotation = Quaternion.Slerp(executionLeftStart, Quaternion.Euler(l), entry);
            rightArm.localRotation = Quaternion.Slerp(executionRightStart, Quaternion.Euler(r), entry);
            leftForearm.localRotation = Quaternion.Slerp(executionLeftElbowStart, Quaternion.Euler(-38f * grab - tension * 24f + collapse * 36f + limbTear * 18f, struggle * 5f, punch * 9f), entry);
            rightForearm.localRotation = Quaternion.Slerp(executionRightElbowStart, Quaternion.Euler(-27f * grab - tension * 30f + collapse * 31f + blade * 19f, struggle * -7f, -punch * 13f), entry);
        }

        static float ImpactEnvelope(float t, float begin, float peak, float end)
        {
            if (t <= begin || t >= end) return 0f;
            return t < peak ? Ease((t - begin) / (peak - begin)) : 1f - Ease((t - peak) / (end - peak));
        }

        public void FinishExecution() { FinishExecution(IPExecutionStyle.Frontal, -transform.right); }

        public void FinishExecution(IPExecutionStyle style, Vector3 cutDirection)
        {
            if (!initialized || IsDead) return;
            executionStyle = style;
            executionCutDirection = cutDirection.sqrMagnitude > .01f ? cutDirection.normalized : transform.right;
            bool staged = executionLocked;
            IsDead = true; health = 0f;
            Active.Remove(this);
            if (controller != null) controller.enabled = false;
            SetOutline(false);
            warningOrb.gameObject.SetActive(false);
            Vector3 at = transform.position + Vector3.up * 1.30f * modelScale;
            if (style == IPExecutionStyle.StrafeLeft || style == IPExecutionStyle.StrafeRight)
            {
                Transform arm = style == IPExecutionStyle.StrafeLeft ? leftArm : rightArm;
                at = arm.position;
                SeverLimb(arm, executionCutDirection * 4.1f + Vector3.up * 2f, false);
            }
            else if (style == IPExecutionStyle.RisingCut)
            {
                at = head.position;
                SeverLimb(head, Vector3.up * 6f + executionCutDirection * 2f, true);
            }
            else if (style == IPExecutionStyle.RearCut) at = head.position - Vector3.up * .13f;
            else if (style == IPExecutionStyle.DashThrust)
                DetachGroup("Katana / punctured chest armor", at, executionCutDirection * .8f + Vector3.up, new Vector3(60, 90, 40),
                    source => source.name == "Chest vent" || source.name == "Exposed power core");
            CutSpray(at, executionCutDirection, 15, 11);
            CutFlash(at, executionCutDirection, modelScale);
            IPGame.Audio("execute", at, 1f);
            if (IPGame.Instance != null) IPGame.Instance.EnemyKilled(this, true);
            // Register the kill at blade contact, but retain every staged victim for the physical second pull.
            if (!staged) FinalizeExecutionBody();
            Destroy(gameObject, 1.15f); // Cleanup also covers an interrupted/reset finisher.
        }

        void SeverLimb(Transform joint, Vector3 launch, bool neck)
        {
            if (joint == null) return;
            int count = DetachGroup(neck ? "Severed head and torn neck cables" : "Detached articulated limb and exposed socket",
                joint.position, launch, new Vector3(110, 80, neck ? 160 : -135), source => source.transform.IsChildOf(joint));
            if (count == 0) return;
            EnsureCutMaterials();
            Transform socket = Part("Torn socket / exposed dark metal", joint, Vector3.zero,
                neck ? new Vector3(.15f, .055f, .14f) : new Vector3(.16f, .055f, .16f), cutFaceMaterial).transform;
            for (int i = -1; i <= 1; i++)
            {
                Transform cable = Part("Ruptured hydraulic cord", socket, new Vector3(i * .22f, -.8f, 0), new Vector3(.12f, 1.4f, .12f), hydraulicMaterial).transform;
                cable.localRotation = Quaternion.Euler(i * 17f, 0, i * 24f);
            }
        }

        void FinalizeExecutionBody()
        {
            if (executionFinalized) return;
            executionFinalized = true;
            Vector3 centre = transform.position + Vector3.up * 1.12f * modelScale;
            Vector3 cut = executionCutDirection.sqrMagnitude > .01f ? executionCutDirection : transform.right;
            if (executionStyle == IPExecutionStyle.RearCut)
            {
                SeverLimb(head, transform.forward * 2.7f + cut * 1.6f + Vector3.up * 2.3f, true);
                DetachGroup("Katana / collapsed decapitated body", centre, transform.forward * 1.6f + Vector3.up * .45f,
                    new Vector3(74, 16, 22), source => true);
                CutSpray(head.position - Vector3.up * .12f, -transform.forward + Vector3.up, 18, 8);
            }
            else
            {
                if (executionStyle == IPExecutionStyle.Frontal || executionStyle == IPExecutionStyle.DashThrust)
                {
                    // The fist extracts an actual connected chest assembly, exposing the clipped frame behind it.
                    Vector3 chest = ExecutionChestPoint;
                    DetachGroup("Bare hands / ripped chest plating and power core", chest,
                        transform.forward * 3.0f + transform.right * .8f + Vector3.up * 1.4f,
                        new Vector3(-110, 72, 44), source => source.transform.IsChildOf(torso) &&
                            (source.name == "Angular pectoral armor" || source.name == "Chest vent" || source.name == "Exposed power core" || source.name == "Chest hazard strip"));
                    CutSpray(chest, transform.forward + Vector3.up * .25f, 7, 5);
                }
                else if (executionStyle == IPExecutionStyle.StrafeLeft || executionStyle == IPExecutionStyle.StrafeRight)
                {
                    // After the blade removes the attacking arm, the bare hand twists the head free.
                    Vector3 neck = head.position - Vector3.up * .13f;
                    SeverLimb(head, transform.forward * 2.8f + cut * 1.5f + Vector3.up * 2f, true);
                    CutSpray(neck, transform.forward + cut * .4f, 8, 5);
                }
                Vector3 normal = Vector3.up;
                float cutHeight = 1.15f;
                if (executionStyle == IPExecutionStyle.AerialPlunge) { normal = transform.right; cutHeight = 1.38f; }
                else if (executionStyle == IPExecutionStyle.DashThrust) { normal = (transform.right + transform.forward * .3f).normalized; cutHeight = 1.28f; }
                else if (executionStyle == IPExecutionStyle.Frontal) { normal = (Vector3.up * .83f + transform.right * .37f).normalized; cutHeight = 1.27f; }
                else if (executionStyle == IPExecutionStyle.RisingCut) { normal = (Vector3.up + transform.right * .32f).normalized; cutHeight = 1.11f; }
                else { normal = (Vector3.up + transform.right * .13f).normalized; cutHeight = 1.14f; }
                Vector3 planePoint = transform.position + Vector3.up * cutHeight * modelScale;
                Plane cutPlane = new Plane(normal, planePoint);
                Vector3 separation = normal * (executionStyle == IPExecutionStyle.AerialPlunge ? 3.3f : 2.5f);
                DetachGroup("Katana / torn upper section", centre, separation + cut * 1.35f + Vector3.up * 1.9f,
                    new Vector3(105, 52, 61), source => true, cutPlane, true, false);
                DetachGroup("Katana / severed lower section", centre, -separation + cut * .5f + Vector3.up * 1.05f,
                    new Vector3(-70, -38, -56), source => true, cutPlane, false, false);
                foreach (Renderer source in originals) if (source != null) source.enabled = false;
                CutSpray(planePoint, cut, 22, 13);
                CutFlash(planePoint, cut, modelScale * .75f);
            }
            IPGame.Audio("metalTear", centre, .95f);
            Destroy(gameObject, .04f);
        }

        static void EnsureCutMaterials()
        {
            if (cutFaceMaterial == null) cutFaceMaterial = IPArt.Mat("Exposed torn cybernetic interior", new Color(.105f, .018f, .012f));
            if (hydraulicMaterial == null) hydraulicMaterial = IPArt.Mat("Hydraulic fluid", new Color(.045f, .007f, .004f));
        }

        int DetachGroup(string label, Vector3 pivot, Vector3 launch, Vector3 spin, System.Predicate<Renderer> selection,
            Plane? clipPlane = null, bool positive = true, bool hideOriginal = true)
        {
            EnsureCutMaterials();
            GameObject group = new GameObject(label); group.layer = 2; group.transform.position = pivot;
            var generated = new List<Mesh>();
            int count = 0;
            foreach (Renderer source in originals)
            {
                if (source == null || !source.enabled || !source.gameObject.activeInHierarchy || source.transform == warningOrb || !selection(source)) continue;
                MeshFilter filter = source.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null) continue;
                Mesh mesh = filter.sharedMesh;
                bool clipped = false;
                if (clipPlane.HasValue)
                {
                    Plane plane = clipPlane.Value;
                    if (PlaneCrossesBounds(plane, source.bounds))
                    {
                        mesh = ClipMesh(mesh, source.transform, plane, positive, pivot);
                        if (mesh == null) continue;
                        generated.Add(mesh); clipped = true;
                    }
                    else if ((plane.GetDistanceToPoint(source.bounds.center) >= 0f) != positive) continue;
                }
                GameObject piece = new GameObject(source.name); piece.layer = 2;
                piece.transform.SetParent(group.transform, false);
                if (!clipped)
                {
                    piece.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
                    piece.transform.localScale = source.transform.lossyScale;
                }
                piece.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = piece.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = clipped ? new[] { source.sharedMaterial, cutFaceMaterial } : source.sharedMaterials;
                renderer.shadowCastingMode = ShadowCastingMode.On; renderer.receiveShadows = true;
                if (hideOriginal) source.enabled = false;
                count++;
            }
            if (count == 0) { Destroy(group); return 0; }
            var debris = group.AddComponent<IPMechanicalDebris>();
            debris.Configure(launch, spin, null); debris.OwnMeshes(generated);
            return count;
        }

        void Die(bool executed)
        {
            if (executed) { FinishExecution(executionStyle, executionCutDirection); return; }
            if (IsDead) return;
            IsDead = true; health = 0f; Active.Remove(this);
            if (controller != null) controller.enabled = false;
            SetOutline(false); warningOrb.gameObject.SetActive(false);
            Vector3 centre = transform.position + Vector3.up * 1.05f * modelScale;
            Vector3 hitAt = lastHitPoint.sqrMagnitude > .01f ? lastHitPoint : centre;
            CutSpray(hitAt, lastHitDirection, 6, 7);
            IPGame.Audio("enemyDeath", centre, .75f);
            // Most armor stays attached to the body, giving an ordinary kill weight and a silhouette.
            DetachGroup("Ballistic / dislodged armor plates", hitAt, lastHitDirection * 3.5f + Vector3.up * 1.9f,
                new Vector3(150, 90, 85), source => source.name == "Angular pectoral armor");
            DetachGroup("Cyborg / falling articulated body", centre, lastHitDirection * 2.15f + Vector3.up * .9f,
                new Vector3(79, Vector3.Dot(lastHitDirection, transform.right) * 38f, 15), source => true);
            if (IPGame.Instance != null) IPGame.Instance.EnemyKilled(this, false);
            Destroy(gameObject, .04f);
        }

        static bool PlaneCrossesBounds(Plane plane, Bounds bounds)
        {
            Vector3 n = plane.normal, e = bounds.extents;
            float radius = Mathf.Abs(n.x) * e.x + Mathf.Abs(n.y) * e.y + Mathf.Abs(n.z) * e.z;
            return Mathf.Abs(plane.GetDistanceToPoint(bounds.center)) < radius * .96f;
        }

        struct CutVertex
        {
            public Vector3 position, normal;
            public Vector2 uv;
            public CutVertex(Vector3 p, Vector3 n, Vector2 t) { position = p; normal = n; uv = t; }
            public static CutVertex Lerp(CutVertex a, CutVertex b, float t)
            { return new CutVertex(Vector3.Lerp(a.position, b.position, t), Vector3.Lerp(a.normal, b.normal, t).normalized, Vector2.Lerp(a.uv, b.uv, t)); }
        }

        // Clip every triangle against a world-space blade plane and cap its open rim with exposed metal.
        static Mesh ClipMesh(Mesh source, Transform sourceTransform, Plane plane, bool positive, Vector3 origin)
        {
            if (source == null || !source.isReadable) return null;
            Vector3[] vertices = source.vertices, normals = source.normals;
            Vector2[] uv = source.uv;
            int[] triangles = source.triangles;
            List<Vector3> output = new List<Vector3>(), outputNormals = new List<Vector3>(), rim = new List<Vector3>();
            List<Vector2> outputUv = new List<Vector2>();
            List<int> faces = new List<int>(), caps = new List<int>();
            Matrix4x4 normalMatrix = sourceTransform.localToWorldMatrix.inverse.transpose;
            for (int i = 0; i + 2 < triangles.Length; i += 3)
            {
                List<CutVertex> polygon = new List<CutVertex>(4);
                CutVertex[] triangle = new CutVertex[3];
                for (int v = 0; v < 3; v++)
                {
                    int index = triangles[i + v];
                    Vector3 p = sourceTransform.TransformPoint(vertices[index]);
                    Vector3 n = normals.Length > index ? normalMatrix.MultiplyVector(normals[index]).normalized : plane.normal;
                    triangle[v] = new CutVertex(p, n, uv.Length > index ? uv[index] : Vector2.zero);
                }
                for (int v = 0; v < 3; v++)
                {
                    CutVertex a = triangle[v], b = triangle[(v + 1) % 3];
                    float da = plane.GetDistanceToPoint(a.position) * (positive ? 1f : -1f);
                    float db = plane.GetDistanceToPoint(b.position) * (positive ? 1f : -1f);
                    bool insideA = da >= -.0001f, insideB = db >= -.0001f;
                    if (insideA) polygon.Add(a);
                    if (insideA != insideB)
                    {
                        CutVertex edge = CutVertex.Lerp(a, b, da / (da - db));
                        polygon.Add(edge);
                        bool duplicate = false;
                        foreach (Vector3 p in rim) if ((p - edge.position).sqrMagnitude < .000002f) { duplicate = true; break; }
                        if (!duplicate) rim.Add(edge.position);
                    }
                }
                if (polygon.Count < 3) continue;
                int start = output.Count;
                foreach (CutVertex v in polygon) { output.Add(v.position - origin); outputNormals.Add(v.normal); outputUv.Add(v.uv); }
                for (int v = 1; v + 1 < polygon.Count; v++) { faces.Add(start); faces.Add(start + v); faces.Add(start + v + 1); }
            }
            if (output.Count < 3) return null;
            if (rim.Count >= 3)
            {
                Vector3 centre = Vector3.zero; foreach (Vector3 p in rim) centre += p; centre /= rim.Count;
                Vector3 normal = plane.normal * (positive ? -1f : 1f);
                Vector3 tangent = Vector3.Cross(normal, Mathf.Abs(normal.y) > .9f ? Vector3.right : Vector3.up).normalized;
                Vector3 bitangent = Vector3.Cross(normal, tangent);
                rim.Sort((a, b) => Mathf.Atan2(Vector3.Dot(a - centre, bitangent), Vector3.Dot(a - centre, tangent)).CompareTo(Mathf.Atan2(Vector3.Dot(b - centre, bitangent), Vector3.Dot(b - centre, tangent))));
                int start = output.Count;
                output.Add(centre - origin); outputNormals.Add(normal); outputUv.Add(Vector2.zero);
                foreach (Vector3 p in rim) { output.Add(p - origin); outputNormals.Add(normal); outputUv.Add(new Vector2(Vector3.Dot(p - centre, tangent), Vector3.Dot(p - centre, bitangent))); }
                for (int i = 0; i < rim.Count; i++) { caps.Add(start); caps.Add(start + i + 1); caps.Add(start + (i + 1) % rim.Count + 1); }
            }
            Mesh result = new Mesh { name = "Katana section / capped metal" };
            result.SetVertices(output); result.SetNormals(outputNormals); result.SetUVs(0, outputUv); result.subMeshCount = 2;
            result.SetTriangles(faces, 0); result.SetTriangles(caps, 1); result.RecalculateBounds();
            return result;
        }

        static void CutFlash(Vector3 point, Vector3 direction, float scale)
        {
            GameObject flash = new GameObject("Katana impact / metal shear");
            flash.layer = 2;
            LineRenderer line = flash.AddComponent<LineRenderer>();
            line.useWorldSpace = true; line.positionCount = 3;
            Vector3 dir = direction.normalized;
            line.SetPosition(0, point - dir * .74f * scale);
            line.SetPosition(1, point + Vector3.up * .025f);
            line.SetPosition(2, point + dir * .85f * scale);
            line.startWidth = .026f; line.endWidth = .011f;
            if (sparkMaterial == null) sparkMaterial = IPArt.Mat("Katana sparks / warm steel", new Color(1f, .66f, .28f), 2f);
            line.sharedMaterial = sparkMaterial;
            Destroy(flash, .07f);
        }

        static void CutSpray(Vector3 point, Vector3 direction, int oilCount, int sparkCount)
        {
            if (hydraulicMaterial == null) hydraulicMaterial = IPArt.Mat("Hydraulic fluid", new Color(.025f, .012f, .009f));
            if (sparkMaterial == null) sparkMaterial = IPArt.Mat("Katana sparks / warm steel", new Color(1f, .66f, .28f), 2f);
            Vector3 lateral = Vector3.Cross(direction.normalized, Vector3.up);
            if (lateral.sqrMagnitude < .1f) lateral = Vector3.right;
            for (int i = 0; i < oilCount + sparkCount; i++)
            {
                bool spark = i >= oilCount;
                GameObject particle = GameObject.CreatePrimitive(PrimitiveType.Cube);
                particle.name = spark ? "Sheared metal spark" : "Severed hydraulic line / oil";
                particle.layer = 2; IPArt.RemoveCollider(particle);
                particle.transform.position = point + Random.insideUnitSphere * .09f;
                particle.transform.localScale = spark ? new Vector3(.009f, .009f, Random.Range(.06f, .15f)) : Vector3.one * Random.Range(.018f, .043f);
                particle.GetComponent<Renderer>().sharedMaterial = spark ? sparkMaterial : hydraulicMaterial;
                Vector3 velocity = direction.normalized * Random.Range(1.3f, 4.8f) + lateral * Random.Range(-2.8f, 2.8f) + Vector3.up * Random.Range(.5f, 3.4f) + Random.insideUnitSphere;
                if (spark) velocity *= 1.7f;
                particle.AddComponent<IPMechanicalParticle>().Initialize(velocity, spark);
            }
        }

        void OnDestroy() { Active.Remove(this); }
    }

    /// <summary>Decorative sections with local ballistic collision; never obstruct the player or a shot.</summary>
    public sealed class IPMechanicalDebris : MonoBehaviour
    {
        float life = 6f;
        Vector3 originalScale, velocity, angularVelocity;
        Mesh ownedMesh;
        readonly List<Mesh> ownedSections = new List<Mesh>();
        Bounds localBounds;
        bool hasBounds;
        bool resting;
        public void Configure(Vector3 launch, Vector3 spin, Mesh generatedMesh)
        { velocity = launch; angularVelocity = spin; ownedMesh = generatedMesh; originalScale = transform.localScale; }
        public void OwnMeshes(List<Mesh> generated) { if (generated != null) ownedSections.AddRange(generated); }
        void Start()
        {
            if (originalScale == Vector3.zero) originalScale = transform.localScale;
            foreach (Renderer renderer in GetComponentsInChildren<Renderer>())
            {
                Bounds bounds = renderer.bounds;
                for (int x = -1; x <= 1; x += 2)
                 for (int y = -1; y <= 1; y += 2)
                  for (int z = -1; z <= 1; z += 2)
                  {
                   Vector3 p = transform.InverseTransformPoint(bounds.center + Vector3.Scale(bounds.extents, new Vector3(x, y, z)));
                   if (!hasBounds) { localBounds = new Bounds(p, Vector3.zero); hasBounds = true; } else localBounds.Encapsulate(p);
                  }
            }
        }
        float SupportDistance(Vector3 normal)
        {
            if (!hasBounds) return .075f;
            Vector3 n = transform.InverseTransformDirection(normal), extents = localBounds.extents;
            float radius = Mathf.Abs(n.x) * extents.x + Mathf.Abs(n.y) * extents.y + Mathf.Abs(n.z) * extents.z;
            return Mathf.Max(.035f, radius - Vector3.Dot(localBounds.center, n));
        }
        void Update()
        {
            float dt = Time.deltaTime; life -= dt;
            if (!resting && dt > 0f)
            {
                velocity += Vector3.down * (20f * dt);
                Vector3 delta = velocity * dt;
                transform.Rotate(angularVelocity * dt, Space.World);
                float support = SupportDistance(-delta.normalized);
                if (delta.sqrMagnitude > .000001f && Physics.Raycast(transform.position, delta.normalized, out RaycastHit hit, delta.magnitude + support, ~(1 << 2), QueryTriggerInteraction.Ignore))
                {
                    transform.position = hit.point + hit.normal * (SupportDistance(hit.normal) + .012f);
                    velocity = Vector3.Reflect(velocity, hit.normal) * .22f;
                    angularVelocity *= .3f;
                    if (velocity.magnitude < 1.7f && hit.normal.y > .6f) resting = true;
                }
                else transform.position += delta;
                velocity *= Mathf.Exp(-dt * .25f);
            }
            if (life < 1f) transform.localScale = originalScale * Mathf.Max(0f, life);
            if (life <= 0 || transform.position.y < -18f) Destroy(gameObject);
        }
        void OnDestroy()
        {
            if (ownedMesh != null) Destroy(ownedMesh);
            foreach (Mesh section in ownedSections) if (section != null) Destroy(section);
        }
    }

    public sealed class IPMechanicalParticle : MonoBehaviour
    {
        Vector3 velocity, size;
        float life;
        bool isSpark, settled;
        public void Initialize(Vector3 launch, bool spark)
        { velocity = launch; isSpark = spark; life = spark ? Random.Range(.12f, .34f) : Random.Range(1.9f, 3.5f); size = transform.localScale; }
        void Update()
        {
            float dt = Time.deltaTime; life -= dt;
            if (!settled)
            {
                velocity += Vector3.down * (18f * dt);
                Vector3 delta = velocity * dt;
                if (delta.sqrMagnitude > .000001f && Physics.Raycast(transform.position, delta.normalized, out RaycastHit hit, delta.magnitude + .01f, ~(1 << 2), QueryTriggerInteraction.Ignore))
                {
                    if (isSpark) { Destroy(gameObject); return; }
                    transform.position = hit.point + hit.normal * .008f;
                    transform.rotation = Quaternion.FromToRotation(Vector3.up, hit.normal);
                    size = new Vector3(size.x * 3f, .007f, size.z * 3f);
                    transform.localScale = size; settled = true;
                }
                else transform.position += delta;
                if (isSpark && velocity.sqrMagnitude > .01f) transform.rotation = Quaternion.LookRotation(velocity);
            }
            if (life < .35f) transform.localScale = size * Mathf.Clamp01(life / .35f);
            if (life <= 0f) Destroy(gameObject);
        }
    }
}


