using System.Collections.Generic;
using UnityEngine;

namespace ChiStrikers
{
    /// <summary>Materials, generated meshes and small helpers. Everything is built in code, so the game runs with no art assets.</summary>
    public static class Util
    {
        static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();
        static Material litTemplate;
        public static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        public static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }

        static string Key(string kind, Color c, float a = 0, float b = 0) =>
            kind + ColorUtility.ToHtmlStringRGBA(c) + a.ToString("F2") + b.ToString("F2");

        static Shader toon;
        static Shader Toon => toon ? toon : (toon = Shader.Find("ChiStrikers/Toon"));

        /// <summary>
        /// Flat 2-tone cel material with an ink outline (the look of the reference reel).
        /// outline = width in pixels (0 = none). Falls back to the pipeline's default lit shader.
        /// </summary>
        public static Material Lit(Color c, float outline = 2.2f, Color? shade = null)
        {
            string k = Key("toon", c, outline) + (shade.HasValue ? ColorUtility.ToHtmlStringRGB(shade.Value) : "");
            if (mats.TryGetValue(k, out var m)) return m;
            if (Toon != null)
            {
                m = new Material(Toon) { color = c };
                m.SetFloat("_Outline", outline);
                if (shade.HasValue) m.SetColor("_Shade", shade.Value);
            }
            else
            {
                if (litTemplate == null)
                {
                    var tmp = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    litTemplate = new Material(tmp.GetComponent<Renderer>().sharedMaterial);
                    Object.Destroy(tmp);
                }
                m = new Material(litTemplate) { color = c };
                if (m.HasProperty(BaseColorId)) m.SetColor(BaseColorId, c);
            }
            mats[k] = m;
            return m;
        }

        /// <summary>A flat, fully lit toon colour (glowing visors, LED trims).</summary>
        public static Material Emissive(Color c, float outline = 1.5f)
        {
            string k = Key("emi", c, outline);
            if (mats.TryGetValue(k, out var m)) return m;
            m = new Material(Lit(c, outline));
            if (m.HasProperty("_Emission")) m.SetFloat("_Emission", 1f);
            mats[k] = m;
            return m;
        }

        static Shader spriteShader, addShader;
        public static Shader SpriteShader => spriteShader ? spriteShader : (spriteShader = Shader.Find("Sprites/Default"));
        public static Shader AdditiveShader
        {
            get
            {
                if (addShader) return addShader;
                addShader = Shader.Find("Legacy Shaders/Particles/Additive") ?? Shader.Find("Particles/Additive") ?? SpriteShader;
                return addShader;
            }
        }

        /// <summary>Unlit, alpha-blended: fades by changing its colour's alpha. Each call returns a NEW material (so it can fade on its own).</summary>
        public static Material Fade(Color c)
        {
            var m = new Material(SpriteShader) { color = c };
            return m;
        }

        /// <summary>Unlit additive glow. Each call returns a NEW material.</summary>
        public static Material Glow(Color c, Texture tex = null)
        {
            var m = new Material(AdditiveShader);
            if (m.HasProperty("_TintColor")) m.SetColor("_TintColor", c * 0.5f);
            m.color = c;
            m.mainTexture = tex ? tex : SoftDot;
            return m;
        }

        /// <summary>Shared unlit colour (for eyes, lines on the pitch, crowd).</summary>
        public static Material Flat(Color c)
        {
            string k = Key("flat", c);
            if (mats.TryGetValue(k, out var m)) return m;
            m = new Material(SpriteShader) { color = c };
            mats[k] = m;
            return m;
        }

