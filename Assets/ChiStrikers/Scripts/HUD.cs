using System.Collections.Generic;
using UnityEngine;

namespace ChiStrikers
{
    /// <summary>All UI in IMGUI, styled after the reel: navy plates, yellow titles, bold italic kinetic type for specials.</summary>
    public sealed class HUD : MonoBehaviour
    {
        public Match M;
        static readonly Color Navy = Util.Hex("#0c1a33"), Yellow = Util.Hex("#ffc93c"), Orange = Util.Hex("#f28a1e"), Ink = Util.Hex("#1b1b22"), Sky = Util.Hex("#5bb8f0");
        readonly Dictionary<Color, Texture2D> texes = new Dictionary<Color, Texture2D>();
        GUIStyle style;
        float s; // UI scale
        float specialSeen = -1;

        Texture2D Tex(Color c)
        {
            if (texes.TryGetValue(c, out var t) && t) return t;
            t = new Texture2D(1, 1); t.SetPixel(0, 0, c); t.Apply();
            texes[c] = t;
            return t;
        }

        void Box(Rect r, Color c) { GUI.DrawTexture(r, Tex(c)); }

        void Text(Rect r, string text, int size, Color c, TextAnchor a = TextAnchor.MiddleCenter, FontStyle fs = FontStyle.Bold, float outline = 0, Color? outlineC = null)
        {
            style.fontSize = Mathf.Max(8, (int)(size * s));
            style.alignment = a;
            style.fontStyle = fs;
            if (outline > 0)
            {
                style.normal.textColor = outlineC ?? Ink;
                float o = outline * s;
                for (int i = 0; i < 8; i++)
                {
                    float ang = i * Mathf.PI / 4;
                    GUI.Label(new Rect(r.x + Mathf.Cos(ang) * o, r.y + Mathf.Sin(ang) * o, r.width, r.height), text, style);
                }
            }
            style.normal.textColor = c;
            GUI.Label(r, text, style);
        }

        bool Clicked(Rect r)
        {
            var e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0 && r.Contains(e.mousePosition)) { e.Use(); return true; }
            return false;
        }

        void OnGUI()
        {
            if (M == null) return;
            if ((M.ShowHelp || M.State == MatchState.FullTime) && Event.current.type == EventType.MouseDown) M.ClickContinue();
            if (style == null)
            {
                style = new GUIStyle(GUI.skin.label) { wordWrap = false, richText = false };
                style.font = Util.Font;
                style.clipping = TextClipping.Overflow;
            }
            s = Screen.height / 1080f;
            float W = Screen.width, H = Screen.height;

            if (M.Cinematic) Cinematic(W, H);

            if (M.State == MatchState.Title) { Title(W, H); Flash(W, H); return; }

            Scoreboard(W);
            ChiCards(W, H);
            Markers();
            Banners(W, H);
            SpecialTitle(W, H);
            if (M.ToastT > 0 && !string.IsNullOrEmpty(M.Toast))
            {
                var r = new Rect(W * 0.5f - 400 * s, H - 230 * s, 800 * s, 50 * s);
                Text(r, M.Toast, 30, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold, 2.5f);
            }
            if (M.Paused) Pause(W, H);
            Flash(W, H);
        }

        void Flash(float W, float H)
        {
            if (M.Fx && M.Fx.FlashA > 0.01f) Box(new Rect(0, 0, W, H), new Color(1, 1, 1, M.Fx.FlashA));
        }

        // ---------------------------------------------------------------- in-game
        void Scoreboard(float W)
        {
            float w = 620 * s, h = 76 * s, x = W * 0.5f - w * 0.5f, y = 18 * s;
            Box(new Rect(x, y, w, h), new Color(Navy.r, Navy.g, Navy.b, 0.92f));
            Box(new Rect(x, y + h - 5 * s, w, 5 * s), Yellow);
            var home = M.Home; var away = M.Away;
            Box(new Rect(x + 14 * s, y + 16 * s, 12 * s, 44 * s), home.Def.Primary);
            Box(new Rect(x + w - 26 * s, y + 16 * s, 12 * s, 44 * s), away.Def.Ui);
            Text(new Rect(x + 34 * s, y, 170 * s, h), home.Def.Name.ToUpper(), 22, Color.white, TextAnchor.MiddleLeft);
            Text(new Rect(x + w - 204 * s, y, 170 * s, h), away.Def.Name.ToUpper(), 22, Color.white, TextAnchor.MiddleRight);
            Text(new Rect(x + w * 0.5f - 90 * s, y - 6 * s, 180 * s, h), $"{home.Score}  -  {away.Score}", 46, Yellow, TextAnchor.MiddleCenter, FontStyle.Bold);
            string half = M.Golden ? "GOLDEN GOAL" : (M.Half == 1 ? "1ST HALF  " : "2ND HALF  ") + M.ClockText();
            Text(new Rect(x + w * 0.5f - 150 * s, y + h - 4 * s, 300 * s, 32 * s), half, 20, Sky);
        }

