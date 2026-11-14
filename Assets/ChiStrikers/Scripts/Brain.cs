using UnityEngine;

namespace ChiStrikers
{
    /// <summary>
    /// CPU football. Attack: runs into open space, support angles, through balls, one-twos, weighted shot / pass / dribble
    /// choices. Defence: goal-side pressing, a second man cutting the passing lane, man-marking, interceptions, a last
    /// defender who holds the line, and a keeper who narrows the angle. Sharpness comes from the team's Skill (difficulty).
    /// </summary>
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

        static Vector3 OppGoal(Team t) => new Vector3(t.Side * Pitch.HalfL, 0, 0);
        static Vector3 OwnGoal(Team t) => new Vector3(-t.Side * Pitch.HalfL, 0, 0);

        /// <summary>Base formation spot, sliding with the ball.</summary>
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
            var m = p.M; var b = m.Ball; var t = p.Team; var sk = t.Skill;
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

            // interceptions: a loose ball (their pass) we can reach first
            if (b.Loose && !b.InNet && b.Pos.y < 1.6f)
            {
                var spot = InterceptPoint(p, b, out float tReach);
                if (spot.HasValue)
                {
                    bool ours = b.Receiver != null && b.Receiver.Team == t;
                    var chaser = t.ClosestTo(b.Pos, true, true);
                    bool theirPass = b.Receiver != null && b.Receiver.Team != t;
                    if (chaser == p || (theirPass && sk.Intercept > Mathf.Repeat(p.Index * 0.37f + b.Receiver.Index * 0.21f, 1f) && tReach < ReceiverTime(b)) || Util.FlatDist(p.Pos, b.Pos) < 2.5f)
                    {
                        if (!ours || b.Receiver == p) { Chase(p, spot.Value, true); return; }
                    }
                }
            }

            // a loose ball nobody of ours is receiving: the nearest player goes for it
            if (b.Loose && !b.InNet && t.ClosestTo(b.Pos, true, true) == p && (b.Receiver == null || b.Receiver.Team != t))
            {
                float dd = Util.FlatDist(p.Pos, b.Pos);
                Chase(p, b.Predict(Mathf.Clamp(dd / 9f, 0.05f, 0.6f)), dd > 3f);
                return;
            }

            if (theyHave) { Defend(p, b.Owner, dt); return; }

            if (weHave) { Support(p, b.Owner, dt); return; }

