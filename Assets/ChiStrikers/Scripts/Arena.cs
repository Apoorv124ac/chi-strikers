using System.Collections.Generic;
using UnityEngine;

namespace ChiStrikers
{
    /// <summary>
    /// The indoor arena, in the reel's style: a glossy cyan court that mirrors the players, white and orange lines,
    /// grey ad boards, two-tier amber stands packed with tiny outlined fans, and a red LED scoreboard.
    /// </summary>
    public sealed class Arena : MonoBehaviour
    {
        public Match M;
        Transform[] nets = new Transform[2];
        float[] bulge = new float[2];
        readonly List<Transform> crowds = new List<Transform>();
        readonly List<float> crowdPhase = new List<float>();
        public float Excite;
        TextMesh ledScore, ledClock;

        static readonly Color Ink = Util.Hex("#1b1b22");

        public static Arena Build(Match m)
        {
            var go = new GameObject("Arena");
            var a = go.AddComponent<Arena>();
            a.M = m;
            a.BuildLight();
            a.BuildFloor();
            a.BuildGoal(1);
            a.BuildGoal(-1);
            a.BuildBoards();
            a.BuildStands();
            a.BuildBackdrop();
            return a;
        }

        void BuildLight()
        {
            var lg = new GameObject("Key Light");
            lg.transform.SetParent(transform, false);
            lg.transform.rotation = Quaternion.Euler(48f, 18f, 0);
            var l = lg.AddComponent<Light>();
            l.type = LightType.Directional;
            l.intensity = 1f;
            l.color = Color.white;
            l.shadows = LightShadows.None;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.6f, 0.7f);
            RenderSettings.fog = false;
            RenderSettings.skybox = null;
            QualitySettings.shadows = ShadowQuality.Disable;
            QualitySettings.antiAliasing = 4;
        }

        Material FloorMat(Color c, float alpha, int queue)
        {
            c.a = alpha;
            var m = Util.Fade(c);
            m.renderQueue = queue;
            return m;
        }

