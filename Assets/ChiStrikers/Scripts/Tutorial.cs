using UnityEngine;

namespace ChiStrikers
{
    /// <summary>
    /// Interactive tutorial in the style of mobile football games: one short drill at a time on the real pitch,
    /// a coach card with the keys to press, a ring or an arrow showing where to go, "NICE!" when it's done.
    /// </summary>
    public sealed class Tutorial
    {
        public const int Count = 9;
        readonly Match m;
        public int Step = -1;
        public float StepT, SuccessT = -1;
        public int Tries;
        public string Title, Body;
        public string[] Keys = new string[0];
        public Player Highlight; public string HighlightLabel;
        public bool Done => Step >= Count;
        public const string PrefKey = "chi_tutorial_done";

        GameObject ring; Material ringMat; Vector3? target;
        float sprintTime; bool shotTaken; float resetT; bool specialSeen;
        Player actor, runner; Vector3 runnerGoal;

        public Tutorial(Match match) { m = match; }

        Team Home => m.Home;
        Team Away => m.Away;
        Vector3 Rel(float x, float z) => new Vector3(x * Home.Side, 0, z);
        bool Pad => m.P1.HasPad;
        string K(string key, string pad) => Pad ? key + "  /  " + pad : key;

        public void Begin()
        {
            Step = -1;
            if (!ring)
            {
                ringMat = Util.Fade(new Color(1f, 0.79f, 0.24f, 0.9f));
                ringMat.renderQueue = 3090;
                ring = Util.MeshObj("Tutorial target", Util.Ring(0.9f, 1.25f, 48), null, Vector3.zero, ringMat);
            }
            Next();
        }

        public void End()
        {
            if (ring) ring.SetActive(false);
            Step = -1; Highlight = null; target = null;
        }

        void Next()
        {
            Step++;
            StepT = 0; SuccessT = -1; Tries = 0;
            Setup();
        }

        // ------------------------------------------------------------------ drills
        void Arrange(Player controlled, Vector3 ballAt)
        {
            // everyone to a neutral spot; the opposition stand as passive "mannequins" away from the drill
            var awaySpots = new[] { Rel(19.5f, 0), Rel(15, -8), Rel(16, 8), Rel(11, 10), Rel(18, -3) };
            for (int i = 0; i < 5; i++) Away.Players[i].ResetAt(awaySpots[i], Home.Side > 0 ? -90 : 90);
            var homeSpots = new[] { Rel(-19.5f, 0), Rel(-11, 0), Rel(-6, -6), Rel(-3, 8), Rel(-1, -2) };
            for (int i = 0; i < 5; i++) Home.Players[i].ResetAt(homeSpots[i], Home.Side > 0 ? 90 : -90);
            foreach (var p in m.AllPlayers()) { p.Chi = 0.3f; p.ReturnTo = null; }
            m.Ball.ResetTo(ballAt + Vector3.up * Ball.R);
            m.Ball.ClearSpecial();
            if (controlled != null) m.GiveControl(Home, m.P1, controlled);
            Highlight = null; HighlightLabel = null; target = null; actor = null; runner = null;
            shotTaken = false; resetT = 0; specialSeen = false; sprintTime = 0;
            m.LastPassThrough = false;
            m.Cam.Snap();
        }

        void Put(Player p, Vector3 at) { p.ResetAt(at, Home.Side > 0 ? 90 : -90); }

        void GiveBall(Player p)
        {
            m.Ball.ResetTo(p.Pos + p.Fwd * 0.5f + Vector3.up * Ball.R);
            m.Ball.TakeBy(p);
        }

        Player Kabir => Home.ByRole(Role.Playmaker);
        Player Tara => Home.ByRole(Role.Winger);
        Player Veer => Home.ByRole(Role.Striker);
        Player Bhola => Home.ByRole(Role.Defender);
        Player Rex => Away.ByRole(Role.Striker);

