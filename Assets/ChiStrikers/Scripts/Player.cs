using UnityEngine;

namespace ChiStrikers
{
    public enum PState { Free, Kick, Poke, Slide, Down, Flying, Dive, Hold, Special, Celebrate, Stun }

    /// <summary>One footballer: movement, ball control, passes, shots, tackles, Chi. Driven by a human (Controls) or by Brain.</summary>
    public sealed class Player : MonoBehaviour
    {
        public Match M;
        public Team Team;
        public HeroDef Def;
        public int Index;
        public Body Body;

        public Vector3 Vel;
        public float Chi;
        public Controls Human;         // set while a human controls this player
        public PState State;
        public float StateT, StateLen;
        public float Lift, LiftVel;     // jump height
        public float WallTime;          // Akhada Wall / Titan Slam: blocks any shot
        public float TackleCd, ThinkT, PassCd;
        public Vector3 Wish;            // desired velocity (AI or human)
        public bool WishSprint;
        public Vector3 Home;            // formation slot this frame
        public float Charge = -1;       // shot power while the shoot button is held (-1 = not charging)
        public float Dribble = 1f;

        // queued kick, released at the strike frame of the animation
        bool kickQueued; Vector3 kickVel; bool kickShot; float kickAt; HeroDef kickSpecial; float kickPower; float kickGravity = 1;

        Transform ring;
        public Vector3 Pos { get => transform.position; set => transform.position = value; }
        public bool IsKeeper => Def.Role == Role.Keeper;
        public bool HasBall => M.Ball.Owner == this;
        public float Yaw => transform.eulerAngles.y;
        public Vector3 Fwd => transform.forward;
        public bool CanAct => State == PState.Free || State == PState.Hold;
        public bool Grounded => Lift <= 0.001f;
        public float BaseSpeed => 6.2f * Def.Speed;

        public static Player Spawn(Match m, Team t, HeroDef def, int index)
        {
            var go = new GameObject(def.Name);
            var p = go.AddComponent<Player>();
            p.M = m; p.Team = t; p.Def = def; p.Index = index;
            p.Body = Body.Build(go.transform, def.Look);
            p.Chi = 0.3f;
            // controlled-player marker: a coloured ring on the floor
            var r = Util.MeshObj("Marker", Util.Ring(0.55f, 0.72f, 48), go.transform, new Vector3(0, 0.02f, 0), MarkerMat(t.Def.Ui));
            r.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            p.ring = r.transform;
            r.SetActive(false);
            return p;
        }

        static Material MarkerMat(Color c)
        {
            var m = Util.Fade(c);
            m.renderQueue = 3100;
            return m;
        }

        public void ResetAt(Vector3 pos, float yaw)
        {
            Pos = new Vector3(pos.x, 0, pos.z);
            transform.rotation = Quaternion.Euler(0, yaw, 0);
            Vel = Vector3.zero; Lift = 0; LiftVel = 0;
            State = PState.Free; StateT = 0; kickQueued = false; Charge = -1;
            WallTime = 0; TackleCd = 0;
            Body.Pose = Pose.Idle; Body.FlipX = 0; Body.SpinY = 0; Body.Lift = 0;
        }

        public void SetState(PState s, float len)
        {
            State = s; StateT = 0; StateLen = len;
        }

