using System.Collections.Generic;
using UnityEngine;

namespace ChiStrikers
{
    public enum MatchState { Title, Kickoff, Playing, Goal, HalfTime, FullTime }
    public enum GameMode { Demo, VsCpu, Versus, Coop }

    public sealed class Team
    {
        public TeamDef Def;
        public int Side;               // +1 attacks the east goal (x > 0)
        public readonly List<Player> Players = new List<Player>();
        public int Score;
        public Team Opp;
        public readonly List<Controls> Humans = new List<Controls>();

        public Player Keeper => Players[0];
        public Player ByRole(Role r) => Players[(int)r];

        public Player ClosestTo(Vector3 pos, bool outfieldOnly, bool excludeHumans)
        {
            Player best = null; float bd = float.MaxValue;
            foreach (var p in Players)
            {
                if (outfieldOnly && p.IsKeeper) continue;
                if (excludeHumans && p.Human != null) continue;
                if (p.State == PState.Down || p.State == PState.Flying) continue;
                float d = Util.FlatDist(p.Pos, pos);
                if (d < bd) { bd = d; best = p; }
            }
            return best;
        }
    }

    /// <summary>
    /// Entry point: builds the arena, both teams, the ball, camera, effects and HUD in code, and runs the match:
    /// title screen with a CPU-vs-CPU demo behind it, kick-off, two halves, goals, golden goal and full time.
    /// </summary>
    public sealed class Match : MonoBehaviour
    {
        public const float HalfLength = 180f;

        public Team Home, Away;
        public Ball Ball;
        public CameraRig Cam;
        public Fx Fx;
        public Arena Arena;
        public Specials Specials;
        public HUD Hud;

        public MatchState State;
        public GameMode Mode = GameMode.Demo;
        public bool Cinematic, Paused, Golden;
        public int Half = 1;
        public float Clock, StateT;
        public Team KickingTeam;
        public Player LastScorer; public Team LastScoringTeam; public bool LastWasSpecial;
        public string Toast; public float ToastT;
        public readonly Controls P1 = new Controls(0), P2 = new Controls(1);
        public int MenuIndex, PauseIndex;
        public bool ShowHelp;

        void Awake()
        {
            Application.targetFrameRate = 120;
            Time.timeScale = 1f;
            Sfx.Init(gameObject);
            Fx = gameObject.AddComponent<Fx>();
            Fx.Init(this);
            Arena = Arena.Build(this);
            Cam = CameraRig.Create(this);
            Ball = Ball.Create(this);
            Specials = new Specials(this);
            Hud = gameObject.AddComponent<HUD>();
            Hud.M = this;

            Home = new Team { Def = Teams.Mohalla, Side = -1 };
            Away = new Team { Def = Teams.Titan, Side = 1 };
            Home.Opp = Away; Away.Opp = Home;
            foreach (var t in new[] { Home, Away })
                for (int i = 0; i < 5; i++)
                    t.Players.Add(Player.Spawn(this, t, t.Def.Heroes[i], i));

            GoToTitle();
        }

        // ------------------------------------------------------------------ flow
        public void GoToTitle()
        {
            Mode = GameMode.Demo;
            SetupHumans();
            NewMatch();
            State = MatchState.Title;
            MenuIndex = 0;
            Paused = false; ShowHelp = false;
            Time.timeScale = 1;
        }

        public void StartMode(GameMode mode)
        {
            Mode = mode;
            SetupHumans();
            NewMatch();
            BeginKickoff(Home);
        }

        void SetupHumans()
        {
            Home.Humans.Clear(); Away.Humans.Clear();
            foreach (var p in AllPlayers()) p.Human = null;
            switch (Mode)
            {
                case GameMode.VsCpu: Home.Humans.Add(P1); break;
                case GameMode.Versus: Home.Humans.Add(P1); Away.Humans.Add(P2); break;
                case GameMode.Coop: Home.Humans.Add(P1); Home.Humans.Add(P2); break;
            }
        }

        void NewMatch()
        {
            Home.Score = Away.Score = 0;
            Half = 1; Clock = HalfLength; Golden = false;
            foreach (var p in AllPlayers()) p.Chi = 0.3f;
            Place(Home);
            Cam.Snap();
        }

