using UnityEngine;

namespace ChiStrikers
{
    /// <summary>Indoor arena dimensions (metres). The pitch runs along X, the camera looks from -Z.</summary>
    public static class Pitch
    {
        public const float HalfL = 21f, HalfW = 12f;
        public const float GoalHalfW = 3.1f, GoalH = 2.5f, GoalDepth = 1.6f;
        public const float BoxDepth = 6f, BoxHalfW = 7f;
        public static bool InBox(Vector3 p, float goalX) => Mathf.Abs(p.x - goalX) < BoxDepth && Mathf.Abs(p.z) < BoxHalfW;
    }

    /// <summary>The ball: arcade physics (bounces off the boards, posts and crossbar), dribbling, shots, curve and specials.</summary>
    public sealed class Ball : MonoBehaviour
    {
        public const float R = 0.24f;
        public const float Gravity = 15f;

        public Match M;
        public Vector3 Vel;
        public Player Owner, LastTouch, Receiver;
        public Team ShotTeam;            // set while the ball is a shot on goal
        public HeroDef Special;          // set while the ball carries a special move
        public Player SpecialBy;
        public float SpecialPower;       // how many players it can blast through
        public float GravityScale = 1f;
        public Vector3 HomingTarget; public float HomingTime, HomingStrength; // curve specials
        public bool KeeperChecked;
        public float NoPickup;           // nobody can control it for this long
        public Player Ignore; public float IgnoreTime;
        public bool InNet;
        public Transform Visual, Aura;
        Transform mirror;

        public Vector3 Pos { get => transform.position; set => transform.position = value; }
        public bool Loose => Owner == null;
        public float Speed => Vel.magnitude;

        public static Ball Create(Match m)
        {
            var go = new GameObject("Ball");
            var b = go.AddComponent<Ball>();
            b.M = m;
            b.Visual = MakeBall(go.transform);
            var mg = new GameObject("BallReflection");
            b.mirror = mg.transform;
            MakeBall(b.mirror);
            b.mirror.localScale = new Vector3(1, -1, 1);
            var aura = new GameObject("Aura");
            aura.transform.SetParent(go.transform, false);
            b.Aura = aura.transform;
            aura.SetActive(false);
            return b;
        }

        /// <summary>White ball with blue and yellow panels (the reel's ball colours).</summary>
        static Transform MakeBall(Transform parent)
        {
            var v = new GameObject("Visual").transform;
            v.SetParent(parent, false);
            Util.Prim(PrimitiveType.Sphere, v, Vector3.zero, Vector3.one * R * 2, Util.Lit(Util.Hex("#f6f6f2"), 2f), default, false);
            var blue = Util.Lit(Util.Hex("#2c6fd1"), 0f);
            var yellow = Util.Lit(Util.Hex("#f4c430"), 0f);
            Vector3[] dirs = { Vector3.up, Vector3.down, Vector3.left, Vector3.right, Vector3.forward, Vector3.back };
            for (int i = 0; i < dirs.Length; i++)
            {
                var d = dirs[i];
                var p = Util.Prim(PrimitiveType.Sphere, v, d * R * 0.93f, new Vector3(R * 0.95f, R * 0.95f, R * 0.95f), i % 2 == 0 ? blue : yellow, default, false);
                p.transform.localRotation = Quaternion.LookRotation(d == Vector3.up || d == Vector3.down ? Vector3.forward : Vector3.up, d);
                p.transform.localScale = new Vector3(R * 0.9f, R * 0.25f, R * 0.9f);
            }
            return v;
        }

        public void ResetTo(Vector3 p)
        {
            Owner = null; Receiver = null; ClearShot();
            Vel = Vector3.zero; Pos = p; InNet = false; NoPickup = 0;
        }

        public void ClearShot()
        {
            ShotTeam = null; KeeperChecked = false;
            ClearSpecial();
        }

        public void ClearSpecial()
        {
            Special = null; SpecialBy = null; SpecialPower = 0; GravityScale = 1; HomingTime = 0;
            if (Aura) Aura.gameObject.SetActive(false);
        }

        public void TakeBy(Player p)
        {
            if (Owner == p) return;
            var prev = Owner; var lastTouch = LastTouch;
            Owner = p; LastTouch = p; Receiver = null;
            ClearShot();
            Vel = Vector3.zero;
            M.OnPossession(p, prev, lastTouch);
        }

        /// <summary>Kick the ball away from its owner.</summary>
        public void Release(Player by, Vector3 vel, bool shot)
        {
            Owner = null; LastTouch = by;
            Vel = vel;
            Ignore = by; IgnoreTime = 0.3f;
            NoPickup = 0.05f;
            ShotTeam = shot ? by.Team : null;
            KeeperChecked = false;
            var p = Pos; if (p.y < R) p.y = R; Pos = p;
        }

        public void Step(float dt)
        {
            if (dt <= 0) return;
            NoPickup -= dt; IgnoreTime -= dt;

            if (Owner == null)
            {
                Integrate(dt);
            }

            // roll the visual
            var flat = Util.Flat(Vel);
            if (flat.sqrMagnitude > 0.01f)
            {
                var axis = Vector3.Cross(Vector3.up, flat.normalized);
                Visual.Rotate(axis, flat.magnitude / R * Mathf.Rad2Deg * dt, Space.World);
            }
            if (Special != null && Special.Special == SpecialType.CurveShot) Visual.Rotate(Vector3.up, 1440 * dt, Space.World);

            if (mirror)
            {
                var p = Pos;
                mirror.position = new Vector3(p.x, -p.y, p.z);
                mirror.GetChild(0).rotation = Visual.rotation;
            }
        }