        // -------------------------------------------------------------------------------- per-frame
        public void Tick(float dt)
        {
            StateT += dt;
            TackleCd -= dt; PassCd -= dt;
            if (WallTime > 0) { WallTime -= dt; if (Random.value < dt * 30) M.Fx.Aura(Pos, Def.FxA, Def.FxB, 1.2f); }
            Chi = Mathf.Min(1, Chi + dt * 0.012f);

            // jumping / launched
            if (Lift > 0 || LiftVel > 0)
            {
                LiftVel -= 22f * dt;
                Lift += LiftVel * dt;
                if (Lift <= 0)
                {
                    Lift = 0; LiftVel = 0;
                    if (State == PState.Flying) { SetState(PState.Down, 1.1f); M.Fx.Dust(Pos, 10); Sfx.Play(Sfx.Thud, 0.6f); }
                }
            }

            switch (State)
            {
                case PState.Free:
                case PState.Hold:
                    if (State == PState.Hold && !HasBall) State = PState.Free;
                    Move(dt);
                    break;
                case PState.Kick:
                    Vel = Vector3.MoveTowards(Vel, Vector3.zero, 30 * dt);
                    if (kickQueued && StateT >= kickAt) DoKick();
                    if (StateT >= StateLen) State = PState.Free;
                    break;
                case PState.Poke:
                case PState.Stun:
                    Vel = Vector3.MoveTowards(Vel, Vector3.zero, 25 * dt);
                    if (StateT >= StateLen) State = PState.Free;
                    break;
                case PState.Slide:
                    Vel = Vector3.MoveTowards(Vel, Vector3.zero, 13 * dt);
                    if (Random.value < dt * 40) M.Fx.Dust(Pos + Fwd * 0.6f, 1);
                    SlideHits();
                    if (StateT >= StateLen) SetState(PState.Stun, 0.25f);
                    break;
                case PState.Down:
                    Vel = Vector3.MoveTowards(Vel, Vector3.zero, 20 * dt);
                    if (StateT >= StateLen) State = PState.Free;
                    break;
                case PState.Flying:
                    break; // ballistic until it lands
                case PState.Dive:
                    Vel = Vector3.MoveTowards(Vel, Vector3.zero, 9 * dt);
                    if (StateT >= StateLen) SetState(PState.Stun, 0.35f);
                    break;
                case PState.Celebrate:
                    Vel = Vector3.MoveTowards(Vel, Vector3.zero, 20 * dt);
                    break;
                case PState.Special:
                    break; // Specials drives this player
            }

            if (State != PState.Special) Pos += Vel * dt;
            ClampToPitch();

            if (HasBall && State != PState.Special) CarryBall(dt);
            if (Charge >= 0) Charge = Mathf.Min(1.15f, Charge + dt * 1.4f);

            // facing
            var flat = Util.Flat(Vel);
            if (State == PState.Free || State == PState.Hold)
            {
                Vector3 face = flat.sqrMagnitude > 0.5f ? flat : Vector3.zero;
                if (face == Vector3.zero && !HasBall && State == PState.Free)
                    face = Util.Flat(M.Ball.Pos - Pos) * 0.3f;
                if (face.sqrMagnitude > 0.01f)
                {
                    var target = Quaternion.LookRotation(face.normalized);
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, target, 900 * dt);
                }
            }

            UpdatePose();
            Body.Animate(dt);
            Body.SyncMirror();
            ring.gameObject.SetActive(Human != null && !M.Cinematic && M.State != MatchState.Title);
            if (ring.gameObject.activeSelf) ring.Rotate(0, 90 * dt, 0, Space.World);
        }

        void Move(float dt)
        {
            float top = BaseSpeed * (WishSprint ? 1.38f : 1f) * (HasBall ? 0.9f : 1f);
            if (State == PState.Hold) top = 2.5f;
            var want = Vector3.ClampMagnitude(Wish, 1f) * top;
            float acc = want.sqrMagnitude > Vel.sqrMagnitude ? 38f : 30f;
            Vel = Vector3.MoveTowards(Vel, want, acc * dt);
            Vel.y = 0;
        }

        void ClampToPitch()
        {
            var p = Pos;
            float lim = Pitch.HalfL - 0.4f;
            if (IsKeeper) lim = Pitch.HalfL - 0.2f;
            p.x = Mathf.Clamp(p.x, -lim, lim);
            p.z = Mathf.Clamp(p.z, -Pitch.HalfW + 0.4f, Pitch.HalfW - 0.4f);
            p.y = 0;
            Pos = p;
        }

