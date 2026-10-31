using System.Collections.Generic;
using UnityEngine;

namespace MidnightCalico
{
    /// <summary>室内の時計もゲーム内の午前2時〜6時に合わせて進む。</summary>
    public class InteriorClock : MonoBehaviour
    {
        public Transform hourHand, minuteHand;
        void LateUpdate()
        {
            float minutes = GameManager.I != null ? GameManager.I.GameMinutes : 120f;
            if (hourHand != null) hourHand.localRotation = Quaternion.Euler(0, 0, -minutes * 0.5f);
            if (minuteHand != null) minuteHand.localRotation = Quaternion.Euler(0, 0, -minutes * 6f);
        }
    }

    /// <summary>突然の地震と、ミケが実際にコンロへ移動して起こすいたずら。</summary>
    public class NightEvents : MonoBehaviour
    {
        GameManager gm;
        SearchPoint stove;
        GameObject flames;
        Transform[] burners = new Transform[2];
        AudioSource gas;
        AudioClip rumble, gasClip;
        Mesh flameMesh;
        float nextQuake, quakeEnd, nextStove, litAt, nextWarning;
        bool scorched;
        public const float QuakeDuration = 20f;
        float nextFall;
        bool quakeAnnounced;
        readonly HashSet<Prop> quakeProps = new HashSet<Prop>();
        AudioSource quakeAudio;
        public bool Quaking { get { return gm != null && quakeEnd > gm.elapsed; } }
        public float QuakeSecondsLeft { get { return gm != null ? Mathf.Max(0f, quakeEnd - gm.elapsed) : 0f; } }
        public bool StoveOn { get; private set; }
        public float QuakeStrength { get { return gm != null ? Mathf.Clamp01((quakeEnd - gm.elapsed) / 1.5f) : 0f; } }

        public void Init(GameManager manager)
        {
            gm = manager;
            foreach (var point in gm.house.searches) if (point.name == "コンロ") { stove = point; break; }
            nextQuake = Random.Range(45f, 75f);
            nextStove = Random.Range(65f, 95f);
            rumble = MakeNoise("家の地鳴り", 4.5f, false);
            quakeAudio = gameObject.AddComponent<AudioSource>();
            quakeAudio.clip = rumble; quakeAudio.loop = true; quakeAudio.playOnAwake = false; quakeAudio.volume = 0.5f;
            gasClip = MakeNoise("コンロの燃焼音", 1f, true);
            flames = new GameObject("コンロの青い炎");
            flames.transform.SetParent(gm.house.root, false);
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            // 五徳の周囲に小さな円錐の炎。全てを1メッシュにまとめる。
            for (int i = 0; i < 10; i++)
            {
                float a = i * Mathf.PI * 2f / 10f;
                Vector3 center = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 0.075f;
                int start = vertices.Count;
                for (int j = 0; j < 4; j++)
                {
                    float b = j * Mathf.PI * 0.5f;
                    vertices.Add(center + new Vector3(Mathf.Cos(b), 0f, Mathf.Sin(b)) * 0.023f);
                }
                vertices.Add(center + Vector3.up * (0.1f + (i % 3) * 0.014f));
                for (int j = 0; j < 4; j++) { triangles.Add(start+j); triangles.Add(start+4); triangles.Add(start+(j+1)%4); }
            }
            flameMesh = new Mesh { name = "バーナーの炎" };
            flameMesh.SetVertices(vertices); flameMesh.SetTriangles(triangles, 0); flameMesh.RecalculateNormals();
            var material = Mat.Emissive("stoveFlame", new Color(0.06f, 0.35f, 1f), new Color(0.12f, 0.65f, 2f));
            for (int i = 0; i < burners.Length; i++)
            {
                var burner = new GameObject("青い炎" + i);
                burner.transform.SetParent(flames.transform, false);
                burner.transform.localPosition = new Vector3(i == 0 ? 14.12f : 14.58f, 0.94f, 0.37f);
                burner.AddComponent<MeshFilter>().sharedMesh = flameMesh;
                var renderer = burner.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                burners[i] = burner.transform;
            }
            gas = flames.AddComponent<AudioSource>();
            gas.clip = gasClip; gas.loop = true; gas.playOnAwake = false;
            gas.spatialBlend = 1f; gas.volume = 0.32f; gas.minDistance = 0.6f; gas.maxDistance = 8f;
            gas.rolloffMode = AudioRolloffMode.Linear;
            // 音源はバーナーの位置。炎のワールド座標は保つ。
            flames.transform.position = new Vector3(14.35f, 0.94f, 0.37f);
            for (int i = 0; i < burners.Length; i++) burners[i].position = new Vector3(i == 0 ? 14.12f : 14.58f, 0.94f, 0.37f);
            flames.SetActive(false);
        }