        public IEnumerable<Player> AllPlayers()
        {
            foreach (var p in Home.Players) yield return p;
            foreach (var p in Away.Players) yield return p;
        }

        void Place(Team kicking)
        {
            KickingTeam = kicking;
            foreach (var t in new[] { Home, Away })
                for (int i = 0; i < t.Players.Count; i++)
                {
                    var spot = Brain.KickoffSpot(t, i, t == kicking);
                    t.Players[i].ResetAt(spot, t.Side > 0 ? 90 : -90);
                }
            Ball.ResetTo(new Vector3(0, Ball.R, 0));
            Ball.ClearSpecial();
        }

        void BeginKickoff(Team kicking)
        {
            Place(kicking);
            State = MatchState.Kickoff; StateT = 0;
            Cinematic = false;
            Cam.Release(); Cam.Snap();
            foreach (var t in new[] { Home, Away })
                foreach (var c in t.Humans) AssignControl(t, c, t.ByRole(Role.Striker));
        }

        void Update()
        {
            float dt = Time.deltaTime;
            StateT += Time.unscaledDeltaTime;
            ToastT -= Time.unscaledDeltaTime;

            if (State == MatchState.Title) { TitleInput(); }
            else if (Controls.PausePressed() && State != MatchState.FullTime) { TogglePause(); }
            if (Paused) { PauseInput(); return; }

            switch (State)
            {
                case MatchState.Kickoff:
                    if (StateT > 1.6f)
                    {
                        State = MatchState.Playing; StateT = 0;
                        Sfx.Play(Sfx.Whistle, 0.8f);
                        Ball.TakeBy(KickingTeam.ByRole(Role.Striker));
                    }
                    break;
                case MatchState.Playing:
                case MatchState.Title:
                    Clock -= dt;
                    if (Clock <= 0 && !Golden) EndOfHalf();
                    break;
                case MatchState.Goal:
                    if (StateT > 3.4f)
                    {
                        if (Golden) { FullTime(); break; }
                        BeginKickoff(LastScoringTeam.Opp);
                    }
                    break;
                case MatchState.HalfTime:
                    if (StateT > 3f) { Half = 2; Clock = HalfLength; BeginKickoff(Away); }
                    break;
                case MatchState.FullTime:
                    if (StateT > 1.5f && (P1.Confirm || P2.Confirm)) GoToTitle();
                    break;
            }

            // humans, then AI, then movement
            bool live = State == MatchState.Playing || State == MatchState.Title;
            foreach (var t in new[] { Home, Away })
                foreach (var c in t.Humans) if (live) HumanControl(t, c);
            foreach (var p in AllPlayers())
            {
                if (live && p.Human == null) Brain.Think(p, dt);
                else if (!live) { p.Wish = Vector3.zero; p.WishSprint = false; }
            }
            foreach (var p in AllPlayers()) p.Tick(dt);
            Separate();
            if (State != MatchState.Kickoff) Ball.Step(dt);
            else Ball.Step(0.0001f);
            if (live) Contacts();

            Arena.SetBoard($"{Home.Def.Short} {Home.Score} - {Away.Score} {Away.Def.Short}", ClockText());
            Sfx.CrowdLevel(State == MatchState.Goal ? 0.5f : 0.22f + Arena.Excite * 0.25f);

            // the demo behind the title loops forever
            if (State == MatchState.Title && demoReset > 0)
            {
                demoReset -= dt;
                if (demoReset <= 0)
                {
                    if (Home.Score + Away.Score >= 5) { Home.Score = Away.Score = 0; }
                    BeginKickoffDemo(demoConceded);
                }
            }
        }

        float demoReset; Team demoConceded;
        void BeginKickoffDemo(Team t = null) { t = t ?? Home; Place(t); Ball.TakeBy(t.ByRole(Role.Striker)); }

        public string ClockText()
        {
            if (Golden) return "GOLDEN GOAL";
            float c = Mathf.Max(0, Clock);
            int m = (int)(c / 60), s = (int)(c % 60);
            return $"{m}:{s:00}";
        }

        void EndOfHalf()
        {
            if (State == MatchState.Title) { Clock = HalfLength; return; }
            Sfx.Play(Sfx.LongWhistle, 0.8f);
            if (Half == 1) { State = MatchState.HalfTime; StateT = 0; return; }
            if (Home.Score == Away.Score)
            {
                Golden = true;
                Toast = "GOLDEN GOAL! Next goal wins."; ToastT = 3f;
                BeginKickoff(Home);
                return;
            }
            FullTime();
        }

