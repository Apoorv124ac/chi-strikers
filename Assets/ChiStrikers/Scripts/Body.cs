using System.Collections.Generic;
using UnityEngine;

namespace ChiStrikers
{
    public enum Pose { Idle, Run, Kick, Pass, Poke, Slide, Down, Air, Volley, Dive, Catch, Ready, Slam, WindUp, Celebrate, Flying }

    /// <summary>
    /// A procedural cartoon athlete, modelled on the reference reel: about 6.5 heads tall, legs half the height,
    /// slim tapered limbs, slightly oversized mitt hands, chunky sneakers, a loose jersey that flares at the hem,
    /// dot eyes with a heavy brow, and a hair silhouette that gives each player personality.
    /// Flat 2-tone toon colours with ink outlines. Animated in code with snappy, keyframe-like poses.
    /// </summary>
    public sealed class Body
    {
        public Transform Root, Rig, Spine, Head, ArmL, ArmR, ForeL, ForeR, LegL, LegR, ShinL, ShinR, FootL, FootR, Tail;
        public Transform HandR, HandL;
        public Look Look;
        public float H; // height scale

        // animation inputs
        public Pose Pose;
        public float T;          // 0..1 progress inside an action pose
        public float Speed;      // m/s
        public float Side;       // dive side (+1 right of the player, -1 left)
        public float Lift;       // extra rig height (jumps)
        public float FlipX, SpinY; // whole-body flips / spins in degrees (aerial specials)

        float phase, breathe, hop;
        readonly List<Transform> all = new List<Transform>();
        Body mirror;
        Transform mirrorRoot;

        const float HipY = 0.93f;

        public static Body Build(Transform parent, Look look, bool forMirror = false)
        {
            var b = new Body { Look = look, H = look.Height <= 0 ? 1 : look.Height };
            b.Construct(parent, forMirror);
            return b;
        }

