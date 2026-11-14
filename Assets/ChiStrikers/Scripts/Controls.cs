using UnityEngine;
using UnityEngine.InputSystem;

namespace ChiStrikers
{
    /// <summary>
    /// One human player's buttons. Player 1 = WASD keyboard or the first gamepad; Player 2 = arrow keys or the second gamepad.
    ///   With the ball:    Pass · Shoot (hold for power) · Lob / through ball · Special (full Chi)
    ///   Without the ball: Tackle · Slide tackle · Switch player · Special (defensive moves)
    /// </summary>
    public sealed class Controls
    {
        public readonly int Slot; // 0 = player 1, 1 = player 2
        public Controls(int slot) { Slot = slot; }

        static Keyboard K => Keyboard.current;
        Gamepad Pad => Gamepad.all.Count > Slot ? Gamepad.all[Slot] : null;

        // If the new Input System backend isn't enabled (Keyboard.current is null), fall back to the old Input Manager.
        static bool Held(Key k) => K != null ? K[k].isPressed : Legacy.Held(k);
        static bool Down(Key k) => K != null ? K[k].wasPressedThisFrame : Legacy.Down(k);
        bool LegacyPad(int button, bool down) => K == null && Gamepad.all.Count == 0 && Legacy.Joy(Slot, button, down);

        public string KeyPass => Slot == 0 ? "J" : "Num1 / ,";
        public string KeyShoot => Slot == 0 ? "K" : "Num2 / .";
        public string KeyLob => Slot == 0 ? "L" : "Num3 / /";
        public string KeySpecial => Slot == 0 ? "Space" : "Num0 / Right Ctrl";
        public string KeySprint => Slot == 0 ? "Shift" : "Right Shift";

        public Vector2 Move
        {
            get
            {
                Vector2 v;
                if (Slot == 0)
                    v = new Vector2((Held(Key.D) ? 1 : 0) - (Held(Key.A) ? 1 : 0), (Held(Key.W) ? 1 : 0) - (Held(Key.S) ? 1 : 0));
                else
                    v = new Vector2((Held(Key.RightArrow) ? 1 : 0) - (Held(Key.LeftArrow) ? 1 : 0), (Held(Key.UpArrow) ? 1 : 0) - (Held(Key.DownArrow) ? 1 : 0));
                var p = Pad;
                if (p != null)
                {
                    var s = p.leftStick.ReadValue() + p.dpad.ReadValue();
                    if (s.magnitude > 0.2f) v += s;
                }
                else if (K == null && Gamepad.all.Count == 0) v += Legacy.Stick(Slot);
                return Vector2.ClampMagnitude(v, 1f);
            }
        }

        public bool Sprint => (Slot == 0 ? Held(Key.LeftShift) : Held(Key.RightShift)) || (Pad != null && (Pad.rightTrigger.isPressed || Pad.leftStickButton.isPressed));

        public bool PassDown => (Slot == 0 ? Down(Key.J) : Down(Key.Numpad1) || Down(Key.Comma)) || (Pad != null && Pad.buttonSouth.wasPressedThisFrame) || LegacyPad(0, true);
        public bool ShootDown => (Slot == 0 ? Down(Key.K) : Down(Key.Numpad2) || Down(Key.Period)) || (Pad != null && Pad.buttonEast.wasPressedThisFrame) || LegacyPad(1, true);
        public bool ShootHeld => (Slot == 0 ? Held(Key.K) : Held(Key.Numpad2) || Held(Key.Period)) || (Pad != null && Pad.buttonEast.isPressed) || LegacyPad(1, false);
        public bool LobDown => (Slot == 0 ? Down(Key.L) : Down(Key.Numpad3) || Down(Key.Slash)) || (Pad != null && Pad.buttonWest.wasPressedThisFrame) || LegacyPad(2, true);
        public bool SpecialDown => (Slot == 0 ? Down(Key.Space) : Down(Key.Numpad0) || Down(Key.RightCtrl)) || (Pad != null && (Pad.buttonNorth.wasPressedThisFrame || Pad.rightShoulder.wasPressedThisFrame)) || LegacyPad(3, true);
        public bool SwitchDown => LobDown || (Pad != null && Pad.leftShoulder.wasPressedThisFrame);