        void Integrate(float dt)
        {
            var p = Pos;
            // curve specials: steer toward the target, bending around everything
            if (HomingTime > 0)
            {
                HomingTime -= dt;
                var want = (HomingTarget - p).normalized * Vel.magnitude;
                Vel = Vector3.RotateTowards(Vel, want, HomingStrength * dt, 0);
            }
            Vel.y -= Gravity * GravityScale * dt;
            bool onGround = p.y <= R + 0.01f;
            float drag = onGround ? 0.9f : 0.12f;
            Vel.x *= Mathf.Exp(-drag * dt);
            Vel.z *= Mathf.Exp(-drag * dt);
            var np = p + Vel * dt;

            // floor
            if (np.y < R)
            {
                np.y = R;
                if (Vel.y < -2f) { Vel.y = -Vel.y * 0.55f; if (Vel.y > 3) Sfx.Play(Sfx.Pass, 0.25f, 1.4f); }
                else Vel.y = 0;
                if (Special != null && Special.Special != SpecialType.CurveShot && SpecialPower > 0) M.Fx.Crack(np, Vel, Special.FxB);
            }
            if (np.y > 16) { np.y = 16; Vel.y = -Mathf.Abs(Vel.y) * 0.5f; }

            // side boards
            if (Mathf.Abs(np.z) > Pitch.HalfW - R)
            {
                np.z = Mathf.Sign(np.z) * (Pitch.HalfW - R);
                Vel.z = -Vel.z * 0.65f;
                Bonk();
            }

            // goal ends
            float gx = Pitch.HalfL;
            if (!InNet && Mathf.Abs(np.x) > gx - R)
            {
                bool mouth = Mathf.Abs(np.z) < Pitch.GoalHalfW - R * 0.5f && np.y < Pitch.GoalH - R * 0.5f;
                bool postZone = Mathf.Abs(Mathf.Abs(np.z) - Pitch.GoalHalfW) < R + 0.08f && np.y < Pitch.GoalH + R;
                bool barZone = Mathf.Abs(np.z) < Pitch.GoalHalfW + R && Mathf.Abs(np.y - Pitch.GoalH) < R + 0.08f;
                if (mouth && !postZone && !barZone)
                {
                    if (Mathf.Abs(np.x) > gx + R * 0.5f) { InNet = true; M.OnGoal(np.x > 0 ? 1 : -1, this); }
                }
                else if (postZone || barZone)
                {
                    // woodwork
                    np.x = Mathf.Sign(np.x) * (gx - R);
                    Vel.x = -Vel.x * 0.6f;
                    if (barZone) Vel.y = Mathf.Abs(Vel.y) * 0.5f + 1f;
                    Sfx.Play(Sfx.Chime, 0.7f, 1.6f);
                    M.Fx.Shake(0.15f);
                    ClearShot();
                }
                else
                {
                    np.x = Mathf.Sign(np.x) * (gx - R);
                    Vel.x = -Vel.x * 0.65f;
                    Bonk();
                    ClearShot();
                }
            }
            if (InNet)
            {
                // inside the net: soak up the speed
                float back = gx + Pitch.GoalDepth - R;
                if (Mathf.Abs(np.x) > back) { np.x = Mathf.Sign(np.x) * back; Vel.x = -Vel.x * 0.15f; Vel *= 0.4f; M.Fx.NetBulge(np.x > 0 ? 1 : -1, np); }
                if (Mathf.Abs(np.z) > Pitch.GoalHalfW - R) { np.z = Mathf.Sign(np.z) * (Pitch.GoalHalfW - R); Vel.z *= -0.2f; }
                if (np.y > Pitch.GoalH - R) { np.y = Pitch.GoalH - R; Vel.y = -Mathf.Abs(Vel.y) * 0.2f; }
                Vel *= Mathf.Exp(-2.5f * dt);
            }

            Pos = np;
            if (ShotTeam != null && Vel.magnitude < 5f) ClearShot();
            if (Special != null && Vel.magnitude < 8f) ClearSpecial();
        }

        void Bonk()
        {
            if (Vel.magnitude > 4) Sfx.Play(Sfx.Thud, 0.35f, 1.3f);
            if (Special != null) { M.Fx.Burst(Pos, Special.FxA, Special.FxB, 20, 6f); }
            ClearSpecial();
        }

        /// <summary>Where the ball will be in t seconds (ignoring bounces), used by the AI.</summary>
        public Vector3 Predict(float t)
        {
            if (Owner != null) return Owner.transform.position + Owner.Vel * t;
            var v = Vel; var p = Pos;
            float k = Mathf.Exp(-0.9f * t);
            var flat = new Vector3(v.x, 0, v.z) * ((1 - k) / 0.9f);
            var q = p + flat;
            q.x = Mathf.Clamp(q.x, -Pitch.HalfL, Pitch.HalfL);
            q.z = Mathf.Clamp(q.z, -Pitch.HalfW, Pitch.HalfW);
            q.y = Mathf.Max(R, p.y + v.y * t - 0.5f * Gravity * GravityScale * t * t);
            return q;
        }

        /// <summary>Velocity that sends the ball from 'from' to 'to' at roughly 'speed' (horizontal), under this ball's gravity.</summary>
        public static Vector3 Aim(Vector3 from, Vector3 to, float speed, float gravityScale = 1f)
        {
            var d = to - from;
            var flat = new Vector3(d.x, 0, d.z);
            float dist = flat.magnitude;
            if (dist < 0.01f) return Vector3.up * 2;
            // compensate for rolling/air drag a little so passes arrive
            float t = dist / speed;
            float vy = (d.y + 0.5f * Gravity * gravityScale * t * t) / t;
            return flat / dist * speed + Vector3.up * vy;
        }
    }
}
