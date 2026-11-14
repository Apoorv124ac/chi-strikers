using UnityEngine;

namespace ChiStrikers
{
    /// <summary>
    /// Side-on broadcast camera that pans horizontally with the play (like the reference reel), with a narrow lens
    /// so it reads almost orthographic. Specials swing it in close around the hero, then whip-pan after the ball.
    /// </summary>
    public sealed class CameraRig : MonoBehaviour
    {
        public Match M;
        public Camera Cam;
        enum Mode { Play, Focus, FollowBall, Frame }
        Mode mode;
        Transform focus; float focusDist, focusHeight, focusAngle, modeT, followLen;
        Vector3 framePos, frameLook;
        float shake;
        Vector3 pos, look;
        float fov;

        const float PlayFov = 28f;
        static readonly Vector3 PlayOffset = new Vector3(0, 13f, -27f);

        public static CameraRig Create(Match m)
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            var cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Util.Hex("#0c1a33");
            cam.fieldOfView = PlayFov;
            cam.nearClipPlane = 0.2f;
            cam.farClipPlane = 300f;
            cam.allowHDR = false;
            go.AddComponent<AudioListener>();
            var r = go.AddComponent<CameraRig>();
            r.M = m; r.Cam = cam;
            r.pos = PlayOffset; r.look = Vector3.zero; r.fov = PlayFov;
            return r;
        }

        public void Focus(Transform t, float dist, float height)
        {
            mode = Mode.Focus; focus = t; focusDist = dist; focusHeight = height; modeT = 0;
            // swing in from the camera's side, slightly in front of the hero
            var toCam = Util.Flat(transform.position - t.position).normalized;
            float a = Mathf.Atan2(toCam.x, toCam.z) * Mathf.Rad2Deg;
            focusAngle = a + Vector3.SignedAngle(Vector3.forward, Util.Flat(t.forward), Vector3.up) * 0f;
        }

        public void FollowBall(float seconds) { mode = Mode.FollowBall; modeT = 0; followLen = seconds; }

        public void FrameOn(Vector3 lookAt, Vector3 from) { mode = Mode.Frame; frameLook = lookAt; framePos = from; modeT = 0; }

        public void Release() { if (mode == Mode.Focus) mode = Mode.Play; }

        public void Shake(float a) { shake = Mathf.Max(shake, a); }

        public void Snap()
        {
            var bx = M && M.Ball ? M.Ball.Pos.x : 0;
            pos = new Vector3(Mathf.Clamp(bx, -Pitch.HalfL + 12f, Pitch.HalfL - 12f), 0, 0) + PlayOffset;
            look = new Vector3(pos.x, 0, -1f);
            mode = Mode.Play;
        }

        void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            modeT += dt;
            Vector3 wantPos, wantLook; float wantFov = PlayFov; float rate = 4f;

            switch (mode)
            {
                case Mode.Focus when focus:
                {
                    // slow orbit around the hero, low angle, close lens
                    focusAngle += dt * 22f;
                    var center = focus.position + Vector3.up * focusHeight;
                    var off = Quaternion.Euler(0, focusAngle, 0) * Vector3.forward * focusDist;
                    wantPos = center + off + Vector3.up * 0.35f;
                    wantLook = center;
                    wantFov = 38f;
                    rate = 7f;
                    break;
                }
                case Mode.FollowBall:
                {
                    var b = M.Ball.Pos;
                    var v = Util.Flat(M.Ball.Vel);
                    var dir = v.sqrMagnitude > 1 ? v.normalized : Vector3.right;
                    wantPos = b - dir * 6f + Vector3.up * 2.4f + new Vector3(0, 0, -5.5f);
                    wantLook = b + dir * 3f;
                    wantFov = 42f;
                    rate = 9f;
                    if (modeT > followLen) mode = Mode.Play;
                    break;
                }
                case Mode.Frame:
                    wantPos = framePos; wantLook = frameLook; wantFov = 34f; rate = 3f;
                    break;
                default:
                {
                    mode = Mode.Play;
                    float bx = M.Ball ? M.Ball.Pos.x : 0;
                    float lead = M.Ball ? M.Ball.Vel.x * 0.25f : 0;
                    float x = Mathf.Clamp(bx + lead, -Pitch.HalfL + 12f, Pitch.HalfL - 12f);
                    wantPos = new Vector3(x, 0, 0) + PlayOffset;
                    wantLook = new Vector3(x, 0, -1f);
                    rate = 3.2f;
                    break;
                }
            }

            pos = Util.Damp(pos, wantPos, rate, dt);
            look = Util.Damp(look, wantLook, rate * 1.4f, dt);
            fov = Util.Damp(fov, wantFov, rate, dt);

            var sh = Vector3.zero;
            if (shake > 0)
            {
                sh = Random.insideUnitSphere * shake * 0.6f;
                shake = Mathf.MoveTowards(shake, 0, dt * 1.6f);
            }
            transform.position = pos + sh;
            transform.rotation = Quaternion.LookRotation((look - pos).normalized);
            Cam.fieldOfView = fov;
        }
    }
}
