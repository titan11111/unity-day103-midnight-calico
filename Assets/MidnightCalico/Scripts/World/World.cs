using System.Collections.Generic;
using UnityEngine;

namespace MidnightCalico
{
    public class Room
    {
        public string name;
        public Rect rect;          // x = X座標, y = Z座標
        public Material wallMat;
        public Material floorMat;
        public Room(string n, float x0, float z0, float x1, float z1)
        {
            name = n;
            rect = Rect.MinMaxRect(x0, z0, x1, z1);
        }
        public bool Contains(Vector3 p) { return rect.Contains(new Vector2(p.x, p.z)); }
        public Vector3 Center { get { return new Vector3(rect.center.x, 0f, rect.center.y); } }
    }

    public class CamZone
    {
        public string room;
        public Rect rect;
        public Vector3 pos;
        public Vector3 look;
        public float track;   // 0 = 完全固定, 1 = 主人公を追う
        public float fov;
        public CamZone(string room, float x0, float z0, float x1, float z1, Vector3 pos, Vector3 look, float track = 0.3f, float fov = 55f)
        {
            this.room = room;
            rect = Rect.MinMaxRect(x0, z0, x1, z1);
            this.pos = pos; this.look = look; this.track = track; this.fov = fov;
        }
        public bool Contains(Vector3 p) { return rect.Contains(new Vector2(p.x, p.z)); }
    }

    public enum ItemId { None, Flashlight, Toy, Food, Paste, Net, Matatabi, Bandage }

    public class HideSpot
    {
        public string name;
        public Vector3 pos;
        public Vector3 approach;   // 床の上の入り口
        public bool reachable;     // 手が届くか
        public int node = -1;
        public string room;
        public HideSpot(string n, Vector3 pos, Vector3 approach, bool reachable)
        {
            name = n; this.pos = pos; this.approach = approach; this.reachable = reachable;
        }
    }

    public class SearchPoint
    {
        public string name;
        public Vector3 pos;
        public float radius = 1.3f;
        public string[] lines;
        public string[] afterLines;
        public ItemId item;
        public int qty = 1;
        public bool taken;
        public GameObject glint;
        public bool locked;  // 玄関など
        public SearchPoint(string n, Vector3 p, ItemId item, int qty, params string[] lines)
        {
            name = n; pos = p; this.item = item; this.qty = qty; this.lines = lines;
        }
    }

    /// <summary>部屋の照明スイッチ・引きひも。近づいてActionで点灯/消灯する（2026-10-29 追加）。</summary>
    public class LightSwitch
    {
        public string name;
        public Vector3 pos;
        public float radius = 1.3f;
        public Light light;
        public float onIntensity;
        public bool on;
        public string room;
        public LightSwitch(string n, Vector3 p, Light light, float onIntensity)
        {
            name = n; pos = p; this.light = light; this.onIntensity = onIntensity;
        }
    }

    /// <summary>床に置いたエサ・またたび</summary>
    public class LureBowl : MonoBehaviour
    {
        public static readonly List<LureBowl> all = new List<LureBowl>();
        public ItemId kind;
        public bool consumed;
        void OnEnable() { all.Add(this); }
        void OnDisable() { all.Remove(this); }
        public void Consume()
        {
            consumed = true;
            Destroy(gameObject, 0.2f);
        }
    }

    /// <summary>猫が走り抜けると倒れる小物</summary>
    public class Prop : MonoBehaviour
    {
        public static readonly List<Prop> all = new List<Prop>();
        public bool fragile;
        Rigidbody rb;
        float lastSound;
        bool counted;
        void Awake() { rb = GetComponent<Rigidbody>(); }
        void OnEnable() { all.Add(this); }
        void OnDisable() { all.Remove(this); }

        public void Knock(Vector3 dir, float power)
        {
            if (rb == null) return;
            rb.WakeUp();
            rb.AddForce((dir.normalized * 1.6f + Vector3.up * 1.2f) * power * rb.mass, ForceMode.Impulse);
            rb.AddTorque(Random.insideUnitSphere * 0.6f * rb.mass, ForceMode.Impulse);
            if (!counted)
            {
                counted = true;
                if (GameManager.I != null) GameManager.I.AddDamage();
            }
        }

        void OnCollisionEnter(Collision c)
        {
            if (c.relativeVelocity.magnitude < 1.6f) return;
            if (Time.time - lastSound < 0.3f) return;
            lastSound = Time.time;
            if (AudioMan.I != null)
                AudioMan.I.PlayAt(fragile ? Sfx.Crash : Sfx.Clatter, transform.position, fragile ? 0.9f : 0.6f, Random.Range(0.85f, 1.15f));
        }
    }

