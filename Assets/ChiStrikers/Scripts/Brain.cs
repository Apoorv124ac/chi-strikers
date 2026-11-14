using UnityEngine;

namespace ChiStrikers
{
    /// <summary>CPU football: formation, pressing, passing, shooting, specials and goalkeeping.</summary>
    public static class Brain
    {
        // formation slots for a team attacking +X (x, z), index = Role
        static readonly Vector2[] Slots =
        {
            new Vector2(-19.5f, 0),   // keeper
            new Vector2(-11f, 0.5f),  // defender
            new Vector2(-4f, -4f),    // playmaker
            new Vector2(1f, 6.5f),    // winger
            new Vector2(6f, -1.5f),   // striker
        };

        public static Vector3 KickoffSpot(Team t, int i, bool kicking)
        {
            var s = Slots[i];
            float x = s.x;
            if (i > 0) x = Mathf.Min(x * 0.9f - 3f, -3.6f);
            if (kicking && i == (int)Role.Striker) return new Vector3(-0.5f * t.Side, 0, 0);
            if (kicking && i == (int)Role.Playmaker) return new Vector3(-3f * t.Side, 0, -2.5f);
            return new Vector3(x * t.Side, 0, s.y);
        }

        /// <summary>Where this player should stand right now, based on where the ball is.</summary>
        public static Vector3 FormationSpot(Player p)
        {
            var t = p.Team; var m = p.M; var ball = m.Ball.Pos;
            var s = Slots[(int)p.Def.Role];
            bool attacking = m.Ball.Owner != null && m.Ball.Owner.Team == t;
            float bx = ball.x * t.Side; // ball x in this team's frame
            float x = s.x + bx * 0.5f + (attacking ? 4.5f : -2.5f);
            if (p.Def.Role == Role.Defender) x = Mathf.Min(x, bx - 3f);
            if (p.Def.Role == Role.Striker && attacking) x = Mathf.Max(x, bx + 3f);
            x = Mathf.Clamp(x, -Pitch.HalfL + 3f, Pitch.HalfL - 3.5f);
            float z = s.y * (attacking ? 1.15f : 0.8f) + ball.z * 0.35f;
            z = Mathf.Clamp(z, -Pitch.HalfW + 1.5f, Pitch.HalfW - 1.5f);
            return new Vector3(x * t.Side, 0, z);
        }

        public static void Think(Player p, float dt)
        {
            var m = p.M; var b = m.Ball; var t = p.Team;
            p.WishSprint = false;
            if (!p.CanAct) { p.Wish = Vector3.zero; return; }
            if (p.IsKeeper) { Keeper(p, dt); return; }

            p.ThinkT -= dt;
            if (b.Owner == p) { Carrier(p, dt); return; }

            // incoming pass: go and meet it
            if (b.Receiver == p && b.Loose)
            {
                Chase(p, b.Predict(0.25f), true);
                return;
            }

            bool weHave = b.Owner != null && b.Owner.Team == t;
            bool theyHave = b.Owner != null && b.Owner.Team != t;

            if (theyHave)
            {
                var carrier = b.Owner;
                var presser = t.ClosestTo(carrier.Pos, true, true);
                if (presser == p)
                {
                    // goal-side pressure, then tackle
                    var goal = new Vector3(-t.Side * Pitch.HalfL, 0, 0);
                    var spot = carrier.Pos + (goal - carrier.Pos).normalized * 0.9f + carrier.Vel * 0.15f;
                    float d = Util.FlatDist(carrier.Pos, p.Pos);
                    Chase(p, d < 2.5f ? carrier.Pos : spot, d > 5f);
                    if (p.ThinkT <= 0)
                    {
                        p.ThinkT = 0.18f;
                        if (d < 1.55f && Random.value < 0.35f) p.Poke();
                        else if (d < 3.2f && d > 1.4f && Random.value < 0.08f && Vector3.Dot(p.Fwd, (carrier.Pos - p.Pos).normalized) > 0.7f) p.Slide();
                        else if (p.Def.Special == SpecialType.Shockwave && p.Chi >= 1 && d < 4.5f && Random.value < 0.5f) m.Specials.Begin(p);
                    }
                    return;
                }
                // second man: cover the most dangerous runner
                var mark = Mark(p);
                if (mark != null)
                {
                    var goal = new Vector3(-t.Side * Pitch.HalfL, 0, 0);
                    Chase(p, mark.Pos + (goal - mark.Pos).normalized * 1.6f, false);
                    return;
                }
                Chase(p, FormationSpot(p), false);
                return;
            }

            if (!weHave)
            {
                // loose ball: the nearest player chases it
                var chaser = t.ClosestTo(b.Pos, true, true);
                if (chaser == p || Util.FlatDist(p.Pos, b.Pos) < 3f)
                {
                    float d = Util.FlatDist(p.Pos, b.Pos);
                    Chase(p, b.Predict(Mathf.Clamp(d / 9f, 0.05f, 0.6f)), d > 3f);
                    return;
                }
            }

            // support play
            var spotA = FormationSpot(p);
            if (weHave && p.Def.Role == Role.Striker)
            {
                // make a run into space behind the defence
                spotA.x += t.Side * 2f;
            }
            Chase(p, spotA, false);
        }