        void Update()
        {
            if (gm == null) return;
            bool playing = gm.state == GState.Play && !gm.caught;
            if (gas != null)
            {
                if (playing && StoveOn && !gas.isPlaying) gas.Play();
                else if (!playing && gas.isPlaying) gas.Pause();
            }
            if (quakeAudio != null)
            {
                if (playing && Quaking) { if (!quakeAudio.isPlaying) quakeAudio.UnPause(); if (!quakeAudio.isPlaying) quakeAudio.Play(); quakeAudio.volume = 0.5f * QuakeStrength; }
                else if (Quaking) quakeAudio.Pause();
                else quakeAudio.Stop();
            }
            if (!playing) return;
            if (Quaking && gm.elapsed >= nextFall)
            {
                DropQuakeProps(); nextFall = gm.elapsed + 2.5f;
            }
            else if (!Quaking && quakeAnnounced)
            {
                quakeAnnounced = false;
                gm.Toast(gm.player.IsSheltered ? "揺れがおさまった。A／調べるでテーブルの外へ。" : "……揺れがおさまった。床の小物に気をつけよう。");
            }
            if (gm.elapsed >= nextQuake) TriggerQuake();
            if (!StoveOn && gm.elapsed >= nextStove && stove != null)
            {
                // 食事・隠れ・逃走を上書きしない。落ち着いた時に再試行。
                bool started = gm.cat != null && gm.cat.VisitStove();
                nextStove = gm.elapsed + (started ? Random.Range(100f, 150f) : 8f);
            }
            if (!StoveOn) return;
            for (int i = 0; i < burners.Length; i++)
                burners[i].localScale = new Vector3(1f, 0.85f + 0.22f * Mathf.Sin(gm.elapsed * 25f + i * 2f), 1f);
            if (!scorched && gm.elapsed - litAt >= 45f)
            {
                scorched = true;
                gm.RecordDamage("コンロの焦げ跡", stove.pos);
                gm.Toast("焦げ臭い……！ キッチンのコンロを調べて消火しよう。");
                nextWarning = gm.elapsed + 15f;
            }
            else if (gm.elapsed >= nextWarning)
            {
                gm.Toast("キッチンのコンロに火がついている！ コンロを調べて消火。");
                nextWarning = gm.elapsed + 15f;
            }
        }

        public void TriggerQuake()
        {
            if (gm == null || gm.state != GState.Play || gm.caught) return;
            quakeEnd = gm.elapsed + QuakeDuration;
            quakeAnnounced = true; quakeProps.Clear(); nextFall = gm.elapsed + 3f;
            nextQuake = gm.elapsed + Random.Range(110f, 180f);
            gm.Toast("ガタガタ……！ 地震だ！ キッチンのテーブル脇でA／調べる → 下に隠れる！");
            if (quakeAudio != null) quakeAudio.Play();
            if (gm.cat != null && gm.cat.state != CatState.Caught) gm.cat.StartFlee(20f);
        }