            // loose and not ours to chase: hold shape
            Chase(p, FormationSpot(p), false);
        }

        /// <summary>Earliest point on the ball's path this player can get to in time.</summary>
        static Vector3? InterceptPoint(Player p, Ball b, out float tReach)
        {
            float speed = p.BaseSpeed * 1.3f;
            for (float tt = 0.05f; tt < 1.6f; tt += 0.1f)
            {
                var q = b.Predict(tt);
                float need = Util.FlatDist(p.Pos, q) / speed + 0.12f;
                if (need <= tt) { tReach = tt; return q; }
            }
            tReach = 99;
            return null;
        }

        static float ReceiverTime(Ball b)
        {
            if (b.Receiver == null) return 99;
            float speed = b.Receiver.BaseSpeed * 1.3f;
            for (float tt = 0.05f; tt < 1.6f; tt += 0.1f)
                if (Util.FlatDist(b.Receiver.Pos, b.Predict(tt)) / speed <= tt) return tt;
            return 99;
        }

        // ------------------------------------------------------------------ defending
        static void Defend(Player p, Player carrier, float dt)
        {
            var t = p.Team; var m = p.M; var sk = t.Skill;
            var goal = OwnGoal(t);
            var presser = t.ClosestTo(carrier.Pos, true, true);
            float d = Util.FlatDist(carrier.Pos, p.Pos);

            if (presser == p)
            {
                // goal-side pressure: get between the ball and the goal, then tackle
                var spot = carrier.Pos + (goal - carrier.Pos).normalized * 0.9f + carrier.Vel * 0.18f;
                Chase(p, d < 2.2f ? carrier.Pos + carrier.Vel * 0.1f : spot, d > 3f || sk.Press > 1.1f);
                if (p.ThinkT <= 0)
                {
                    p.ThinkT = 0.16f * sk.Think;
                    float tackleRate = 0.28f * sk.Press;
                    bool goalSide = Vector3.Dot(Util.Flat(goal - carrier.Pos), Util.Flat(p.Pos - carrier.Pos)) > 0;
                    if (d < 1.55f && Random.value < tackleRate * (goalSide ? 1.4f : 0.8f)) p.Poke();
                    else if (d < 3.2f && d > 1.3f && Random.value < 0.07f * sk.Press && Vector3.Dot(p.Fwd, (carrier.Pos - p.Pos).normalized) > 0.75f) p.Slide();
                    else if (p.Def.Special == SpecialType.Shockwave && p.Chi >= 1 && d < 5f && Random.value < 0.5f) m.Specials.Begin(p);
                }
                return;
            }

            // a defender with a full Akhada Wall uses it when the carrier gets close to goal
            if (p.Def.Special == SpecialType.Shockwave && p.Chi >= 1 && d < 6f && Util.FlatDist(carrier.Pos, goal) < 14f && p.ThinkT <= 0)
            {
                p.ThinkT = 0.3f;
                if (Random.value < 0.6f * sk.Press) { m.Specials.Begin(p); return; }
            }

            // second closest: cut the most dangerous passing lane
            var second = SecondClosest(t, carrier.Pos);
            if (second == p)
            {
                var danger = MostDangerous(t.Opp, carrier);
                if (danger != null)
                {
                    var lane = Vector3.Lerp(carrier.Pos, danger.Pos, 0.55f);
                    Chase(p, lane, Util.FlatDist(p.Pos, lane) > 4f);
                    return;
                }
            }

            // the defender is the last line: stay goal-side of the deepest attacker
            if (p.Def.Role == Role.Defender)
            {
                var deepest = Deepest(t.Opp);
                if (deepest != null)
                {
                    var spot = deepest.Pos + (goal - deepest.Pos).normalized * 2f;
                    spot.z = Mathf.Lerp(spot.z, carrier.Pos.z, 0.3f);
                    Chase(p, spot, Util.FlatDist(p.Pos, spot) > 3f);
                    return;
                }
            }

            // everyone else: mark a man, goal-side
            var mark = Mark(p);
            if (mark != null && mark != carrier)
            {
                var spot = mark.Pos + (goal - mark.Pos).normalized * 1.5f + mark.Vel * 0.25f;
                Chase(p, spot, Util.FlatDist(p.Pos, spot) > 4f);
                return;
            }
            Chase(p, FormationSpot(p), false);
        }

        static Player SecondClosest(Team t, Vector3 pos)
        {
            Player a = null, bb = null; float da = 999, db = 999;
            foreach (var p in t.Players)
            {
                if (p.IsKeeper || p.Human != null || p.State == PState.Down || p.State == PState.Flying) continue;
                float d = Util.FlatDist(p.Pos, pos);
                if (d < da) { bb = a; db = da; a = p; da = d; }
                else if (d < db) { bb = p; db = d; }
            }
            return bb;
        }

        /// <summary>The opponent (not the carrier) closest to our goal and most open.</summary>
        static Player MostDangerous(Team opp, Player carrier)
        {
            Player best = null; float bs = -999;
            var goal = new Vector3(opp.Side * Pitch.HalfL, 0, 0);
            foreach (var o in opp.Players)
            {
                if (o == carrier || o.IsKeeper) continue;
                float s = -Util.FlatDist(o.Pos, goal) + carrier.Openness(o.Pos, carrier.Pos) * 6f;
                if (s > bs) { bs = s; best = o; }
            }
            return best;
        }

        static Player Deepest(Team opp)
        {
            Player best = null; float bx = -999;
            foreach (var o in opp.Players)
            {
                if (o.IsKeeper) continue;
                float x = o.Pos.x * opp.Side;
                if (x > bx) { bx = x; best = o; }
            }
            return best;
        }

        static Player Mark(Player p)
        {
            var t = p.Team;
            switch (p.Def.Role)
            {
                case Role.Defender: return t.Opp.ByRole(Role.Striker);
                case Role.Playmaker: return t.Opp.ByRole(Role.Playmaker);
                case Role.Winger: return t.Opp.ByRole(Role.Winger);
                case Role.Striker:
                    // strikers stay high to counter, loosely tracking their defender
                    return null;
            }
            return null;
        }

        // ------------------------------------------------------------------ attacking off the ball
        static void Support(Player p, Player carrier, float dt)
        {
            var t = p.Team;
            // one-two: the runner is coming, stay put and get ready
            p.SupportT -= dt;
            if (p.SupportT <= 0)
            {
                p.SupportT = Random.Range(0.35f, 0.6f) * t.Skill.Think;
                p.SupportSpot = BestSupportSpot(p, carrier);
            }
            bool run = p.Def.Role == Role.Striker || p.Def.Role == Role.Winger;
            Chase(p, p.SupportSpot, run && Util.FlatDist(p.Pos, p.SupportSpot) > 3f);
        }

        /// <summary>Scores spots around the formation slot: open, a clear lane from the ball, forward, spaced out.</summary>
        static Vector3 BestSupportSpot(Player p, Player carrier)
        {
            var t = p.Team;
            var basis = FormationSpot(p);
            var goal = OppGoal(t);
            Vector3 best = basis; float bs = -999;
            for (int i = 0; i < 12; i++)
            {
                Vector3 c;
                if (i == 0) c = basis;
                else
                {
                    float ang = i * 30f;
                    float r = (i % 2 == 0) ? 3.5f : 6f;
                    c = basis + Quaternion.Euler(0, ang, 0) * Vector3.forward * r;
                }
                // runners push in behind the last defender
                if (p.Def.Role == Role.Striker && i == 1)
                {
                    // run in behind: just past the opponents' last outfield player
                    float line = -99;
                    foreach (var o in t.Opp.Players) if (!o.IsKeeper) line = Mathf.Max(line, o.Pos.x * t.Side);
                    c = new Vector3((line + 1.5f) * t.Side, 0, basis.z);
                }
                c.x = Mathf.Clamp(c.x, -Pitch.HalfL + 2f, Pitch.HalfL - 2f);
                c.z = Mathf.Clamp(c.z, -Pitch.HalfW + 1.2f, Pitch.HalfW - 1.2f);

                float s = 0;
                // space from opponents
                float near = 99;
                foreach (var o in t.Opp.Players) near = Mathf.Min(near, Util.FlatDist(o.Pos, c));
                s += Mathf.Clamp(near, 0, 6) * 0.9f;
                // a clean passing lane from the carrier
                s += carrier.Openness(c, carrier.Pos) * 4f;
                // forward progress
                s += t.Side * (c.x - carrier.Pos.x) * 0.25f;
                // distance from the carrier: 6 to 14 m is ideal
                float dc = Util.FlatDist(c, carrier.Pos);
                s -= Mathf.Abs(dc - 9f) * 0.25f;
                // don't crowd teammates
                foreach (var o in t.Players) if (o != p) s -= Mathf.Max(0, 4f - Util.FlatDist(o.Pos, c)) * 0.8f;
                // strikers like being near goal
                if (p.Def.Role == Role.Striker) s -= Util.FlatDist(c, goal) * 0.12f;
                // stay near the role's slot
                s -= Util.FlatDist(c, basis) * 0.15f;
                if (s > bs) { bs = s; best = c; }
            }
            return best;
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

        // ------------------------------------------------------------------ on the ball
        static void Carrier(Player p, float dt)
        {
            var t = p.Team; var m = p.M; var sk = t.Skill;
            var goal = OppGoal(t);
            float goalDist = Util.FlatDist(goal, p.Pos);
            var toGoal = Util.Flat(goal - p.Pos).normalized;

            // pressure
            float pressure = 99;
            foreach (var o in t.Opp.Players)
            {
                if (o.State == PState.Down || o.State == PState.Flying) continue;
                pressure = Mathf.Min(pressure, Util.FlatDist(o.Pos, p.Pos));
            }

            // one-two return: play it straight into the runner's path
            if (p.ReturnTo != null && p.ReturnTo.CanAct)
            {
                var runner = p.ReturnTo;
                p.ReturnTo = null;
                p.PassToSpace(runner);
                return;
            }

            if (p.ThinkT <= 0)
            {
                p.ThinkT = Random.Range(0.15f, 0.28f) * sk.Think;

                // special move?
                if (p.Chi >= 1 && m.Specials.CanUse(p))
                {
                    float range = p.Def.Special == SpecialType.AerialShot ? 21 : p.Def.Special == SpecialType.CurveShot ? 24 : 26;
                    if (goalDist < range && goalDist > 7 && Random.value < 0.4f + 0.3f * sk.Vision) { m.Specials.Begin(p); return; }
                }

                // weigh shooting, passing and dribbling
                float lane = p.Openness(goal, p.Pos);
                float angle = Mathf.Abs(Vector3.Angle(toGoal, new Vector3(t.Side, 0, 0)));
                float shoot = 0;
                if (goalDist < 20f) shoot = (20f - goalDist) * 0.32f + lane * 2.5f - angle * 0.04f + (goalDist < 7.5f ? 3 : 0);

                Player bestMate = null; float pass = -99; bool through = false;
                foreach (var mate in t.Players)
                {
                    if (mate == p || mate.State == PState.Down || mate.State == PState.Flying || mate.IsKeeper) continue;
                    float open = p.Openness(mate.Pos, p.Pos);
                    float prog = t.Side * (mate.Pos.x - p.Pos.x);
                    float matGoal = Util.FlatDist(mate.Pos, goal);
                    float sc = open * 4f + prog * 0.22f - matGoal * 0.08f - Mathf.Abs(Util.FlatDist(mate.Pos, p.Pos) - 9f) * 0.12f;
                    // runners ahead with space behind the defence: through ball
                    bool canThrough = prog > 2 && (mate.Def.Role == Role.Striker || mate.Def.Role == Role.Winger) && open > 0.35f && matGoal > 5;
                    if (canThrough) sc += 1.2f;
                    if (sc > pass) { pass = sc; bestMate = mate; through = canThrough; }
                }
                if (pressure < 2.2f) pass += 2.5f; else if (pressure > 5f) pass -= 1.5f;

                float dribble = pressure > 3.5f ? 2.5f : pressure > 2.2f ? 0.8f : -1f;
                dribble += (goalDist > 14f ? 1f : 0f);

                // weaker CPUs sometimes make the second-best choice
                if (Random.value > sk.Vision) { shoot += Random.Range(-2f, 2f); pass += Random.Range(-2f, 2f); dribble += Random.Range(-2f, 2f); }

                if (shoot > pass && shoot > dribble && shoot > 1.2f)
                {
                    var keeper = t.Opp.Keeper;
                    float aimZ = keeper != null ? (keeper.Pos.z > 0 ? -0.85f : 0.85f) : 0;
                    float power = goalDist < 9 ? Random.Range(0.55f, 0.85f) : Random.Range(0.8f, 1f);
                    p.Shoot(power, aimZ);
                    return;
                }
                if (bestMate != null && p.PassCd <= 0 && pass > dribble && pass > 0.5f)
                {
                    float open = p.Openness(bestMate.Pos, p.Pos);
                    float dist = Util.FlatDist(bestMate.Pos, p.Pos);
                    if (through && Random.value < 0.6f) p.PassToSpace(bestMate);
                    else if (open < 0.5f && dist > 9f) p.PassTo(bestMate, true);
                    else if (pressure < 2.5f && dist < 12f && Random.value < 0.35f * sk.Vision) p.OneTwo(bestMate);
                    else p.PassTo(bestMate, false);
                    return;
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
                    dir += side * s * (4f - d) * 0.4f + away / d * (4f - d) * 0.12f;
                }
            }
            if (Mathf.Abs(p.Pos.z) > Pitch.HalfW - 2.5f) dir += new Vector3(0, 0, -Mathf.Sign(p.Pos.z) * 0.8f);
            p.Wish = dir.normalized;
            p.WishSprint = pressure > 3f;
        }

        // ------------------------------------------------------------------ goalkeeper
        static void Keeper(Player p, float dt)
        {
            var t = p.Team; var m = p.M; var b = m.Ball; var sk = t.Skill;
            float goalX = -t.Side * Pitch.HalfL;

            if (b.Owner == p)
            {
                // hold for a moment, then distribute (quick throw to an open player, or a long ball to a runner)
                p.Wish = Vector3.zero;
                p.ThinkT -= dt;
                if (p.StateT > 0.8f * sk.Think && p.ThinkT <= 0)
                {
                    p.ThinkT = 0.4f;
                    var target = p.PickPassTarget(new Vector3(t.Side, 0, 0), true);
                    if (target != null)
                    {
                        p.State = PState.Free;
                        float dist = Util.FlatDist(target.Pos, p.Pos);
                        if (dist > 14f && target.Def.Role != Role.Defender) p.PassToSpace(target);
                        else p.PassTo(target, dist > 12f);
                    }
                }
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
                        float react = 0.4f * Mathf.Clamp(sk.Keeper, 0.7f, 1.3f);
                        if (Mathf.Abs(dz) > 0.9f && tt < react && p.State == PState.Free)
                        {
                            p.SetState(PState.Dive, 0.8f);
                            float side = Mathf.Sign(dz);
                            p.Body.Side = Vector3.Dot(p.transform.right, new Vector3(0, 0, side)) >= 0 ? 1 : -1;
                            p.Vel = new Vector3(0, 0, side * Mathf.Min(Mathf.Abs(dz) / Mathf.Max(tt, 0.15f), 9f * sk.Keeper));
                            p.Jump(Mathf.Clamp(hit.y * 3.2f, 1.5f, 6f));
                        }
                        return;
                    }
                }
            }

            // rush out for a loose ball in the box, or to smother a through ball
            bool inBox = Pitch.InBox(b.Pos, goalX);
            if (b.Loose && !b.InNet && b.Pos.y < 2.2f && (inBox || Util.FlatDist(b.Pos, p.Pos) < 5f))
            {
                var nearestOpp = t.Opp.ClosestTo(b.Pos, true, false);
                float myT = Util.FlatDist(p.Pos, b.Pos) / p.BaseSpeed;
                float theirT = nearestOpp != null ? Util.FlatDist(nearestOpp.Pos, b.Pos) / nearestOpp.BaseSpeed : 99;
                if (t.ClosestTo(b.Pos, false, false) == p || myT < theirT * 0.9f)
                {
                    var d = Util.Flat(b.Predict(0.15f) - p.Pos);
                    p.Wish = d.normalized; p.WishSprint = true;
                    return;
                }
            }

            // narrow the angle: stand on the line from the ball to the goal centre
            float bxRel = Mathf.Abs(b.Pos.x - goalX);
            float out_ = bxRel < 8 ? 1.8f : bxRel < 16 ? 2.6f : 1.4f;
            var goalC = new Vector3(goalX, 0, 0);
            var toBall = Util.Flat(b.Pos - goalC);
            var spot = goalC + (toBall.sqrMagnitude > 0.01f ? toBall.normalized : new Vector3(t.Side, 0, 0)) * out_;
            spot.z = Mathf.Clamp(spot.z, -Pitch.GoalHalfW + 0.5f, Pitch.GoalHalfW - 0.5f);
            if ((spot.x - goalX) * t.Side < 0.6f) spot.x = goalX + t.Side * 0.6f;
            var dd = Util.Flat(spot - p.Pos);
            p.Wish = dd.magnitude > 0.2f ? Vector3.ClampMagnitude(dd * 1.2f, 1f) : Vector3.zero;
            if (p.Wish.sqrMagnitude < 0.04f)
            {
                var face = Util.Flat(b.Pos - p.Pos);
                if (face.sqrMagnitude > 0.01f)
                    p.transform.rotation = Quaternion.RotateTowards(p.transform.rotation, Quaternion.LookRotation(face.normalized), 400 * dt);
            }
        }
    }
}