        void FullTime()
        {
            State = MatchState.FullTime; StateT = 0;
            Sfx.Play(Sfx.LongWhistle, 0.9f);
            var winner = Home.Score > Away.Score ? Home : Away;
            foreach (var p in winner.Players) p.SetState(PState.Celebrate, 99);
            Arena.Excite = 1;
        }

        void TogglePause()
        {
            Paused = !Paused;
            PauseIndex = 0;
            Time.timeScale = Paused ? 0 : (Specials.Running ? 0.3f : 1f);
        }

        void PauseInput()
        {
            int n = P1.NavY(); if (n != 0) { PauseIndex = (PauseIndex - n + 3) % 3; Sfx.Play(Sfx.Pass, 0.3f, 1.5f); }
            if (P1.Confirm)
            {
                if (PauseIndex == 0) TogglePause();
                else if (PauseIndex == 1) { Paused = false; Time.timeScale = 1; StopAllCoroutines(); Specials.Running = false; Cinematic = false; StartMode(Mode); }
                else { Paused = false; StopAllCoroutines(); Specials.Running = false; Cinematic = false; GoToTitle(); }
            }
        }

        void TitleInput()
        {
            if (ShowHelp) { if (P1.Confirm || P1.Back || Controls.PausePressed()) ShowHelp = false; return; }
            int n = P1.NavY(); if (n != 0) { MenuIndex = (MenuIndex - n + 4) % 4; Sfx.Play(Sfx.Pass, 0.3f, 1.5f); }
            if (P1.Confirm)
            {
                Sfx.Play(Sfx.Chime, 0.6f);
                StopAllCoroutines(); Specials.Running = false; Cinematic = false; Time.timeScale = 1;
                switch (MenuIndex)
                {
                    case 0: StartMode(GameMode.VsCpu); break;
                    case 1: StartMode(GameMode.Versus); break;
                    case 2: StartMode(GameMode.Coop); break;
                    case 3: ShowHelp = true; break;
                }
            }
        }

        // ------------------------------------------------------------------ human control
        Player ControlledBy(Team t, Controls c)
        {
            foreach (var p in t.Players) if (p.Human == c) return p;
            return null;
        }

        void AssignControl(Team t, Controls c, Player p)
        {
            if (p == null || p.IsKeeper) return;
            if (p.Human != null && p.Human != c) return; // the other human has this one
            var cur = ControlledBy(t, c);
            if (cur == p) return;
            if (cur != null) { cur.Human = null; cur.Charge = -1; }
            p.Human = c;
        }

        Player NearestFree(Team t, Vector3 pos, Controls c)
        {
            Player best = null; float bd = float.MaxValue;
            foreach (var p in t.Players)
            {
                if (p.IsKeeper || (p.Human != null && p.Human != c)) continue;
                float d = Util.FlatDist(p.Pos, pos);
                if (d < bd) { bd = d; best = p; }
            }
            return best;
        }

