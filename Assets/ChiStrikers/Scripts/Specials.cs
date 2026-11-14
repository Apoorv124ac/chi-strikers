using System.Collections;
using UnityEngine;

namespace ChiStrikers
{
    /// <summary>
    /// The special moves. Each one is a short cinematic: slow-motion, a camera swing onto the hero, a big kinetic title,
    /// then the move itself with its effects. Five mechanics are shared by both teams, each hero has their own colours.
    /// </summary>
    public sealed class Specials
    {
        readonly Match m;
        public bool Running;
        public Player Current;
        public float TitleT = -1; public string Title, Sub; public Color TitleColor;

        public Specials(Match match) { m = match; }

        public bool CanUse(Player p)
        {
            if (Running || p.Chi < 1 || !p.CanAct || (m.State != MatchState.Playing && m.State != MatchState.Title)) return false;
            switch (p.Def.Special)
            {
                case SpecialType.PowerShot:
                case SpecialType.AerialShot:
                case SpecialType.CurveShot:
                    return p.HasBall && p.State == PState.Free;
                case SpecialType.Shockwave:
                    return true;
                default:
                    return false; // keeper saves trigger themselves
            }
        }

        public void Begin(Player p)
        {
            if (!CanUse(p)) return;
            m.StartCoroutine(Run(p));
        }

        void Announce(Player p)
        {
            Title = p.Def.SpecialName; Sub = p.Def.Name.ToUpper() + "  ·  " + p.Def.Art.ToUpper();
            TitleColor = p.Def.FxB; TitleT = 0;
        }

        static IEnumerator Wait(float realSeconds)
        {
            float t = 0;
            while (t < realSeconds) { t += Time.unscaledDeltaTime; yield return null; }
        }

        void SetSlow(float s) { Time.timeScale = s; Time.fixedDeltaTime = 0.02f * Mathf.Max(0.05f, s); }

        Vector3 GoalPoint(Player p, float y)
        {
            var keeper = p.Team.Opp.Keeper;
            float z = keeper != null && keeper.Pos.z > 0 ? -1.9f : 1.9f;
            return new Vector3(p.Team.Side * (Pitch.HalfL + 0.6f), y, z);
        }

        IEnumerator Run(Player p)
        {
            Running = true; Current = p;
            p.Chi = 0;
            p.SetState(PState.Special, 99);
            p.Vel = Vector3.zero;
            m.Cinematic = true;
            var type = p.Def.Special;

            // ---- gather chi: slow-mo, camera swings in close, eyes snap open
            SetSlow(0.12f);
            m.Cam.Focus(p.transform, type == SpecialType.Shockwave ? 4.5f : 3.2f, 1.45f);
            Sfx.Play(Sfx.Whoosh, 0.9f, 0.8f);
            Sfx.Play(Sfx.Chime, 0.5f, 0.7f);
            Announce(p);
            p.Body.Pose = Pose.WindUp;
            if (type != SpecialType.Shockwave)
            {
                var goal = new Vector3(p.Team.Side * Pitch.HalfL, 0, 0);
                p.transform.rotation = Quaternion.LookRotation(Util.Flat(goal - p.Pos).normalized);
            }
            float t = 0;
            while (t < 0.85f)
            {
                t += Time.unscaledDeltaTime;
                p.Body.Animate(Time.unscaledDeltaTime);
                p.Body.SyncMirror();
                if (m.Ball.Owner == p) m.Ball.Pos = p.Pos + p.Fwd * 0.6f + Vector3.up * Ball.R;
                m.Fx.Aura(p.Pos, p.Def.FxA, p.Def.FxB, 1.6f);
                if (type == SpecialType.PowerShot) m.Fx.FireWall(p.Pos, p.Def.FxA, p.Def.FxB);
                yield return null;
            }
            m.Fx.Flash(0.6f);
            m.Fx.Ring(p.Pos + Vector3.up * 0.05f, 0.5f, 4f, p.Def.FxB, 0.5f);

            switch (type)
            {
                case SpecialType.PowerShot: yield return PowerShot(p); break;
                case SpecialType.AerialShot: yield return AerialShot(p); break;
                case SpecialType.CurveShot: yield return CurveShot(p); break;
                case SpecialType.Shockwave: yield return Shockwave(p); break;
            }

            if (p.State == PState.Special) p.SetState(PState.Free, 0);
            p.Body.FlipX = 0; p.Body.SpinY = 0;
            SetSlow(1f);
            m.Cinematic = false;
            m.Cam.Release();
            Running = false; Current = null;
        }

