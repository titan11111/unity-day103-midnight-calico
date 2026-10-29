using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MidnightCalico
{
    /// <summary>
    /// 一軒家（平屋 16m×14m）をまるごと組み立てる。
    /// リビング / 和室 / 廊下・玄関 / キッチン / トイレ / 脱衣所 / 浴室 / 寝室
    /// 座標: X が東、Z が北。単位はメートル。
    /// </summary>
    public class House
    {
        public Transform root;
        public readonly List<Room> rooms = new List<Room>();
        public readonly List<CamZone> zones = new List<CamZone>();
        public readonly List<HideSpot> hides = new List<HideSpot>();
        public readonly List<SearchPoint> searches = new List<SearchPoint>();
        public readonly List<LightSwitch> switches = new List<LightSwitch>();
        public readonly CatGraph graph = new CatGraph();
        public Vector3 playerSpawn = new Vector3(7f, 0f, 2.4f);
        public Vector3 catSpawn = new Vector3(2.2f, 0f, 5.6f);

        public Room RoomAt(Vector3 p)
        {
            foreach (var r in rooms) if (r.Contains(p)) return r;
            return null;
        }
        public string RoomName(Vector3 p)
        {
            var r = RoomAt(p);
            return r != null ? r.name : "";
        }
    }

    public static class HouseBuilder
    {
        const float H = 2.5f;      // 天井高
        const float T = 0.12f;     // 壁厚
        const float DoorH = 2.05f;

        static House h;
        static Transform root;
        static Transform propRoot;   // 動く小物（静的バッチングの対象外）

        // 質感
        static Material woodLight, woodDark, woodWhite, woodFloorDark, sofaFab, rugFab, blanketFab, futonFab, sheetFab, pillowFab;
        static Material metal, blackPl, ceramic, glassWin, tvScreen, shade, plant, pot, mirror, counterTop, lidMat, shojiPaper, wicker, stoneFloor, extMat, ceilingMat, frameMat, bookA, bookB, bookC, bookD, fruit, sisal, beige;

        public static House Build()
        {
            h = new House();
            root = new GameObject("House").transform;
            h.root = root;
            propRoot = new GameObject("Props").transform;
            CreateMaterials();
            DefineRooms();
            BuildFloorsAndCeiling();
            BuildWalls();
            BuildLiving();
            BuildWashitsu();
            BuildCorridor();
            BuildKitchen();
            BuildToilet();
            BuildDatsuijo();
            BuildBath();
            BuildBedroom();
            BuildDetails();
            BuildContactShadows();
            DefineCameraZones();
            DefineGraph();
            foreach (var s in h.hides)
            {
                s.room = h.RoomName(s.approach);
                s.node = h.graph.Nearest(s.approach, s.room);
            }
            // 動かない家具・壁をまとめて描画を軽くする（WebGL・スマホ向け）
            try { StaticBatchingUtility.Combine(root.gameObject); }
            catch (System.Exception e) { Debug.LogWarning("[MidnightCalico] static batching skipped: " + e.Message); }
            return h;
        }

        // ───────────────────────── 質感 ─────────────────────────
        static void CreateMaterials()
        {
            woodLight = Mat.WithNormal(Mat.Make("woodLight", Color.white, Tex.Wood(new Color(0.80f, 0.62f, 0.42f), new Color(0.52f, 0.35f, 0.20f), 1.3f), 0.35f), 4.0f);
            woodDark = Mat.WithNormal(Mat.Make("woodDark", Color.white, Tex.Wood(new Color(0.42f, 0.27f, 0.16f), new Color(0.17f, 0.10f, 0.05f), 4.1f), 0.45f), 4.0f);
            woodWhite = Mat.WithNormal(Mat.Make("woodWhite", new Color(0.86f, 0.85f, 0.81f), Tex.Plaster(Color.white, 0.08f), 0.35f), 3.0f);
            woodFloorDark = Mat.WithNormal(Mat.Make("floorDark", Color.white, Tex.Planks(new Color(0.48f, 0.33f, 0.21f), new Color(0.25f, 0.16f, 0.09f), 2.2f), 0.55f), 5.0f);
            sofaFab = Mat.WithNormal(Mat.Make("sofa", Color.white, Tex.Fabric(new Color(0.33f, 0.38f, 0.31f), 0.25f), 0.05f), 2.5f);
            rugFab = Mat.WithNormal(Mat.Make("rug", Color.white, Tex.Fabric(new Color(0.48f, 0.17f, 0.14f), 0.4f), 0.02f, 0f, 3f, 3f), 2.5f);
            blanketFab = Mat.WithNormal(Mat.Make("blanket", Color.white, Tex.Fabric(new Color(0.30f, 0.38f, 0.52f), 0.3f), 0.05f), 2.5f);
            futonFab = Mat.WithNormal(Mat.Make("kotatsuFuton", Color.white, Tex.Fabric(new Color(0.55f, 0.20f, 0.17f), 0.5f), 0.05f, 0f, 2f, 2f), 2.5f);
            sheetFab = Mat.WithNormal(Mat.Make("sheet", Color.white, Tex.Fabric(new Color(0.90f, 0.89f, 0.86f), 0.15f), 0.05f), 2.5f);
            pillowFab = Mat.WithNormal(Mat.Make("pillow", Color.white, Tex.Fabric(new Color(0.85f, 0.82f, 0.74f), 0.2f), 0.05f), 2.5f);
            beige = Mat.WithNormal(Mat.Make("beigeFab", Color.white, Tex.Fabric(new Color(0.78f, 0.70f, 0.55f), 0.3f), 0.05f), 2.5f);
            metal = Mat.Make("metal", new Color(0.72f, 0.72f, 0.74f), null, 0.75f, 0.85f);
            blackPl = Mat.Make("blackPlastic", new Color(0.05f, 0.05f, 0.06f), null, 0.55f);
            ceramic = Mat.Make("ceramic", new Color(0.95f, 0.95f, 0.93f), null, 0.88f);
            glassWin = Mat.Emissive("nightWindow", new Color(0.02f, 0.03f, 0.06f), new Color(0.06f, 0.10f, 0.22f));
            tvScreen = Mat.Emissive("tvScreen", new Color(0.02f, 0.02f, 0.03f), new Color(0.015f, 0.02f, 0.04f));
            shade = Mat.Emissive("lampShade", new Color(0.92f, 0.82f, 0.62f), new Color(1.0f, 0.68f, 0.34f) * 0.9f);
            plant = Mat.Make("plant", new Color(0.13f, 0.28f, 0.11f), null, 0.3f);
            pot = Mat.Make("pot", new Color(0.62f, 0.32f, 0.20f), null, 0.15f);
            mirror = Mat.Make("mirror", new Color(0.55f, 0.6f, 0.65f), null, 0.97f, 0.95f);
            counterTop = Mat.Make("counterTop", new Color(0.58f, 0.58f, 0.57f), null, 0.75f, 0.6f);
            lidMat = Mat.Make("bathLid", new Color(0.70f, 0.74f, 0.74f), null, 0.5f);
            shojiPaper = Mat.Emissive("shoji", new Color(0.86f, 0.84f, 0.78f), new Color(0.10f, 0.12f, 0.17f));
            wicker = Mat.WithNormal(Mat.Make("wicker", Color.white, Tex.Fabric(new Color(0.62f, 0.48f, 0.30f), 0.6f), 0.1f, 0f, 3f, 3f), 2.5f);
            stoneFloor = Mat.WithNormal(Mat.Make("genkanStone", Color.white, Tex.Tile(new Color(0.30f, 0.29f, 0.28f), new Color(0.16f, 0.16f, 0.15f), 3, 0.3f), 0.4f), 6.0f);
            extMat = Mat.Make("exterior", new Color(0.2f, 0.2f, 0.2f));
            ceilingMat = Mat.WithNormal(Mat.Make("ceiling", new Color(0.86f, 0.85f, 0.82f), Tex.Plaster(Color.white, 0.12f), 0.1f, 0f, 6f, 6f), 3.0f);
            frameMat = woodLight;
            bookA = Mat.Make("bookA", new Color(0.45f, 0.10f, 0.08f), null, 0.3f);
            bookB = Mat.Make("bookB", new Color(0.10f, 0.20f, 0.35f), null, 0.3f);
            bookC = Mat.Make("bookC", new Color(0.75f, 0.68f, 0.50f), null, 0.3f);
            bookD = Mat.Make("bookD", new Color(0.15f, 0.28f, 0.16f), null, 0.3f);
            fruit = Mat.Make("mikan", new Color(0.95f, 0.50f, 0.08f), null, 0.5f);
            sisal = Mat.WithNormal(Mat.Make("sisal", Color.white, Tex.Fabric(new Color(0.70f, 0.58f, 0.40f), 0.7f), 0.05f, 0f, 1f, 6f), 2.5f);
        }

        // ───────────────────────── 部屋 ─────────────────────────
        static void DefineRooms()
        {
            var wpLiving = Mat.WithNormal(Mat.Make("wpLiving", Color.white, Tex.Wallpaper(new Color(0.82f, 0.76f, 0.64f), 0.06f), 0.1f, 0f, 3f, 1f), 2.0f);
            var wpCorr = Mat.WithNormal(Mat.Make("wpCorridor", Color.white, Tex.Wallpaper(new Color(0.70f, 0.64f, 0.52f), 0.08f), 0.1f, 0f, 3f, 1f), 2.0f);
            var wpKitchen = Mat.WithNormal(Mat.Make("wpKitchen", Color.white, Tex.Wallpaper(new Color(0.86f, 0.84f, 0.76f), 0.03f), 0.15f, 0f, 3f, 1f), 2.0f);
            var wpBed = Mat.WithNormal(Mat.Make("wpBed", Color.white, Tex.Wallpaper(new Color(0.66f, 0.70f, 0.72f), 0.07f), 0.1f, 0f, 3f, 1f), 2.0f);
            var sandWall = Mat.WithNormal(Mat.Make("sandWall", Color.white, Tex.Plaster(new Color(0.74f, 0.62f, 0.44f), 0.35f), 0.05f, 0f, 2f, 2f), 3.0f);
            var tileWall = Mat.WithNormal(Mat.Make("tileWall", Color.white, Tex.Tile(new Color(0.88f, 0.89f, 0.88f), new Color(0.62f, 0.63f, 0.62f), 8, 0.06f), 0.8f, 0f, 2f, 2f), 6.0f);

            var planks = Mat.WithNormal(Mat.Make("planks", Color.white, Tex.Planks(new Color(0.72f, 0.54f, 0.36f), new Color(0.42f, 0.28f, 0.16f), 0.7f), 0.5f), 5.0f);
            var kitchenFloor = Mat.WithNormal(Mat.Make("cushionFloor", Color.white, Tex.Tile(new Color(0.78f, 0.74f, 0.64f), new Color(0.62f, 0.58f, 0.50f), 4, 0.15f), 0.45f), 6.0f);
            var toiletFloor = Mat.WithNormal(Mat.Make("toiletFloor", Color.white, Tex.Tile(new Color(0.80f, 0.80f, 0.78f), new Color(0.5f, 0.5f, 0.5f), 4, 0.08f), 0.6f), 6.0f);
            var bathFloor = Mat.WithNormal(Mat.Make("bathFloor", Color.white, Tex.Tile(new Color(0.32f, 0.38f, 0.42f), new Color(0.18f, 0.2f, 0.22f), 8, 0.1f), 0.75f), 6.0f);
            var carpet = Mat.WithNormal(Mat.Make("carpet", Color.white, Tex.Fabric(new Color(0.42f, 0.38f, 0.34f), 0.35f), 0.02f), 2.5f);
            var tatami = Mat.WithNormal(Mat.Make("tatami", Color.white, Tex.Tatami(true), 0.15f), 4.0f);

            AddRoom("リビング", 0, 0, 6, 7, wpLiving, planks);
            AddRoom("和室", 0, 7, 6, 14, sandWall, tatami);
            AddRoom("廊下", 6, 0, 8, 14, wpCorr, woodFloorDark);
            AddRoom("キッチン", 8, 0, 16, 5, wpKitchen, kitchenFloor);
            AddRoom("トイレ", 8, 5, 11, 7, tileWall, toiletFloor);
            AddRoom("脱衣所", 8, 7, 11, 9.5f, wpKitchen, kitchenFloor);
            AddRoom("浴室", 11, 5, 16, 9.5f, tileWall, bathFloor);
            AddRoom("寝室", 8, 9.5f, 16, 14, wpBed, carpet);
        }

        static void AddRoom(string n, float x0, float z0, float x1, float z1, Material wall, Material floor)
        {
            var r = new Room(n, x0, z0, x1, z1);
            r.wallMat = wall;
            r.floorMat = floor;
            h.rooms.Add(r);
        }

        static void BuildFloorsAndCeiling()
        {
            foreach (var r in h.rooms)
            {
                float w = r.rect.width, d = r.rect.height;
                Material fm;
                if (r.name == "和室") fm = Mat.Tiled(r.floorMat, w / 0.9f, d / 1.8f);
                else if (r.name == "廊下") fm = Mat.Tiled(r.floorMat, w / 1.2f, d / 2.4f);
                else fm = Mat.Tiled(r.floorMat, w / 2f, d / 2f);
                float z0 = r.rect.yMin;
                if (r.name == "廊下")
                {
                    // 玄関の土間（一段下がる）
                    Geo.BoxMM(root, "玄関土間", 6f, -0.35f, 0f, 8f, -0.15f, 1.6f, Mat.Tiled(stoneFloor, 2f, 1.6f));
                    Geo.BoxMM(root, "上がり框", 6f, -0.15f, 1.55f, 8f, 0.0f, 1.65f, woodDark);
                    z0 = 1.6f;
                }
                Geo.BoxMM(root, r.name + "_床", r.rect.xMin, -0.2f, z0, r.rect.xMax, 0f, r.rect.yMax, fm);
            }
            var ceil = Geo.BoxMM(root, "天井", 0f, H, 0f, 16f, H + 0.1f, 14f, ceilingMat, false);
            ceil.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            // 外の地面（窓の外が真っ黒にならないように）
            Geo.BoxMM(root, "外構", -10f, -0.5f, -10f, 26f, -0.4f, 24f, extMat, false);
        }

        // ───────────────────────── 壁 ─────────────────────────
        static void BuildWalls()
        {
            // 外周
            WallLine(true, 0f, 0f, 14f);
            WallLine(true, 16f, 0f, 14f);
            WallLine(false, 0f, 0f, 16f, 6.3f, 7.7f);
            WallLine(false, 14f, 0f, 16f);
            // 内壁
            WallLine(true, 6f, 0f, 14f, 2.6f, 4.0f, 10f, 11f);
            WallLine(true, 8f, 0f, 14f, 2f, 3.2f, 5.5f, 6.5f, 7.6f, 8.6f, 11.2f, 12.4f);
            WallLine(false, 7f, 0f, 6f, 2.2f, 3.8f);
            WallLine(false, 5f, 8f, 16f);
            WallLine(false, 7f, 8f, 11f);
            WallLine(false, 9.5f, 8f, 16f);
            WallLine(true, 11f, 5f, 9.5f, 7.8f, 8.8f);

            // 開いたドア
            DoorPanel(true, 6f, 2.6f, 4.0f, -1, "リビングのドア");
            DoorPanel(true, 8f, 2f, 3.2f, +1, "キッチンのドア");
            DoorPanel(true, 8f, 5.5f, 6.5f, -1, "トイレのドア");
            DoorPanel(true, 8f, 7.6f, 8.6f, -1, "脱衣所のドア");
            DoorPanel(true, 8f, 11.2f, 12.4f, +1, "寝室のドア");
            DoorPanel(true, 11f, 7.8f, 8.8f, +1, "浴室の折れ戸");
            // 和室の襖（開いている）
            FusumaPanel(new Vector3(6f - 0.07f, 0f, 10f), true, 1.0f, false);
            FusumaPanel(new Vector3(4.6f, 0f, 7f + 0.07f), false, 1.6f, true);
        }

        /// <summary>
        /// 壁の線。alongZ = true なら x = c の線（Z 方向にのびる）。
        /// gaps は (開始, 終了) のペア。両側の部屋に合わせて内装材を変える。
        /// </summary>
        static void WallLine(bool alongZ, float c, float a0, float a1, params float[] gaps)
        {
            // 開口部以外の区間
            var solids = new List<Vector2>();
            float cur = a0;
            for (int i = 0; i + 1 < gaps.Length; i += 2)
            {
                if (gaps[i] > cur) solids.Add(new Vector2(cur, gaps[i]));
                cur = gaps[i + 1];
            }
            if (cur < a1) solids.Add(new Vector2(cur, a1));

            // 部屋の境目で分割（両面で別の内装にするため）
            var breaks = new List<float>();
            foreach (var r in h.rooms)
            {
                breaks.Add(alongZ ? r.rect.yMin : r.rect.xMin);
                breaks.Add(alongZ ? r.rect.yMax : r.rect.xMax);
            }
            breaks.Sort();
            var pieces = new List<Vector2>();
            foreach (var s in solids)
            {
                float st = s.x;
                foreach (var b in breaks)
                {
                    if (b > st + 0.01f && b < s.y - 0.01f) { pieces.Add(new Vector2(st, b)); st = b; }
                }
                pieces.Add(new Vector2(st, s.y));
            }

            foreach (var p in pieces) WallPiece(alongZ, c, p.x, p.y, -0.2f, H);
            // 開口部の上（垂れ壁）と枠
            for (int i = 0; i + 1 < gaps.Length; i += 2)
            {
                WallPiece(alongZ, c, gaps[i], gaps[i + 1], DoorH, H);
                DoorFrame(alongZ, c, gaps[i], gaps[i + 1]);
            }
        }

        static void WallPiece(bool alongZ, float c, float s, float e, float y0, float y1)
        {
            float mid = (s + e) * 0.5f;
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 sample = alongZ ? new Vector3(c + side * 0.3f, 0, mid) : new Vector3(mid, 0, c + side * 0.3f);
                Room r = h.RoomAt(sample);
                Material m = r != null ? r.wallMat : extMat;
                float len = e - s;
                if (r != null) m = Mat.Tiled(m, Mathf.Max(0.5f, len / 2f), (y1 - y0) / 2.5f);
                float o0 = c + (side < 0 ? -T * 0.5f : 0f), o1 = c + (side < 0 ? 0f : T * 0.5f);
                GameObject g = alongZ
                    ? Geo.BoxMM(root, "壁", o0, y0, s, o1, y1, e, m)
                    : Geo.BoxMM(root, "壁", s, y0, o0, e, y1, o1, m);
                if (r == null) g.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                // 壁ぎわの床に落ちる陰（アンビエントオクルージョンの代わり）
                if (r != null && y0 < 0.01f)
                {
                    float fy = (r.name == "廊下" && s < 1.6f && !alongZ) ? -0.145f : 0.004f;
                    WallShade(alongZ, c + side * T * 0.5f, side, s, e, fy, r.name == "和室" ? 0.4f : 0.5f);
                }
                // 廻り縁（天井との境目）
                if (r != null && y1 > H - 0.01f && r.name != "浴室")
                {
                    float m0 = side < 0 ? c - T * 0.5f - 0.02f : c + T * 0.5f;
                    float m1 = m0 + 0.02f;
                    var crown = r.name == "和室" ? woodLight : woodWhite;
                    if (alongZ) Geo.BoxMM(root, "廻り縁", m0, H - 0.05f, s, m1, H, e, crown, false);
                    else Geo.BoxMM(root, "廻り縁", s, H - 0.05f, m0, e, H, m1, crown, false);
                }
                // 幅木
                if (r != null && y0 < 0.01f && r.name != "浴室" && r.name != "トイレ")
                {
                    float b0 = side < 0 ? c - T * 0.5f - 0.015f : c + T * 0.5f;
                    float b1 = b0 + 0.015f;
                    if (alongZ) Geo.BoxMM(root, "幅木", b0, 0f, s, b1, 0.08f, e, woodDark, false);
                    else Geo.BoxMM(root, "幅木", s, 0f, b0, e, 0.08f, b1, woodDark, false);
                }
            }
        }

        static void DoorFrame(bool alongZ, float c, float g0, float g1)
        {
            float d = T * 0.5f + 0.015f;
            if (alongZ)
            {
                Geo.BoxMM(root, "枠", c - d, 0f, g0, c + d, DoorH, g0 + 0.05f, frameMat, false);
                Geo.BoxMM(root, "枠", c - d, 0f, g1 - 0.05f, c + d, DoorH, g1, frameMat, false);
                Geo.BoxMM(root, "枠", c - d, DoorH - 0.05f, g0, c + d, DoorH, g1, frameMat, false);
            }
            else
            {
                Geo.BoxMM(root, "枠", g0, 0f, c - d, g0 + 0.05f, DoorH, c + d, frameMat, false);
                Geo.BoxMM(root, "枠", g1 - 0.05f, 0f, c - d, g1, DoorH, c + d, frameMat, false);
                Geo.BoxMM(root, "枠", g0, DoorH - 0.05f, c - d, g1, DoorH, c + d, frameMat, false);
            }
        }

        /// <summary>壁に沿って開け放したドア</summary>
        static void DoorPanel(bool alongZ, float c, float g0, float g1, int side, string name)
        {
            float w = g1 - g0 - 0.06f;
            // 蝶番は g0 側。部屋側(+x)へ90度開いて壁に沿う
            float x0 = c + side * (T * 0.5f + 0.01f), x1 = x0 + side * 0.04f;
            var door = Geo.BoxMM(root, name, Mathf.Min(x0, x1), 0f, g0 - w - 0.02f, Mathf.Max(x0, x1), DoorH - 0.08f, g0 - 0.02f, woodLight);
            // ドアノブ
            Geo.Cyl(door.transform.parent, "ドアノブ", new Vector3(x1 + side * 0.03f, 0.95f, g0 - w + 0.08f), 0.025f, 0.06f, metal, false).transform.rotation = Quaternion.Euler(0, 0, 90);
            if (g0 - w - 0.02f < 0.1f) door.SetActive(false);
        }

        static void FusumaPanel(Vector3 p, bool alongZ, float width, bool washitsuSide)
        {
            var paper = Mat.WithNormal(Mat.Make("fusuma", new Color(0.82f, 0.78f, 0.66f), Tex.Plaster(new Color(1f, 0.98f, 0.9f), 0.15f), 0.1f), 3.0f);
            if (alongZ)
                Geo.BoxMM(root, "襖", p.x - 0.02f, 0f, p.z - width - 0.1f, p.x + 0.02f, 1.8f, p.z - 0.1f, paper, false);
            else
                Geo.BoxMM(root, "襖", p.x, 0f, p.z - 0.02f, p.x + width, 1.8f, p.z + 0.02f, paper, false);
        }

        static void QuadFacing(string name, Vector3 center, Vector2 size, Vector3 dir, Material m)
        {
            float yaw = Mathf.Atan2(-dir.x, -dir.z) * Mathf.Rad2Deg;
            Geo.Quad(root, name, center, size, new Vector3(0f, yaw, 0f), m);
        }

        /// <summary>窓（夜の月明かり＋カーテン）。normal は部屋の内側向き。</summary>
        static void Window(Vector3 center, Vector3 normal, float w, float hgt, bool curtains, bool frosted = false)
        {
            bool xNormal = Mathf.Abs(normal.x) > 0.5f;
            Vector3 surf = center + normal * (T * 0.5f + 0.005f);
            var glass = frosted ? Mat.Emissive("frosted", new Color(0.5f, 0.55f, 0.6f), new Color(0.10f, 0.14f, 0.22f)) : glassWin;
            QuadFacing("窓ガラス", surf, new Vector2(w, hgt), normal, glass);
            float fd = 0.06f, ft = 0.05f;
            Vector3 fo = normal * (T * 0.5f + fd * 0.5f);
            // 枠
            void FrameBox(Vector3 c, float sw, float sh)
            {
                Vector3 size = xNormal ? new Vector3(fd, sh, sw) : new Vector3(sw, sh, fd);
                Geo.Box(root, "窓枠", c, size, woodWhite, false);
            }
            Vector3 tan = xNormal ? Vector3.forward : Vector3.right;
            FrameBox(center + fo + Vector3.up * (hgt * 0.5f), w + ft, ft);
            FrameBox(center + fo - Vector3.up * (hgt * 0.5f), w + ft, ft);
            FrameBox(center + fo + tan * (w * 0.5f), ft, hgt);
            FrameBox(center + fo - tan * (w * 0.5f), ft, hgt);
            FrameBox(center + fo, ft * 0.6f, hgt); // 中桟
            if (curtains)
            {
                var cur = Mat.WithNormal(Mat.Make("curtain", Color.white, Tex.Fabric(new Color(0.55f, 0.50f, 0.42f), 0.3f), 0.05f, 0f, 1f, 3f), 2.5f);
                float top = center.y + hgt * 0.5f + 0.2f;
                for (int s = -1; s <= 1; s += 2)
                {
                    Vector3 cc = center + normal * (T * 0.5f + 0.12f) + tan * s * (w * 0.5f + 0.05f);
                    cc.y = (top + 0.05f) * 0.5f;
                    Vector3 size = xNormal ? new Vector3(0.06f, top - 0.05f, 0.4f) : new Vector3(0.4f, top - 0.05f, 0.06f);
                    Geo.Box(root, "カーテン", cc, size, cur, false);
                }
                Vector3 rail = center + normal * (T * 0.5f + 0.12f) + Vector3.up * (top - center.y);
                Geo.Box(root, "カーテンレール", rail, xNormal ? new Vector3(0.04f, 0.04f, w + 1f) : new Vector3(w + 1f, 0.04f, 0.04f), woodWhite, false);
            }
            // 月明かり
            AddLight(center + normal * 0.9f, new Color(0.45f, 0.55f, 0.85f), 0.35f, 3.5f);
        }

        static Light AddLight(Vector3 pos, Color c, float intensity, float range, bool flicker = false, float flickerAmount = 0.6f)
        {
            var g = new GameObject("照明");
            g.transform.SetParent(root, false);
            g.transform.position = pos;
            var l = g.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = c;
            l.intensity = intensity;
            l.range = range;
            l.shadows = LightShadows.None;
            if (flicker)
            {
                var f = g.AddComponent<Flicker>();
                f.baseIntensity = intensity;
                f.amount = flickerAmount;
            }
            return l;
        }

        /// <summary>見えない当たり判定だけの箱</summary>
        static void ColliderBox(string name, float x0, float y0, float z0, float x1, float y1, float z1)
        {
            var g = new GameObject(name + "_col");
            g.transform.SetParent(root, false);
            g.transform.position = new Vector3((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, (z0 + z1) * 0.5f);
            var b = g.AddComponent<BoxCollider>();
            b.size = new Vector3(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0), Mathf.Abs(z1 - z0));
        }

        /// <summary>落ちたり倒れたりする小物</summary>
        static GameObject PropBox(string name, Vector3 pos, Vector3 size, Material m, float mass, bool fragile)
        {
            var g = Geo.Box(root, name, pos, size, m, true);
            MakeProp(g, mass, fragile);
            return g;
        }

        static GameObject PropCyl(string name, Vector3 pos, float radius, float height, Material m, float mass, bool fragile)
        {
            var g = Geo.Cyl(root, name, pos, radius, height, m, true);
            // 円柱の当たりは箱で近似（お皿が球のように転がらないように）
            Object.DestroyImmediate(g.GetComponent<Collider>());
            var bc = g.AddComponent<BoxCollider>();
            bc.size = new Vector3(1f, 2f, 1f);
            MakeProp(g, mass, fragile);
            return g;
        }

        static void MakeProp(GameObject g, float mass, bool fragile)
        {
            g.transform.SetParent(propRoot, true);
            var rb = g.AddComponent<Rigidbody>();
            rb.mass = mass;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            var p = g.AddComponent<Prop>();
            p.fragile = fragile;
            g.layer = 2; // Ignore Raycast（猫の足場判定・視線判定に使わない）
        }

        static SearchPoint Search(string name, Vector3 pos, ItemId item, int qty, params string[] lines)
        {
            var s = new SearchPoint(name, pos, item, qty, lines);
            if (item != ItemId.None)
            {
                var glow = Mat.Emissive("glint", Color.white, new Color(1.8f, 1.7f, 1.2f));
                var g = Geo.Sph(root, "きらめき", pos + Vector3.up * 0.9f, Vector3.one * 0.07f, glow, false);
                g.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                g.AddComponent<Glint>();
                g.transform.SetParent(propRoot, true);
                s.glint = g;
            }
            h.searches.Add(s);
            return s;
        }

        static void Hide(string name, Vector3 pos, Vector3 approach, bool reachable)
        {
            h.hides.Add(new HideSpot(name, pos, approach, reachable));
        }

        // ───────────────────────── 陰（接地感） ─────────────────────────
        static Material aoMat, edgeMat;

        /// <summary>家具の下の柔らかい影</summary>
        static void AO(float x0, float z0, float x1, float z1, float strength = 0.6f, float pad = 0.14f, float y = 0.005f)
        {
            if (aoMat == null) aoMat = Mat.Unlit("aoRect", Tex.SoftRect(), Color.white);
            var m = strength >= 0.99f ? aoMat : Mat.Unlit("aoRect" + strength.ToString("0.00"), Tex.SoftRect(), new Color(1, 1, 1, strength));
            var g = Geo.Quad(root, "接地影", new Vector3((x0 + x1) * 0.5f, y, (z0 + z1) * 0.5f), new Vector2(x1 - x0 + pad * 2f, z1 - z0 + pad * 2f), new Vector3(90f, 0f, 0f), m);
            g.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            g.GetComponent<Renderer>().receiveShadows = false;
        }

        /// <summary>壁ぎわの帯状の陰。face は壁の表面の座標、side は部屋側の向き(+1/-1)</summary>
        static void WallShade(bool alongZ, float face, int side, float s, float e, float y, float strength)
        {
            if (edgeMat == null) edgeMat = Mat.Unlit("edgeShade", Tex.EdgeFade(), new Color(1, 1, 1, 0.5f));
            var m = Mat.Unlit("edgeShade" + strength.ToString("0.00"), Tex.EdgeFade(), new Color(1, 1, 1, strength));
            const float w = 0.38f;
            Vector3 center;
            float yaw;
            if (alongZ) { center = new Vector3(face + side * w * 0.5f, y, (s + e) * 0.5f); yaw = side > 0 ? 90f : -90f; }
            else { center = new Vector3((s + e) * 0.5f, y, face + side * w * 0.5f); yaw = side > 0 ? 0f : 180f; }
            var g = Geo.Quad(root, "壁ぎわの陰", center, new Vector2(e - s, w), new Vector3(90f, yaw, 0f), m);
            g.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            g.GetComponent<Renderer>().receiveShadows = false;
        }

        static void BuildContactShadows()
        {
            // リビング
            AO(0.08f, 2.9f, 0.5f, 4.5f);          // テレビ台
            AO(3.8f, 2.4f, 4.65f, 5.0f, 0.75f);   // ソファ
            AO(1.8f, 3.0f, 2.8f, 4.4f, 0.45f);    // ローテーブル
            AO(0.2f, 0.07f, 1.7f, 0.42f, 0.7f);   // 本棚
            AO(4.95f, 5.95f, 5.75f, 6.75f, 0.5f); // キャットタワー
            // 和室
            AO(2.55f, 9.15f, 4.25f, 10.85f, 0.8f);
            AO(5.4f, 12.0f, 5.94f, 13.5f, 0.7f);
            AO(0.06f, 13.1f, 2.4f, 13.94f, 0.6f);
            // 廊下・玄関
            AO(7.5f, 0.15f, 7.94f, 1.45f, 0.6f, 0.12f, -0.145f);
            AO(6.06f, 5.0f, 6.45f, 5.6f, 0.5f);
            // キッチン
            AO(10.0f, 0.06f, 15.1f, 0.66f, 0.75f);
            AO(15.2f, 0.06f, 15.94f, 0.8f, 0.8f);
            AO(15.45f, 2.0f, 15.94f, 4.2f, 0.7f);
            AO(10.4f, 2.6f, 12.6f, 3.8f, 0.45f, 0.3f);
            AO(8.3f, 4.35f, 8.6f, 4.65f, 0.5f);
            // トイレ・脱衣所・浴室
            AO(10.25f, 5.75f, 10.94f, 6.25f, 0.6f);
            AO(8.15f, 8.95f, 9.15f, 9.44f, 0.6f);
            AO(9.85f, 8.8f, 10.5f, 9.44f, 0.7f);
            AO(8.8f, 7.1f, 9.4f, 7.6f, 0.5f);
            AO(14.2f, 5.06f, 15.94f, 7.7f, 0.6f);
            // 寝室
            AO(11.4f, 11.5f, 13.6f, 13.94f, 0.85f, 0.2f);
            AO(10.9f, 13.45f, 11.35f, 13.94f, 0.5f);
            AO(13.65f, 13.45f, 14.1f, 13.94f, 0.5f);
            AO(15.3f, 10.0f, 15.94f, 12.0f, 0.6f);
            AO(9.0f, 9.56f, 10.5f, 10.05f, 0.6f);
        }

        // ───────────────────────── 生活感の小物 ─────────────────────────
        static void BuildDetails()
        {
            var plate = Mat.Make("switchPlate", new Color(0.93f, 0.92f, 0.88f), null, 0.5f);
            var dark = Mat.Make("switchDark", new Color(0.25f, 0.25f, 0.25f), null, 0.5f);
            // 照明スイッチ（ドアの横、高さ 1.2m）
            void SwitchX(float x, float z, int side)
            {
                float f = x + side * (T * 0.5f + 0.006f);
                Geo.BoxMM(root, "スイッチ", Mathf.Min(f, f + side * 0.012f), 1.14f, z - 0.04f, Mathf.Max(f, f + side * 0.012f), 1.26f, z + 0.04f, plate, false);
            }
            SwitchX(6f, 4.2f, +1); SwitchX(6f, 2.4f, -1);
            SwitchX(8f, 1.8f, -1); SwitchX(8f, 1.8f, +1);
            SwitchX(8f, 5.3f, -1); SwitchX(8f, 7.4f, -1);
            SwitchX(8f, 11.0f, -1); SwitchX(8f, 12.6f, +1);
            SwitchX(6f, 9.8f, -1); SwitchX(6f, 11.2f, +1);
            // コンセント（床から 0.25m）
            void OutletX(float x, float z, int side)
            {
                float f = x + side * (T * 0.5f + 0.006f);
                Geo.BoxMM(root, "コンセント", Mathf.Min(f, f + side * 0.01f), 0.2f, z - 0.035f, Mathf.Max(f, f + side * 0.01f), 0.3f, z + 0.035f, plate, false);
                Geo.BoxMM(root, "差込口", Mathf.Min(f, f + side * 0.013f), 0.235f, z - 0.015f, Mathf.Max(f, f + side * 0.013f), 0.265f, z + 0.015f, dark, false);
            }
            OutletX(0f, 4.8f, +1);    // テレビ横
            OutletX(16f, 2.5f, -1);   // 食器棚の横
            OutletX(8f, 13.2f, +1);   // 寝室
            OutletX(0f, 12.3f, +1);   // 和室

            // 天井の照明器具（消灯中）。スイッチ/引きひもで点灯できる（2026-10-29 追加）
            var fixture = Mat.Make("fixture", new Color(0.92f, 0.92f, 0.9f), null, 0.6f);
            Geo.Cyl(root, "シーリングライト", new Vector3(3f, H - 0.04f, 3.5f), 0.3f, 0.07f, fixture, false);
            var livingLight = AddLight(new Vector3(3f, H - 0.3f, 3.5f), new Color(1f, 0.96f, 0.85f), 0f, 7f);
            h.switches.Add(new LightSwitch("リビングの照明", new Vector3(5.5f, 0f, 4.2f), livingLight, 2.6f));

            Geo.Cyl(root, "シーリングライト", new Vector3(12f, H - 0.04f, 11.8f), 0.3f, 0.07f, fixture, false);
            var bedLight = AddLight(new Vector3(12f, H - 0.3f, 11.8f), new Color(1f, 0.93f, 0.8f), 0f, 7f);
            h.switches.Add(new LightSwitch("寝室の照明", new Vector3(8.5f, 0f, 11.0f), bedLight, 2.4f));

            Geo.Cyl(root, "浴室灯", new Vector3(13.5f, H - 0.03f, 7.2f), 0.14f, 0.05f, fixture, false);
            var bathLight = AddLight(new Vector3(13.5f, H - 0.25f, 7.2f), new Color(0.9f, 0.96f, 1f), 0f, 6f);
            h.switches.Add(new LightSwitch("浴室の照明", new Vector3(11.5f, 0f, 7.2f), bathLight, 2.2f));

            var washi = Mat.Make("washiShade", new Color(0.9f, 0.86f, 0.74f), null, 0.2f);
            Geo.BoxMM(root, "和風照明", 2.95f, H - 0.45f, 10.15f, 3.85f, H - 0.25f, 11.05f, washi, false);
            Geo.Cyl(root, "吊り紐", new Vector3(3.4f, H - 0.12f, 10.6f), 0.005f, 0.25f, dark, false);
            Geo.Cyl(root, "引きひも", new Vector3(3.6f, H - 0.75f, 10.8f), 0.003f, 0.6f, dark, false);
            var washitsuLight = AddLight(new Vector3(3.4f, H - 0.5f, 10.6f), new Color(1f, 0.85f, 0.6f), 0f, 6.5f);
            h.switches.Add(new LightSwitch("和室の照明（引きひも）", new Vector3(3.6f, 0f, 10.8f), washitsuLight, 2.3f));

            // 額縁の絵
            Picture(new Vector3(0f, 1.75f, 3.7f), Vector3.right, 0.8f, 0.55f, 1.3f);
            Picture(new Vector3(6f, 1.55f, 7.6f), Vector3.right, 0.45f, 0.6f, 2.7f);
            Picture(new Vector3(12.5f, 1.55f, 14f), Vector3.back, 0.9f, 0.5f, 4.4f);
            Picture(new Vector3(8f, 1.6f, 9.0f), Vector3.left, 0.4f, 0.5f, 5.1f);

            // カレンダー（キッチン）
            var cal = Mat.Make("calendar", new Color(0.95f, 0.94f, 0.9f), Tex.Tile(Color.white, new Color(0.6f, 0.6f, 0.6f), 7, 0f), 0.1f);
            QuadFacing("カレンダー", new Vector3(8f + T * 0.5f + 0.004f, 1.45f, 4.3f), new Vector2(0.35f, 0.45f), Vector3.right, cal);
            QuadFacing("カレンダーの写真", new Vector3(8f + T * 0.5f + 0.005f, 1.6f, 4.3f), new Vector2(0.33f, 0.18f), Vector3.right, Mat.Make("calPhoto", Color.white, Tex.Calico(), 0.1f));
            // 冷蔵庫のマグネットとメモ
            QuadFacing("メモ", new Vector3(15.6f, 1.45f, 0.805f), new Vector2(0.12f, 0.16f), Vector3.forward, Mat.Make("memo", new Color(1f, 0.95f, 0.6f), null, 0.1f));
            // 玄関マット
            Geo.BoxMM(root, "玄関マット", 6.4f, 0f, 1.7f, 7.6f, 0.01f, 2.3f, Mat.Make("doorMat", Color.white, Tex.Fabric(new Color(0.3f, 0.22f, 0.18f), 0.7f), 0.02f), false);
            // 廊下のランナー
            Geo.BoxMM(root, "廊下マット", 6.55f, 0f, 6.8f, 7.45f, 0.008f, 9.2f, Mat.Make("runner", Color.white, Tex.Fabric(new Color(0.35f, 0.22f, 0.2f), 0.5f), 0.02f, 0f, 1f, 3f), false);
            // 和室の畳の縁（ふち）を強調する敷居
            Geo.BoxMM(root, "敷居", 2.2f, 0f, 6.98f, 3.8f, 0.012f, 7.02f, woodLight, false);
            // 猫の食器（いつもの場所。空っぽ）
            Geo.Cyl(root, "猫の水入れ", new Vector3(9.0f, 0.03f, 4.5f), 0.08f, 0.06f, Mat.Make("catBowlW", new Color(0.85f, 0.85f, 0.88f), null, 0.85f, 0.5f), false);
            Geo.Cyl(root, "水", new Vector3(9.0f, 0.055f, 4.5f), 0.065f, 0.005f, Mat.Make("water", new Color(0.18f, 0.26f, 0.3f), null, 0.97f), false);
            // 爪とぎ（ボロボロ）
            Geo.BoxMM(root, "爪とぎ", 0.6f, 0f, 5.6f, 1.1f, 0.06f, 6.0f, Mat.Make("scratcher", Color.white, Tex.Fabric(new Color(0.6f, 0.48f, 0.32f), 0.9f), 0.05f, 0f, 1f, 4f), false);
            // ソファの爪痕・テレビ台の傷 → 引っかき傷の線
            var scratch = Mat.Make("scratchMark", new Color(0.55f, 0.5f, 0.4f), null, 0.1f);
            for (int i = 0; i < 4; i++)
                Geo.BoxMM(root, "爪痕", 4.651f, 0.3f + i * 0.02f, 4.55f + i * 0.03f, 4.656f, 0.48f + i * 0.02f, 4.56f + i * 0.03f, scratch, false);
        }

        static void Picture(Vector3 wallPoint, Vector3 normal, float w, float hgt, float seed)
        {
            bool xN = Mathf.Abs(normal.x) > 0.5f;
            Vector3 c = wallPoint + normal * (T * 0.5f + 0.02f);
            Vector3 frameSize = xN ? new Vector3(0.03f, hgt + 0.06f, w + 0.06f) : new Vector3(w + 0.06f, hgt + 0.06f, 0.03f);
            Geo.Box(root, "額縁", c, frameSize, woodDark, false);
            var art = Mat.Make("painting" + seed.ToString("0.0"), Color.white, Tex.Painting(seed), 0.35f);
            QuadFacing("絵", c + normal * 0.016f, new Vector2(w, hgt), normal, art);
        }

        // ───────────────────────── リビング ─────────────────────────
        static void BuildLiving()
        {
            // ラグ
            Geo.BoxMM(root, "ラグ", 1.2f, 0f, 2.2f, 3.5f, 0.015f, 5.2f, rugFab, false);
            // テレビ台とテレビ
            Geo.BoxMM(root, "テレビ台", 0.08f, 0f, 2.9f, 0.5f, 0.45f, 4.5f, woodDark);
            Geo.BoxMM(root, "テレビ", 0.16f, 0.48f, 3.0f, 0.24f, 1.08f, 4.4f, blackPl);
            Geo.BoxMM(root, "テレビ脚", 0.2f, 0.45f, 3.55f, 0.36f, 0.48f, 3.85f, blackPl, false);
            QuadFacing("テレビ画面", new Vector3(0.245f, 0.78f, 3.7f), new Vector2(1.3f, 0.54f), Vector3.right, tvScreen);
            AddLight(new Vector3(0.5f, 0.8f, 3.7f), new Color(0.3f, 0.4f, 0.8f), 0.25f, 2.0f, true, 0.4f); // 待機ランプの残光
            // ローテーブル
            Geo.BoxMM(root, "ローテーブル", 1.8f, 0.36f, 3.0f, 2.8f, 0.40f, 4.4f, woodLight);
            foreach (var lp in new[] { new Vector2(1.85f, 3.05f), new Vector2(2.75f, 3.05f), new Vector2(1.85f, 4.35f), new Vector2(2.75f, 4.35f) })
                Geo.BoxMM(root, "脚", lp.x - 0.03f, 0f, lp.y - 0.03f, lp.x + 0.03f, 0.36f, lp.y + 0.03f, woodLight, false);
            // ソファ（西向き）
            Geo.BoxMM(root, "ソファ座面", 3.8f, 0.12f, 2.4f, 4.65f, 0.42f, 5.0f, sofaFab);
            Geo.BoxMM(root, "ソファ背", 4.4f, 0.42f, 2.4f, 4.65f, 0.88f, 5.0f, sofaFab);
            Geo.BoxMM(root, "ひじ掛け", 3.8f, 0.42f, 2.4f, 4.65f, 0.62f, 2.62f, sofaFab);
            Geo.BoxMM(root, "ひじ掛け", 3.8f, 0.42f, 4.78f, 4.65f, 0.62f, 5.0f, sofaFab);
            Geo.BoxMM(root, "クッション", 3.85f, 0.42f, 2.66f, 4.38f, 0.5f, 3.68f, sofaFab, false);
            Geo.BoxMM(root, "クッション", 3.85f, 0.42f, 3.72f, 4.38f, 0.5f, 4.74f, sofaFab, false);
            Geo.BoxMM(root, "ソファ脚", 3.85f, 0f, 2.45f, 4.6f, 0.12f, 4.95f, woodDark, false);
            PropBox("クッション小", new Vector3(4.2f, 0.62f, 4.4f), new Vector3(0.12f, 0.36f, 0.36f), beige, 0.3f, false);
            // 本棚
            Geo.BoxMM(root, "本棚背板", 0.2f, 0f, 0.07f, 1.7f, 1.8f, 0.1f, woodDark, false);
            Geo.BoxMM(root, "本棚側板", 0.2f, 0f, 0.07f, 0.24f, 1.8f, 0.42f, woodDark, false);
            Geo.BoxMM(root, "本棚側板", 1.66f, 0f, 0.07f, 1.7f, 1.8f, 0.42f, woodDark, false);
            var books = new[] { bookA, bookB, bookC, bookD };
            for (int s = 0; s < 4; s++)
            {
                float y = 0.02f + s * 0.45f;
                Geo.BoxMM(root, "棚板", 0.24f, y, 0.1f, 1.66f, y + 0.03f, 0.42f, woodDark, false);
                if (s == 3) break;
                float x = 0.27f;
                int k = s * 7;
                while (x < 1.55f)
                {
                    float bw = 0.04f + 0.04f * Mathf.PerlinNoise(k * 0.7f, s);
                    float bh = 0.26f + 0.12f * Mathf.PerlinNoise(k * 1.3f, s + 3f);
                    Geo.BoxMM(root, "本", x, y + 0.03f, 0.13f, x + bw, y + 0.03f + bh, 0.38f, books[k % 4], false);
                    x += bw + 0.005f; k++;
                }
            }
            Geo.BoxMM(root, "天板", 0.2f, 1.8f, 0.07f, 1.7f, 1.83f, 0.42f, woodDark, false);
            ColliderBox("本棚", 0.2f, 0f, 0.07f, 1.7f, 1.83f, 0.42f);
            PropBox("写真立て", new Vector3(0.6f, 1.92f, 0.25f), new Vector3(0.16f, 0.2f, 0.03f), woodLight, 0.2f, true);
            PropBox("写真立て", new Vector3(1.25f, 1.92f, 0.25f), new Vector3(0.12f, 0.16f, 0.03f), metal, 0.2f, true);
            // キャットタワー
            Geo.BoxMM(root, "タワー台座", 4.95f, 0f, 5.95f, 5.75f, 0.06f, 6.75f, beige);
            Geo.Cyl(root, "タワー柱", new Vector3(5.35f, 0.75f, 6.35f), 0.06f, 1.5f, sisal);
            Geo.Cyl(root, "タワー段", new Vector3(5.35f, 0.62f, 6.35f), 0.32f, 0.04f, beige, false);
            Geo.Cyl(root, "タワー段", new Vector3(5.35f, 1.5f, 6.35f), 0.34f, 0.05f, beige, false);
            ColliderBox("タワー上段", 5.05f, 1.47f, 6.05f, 5.65f, 1.52f, 6.65f);
            // 観葉植物
            Geo.Cyl(root, "鉢", new Vector3(5.5f, 0.17f, 0.5f), 0.18f, 0.34f, pot);
            for (int i = 0; i < 6; i++)
            {
                float a = i * 1.05f;
                Geo.Sph(root, "葉", new Vector3(5.5f + Mathf.Cos(a) * 0.14f, 0.55f + i * 0.09f, 0.5f + Mathf.Sin(a) * 0.14f), new Vector3(0.3f, 0.12f, 0.3f), plant);
            }
            // フロアランプ
            Geo.Cyl(root, "ランプ柱", new Vector3(0.45f, 0.75f, 6.5f), 0.02f, 1.5f, metal);
            Geo.Cyl(root, "ランプ台", new Vector3(0.45f, 0.02f, 6.5f), 0.16f, 0.04f, metal, false);
            Geo.Cyl(root, "ランプシェード", new Vector3(0.45f, 1.55f, 6.5f), 0.2f, 0.28f, shade, false);
            AddLight(new Vector3(0.45f, 1.5f, 6.3f), new Color(1f, 0.72f, 0.45f), 0.9f, 5.5f);
            // 窓
            Window(new Vector3(3.3f, 1.4f, 0f), Vector3.forward, 2.0f, 1.2f, true);
            // 壁の時計
            Geo.Cyl(root, "壁掛け時計", new Vector3(3.0f, 1.9f, 6.93f), 0.16f, 0.04f, woodLight, false).transform.rotation = Quaternion.Euler(90, 0, 0);
            // 小物
            PropCyl("マグカップ", new Vector3(2.1f, 0.46f, 3.4f), 0.04f, 0.1f, ceramic, 0.25f, true);
            PropBox("リモコン", new Vector3(2.5f, 0.42f, 3.9f), new Vector3(0.05f, 0.025f, 0.18f), blackPl, 0.15f, false);
            PropBox("雑誌", new Vector3(2.3f, 0.42f, 4.1f), new Vector3(0.22f, 0.02f, 0.3f), bookC, 0.2f, false);
            PropCyl("花瓶", new Vector3(0.3f, 0.6f, 4.3f), 0.06f, 0.26f, ceramic, 0.4f, true);
            PropBox("ティッシュ箱", new Vector3(0.3f, 0.5f, 3.2f), new Vector3(0.12f, 0.08f, 0.24f), woodWhite, 0.15f, false);

            Search("テレビ", new Vector3(0.6f, 0f, 3.7f), ItemId.None, 0, "古いテレビだ。画面に猫パンチの跡がついている。");
            Search("本棚", new Vector3(0.95f, 0f, 0.75f), ItemId.None, 0, "本棚だ。『猫の気持ちがわかる本』が逆さまに刺さっている。");
            Search("ソファ", new Vector3(3.55f, 0f, 3.7f), ItemId.None, 0, "ソファのクッションの下……何もない。毛だらけだ。");
            Search("窓", new Vector3(3.3f, 0f, 0.45f), ItemId.None, 0, "カーテンが揺れている。……窓はちゃんと閉まっている。外には逃げていない。");
            Search("観葉植物", new Vector3(5.2f, 0f, 0.9f), ItemId.None, 0, "鉢植えの葉がかじられている。");

            Hide("ソファの裏", new Vector3(5.05f, 0f, 3.7f), new Vector3(5.3f, 0f, 3.1f), true);
            Hide("キャットタワーの上", new Vector3(5.35f, 1.53f, 6.35f), new Vector3(4.6f, 0f, 6.0f), true);
        }

        // ───────────────────────── 和室 ─────────────────────────
        static void BuildWashitsu()
        {
            // 押入れ
            var oshiire = Mat.Make("oshiire", new Color(0.62f, 0.50f, 0.36f), null, 0.2f);
            Geo.BoxMM(root, "押入れ側板", 0.06f, 0f, 13.1f, 0.12f, 2.2f, 13.94f, woodLight);
            Geo.BoxMM(root, "押入れ側板", 2.34f, 0f, 13.1f, 2.4f, 2.2f, 13.94f, woodLight);
            Geo.BoxMM(root, "中段", 0.12f, 0.75f, 13.1f, 2.34f, 0.8f, 13.94f, woodLight);
            Geo.BoxMM(root, "押入れ天", 0.06f, 2.2f, 13.1f, 2.4f, 2.5f, 13.94f, woodLight, false);
            Geo.BoxMM(root, "床", 0.12f, 0f, 13.1f, 2.34f, 0.02f, 13.94f, oshiire, false);
            Geo.BoxMM(root, "布団", 0.15f, 0.8f, 13.2f, 2.25f, 1.05f, 13.9f, sheetFab, false);
            Geo.BoxMM(root, "布団", 0.18f, 1.05f, 13.22f, 2.2f, 1.25f, 13.88f, futonFab, false);
            Geo.BoxMM(root, "衣装ケース", 0.15f, 0.02f, 13.2f, 1.0f, 0.5f, 13.9f, woodWhite);
            var fusuma = Mat.WithNormal(Mat.Make("fusuma", new Color(0.82f, 0.78f, 0.66f), Tex.Plaster(new Color(1f, 0.98f, 0.9f), 0.15f), 0.1f), 3.0f);
            Geo.BoxMM(root, "押入れの襖", 0.12f, 0f, 13.04f, 1.25f, 2.18f, 13.08f, fusuma);
            Geo.BoxMM(root, "押入れの襖", 0.2f, 0f, 12.98f, 1.33f, 2.18f, 13.02f, fusuma);
            // 床の間
            Geo.BoxMM(root, "床の間", 2.6f, 0f, 13.3f, 4.3f, 0.12f, 13.94f, woodDark);
            Geo.BoxMM(root, "床柱", 2.55f, 0f, 13.25f, 2.65f, 2.5f, 13.35f, woodDark);
            var scroll = Mat.WithNormal(Mat.Make("kakejiku", new Color(0.86f, 0.82f, 0.70f), Tex.Plaster(Color.white, 0.25f), 0.1f), 3.0f);
            QuadFacing("掛け軸", new Vector3(3.45f, 1.35f, 13.93f), new Vector2(0.45f, 1.2f), Vector3.back, scroll);
            var ink = Mat.Make("sumi", new Color(0.1f, 0.1f, 0.1f), null, 0.1f);
            Geo.Sph(root, "墨絵の猫", new Vector3(3.45f, 1.2f, 13.92f), new Vector3(0.16f, 0.12f, 0.005f), ink);
            Geo.Sph(root, "墨絵の猫", new Vector3(3.52f, 1.32f, 13.92f), new Vector3(0.09f, 0.09f, 0.005f), ink);
            PropCyl("一輪挿し", new Vector3(3.9f, 0.25f, 13.6f), 0.05f, 0.24f, ceramic, 0.3f, true);
            // 箪笥
            Geo.BoxMM(root, "箪笥", 5.4f, 0f, 12.0f, 5.94f, 1.15f, 13.5f, woodDark);
            for (int i = 0; i < 4; i++)
            {
                float y = 0.15f + i * 0.26f;
                Geo.BoxMM(root, "引き出し線", 5.38f, y, 12.05f, 5.4f, y + 0.012f, 13.45f, blackPl, false);
                Geo.BoxMM(root, "取っ手", 5.36f, y + 0.1f, 12.5f, 5.39f, y + 0.13f, 12.62f, metal, false);
                Geo.BoxMM(root, "取っ手", 5.36f, y + 0.1f, 12.88f, 5.39f, y + 0.13f, 13.0f, metal, false);
            }
            PropBox("こけし", new Vector3(5.65f, 1.25f, 12.4f), new Vector3(0.06f, 0.2f, 0.06f), woodLight, 0.2f, false);
            // こたつ
            Geo.BoxMM(root, "こたつ布団", 2.55f, 0.04f, 9.15f, 4.25f, 0.38f, 10.85f, futonFab);
            Geo.BoxMM(root, "こたつ天板", 2.5f, 0.38f, 9.1f, 4.3f, 0.42f, 10.9f, woodLight);
            for (int i = 0; i < 4; i++)
                PropSphere("みかん", new Vector3(3.3f + (i % 2) * 0.09f, 0.47f, 10.1f + (i / 2) * 0.09f), 0.075f);
            PropCyl("湯のみ", new Vector3(3.9f, 0.47f, 9.5f), 0.035f, 0.08f, ceramic, 0.2f, true);
            // 座布団
            var zabuton = Mat.WithNormal(Mat.Make("zabuton", Color.white, Tex.Fabric(new Color(0.35f, 0.18f, 0.25f), 0.3f), 0.05f), 2.5f);
            Geo.BoxMM(root, "座布団", 3.1f, 0f, 8.35f, 3.7f, 0.07f, 8.95f, zabuton, false);
            Geo.BoxMM(root, "座布団", 1.75f, 0f, 9.7f, 2.35f, 0.07f, 10.3f, zabuton, false);
            Geo.BoxMM(root, "座布団", 4.45f, 0f, 9.7f, 5.05f, 0.07f, 10.3f, zabuton, false);
            // 障子窓（西）
            QuadFacing("障子", new Vector3(0.067f, 1.2f, 9.5f), new Vector2(2.0f, 1.6f), Vector3.right, shojiPaper);
            for (int i = 0; i <= 6; i++)
                Geo.BoxMM(root, "障子桟", 0.06f, 0.4f, 8.5f + i * (2f / 6f) - 0.012f, 0.1f, 2.0f, 8.5f + i * (2f / 6f) + 0.012f, woodLight, false);
            for (int i = 0; i <= 5; i++)
                Geo.BoxMM(root, "障子桟", 0.06f, 0.4f + i * 0.32f - 0.012f, 8.5f, 0.1f, 0.4f + i * 0.32f + 0.012f, 10.5f, woodLight, false);
            AddLight(new Vector3(0.9f, 1.4f, 9.5f), new Color(0.45f, 0.55f, 0.85f), 0.45f, 4f);
            // 行灯
            var andon = Mat.Emissive("andon", new Color(0.95f, 0.88f, 0.7f), new Color(1.0f, 0.6f, 0.3f) * 0.7f);
            Geo.BoxMM(root, "行灯", 5.25f, 0f, 7.35f, 5.65f, 0.65f, 7.75f, andon, true);
            AddLight(new Vector3(5.2f, 0.7f, 7.8f), new Color(1f, 0.6f, 0.32f), 0.8f, 4.5f);

            Search("箪笥", new Vector3(5.0f, 0f, 12.75f), ItemId.Toy, 1,
                "古い桐の箪笥。三段目の引き出しを開けてみた。",
                "……奥に、ミケのお気に入りのおもちゃが入っている。");
            Search("床の間", new Vector3(3.45f, 0f, 12.9f), ItemId.None, 0, "掛け軸がある。墨で描かれた猫の絵だ。……どことなくミケに似ている。");
            Search("押入れ", new Vector3(1.8f, 0f, 12.6f), ItemId.None, 0, "押入れの下の段は真っ暗だ。奥に潜り込まれたら、手が届かない。");
            Search("こたつ", new Vector3(3.4f, 0f, 8.75f), ItemId.None, 0, "こたつ布団をめくった。……中はまだほんのり温かい。");
            Search("障子", new Vector3(0.45f, 0f, 9.5f), ItemId.None, 0, "障子に小さな穴が三つ。犯人はわかっている。");

            Hide("こたつの中", new Vector3(3.4f, 0f, 10.0f), new Vector3(3.4f, 0f, 8.85f), true);
            Hide("押入れの奥", new Vector3(1.85f, 0.02f, 13.6f), new Vector3(1.8f, 0f, 12.6f), false);
        }

        static void PropSphere(string name, Vector3 pos, float d)
        {
            var g = Geo.Sph(root, name, pos, Vector3.one * d, fruit, true);
            MakeProp(g, 0.1f, false);
        }

        // ───────────────────────── 廊下・玄関 ─────────────────────────
        static void BuildCorridor()
        {
            // 下駄箱
            Geo.BoxMM(root, "下駄箱", 7.5f, -0.15f, 0.15f, 7.94f, 0.95f, 1.45f, woodLight);
            Geo.BoxMM(root, "下駄箱の扉線", 7.49f, -0.1f, 0.79f, 7.5f, 0.9f, 0.81f, woodDark, false);
            PropCyl("鍵入れ", new Vector3(7.72f, 1.0f, 0.5f), 0.07f, 0.06f, ceramic, 0.2f, true);
            PropBox("小さな置物", new Vector3(7.72f, 1.05f, 1.1f), new Vector3(0.08f, 0.16f, 0.08f), ceramic, 0.2f, true);
            // 靴
            Geo.BoxMM(root, "靴", 6.5f, -0.15f, 0.6f, 6.62f, -0.06f, 0.88f, blackPl, false);
            Geo.BoxMM(root, "靴", 6.66f, -0.15f, 0.62f, 6.78f, -0.06f, 0.9f, blackPl, false);
            Geo.BoxMM(root, "サンダル", 7.0f, -0.15f, 0.9f, 7.1f, -0.12f, 1.15f, pot, false);
            // 玄関ドア
            var doorMat = Mat.WithNormal(Mat.Make("frontDoor", Color.white, Tex.Wood(new Color(0.30f, 0.22f, 0.16f), new Color(0.15f, 0.10f, 0.07f), 9f), 0.5f), 4.0f);
            Geo.BoxMM(root, "玄関ドア", 6.3f, -0.15f, -0.04f, 7.7f, DoorH, 0.04f, doorMat);
            Geo.Cyl(root, "ドアノブ", new Vector3(7.5f, 0.95f, 0.08f), 0.03f, 0.1f, metal, false).transform.rotation = Quaternion.Euler(90, 0, 0);
            QuadFacing("ドアの採光窓", new Vector3(7.0f, 1.75f, 0.045f), new Vector2(0.2f, 0.4f), Vector3.forward, Mat.Emissive("frosted", new Color(0.5f, 0.55f, 0.6f), new Color(0.10f, 0.14f, 0.22f)));
            // 電話台
            Geo.BoxMM(root, "電話台", 6.06f, 0f, 5.0f, 6.45f, 0.75f, 5.6f, woodDark);
            PropBox("黒電話", new Vector3(6.25f, 0.82f, 5.3f), new Vector3(0.2f, 0.1f, 0.22f), blackPl, 0.6f, false);
            // 柱時計
            Geo.BoxMM(root, "柱時計", 7.82f, 1.1f, 4.15f, 7.94f, 1.9f, 4.55f, woodDark, false);
            Geo.Cyl(root, "文字盤", new Vector3(7.81f, 1.7f, 4.35f), 0.12f, 0.01f, ceramic, false).transform.rotation = Quaternion.Euler(0, 0, 90);
            // 突き当たりの小窓
            Window(new Vector3(7f, 1.5f, 14f), Vector3.back, 0.8f, 0.6f, false, true);
            // 照明（ちらつく）
            Geo.Cyl(root, "廊下の照明", new Vector3(7f, H - 0.03f, 6.5f), 0.15f, 0.04f, shade, false);
            AddLight(new Vector3(7f, 2.2f, 6.5f), new Color(1f, 0.85f, 0.65f), 0.75f, 5.5f, true, 0.85f);
            AddLight(new Vector3(7.85f, 0.25f, 10.5f), new Color(1f, 0.6f, 0.3f), 0.5f, 2.5f); // 足元灯
            AddLight(new Vector3(7f, 1.8f, 1.2f), new Color(0.6f, 0.65f, 0.85f), 0.35f, 3.5f); // 玄関のほの明かり

            var door = Search("玄関のドア", new Vector3(7f, 0f, 0.7f), ItemId.None, 0,
                "玄関のドア。鍵もチェーンもかけてある。",
                "ミケは外に出ていない。……この家のどこかにいる。");
            door.locked = true;
            Search("下駄箱", new Vector3(7.1f, 0f, 1.1f), ItemId.Matatabi, 1,
                "下駄箱の上の段に、小さな紙袋がある。",
                "去年の猫の日にもらった、またたびの粉だ。");
            Search("電話台", new Vector3(6.6f, 0f, 5.3f), ItemId.None, 0, "黒電話だ。こんな時間に、誰にかけるというのか。");
            Search("柱時計", new Vector3(7.5f, 0f, 4.35f), ItemId.None, 0, "柱時計がコチ、コチと時を刻んでいる。朝まで、あまり時間はない。");
        }

        // ───────────────────────── キッチン ─────────────────────────
        static void BuildKitchen()
        {
            Geo.BoxMM(root, "キッチン台", 10.0f, 0f, 0.06f, 15.1f, 0.85f, 0.66f, woodWhite);
            Geo.BoxMM(root, "天板", 9.98f, 0.85f, 0.04f, 15.12f, 0.9f, 0.7f, counterTop);
            Geo.BoxMM(root, "シンク", 12.0f, 0.9f, 0.15f, 12.9f, 0.905f, 0.6f, blackPl, false);
            Geo.Cyl(root, "蛇口", new Vector3(12.45f, 1.05f, 0.12f), 0.015f, 0.3f, metal, false);
            Geo.BoxMM(root, "コンロ", 13.9f, 0.9f, 0.12f, 14.8f, 0.92f, 0.62f, blackPl, false);
            Geo.Cyl(root, "五徳", new Vector3(14.12f, 0.93f, 0.37f), 0.1f, 0.01f, metal, false);
            Geo.Cyl(root, "五徳", new Vector3(14.58f, 0.93f, 0.37f), 0.1f, 0.01f, metal, false);
            Geo.BoxMM(root, "レンジフード", 13.85f, 1.75f, 0.06f, 14.85f, 2.05f, 0.55f, metal, false);
            AddLight(new Vector3(14.35f, 1.65f, 0.45f), new Color(1f, 0.82f, 0.6f), 0.8f, 4.5f);
            // 吊り戸棚
            Geo.BoxMM(root, "吊り戸棚", 10.0f, 1.5f, 0.06f, 11.6f, 2.2f, 0.4f, woodWhite, false);
            // 窓（シンクの上）
            Window(new Vector3(12.7f, 1.55f, 0f), Vector3.forward, 1.3f, 0.7f, false);
            // 冷蔵庫
            var fridge = Mat.Make("fridge", new Color(0.88f, 0.89f, 0.88f), null, 0.65f, 0.1f);
            Geo.BoxMM(root, "冷蔵庫", 15.2f, 0f, 0.06f, 15.94f, 1.85f, 0.8f, fridge);
            Geo.BoxMM(root, "冷蔵庫の扉線", 15.2f, 1.2f, 0.79f, 15.94f, 1.21f, 0.81f, blackPl, false);
            Geo.BoxMM(root, "取っ手", 15.25f, 0.5f, 0.8f, 15.28f, 1.1f, 0.84f, metal, false);
            // 食器棚
            Geo.BoxMM(root, "食器棚", 15.45f, 0f, 2.0f, 15.94f, 1.9f, 4.2f, woodLight);
            var glassDoor = Mat.Make("cupGlass", new Color(0.12f, 0.14f, 0.16f), null, 0.95f, 0.3f);
            Geo.BoxMM(root, "ガラス戸", 15.43f, 1.0f, 2.08f, 15.45f, 1.82f, 4.12f, glassDoor, false);
            Geo.BoxMM(root, "下段の扉線", 15.43f, 0.1f, 3.09f, 15.45f, 0.9f, 3.11f, woodDark, false);
            // ダイニングテーブル
            Geo.BoxMM(root, "ダイニングテーブル", 10.4f, 0.7f, 2.6f, 12.6f, 0.74f, 3.8f, woodLight);
            foreach (var lp in new[] { new Vector2(10.48f, 2.68f), new Vector2(12.52f, 2.68f), new Vector2(10.48f, 3.72f), new Vector2(12.52f, 3.72f) })
                Geo.BoxMM(root, "脚", lp.x - 0.03f, 0f, lp.y - 0.03f, lp.x + 0.03f, 0.7f, lp.y + 0.03f, woodLight, false);
            Chair(10.9f, 2.25f, false);
            Chair(12.1f, 2.25f, false);
            Chair(10.9f, 4.15f, true);
            Chair(12.1f, 4.15f, true);
            // ペンダントライト（消灯）
            Geo.Cyl(root, "ペンダント", new Vector3(11.5f, 1.85f, 3.2f), 0.18f, 0.18f, Mat.Make("pendant", new Color(0.3f, 0.3f, 0.28f), null, 0.5f), false);
            Geo.Cyl(root, "コード", new Vector3(11.5f, 2.2f, 3.2f), 0.006f, 0.6f, blackPl, false);
            // ゴミ箱
            Geo.Cyl(root, "ゴミ箱", new Vector3(8.45f, 0.25f, 4.5f), 0.17f, 0.5f, metal);
            // 小物
            PropCyl("皿", new Vector3(10.9f, 0.76f, 3.0f), 0.11f, 0.02f, ceramic, 0.3f, true);
            PropCyl("皿", new Vector3(12.0f, 0.76f, 3.4f), 0.11f, 0.02f, ceramic, 0.3f, true);
            PropCyl("コップ", new Vector3(11.4f, 0.8f, 3.1f), 0.035f, 0.11f, glassDoor, 0.2f, true);
            PropCyl("調味料", new Vector3(10.4f, 0.98f, 0.4f), 0.035f, 0.16f, bookA, 0.25f, true);
            PropCyl("調味料", new Vector3(10.52f, 0.98f, 0.42f), 0.03f, 0.14f, bookC, 0.25f, true);
            PropCyl("ボウル", new Vector3(11.2f, 0.95f, 0.38f), 0.1f, 0.08f, ceramic, 0.3f, true);
            PropCyl("やかん", new Vector3(14.12f, 1.02f, 0.37f), 0.1f, 0.18f, metal, 0.6f, false);
            PropBox("まな板", new Vector3(13.3f, 0.91f, 0.38f), new Vector3(0.4f, 0.02f, 0.25f), woodLight, 0.4f, false);

            Search("食器棚", new Vector3(15.0f, 0f, 3.1f), ItemId.Food, 2,
                "食器棚の下の段に、キャットフードの袋がある。",
                "カリカリと音を立てれば、お腹をすかせたミケが来るかもしれない。");
            Search("冷蔵庫", new Vector3(14.75f, 0f, 1.25f), ItemId.None, 0, "冷蔵庫のモーター音だけが響いている。……上の方から、視線を感じる。");
            Search("シンク", new Vector3(12.45f, 0f, 1.15f), ItemId.None, 0, "シンクに小さな足跡。ミケが水を飲みに来たらしい。");
            Search("コンロ", new Vector3(14.35f, 0f, 1.15f), ItemId.None, 0, "火の元は大丈夫だ。");
            Search("ゴミ箱", new Vector3(8.85f, 0f, 4.4f), ItemId.None, 0, "ゴミ箱は無事だ。……今のところは。");

            Hide("冷蔵庫の上", new Vector3(15.57f, 1.86f, 0.45f), new Vector3(14.9f, 0f, 1.25f), false);
            Hide("テーブルの下", new Vector3(11.5f, 0f, 3.2f), new Vector3(9.9f, 0f, 3.2f), true);
        }

        static void Chair(float x, float z, bool facingSouth)
        {
            Geo.BoxMM(root, "椅子座面", x - 0.22f, 0.42f, z - 0.2f, x + 0.22f, 0.46f, z + 0.2f, woodLight);
            float bz = facingSouth ? z + 0.18f : z - 0.22f;
            Geo.BoxMM(root, "椅子背", x - 0.22f, 0.46f, bz, x + 0.22f, 0.95f, bz + 0.04f, woodLight, false);
            foreach (var o in new[] { new Vector2(-0.19f, -0.17f), new Vector2(0.19f, -0.17f), new Vector2(-0.19f, 0.17f), new Vector2(0.19f, 0.17f) })
                Geo.BoxMM(root, "椅子脚", x + o.x - 0.02f, 0f, z + o.y - 0.02f, x + o.x + 0.02f, 0.42f, z + o.y + 0.02f, woodLight, false);
        }

        // ───────────────────────── トイレ ─────────────────────────
        static void BuildToilet()
        {
            Geo.BoxMM(root, "タンク", 10.72f, 0.42f, 5.75f, 10.94f, 0.95f, 6.25f, ceramic);
            var bowl = Geo.Cyl(root, "便器", new Vector3(10.45f, 0.2f, 6.0f), 0.2f, 0.4f, ceramic, false);
            bowl.transform.localScale = new Vector3(0.36f, 0.2f, 0.46f);
            Geo.Cyl(root, "便座のフタ", new Vector3(10.45f, 0.41f, 6.0f), 0.2f, 0.03f, Mat.Make("seat", new Color(0.92f, 0.9f, 0.86f), null, 0.6f), false).transform.localScale = new Vector3(0.38f, 0.015f, 0.48f);
            ColliderBox("便器", 10.25f, 0f, 5.78f, 10.94f, 0.45f, 6.22f);
            Geo.BoxMM(root, "棚", 10.6f, 1.3f, 5.6f, 10.94f, 1.33f, 6.4f, woodLight, false);
            PropCyl("芳香剤", new Vector3(10.78f, 1.4f, 5.8f), 0.04f, 0.12f, Mat.Make("freshener", new Color(0.4f, 0.6f, 0.9f), null, 0.6f), 0.15f, false);
            PropCyl("予備のロール", new Vector3(10.78f, 1.4f, 6.15f), 0.055f, 0.1f, sheetFab, 0.1f, false);
            // ペーパーホルダー
            Geo.BoxMM(root, "ホルダー", 10.0f, 0.7f, 6.88f, 10.3f, 0.74f, 6.94f, metal, false);
            var roll = Geo.Cyl(root, "トイレットペーパー", new Vector3(10.15f, 0.63f, 6.85f), 0.055f, 0.11f, sheetFab, false);
            roll.transform.rotation = Quaternion.Euler(0, 0, 90);
            Geo.BoxMM(root, "引き出されたペーパー", 9.6f, 0.0f, 6.0f, 10.1f, 0.005f, 6.85f, sheetFab, false);
            Geo.BoxMM(root, "トイレマット", 9.9f, 0f, 5.6f, 10.3f, 0.012f, 6.4f, Mat.Make("toiletMat", Color.white, Tex.Fabric(new Color(0.45f, 0.62f, 0.55f), 0.5f), 0.02f), false);
            Geo.Cyl(root, "トイレの照明", new Vector3(9.5f, H - 0.03f, 6f), 0.12f, 0.04f, shade, false);
            AddLight(new Vector3(9.5f, 2.2f, 6f), new Color(0.85f, 0.95f, 1f), 0.9f, 3.5f, true, 0.9f);

            Search("便器", new Vector3(9.9f, 0f, 6.0f), ItemId.None, 0, "フタはちゃんと閉まっている。……よかった。");
            Search("トイレットペーパー", new Vector3(9.9f, 0f, 6.55f), ItemId.None, 0, "ペーパーが床まで引き出されている。完全に遊ばれた跡だ。");

            Hide("便器の横", new Vector3(10.6f, 0f, 6.62f), new Vector3(9.75f, 0f, 6.5f), true);
        }

        // ───────────────────────── 脱衣所 ─────────────────────────
        static void BuildDatsuijo()
        {
            Geo.BoxMM(root, "洗面台", 8.15f, 0f, 8.95f, 9.15f, 0.85f, 9.44f, woodWhite);
            Geo.BoxMM(root, "洗面ボウル", 8.3f, 0.85f, 9.0f, 9.0f, 0.88f, 9.4f, ceramic, false);
            QuadFacing("鏡", new Vector3(8.65f, 1.45f, 9.437f), new Vector2(0.85f, 0.7f), Vector3.back, mirror);
            PropCyl("歯ブラシ立て", new Vector3(8.4f, 0.93f, 9.32f), 0.03f, 0.1f, ceramic, 0.15f, true);
            PropCyl("ハンドソープ", new Vector3(8.95f, 0.95f, 9.32f), 0.03f, 0.14f, Mat.Make("soap", new Color(0.9f, 0.85f, 0.95f), null, 0.6f), 0.2f, false);
            // 洗濯機
            var washer = Mat.Make("washer", new Color(0.9f, 0.9f, 0.9f), null, 0.6f);
            Geo.BoxMM(root, "洗濯機", 9.85f, 0f, 8.8f, 10.5f, 0.95f, 9.44f, washer);
            var drum = Geo.Cyl(root, "洗濯機の窓", new Vector3(10.175f, 0.55f, 8.795f), 0.2f, 0.01f, glassWin, false);
            drum.transform.rotation = Quaternion.Euler(90, 0, 0);
            PropBox("洗剤", new Vector3(10.05f, 1.05f, 9.2f), new Vector3(0.15f, 0.2f, 0.1f), Mat.Make("detergent", new Color(0.2f, 0.45f, 0.85f), null, 0.5f), 0.4f, false);
            // 洗濯カゴ
            Geo.BoxMM(root, "洗濯カゴ", 8.8f, 0f, 7.1f, 9.4f, 0.45f, 7.6f, wicker);
            Geo.BoxMM(root, "洗濯物", 8.85f, 0.45f, 7.15f, 9.35f, 0.5f, 7.55f, sheetFab, false);
            Geo.BoxMM(root, "バスマット", 10.35f, 0f, 7.9f, 10.9f, 0.012f, 8.7f, Mat.Make("bathMat", Color.white, Tex.Fabric(new Color(0.85f, 0.75f, 0.55f), 0.5f), 0.02f), false);
            AddLight(new Vector3(9.5f, 2.2f, 8.2f), new Color(0.85f, 0.9f, 1f), 0.45f, 3.5f);

            Search("洗面台", new Vector3(8.65f, 0f, 8.4f), ItemId.Bandage, 2,
                "洗面台の引き出しを開けた。",
                "絆創膏の箱がある。ひっかき傷にはこれだ。");
            Search("洗濯カゴ", new Vector3(9.1f, 0f, 8.0f), ItemId.Net, 1,
                "洗濯カゴの中に、大きめの洗濯ネットがある。",
                "……そうだ。病院に連れて行く時、猫はこれに入れると落ち着くと聞いた。");
            Search("洗濯機", new Vector3(10.15f, 0f, 8.35f), ItemId.None, 0, "洗濯機と壁のすき間は狭い。あそこに入られたら手は届かない。");

            Hide("洗濯機のすき間", new Vector3(10.73f, 0f, 9.2f), new Vector3(10.6f, 0f, 8.4f), false);
        }

        // ───────────────────────── 浴室 ─────────────────────────
        static void BuildBath()
        {
            var enamel = Mat.Make("enamel", new Color(0.92f, 0.93f, 0.93f), null, 0.85f);
            // 浴槽（中が空洞）
            Geo.BoxMM(root, "浴槽外", 14.2f, 0f, 5.1f, 14.3f, 0.6f, 7.7f, enamel);
            Geo.BoxMM(root, "浴槽外", 14.2f, 0f, 7.6f, 15.94f, 0.6f, 7.7f, enamel);
            Geo.BoxMM(root, "浴槽外", 14.2f, 0f, 5.06f, 15.94f, 0.6f, 5.16f, enamel);
            Geo.BoxMM(root, "浴槽外", 15.86f, 0f, 5.1f, 15.94f, 0.6f, 7.7f, enamel);
            Geo.BoxMM(root, "浴槽底", 14.3f, 0f, 5.16f, 15.86f, 0.1f, 7.6f, enamel);
            Geo.BoxMM(root, "残り湯", 14.3f, 0.1f, 5.16f, 15.86f, 0.12f, 7.6f, Mat.Make("water", new Color(0.18f, 0.26f, 0.3f), null, 0.97f), false);
            Geo.BoxMM(root, "風呂ふた", 14.25f, 0.6f, 6.5f, 15.9f, 0.63f, 7.65f, lidMat);
            // シャワー
            Geo.BoxMM(root, "水栓", 12.3f, 0.75f, 9.38f, 12.7f, 0.85f, 9.44f, metal, false);
            Geo.Cyl(root, "シャワーバー", new Vector3(12.9f, 1.4f, 9.4f), 0.015f, 1.0f, metal, false);
            var head = Geo.Cyl(root, "シャワーヘッド", new Vector3(12.9f, 1.75f, 9.32f), 0.05f, 0.04f, metal, false);
            head.transform.rotation = Quaternion.Euler(60, 0, 0);
            QuadFacing("浴室の鏡", new Vector3(12.0f, 1.3f, 9.437f), new Vector2(0.5f, 0.8f), Vector3.back, mirror);
            Geo.BoxMM(root, "シャンプー棚", 13.2f, 0.9f, 9.25f, 13.8f, 0.92f, 9.44f, metal, false);
            PropCyl("シャンプー", new Vector3(13.35f, 1.0f, 9.35f), 0.035f, 0.18f, Mat.Make("shampoo", new Color(0.9f, 0.6f, 0.7f), null, 0.6f), 0.3f, false);
            PropCyl("リンス", new Vector3(13.5f, 1.0f, 9.35f), 0.035f, 0.18f, Mat.Make("rinse", new Color(0.6f, 0.8f, 0.9f), null, 0.6f), 0.3f, false);
            PropCyl("ボディソープ", new Vector3(13.65f, 1.0f, 9.35f), 0.035f, 0.16f, ceramic, 0.3f, false);
            Geo.Cyl(root, "風呂いす", new Vector3(12.9f, 0.13f, 8.7f), 0.15f, 0.26f, lidMat);
            PropCyl("洗面器", new Vector3(13.3f, 0.06f, 8.3f), 0.15f, 0.1f, lidMat, 0.3f, false);
            Window(new Vector3(16f, 1.6f, 8.5f), Vector3.left, 0.9f, 0.6f, false, true);
            AddLight(new Vector3(15.2f, 2.0f, 8.4f), new Color(0.5f, 0.6f, 0.9f), 0.45f, 5f);
            // 排水口
            Geo.Cyl(root, "排水口", new Vector3(12.4f, 0.002f, 6.2f), 0.06f, 0.004f, metal, false);

            Search("シャワー", new Vector3(12.7f, 0f, 8.9f), ItemId.None, 0, "シャワーから水がポタ……ポタ……と垂れている。");
            Search("浴槽", new Vector3(13.7f, 0f, 6.0f), ItemId.None, 0, "残り湯の表面に、小さな波紋が残っている。");
            Search("鏡", new Vector3(12.0f, 0f, 8.9f), ItemId.None, 0, "曇った鏡に、肉球の跡がついている。");

            Hide("浴槽の中", new Vector3(15.0f, 0.12f, 5.8f), new Vector3(13.65f, 0f, 5.9f), true);
        }

        // ───────────────────────── 寝室 ─────────────────────────
        static void BuildBedroom()
        {
            Geo.BoxMM(root, "ラグ", 10.6f, 0f, 10.6f, 14.4f, 0.012f, 13.94f, Mat.Make("bedRug", Color.white, Tex.Fabric(new Color(0.55f, 0.52f, 0.46f), 0.5f), 0.02f, 0f, 3f, 3f), false);
            // ベッド
            foreach (var lp in new[] { new Vector2(11.45f, 11.55f), new Vector2(13.55f, 11.55f), new Vector2(11.45f, 13.85f), new Vector2(13.55f, 13.85f) })
                Geo.BoxMM(root, "ベッド脚", lp.x - 0.04f, 0f, lp.y - 0.04f, lp.x + 0.04f, 0.26f, lp.y + 0.04f, woodDark, false);
            Geo.BoxMM(root, "ベッドフレーム", 11.4f, 0.26f, 11.5f, 13.6f, 0.38f, 13.94f, woodDark);
            Geo.BoxMM(root, "ヘッドボード", 11.4f, 0f, 13.82f, 13.6f, 1.0f, 13.94f, woodDark);
            Geo.BoxMM(root, "マットレス", 11.45f, 0.38f, 11.55f, 13.55f, 0.58f, 13.82f, sheetFab);
            Geo.BoxMM(root, "掛け布団", 11.42f, 0.58f, 11.5f, 13.58f, 0.66f, 13.1f, blanketFab, false);
            Geo.BoxMM(root, "掛け布団のたれ", 11.38f, 0.3f, 11.6f, 11.42f, 0.66f, 13.1f, blanketFab, false);
            Geo.BoxMM(root, "掛け布団のたれ", 13.58f, 0.3f, 11.6f, 13.62f, 0.66f, 13.1f, blanketFab, false);
            Geo.BoxMM(root, "めくれた布団", 12.6f, 0.66f, 12.4f, 13.5f, 0.72f, 13.15f, blanketFab, false);
            Geo.BoxMM(root, "枕", 11.65f, 0.58f, 13.3f, 12.4f, 0.7f, 13.75f, pillowFab, false);
            Geo.BoxMM(root, "枕", 12.6f, 0.58f, 13.3f, 13.35f, 0.7f, 13.75f, pillowFab, false);
            // ナイトテーブル
            Geo.BoxMM(root, "ナイトテーブル", 10.9f, 0f, 13.45f, 11.35f, 0.55f, 13.94f, woodLight);
            Geo.BoxMM(root, "ナイトテーブル", 13.65f, 0f, 13.45f, 14.1f, 0.55f, 13.94f, woodLight);
            Geo.Cyl(root, "スタンド", new Vector3(11.12f, 0.75f, 13.72f), 0.12f, 0.2f, shade, false);
            Geo.Cyl(root, "スタンド台", new Vector3(11.12f, 0.6f, 13.72f), 0.05f, 0.1f, ceramic, false);
            AddLight(new Vector3(11.2f, 0.9f, 13.4f), new Color(1f, 0.7f, 0.42f), 0.75f, 4.5f);
            PropBox("目覚まし時計", new Vector3(13.85f, 0.62f, 13.7f), new Vector3(0.12f, 0.1f, 0.06f), Mat.Make("clockRed", new Color(0.7f, 0.12f, 0.1f), null, 0.6f), 0.2f, false);
            PropCyl("水の入ったコップ", new Vector3(13.95f, 0.61f, 13.55f), 0.035f, 0.11f, Mat.Make("cupGlass", new Color(0.12f, 0.14f, 0.16f), null, 0.95f, 0.3f), 0.2f, true);
            // クローゼット（扉が半開き）
            Geo.BoxMM(root, "クローゼット側板", 15.3f, 0f, 10.0f, 15.94f, 2.1f, 10.05f, woodWhite);
            Geo.BoxMM(root, "クローゼット側板", 15.3f, 0f, 11.95f, 15.94f, 2.1f, 12.0f, woodWhite);
            Geo.BoxMM(root, "クローゼット天", 15.3f, 2.05f, 10.0f, 15.94f, 2.1f, 12.0f, woodWhite, false);
            Geo.BoxMM(root, "閉じた扉", 15.28f, 0f, 10.05f, 15.32f, 2.05f, 11.0f, woodWhite);
            Geo.BoxMM(root, "開いた扉", 14.35f, 0f, 11.93f, 15.3f, 2.05f, 11.97f, woodWhite);
            Geo.Cyl(root, "ハンガーパイプ", new Vector3(15.62f, 1.85f, 11.0f), 0.015f, 1.9f, metal, false).transform.rotation = Quaternion.Euler(90, 0, 0);
            var clothes = new[] { bookB, beige, bookA, sheetFab, bookD };
            for (int i = 0; i < 9; i++)
                Geo.BoxMM(root, "服", 15.45f, 0.95f - (i % 3) * 0.1f, 10.15f + i * 0.2f, 15.8f, 1.82f, 10.3f + i * 0.2f, clothes[i % clothes.Length], false);
            // 鏡台
            Geo.BoxMM(root, "鏡台", 9.0f, 0f, 9.56f, 10.5f, 0.9f, 10.05f, woodLight);
            QuadFacing("鏡台の鏡", new Vector3(9.75f, 1.35f, 9.567f), new Vector2(0.9f, 0.8f), Vector3.forward, mirror);
            PropCyl("香水", new Vector3(9.3f, 0.97f, 9.8f), 0.025f, 0.12f, Mat.Make("perfume", new Color(0.8f, 0.6f, 0.75f), null, 0.95f), 0.15f, true);
            PropCyl("香水", new Vector3(9.42f, 0.96f, 9.82f), 0.03f, 0.1f, glassWin, 0.15f, true);
            PropBox("小物入れ", new Vector3(10.1f, 0.95f, 9.82f), new Vector3(0.18f, 0.1f, 0.12f), woodDark, 0.3f, false);
            // 窓
            Window(new Vector3(9.8f, 1.4f, 14f), Vector3.back, 1.4f, 1.1f, true);

            Search("ナイトテーブル", new Vector3(10.6f, 0f, 13.1f), ItemId.Paste, 2,
                "ナイトテーブルの引き出しに、ミケの大好きなおやつペーストがある。",
                "封を切った瞬間、どこにいても飛んでくる……はずだ。");
            Search("ベッド", new Vector3(12.5f, 0f, 11.05f), ItemId.None, 0, "布団が乱れている。ベッドの下は暗くて、手が届かない。");
            Search("鏡台", new Vector3(9.75f, 0f, 10.45f), ItemId.None, 0, "鏡に映る自分の顔。ひどい寝ぐせだ。");
            Search("クローゼット", new Vector3(14.7f, 0f, 11.1f), ItemId.None, 0, "クローゼットの服に、猫の毛がびっしりついている。");

            Hide("ベッドの下", new Vector3(12.5f, 0f, 12.7f), new Vector3(12.5f, 0f, 11.1f), false);
            Hide("クローゼットの中", new Vector3(15.6f, 0f, 11.45f), new Vector3(14.7f, 0f, 11.4f), true);
        }

        // ───────────────────────── 固定カメラ ─────────────────────────
        static void DefineCameraZones()
        {
            var z = h.zones;
            z.Add(new CamZone("リビング", 0, 3.5f, 6, 7, new Vector3(0.45f, 2.25f, 0.45f), new Vector3(3.6f, 0.5f, 4.8f), 0.35f));
            z.Add(new CamZone("リビング", 0, 0, 6, 3.5f, new Vector3(5.6f, 2.25f, 6.55f), new Vector3(2.4f, 0.5f, 1.8f), 0.35f));
            z.Add(new CamZone("和室", 0, 7, 6, 10.5f, new Vector3(5.55f, 2.2f, 13.55f), new Vector3(2.5f, 0.4f, 8.6f), 0.3f));
            z.Add(new CamZone("和室", 0, 10.5f, 6, 14, new Vector3(0.45f, 2.2f, 7.45f), new Vector3(3.0f, 0.4f, 12.5f), 0.3f));
            z.Add(new CamZone("廊下", 6, -1, 8, 4.5f, new Vector3(6.35f, 2.3f, 5.6f), new Vector3(7.2f, 0.4f, 1.0f), 0.25f, 60f));
            z.Add(new CamZone("廊下", 6, 4.5f, 8, 9.5f, new Vector3(7.65f, 2.3f, 13.6f), new Vector3(7.0f, 0.4f, 5.0f), 0.25f, 50f));
            z.Add(new CamZone("廊下", 6, 9.5f, 8, 14, new Vector3(6.35f, 2.3f, 4.6f), new Vector3(7.0f, 0.4f, 13.0f), 0.25f, 45f));
            z.Add(new CamZone("キッチン", 8, 0, 12, 5, new Vector3(15.55f, 2.3f, 4.6f), new Vector3(10.0f, 0.4f, 1.8f), 0.35f));
            z.Add(new CamZone("キッチン", 12, 0, 16, 5, new Vector3(8.45f, 2.3f, 0.45f), new Vector3(14.0f, 0.4f, 3.0f), 0.35f));
            z.Add(new CamZone("トイレ", 8, 5, 11, 7, new Vector3(10.85f, 2.3f, 6.85f), new Vector3(9.0f, 0.5f, 5.6f), 0.2f, 72f));
            z.Add(new CamZone("脱衣所", 8, 7, 11, 9.5f, new Vector3(10.85f, 2.3f, 9.35f), new Vector3(8.9f, 0.4f, 7.6f), 0.25f, 70f));
            z.Add(new CamZone("浴室", 11, 5, 16, 9.5f, new Vector3(11.2f, 2.3f, 9.3f), new Vector3(14.5f, 0.3f, 5.8f), 0.3f, 62f));
            z.Add(new CamZone("寝室", 8, 9.5f, 12, 14, new Vector3(15.55f, 2.3f, 13.55f), new Vector3(9.5f, 0.4f, 10.5f), 0.35f));
            z.Add(new CamZone("寝室", 12, 9.5f, 16, 14, new Vector3(8.45f, 2.3f, 9.95f), new Vector3(14.0f, 0.4f, 12.5f), 0.35f));
        }

        // ───────────────────────── 猫の道 ─────────────────────────
        static void DefineGraph()
        {
            var g = h.graph;
            // リビング
            g.Add("LA", "リビング", 2.0f, 1.4f);
            g.Add("LB", "リビング", 2.0f, 5.8f);
            g.Add("LC", "リビング", 5.0f, 1.5f);
            g.Add("LD", "リビング", 4.4f, 6.0f);
            g.Add("LDoor", "リビング", 5.4f, 3.3f);
            g.Add("LWs", "リビング", 3.0f, 6.5f);
            // 和室
            g.Add("W1", "和室", 3.0f, 7.6f);
            g.Add("W2", "和室", 1.2f, 8.4f);
            g.Add("W3", "和室", 5.0f, 8.3f);
            g.Add("W4", "和室", 1.2f, 11.8f);
            g.Add("W5", "和室", 4.8f, 11.6f);
            g.Add("WDoor", "和室", 5.4f, 10.5f);
            // 廊下
            g.Add("C1", "廊下", 7f, 3.3f);
            g.Add("C2", "廊下", 7f, 6.0f);
            g.Add("C3", "廊下", 7f, 8.1f);
            g.Add("C4", "廊下", 7f, 10.5f);
            g.Add("C5", "廊下", 7f, 11.8f);
            g.Add("C6", "廊下", 7f, 13.3f);
            g.Add("C0", "廊下", 7f, 2.1f);
            // キッチン
            g.Add("K1", "キッチン", 8.6f, 2.6f);
            g.Add("K2", "キッチン", 9.4f, 1.2f);
            g.Add("K3", "キッチン", 13.5f, 1.4f);
            g.Add("K4", "キッチン", 14.2f, 3.2f);
            g.Add("K5", "キッチン", 9.4f, 4.3f);
            // トイレ・脱衣所・浴室
            g.Add("T1", "トイレ", 8.6f, 6.0f);
            g.Add("T2", "トイレ", 9.6f, 6.0f);
            g.Add("E1", "脱衣所", 8.6f, 8.1f);
            g.Add("E2", "脱衣所", 9.6f, 8.1f);
            g.Add("E3", "脱衣所", 10.5f, 8.3f);
            g.Add("B1", "浴室", 11.6f, 8.3f);
            g.Add("B2", "浴室", 12.6f, 7.3f);
            g.Add("B3", "浴室", 13.3f, 6.0f);
            // 寝室
            g.Add("R1", "寝室", 8.6f, 11.8f);
            g.Add("R2", "寝室", 9.6f, 12.8f);
            g.Add("R3", "寝室", 9.8f, 10.7f);
            g.Add("R4", "寝室", 13.6f, 10.6f);
            g.Add("R5", "寝室", 14.6f, 12.6f);

            g.Link("LA", "LB"); g.Link("LA", "LC"); g.Link("LB", "LD"); g.Link("LC", "LDoor"); g.Link("LD", "LDoor");
            g.Link("LB", "LWs"); g.Link("LD", "LWs"); g.Link("LWs", "W1");
            g.Link("W1", "W2"); g.Link("W1", "W3"); g.Link("W2", "W4"); g.Link("W3", "WDoor"); g.Link("WDoor", "W5"); g.Link("W4", "W5");
            g.Link("LDoor", "C1"); g.Link("WDoor", "C4");
            g.Link("C0", "C1"); g.Link("C1", "C2"); g.Link("C2", "C3"); g.Link("C3", "C4"); g.Link("C4", "C5"); g.Link("C5", "C6");
            g.Link("C1", "K1"); g.Link("K1", "K2"); g.Link("K1", "K5"); g.Link("K2", "K3"); g.Link("K3", "K4"); g.Link("K5", "K4");
            g.Link("C2", "T1"); g.Link("T1", "T2");
            g.Link("C3", "E1"); g.Link("E1", "E2"); g.Link("E2", "E3"); g.Link("E3", "B1");
            g.Link("B1", "B2"); g.Link("B2", "B3"); g.Link("B1", "B3");
            g.Link("C5", "R1"); g.Link("R1", "R2"); g.Link("R1", "R3"); g.Link("R3", "R4"); g.Link("R2", "R5"); g.Link("R4", "R5");
        }
    }
}