        void Setup()
        {
            switch (Step)
            {
                case 0:
                    Title = "MOVE"; Body = "Move Kabir onto the yellow ring. The ball stays at your feet while you run.";
                    Keys = new[] { K("W A S D", "Left stick") };
                    Arrange(Kabir, Vector3.zero); Put(Kabir, Rel(-6, 0)); GiveBall(Kabir);
                    target = Rel(-1, 5);
                    break;
                case 1:
                    Title = "SPRINT"; Body = "Hold Sprint and race to the next ring. You're faster, but the ball is harder to keep.";
                    Keys = new[] { K("Hold SHIFT", "Hold RT") };
                    target = Rel(9, -4);
                    break;
                case 2:
                    Title = "PASS"; Body = "Turn toward Tara and press Pass. The ball goes to the teammate you're facing. You then control her.";
                    Keys = new[] { K("J", "A") };
                    Arrange(Kabir, Vector3.zero); Put(Kabir, Rel(1, -3)); Put(Tara, Rel(7, 6)); GiveBall(Kabir);
                    Highlight = Tara; HighlightLabel = "PASS HERE";
                    break;
                case 3:
                    Title = "DIRECT PASS"; Body = "No time to turn? Every teammate shows a key over their head. Press it to pass straight to them. Pass to Veer!";
                    Keys = new[] { Pad ? "LT + A" : "5" };
                    Arrange(Tara, Vector3.zero); Put(Tara, Rel(4, 7)); Put(Veer, Rel(10, -4)); Put(Kabir, Rel(-1, -6)); GiveBall(Tara);
                    Highlight = Veer; HighlightLabel = Pad ? "LT + A" : "PRESS 5";
                    break;
                case 4:
                    Title = "THROUGH BALL"; Body = "Tara is making a run. Play a through ball into the space ahead of her, so she runs onto it.";
                    Keys = new[] { K("U", "LB") };
                    Arrange(Veer, Vector3.zero); Put(Veer, Rel(3, -3)); Put(Tara, Rel(5, 7)); GiveBall(Veer);
                    runner = Tara; runnerGoal = Rel(15, 4);
                    Highlight = Tara; HighlightLabel = "RUNNING";
                    break;
                case 5:
                    Title = "SHOOT"; Body = "Hold Shoot to power up and let go to shoot. Hold W or S to pick the top or bottom corner. Don't overcharge past the white mark. Score a goal!";
                    Keys = new[] { K("Hold K, release", "Hold B, release"), K("W / S aim", "Stick aim") };
                    Arrange(Tara, Vector3.zero); Put(Tara, Rel(11, 3)); GiveBall(Tara);
                    Away.Skill = Skill.For(Level.Easy);
                    break;
                case 6:
                    Title = "TACKLE"; Body = "Rex has the ball! Get close and press Tackle to steal it, or press Slide to dive in from a few metres.";
                    Keys = new[] { K("J tackle", "A tackle"), K("K slide", "B slide") };
                    Arrange(Bhola, Vector3.zero); Put(Bhola, Rel(-7, 0)); Put(Rex, Rel(3, 1)); GiveBall(Rex);
                    actor = Rex;
                    Highlight = Rex; HighlightLabel = "WIN THE BALL";
                    break;
                case 7:
                    Title = "SPECIAL: AGNI STRIKE"; Body = "Passes, tackles and saves fill a player's Chi bar (over their head). Veer's is full now. Press Special for his fire comet!";
                    Keys = new[] { K("SPACE", "Y or RB") };
                    Arrange(Veer, Vector3.zero); Put(Veer, Rel(8, 1)); GiveBall(Veer);
                    Veer.Chi = 1f; Away.Keeper.Chi = 0;
                    Away.Skill = Skill.For(Level.Easy);
                    break;
                case 8:
                    Title = "SPECIAL: AKHADA WALL"; Body = "Defending specials work without the ball. Bhola's Chi is full: get near Rex and press Special to flatten the attack.";
                    Keys = new[] { K("SPACE", "Y or RB") };
                    Arrange(Bhola, Vector3.zero); Put(Bhola, Rel(-9, 0)); Put(Rex, Rel(-1, 0)); GiveBall(Rex);
                    Bhola.Chi = 1f; actor = Rex;
                    Highlight = Rex; HighlightLabel = "GET CLOSE";
                    break;
                default:
                    Title = "TUTORIAL COMPLETE!"; Body = "You know the basics. Now beat Titan FC. You can pick the difficulty on the title screen.";
                    Keys = new[] { K("ENTER: play vs CPU", "A: play vs CPU"), "ESC: menu" };
                    Arrange(Kabir, Vector3.zero);
                    PlayerPrefs.SetInt(PrefKey, 1); PlayerPrefs.Save();
                    foreach (var p in Home.Players) p.SetState(PState.Celebrate, 99);
                    m.Arena.Excite = 1f;
                    Sfx.Play(Sfx.Roar, 0.8f);
                    break;
            }
            if (ring) ring.SetActive(target.HasValue);
        }