        // Agni Strike / Rail Shot: spin-jump and blast a comet that ploughs through players and rips the floor
        IEnumerator PowerShot(Player p)
        {
            var b = m.Ball;
            SetSlow(0.35f);
            p.Jump(7.5f);
            p.Body.Pose = Pose.Air;
            float t = 0, spinLen = 0.5f;
            while (t < spinLen)
            {
                t += Time.deltaTime;
                p.Body.SpinY = 720 * Mathf.SmoothStep(0, 1, t / spinLen);
                if (b.Owner == p) b.Pos = p.Pos + p.Fwd * 0.6f + Vector3.up * (Ball.R + p.Lift * 0.7f);
                if (Random.value < 0.5f) m.Fx.Ghost(p.Body, p.Def.FxB, 0.25f);
                yield return null;
            }
            p.Body.SpinY = 0;
            var aim = GoalPoint(p, 1.0f);
            var v = Ball.Aim(b.Pos, aim, 36f, 0.1f);
            p.Kick(v, true, Pose.Volley, 0.3f, p.Def, 3.2f, 0.1f);
            b.Aura.gameObject.SetActive(true);
            m.Fx.SetAura(b, p.Def, 1.5f);
            // strike frame
            while (p.State == PState.Kick && b.Owner == p) yield return null;
            SetSlow(1f);
            Sfx.Play(Sfx.Boom, 1f);
            m.Fx.Shake(0.5f);
            m.Fx.Ring(p.Pos + Vector3.up * 0.05f, 0.4f, 7f, p.Def.FxB, 0.6f);
            m.Fx.Burst(b.Pos, p.Def.FxA, p.Def.FxB, 50, 10f);
            m.Cam.FollowBall(0.9f);
            m.OnSpecialLaunched(p);
            yield return Wait(0.6f);
        }