        GameObject FloorQuad(string name, Vector3 center, Vector2 size, Material mat, float y = 0)
        {
            var go = Util.MeshObj(name, Util.GroundQuad(), transform, new Vector3(center.x, y, center.z), mat, Vector3.zero, new Vector3(size.x, 1, size.y));
            go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        /// <summary>Four quads filling the area between an inner and an outer centred rectangle.</summary>
        void Frame(string name, Vector2 inner, Vector2 outer, Material mat)
        {
            float bandZ = (outer.y - inner.y) * 0.5f, bandX = (outer.x - inner.x) * 0.5f;
            FloorQuad(name, new Vector3(0, 0, (inner.y + bandZ) * 0.5f), new Vector2(outer.x, bandZ), mat);
            FloorQuad(name, new Vector3(0, 0, -(inner.y + bandZ) * 0.5f), new Vector2(outer.x, bandZ), mat);
            FloorQuad(name, new Vector3((inner.x + bandX) * 0.5f, 0, 0), new Vector2(bandX, inner.y), mat);
            FloorQuad(name, new Vector3(-(inner.x + bandX) * 0.5f, 0, 0), new Vector2(bandX, inner.y), mat);
        }

        void BuildFloor()
        {
            // surround, court bands, lines. The floor is semi-transparent so the mirrored players show through it.
            // each layer is a frame around the next, so the court is a single see-through layer
            var court = new Vector2(Pitch.HalfL * 2, Pitch.HalfW * 2);
            var apron = new Vector2(Pitch.HalfL * 2 + 6, Pitch.HalfW * 2 + 2.4f);
            Frame("Apron", court, apron, FloorMat(Util.Hex("#1676cc"), 0.8f, 3001));
            Frame("Surround", apron, new Vector2(140, 90), FloorMat(Util.Hex("#155da6"), 0.88f, 3000));
            int bands = 10;
            float bw = Pitch.HalfL * 2 / bands;
            for (int i = 0; i < bands; i++)
            {
                var c = i % 2 == 0 ? Util.Hex("#1e9cf0") : Util.Hex("#2aa8f6");
                FloorQuad("Band", new Vector3(-Pitch.HalfL + bw * (i + 0.5f), 0, 0), new Vector2(bw, Pitch.HalfW * 2), FloorMat(c, 0.72f, 3002));
            }
            var white = FloorMat(Util.Hex("#f2f2f0"), 0.95f, 3003);
            var orange = FloorMat(Util.Hex("#f2a12e"), 0.95f, 3003);
            float lw = 0.12f, y = 0.004f;
            // boundary
            FloorQuad("Line", new Vector3(0, 0, Pitch.HalfW), new Vector2(Pitch.HalfL * 2 + lw, lw), white, y);
            FloorQuad("Line", new Vector3(0, 0, -Pitch.HalfW), new Vector2(Pitch.HalfL * 2 + lw, lw), white, y);
            FloorQuad("Line", new Vector3(Pitch.HalfL, 0, 0), new Vector2(lw, Pitch.HalfW * 2), white, y);
            FloorQuad("Line", new Vector3(-Pitch.HalfL, 0, 0), new Vector2(lw, Pitch.HalfW * 2), white, y);
            FloorQuad("Halfway", Vector3.zero, new Vector2(lw, Pitch.HalfW * 2), white, y);
            var circle = Util.MeshObj("Centre circle", Util.Ring(3f, 3f + lw * 1.6f, 80), transform, new Vector3(0, y, 0), orange);
            circle.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            FloorQuad("Spot", Vector3.zero, new Vector2(0.35f, 0.35f), orange, y);
            foreach (int s in new[] { -1, 1 })
            {
                float gx = s * Pitch.HalfL;
                // penalty area
                FloorQuad("Box", new Vector3(gx - s * Pitch.BoxDepth, 0, 0), new Vector2(lw, Pitch.BoxHalfW * 2), white, y);
                FloorQuad("Box", new Vector3(gx - s * Pitch.BoxDepth * 0.5f, 0, Pitch.BoxHalfW), new Vector2(Pitch.BoxDepth, lw), white, y);
                FloorQuad("Box", new Vector3(gx - s * Pitch.BoxDepth * 0.5f, 0, -Pitch.BoxHalfW), new Vector2(Pitch.BoxDepth, lw), white, y);
                // goal area tint
                FloorQuad("Crease", new Vector3(gx - s * 1.2f, 0, 0), new Vector2(2.4f, Pitch.GoalHalfW * 2 + 1.5f), FloorMat(Util.Hex("#f2a12e"), 0.35f, 3003), y * 0.5f);
                FloorQuad("PenSpot", new Vector3(gx - s * 4.5f, 0, 0), new Vector2(0.3f, 0.3f), white, y);
                // half-circle arc
                var arc = Util.MeshObj("Arc", Util.Ring(2.2f, 2.2f + lw * 1.4f, 40, 180), transform, new Vector3(gx - s * Pitch.BoxDepth, y, 0), white, new Vector3(0, s > 0 ? 90 : -90, 0));
                arc.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        static Texture2D netTex;
        static Texture2D NetTexture()
        {
            if (netTex) return netTex;
            const int n = 64;
            netTex = new Texture2D(n, n, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, name = "net" };
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    bool line = (x % 16) < 2 || (y % 16) < 2;
                    px[y * n + x] = line ? new Color(1, 1, 1, 0.75f) : new Color(1, 1, 1, 0);
                }
            netTex.SetPixels(px); netTex.Apply();
            return netTex;
        }

        void BuildGoal(int side)
        {
            var g = new GameObject(side > 0 ? "Goal East" : "Goal West").transform;
            g.SetParent(transform, false);
            g.localPosition = new Vector3(side * Pitch.HalfL, 0, 0);
            var post = Util.Lit(Util.Hex("#f2f2f0"), 2f);
            float r = 0.16f, H = Pitch.GoalH, W = Pitch.GoalHalfW, D = Pitch.GoalDepth;
            foreach (int zs in new[] { -1, 1 })
            {
                Util.Prim(PrimitiveType.Cylinder, g, new Vector3(0, H * 0.5f, zs * W), new Vector3(r, H * 0.5f, r), post);
                Util.Prim(PrimitiveType.Cylinder, g, new Vector3(side * D * 0.5f, 0.05f, zs * W), new Vector3(0.08f, D * 0.5f, 0.08f), post, new Vector3(0, 0, 90));
                Util.Prim(PrimitiveType.Cylinder, g, new Vector3(side * D * 0.5f, H, zs * W), new Vector3(0.08f, D * 0.5f, 0.08f), post, new Vector3(0, 0, 90));
                Util.Prim(PrimitiveType.Cylinder, g, new Vector3(side * D, H * 0.5f, zs * W), new Vector3(0.08f, H * 0.5f, 0.08f), post);
            }
            Util.Prim(PrimitiveType.Cylinder, g, new Vector3(0, H, 0), new Vector3(r, W, r), post, new Vector3(90, 0, 0));
            Util.Prim(PrimitiveType.Cylinder, g, new Vector3(side * D, H, 0), new Vector3(0.08f, W, 0.08f), post, new Vector3(90, 0, 0));

            // nets
            var netMat = new Material(Util.SpriteShader) { mainTexture = NetTexture(), color = new Color(1, 1, 1, 0.8f) };
            netMat.mainTextureScale = new Vector2(W * 2 / 0.6f, H / 0.6f);
            netMat.renderQueue = 3080;
            var net = new GameObject("Net").transform;
            net.SetParent(g, false);
            Quad(net, new Vector3(side * D, H * 0.5f, 0), new Vector3(W * 2, H, 1), new Vector3(0, 90, 0), netMat);
            var sideMat = new Material(netMat); sideMat.mainTextureScale = new Vector2(D / 0.6f, H / 0.6f);
            Quad(net, new Vector3(side * D * 0.5f, H * 0.5f, W), new Vector3(D, H, 1), Vector3.zero, sideMat);
            Quad(net, new Vector3(side * D * 0.5f, H * 0.5f, -W), new Vector3(D, H, 1), Vector3.zero, sideMat);
            var topMat = new Material(netMat); topMat.mainTextureScale = new Vector2(D / 0.6f, W * 2 / 0.6f);
            Quad(net, new Vector3(side * D * 0.5f, H, 0), new Vector3(D, W * 2, 1), new Vector3(90, 0, 0), topMat);
            nets[side > 0 ? 1 : 0] = net;

            // the goal frame reflects in the floor too
            var refl = Instantiate(g.gameObject, transform).transform;
            refl.name = "Goal reflection";
            refl.localPosition = new Vector3(side * Pitch.HalfL, 0, 0);
            refl.localScale = new Vector3(1, -1, 1);
            var rn = refl.Find("Net"); if (rn) Destroy(rn.gameObject);
        }

        static void Quad(Transform parent, Vector3 pos, Vector3 scale, Vector3 euler, Material mat)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(q.GetComponent<Collider>());
            q.transform.SetParent(parent, false);
            q.transform.localPosition = pos;
            q.transform.localScale = scale;
            q.transform.localRotation = Quaternion.Euler(euler);
            q.GetComponent<Renderer>().sharedMaterial = mat;
            q.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        void BuildBoards()
        {
            var board = Util.Lit(Util.Hex("#c5c9cf"));
            string[] ads = { "MOHALLA CHAI", "CHI STRIKERS CUP", "GULLY SPORTS", "TITAN CORP", "FAST FOOD KING", "BAZAAR MART" };
            Color[] adCols = { Util.Hex("#f28a1e"), Util.Hex("#26324f"), Util.Hex("#e0352b"), Util.Hex("#1e1e24"), Util.Hex("#3bb0c8"), Util.Hex("#ffc93c") };
            float h = 0.9f, z = Pitch.HalfW + 0.35f;
            int panels = 6;
            float pw = (Pitch.HalfL * 2 + 4) / panels;
            for (int i = 0; i < panels; i++)
            {
                float x = -Pitch.HalfL - 2 + pw * (i + 0.5f);
                // far side (faces the camera) with adverts
                Util.Prim(PrimitiveType.Cube, transform, new Vector3(x, h * 0.5f, z), new Vector3(pw - 0.08f, h, 0.2f), board);
                var ad = Util.Prim(PrimitiveType.Cube, transform, new Vector3(x, h * 0.5f, z - 0.11f), new Vector3(pw - 0.5f, h - 0.25f, 0.02f), Util.Lit(adCols[i % adCols.Length], 0f));
                var txtCol = (i % adCols.Length == 5) ? Util.Hex("#26324f") : Color.white;
                Util.Text3D(transform, ads[i % ads.Length], new Vector3(x, h * 0.5f, z - 0.13f), Vector3.zero, 0.42f, txtCol);
                // near side: plain, low
                Util.Prim(PrimitiveType.Cube, transform, new Vector3(x, 0.3f, -z), new Vector3(pw - 0.08f, 0.6f, 0.2f), board);
            }
            // end boards beside the goals
            foreach (int s in new[] { -1, 1 })
                foreach (int zs in new[] { -1, 1 })
                {
                    float len = Pitch.HalfW - Pitch.GoalHalfW - 0.2f;
                    Util.Prim(PrimitiveType.Cube, transform, new Vector3(s * (Pitch.HalfL + 0.35f), h * 0.5f, zs * (Pitch.GoalHalfW + 0.2f + len * 0.5f)), new Vector3(0.2f, h, len), board);
                }
        }

        void BuildStands()
        {
            var seat = Util.Lit(Util.Hex("#d98b2c"), 1.6f, Util.Hex("#a86520"));
            var step = Util.Lit(Util.Hex("#a86520"), 1.6f, Util.Hex("#7e4a17"));
            // far side: two tiers
            BuildTier(new Vector3(0, 0, Pitch.HalfW + 2.2f), Pitch.HalfL * 2 + 6, 7, 0.9f, 0.55f, 0.4f, seat, step, 0);
            BuildTier(new Vector3(0, 7.2f, Pitch.HalfW + 10.5f), Pitch.HalfL * 2 + 14, 7, 0.95f, 0.6f, 0f, seat, step, 1);
            // dark band between the tiers with a lit rail
            Util.Prim(PrimitiveType.Cube, transform, new Vector3(0, 6.3f, Pitch.HalfW + 9.8f), new Vector3(Pitch.HalfL * 2 + 14, 1.6f, 0.6f), Util.Lit(Util.Hex("#26324f")));
            Util.Prim(PrimitiveType.Cube, transform, new Vector3(0, 6.0f, Pitch.HalfW + 9.45f), new Vector3(Pitch.HalfL * 2 + 13, 0.12f, 0.05f), Util.Emissive(Util.Hex("#ffc93c"), 0));
            // behind the goals: one tier each, set back
            foreach (int s in new[] { -1, 1 })
            {
                var t = BuildTier(new Vector3(s * (Pitch.HalfL + 4.5f), 0, 0), Pitch.HalfW * 2 + 2, 5, 0.9f, 0.55f, 0.4f, seat, step, 2 + (s > 0 ? 1 : 0));
                t.localRotation = Quaternion.Euler(0, s > 0 ? 90 : -90, 0);
            }
        }

        /// <summary>A tier of stepped seating with a crowd. The crowd is merged into a few meshes so it stays cheap.</summary>
        Transform BuildTier(Vector3 origin, float width, int rows, float rowDepth, float rowRise, float baseY, Material seat, Material stepM, int seed)
        {
            var tier = new GameObject("Tier").transform;
            tier.SetParent(transform, false);
            tier.localPosition = origin;
            for (int r = 0; r < rows; r++)
            {
                float y = baseY + r * rowRise;
                Util.Prim(PrimitiveType.Cube, tier, new Vector3(0, y * 0.5f + 0.2f, r * rowDepth), new Vector3(width, y + 0.4f, rowDepth), r % 2 == 0 ? seat : stepM);
            }
            // crowd
            var crowd = new GameObject("Crowd").transform;
            crowd.SetParent(tier, false);
            var rng = new System.Random(1234 + seed);
            Color[] shirts =
            {
                Util.Hex("#f28a1e"), Util.Hex("#f28a1e"), Util.Hex("#f2f2f0"), Util.Hex("#26324f"), Util.Hex("#e0352b"),
                Util.Hex("#3bb0c8"), Util.Hex("#ffc93c"), Util.Hex("#5bb8f0"), Util.Hex("#1e1e24")
            };
            Color[] skins = { Util.Hex("#f1c9a1"), Util.Hex("#d9a07a"), Util.Hex("#a8704c"), Util.Hex("#6e4630") };
            var temp = new List<GameObject>();
            for (int r = 0; r < rows; r++)
            {
                float y = baseY + r * rowRise + 0.4f;
                int count = (int)(width / 0.62f);
                for (int i = 0; i < count; i++)
                {
                    if (rng.NextDouble() < 0.12) continue; // empty seat
                    float x = -width * 0.5f + (i + 0.5f) * (width / count) + (float)(rng.NextDouble() - 0.5) * 0.15f;
                    var shirt = Util.Lit(shirts[rng.Next(shirts.Length)], 1.1f);
                    var skin = Util.Lit(skins[rng.Next(skins.Length)], 1.1f);
                    var bodyGo = Util.Prim(PrimitiveType.Capsule, crowd, new Vector3(x, y + 0.32f, r * rowDepth + 0.1f), new Vector3(0.36f, 0.27f, 0.3f), shirt, default, false);
                    var headGo = Util.Prim(PrimitiveType.Sphere, crowd, new Vector3(x, y + 0.78f, r * rowDepth + 0.1f), new Vector3(0.26f, 0.29f, 0.26f), skin, default, false);
                    temp.Add(bodyGo); temp.Add(headGo);
                    if (rng.NextDouble() < 0.6)
                    {
                        var hair = Util.Prim(PrimitiveType.Sphere, crowd, new Vector3(x, y + 0.85f, r * rowDepth + 0.13f), new Vector3(0.28f, 0.2f, 0.27f), Util.Lit(Util.Hex(rng.NextDouble() < 0.8 ? "#1a1a1a" : "#3a2a20"), 1.1f), default, false);
                        temp.Add(hair);
                    }
                }
            }
            Merge(crowd, temp);
            crowds.Add(crowd);
            crowdPhase.Add(seed * 1.7f);
            return tier;
        }

        /// <summary>Combines many small renderers into one mesh per material.</summary>
        static void Merge(Transform root, List<GameObject> parts)
        {
            var groups = new Dictionary<Material, List<CombineInstance>>();
            foreach (var p in parts)
            {
                var mr = p.GetComponent<MeshRenderer>();
                var mf = p.GetComponent<MeshFilter>();
                if (!groups.TryGetValue(mr.sharedMaterial, out var list)) groups[mr.sharedMaterial] = list = new List<CombineInstance>();
                list.Add(new CombineInstance { mesh = mf.sharedMesh, transform = root.worldToLocalMatrix * p.transform.localToWorldMatrix });
            }
            foreach (var kv in groups)
            {
                var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32, name = "crowd" };
                mesh.CombineMeshes(kv.Value.ToArray(), true, true);
                var go = Util.MeshObj("Crowd " + kv.Key.name, mesh, root, Vector3.zero, kv.Key);
                go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            foreach (var p in parts) Destroy(p);
        }

        void BuildBackdrop()
        {
            var navy = Util.Lit(Util.Hex("#0c1a33"), 0f);
            // back wall and roof edge
            Util.Prim(PrimitiveType.Cube, transform, new Vector3(0, 13, Pitch.HalfW + 19f), new Vector3(90, 30, 1), navy, default, false);
            Util.Prim(PrimitiveType.Cube, transform, new Vector3(0, 21.5f, Pitch.HalfW + 6f), new Vector3(90, 1.2f, 30), Util.Lit(Util.Hex("#26324f"), 0f), default, false);
            // banner
            Util.Prim(PrimitiveType.Cube, transform, new Vector3(0, 16.2f, Pitch.HalfW + 18.3f), new Vector3(26, 2.6f, 0.2f), Util.Lit(Util.Hex("#26324f"), 2f), default, false);
            Util.Text3D(transform, "CHI STRIKERS CUP", new Vector3(0, 16.2f, Pitch.HalfW + 18.1f), Vector3.zero, 2.1f, Util.Hex("#ffc93c"));
            // roof light rows
            var lightMat = Util.Glow(new Color(1f, 0.95f, 0.8f, 0.9f));
            for (int i = -6; i <= 6; i++)
            {
                var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Destroy(q.GetComponent<Collider>());
                q.transform.SetParent(transform, false);
                q.transform.localPosition = new Vector3(i * 6f, 20.6f, Pitch.HalfW + 2f);
                q.transform.localScale = new Vector3(3.2f, 1.2f, 1);
                q.transform.localRotation = Quaternion.Euler(-70, 0, 0);
                q.GetComponent<Renderer>().sharedMaterial = lightMat;
            }
            // LED scoreboard hanging above the far stand
            var sb = new GameObject("Scoreboard").transform;
            sb.SetParent(transform, false);
            sb.localPosition = new Vector3(0, 11.6f, Pitch.HalfW + 6.5f);
            Util.Prim(PrimitiveType.Cube, sb, Vector3.zero, new Vector3(9f, 2.8f, 0.6f), Util.Lit(Util.Hex("#120e10"), 2.5f));
            Util.Prim(PrimitiveType.Cylinder, sb, new Vector3(-3, 3.5f, 0), new Vector3(0.06f, 2.2f, 0.06f), Util.Lit(Util.Hex("#1e1e24")));
            Util.Prim(PrimitiveType.Cylinder, sb, new Vector3(3, 3.5f, 0), new Vector3(0.06f, 2.2f, 0.06f), Util.Lit(Util.Hex("#1e1e24")));
            ledScore = Util.Text3D(sb, "MOH 0 - 0 TTN", new Vector3(0, 0.45f, -0.32f), Vector3.zero, 0.85f, Util.Hex("#ff2b1c"));
            ledClock = Util.Text3D(sb, "0:00", new Vector3(0, -0.75f, -0.32f), Vector3.zero, 0.8f, Util.Hex("#ffc93c"));
        }

        public void SetBoard(string score, string clock)
        {
            if (ledScore && ledScore.text != score) ledScore.text = score;
            if (ledClock && ledClock.text != clock) ledClock.text = clock;
        }

        public void Bulge(int side) { bulge[side > 0 ? 1 : 0] = 1f; }

        void Update()
        {
            float dt = Time.deltaTime;
            for (int i = 0; i < 2; i++)
            {
                if (!nets[i]) continue;
                bulge[i] = Mathf.MoveTowards(bulge[i], 0, dt * 2.5f);
                float k = Mathf.Sin(bulge[i] * Mathf.PI) * 0.18f;
                nets[i].localScale = new Vector3(1 + k * 1.5f, 1 + k * 0.3f, 1 + k * 0.3f);
            }
            Excite = Mathf.MoveTowards(Excite, 0, Time.unscaledDeltaTime * 0.25f);
            for (int i = 0; i < crowds.Count; i++)
            {
                float t = Time.time * (6f + Excite * 6f) + crowdPhase[i];
                float amp = 0.02f + Excite * 0.22f;
                crowds[i].localPosition = new Vector3(0, Mathf.Abs(Mathf.Sin(t)) * amp, 0);
            }
        }
    }
}