        // ------------------------------------------------------------------ per frame
        public void Update(float dt)
        {
            StepT += Time.unscaledDeltaTime;
            if (ring && target.HasValue)
            {
                ring.transform.position = target.Value + Vector3.up * 0.03f;
                float pulse = 1 + Mathf.Sin(Time.unscaledTime * 6) * 0.08f;
                ring.transform.localScale = new Vector3(pulse, 1, pulse);
            }

            if (Done)
            {
                if (m.P1.Confirm && StepT > 0.8f) { End(); m.StartMode(GameMode.VsCpu); }
                return;
            }

            if (SuccessT >= 0)
            {
                if (m.Specials.Running) return; // let the special play out first
                SuccessT += Time.unscaledDeltaTime;
                if (SuccessT > (Step == 7 ? 1.8f : 1.4f)) Next();
                return;
            }

            if (m.P1.SkipDown && StepT > 0.4f && !m.Specials.Running) { Next(); return; }

            var me = Controlled();
            if (me != null && me.WishSprint && Util.Flat(me.Vel).magnitude > 6.5f) sprintTime += dt;

            // scripted players
            if (runner != null && m.Ball.Receiver != runner && m.Ball.Owner != runner)
            {
                var d = Util.Flat(runnerGoal - runner.Pos);
                runner.Wish = d.magnitude > 0.5f ? d.normalized * 0.75f : Vector3.zero;
            }
            if (actor != null && m.Ball.Owner == actor && actor.CanAct)
            {
                var goal = new Vector3(-Home.Side * Pitch.HalfL, 0, 0);
                var d = Util.Flat(goal - actor.Pos);
                actor.Wish = d.normalized * 0.5f;
                actor.WishSprint = false;
                // reached the box: try again
                if (Mathf.Abs(actor.Pos.x - goal.x) < 9f) { resetT += dt; if (resetT > 0.3f) Retry(); }
            }

            if (Check()) Success();
        }

        Player Controlled()
        {
            foreach (var p in Home.Players) if (p.Human == m.P1) return p;
            return null;
        }

        bool Check()
        {
            var b = m.Ball; var me = Controlled();
            switch (Step)
            {
                case 0: return me != null && target.HasValue && Util.FlatDist(me.Pos, target.Value) < 1.3f;
                case 1: return me != null && target.HasValue && Util.FlatDist(me.Pos, target.Value) < 1.3f && sprintTime > 0.3f;
                case 2: return b.Owner != null && b.Owner.Team == Home && b.Owner != Kabir && !b.Owner.IsKeeper;
                case 3: return b.Owner == Veer;
                case 4:
                    if (b.Owner != null && b.Owner != Tara && b.Owner.Team == Home && b.Owner != Veer) { Retry(); return false; }
                    return b.Owner == Tara && m.LastPassThrough;
                case 5:
                    if (b.ShotTeam == Home) shotTaken = true;
                    if (shotTaken && (b.Owner == Away.Keeper || (b.Loose && b.ShotTeam == null && b.Speed < 4f && !b.InNet)))
                    {
                        resetT += Time.deltaTime;
                        if (resetT > 1f) { Tries++; m.ShowToast(Tries >= 2 ? "Close! Try again (Tab skips)" : "Saved! Try again"); Retry(); }
                    }
                    return false; // success comes from OnGoal
                case 6: return b.Owner != null && b.Owner.Team == Home;
                case 7: return false; // OnSpecial / OnGoal
                case 8: return specialSeen;
            }
            return false;
        }

        void Retry()
        {
            int t = Tries;
            Setup();
            Tries = t;
        }

        void Success()
        {
            if (SuccessT >= 0) return;
            SuccessT = 0;
            if (ring) ring.SetActive(false);
            Sfx.Play(Sfx.Chime, 0.9f, 1.2f);
            m.Arena.Excite = Mathf.Max(m.Arena.Excite, 0.5f);
        }

        // ------------------------------------------------------------------ events
        public void OnGoal(Team t)
        {
            if (Done || SuccessT >= 0) return;
            if (t == Home && (Step == 5 || Step == 7)) Success();
            else Retry(); // a goal that isn't the drill: put everything back
        }

        public void OnSpecial(Player p)
        {
            if (Step == 7 && p == Veer) { specialSeen = true; Success(); }
            if (Step == 8 && p == Bhola) specialSeen = true;
        }

        /// <summary>Which non-human players the normal CPU brain may drive during a drill.</summary>
        public bool UsesBrain(Player p)
        {
            if (p.IsKeeper) return Step == 5 || Step == 7;
            if (p == runner && m.Ball.Receiver != runner) return false;
            if (p == actor) return m.Ball.Owner != actor; // after losing the ball, chase like normal
            return m.Ball.Receiver == p;
        }
    }
}