        static Player Mark(Player p)
        {
            // the defender and playmaker mark the opponents' striker and winger
            var t = p.Team;
            if (p.Def.Role == Role.Defender) return t.Opp.ByRole(Role.Striker);
            if (p.Def.Role == Role.Playmaker) return t.Opp.ByRole(Role.Playmaker);
            if (p.Def.Role == Role.Winger && Mathf.Abs(p.M.Ball.Pos.x + t.Side * Pitch.HalfL) < 14) return t.Opp.ByRole(Role.Winger);
            return null;
        }

        static void Chase(Player p, Vector3 target, bool sprint)
        {
            var d = Util.Flat(target - p.Pos);
            float dist = d.magnitude;
            if (dist < 0.35f) { p.Wish = Vector3.zero; return; }
            float k = Mathf.Clamp01(dist / 1.5f);
            p.Wish = d / dist * k;
            p.WishSprint = sprint && dist > 2.5f;
            // keep a little space from teammates
            foreach (var o in p.Team.Players)
            {
                if (o == p) continue;
                var away = Util.Flat(p.Pos - o.Pos);
                float ad = away.magnitude;
                if (ad < 2.2f && ad > 0.01f) p.Wish += away / ad * (2.2f - ad) * 0.35f;
            }
        }

        static void Carrier(Player p, float dt)
        {
            var t = p.Team; var m = p.M;
            var goal = new Vector3(t.Side * Pitch.HalfL, 0, 0);
            float goalDist = Util.FlatDist(goal, p.Pos);
            var toGoal = Util.Flat(goal - p.Pos).normalized;

            // pressure in front
            float pressure = 99; Player nearest = null;
            foreach (var o in t.Opp.Players)
            {
                if (o.State == PState.Down || o.State == PState.Flying) continue;
                float d = Util.FlatDist(o.Pos, p.Pos);
                if (d < pressure) { pressure = d; nearest = o; }
            }

            if (p.ThinkT <= 0)
            {
                p.ThinkT = Random.Range(0.15f, 0.3f);

                // special move?
                if (p.Chi >= 1 && m.Specials.CanUse(p))
                {
                    float range = p.Def.Special == SpecialType.AerialShot ? 21 : p.Def.Special == SpecialType.CurveShot ? 24 : 26;
                    if (goalDist < range && Random.value < 0.6f) { m.Specials.Begin(p); return; }
                }

                // shoot?
                float lane = p.Openness(goal, p.Pos);
                if (goalDist < 7.5f || (goalDist < 15f && lane > 0.3f && Random.value < 0.55f) || (goalDist < 19f && lane > 0.75f && Random.value < 0.18f))
                {
                    p.Shoot(Random.Range(0.6f, 1f), Random.Range(-1f, 1f) * (Random.value < 0.6f ? 1 : 0));
                    return;
                }

                // pass when pressed, or now and then to keep the ball moving
                if (p.PassCd <= 0 && (pressure < 2.3f || Random.value < 0.05f))
                {
                    var target = p.PickPassTarget(toGoal, true);
                    if (target != null && p.Openness(target.Pos, p.Pos) > 0.3f)
                    {
                        bool lob = p.Openness(target.Pos, p.Pos) < 0.5f && Util.FlatDist(target.Pos, p.Pos) > 9f;
                        p.PassTo(target, lob);
                        return;
                    }
                }
            }

            // dribble toward goal, swerving around defenders
            var dir = toGoal;
            foreach (var o in t.Opp.Players)
            {
                var away = Util.Flat(p.Pos - o.Pos);
                float d = away.magnitude;
                if (d < 4f && d > 0.01f)
                {
                    var side = Vector3.Cross(Vector3.up, toGoal);
                    float s = Vector3.Dot(away, side) >= 0 ? 1 : -1;
                    dir += side * s * (4f - d) * 0.35f + away / d * (4f - d) * 0.1f;
                }
            }
            // stay off the boards
            if (Mathf.Abs(p.Pos.z) > Pitch.HalfW - 2.5f) dir += new Vector3(0, 0, -Mathf.Sign(p.Pos.z) * 0.8f);
            p.Wish = dir.normalized;
            p.WishSprint = pressure > 3.5f;
        }

