using System.Collections.Generic;
using UnityEngine;

namespace ChiStrikers
{
    /// <summary>
    /// All visual effects, made in code: glow and smoke particles, shockwave rings, floor cracks, after-images,
    /// the golden hands, ball auras (fire comet, chakram ring, villain smoke), screen flash and camera shake.
    /// </summary>
    public sealed class Fx : MonoBehaviour
    {
        public Match M;
        ParticleSystem glow, smoke, debris;
        public float FlashA;
        readonly List<Item> items = new List<Item>();
        Vector3 lastCrack = new Vector3(999, 0, 0);
        Transform auraQuad, auraRing;
        HeroDef auraDef; float auraScale;

        sealed class Item
        {
            public GameObject Go; public Material Mat; public float T, Dur; public bool Unscaled;
            public System.Action<Item, float> Step;
            public Color Color; public float A, B;
        }

        public void Init(Match m)
        {
            M = m;
            glow = MakeSystem("FxGlow", Util.Glow(Color.white), 0f);
            smoke = MakeSystem("FxSmoke", MakeSmokeMat(), -0.05f);
            debris = MakeSystem("FxDebris", MakeSmokeMat(), 1.6f, false);
        }

        static Material MakeSmokeMat()
        {
            var mat = new Material(Util.SpriteShader) { mainTexture = Util.SoftDot };
            return mat;
        }