        public static GameObject Prim(PrimitiveType t, Transform parent, Vector3 pos, Vector3 scale, Material mat, Vector3 euler = default, bool shadows = true)
        {
            var go = GameObject.CreatePrimitive(t);
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.transform.localRotation = Quaternion.Euler(euler);
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = shadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        public static GameObject MeshObj(string name, Mesh mesh, Transform parent, Vector3 pos, Material mat, Vector3 euler = default, Vector3? scale = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.transform.localScale = scale ?? Vector3.one;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        static readonly Dictionary<string, Mesh> meshes = new Dictionary<string, Mesh>();

        /// <summary>A flat ring in the XZ plane (pitch circles, shockwaves, the controlled-player marker).</summary>
        public static Mesh Ring(float inner, float outer, int seg = 64, float arcDeg = 360f)
        {
            string key = $"ring{inner:F3}_{outer:F3}_{seg}_{arcDeg:F0}";
            if (meshes.TryGetValue(key, out var m)) return m;
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            for (int i = 0; i <= seg; i++)
            {
                float a = (i / (float)seg) * arcDeg * Mathf.Deg2Rad;
                var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                v.Add(d * inner); v.Add(d * outer);
                uv.Add(new Vector2(i / (float)seg, 0)); uv.Add(new Vector2(i / (float)seg, 1));
            }
            for (int i = 0; i < seg; i++)
            {
                int a = i * 2;
                t.AddRange(new[] { a, a + 1, a + 2, a + 1, a + 3, a + 2 });
            }
            m = new Mesh { name = key };
            m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            meshes[key] = m;
            return m;
        }

        /// <summary>A flat quad lying on the ground (XZ), centred.</summary>
        public static Mesh GroundQuad()
        {
            if (meshes.TryGetValue("gq", out var m)) return m;
            m = new Mesh { name = "gq" };
            m.vertices = new[] { new Vector3(-0.5f, 0, -0.5f), new Vector3(-0.5f, 0, 0.5f), new Vector3(0.5f, 0, 0.5f), new Vector3(0.5f, 0, -0.5f) };
            m.uv = new[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) };
            m.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            m.RecalculateNormals(); m.RecalculateBounds();
            meshes["gq"] = m;
            return m;
        }

        static Texture2D softDot;
        /// <summary>A soft round particle texture (white, alpha falls off to the edge).</summary>
        public static Texture2D SoftDot
        {
            get
            {
                if (softDot) return softDot;
                const int n = 64;
                softDot = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "softdot" };
                var px = new Color[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float dx = (x + 0.5f) / n * 2 - 1, dy = (y + 0.5f) / n * 2 - 1;
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        float a = Mathf.Clamp01(1 - d);
                        a = a * a * (3 - 2 * a);
                        px[y * n + x] = new Color(1, 1, 1, a);
                    }
                softDot.SetPixels(px); softDot.Apply();
                return softDot;
            }
        }

        static Texture2D white;
        public static Texture2D White
        {
            get
            {
                if (white) return white;
                white = new Texture2D(2, 2);
                white.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
                white.Apply();
                return white;
            }
        }

        static Font font;
        public static Font Font
        {
            get
            {
                if (font) return font;
                font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (!font) font = Font.CreateDynamicFontFromOSFont("Arial", 32);
                return font;
            }
        }

        /// <summary>3D text (jersey numbers, board adverts).</summary>
        public static TextMesh Text3D(Transform parent, string text, Vector3 pos, Vector3 euler, float size, Color c)
        {
            var go = new GameObject("Text " + text);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(euler);
            var tm = go.AddComponent<TextMesh>();
            tm.font = Font;
            tm.text = text;
            tm.fontSize = 64;
            tm.characterSize = size / 6.4f; // size = line height in metres
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = c;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = Font.material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return tm;
        }

        public static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0, v.z);
        public static Vector2 XZ(Vector3 v) => new Vector2(v.x, v.z);
        public static float FlatDist(Vector3 a, Vector3 b) { float dx = a.x - b.x, dz = a.z - b.z; return Mathf.Sqrt(dx * dx + dz * dz); }

        /// <summary>Exponential smoothing that is frame-rate independent.</summary>
        public static float Damp(float a, float b, float rate, float dt) => Mathf.Lerp(a, b, 1 - Mathf.Exp(-rate * dt));
        public static Vector3 Damp(Vector3 a, Vector3 b, float rate, float dt) => Vector3.Lerp(a, b, 1 - Mathf.Exp(-rate * dt));

        public static void SetLayerColor(Renderer r, Color c)
        {
            if (r == null) return;
            r.material.color = c;
        }
    }
}