        void CarryBall(float dt)
        {
            var b = M.Ball;
            if (State == PState.Hold)
            {
                b.Pos = Pos + Fwd * 0.35f + Vector3.up * (1.05f * Def.Look.Height);
                b.Vel = Vel;
                return;
            }
            // dribble: the ball rolls a little ahead, tapped forward with each stride
            float speed = Util.Flat(Vel).magnitude;
            float lead = 0.5f + Mathf.Clamp01(speed / 8f) * (0.25f + 0.18f * Mathf.Sin(Time.time * 10f));
            var target = Pos + Fwd * lead + Vector3.up * (Ball.R + Lift);
            b.Pos = Vector3.Lerp(b.Pos, target, 1 - Mathf.Exp(-dt * 25));
            b.Vel = Vel;
        }

        void UpdatePose()
        {
            Body.Speed = Util.Flat(Vel).magnitude;
            Body.Lift = Lift;
            switch (State)
            {
                case PState.Free:
                    if (!Grounded) Body.Pose = Pose.Air;
                    else if (IsKeeper && Body.Speed < 2.5f && !HasBall) Body.Pose = Pose.Ready;
                    else Body.Pose = Body.Speed > 0.3f ? Pose.Run : Pose.Idle;
                    break;
                case PState.Hold: Body.Pose = Body.Speed > 0.3f ? Pose.Run : Pose.Catch; if (Body.Speed < 0.3f) Body.Pose = Pose.Catch; break;
                case PState.Kick: Body.T = StateT / StateLen; break;
                case PState.Poke: Body.Pose = Pose.Poke; Body.T = StateT / StateLen; break;
                case PState.Slide: Body.Pose = Pose.Slide; break;
                case PState.Down: Body.Pose = Pose.Down; break;
                case PState.Stun: Body.Pose = Pose.Idle; break;
                case PState.Flying: Body.Pose = Pose.Flying; break;
                case PState.Dive: Body.Pose = Pose.Dive; Body.T = StateT / StateLen; break;
                case PState.Celebrate: Body.Pose = Pose.Celebrate; break;
            }
        }

        // -------------------------------------------------------------------------------- actions
        /// <summary>Start a kick animation; the ball leaves the foot at the strike frame.</summary>
        public void Kick(Vector3 vel, bool shot, Pose pose, float len = 0.34f, HeroDef special = null, float power = 0, float gravity = 1)
        {
            if (!HasBall && special == null) return;
            kickQueued = true; kickVel = vel; kickShot = shot; kickSpecial = special; kickPower = power; kickGravity = gravity;
            kickAt = len * 0.4f;
            SetState(PState.Kick, len);
            Body.Pose = pose; Body.T = 0;
            if (Util.Flat(vel).sqrMagnitude > 0.1f) transform.rotation = Quaternion.LookRotation(Util.Flat(vel).normalized);
            Charge = -1;
        }

        void DoKick()
        {
            kickQueued = false;
            var b = M.Ball;
            if (b.Owner != this && kickSpecial == null) return;
            if (b.Owner == this) b.Pos = Pos + Fwd * 0.55f + Vector3.up * Mathf.Max(Ball.R, Lift + Ball.R);
            b.Release(this, kickVel, kickShot);
            b.GravityScale = kickGravity;
            if (kickSpecial != null)
            {
                b.Special = kickSpecial; b.SpecialBy = this; b.SpecialPower = kickPower; b.ShotTeam = Team;
            }
            float sp = kickVel.magnitude;
            Sfx.Play(sp > 18 ? Sfx.Kick : Sfx.Pass, Mathf.Clamp01(sp / 25f) * 0.8f + 0.2f, Random.Range(0.92f, 1.08f));
            if (sp > 20) { M.Fx.Burst(b.Pos, Color.white, Team.Def.Ui, 8, 4f); M.Fx.Shake(0.08f); }
        }