        void HumanControl(Team t, Controls c)
        {
            var p = ControlledBy(t, c);
            if (p == null) { AssignControl(t, c, NearestFree(t, Ball.Pos, c)); p = ControlledBy(t, c); if (p == null) return; }

            var mv = c.Move;
            var dir = new Vector3(mv.x, 0, mv.y);
            p.Wish = dir;
            p.WishSprint = c.Sprint;

            // no input while a pass is coming: run onto it
            if (dir.sqrMagnitude < 0.01f && Ball.Receiver == p && Ball.Loose)
            {
                var d = Util.Flat(Ball.Predict(0.25f) - p.Pos);
                if (d.magnitude > 0.4f) p.Wish = d.normalized;
            }

            if (!p.CanAct || Specials.Running) return;

            if (p.HasBall)
            {
                var aimDir = dir.sqrMagnitude > 0.01f ? dir : p.Fwd;
                if (c.SpecialDown)
                {
                    if (Specials.CanUse(p)) Specials.Begin(p);
                    else ShowToast(p.Chi < 1 ? $"{p.Def.Name}'s Chi isn't full yet" : $"{p.Def.Name}'s special is for defending");
                }
                else if (c.PassDown) p.PassTo(p.PickPassTarget(aimDir, false), false);
                else if (c.LobDown) p.PassTo(p.PickPassTarget(aimDir, true), true);
                else if (c.ShootDown) p.Charge = 0;

                if (p.Charge >= 0 && !c.ShootHeld && p.State == PState.Free)
                    p.Shoot(p.Charge, mv.y);
            }
            else
            {
                p.Charge = -1;
                if (c.PassDown) p.Poke();
                else if (c.ShootDown) p.Slide();
                else if (c.SwitchDown) Switch(t, c, p);
                else if (c.SpecialDown)
                {
                    if (p.Def.Special == SpecialType.Shockwave && Specials.CanUse(p)) Specials.Begin(p);
                    else
                    {
                        // jump control to whoever on the team can use a special right now
                        Player ready = null;
                        foreach (var o in t.Players) if (o.Def.Special == SpecialType.Shockwave && Specials.CanUse(o) && o.Human == null) ready = o;
                        if (ready != null) { AssignControl(t, c, ready); Specials.Begin(ready); }
                        else ShowToast("Specials need the ball (Bhola's works anytime)");
                    }
                }
            }
        }

        void Switch(Team t, Controls c, Player cur)
        {
            Player best = null; float bd = float.MaxValue;
            foreach (var p in t.Players)
            {
                if (p == cur || p.IsKeeper || (p.Human != null && p.Human != c)) continue;
                float d = Util.FlatDist(p.Pos, Ball.Pos);
                if (d < bd) { bd = d; best = p; }
            }
            if (best != null) { AssignControl(t, c, best); Sfx.Play(Sfx.Pass, 0.2f, 2f); }
        }

        public void ShowToast(string s) { Toast = s; ToastT = 2f; }

        // ------------------------------------------------------------------ events
        public void OnPossession(Player p, Player prevOwner, Player lastTouch)
        {
            if (lastTouch != null && lastTouch != p && prevOwner == null)
            {
                if (lastTouch.Team == p.Team) { p.Chi = Mathf.Min(1, p.Chi + 0.08f); lastTouch.Chi = Mathf.Min(1, lastTouch.Chi + 0.06f); }
                else p.Chi = Mathf.Min(1, p.Chi + 0.1f);
            }
            if (p.IsKeeper && Pitch.InBox(p.Pos, -p.Team.Side * Pitch.HalfL)) p.SetState(PState.Hold, 99);

            // humans take over whoever on their team just got the ball (unless the other human has them)
            foreach (var c in p.Team.Humans)
            {
                var cur = ControlledBy(p.Team, c);
                if (cur == p) return;
            }
            if (!p.IsKeeper && p.Team.Humans.Count > 0 && p.Human == null)
            {
                // pick the human whose player is closest
                Controls bestC = null; float bd = float.MaxValue;
                foreach (var c in p.Team.Humans)
                {
                    var cur = ControlledBy(p.Team, c);
                    float d = cur ? Util.FlatDist(cur.Pos, p.Pos) : 0;
                    if (d < bd) { bd = d; bestC = c; }
                }
                if (bestC != null) AssignControl(p.Team, bestC, p);
            }
        }

        public void OnPass(Player from, Player to)
        {
            if (from.Human != null && to != null && !to.IsKeeper && to.Human == null) AssignControl(from.Team, from.Human, to);
        }

        public void OnSpecialLaunched(Player p)
        {
            Arena.Excite = Mathf.Max(Arena.Excite, 0.7f);
        }