        public int DropQuakeProps()
        {
            if (gm == null || gm.state != GState.Play || !Quaking || quakeProps.Count >= 10) return 0;
            string room = gm.house.RoomName(gm.player.transform.position);
            // 見えている部屋の高い小物から、棚の縁を越える方向へ押し出す。
            Prop best = null; float highest = 0.35f;
            foreach (var prop in Prop.all)
            {
                if (prop == null || quakeProps.Contains(prop) || gm.house.RoomName(prop.transform.position) != room) continue;
                if (prop.transform.position.y > highest) { highest = prop.transform.position.y; best = prop; }
            }
            if (best == null) return 0;
            quakeProps.Add(best);
            var r = gm.house.RoomAt(best.transform.position);
            Vector3 direction = r != null ? r.Center - best.transform.position : Vector3.forward;
            direction.y = 0;
            best.QuakeFall(direction);
            return 1;
        }

        public void IgniteFromCat(CatAI cat)
        {
            if (gm == null || gm.state != GState.Play || gm.caught || StoveOn || stove == null || cat != gm.cat) return;
            // タイマーだけで点火しない。ミケが天板のスイッチに触れた位置か確認。
            if (Vector3.Distance(cat.transform.position, new Vector3(14.35f, 0.92f, 0.65f)) > 0.3f) return;
            StoveOn = true; scorched = false; litAt = gm.elapsed; nextWarning = gm.elapsed + 12f;
            stove.name = "コンロ（消火）";
            flames.SetActive(true); gas.Play();
            if (AudioMan.I != null) AudioMan.I.PlayAt(Sfx.Cursor, cat.transform.position, 0.8f, 0.7f);
            gm.Toast("カチッ、ボッ……！ ミケがコンロに触って火がついた！ 調べて消火！");
        }

        public bool TryExtinguish(SearchPoint point)
        {
            if (!StoveOn || point != stove) return false;
            StoveOn = false; gas.Stop(); flames.SetActive(false);
            stove.name = "コンロ";
            nextStove = gm.elapsed + Random.Range(100f, 150f);
            gm.Toast("コンロの火を消した。……よし、火の元は大丈夫。");
            if (AudioMan.I != null) AudioMan.I.Play2D(Sfx.Cursor, 0.65f, 0.8f);
            return true;
        }