        // Sky Step / Prism Volley: pop the ball up, run up the air, hang at the top, volley it down
        IEnumerator AerialShot(Player p)
        {
            var b = m.Ball;
            SetSlow(0.5f);
            // pop it up
            b.Owner = null; b.Vel = Vector3.up * 13f + p.Fwd * 1.5f; b.NoPickup = 5f; b.LastTouch = p;
            Sfx.Play(Sfx.Pass, 0.8f, 1.2f);
            p.Body.Pose = Pose.Air;
            // three steps up the air
            for (int i = 0; i < 3; i++)
            {
                p.LiftVel = 13f; p.Lift = Mathf.Max(p.Lift, 0.05f);
                m.Fx.Ring(p.Pos + Vector3.up * (p.Lift + 0.05f), 0.2f, 1.6f, p.Def.FxB, 0.35f);
                m.Fx.Ghost(p.Body, p.Def.FxB, 0.4f);
                Sfx.Play(Sfx.Chime, 0.4f, 1.2f + i * 0.2f);
                float tt = 0;
                while (tt < 0.22f) { tt += Time.deltaTime; p.Pos += p.Fwd * 3f * Time.deltaTime; yield return null; }
            }
            // apex: hang time in slow motion, flip, the ball meets the foot
            SetSlow(0.1f);
            p.LiftVel = 0;
            var apex = p.Lift;
            float t = 0;
            var startBall = b.Pos;
            while (t < 0.75f)
            {
                t += Time.unscaledDeltaTime;
                p.Lift = apex; p.LiftVel = 0;
                p.Body.FlipX = -360 * Mathf.SmoothStep(0, 1, t / 0.75f);
                b.Vel = Vector3.zero;
                b.Pos = Vector3.Lerp(startBall, p.Pos + Vector3.up * (apex + 0.9f) + p.Fwd * 0.5f, Mathf.SmoothStep(0, 1, t / 0.5f));
                m.Fx.Aura(b.Pos, p.Def.FxA, p.Def.FxB, 0.8f);
                yield return null;
            }
            p.Body.FlipX = 0;
            SetSlow(0.6f);
            b.Owner = null;
            var aim = GoalPoint(p, 0.5f);
            var v = Ball.Aim(b.Pos, aim, 30f, 0.5f);
            b.Release(p, v, true);
            b.GravityScale = 0.5f; b.Special = p.Def; b.SpecialBy = p; b.SpecialPower = 2.5f; b.NoPickup = 0;
            b.Aura.gameObject.SetActive(true);
            m.Fx.SetAura(b, p.Def, 1.2f);
            p.Body.Pose = Pose.Volley; p.Body.T = 0.5f;
            Sfx.Play(Sfx.Kick, 1f, 0.8f);
            Sfx.Play(Sfx.Boom, 0.6f, 1.3f);
            m.Fx.Flash(0.4f);
            m.Fx.Shake(0.35f);
            m.Fx.Burst(b.Pos, p.Def.FxA, p.Def.FxB, 40, 8f);
            m.Cam.FollowBall(0.8f);
            m.OnSpecialLaunched(p);
            // fall back down
            p.SetState(PState.Free, 0);
            p.LiftVel = 0;
            yield return Wait(0.25f);
            SetSlow(1f);
            yield return Wait(0.4f);
        }

        // Chakra Strike / Vortex Curve: spin with the ball, then a shot that bends around everyone
        IEnumerator CurveShot(Player p)
        {
            var b = m.Ball;
            SetSlow(0.3f);
            float t = 0;
            b.Aura.gameObject.SetActive(true);
            m.Fx.SetAura(b, p.Def, 0.9f);
            while (t < 0.45f)
            {
                t += Time.deltaTime;
                p.Body.SpinY = 360 * Mathf.SmoothStep(0, 1, t / 0.45f);
                p.Body.Pose = Pose.Run; p.Body.Speed = 9;
                b.Pos = p.Pos + Quaternion.Euler(0, p.Body.SpinY, 0) * p.Fwd * 0.9f + Vector3.up * 0.4f;
                if (Random.value < 0.4f) m.Fx.Ghost(p.Body, p.Def.FxB, 0.25f);
                yield return null;
            }
            p.Body.SpinY = 0;
            var goalPt = GoalPoint(p, 0.8f);
            var toGoal = Util.Flat(goalPt - p.Pos).normalized;
            float side = p.Pos.z > 0 ? 1 : -1;
            var launch = Quaternion.Euler(0, 55 * side * p.Team.Side, 0) * toGoal; // go wide first
            var v = launch * 24f + Vector3.up * 3f;
            p.Kick(v, true, Pose.Kick, 0.3f, p.Def, 2.4f, 0.15f);
            while (p.State == PState.Kick && b.Owner == p) yield return null;
            b.HomingTarget = goalPt; b.HomingTime = 2.5f; b.HomingStrength = 2.6f;
            SetSlow(1f);
            Sfx.Play(Sfx.Whoosh, 1f, 1.3f);
            m.Fx.Ring(b.Pos, 0.2f, 3f, p.Def.FxB, 0.4f);
            m.Cam.FollowBall(1.0f);
            m.OnSpecialLaunched(p);
            yield return Wait(0.5f);
        }