        ParticleSystem MakeSystem(string name, Material mat, float gravity, bool fade = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.maxParticles = 6000;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startSpeed = 0;
            main.startLifetime = 1;
            main.gravityModifier = gravity;
            var em = ps.emission; em.enabled = false;
            var sh = ps.shape; sh.enabled = false;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                      fade ? new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0.8f, 0.5f), new GradientAlphaKey(0, 1) }
                           : new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, 0.8f), new GradientAlphaKey(0, 1) });
            col.color = new ParticleSystem.MinMaxGradient(g);
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, fade ? AnimationCurve.Linear(0, 1, 1, 0.2f) : AnimationCurve.Linear(0, 1, 1, 0.8f));
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.material = mat;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.sortingFudge = fade ? 0 : -10;
            ps.Play();
            return ps;
        }

        void Emit(ParticleSystem ps, Vector3 pos, Vector3 vel, float size, float life, Color c)
        {
            var e = new ParticleSystem.EmitParams { position = pos, velocity = vel, startSize = size, startLifetime = life, startColor = c, applyShapeToPosition = false };
            ps.Emit(e, 1);
        }

        // ------------------------------------------------------------------ public effects
        public void Burst(Vector3 pos, Color a, Color b, int n, float speed)
        {
            for (int i = 0; i < n; i++)
                Emit(glow, pos, Random.onUnitSphere * speed * Random.Range(0.3f, 1f), Random.Range(0.15f, 0.45f), Random.Range(0.25f, 0.6f), Color.Lerp(a, b, Random.value));
        }

        public void Aura(Vector3 pos, Color a, Color b, float scale)
        {
            for (int i = 0; i < 2; i++)
            {
                var o = Random.insideUnitCircle * 0.7f * scale;
                Emit(glow, pos + new Vector3(o.x, Random.Range(0, 0.4f), o.y), Vector3.up * Random.Range(2f, 4.5f) * scale, Random.Range(0.2f, 0.45f) * scale, Random.Range(0.35f, 0.7f), Color.Lerp(a, b, Random.value));
            }
        }

        /// <summary>Walls of fire rising around a striker gathering chi.</summary>
        public void FireWall(Vector3 pos, Color a, Color b)
        {
            for (int i = 0; i < 4; i++)
            {
                float ang = Random.value * Mathf.PI * 2;
                var p = pos + new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang)) * Random.Range(1.6f, 2.4f);
                Emit(glow, p, Vector3.up * Random.Range(4f, 8f), Random.Range(0.4f, 0.8f), Random.Range(0.3f, 0.6f), Color.Lerp(a, b, Random.value * 0.8f + 0.2f));
            }
        }

        public void Dust(Vector3 pos, int n)
        {
            for (int i = 0; i < n; i++)
            {
                var d = Random.insideUnitCircle;
                Emit(smoke, pos + new Vector3(d.x * 0.3f, 0.1f, d.y * 0.3f), new Vector3(d.x * 2, Random.Range(0.4f, 1.6f), d.y * 2), Random.Range(0.4f, 0.8f), Random.Range(0.4f, 0.8f), new Color(0.85f, 0.92f, 1f, 0.55f));
            }
        }

        public void Debris(Vector3 pos, int n, Color c)
        {
            var floor = Util.Hex("#1676cc");
            for (int i = 0; i < n; i++)
            {
                var d = Random.insideUnitCircle.normalized * Random.Range(2f, 7f);
                Emit(debris, pos + Vector3.up * 0.1f, new Vector3(d.x, Random.Range(4f, 10f), d.y), Random.Range(0.12f, 0.32f), Random.Range(0.8f, 1.4f), Random.value < 0.7f ? floor : c);
            }
            Dust(pos, n / 2);
        }

        public void Confetti(Vector3 pos, Color a, Color b)
        {
            for (int i = 0; i < 90; i++)
            {
                var d = Random.insideUnitCircle * 6f;
                Emit(debris, pos + new Vector3(d.x, Random.Range(4f, 8f), d.y), new Vector3(Random.Range(-2f, 2f), Random.Range(1f, 5f), Random.Range(-2f, 2f)), Random.Range(0.08f, 0.16f), Random.Range(1.6f, 2.6f), Random.value < 0.5f ? a : b);
            }
        }

        public void Flash(float a) { FlashA = Mathf.Max(FlashA, a); }
        public void Shake(float a) { if (M.Cam) M.Cam.Shake(a); }

        public void NetBulge(int side, Vector3 at) { if (M.Arena) M.Arena.Bulge(side); }

        /// <summary>An expanding ring on the floor (shockwaves, landings, chi steps).</summary>
        public void Ring(Vector3 pos, float r0, float r1, Color c, float dur)
        {
            var mat = Util.Fade(c);
            mat.renderQueue = 3060;
            var go = Util.MeshObj("Ring", Util.Ring(0.82f, 1f, 64), null, pos, mat);
            go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Add(new Item
            {
                Go = go, Mat = mat, Dur = dur, Color = c, A = r0, B = r1,
                Step = (it, k) =>
                {
                    float e = 1 - (1 - k) * (1 - k);
                    float r = Mathf.Lerp(it.A, it.B, e);
                    it.Go.transform.localScale = new Vector3(r, 1, r);
                    var col = it.Color; col.a = 1 - k; it.Mat.color = col;
                }
            });
        }

        /// <summary>Jagged dark cracks radiating from a point on the floor.</summary>
        public void RadialCracks(Vector3 pos, int n, float len)
        {
            for (int i = 0; i < n; i++)
            {
                float ang = (i + Random.value * 0.6f) / n * 360f;
                var p = pos;
                float segs = 3;
                for (int s = 0; s < segs; s++)
                {
                    float l = len / segs * Random.Range(0.7f, 1.3f);
                    float a = ang + Random.Range(-25f, 25f);
                    var dir = Quaternion.Euler(0, a, 0) * Vector3.forward;
                    CrackQuad(p + dir * l * 0.5f, dir, l, Mathf.Lerp(0.16f, 0.05f, s / segs), 6f);
                    p += dir * l;
                }
            }
        }

        void CrackQuad(Vector3 mid, Vector3 dir, float len, float width, float life)
        {
            var mat = Util.Fade(new Color(0.06f, 0.06f, 0.1f, 0.85f));
            mat.renderQueue = 3050;
            var go = Util.MeshObj("Crack", Util.GroundQuad(), null, new Vector3(mid.x, 0.015f, mid.z), mat,
                new Vector3(0, Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg, 0), new Vector3(width, 1, len));
            go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Add(new Item
            {
                Go = go, Mat = mat, Dur = life,
                Step = (it, k) => { var c = it.Mat.color; c.a = 0.85f * Mathf.Clamp01((1 - k) * 4); it.Mat.color = c; }
            });
        }

        /// <summary>The trench a fire comet rips along the floor.</summary>
        public void Crack(Vector3 pos, Vector3 vel, Color c)
        {
            if (Util.FlatDist(pos, lastCrack) < 0.7f) return;
            lastCrack = pos;
            var dir = Util.Flat(vel).normalized;
            if (dir == Vector3.zero) return;
            CrackQuad(pos, dir, 0.9f, 0.35f, 5f);
            CrackQuad(pos + Vector3.Cross(Vector3.up, dir) * 0.35f, Quaternion.Euler(0, 25, 0) * dir, 0.5f, 0.06f, 5f);
            CrackQuad(pos - Vector3.Cross(Vector3.up, dir) * 0.35f, Quaternion.Euler(0, -25, 0) * dir, 0.5f, 0.06f, 5f);
            for (int i = 0; i < 4; i++)
            {
                var side = Vector3.Cross(Vector3.up, dir) * (Random.value < 0.5f ? -1 : 1);
                Emit(debris, pos, side * Random.Range(1.5f, 4f) + Vector3.up * Random.Range(3f, 6f) + dir * 2, Random.Range(0.1f, 0.25f), Random.Range(0.6f, 1.1f), Util.Hex("#1676cc"));
            }
            Burst(pos, c, c, 3, 2f);
        }

        /// <summary>A fading coloured after-image of a body.</summary>
        public void Ghost(Body body, Color c, float dur)
        {
            var go = Instantiate(body.Root.gameObject, body.Root.position, body.Root.rotation);
            go.transform.localScale = body.Root.lossyScale;
            go.name = "Ghost";
            c.a = 0.55f;
            var mat = Util.Fade(c);
            mat.renderQueue = 3070;
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                var arr = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < arr.Length; i++) arr[i] = mat;
                r.sharedMaterials = arr;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            Add(new Item
            {
                Go = go, Mat = mat, Dur = dur, Color = c, Unscaled = true,
                Step = (it, k) => { var col = it.Color; col.a = 0.55f * (1 - k); it.Mat.color = col; }
            });
        }

        /// <summary>Thousand Hands: golden afterimage arms fan out around the keeper.</summary>
        public void Arms(Player k, Color a, Color b)
        {
            var root = new GameObject("Hands").transform;
            var chest = k.Pos + Vector3.up * 1.25f;
            root.position = chest;
            root.rotation = k.transform.rotation;
            var mat = Util.Glow(b);
            var handMat = Util.Glow(a);
            int n = 26;
            for (int i = 0; i < n; i++)
            {
                float ang = -110 + 220f * i / (n - 1);
                var arm = new GameObject("Arm").transform;
                arm.SetParent(root, false);
                arm.localRotation = Quaternion.Euler(Random.Range(-35f, -5f), 0, ang);
                float len = Random.Range(1.1f, 1.9f);
                var q = MakeQuadBeam(arm, len, 0.32f, mat);
                var hand = MakeBillboard(arm, new Vector3(0, len, 0), 0.6f, handMat);
            }
            Add(new Item
            {
                Go = root.gameObject, Mat = mat, Dur = 1.6f, Unscaled = true, Color = b,
                Step = (it, kk) =>
                {
                    float grow = Mathf.SmoothStep(0, 1, Mathf.Clamp01(kk * 4));
                    it.Go.transform.localScale = Vector3.one * Mathf.Lerp(0.2f, 1.6f, grow);
                    var col = it.Color; col.a = Mathf.Clamp01((1 - kk) * 2f);
                    it.Mat.color = col;
                    it.Go.transform.Rotate(0, 0, Mathf.Sin(kk * 20) * 0.3f, Space.Self);
                }
            });
        }

        static GameObject MakeQuadBeam(Transform parent, float len, float width, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0, len * 0.5f, 0);
            go.transform.localScale = new Vector3(width, len, 1);
            go.GetComponent<Renderer>().sharedMaterial = mat;
            go.AddComponent<FaceCamera>().AxisLocked = true;
            return go;
        }

        static GameObject MakeBillboard(Transform parent, Vector3 pos, float size, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = Vector3.one * size;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            go.AddComponent<FaceCamera>();
            return go;
        }

        /// <summary>Gives the ball its special-move look until the special ends.</summary>
        public void SetAura(Ball b, HeroDef def, float scale)
        {
            auraDef = def; auraScale = scale;
            if (auraQuad) Destroy(auraQuad.gameObject);
            if (auraRing) Destroy(auraRing.gameObject);
            auraQuad = MakeBillboard(b.Aura, Vector3.zero, 1.6f * scale, Util.Glow(def.FxB)).transform;
            if (def.Special == SpecialType.CurveShot)
            {
                var mat = Util.Glow(def.FxA);
                auraRing = Util.MeshObj("Chakra", Util.Ring(0.55f, 0.75f, 48), b.Aura, Vector3.zero, mat).transform;
            }
        }

        static bool IsVillain(HeroDef d) => System.Array.IndexOf(Teams.Titan.Heroes, d) >= 0;

        void Update()
        {
            float dt = Time.deltaTime, udt = Time.unscaledDeltaTime;
            FlashA = Mathf.MoveTowards(FlashA, 0, udt * 2.2f);

            for (int i = items.Count - 1; i >= 0; i--)
            {
                var it = items[i];
                if (!it.Go) { items.RemoveAt(i); continue; }
                it.T += it.Unscaled ? udt : dt;
                float k = Mathf.Clamp01(it.T / it.Dur);
                it.Step?.Invoke(it, k);
                if (k >= 1) { Destroy(it.Go); items.RemoveAt(i); }
            }

            // special ball trail
            var b = M.Ball;
            if (b && b.Special != null && auraDef != null)
            {
                var d = b.Special;
                bool comet = d.Special == SpecialType.PowerShot;
                int n = comet ? 6 : 3;
                for (int i = 0; i < n; i++)
                    Emit(glow, b.Pos + Random.insideUnitSphere * 0.25f * auraScale, -b.Vel * 0.08f + Random.insideUnitSphere * 1.5f,
                        Random.Range(0.35f, 0.8f) * auraScale, Random.Range(0.25f, 0.5f), Color.Lerp(d.FxA, d.FxB, Random.value));
                if (IsVillain(d))
                    Emit(smoke, b.Pos + Random.insideUnitSphere * 0.3f, -b.Vel * 0.05f + Random.insideUnitSphere, Random.Range(0.6f, 1.1f), Random.Range(0.4f, 0.8f), new Color(0.1f, 0.1f, 0.14f, 0.7f));
                if (auraQuad) auraQuad.localScale = Vector3.one * (1.6f + Mathf.Sin(Time.unscaledTime * 30) * 0.15f) * auraScale;
                if (auraRing) auraRing.Rotate(0, 900 * dt, 0, Space.Self);
            }
        }

        void Add(Item it) { items.Add(it); it.Step?.Invoke(it, 0); }
    }

    /// <summary>Billboard helper. AxisLocked keeps the quad's local up axis and only turns it to face the camera.</summary>
    public sealed class FaceCamera : MonoBehaviour
    {
        public bool AxisLocked;
        void LateUpdate()
        {
            var cam = Camera.main;
            if (!cam) return;
            if (!AxisLocked) { transform.rotation = cam.transform.rotation; return; }
            var up = transform.parent ? transform.parent.up : Vector3.up;
            var toCam = cam.transform.position - transform.position;
            var fwd = Vector3.ProjectOnPlane(-toCam, up);
            if (fwd.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(fwd, up);
        }
    }
}