        void ChiCards(float W, float H)
        {
            Cards(M.Home, 20 * s, H, false);
            Cards(M.Away, W - 20 * s, H, true);
        }

        void Cards(Team t, float edge, float H, bool right)
        {
            float cw = 168 * s, ch = 74 * s, gap = 8 * s;
            float total = 5 * cw + 4 * gap;
            bool human = t.Humans.Count > 0;
            float scale = human ? 1f : 0.78f;
            cw *= scale; ch *= scale; gap *= scale; total *= scale;
            float x0 = right ? edge - total : edge;
            float y = H - ch - 18 * s;
            for (int i = 0; i < t.Players.Count; i++)
            {
                var p = t.Players[i];
                float x = x0 + i * (cw + gap);
                bool ready = p.Chi >= 1 && p.Def.Special != SpecialType.KeeperSave;
                bool keeperReady = p.Chi >= 1 && p.Def.Special == SpecialType.KeeperSave;
                bool pulse = (ready || keeperReady) && Mathf.Repeat(Time.unscaledTime * 2.5f, 1f) < 0.5f;
                Box(new Rect(x, y, cw, ch), new Color(Navy.r, Navy.g, Navy.b, 0.9f));
                Box(new Rect(x, y, 6 * s * scale, ch), t.Def.Primary == Util.Hex("#26324f") ? t.Def.Ui : t.Def.Primary);
                if (p.Human != null) Box(new Rect(x, y - 5 * s, cw, 5 * s), p.Human.Slot == 0 ? Yellow : Sky);
                Text(new Rect(x + 12 * s * scale, y + 4 * s * scale, cw, 26 * s * scale), $"{p.Def.Look.Number}  {p.Def.Name.ToUpper()}", (int)(19 * scale), Color.white, TextAnchor.MiddleLeft);
                Text(new Rect(x + 12 * s * scale, y + 26 * s * scale, cw, 20 * s * scale), p.Def.SpecialName, (int)(13 * scale), pulse ? Color.white : p.Def.FxB, TextAnchor.MiddleLeft);
                // chi bar
                var bar = new Rect(x + 12 * s * scale, y + ch - 20 * s * scale, cw - 24 * s * scale, 10 * s * scale);
                Box(bar, new Color(0, 0, 0, 0.6f));
                Box(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(p.Chi), bar.height), pulse ? Color.white : Color.Lerp(p.Def.FxB, p.Def.FxA, p.Chi >= 1 ? 1 : 0));
                if (ready && p.Human != null)
                    Text(new Rect(x, y - 34 * s, cw, 28 * s), $"PRESS {p.Human.KeySpecial.ToUpper()}!", 16, Yellow, TextAnchor.MiddleCenter, FontStyle.Bold, 2);
                else if (ready && human)
                    Text(new Rect(x, y - 30 * s, cw, 26 * s), "READY", 15, Yellow, TextAnchor.MiddleCenter, FontStyle.Bold, 2);
            }
        }

        void Markers()
        {
            var cam = M.Cam ? M.Cam.Cam : null;
            if (!cam || M.Cinematic) return;
            foreach (var p in M.AllPlayers())
            {
                if (p.Human == null) continue;
                var sp = cam.WorldToScreenPoint(p.Pos + Vector3.up * (2.25f * p.Def.Look.Height + p.Lift));
                if (sp.z < 0) continue;
                float x = sp.x, y = Screen.height - sp.y;
                var col = p.Human.Slot == 0 ? Yellow : Sky;
                Text(new Rect(x - 60 * s, y - 40 * s, 120 * s, 30 * s), (p.Human.Slot == 0 ? "P1 " : "P2 ") + p.Def.Name.ToUpper(), 18, col, TextAnchor.MiddleCenter, FontStyle.Bold, 2);
                Box(new Rect(x - 5 * s, y - 12 * s, 10 * s, 10 * s), col);
                if (p.Charge >= 0)
                {
                    var r = new Rect(x - 50 * s, y + 10 * s, 100 * s, 12 * s);
                    Box(new Rect(r.x - 2 * s, r.y - 2 * s, r.width + 4 * s, r.height + 4 * s), Ink);
                    float k = Mathf.Clamp01(p.Charge / 1.15f);
                    Box(new Rect(r.x, r.y, r.width * k, r.height), p.Charge > 1f ? Util.Hex("#e0352b") : Color.Lerp(Yellow, Orange, k));
                    Box(new Rect(r.x + r.width * (1f / 1.15f), r.y - 3 * s, 2 * s, r.height + 6 * s), Color.white);
                }
            }
        }

        void Banners(float W, float H)
        {
            switch (M.State)
            {
                case MatchState.Kickoff:
                    Big(W, H, M.Golden ? "GOLDEN GOAL" : "KICK OFF", Yellow, M.StateT, M.Golden ? "Next goal wins" : null);
                    break;
                case MatchState.Goal:
                {
                    var t = M.LastScoringTeam;
                    string sub = M.LastScorer != null ? M.LastScorer.Def.Name.ToUpper() + (M.LastWasSpecial ? "  ·  SPECIAL MOVE GOAL" : "") : t.Def.Name.ToUpper();
                    Big(W, H, "GOAL!", t.Def.Primary == Util.Hex("#26324f") ? t.Def.Ui : t.Def.Primary, M.StateT, sub);
                    break;
                }
                case MatchState.HalfTime:
                    Big(W, H, "HALF TIME", Yellow, M.StateT, $"{M.Home.Def.Name}  {M.Home.Score} - {M.Away.Score}  {M.Away.Def.Name}");
                    break;
                case MatchState.FullTime:
                {
                    var win = M.Home.Score > M.Away.Score ? M.Home : M.Away;
                    Box(new Rect(0, H * 0.3f, W, H * 0.4f), new Color(Navy.r, Navy.g, Navy.b, 0.85f));
                    Text(new Rect(0, H * 0.33f, W, 120 * s), "FULL TIME", 96, Yellow, TextAnchor.MiddleCenter, FontStyle.BoldAndItalic, 5);
                    Text(new Rect(0, H * 0.33f + 110 * s, W, 70 * s), $"{M.Home.Def.Name.ToUpper()}  {M.Home.Score} - {M.Away.Score}  {M.Away.Def.Name.ToUpper()}", 46, Color.white);
                    Text(new Rect(0, H * 0.33f + 180 * s, W, 50 * s), win.Def.Name.ToUpper() + " WIN!", 36, win.Def.Primary == Util.Hex("#26324f") ? win.Def.Ui : win.Def.Primary, TextAnchor.MiddleCenter, FontStyle.Bold, 2);
                    if (M.StateT > 1.5f) Text(new Rect(0, H * 0.33f + 240 * s, W, 40 * s), "Press Enter / A to continue", 24, Sky);
                    break;
                }
            }
        }

        /// <summary>Bold italic kinetic title that punches in toward the camera.</summary>
        void Big(float W, float H, string text, Color c, float t, string sub)
        {
            float k = Mathf.Clamp01(t / 0.22f);
            float scale = Mathf.Lerp(2.6f, 1f, 1 - (1 - k) * (1 - k));
            float alpha = Mathf.Clamp01(t / 0.1f);
            var m = GUI.matrix;
            GUIUtility.ScaleAroundPivot(new Vector2(scale, scale), new Vector2(W * 0.5f, H * 0.42f));
            var col = c; col.a = alpha;
            Text(new Rect(0, H * 0.42f - 80 * s, W, 160 * s), text, 130, col, TextAnchor.MiddleCenter, FontStyle.BoldAndItalic, 6, new Color(Ink.r, Ink.g, Ink.b, alpha));
            GUI.matrix = m;
            if (sub != null && t > 0.2f) Text(new Rect(0, H * 0.42f + 70 * s, W, 60 * s), sub, 34, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold, 3);
        }

        void Cinematic(float W, float H)
        {
            // letterbox + speed lines
            float bar = H * 0.09f;
            Box(new Rect(0, 0, W, bar), Ink);
            Box(new Rect(0, H - bar, W, bar), Ink);
            var rng = new System.Random((int)(Time.unscaledTime * 24));
            var c = new Color(1, 1, 1, 0.55f);
            var center = new Vector2(W * 0.5f, H * 0.5f);
            for (int i = 0; i < 26; i++)
            {
                float ang = (float)rng.NextDouble() * 360f;
                float r0 = (float)(0.36 + rng.NextDouble() * 0.25) * W;
                float len = (float)(0.15 + rng.NextDouble() * 0.3) * W;
                var m = GUI.matrix;
                GUIUtility.RotateAroundPivot(ang, center);
                Box(new Rect(center.x + r0, center.y - 1.5f * s, len, 3 * s), c);
                GUI.matrix = m;
            }
        }

        void SpecialTitle(float W, float H)
        {
            var sp = M.Specials;
            if (sp == null || sp.TitleT < 0 || string.IsNullOrEmpty(sp.Title)) return;
            sp.TitleT += Time.unscaledDeltaTime;
            float t = sp.TitleT;
            if (t > 1.8f) { sp.TitleT = -1; return; }
            float k = Mathf.Clamp01(t / 0.18f);
            float scale = Mathf.Lerp(3f, 1f, 1 - (1 - k) * (1 - k));
            float slide = t > 1.4f ? (t - 1.4f) / 0.4f : 0;
            float alpha = 1 - slide;
            var m = GUI.matrix;
            float cy = H * 0.72f;
            GUIUtility.ScaleAroundPivot(new Vector2(scale, scale), new Vector2(W * 0.5f, cy));
            var col = sp.TitleColor; col.a = alpha;
            Box(new Rect(-10, cy - 60 * s, W + 20, 120 * s), new Color(Navy.r, Navy.g, Navy.b, 0.75f * alpha));
            Text(new Rect(slide * W * 0.3f, cy - 70 * s, W, 110 * s), sp.Title, 96, col, TextAnchor.MiddleCenter, FontStyle.BoldAndItalic, 5, new Color(1, 1, 1, alpha));
            GUI.matrix = m;
            Text(new Rect(-slide * W * 0.3f, cy + 34 * s, W, 40 * s), sp.Sub, 26, new Color(Yellow.r, Yellow.g, Yellow.b, alpha), TextAnchor.MiddleCenter, FontStyle.Bold, 2);
        }

        // ---------------------------------------------------------------- menus
        void Logo(float W, float y, float size)
        {
            Text(new Rect(6 * s, y + 8 * s, W, size * 1.4f * s), "CHI STRIKERS", (int)size, Orange, TextAnchor.MiddleCenter, FontStyle.BoldAndItalic, 7);
            Text(new Rect(0, y, W, size * 1.4f * s), "CHI STRIKERS", (int)size, Util.Hex("#ffd84a"), TextAnchor.MiddleCenter, FontStyle.BoldAndItalic, 6);
        }

        void Title(float W, float H)
        {
            Box(new Rect(0, 0, W, H), new Color(Navy.r, Navy.g, Navy.b, 0.45f));
            Logo(W, H * 0.12f, 150);
            Text(new Rect(0, H * 0.12f + 200 * s, W, 50 * s), "SUPERPOWER STREET FOOTBALL", 34, Sky, TextAnchor.MiddleCenter, FontStyle.Bold, 3);
            Text(new Rect(0, H * 0.12f + 245 * s, W, 40 * s), "Mohalla United  vs  Titan FC", 26, Color.white, TextAnchor.MiddleCenter, FontStyle.Italic, 2);

            if (M.ShowHelp) { Help(W, H); return; }

            string[] items = { "PLAY vs CPU", "2 PLAYERS: VERSUS", "2 PLAYERS: CO-OP vs CPU", "HOW TO PLAY" };
            float iw = 560 * s, ih = 70 * s, y0 = H * 0.5f;
            for (int i = 0; i < items.Length; i++)
            {
                bool sel = i == M.MenuIndex;
                var r = new Rect(W * 0.5f - iw * 0.5f, y0 + i * (ih + 14 * s), iw, ih);
                if (Clicked(r)) M.ClickTitle(i);
                if (r.Contains(Event.current.mousePosition) && Event.current.type == EventType.MouseMove) M.MenuIndex = i;
                Box(r, sel ? Yellow : new Color(Navy.r, Navy.g, Navy.b, 0.9f));
                if (sel) Box(new Rect(r.x, r.yMax - 6 * s, r.width, 6 * s), Orange);
                Text(r, items[i], 32, sel ? Navy : Color.white, TextAnchor.MiddleCenter, FontStyle.BoldAndItalic);
            }
            Text(new Rect(0, H - 70 * s, W, 40 * s), "Click, or W / S and Enter, or D-pad and A  ·  click the Game view first so it gets the keyboard", 22, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold, 2);
            Text(new Rect(10 * s, H - 34 * s, 600 * s, 30 * s), "Input: " + Controls.Status, 16, Controls.Status == "NO INPUT" ? Util.Hex("#e0352b") : Sky, TextAnchor.MiddleLeft, FontStyle.Normal);
        }

        void Help(float W, float H)
        {
            float w = 1500 * s, h = 560 * s;
            var r = new Rect(W * 0.5f - w * 0.5f, H * 0.42f, w, h);
            Box(r, new Color(Navy.r, Navy.g, Navy.b, 0.95f));
            Box(new Rect(r.x, r.y, r.width, 6 * s), Yellow);
            string[,] rows =
            {
                { "", "PLAYER 1 KEYBOARD", "GAMEPAD", "PLAYER 2 KEYBOARD" },
                { "Move", "W A S D", "Left stick", "Arrow keys" },
                { "Sprint", "Shift", "RT", "Right Shift" },
                { "Pass  /  Tackle", "J", "A", "Num 1  or  ," },
                { "Shoot (hold for power)  /  Slide tackle", "K", "B", "Num 2  or  ." },
                { "Lob  /  Switch player", "L", "X  (LB switch)", "Num 3  or  /" },
                { "SPECIAL MOVE (full Chi)", "Space", "Y or RB", "Num 0  or  Right Ctrl" },
                { "Pause", "Esc", "Start", "" },
            };
            float cx0 = r.x + 30 * s;
            float[] cols = { 0, 560 * s, 860 * s, 1130 * s };
            for (int i = 0; i < rows.GetLength(0); i++)
                for (int j = 0; j < 4; j++)
                    Text(new Rect(cx0 + cols[j], r.y + 24 * s + i * 46 * s, 400 * s, 40 * s), rows[i, j], i == 0 ? 20 : 24, i == 0 ? Yellow : (j == 0 ? Color.white : Sky), TextAnchor.MiddleLeft, i == 0 ? FontStyle.Bold : FontStyle.Normal);
            Text(new Rect(r.x + 30 * s, r.y + 24 * s + 8 * 46 * s, w - 60 * s, 90 * s),
                "Chi fills from passes, tackles, interceptions and saves. With a full bar, press Special with the ball: Veer's Agni Strike, Tara's Sky Step and Kabir's Chakra Strike are shots. Bhola's Akhada Wall works anytime and blocks any shot. Guru Dada's Thousand Hands saves a special by itself. Only specials beat specials.",
                20, Color.white, TextAnchor.UpperLeft, FontStyle.Italic);
            style.wordWrap = false;
            Text(new Rect(0, r.yMax + 12 * s, W, 36 * s), "Press Enter / A to go back", 22, Yellow, TextAnchor.MiddleCenter, FontStyle.Bold, 2);
        }

        void Pause(float W, float H)
        {
            Box(new Rect(0, 0, W, H), new Color(Navy.r, Navy.g, Navy.b, 0.7f));
            Logo(W, H * 0.14f, 90);
            string[] items = { "RESUME", "RESTART MATCH", "QUIT TO TITLE" };
            float iw = 480 * s, ih = 64 * s, y0 = H * 0.4f;
            for (int i = 0; i < items.Length; i++)
            {
                bool sel = i == M.PauseIndex;
                var r = new Rect(W * 0.5f - iw * 0.5f, y0 + i * (ih + 12 * s), iw, ih);
                if (Clicked(r)) M.ClickPause(i);
                Box(r, sel ? Yellow : Navy);
                Text(r, items[i], 30, sel ? Navy : Color.white, TextAnchor.MiddleCenter, FontStyle.BoldAndItalic);
            }
            Text(new Rect(0, H * 0.72f, W, 40 * s), "J / K pass & shoot  ·  L lob or switch  ·  Space special  ·  Shift sprint", 22, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold, 2);
        }
    }
}