        public bool Confirm => (Slot == 0 ? Down(Key.Enter) || Down(Key.Space) || Down(Key.J) || Down(Key.NumpadEnter) : Down(Key.Numpad1))
                               || (Pad != null && (Pad.buttonSouth.wasPressedThisFrame || Pad.startButton.wasPressedThisFrame)) || LegacyPad(0, true) || LegacyPad(7, true);
        public bool Back => (Slot == 0 ? Down(Key.Backspace) : false) || (Pad != null && Pad.buttonEast.wasPressedThisFrame);

        float lastNavY, lastNavX;
        public int NavY()
        {
            float y = Move.y; int r = 0;
            if (y > 0.6f && lastNavY <= 0.6f) r = 1;
            if (y < -0.6f && lastNavY >= -0.6f) r = -1;
            lastNavY = y;
            return r;
        }
        public int NavX()
        {
            float x = Move.x; int r = 0;
            if (x > 0.6f && lastNavX <= 0.6f) r = 1;
            if (x < -0.6f && lastNavX >= -0.6f) r = -1;
            lastNavX = x;
            return r;
        }

        public static bool PausePressed()
        {
            foreach (var g in Gamepad.all) if (g.startButton.wasPressedThisFrame) return true;
            if (K == null) return Legacy.Down(Key.Escape) || Legacy.Down(Key.P);
            return K.escapeKey.wasPressedThisFrame || K.pKey.wasPressedThisFrame;
        }

        public static string Status => K != null ? "Input System" : Legacy.Works ? "Input Manager (old)" : "NO INPUT";
    }

    /// <summary>Old Input Manager fallback. Every call is guarded: it throws when the project is set to the new Input System only.</summary>
    static class Legacy
    {
        static bool broken;
        public static bool Works => !broken;

        static KeyCode Map(Key k)
        {
            switch (k)
            {
                case Key.Space: return KeyCode.Space; case Key.Enter: return KeyCode.Return; case Key.NumpadEnter: return KeyCode.KeypadEnter;
                case Key.Tab: return KeyCode.Tab; case Key.Backspace: return KeyCode.Backspace; case Key.Escape: return KeyCode.Escape;
                case Key.LeftShift: return KeyCode.LeftShift; case Key.RightShift: return KeyCode.RightShift;
                case Key.LeftCtrl: return KeyCode.LeftControl; case Key.RightCtrl: return KeyCode.RightControl;
                case Key.UpArrow: return KeyCode.UpArrow; case Key.DownArrow: return KeyCode.DownArrow;
                case Key.LeftArrow: return KeyCode.LeftArrow; case Key.RightArrow: return KeyCode.RightArrow;
                case Key.Comma: return KeyCode.Comma; case Key.Period: return KeyCode.Period; case Key.Slash: return KeyCode.Slash;
                case Key.Numpad0: return KeyCode.Keypad0; case Key.Numpad1: return KeyCode.Keypad1;
                case Key.Numpad2: return KeyCode.Keypad2; case Key.Numpad3: return KeyCode.Keypad3;
            }
            if (k >= Key.A && k <= Key.Z) return KeyCode.A + (k - Key.A);
            return KeyCode.None;
        }

        public static bool Held(Key k)
        {
            if (broken) return false;
            try { return Input.GetKey(Map(k)); } catch (System.Exception) { broken = true; return false; }
        }

        public static bool Down(Key k)
        {
            if (broken) return false;
            try { return Input.GetKeyDown(Map(k)); } catch (System.Exception) { broken = true; return false; }
        }

        /// <summary>Joystick button on the old Input Manager (0 = A, 1 = B, 2 = X, 3 = Y on an Xbox pad).</summary>
        public static bool Joy(int slot, int button, bool down)
        {
            if (broken) return false;
            var code = (slot == 0 ? KeyCode.Joystick1Button0 : KeyCode.Joystick2Button0) + button;
            try { return down ? Input.GetKeyDown(code) : Input.GetKey(code); } catch (System.Exception) { broken = true; return false; }
        }

        public static Vector2 Stick(int slot)
        {
            if (broken || slot != 0) return Vector2.zero;
            try
            {
                // the default Input Manager maps the first joystick to Horizontal / Vertical too (keyboard is read separately)
                var v = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
                return v.magnitude > 0.2f ? v : Vector2.zero;
            }
            catch (System.Exception) { return Vector2.zero; }
        }
    }
}
