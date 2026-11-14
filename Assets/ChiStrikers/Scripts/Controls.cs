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

        bool Held(Key k) => K != null && K[k].isPressed;
        bool Down(Key k) => K != null && K[k].wasPressedThisFrame;
        bool Up(Key k) => K != null && K[k].wasReleasedThisFrame;

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
                return Vector2.ClampMagnitude(v, 1f);
            }
        }

        public bool Sprint => (Slot == 0 ? Held(Key.LeftShift) : Held(Key.RightShift)) || (Pad != null && (Pad.rightTrigger.isPressed || Pad.leftStickButton.isPressed));

        public bool PassDown => (Slot == 0 ? Down(Key.J) : Down(Key.Numpad1) || Down(Key.Comma)) || (Pad != null && Pad.buttonSouth.wasPressedThisFrame);
        public bool ShootDown => (Slot == 0 ? Down(Key.K) : Down(Key.Numpad2) || Down(Key.Period)) || (Pad != null && Pad.buttonEast.wasPressedThisFrame);
        public bool ShootHeld => (Slot == 0 ? Held(Key.K) : Held(Key.Numpad2) || Held(Key.Period)) || (Pad != null && Pad.buttonEast.isPressed);
        public bool LobDown => (Slot == 0 ? Down(Key.L) : Down(Key.Numpad3) || Down(Key.Slash)) || (Pad != null && Pad.buttonWest.wasPressedThisFrame);
        public bool SpecialDown => (Slot == 0 ? Down(Key.Space) : Down(Key.Numpad0) || Down(Key.RightCtrl)) || (Pad != null && (Pad.buttonNorth.wasPressedThisFrame || Pad.rightShoulder.wasPressedThisFrame));
        public bool SwitchDown => LobDown || (Pad != null && Pad.leftShoulder.wasPressedThisFrame);

        public bool Confirm => (Slot == 0 ? Down(Key.Enter) || Down(Key.Space) || Down(Key.J) || Down(Key.NumpadEnter) : Down(Key.Numpad1))
                               || (Pad != null && (Pad.buttonSouth.wasPressedThisFrame || Pad.startButton.wasPressedThisFrame));
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
            return K != null && (K.escapeKey.wasPressedThisFrame || K.pKey.wasPressedThisFrame);
        }
    }
}