        // Akhada Wall / Titan Slam: leap, slam the floor, shockwave flattens opponents, then a wall that stops any shot
        IEnumerator Shockwave(Player p)
        {
            SetSlow(0.45f);
            p.Body.Pose = Pose.Slam; p.Body.T = 0;
            p.Jump(9f);
            while (p.LiftVel > 0) { p.Body.T = 0.2f; yield return null; }
            while (p.Lift > 0) { p.Body.T = 0.2f; yield return null; }
            p.Body.T = 0.8f;
            SetSlow(0.25f);
            Sfx.Play(Sfx.Boom, 1f, 0.8f);
            m.Fx.Shake(0.7f);
            m.Fx.Flash(0.35f);
            m.Fx.Ring(p.Pos + Vector3.up * 0.06f, 0.5f, 8f, p.Def.FxB, 0.7f);
            m.Fx.Ring(p.Pos + Vector3.up * 0.08f, 0.3f, 5f, p.Def.FxA, 0.45f);
            m.Fx.RadialCracks(p.Pos, 10, 3.5f);
            m.Fx.Debris(p.Pos, 40, p.Def.FxB);
            foreach (var o in p.Team.Opp.Players)
            {
                if (o.IsKeeper) continue;
                var d = Util.Flat(o.Pos - p.Pos);
                float dist = d.magnitude;
                if (dist < 7.5f)
                {
                    var dir = dist > 0.01f ? d / dist : Vector3.forward;
                    o.Knock(dir * (9f - dist) + Vector3.up * (10f - dist), true);
                }
            }
            var b = m.Ball;
            if ((b.Loose || b.Owner.Team != p.Team) && Util.FlatDist(b.Pos, p.Pos) < 7.5f && !b.InNet)
            {
                if (b.Owner != null) b.Owner = null;
                b.ClearShot();
                b.TakeBy(p);
            }
            p.WallTime = 3.5f;
            m.OnSpecialLaunched(p);
            yield return Wait(0.7f);
            p.SetState(PState.Free, 0);
        }

        /// <summary>Thousand Hands / Iron Gate: the keeper stops any shot. Called by the save logic.</summary>
        public IEnumerator KeeperSave(Player k)
        {
            Running = true; Current = k;
            k.Chi = 0;
            m.Cinematic = true;
            var b = m.Ball;
            var shotVel = b.Vel;
            SetSlow(0.06f);
            Announce(k);
            m.Cam.Focus(k.transform, 4.2f, 1.3f);
            Sfx.Play(Sfx.Chime, 1f, 0.6f);
            Sfx.Play(Sfx.Whoosh, 0.8f, 0.6f);
            k.SetState(PState.Special, 99);
            k.Body.Pose = Pose.Catch;
            m.Fx.Arms(k, k.Def.FxA, k.Def.FxB);
            // the ball creeps in, held by the hands
            var hold = k.Pos + k.Fwd * 0.6f + Vector3.up * 1.1f;
            var from = b.Pos;
            float t = 0;
            b.Vel = Vector3.zero; b.ClearShot(); b.NoPickup = 9;
            while (t < 1.1f)
            {
                t += Time.unscaledDeltaTime;
                b.Pos = Vector3.Lerp(from, hold, Mathf.SmoothStep(0, 1, t / 0.9f));
                b.Vel = Vector3.zero;
                k.Body.Animate(Time.unscaledDeltaTime); k.Body.SyncMirror();
                yield return null;
            }
            m.Fx.Flash(0.4f);
            m.Fx.Ring(k.Pos + Vector3.up * 0.05f, 0.4f, 5f, k.Def.FxB, 0.5f);
            Sfx.Play(Sfx.Boom, 0.6f, 1.4f);
            b.NoPickup = 0;
            k.SetState(PState.Free, 0);
            b.TakeBy(k);
            SetSlow(1f);
            m.Cinematic = false;
            m.Cam.Release();
            Running = false; Current = null;
        }
    }
}