        /// <summary>Best teammate to pass to, preferring the direction the player is pushing.</summary>
        public Player PickPassTarget(Vector3 dir, bool forward)
        {
            Player best = null; float bestScore = -999;
            foreach (var t in Team.Players)
            {
                if (t == this || t.State == PState.Down || t.State == PState.Flying) continue;
                var d = t.Pos - Pos; d.y = 0;
                float dist = d.magnitude;
                if (dist < 2.5f || dist > 30) continue;
                float score = 0;
                if (dir.sqrMagnitude > 0.01f) score += Vector3.Dot(d / dist, dir.normalized) * 6f;
                score += Team.Side * d.x * (forward ? 0.35f : 0.12f);
                score -= dist * 0.08f;
                score += Openness(t.Pos, Pos) * 2.5f;
                if (t.IsKeeper) score -= 6;
                if (score > bestScore) { bestScore = score; best = t; }
            }
            return best;
        }

        /// <summary>0 = a defender is on the line or next to the receiver, 1 = wide open.</summary>
        public float Openness(Vector3 at, Vector3 from)
        {
            float open = 1;
            foreach (var o in Team.Opp.Players)
            {
                float dTo = Util.FlatDist(o.Pos, at);
                open = Mathf.Min(open, Mathf.Clamp01((dTo - 0.8f) / 3.5f));
                // distance from the pass line
                var a = Util.XZ(from); var b = Util.XZ(at); var c = Util.XZ(o.Pos);
                var ab = b - a; float len = ab.magnitude; if (len < 0.1f) continue;
                float tt = Mathf.Clamp01(Vector2.Dot(c - a, ab) / (len * len));
                float lineDist = (a + ab * tt - c).magnitude;
                if (tt > 0.1f && tt < 0.95f) open = Mathf.Min(open, Mathf.Clamp01((lineDist - 0.5f) / 2.2f));
            }
            return open;
        }

        public void PassTo(Player t, bool lob)
        {
            if (t == null) return;
            var b = M.Ball;
            float dist = Util.FlatDist(t.Pos, Pos);
            // lead the receiver a little
            float travel = dist / (lob ? 14f : 16f);
            var aim = t.Pos + t.Vel * travel * (lob ? 0.9f : 0.6f);
            aim.x = Mathf.Clamp(aim.x, -Pitch.HalfL + 1, Pitch.HalfL - 1);
            aim.z = Mathf.Clamp(aim.z, -Pitch.HalfW + 1, Pitch.HalfW - 1);
            Vector3 vel;
            if (lob)
                vel = Ball.Aim(b.Pos, aim + Vector3.up * 0.6f, Mathf.Clamp(dist * 0.75f + 5f, 9f, 18f));
            else
            {
                var d = Util.Flat(aim - Pos);
                float spd = Mathf.Clamp(dist * 0.95f + 6.5f, 9f, 24f);
                vel = d.normalized * spd + Vector3.up * 0.4f;
            }
            Kick(vel, false, Pose.Pass, 0.28f);
            M.Ball.Receiver = t;
            PassCd = 0.4f;
            M.OnPass(this, t);
        }

        /// <summary>Shoot at the opponent goal. aimZ in -1..1 picks the post; power 0..1 (over 1 = risk of skying it).</summary>
        public void Shoot(float power, float aimZ)
        {
            float gx = Team.Side * Pitch.HalfL;
            var keeper = Team.Opp.Keeper;
            if (Mathf.Abs(aimZ) < 0.2f && keeper != null)
                aimZ = keeper.Pos.z > 0 ? -0.8f : 0.8f; // aim away from the keeper
            float z = Mathf.Clamp(aimZ, -1, 1) * (Pitch.GoalHalfW - 0.55f);
            float acc = 1.1f / Def.Shot;
            z += Random.Range(-acc, acc) * 0.6f;
            float over = Mathf.Max(0, power - 1f);
            float y = Mathf.Lerp(0.35f, 1.9f, Mathf.Clamp01(power)) + over * 6f + Random.Range(-0.25f, 0.25f);
            float spd = (15f + 14f * Mathf.Clamp01(power)) * Def.Shot;
            var target = new Vector3(gx + Team.Side * 0.5f, y, z);
            var v = Ball.Aim(M.Ball.Pos, target, spd);
            Kick(v, true, Pose.Kick, 0.36f);
        }