        static AudioClip MakeNoise(string name, float seconds, bool burning)
        {
            const int rate = 22050;
            var data = new float[Mathf.RoundToInt(rate * seconds)];
            float smooth = 0f;
            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)rate;
                smooth = Mathf.Lerp(smooth, Random.Range(-1f, 1f), burning ? 0.55f : 0.035f);
                float env = burning ? 1f : Mathf.Sin(Mathf.PI * t / seconds);
                data[i] = (smooth * (burning ? 0.18f : 0.7f) + (burning ? 0f : Mathf.Sin(t * 2f * Mathf.PI * 43f) * 0.18f)) * env;
            }
            var clip = AudioClip.Create(name, data.Length, 1, rate, false);
            clip.SetData(data, 0); return clip;
        }

        void OnDestroy()
        {
            if (flameMesh != null) Destroy(flameMesh);
            if (rumble != null) Destroy(rumble);
            if (gasClip != null) Destroy(gasClip);
        }
    }

    public class TableShelter
    {
        public Vector3 entrance, inside;
        public TableShelter(Vector3 entrance, Vector3 inside) { this.entrance = entrance; this.inside = inside; }
    }

    public class Room
    {
        public string name;
        public Rect rect;          // x = X座標, y = Z座標
        public float floorY;
        public bool stairs;
        public Material wallMat;
        public Material floorMat;
        public Room(string n, float x0, float z0, float x1, float z1)
        {
            name = n;
            rect = Rect.MinMaxRect(x0, z0, x1, z1);
        }
        public bool Contains(Vector3 p)
        {
            if (!rect.Contains(new Vector2(p.x,p.z))) return false;
            if (stairs) return p.y >= HouseBuilder.StairHeight(p.z) - 0.45f && p.y < HouseBuilder.StairHeight(p.z) + 1f;
            return p.y >= floorY - 0.4f && p.y < floorY + 2.6f;
        }
        public Vector3 Center { get { return new Vector3(rect.center.x, floorY, rect.center.y); } }
    }

    public class CamZone
    {
        public string room;
        public Rect rect;
        public Vector3 pos;
        public Vector3 look;
        public float minY = -0.5f, maxY = 2.6f;
        public int priority;
        public float track;   // 0 = 完全固定, 1 = 主人公を追う
        public float fov;
        public CamZone(string room, float x0, float z0, float x1, float z1, Vector3 pos, Vector3 look, float track = 0.3f, float fov = 55f)
        {
            this.room = room;
            rect = Rect.MinMaxRect(x0, z0, x1, z1);
            this.pos = pos; this.look = look; this.track = track; this.fov = fov;
        }
        public bool HeightContains(Vector3 p) { return p.y >= minY && p.y < maxY; }
        public bool Contains(Vector3 p) { return HeightContains(p) && rect.Contains(new Vector2(p.x, p.z)); }
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
                if (GameManager.I != null) GameManager.I.RecordDamage(gameObject.name, transform.position);
            }
        }

        bool earthquakeFall;
        float earthquakeHitUntil;
        public bool IsEarthquakeFall { get { return earthquakeFall; } }
        public void QuakeFall(Vector3 direction)
        {
            if (rb == null) return;
            earthquakeFall = true;
            earthquakeHitUntil = Time.time + 4f;
            rb.WakeUp();
            rb.AddForce((direction.normalized * 1.7f + Vector3.up * 0.12f) * rb.mass, ForceMode.Impulse);
            rb.AddTorque(Vector3.Cross(Vector3.up, direction.normalized) * 0.8f * rb.mass, ForceMode.Impulse);
            if (!counted)
            {
                counted = true;
                if (GameManager.I != null) GameManager.I.RecordDamage(gameObject.name + "（地震で落下）", transform.position);
            }
        }

        void FixedUpdate()
        {
            var gm = GameManager.I;
            if (!earthquakeFall || Time.time > earthquakeHitUntil || rb == null || gm == null || gm.state != GState.Play || gm.player == null) return;
            // 静止中のCharacterControllerにも、落下する剛体の接触を届ける。
            if (rb.linearVelocity.magnitude < 1.2f) return;
            var collider = GetComponent<Collider>();
            var controller = gm.player.GetComponent<CharacterController>();
            if (collider == null || controller == null) return;
            Bounds sweep = collider.bounds;
            sweep.Encapsulate(sweep.center + rb.linearVelocity * Time.fixedDeltaTime);
            sweep.Expand(0.06f);
            if (sweep.Intersects(controller.bounds)) gm.player.HitByFallingProp();
        }

        void OnCollisionEnter(Collision c)
        {
            if (earthquakeFall && c.relativeVelocity.magnitude > 1.2f)
            {
                var player = c.gameObject.GetComponentInParent<PlayerController>();
                if (player != null) player.HitByFallingProp();
            }
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

        /// <summary>ダイクストラ法。avoid に近い点はコストを上げる（主人公を避けて逃げる）。
        /// roomCamPos を渡すと、各部屋の固定カメラにも近づきすぎないようにする
        /// （カメラのニアクリップが浅く、至近距離を横切ると猫が巨大に歪んで見えるため）。</summary>
        public List<int> Path(int from, int to, Vector3 avoid, float avoidWeight, Dictionary<string, Vector3> roomCamPos = null, float camWeight = 0f)
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
                    if (camWeight > 0f && roomCamPos != null)
                    {
                        Vector3 camP;
                        if (roomCamPos.TryGetValue(nodeRoom[v], out camP))
                        {
                            float dc = Vector3.Distance(new Vector3(nodes[v].x, 0, nodes[v].z), new Vector3(camP.x, 0, camP.z));
                            if (dc < 2.0f) w += (2.0f - dc) * camWeight;
                        }
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