        public void OnGoal(int side, Ball b)
        {
            if (State != MatchState.Playing && State != MatchState.Title) return;
            var team = Home.Side == side ? Home : Away;
            team.Score++;
            LastScoringTeam = team;
            LastScorer = b.LastTouch != null && b.LastTouch.Team == team ? b.LastTouch : null;
            LastWasSpecial = b.Special != null;
            var goalPos = new Vector3(side * Pitch.HalfL, 1.2f, 0);
            Sfx.Play(Sfx.Roar, 1f);
            Sfx.Play(Sfx.Whistle, 0.7f);
            Arena.Excite = 1f;
            Fx.Confetti(goalPos + new Vector3(side * 2, 0, 0), team.Def.Primary, team.Def.Secondary);
            Fx.Burst(b.Pos, Color.white, team.Def.Ui, 40, 9f);
            Fx.Shake(LastWasSpecial ? 0.8f : 0.3f);
            if (LastWasSpecial)
            {
                Fx.Flash(0.9f);
                Fx.Ring(new Vector3(b.Pos.x, 0.05f, b.Pos.z), 0.5f, 9f, b.Special.FxB, 0.8f);
                Fx.Burst(b.Pos, b.Special.FxA, b.Special.FxB, 120, 14f);
                Sfx.Play(Sfx.Boom, 1f, 0.7f);
            }
            b.ClearSpecial();
            if (State == MatchState.Title)
            {
                demoReset = 2.5f; demoConceded = team.Opp; // demo: restart from the centre shortly
                return;
            }
            State = MatchState.Goal; StateT = 0;
            if (LastScorer != null)
            {
                LastScorer.SetState(PState.Celebrate, 99);
                Cam.FrameOn(LastScorer.Pos + Vector3.up * 1.1f, LastScorer.Pos + new Vector3(-side * 2.5f, 2.2f, -6.5f));
            }
            foreach (var p in team.Players) if (p != LastScorer && !p.IsKeeper) { p.Wish = Vector3.zero; }
        }