        // ---- tackles
        public void Poke()
        {
            if (TackleCd > 0) return;
            TackleCd = 0.7f;
            SetState(PState.Poke, 0.4f);
            var b = M.Ball;
            var carrier = b.Owner;
            if (carrier != null && carrier.Team != Team && Util.FlatDist(carrier.Pos, Pos) < 1.7f && carrier.State != PState.Hold && carrier.State != PState.Special)
            {
                float chance = 0.5f * Def.Tackle / carrier.Dribble;
                if (Random.value < chance)
                {
                    WinBall(carrier, false);
                }
                else Sfx.Play(Sfx.Pass, 0.3f, 0.7f);
            }
            else if (b.Loose && Util.FlatDist(b.Pos, Pos) < 1.6f && b.Pos.y < 1.2f && !b.InNet)
            {
                b.TakeBy(this);
            }
        }

        public void Slide()
        {
            if (TackleCd > 0) return;
            TackleCd = 1.1f;
            SetState(PState.Slide, 0.6f);
            var f = Util.Flat(Vel).sqrMagnitude > 1 ? Util.Flat(Vel).normalized : Fwd;
            transform.rotation = Quaternion.LookRotation(f);
            Vel = f * 10.5f;
            Sfx.Play(Sfx.Whoosh, 0.35f, 1.6f);
        }

        void SlideHits()
        {
            var b = M.Ball;
            var carrier = b.Owner;
            if (carrier != null && carrier.Team != Team && carrier.State != PState.Hold && carrier.State != PState.Special
                && Util.FlatDist(carrier.Pos, Pos + Fwd * 0.5f) < 1.15f)
            {
                WinBall(carrier, true);
            }
            else if (b.Loose && Util.FlatDist(b.Pos, Pos + Fwd * 0.6f) < 0.9f && b.Pos.y < 0.8f && b.NoPickup <= 0)
            {
                b.Owner = null;
                b.LastTouch = this;
                b.Vel = Fwd * 8f + Vector3.up * 1.5f;
                b.ClearShot();
            }
        }

        void WinBall(Player carrier, bool slide)
        {
            var b = M.Ball;
            if (slide)
            {
                carrier.Knock(Fwd * 2f + Vector3.up * 2.5f, false);
                b.Owner = null; b.LastTouch = this;
                b.Vel = (Fwd * 6f + Vector3.up * 2f);
                b.ClearShot();
                b.Ignore = carrier; b.IgnoreTime = 0.6f;
                M.Fx.Dust(carrier.Pos, 12);
            }
            else
            {
                b.TakeBy(this);
                carrier.SetState(PState.Stun, 0.45f);
            }
            Chi = Mathf.Min(1, Chi + 0.15f);
            Sfx.Play(Sfx.Thud, 0.6f, 1.1f);
            M.Fx.Burst(Pos + Vector3.up * 0.5f, Color.white, Team.Def.Ui, 6, 3);
        }

        /// <summary>Knocked down (fly = launched into the air like a ragdoll by a special).</summary>
        public void Knock(Vector3 impulse, bool fly)
        {
            if (State == PState.Special) return;
            if (HasBall)
            {
                var b = M.Ball;
                b.Owner = null;
                b.Vel = Util.Flat(impulse).normalized * 3f + Vector3.up * 2f;
                b.Ignore = this; b.IgnoreTime = 0.8f;
            }
            Charge = -1;
            kickQueued = false;
            Vel = Util.Flat(impulse);
            if (fly)
            {
                LiftVel = Mathf.Max(impulse.y, 6f);
                Lift = Mathf.Max(Lift, 0.05f);
                SetState(PState.Flying, 3f);
                transform.rotation = Quaternion.LookRotation(-Util.Flat(impulse).normalized + Vector3.forward * 0.001f);
            }
            else SetState(PState.Down, 0.9f);
        }

        public void Jump(float vel) { LiftVel = vel; Lift = Mathf.Max(Lift, 0.01f); }

        public void SetHome(Vector3 h) { Home = h; }
    }
}
