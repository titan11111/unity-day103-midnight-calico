using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MidnightCalico
{
    /// <summary>
    /// マテリアル生成。Built-in(Standard) と URP(Lit) の両方に対応。
    /// </summary>
    public static class Mat
    {
        static Shader lit;
        static readonly Dictionary<string, Material> cache = new Dictionary<string, Material>();

        static void Init()
        {
            if (lit != null) return;
            if (GraphicsSettings.currentRenderPipeline != null)
                lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null) lit = Shader.Find("Standard");
            if (lit == null) lit = Shader.Find("Legacy Shaders/Diffuse");
        }

        public static Material Make(string key, Color c, Texture2D tex = null, float smooth = 0.2f,
            float metal = 0f, float tileU = 1f, float tileV = 1f)
        {
            Material m;
            if (cache.TryGetValue(key, out m)) return m;
            Init();
            m = new Material(lit);
            m.name = key;
            m.color = c;
            if (tex != null)
            {
                m.mainTexture = tex;
                m.mainTextureScale = new Vector2(tileU, tileV);
            }
            m.SetFloat("_Glossiness", smooth);
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Metallic", metal);
            cache[key] = m;
            return m;
        }

        /// <summary>発光マテリアル（窓の月明かり、照明のかさ、目の反射など）</summary>
        public static Material Emissive(string key, Color c, Color emission)
        {
            Material m;
            if (cache.TryGetValue(key, out m)) return m;
            m = Make(key + "_base", c, null, 0.4f);
            m = new Material(m);
            m.name = key;
            SetEmission(m, emission);
            cache[key] = m;
            return m;
        }

        public static void SetEmission(Material m, Color emission)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emission);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }

        /// <summary>同じ質感で別タイリングのもの（床・壁の大きさに合わせる）</summary>
        public static Material Tiled(Material src, float u, float v)
        {
            string key = src.name + "_t" + u.ToString("0.0") + "x" + v.ToString("0.0");
            Material m;
            if (cache.TryGetValue(key, out m)) return m;
            m = new Material(src);
            m.name = key;
            m.mainTextureScale = new Vector2(u, v);
            cache[key] = m;
            return m;
        }

        static readonly Dictionary<Texture, Texture2D> normals = new Dictionary<Texture, Texture2D>();
        public static bool normalMaps = true;

        /// <summary>色テクスチャから凹凸を作って貼る（懐中電灯を当てた時に木目や目地が浮き出る）</summary>
        public static Material WithNormal(Material m, float strength)
        {
            if (!normalMaps || m == null) return m;
            var src = m.mainTexture as Texture2D;
            if (src == null) return m;
            Texture2D n;
            if (!normals.TryGetValue(src, out n))
            {
                n = Tex.NormalFromAlbedo(src, strength);
                normals[src] = n;
            }
            m.SetTexture("_BumpMap", n);
            m.SetFloat("_BumpScale", 1f);
            m.EnableKeyword("_NORMALMAP");
            return m;
        }

        static Shader unlitBlend;
        /// <summary>半透明・ライティングなし（接地の影、ほこり、きらめき）</summary>
        public static Material Unlit(string key, Texture2D tex, Color c)
        {
            Material m;
            if (cache.TryGetValue(key, out m)) return m;
            if (unlitBlend == null) unlitBlend = Shader.Find("Sprites/Default");
            if (unlitBlend == null) unlitBlend = Shader.Find("UI/Default");
            m = new Material(unlitBlend);
            m.name = key;
            m.mainTexture = tex;
            m.color = c;
            m.renderQueue = 2450;   // 不透明の後、ほかの半透明の前
            cache[key] = m;
            return m;
        }

        public static void ClearCache() { cache.Clear(); lit = null; }
    }

    /// <summary>
    /// プリミティブで形を組むための補助関数。
    /// </summary>
    public static class Geo
    {
        public static GameObject Box(Transform parent, string name, Vector3 center, Vector3 size, Material m, bool collider = true, float rotY = 0f)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            g.name = name;
            g.transform.SetParent(parent, false);
            g.transform.localPosition = center;
            g.transform.localRotation = Quaternion.Euler(0f, rotY, 0f);
            g.transform.localScale = size;
            g.GetComponent<Renderer>().sharedMaterial = m;
            if (!collider) Object.DestroyImmediate(g.GetComponent<Collider>());
            return g;
        }

        /// <summary>最小座標～最大座標で箱を置く（ワールドの家座標）</summary>
        public static GameObject BoxMM(Transform parent, string name, float x0, float y0, float z0, float x1, float y1, float z1, Material m, bool collider = true)
        {
            var c = new Vector3((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, (z0 + z1) * 0.5f);
            var s = new Vector3(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0), Mathf.Abs(z1 - z0));
            return Box(parent, name, c, s, m, collider);
        }

        public static GameObject Cyl(Transform parent, string name, Vector3 center, float radius, float height, Material m, bool collider = true)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            g.name = name;
            g.transform.SetParent(parent, false);
            g.transform.localPosition = center;
            g.transform.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);
            g.GetComponent<Renderer>().sharedMaterial = m;
            if (!collider) Object.DestroyImmediate(g.GetComponent<Collider>());
            return g;
        }

        public static GameObject Sph(Transform parent, string name, Vector3 center, Vector3 scale, Material m, bool collider = false)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            g.name = name;
            g.transform.SetParent(parent, false);
            g.transform.localPosition = center;
            g.transform.localScale = scale;
            g.GetComponent<Renderer>().sharedMaterial = m;
            if (!collider) Object.DestroyImmediate(g.GetComponent<Collider>());
            return g;
        }

        /// <summary>面1枚（窓の外の月明かり、掛け軸など）</summary>
        public static GameObject Quad(Transform parent, string name, Vector3 center, Vector2 size, Vector3 euler, Material m)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Quad);
            g.name = name;
            g.transform.SetParent(parent, false);
            g.transform.localPosition = center;
            g.transform.localRotation = Quaternion.Euler(euler);
            g.transform.localScale = new Vector3(size.x, size.y, 1f);
            g.GetComponent<Renderer>().sharedMaterial = m;
            Object.DestroyImmediate(g.GetComponent<Collider>());
            return g;
        }

        /// <summary>空の回転軸（関節など）</summary>
        public static Transform Pivot(Transform parent, string name, Vector3 localPos)
        {
            var g = new GameObject(name);
            g.transform.SetParent(parent, false);
            g.transform.localPosition = localPos;
            return g.transform;
        }

        /// <summary>四角錐メッシュ（猫の耳など）</summary>
        static Mesh pyramid;
        public static Mesh Pyramid()
        {
            if (pyramid != null) return pyramid;
            Vector3 a = new Vector3(-0.5f, 0, -0.5f), b = new Vector3(0.5f, 0, -0.5f), c = new Vector3(0.5f, 0, 0.5f), d = new Vector3(-0.5f, 0, 0.5f), top = new Vector3(0, 1, 0);
            var verts = new List<Vector3>();
            var tris = new List<int>();
            void Tri(Vector3 p0, Vector3 p1, Vector3 p2)
            {
                int i = verts.Count;
                verts.Add(p0); verts.Add(p1); verts.Add(p2);
                tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
            }
            Tri(a, top, b); Tri(b, top, c); Tri(c, top, d); Tri(d, top, a);
            Tri(a, b, c); Tri(a, c, d);
            pyramid = new Mesh();
            pyramid.name = "pyramid";
            pyramid.SetVertices(verts);
            pyramid.SetTriangles(tris, 0);
            var uv = new Vector2[verts.Count];
            for (int i = 0; i < uv.Length; i++) uv[i] = new Vector2(verts[i].x + 0.5f, verts[i].y);
            pyramid.uv = uv;
            pyramid.RecalculateNormals();
            pyramid.RecalculateBounds();
            return pyramid;
        }

        public static GameObject MeshObj(Transform parent, string name, Mesh mesh, Vector3 pos, Vector3 euler, Vector3 scale, Material m)
        {
            var g = new GameObject(name);
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.transform.localRotation = Quaternion.Euler(euler);
            g.transform.localScale = scale;
            g.AddComponent<MeshFilter>().sharedMesh = mesh;
            g.AddComponent<MeshRenderer>().sharedMaterial = m;
            return g;
        }

        public static void SetLayerRecursive(GameObject g, int layer)
        {
            g.layer = layer;
            foreach (Transform c in g.transform) SetLayerRecursive(c.gameObject, layer);
        }

        public static void NoShadows(GameObject g)
        {
            foreach (var r in g.GetComponentsInChildren<Renderer>())
                r.shadowCastingMode = ShadowCastingMode.Off;
        }
    }
}