        // ------------------------------------------------------------------ ball contacts
        void Contacts()
        {
            var b = Ball;
            if (b.Owner != null || b.InNet || b.NoPickup > 0) return;
            if (Specials.Running && Specials.Current != null && Specials.Current.State == PState.Special && b.LastTouch == Specials.Current && b.Special == null) return;

            foreach (var p in AllPlayers())
            {
                if (p.State == PState.Down || p.State == PState.Flying || p.State == PState.Special || p.State == PState.Celebrate) continue;
                if (p == b.Ignore && b.IgnoreTime > 0) continue;
                bool opponentShot = b.ShotTeam != null && b.ShotTeam != p.Team;
                var bp = b.Pos;
                float flat = Util.FlatDist(bp, p.Pos);

                // Akhada Wall / Titan Slam: nothing gets past
                if (p.WallTime > 0 && opponentShot && flat < 1.9f && bp.y < 3f)
                {
                    Fx.Ring(p.Pos + Vector3.up * 0.05f, 0.4f, 3.5f, p.Def.FxB, 0.4f);
                    Fx.Burst(bp, p.Def.FxA, p.Def.FxB, 40, 8f);
                    Sfx.Play(Sfx.Boom, 0.8f, 1.2f);
                    Fx.Shake(0.4f);
                    b.ClearShot();
                    b.TakeBy(p);
                    ShowToast($"{p.Def.Name}'s wall stops it!");
                    return;
                }

                if (p.IsKeeper)
                {
                    float goalX = -p.Team.Side * Pitch.HalfL;
                    bool inArea = Mathf.Abs(p.Pos.x - goalX) < Pitch.BoxDepth + 1;
                    var reachC = p.Pos + Vector3.up * (0.9f + p.Lift);
                    float radius = 1.0f;
                    if (p.State == PState.Dive) { reachC += new Vector3(0, -0.2f, p.Vel.z * 0.12f); radius = 1.3f; }
                    var d3 = bp - reachC; d3.y *= 0.6f;
                    if (d3.magnitude > radius) continue;
                    if (!inArea && b.Speed > 3) continue;

                    if (opponentShot && b.Special != null)
                    {
                        if (p.Def.Special == SpecialType.KeeperSave && p.Chi >= 1 && !Specials.Running)
                        {
                            StartCoroutine(Specials.KeeperSave(p));
                            return;
                        }
                        if (p.Chi >= 0.5f && Random.value < 0.5f)
                        {
                            p.Chi -= 0.5f;
                            b.Vel = new Vector3(-b.Vel.x * 0.3f, 7f, b.Vel.z * 0.3f + Random.Range(-4f, 4f));
                            b.ClearShot(); b.LastTouch = p; b.Ignore = p; b.IgnoreTime = 0.5f;
                            p.Knock(new Vector3(-p.Team.Side * -3f, 4f, 0), false);
                            Fx.Burst(bp, b.Vel.normalized == Vector3.zero ? Color.white : Color.white, p.Team.Def.Ui, 30, 8f);
                            Sfx.Play(Sfx.Thud, 1f, 0.8f);
                            ShowToast($"{p.Def.Name} punches it away!");
                            return;
                        }
                        // blasted into the net
                        p.Knock(Util.Flat(b.Vel).normalized * 7f + Vector3.up * 7f, true);
                        b.SpecialPower -= 1f;
                        b.Ignore = p; b.IgnoreTime = 1f;
                        Fx.Burst(bp, b.Special.FxA, b.Special.FxB, 40, 10f);
                        Sfx.Play(Sfx.Thud, 1f, 0.6f);
                        Fx.Shake(0.5f);
                        continue;
                    }
                    if (opponentShot && !b.KeeperChecked)
                    {
                        b.KeeperChecked = true;
                        float speed = b.Speed;
                        float save = 0.9f * p.Def.Keeping - Mathf.Max(0, speed - 17f) * 0.028f - Mathf.Clamp01((bp.y - 1.8f) * 0.4f);
                        if (Random.value < save)
                        {
                            p.Chi = Mathf.Min(1, p.Chi + 0.2f);
                            if (speed < 23f || Random.value < 0.4f) { b.ClearShot(); b.TakeBy(p); Sfx.Play(Sfx.Thud, 0.7f, 1.2f); }
                            else
                            {
                                b.Vel = new Vector3(-b.Vel.x * 0.25f, 5f, Random.Range(-6f, 6f));
                                b.ClearShot(); b.LastTouch = p; b.Ignore = p; b.IgnoreTime = 0.5f;
                                Sfx.Play(Sfx.Thud, 0.9f, 1f);
                                Fx.Burst(bp, Color.white, p.Team.Def.Ui, 12, 5f);
                            }
                            ShowToast(p.Def.Name + " saves!");
                            return;
                        }
                        b.Ignore = p; b.IgnoreTime = 0.5f;
                        continue;
                    }
                    if (!opponentShot || b.Speed < 8f) { b.TakeBy(p); return; }
                    continue;
                }

                // outfield players
                if (flat > 0.75f || bp.y > 1.0f + p.Lift) continue;
                if (b.Special != null)
                {
                    if (!opponentShot) continue;
                    if (b.Special.Special != SpecialType.PowerShot) continue; // bends around / flies over everyone
                    var push = Util.Flat(b.Vel).normalized;
                    p.Knock(push * 8f + Vector3.up * 9f, true);
                    b.SpecialPower -= 1f;
                    b.Ignore = p; b.IgnoreTime = 1f;
                    Fx.Burst(bp, b.Special.FxA, b.Special.FxB, 25, 7f);
                    Sfx.Play(Sfx.Thud, 0.9f, 0.7f);
                    Fx.Shake(0.3f);
                    if (b.SpecialPower <= 0) { b.ClearSpecial(); b.Vel *= 0.35f; b.ClearShot(); }
                    continue;
                }
                if (opponentShot && b.Speed > 15f)
                {
                    // block
                    b.Vel = new Vector3(-b.Vel.x * 0.25f, 3.5f, b.Vel.z * 0.3f + Random.Range(-3f, 3f));
                    b.ClearShot(); b.LastTouch = p; b.Ignore = p; b.IgnoreTime = 0.3f;
                    Sfx.Play(Sfx.Thud, 0.6f, 1.2f);
                    ShowToast(p.Def.Name + " blocks!");
                    return;
                }
                if (b.ShotTeam == p.Team && b.Speed > 15f) continue; // a teammate's shot flies past
                b.TakeBy(p);
                return;
            }
        }

        void Separate()
        {
            var list = new List<Player>(AllPlayers());
            for (int i = 0; i < list.Count; i++)
                for (int j = i + 1; j < list.Count; j++)
                {
                    var a = list[i]; var c = list[j];
                    if (a.State == PState.Flying || c.State == PState.Flying || a.State == PState.Special || c.State == PState.Special) continue;
                    var d = Util.Flat(a.Pos - c.Pos);
                    float dist = d.magnitude;
                    const float min = 0.75f;
                    if (dist < min && dist > 0.0001f)
                    {
                        var push = d / dist * (min - dist) * 0.5f;
                        a.Pos += push; c.Pos -= push;
                    }
                }
        }
    }
}