        // ------------------------------------------------------------------ goalkeeper
        static void Keeper(Player p, float dt)
        {
            var t = p.Team; var m = p.M; var b = m.Ball;
            float goalX = -t.Side * Pitch.HalfL;

            if (b.Owner == p)
            {
                // hold for a moment, then distribute
                p.Wish = Vector3.zero;
                if (p.StateT > 1.0f && p.ThinkT <= 0)
                {
                    p.ThinkT = 0.5f;
                    var target = p.PickPassTarget(new Vector3(t.Side, 0, 0), false);
                    if (target != null)
                    {
                        p.State = PState.Free;
                        p.PassTo(target, Util.FlatDist(target.Pos, p.Pos) > 12f);
                    }
                }
                p.ThinkT -= dt;
                return;
            }

            // shot coming?
            if (b.Loose && b.ShotTeam != null && b.ShotTeam != t && b.Vel.x * -t.Side > 2f)
            {
                float kx = p.Pos.x;
                float tt = (kx - b.Pos.x) / b.Vel.x;
                if (tt > 0 && tt < 1.5f)
                {
                    var hit = b.Pos + b.Vel * tt; hit.y = b.Pos.y + b.Vel.y * tt - 0.5f * Ball.Gravity * b.GravityScale * tt * tt;
                    float dz = hit.z - p.Pos.z;
                    if (Mathf.Abs(hit.z) < Pitch.GoalHalfW + 0.8f)
                    {
                        p.Wish = new Vector3(0, 0, Mathf.Clamp(dz * 3f, -1, 1));
                        p.WishSprint = true;
                        if (Mathf.Abs(dz) > 0.9f && tt < 0.4f && p.State == PState.Free)
                        {
                            // dive toward the shot
                            p.SetState(PState.Dive, 0.8f);
                            float side = Mathf.Sign(dz);
                            // which way is "right" for the body depends on the facing
                            p.Body.Side = Vector3.Dot(p.transform.right, new Vector3(0, 0, side)) >= 0 ? 1 : -1;
                            p.Vel = new Vector3(0, 0, side * Mathf.Min(Mathf.Abs(dz) / Mathf.Max(tt, 0.15f), 9f));
                            p.Jump(Mathf.Clamp(hit.y * 3.2f, 1.5f, 6f));
                        }
                        return;
                    }
                }
            }

            // rush out for a loose ball in the box
            if (b.Loose && !b.InNet && Pitch.InBox(b.Pos, goalX) && t.ClosestTo(b.Pos, false, false) == p && b.Pos.y < 2.2f)
            {
                var d = Util.Flat(b.Predict(0.15f) - p.Pos);
                p.Wish = d.normalized; p.WishSprint = true;
                return;
            }

            // guard the line, shading toward the ball
            float out_ = Mathf.Clamp(1.3f + (Pitch.HalfL - Mathf.Abs(b.Pos.x - goalX)) * 0, 1.0f, 2.5f);
            float bxRel = Mathf.Abs(b.Pos.x - goalX);
            out_ = bxRel < 12 ? 2.2f : 1.2f;
            var spot = new Vector3(goalX + t.Side * out_, 0, Mathf.Clamp(b.Pos.z * 0.4f, -Pitch.GoalHalfW + 0.6f, Pitch.GoalHalfW - 0.6f));
            var dd = Util.Flat(spot - p.Pos);
            p.Wish = dd.magnitude > 0.2f ? Vector3.ClampMagnitude(dd * 1.2f, 1f) : Vector3.zero;
            // face the play
            if (p.Wish.sqrMagnitude < 0.04f)
                p.transform.rotation = Quaternion.RotateTowards(p.transform.rotation, Quaternion.LookRotation(Util.Flat(b.Pos - p.Pos).normalized + new Vector3(t.Side, 0, 0) * 0.01f), 400 * dt);
        }
    }
}