        void Construct(Transform parent, bool forMirror)
        {
            var L = Look;
            float bulk = L.Bulk <= 0 ? 1 : L.Bulk;
            float fem = L.Female ? 0.88f : 1f;

            Root = new GameObject("Body").transform;
            Root.SetParent(parent, false);
            Root.localScale = Vector3.one * H;

            var skin = Util.Lit(L.Skin);
            var shirt = Util.Lit(L.Shirt);
            var shirt2 = Util.Lit(L.Shirt2);
            var shorts = Util.Lit(L.Shorts);
            var socks = Util.Lit(L.Socks);
            var boots = Util.Lit(L.Boots);
            var hairM = Util.Lit(L.Hair);
            var ink = Util.Flat(Util.Hex("#1b1b22"));
            bool keeper = L.Number == 1;

            Rig = Node("Rig", Root, new Vector3(0, HipY, 0));

            // ---- pelvis + shorts
            Util.Prim(PrimitiveType.Sphere, Rig, new Vector3(0, -0.02f, 0), new Vector3(0.30f * bulk * fem + (L.Female ? 0.03f : 0), 0.2f, 0.2f * bulk), shorts);

            // ---- legs
            LegL = Node("LegL", Rig, new Vector3(-0.09f * bulk, -0.04f, 0));
            LegR = Node("LegR", Rig, new Vector3(0.09f * bulk, -0.04f, 0));
            foreach (var leg in new[] { LegL, LegR })
            {
                bool left = leg == LegL;
                // shorts leg + thigh
                Util.Prim(PrimitiveType.Capsule, leg, new Vector3(0, -0.1f, 0), new Vector3(0.16f * bulk, 0.12f, 0.16f * bulk), shorts);
                Util.Prim(PrimitiveType.Capsule, leg, new Vector3(0, -0.22f, 0), new Vector3(0.11f * bulk, 0.22f, 0.11f * bulk), skin);
                var shin = Node("Shin", leg, new Vector3(0, -0.43f, 0));
                Util.Prim(PrimitiveType.Capsule, shin, new Vector3(0, -0.1f, 0), new Vector3(0.088f * bulk, 0.12f, 0.088f * bulk), skin);
                Util.Prim(PrimitiveType.Capsule, shin, new Vector3(0, -0.27f, 0), new Vector3(0.095f * bulk, 0.13f, 0.095f * bulk), socks);
                var foot = Node("Foot", shin, new Vector3(0, -0.37f, 0));
                // chunky sneaker: rounded upper + flat sole
                Util.Prim(PrimitiveType.Sphere, foot, new Vector3(0, -0.02f, 0.06f), new Vector3(0.13f, 0.11f, 0.29f), boots);
                Util.Prim(PrimitiveType.Cube, foot, new Vector3(0, -0.075f, 0.06f), new Vector3(0.13f, 0.035f, 0.29f), Util.Lit(L.Accent, 1.5f));
                if (left) { ShinL = shin; FootL = foot; } else { ShinR = shin; FootR = foot; }
            }

            // ---- torso
            Spine = Node("Spine", Rig, new Vector3(0, 0.06f, 0));
            // loose jersey: flared hem + lean V chest
            Util.Prim(PrimitiveType.Cylinder, Spine, new Vector3(0, 0.05f, 0), new Vector3(0.34f * bulk * fem, 0.06f, 0.24f * bulk), shirt);
            Util.Prim(PrimitiveType.Capsule, Spine, new Vector3(0, 0.17f, 0), new Vector3(0.29f * bulk * fem, 0.16f, 0.2f * bulk), shirt);
            Util.Prim(PrimitiveType.Sphere, Spine, new Vector3(0, 0.36f, 0), new Vector3(0.43f * bulk * fem, 0.27f, 0.23f * bulk), shirt);
            // collar + side stripe in the trim colour
            Util.Prim(PrimitiveType.Cylinder, Spine, new Vector3(0, 0.48f, 0), new Vector3(0.15f, 0.02f, 0.13f), shirt2);
            Util.Prim(PrimitiveType.Cube, Spine, new Vector3(-0.2f * bulk * fem, 0.27f, 0), new Vector3(0.03f, 0.28f, 0.12f * bulk), shirt2);
            Util.Prim(PrimitiveType.Cube, Spine, new Vector3(0.2f * bulk * fem, 0.27f, 0), new Vector3(0.03f, 0.28f, 0.12f * bulk), shirt2);
            if (L.Number > 0)
            {
                Util.Text3D(Spine, L.Number.ToString(), new Vector3(0, 0.32f, -0.125f * bulk), new Vector3(0, 180, 0), 0.22f, L.Shirt2);
                Util.Text3D(Spine, L.Number.ToString(), new Vector3(0.09f, 0.4f, 0.12f * bulk), Vector3.zero, 0.08f, L.Shirt2);
            }

            // ---- neck + head (small head, ~1/6.5 of the height)
            Util.Prim(PrimitiveType.Capsule, Spine, new Vector3(0, 0.53f, 0), new Vector3(0.1f, 0.06f, 0.1f), skin);
            Head = Node("Head", Spine, new Vector3(0, 0.58f, 0));
            BuildHead(Head, skin, hairM, ink);

            // ---- arms (pivot at the shoulder)
            float sh = 0.235f * bulk * fem;
            ArmL = Node("ArmL", Spine, new Vector3(-sh, 0.43f, 0));
            ArmR = Node("ArmR", Spine, new Vector3(sh, 0.43f, 0));
            foreach (var arm in new[] { ArmL, ArmR })
            {
                bool left = arm == ArmL;
                Util.Prim(PrimitiveType.Sphere, arm, new Vector3(0, -0.03f, 0), new Vector3(0.13f * bulk, 0.13f, 0.13f * bulk), shirt); // sleeve cap
                Util.Prim(PrimitiveType.Capsule, arm, new Vector3(0, -0.075f, 0), new Vector3(0.115f * bulk, 0.07f, 0.115f * bulk), shirt); // short sleeve
                Util.Prim(PrimitiveType.Capsule, arm, new Vector3(0, -0.15f, 0), new Vector3(0.078f * bulk, 0.13f, 0.078f * bulk), keeper ? shirt : skin);
                var fore = Node("Fore", arm, new Vector3(0, -0.29f, 0));
                Util.Prim(PrimitiveType.Capsule, fore, new Vector3(0, -0.12f, 0), new Vector3(0.07f * bulk, 0.13f, 0.07f * bulk), keeper ? shirt : skin);
                if (L.Wristbands) Util.Prim(PrimitiveType.Cylinder, fore, new Vector3(0, -0.21f, 0), new Vector3(0.08f, 0.025f, 0.08f), shirt2);
                var hand = Node("Hand", fore, new Vector3(0, -0.29f, 0));
                // oversized mitt (keepers get big gloves)
                float hs = keeper ? 0.15f : 0.11f;
                Util.Prim(PrimitiveType.Sphere, hand, Vector3.zero, new Vector3(hs * 0.8f, hs, hs * 0.9f), keeper ? Util.Lit(L.Accent) : skin);
                if (left) { ForeL = fore; HandL = hand; } else { ForeR = fore; HandR = hand; }
            }

            Root.GetComponentsInChildren(true, all);
            if (!forMirror)
            {
                // the glossy-floor reflection: a mirrored twin below y = 0, seen through the semi-transparent floor
                mirror = Build(parent, L, true);
                mirrorRoot = mirror.Root;
                mirrorRoot.name = "Reflection";
                foreach (var r in mirrorRoot.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        void BuildHead(Transform head, Material skin, Material hairM, Material ink)
        {
            var L = Look;
            // skull + jaw
            Util.Prim(PrimitiveType.Sphere, head, new Vector3(0, 0.12f, 0.005f), new Vector3(0.2f, 0.235f, 0.215f), skin);
            Util.Prim(PrimitiveType.Sphere, head, new Vector3(0, 0.05f, 0.03f), new Vector3(0.16f, 0.13f, 0.16f), skin);
            // ears
            Util.Prim(PrimitiveType.Sphere, head, new Vector3(-0.1f, 0.11f, 0), new Vector3(0.04f, 0.06f, 0.035f), skin);
            Util.Prim(PrimitiveType.Sphere, head, new Vector3(0.1f, 0.11f, 0), new Vector3(0.04f, 0.06f, 0.035f), skin);
            // face: dot eyes, heavy brow, tiny mouth
            float ez = 0.103f;
            if (L.Visor)
                Util.Prim(PrimitiveType.Cube, head, new Vector3(0, 0.135f, ez), new Vector3(0.2f, 0.045f, 0.03f), Util.Emissive(L.Accent), default, false);
            else
            {
                Util.Prim(PrimitiveType.Sphere, head, new Vector3(-0.042f, 0.13f, ez), new Vector3(0.026f, 0.034f, 0.015f), ink, default, false);
                Util.Prim(PrimitiveType.Sphere, head, new Vector3(0.042f, 0.13f, ez), new Vector3(0.026f, 0.034f, 0.015f), ink, default, false);
            }
            var browC = Util.Flat(L.Age > 0.5f ? Util.Hex("#d8d4cc") : Util.Hex("#1b1b22"));
            Util.Prim(PrimitiveType.Cube, head, new Vector3(-0.045f, 0.165f, ez - 0.004f), new Vector3(0.05f, 0.013f, 0.012f), browC, new Vector3(0, 0, L.Female ? 6 : -8), false);
            Util.Prim(PrimitiveType.Cube, head, new Vector3(0.045f, 0.165f, ez - 0.004f), new Vector3(0.05f, 0.013f, 0.012f), browC, new Vector3(0, 0, L.Female ? -6 : 8), false);
            Util.Prim(PrimitiveType.Cube, head, new Vector3(0, 0.06f, 0.104f), new Vector3(0.032f, 0.007f, 0.01f), ink, default, false);
            Util.Prim(PrimitiveType.Sphere, head, new Vector3(0, 0.1f, 0.112f), new Vector3(0.028f, 0.035f, 0.03f), skin);

            if (L.Moustache) Util.Prim(PrimitiveType.Capsule, head, new Vector3(0, 0.077f, 0.1f), new Vector3(0.03f, 0.055f, 0.025f), Util.Lit(Util.Hex("#1a1210"), 1.5f), new Vector3(0, 0, 90));
            if (L.Beard)
            {
                var bc = L.Style == Hair.Turban ? Util.Lit(Util.Hex("#1a1414")) : hairM;
                Util.Prim(PrimitiveType.Sphere, head, new Vector3(0, 0.03f, 0.05f), new Vector3(0.17f, 0.13f, 0.13f), bc);
                Util.Prim(PrimitiveType.Capsule, head, new Vector3(0, 0.077f, 0.1f), new Vector3(0.03f, 0.05f, 0.025f), bc, new Vector3(0, 0, 90));
            }

            switch (L.Style)
            {
                case Hair.Short: // spiky anime clumps
                    Util.Prim(PrimitiveType.Sphere, head, new Vector3(0, 0.17f, -0.01f), new Vector3(0.215f, 0.2f, 0.225f), hairM);
                    for (int i = 0; i < 5; i++)
                    {
                        float a = -50 + i * 25;
                        Util.Prim(PrimitiveType.Capsule, head, new Vector3(Mathf.Sin(a * Mathf.Deg2Rad) * 0.06f, 0.26f, -0.02f - Mathf.Abs(a) * 0.0006f),
                            new Vector3(0.05f, 0.07f, 0.05f), hairM, new Vector3(-35, 0, -a * 0.7f));
                    }
                    Util.Prim(PrimitiveType.Capsule, head, new Vector3(0.03f, 0.22f, 0.09f), new Vector3(0.05f, 0.05f, 0.04f), hairM, new Vector3(60, 0, 30));
                    break;
                case Hair.Ponytail:
                    Util.Prim(PrimitiveType.Sphere, head, new Vector3(0, 0.165f, -0.012f), new Vector3(0.215f, 0.2f, 0.225f), hairM);
                    Util.Prim(PrimitiveType.Sphere, head, new Vector3(0, 0.2f, -0.1f), new Vector3(0.06f, 0.06f, 0.06f), Util.Lit(L.Accent));
                    Tail = Node("Tail", head, new Vector3(0, 0.2f, -0.11f));
                    Util.Prim(PrimitiveType.Capsule, Tail, new Vector3(0, -0.11f, -0.02f), new Vector3(0.07f, 0.12f, 0.07f), hairM);
                    break;
                case Hair.Turban: // patka-style turban
                    Util.Prim(PrimitiveType.Sphere, head, new Vector3(0, 0.19f, -0.005f), new Vector3(0.225f, 0.22f, 0.235f), hairM);
                    Util.Prim(PrimitiveType.Sphere, head, new Vector3(0, 0.26f, 0.0f), new Vector3(0.17f, 0.12f, 0.19f), hairM, new Vector3(-12, 0, 0));
                    Util.Prim(PrimitiveType.Cube, head, new Vector3(0, 0.19f, 0.105f), new Vector3(0.12f, 0.025f, 0.02f), Util.Lit(Util.Hex("#f2f2f0"), 1.2f), new Vector3(0, 0, 0));
                    break;
                case Hair.Bun:
                    Util.Prim(PrimitiveType.Sphere, head, new Vector3(0, 0.16f, -0.015f), new Vector3(0.21f, 0.19f, 0.22f), hairM);
                    Util.Prim(PrimitiveType.Sphere, head, new Vector3(0, 0.27f, -0.05f), new Vector3(0.1f, 0.09f, 0.1f), hairM);
                    break;
                case Hair.Buzz:
                case Hair.Visor:
                    Util.Prim(PrimitiveType.Sphere, head, new Vector3(0, 0.15f, -0.01f), new Vector3(0.207f, 0.215f, 0.22f), hairM);
                    break;
                case Hair.Mohawk:
                    Util.Prim(PrimitiveType.Sphere, head, new Vector3(0, 0.14f, -0.01f), new Vector3(0.205f, 0.21f, 0.218f), Util.Lit(Util.Hex("#3a2a20")));
                    for (int i = 0; i < 4; i++)
                        Util.Prim(PrimitiveType.Capsule, head, new Vector3(0, 0.25f - i * 0.012f, 0.06f - i * 0.055f), new Vector3(0.04f, 0.07f, 0.06f), hairM, new Vector3(-20 - i * 18, 0, 0));
                    break;
                case Hair.Bald:
                    break;
            }
        }

        static Transform Node(string name, Transform parent, Vector3 pos)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = pos;
            return t;
        }

        public void SetVisible(bool on)
        {
            Root.gameObject.SetActive(on);
            if (mirrorRoot) mirrorRoot.gameObject.SetActive(on);
        }

        public void Destroy()
        {
            if (Root) Object.Destroy(Root.gameObject);
            if (mirrorRoot) Object.Destroy(mirrorRoot.gameObject);
        }

        // ------------------------------------------------------------------ animation
        struct Target { public Vector3 legL, legR, shinL, shinR, footL, footR, armL, armR, foreL, foreR, spine, head; public float rigY, rigPitch, rigRoll; }

        public void Animate(float dt)
        {
            breathe += dt;
            var p = new Target();
            float s = Mathf.Clamp01(Speed / 8f);

            switch (Pose)
            {
                case Pose.Idle:
                case Pose.Run:
                {
                    phase += dt * (Speed > 0.3f ? (5.5f + Speed * 0.75f) : 0);
                    if (Speed > 0.3f)
                    {
                        float A = Mathf.Lerp(18, 48, s);
                        float sp = Mathf.Sin(phase), cp = Mathf.Cos(phase);
                        p.legL = new Vector3(-sp * A, 0, 0);
                        p.legR = new Vector3(sp * A, 0, 0);
                        p.shinL = new Vector3(15 + Mathf.Lerp(30, 85, s) * Mathf.Max(0, cp), 0, 0);
                        p.shinR = new Vector3(15 + Mathf.Lerp(30, 85, s) * Mathf.Max(0, -cp), 0, 0);
                        p.armL = new Vector3(sp * A * 0.9f, 0, -8);
                        p.armR = new Vector3(-sp * A * 0.9f, 0, 8);
                        p.foreL = p.foreR = new Vector3(-Mathf.Lerp(40, 85, s), 0, 0);
                        p.spine = new Vector3(Mathf.Lerp(4, 16, s), sp * 6, 0);
                        p.rigY = Mathf.Abs(Mathf.Sin(phase)) * 0.06f * s - 0.02f * s;
                    }
                    else
                    {
                        // athletic ready stance with a small bounce
                        hop += dt * 6;
                        float b = Mathf.Sin(hop) * 0.5f + 0.5f;
                        p.legL = new Vector3(-14, 0, -4); p.legR = new Vector3(-14, 0, 4);
                        p.shinL = p.shinR = new Vector3(26 + b * 6, 0, 0);
                        p.footL = p.footR = new Vector3(-12, 0, 0);
                        p.armL = new Vector3(-12, 0, -14); p.armR = new Vector3(-12, 0, 14);
                        p.foreL = p.foreR = new Vector3(-55, 0, 0);
                        p.spine = new Vector3(10, 0, 0);
                        p.rigY = -0.06f - b * 0.02f;
                    }
                    break;
                }
                case Pose.Kick:
                case Pose.Pass:
                case Pose.Volley:
                {
                    // wind-up (leg cocked back) → strike → follow-through. Strike happens at T = 0.4.
                    bool big = Pose != Pose.Pass;
                    float t = T;
                    float leg, shin;
                    if (t < 0.4f) { float k = t / 0.4f; leg = Mathf.Lerp(0, big ? 70 : 35, k); shin = Mathf.Lerp(10, big ? 100 : 60, k); }
                    else if (t < 0.55f) { float k = (t - 0.4f) / 0.15f; leg = Mathf.Lerp(big ? 70 : 35, big ? -95 : -45, k); shin = Mathf.Lerp(big ? 100 : 60, 0, k); }
                    else { float k = (t - 0.55f) / 0.45f; leg = Mathf.Lerp(big ? -95 : -45, -10, k * k); shin = Mathf.Lerp(0, 15, k); }
                    p.legR = new Vector3(leg, 0, 0); p.shinR = new Vector3(shin, 0, 0);
                    p.footR = new Vector3(big ? 25 : 10, 0, 0);
                    p.legL = new Vector3(-12, 0, -4); p.shinL = new Vector3(25, 0, 0);
                    p.armL = new Vector3(-30, 0, big ? -70 : -35); p.armR = new Vector3(30, 0, big ? 55 : 25);
                    p.foreL = p.foreR = new Vector3(-25, 0, 0);
                    p.spine = new Vector3(t < 0.4f ? -8 : 12, t < 0.4f ? 18 : -14, 0);
                    p.rigY = -0.05f;
                    if (Pose == Pose.Volley) { p.legL = new Vector3(-80, 0, 0); p.shinL = new Vector3(110, 0, 0); }
                    break;
                }
                case Pose.Poke:
                {
                    float k = Mathf.Sin(Mathf.Clamp01(T) * Mathf.PI);
                    p.legR = new Vector3(-70 * k, 0, 0); p.shinR = new Vector3(10, 0, 0);
                    p.legL = new Vector3(-25, 0, 0); p.shinL = new Vector3(55, 0, 0);
                    p.armL = new Vector3(-40, 0, -40); p.armR = new Vector3(30, 0, 40);
                    p.foreL = p.foreR = new Vector3(-30, 0, 0);
                    p.spine = new Vector3(18 * k, 0, 0);
                    p.rigY = -0.18f * k;
                    break;
                }
                case Pose.Slide:
                {
                    p.rigPitch = -62; p.rigY = -0.62f;
                    p.legR = new Vector3(-28, 0, 0); p.shinR = new Vector3(0, 0, 0);
                    p.legL = new Vector3(20, 0, 0); p.shinL = new Vector3(90, 0, 0);
                    p.armL = new Vector3(40, 0, -50); p.armR = new Vector3(-60, 0, 50);
                    p.foreL = p.foreR = new Vector3(-20, 0, 0);
                    p.spine = new Vector3(20, 0, 0); p.head = new Vector3(30, 0, 0);
                    break;
                }
                case Pose.Down:
                {
                    p.rigPitch = -86; p.rigY = -0.8f;
                    p.legL = new Vector3(-8, 0, -12); p.legR = new Vector3(-20, 0, 10); p.shinR = new Vector3(40, 0, 0);
                    p.armL = new Vector3(0, 0, -80); p.armR = new Vector3(0, 0, 95);
                    p.head = new Vector3(-10, 0, 0);
                    break;
                }
                case Pose.Flying: // launched by a special: spread-eagle ragdoll
                {
                    float w = Mathf.Sin(breathe * 18) * 12;
                    p.legL = new Vector3(-30 + w, 0, -25); p.legR = new Vector3(20 - w, 0, 25);
                    p.shinL = p.shinR = new Vector3(40, 0, 0);
                    p.armL = new Vector3(-20, 0, -130 + w); p.armR = new Vector3(10, 0, 130 - w);
                    p.spine = new Vector3(-20, 0, 0); p.head = new Vector3(-25, 0, 0);
                    p.rigPitch = -40;
                    break;
                }
                case Pose.Air:
                {
                    // tucked jump (snappy apex hang)
                    p.legL = new Vector3(-70, 0, -6); p.shinL = new Vector3(120, 0, 0);
                    p.legR = new Vector3(-45, 0, 6); p.shinR = new Vector3(100, 0, 0);
                    p.armL = new Vector3(-20, 0, -110); p.armR = new Vector3(-20, 0, 110);
                    p.foreL = p.foreR = new Vector3(-30, 0, 0);
                    p.spine = new Vector3(14, 0, 0);
                    break;
                }
                case Pose.Dive:
                {
                    float k = Mathf.SmoothStep(0, 1, Mathf.Clamp01(T * 2.2f));
                    p.rigRoll = -82 * Side * k;
                    p.rigY = Mathf.Sin(Mathf.Clamp01(T) * Mathf.PI) * 0.45f - 0.35f * k;
                    p.armL = new Vector3(-20, 0, -165); p.armR = new Vector3(-20, 0, 165);
                    p.foreL = p.foreR = new Vector3(-10, 0, 0);
                    p.legL = new Vector3(-10, 0, -15 * Side); p.legR = new Vector3(10, 0, -25 * Side); p.shinL = new Vector3(30, 0, 0);
                    break;
                }
                case Pose.Catch:
                {
                    p.armL = new Vector3(-70, 0, 10); p.armR = new Vector3(-70, 0, -10);
                    p.foreL = p.foreR = new Vector3(-55, 0, 0);
                    p.legL = new Vector3(-10, 0, 0); p.legR = new Vector3(-10, 0, 0); p.shinL = p.shinR = new Vector3(18, 0, 0);
                    p.spine = new Vector3(14, 0, 0);
                    break;
                }
                case Pose.Ready: // goalkeeper crouch, arms wide
                {
                    hop += dt * 7;
                    float b = Mathf.Sin(hop) * 0.5f + 0.5f;
                    p.legL = new Vector3(-30, 0, -14); p.legR = new Vector3(-30, 0, 14);
                    p.shinL = p.shinR = new Vector3(60 + b * 6, 0, 0);
                    p.footL = p.footR = new Vector3(-25, 0, 0);
                    p.armL = new Vector3(-40, 0, -45); p.armR = new Vector3(-40, 0, 45);
                    p.foreL = p.foreR = new Vector3(-40, 0, 0);
                    p.spine = new Vector3(24, 0, 0);
                    p.rigY = -0.17f - b * 0.02f;
                    break;
                }
                case Pose.Slam:
                {
                    // T < 0.5 rising with fists up, then smash down into a deep squat
                    if (T < 0.5f)
                    {
                        p.armL = new Vector3(-175, 0, -20); p.armR = new Vector3(-175, 0, 20);
                        p.foreL = p.foreR = new Vector3(-40, 0, 0);
                        p.legL = new Vector3(-50, 0, -10); p.legR = new Vector3(-50, 0, 10); p.shinL = p.shinR = new Vector3(90, 0, 0);
                        p.spine = new Vector3(-10, 0, 0);
                    }
                    else
                    {
                        p.armL = new Vector3(-60, 0, -15); p.armR = new Vector3(-60, 0, 15);
                        p.foreL = p.foreR = new Vector3(-10, 0, 0);
                        p.legL = new Vector3(-75, 0, -30); p.legR = new Vector3(-75, 0, 30); p.shinL = p.shinR = new Vector3(120, 0, 0);
                        p.footL = p.footR = new Vector3(-40, 0, 0);
                        p.spine = new Vector3(40, 0, 0);
                        p.rigY = -0.42f;
                    }
                    break;
                }
                case Pose.WindUp: // gathering chi: low wide stance, arms drawn back, trembling
                {
                    float shake = Mathf.Sin(breathe * 60) * 2f;
                    p.legL = new Vector3(-40, 0, -25); p.legR = new Vector3(10, 0, 25);
                    p.shinL = new Vector3(70, 0, 0); p.shinR = new Vector3(40, 0, 0);
                    p.armL = new Vector3(50 + shake, 0, -40); p.armR = new Vector3(50 - shake, 0, 40);
                    p.foreL = p.foreR = new Vector3(-90, 0, 0);
                    p.spine = new Vector3(22, 25, 0); p.head = new Vector3(-15, -20, 0);
                    p.rigY = -0.25f;
                    break;
                }
                case Pose.Celebrate:
                {
                    hop += dt * 9;
                    float b = Mathf.Abs(Mathf.Sin(hop));
                    p.armL = new Vector3(-10, 0, -160 + b * 10); p.armR = new Vector3(-10, 0, 160 - b * 10);
                    p.foreL = p.foreR = new Vector3(-10, 0, 0);
                    p.legL = new Vector3(-20 * b, 0, -5); p.legR = new Vector3(10, 0, 5); p.shinL = new Vector3(60 * b, 0, 0);
                    p.spine = new Vector3(-12, 0, 0); p.head = new Vector3(-20, 0, 0);
                    p.rigY = b * 0.3f;
                    break;
                }
            }

            // snappy, keyframe-like blending (fast ease, no rubbery overshoot)
            float k2 = 1 - Mathf.Exp(-dt * 22f);
            Ease(LegL, p.legL, k2); Ease(LegR, p.legR, k2);
            Ease(ShinL, p.shinL, k2); Ease(ShinR, p.shinR, k2);
            Ease(FootL, p.footL, k2); Ease(FootR, p.footR, k2);
            Ease(ArmL, p.armL, k2); Ease(ArmR, p.armR, k2);
            Ease(ForeL, p.foreL, k2); Ease(ForeR, p.foreR, k2);
            Ease(Spine, p.spine + new Vector3(Mathf.Sin(breathe * 2.2f) * 1.2f, 0, 0), k2);
            Ease(Head, p.head - new Vector3(p.spine.x * 0.5f, p.spine.y * 0.6f, 0), k2);
            if (Tail) Tail.localRotation = Quaternion.Euler(20 + Mathf.Sin(phase) * 12 + s * 40, Mathf.Sin(phase * 0.5f) * 15, 0);

            var rp = Rig.localPosition;
            float targetY = HipY + p.rigY + Lift / H;
            rp.y = Mathf.Lerp(rp.y, targetY, 1 - Mathf.Exp(-dt * 26f));
            if (Lift > 0.01f) rp.y = targetY; // jumps follow the physics exactly
            Rig.localPosition = rp;
            var tr = Quaternion.Euler(p.rigPitch + FlipX, SpinY, p.rigRoll);
            Rig.localRotation = Quaternion.Slerp(Rig.localRotation, tr, (FlipX != 0 || SpinY != 0) ? 1 : 1 - Mathf.Exp(-dt * 18f));
        }

        static void Ease(Transform t, Vector3 euler, float k)
        {
            if (!t) return;
            t.localRotation = Quaternion.Slerp(t.localRotation, Quaternion.Euler(euler), k);
        }

        /// <summary>Copies every joint to the mirrored twin (call after Animate and after the root moved).</summary>
        public void SyncMirror()
        {
            if (mirror == null) return;
            var src = Root; var dst = mirrorRoot;
            var pos = src.position;
            dst.position = new Vector3(pos.x, -pos.y, pos.z);
            dst.rotation = src.rotation;
            dst.localScale = new Vector3(src.localScale.x, -src.localScale.y, src.localScale.z);
            var a = all; var b = mirror.all;
            int n = Mathf.Min(a.Count, b.Count);
            for (int i = 1; i < n; i++)
            {
                b[i].localPosition = a[i].localPosition;
                b[i].localRotation = a[i].localRotation;
                b[i].localScale = a[i].localScale;
            }
        }

        /// <summary>World position of the right foot tip (where the ball is struck).</summary>
        public Vector3 FootTip => FootR ? FootR.TransformPoint(new Vector3(0, 0, 0.18f)) : Root.position;
    }
}