    /// <summary>アイテムのありかを示すきらめき（バイオ風）</summary>
    public class Glint : MonoBehaviour
    {
        Vector3 baseScale;
        float phase;
        void Start() { baseScale = transform.localScale; phase = Random.value * 10f; }
        void Update()
        {
            float t = Time.time * 2.2f + phase;
            float k = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(t)), 6f);
            transform.localScale = baseScale * (0.25f + k);
        }
    }

    /// <summary>ちらつく照明</summary>
    public class Flicker : MonoBehaviour
    {
        public float baseIntensity = 1f;
        public float amount = 0.6f;
        Light l;
        float next;
        void Start() { l = GetComponent<Light>(); }
        void Update()
        {
            if (l == null) return;
            if (Time.time > next)
            {
                next = Time.time + Random.Range(0.03f, 0.25f) * (Random.value < 0.1f ? 8f : 1f);
                l.intensity = baseIntensity * (Random.value < 0.15f ? 1f - amount : 1f - amount * 0.1f * Random.value);
            }
        }
    }

    /// <summary>
    /// 猫の移動用の道しるべグラフ（部屋の中の点と出入口）。
    /// </summary>
    public class CatGraph
    {
        public readonly List<Vector3> nodes = new List<Vector3>();
        public readonly List<string> nodeRoom = new List<string>();
        public readonly List<List<int>> adj = new List<List<int>>();
        readonly Dictionary<string, int> byName = new Dictionary<string, int>();

        public int Add(string name, string room, float x, float z)
        {
            int i = nodes.Count;
            nodes.Add(new Vector3(x, 0f, z));
            nodeRoom.Add(room);
            adj.Add(new List<int>());
            byName[name] = i;
            return i;
        }

        public void Link(string a, string b)
        {
            int ia = byName[a], ib = byName[b];
            if (!adj[ia].Contains(ib)) adj[ia].Add(ib);
            if (!adj[ib].Contains(ia)) adj[ib].Add(ia);
        }

        public int Nearest(Vector3 p, string room)
        {
            int best = -1; float bd = float.MaxValue;
            for (int i = 0; i < nodes.Count; i++)
            {
                if (room != null && nodeRoom[i] != room) continue;
                float d = (new Vector2(nodes[i].x - p.x, nodes[i].z - p.z)).sqrMagnitude;
                if (d < bd) { bd = d; best = i; }
            }
            if (best < 0 && room != null) return Nearest(p, null);
            return best;
        }

        /// <summary>ダイクストラ法。avoid に近い点はコストを上げる（主人公を避けて逃げる）</summary>
        public List<int> Path(int from, int to, Vector3 avoid, float avoidWeight)
        {
            int n = nodes.Count;
            var dist = new float[n];
            var prev = new int[n];
            var done = new bool[n];
            for (int i = 0; i < n; i++) { dist[i] = float.MaxValue; prev[i] = -1; }
            dist[from] = 0f;
            for (int iter = 0; iter < n; iter++)
            {
                int u = -1; float bd = float.MaxValue;
                for (int i = 0; i < n; i++) if (!done[i] && dist[i] < bd) { bd = dist[i]; u = i; }
                if (u < 0) break;
                done[u] = true;
                if (u == to) break;
                foreach (int v in adj[u])
                {
                    float w = Vector3.Distance(nodes[u], nodes[v]);
                    if (avoidWeight > 0f)
                    {
                        float da = Vector3.Distance(new Vector3(nodes[v].x, 0, nodes[v].z), new Vector3(avoid.x, 0, avoid.z));
                        if (da < 2.5f) w += (2.5f - da) * avoidWeight;
                    }
                    if (dist[u] + w < dist[v]) { dist[v] = dist[u] + w; prev[v] = u; }
                }
            }
            var path = new List<int>();
            if (dist[to] == float.MaxValue) return path;
            for (int c = to; c >= 0; c = prev[c]) path.Insert(0, c);
            return path;
        }

        public float PathLength(List<int> p)
        {
            float l = 0f;
            for (int i = 1; i < p.Count; i++) l += Vector3.Distance(nodes[p[i - 1]], nodes[p[i]]);
            return l;
        }
    }

    /// <summary>足もとの丸い影。下の床や家具の上に貼りつく。</summary>
    public class BlobShadow : MonoBehaviour
    {
        Transform owner;
        float baseSize;

        public static void Attach(Transform owner, float size, float alpha, float length = -1f)
        {
            var mat = Mat.Unlit("blob" + alpha.ToString("0.00"), Tex.SoftDot(new Color(0, 0, 0, 1)), new Color(1, 1, 1, alpha));
            var q = Geo.Quad(owner, "足もとの影", Vector3.zero, new Vector2(size, length > 0 ? length * 2.4f : size), new Vector3(90f, 0f, 0f), mat);
            var r = q.GetComponent<Renderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            q.layer = 2;
            var b = q.AddComponent<BlobShadow>();
            b.owner = owner;
            b.baseSize = size;
        }

        void LateUpdate()
        {
            if (owner == null) return;
            // 真下の面に合わせる（ジャンプ中は薄く小さく）
            RaycastHit hit;
            Vector3 o = owner.position + Vector3.up * 0.3f;
            float y = owner.position.y;
            if (Physics.Raycast(o, Vector3.down, out hit, 3f, ~(1 << 2), QueryTriggerInteraction.Ignore)) y = hit.point.y;
            var p = transform.position;
            p.y = y + 0.008f;
            transform.position = p;
            transform.rotation = Quaternion.Euler(90f, owner.eulerAngles.y, 0f);
            float h = Mathf.Clamp01((owner.position.y - y) / 1.2f);
            transform.localScale = new Vector3(transform.localScale.x, transform.localScale.y, 1f);
            var r = GetComponent<Renderer>();
            if (r != null) r.enabled = h < 0.95f;
        }
    }
}
