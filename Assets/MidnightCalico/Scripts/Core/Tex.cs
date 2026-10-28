using UnityEngine;

namespace MidnightCalico
{
    /// <summary>
    /// 外部アセットを使わずに、木目・畳・タイル・布・壁紙・三毛柄などの質感テクスチャを生成する。
    /// </summary>
    public static class Tex
    {
        const int N = 256;

        static Texture2D New(string name, int size = N)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, true);
            t.name = name;
            t.wrapMode = TextureWrapMode.Repeat;
            t.filterMode = FilterMode.Trilinear;
            t.anisoLevel = 4;
            return t;
        }

        static float Fbm(float x, float y, int oct = 4)
        {
            float v = 0f, a = 0.5f, f = 1f;
            for (int i = 0; i < oct; i++)
            {
                v += a * Mathf.PerlinNoise(x * f + 13.1f * i, y * f + 7.7f * i);
                f *= 2f; a *= 0.5f;
            }
            return v; // 0..~1
        }

        static Color Mul(Color c, float k) { return new Color(c.r * k, c.g * k, c.b * k, 1f); }

        /// <summary>家具用の木目</summary>
        public static Texture2D Wood(Color light, Color dark, float seed)
        {
            var t = New("wood");
            var px = new Color[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float u = x / (float)N, v = y / (float)N;
                    float n = Fbm(u * 3f + seed, v * 0.6f + seed, 4);
                    float ring = Mathf.Sin((u * 22f + n * 9f) * Mathf.PI);
                    ring = Mathf.Pow(Mathf.Abs(ring), 0.35f);
                    float fine = Mathf.PerlinNoise(u * 140f + seed, v * 6f) * 0.25f;
                    float k = Mathf.Clamp01(ring * 0.75f + fine);
                    px[y * N + x] = Color.Lerp(dark, light, k);
                }
            t.SetPixels(px); t.Apply(true);
            return t;
        }

        /// <summary>フローリング（板の継ぎ目つき）</summary>
        public static Texture2D Planks(Color light, Color dark, float seed)
        {
            var t = New("planks");
            var px = new Color[N * N];
            int boards = 4;
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float u = x / (float)N, v = y / (float)N;
                    int b = Mathf.FloorToInt(u * boards);
                    float bu = u * boards - b;
                    float offset = (b * 0.37f) % 1f;
                    float bv = (v + offset) % 1f;
                    float tone = 0.85f + 0.3f * Mathf.PerlinNoise(b * 3.3f + seed, 1.7f);
                    float n = Fbm(bu * 1.5f + b * 10f + seed, v * 8f, 3);
                    float grain = Mathf.Abs(Mathf.Sin((bu * 9f + n * 6f) * Mathf.PI));
                    grain = Mathf.Pow(grain, 0.5f);
                    Color c = Color.Lerp(dark, light, Mathf.Clamp01(grain * 0.7f + 0.2f)) * tone;
                    // 継ぎ目
                    if (bu < 0.02f || bu > 0.985f) c = Mul(dark, 0.45f);
                    if (bv < 0.008f) c = Mul(dark, 0.5f);
                    c.a = 1f;
                    px[y * N + x] = c;
                }
            t.SetPixels(px); t.Apply(true);
            return t;
        }

        /// <summary>畳（い草の目＋縁）</summary>
        public static Texture2D Tatami(bool withBorder)
        {
            var t = New("tatami");
            var px = new Color[N * N];
            Color baseC = new Color(0.66f, 0.62f, 0.38f);
            Color darkC = new Color(0.48f, 0.47f, 0.26f);
            Color heri = new Color(0.10f, 0.12f, 0.10f);
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float u = x / (float)N, v = y / (float)N;
                    float weave = Mathf.Abs(Mathf.Sin(v * N * 0.5f * Mathf.PI));
                    float stitch = (Mathf.FloorToInt(u * 24f) % 2 == 0) ? 1f : 0.93f;
                    float n = Mathf.PerlinNoise(u * 20f, v * 200f) * 0.25f;
                    Color c = Color.Lerp(darkC, baseC, weave * 0.6f + 0.25f + n) * stitch;
                    if (withBorder && (u < 0.05f || u > 0.95f)) c = heri * (0.9f + 0.2f * Mathf.PerlinNoise(u * 50f, v * 50f));
                    c.a = 1f;
                    px[y * N + x] = c;
                }
            t.SetPixels(px); t.Apply(true);
            return t;
        }

        /// <summary>タイル（目地つき）</summary>
        public static Texture2D Tile(Color tile, Color grout, int count, float variance)
        {
            var t = New("tile");
            var px = new Color[N * N];
            float g = 0.035f * count;
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float u = x / (float)N * count, v = y / (float)N * count;
                    int iu = Mathf.FloorToInt(u), iv = Mathf.FloorToInt(v);
                    float fu = u - iu, fv = v - iv;
                    float tv = 1f + variance * (Mathf.PerlinNoise(iu * 7.1f, iv * 3.9f) - 0.5f);
                    float sheen = 1f + 0.06f * Mathf.PerlinNoise(u * 3f, v * 3f);
                    Color c = Mul(tile, tv * sheen);
                    if (fu < g || fv < g) c = grout;
                    c.a = 1f;
                    px[y * N + x] = c;
                }
            t.SetPixels(px); t.Apply(true);
            return t;
        }

        /// <summary>布・カーペット・ソファ地</summary>
        public static Texture2D Fabric(Color c, float roughness)
        {
            var t = New("fabric");
            var px = new Color[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float u = x / (float)N, v = y / (float)N;
                    float weave = ((x + y) % 4 < 2 ? 1f : 0.94f) * ((x % 2 == 0) ? 1f : 0.97f);
                    float n = 1f + roughness * (Mathf.PerlinNoise(u * 30f, v * 30f) - 0.5f) + 0.1f * (Mathf.PerlinNoise(u * 4f, v * 4f) - 0.5f);
                    px[y * N + x] = Mul(c, weave * n);
                }
            t.SetPixels(px); t.Apply(true);
            return t;
        }

        /// <summary>壁紙（うっすら縦縞＋汚れ）</summary>
        public static Texture2D Wallpaper(Color c, float stripe)
        {
            var t = New("wallpaper");
            var px = new Color[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float u = x / (float)N, v = y / (float)N;
                    float s = 1f - stripe * (Mathf.Sin(u * 16f * Mathf.PI) * 0.5f + 0.5f);
                    float grime = 1f - 0.12f * Mathf.Pow(1f - v, 6f) * Mathf.PerlinNoise(u * 8f, v * 3f); // 下の方の汚れ
                    float n = 1f + 0.05f * (Mathf.PerlinNoise(u * 60f, v * 60f) - 0.5f);
                    px[y * N + x] = Mul(c, s * grime * n);
                }
            t.SetPixels(px); t.Apply(true);
            return t;
        }

        /// <summary>和室の砂壁・しっくい</summary>
        public static Texture2D Plaster(Color c, float amount)
        {
            var t = New("plaster");
            var px = new Color[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float u = x / (float)N, v = y / (float)N;
                    float n = Fbm(u * 12f, v * 12f, 4);
                    float speck = Mathf.PerlinNoise(u * 180f, v * 180f) > 0.78f ? 0.85f : 1f;
                    px[y * N + x] = Mul(c, (1f - amount * 0.5f + amount * n) * speck);
                }
            t.SetPixels(px); t.Apply(true);
            return t;
        }

        /// <summary>三毛猫の柄（白地に茶と黒のぶち）</summary>
        public static Texture2D Calico()
        {
            var t = New("calico");
            var px = new Color[N * N];
            Color white = new Color(0.95f, 0.93f, 0.88f);
            Color orange = new Color(0.86f, 0.52f, 0.20f);
            Color black = new Color(0.10f, 0.09f, 0.08f);
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float u = x / (float)N, v = y / (float)N;
                    // 継ぎ目をなくすため円周方向にサンプル
                    float a = u * Mathf.PI * 2f;
                    float sx = Mathf.Cos(a) * 1.2f + 5f, sy = Mathf.Sin(a) * 1.2f + 5f;
                    float o = Fbm(sx + v * 2.2f, sy + v * 1.3f, 3);
                    float b = Fbm(sx * 1.3f + 20f + v * 1.7f, sy * 1.3f + 11f - v, 3);
                    Color c = white;
                    if (v > 0.22f) // お腹側（下）は白
                    {
                        if (o > 0.53f) c = orange;
                        if (b > 0.56f) c = black;
                    }
                    // 毛並み
                    float fur = 0.9f + 0.1f * Mathf.PerlinNoise(u * 220f, v * 40f);
                    c = Mul(c, fur);
                    // 茶ぶちのしま
                    if (c.r > 0.6f && c.b < 0.3f) c = Mul(c, 0.9f + 0.1f * Mathf.Sin(u * 90f));
                    px[y * N + x] = c;
                }
            t.SetPixels(px); t.Apply(true);
            return t;
        }

        /// <summary>周辺減光（ビネット）</summary>
        public static Texture2D Vignette()
        {
            int s = 128;
            var t = new Texture2D(s, s, TextureFormat.RGBA32, false);
            t.wrapMode = TextureWrapMode.Clamp;
            var px = new Color[s * s];
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float u = x / (float)(s - 1) * 2f - 1f, v = y / (float)(s - 1) * 2f - 1f;
                    float d = Mathf.Sqrt(u * u * 0.8f + v * v);
                    float a = Mathf.Clamp01((d - 0.55f) / 0.75f);
                    px[y * s + x] = new Color(0, 0, 0, a * a * 0.85f);
                }
            t.SetPixels(px); t.Apply(false);
            return t;
        }

        /// <summary>心電図の波形（インベントリ画面のステータス表示）</summary>
        public static Texture2D Ecg()
        {
            int w = 512, h = 96;
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
            t.wrapMode = TextureWrapMode.Repeat;
            var px = new Color[w * h];
            for (int i = 0; i < px.Length; i++) px[i] = new Color(1, 1, 1, 0);
            int prev = h / 2;
            for (int x = 0; x < w; x++)
            {
                float p = (x % 128) / 128f;
                float y = 0f;
                if (p > 0.30f && p < 0.36f) y = Mathf.Sin((p - 0.30f) / 0.06f * Mathf.PI) * 0.12f;
                else if (p > 0.40f && p < 0.43f) y = -0.15f;
                else if (p > 0.43f && p < 0.47f) y = 0.9f * (1f - Mathf.Abs((p - 0.45f) / 0.02f));
                else if (p > 0.47f && p < 0.50f) y = -0.28f;
                else if (p > 0.58f && p < 0.70f) y = Mathf.Sin((p - 0.58f) / 0.12f * Mathf.PI) * 0.18f;
                int yy = Mathf.Clamp(Mathf.RoundToInt(h / 2 + y * h * 0.45f), 1, h - 2);
                int lo = Mathf.Min(prev, yy), hi = Mathf.Max(prev, yy);
                for (int k = lo; k <= hi; k++)
                {
                    px[k * w + x] = Color.white;
                    if (k + 1 < h) px[(k + 1) * w + x] = new Color(1, 1, 1, 0.5f);
                }
                prev = yy;
            }
            t.SetPixels(px); t.Apply(false);
            return t;
        }

        // ───────────────────────── リアル化用 ─────────────────────────

        /// <summary>
        /// 色テクスチャの明暗から凹凸（法線マップ）を作る。暗い所＝へこみ（木目の溝・タイルの目地・畳の目）。
        /// RGB と A の両方に書くので、PC（DXT5nm 方式）でもスマホ（RGB 方式）でも正しく読まれる。
        /// </summary>
        public static Texture2D NormalFromAlbedo(Texture2D src, float strength)
        {
            int w = src.width, hgt = src.height;
            var c = src.GetPixels();
            var h = new float[w * hgt];
            for (int i = 0; i < c.Length; i++) h[i] = c[i].r * 0.3f + c[i].g * 0.59f + c[i].b * 0.11f;
            var t = new Texture2D(w, hgt, TextureFormat.RGBA32, true, true);
            t.name = src.name + "_normal";
            t.wrapMode = TextureWrapMode.Repeat;
            t.filterMode = FilterMode.Trilinear;
            t.anisoLevel = 4;
            var px = new Color[w * hgt];
            for (int y = 0; y < hgt; y++)
                for (int x = 0; x < w; x++)
                {
                    float l = h[y * w + (x - 1 + w) % w], r = h[y * w + (x + 1) % w];
                    float d = h[((y - 1 + hgt) % hgt) * w + x], u = h[((y + 1) % hgt) * w + x];
                    Vector3 n = new Vector3((l - r) * strength, (d - u) * strength, 1f).normalized;
                    float nx = n.x * 0.5f + 0.5f, ny = n.y * 0.5f + 0.5f, nz = n.z * 0.5f + 0.5f;
                    px[y * w + x] = new Color(nx, ny, nz, nx);
                }
            t.SetPixels(px);
            t.Apply(true);
            return t;
        }

        /// <summary>懐中電灯の光の形（中心のホットスポット＋反射板の輪＋にじみ）</summary>
        public static Texture2D FlashlightCookie()
        {
            int s = 256;
            var t = new Texture2D(s, s, TextureFormat.RGBA32, false);
            t.name = "flashlightCookie";
            t.wrapMode = TextureWrapMode.Clamp;
            t.filterMode = FilterMode.Bilinear;
            var px = new Color[s * s];
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float u = (x + 0.5f) / s * 2f - 1f, v = (y + 0.5f) / s * 2f - 1f;
                    float r = Mathf.Sqrt(u * u + v * v);
                    float hot = Mathf.Exp(-r * r * 9f) * 0.75f;
                    float body = Mathf.Clamp01(1f - Mathf.Pow(r / 0.82f, 6f)) * 0.42f;
                    float ring = Mathf.Exp(-Mathf.Pow((r - 0.62f) / 0.05f, 2f)) * 0.28f + Mathf.Exp(-Mathf.Pow((r - 0.35f) / 0.04f, 2f)) * 0.1f;
                    float ripple = 1f + 0.06f * Mathf.Sin(r * 60f);
                    float smudge = 0.9f + 0.1f * Mathf.PerlinNoise(u * 3f + 10f, v * 3f + 4f);
                    float a = Mathf.Clamp01((hot + body + ring) * ripple * smudge);
                    if (r > 0.97f) a = 0f;
                    px[y * s + x] = new Color(a, a, a, a);
                }
            t.SetPixels(px); t.Apply(false);
            return t;
        }

        /// <summary>家具の下・壁ぎわの「接地の影」（四角がぼけた形。中央が濃い）</summary>
        public static Texture2D SoftRect()
        {
            int s = 64;
            var t = new Texture2D(s, s, TextureFormat.RGBA32, false);
            t.wrapMode = TextureWrapMode.Clamp;
            var px = new Color[s * s];
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float u = Mathf.Abs((x + 0.5f) / s * 2f - 1f), v = Mathf.Abs((y + 0.5f) / s * 2f - 1f);
                    float a = Mathf.SmoothStep(1f, 0f, Mathf.Clamp01((u - 0.45f) / 0.55f)) * Mathf.SmoothStep(1f, 0f, Mathf.Clamp01((v - 0.45f) / 0.55f));
                    px[y * s + x] = new Color(0, 0, 0, a);
                }
            t.SetPixels(px); t.Apply(false);
            return t;
        }

        /// <summary>壁ぎわ用（片側が濃い帯）。v=0 が壁側。</summary>
        public static Texture2D EdgeFade()
        {
            int w = 8, s = 64;
            var t = new Texture2D(w, s, TextureFormat.RGBA32, false);
            t.wrapMode = TextureWrapMode.Clamp;
            var px = new Color[w * s];
            for (int y = 0; y < s; y++)
            {
                float k = 1f - y / (float)(s - 1);
                float a = k * k * k;
                for (int x = 0; x < w; x++) px[y * w + x] = new Color(0, 0, 0, a);
            }
            t.SetPixels(px); t.Apply(false);
            return t;
        }

        /// <summary>丸いぼかし（猫や人の足もとの影、ほこりの粒）</summary>
        public static Texture2D SoftDot(Color c)
        {
            int s = 32;
            var t = new Texture2D(s, s, TextureFormat.RGBA32, false);
            t.wrapMode = TextureWrapMode.Clamp;
            var px = new Color[s * s];
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float u = (x + 0.5f) / s * 2f - 1f, v = (y + 0.5f) / s * 2f - 1f;
                    float r = Mathf.Sqrt(u * u + v * v);
                    float a = Mathf.Clamp01(1f - r);
                    px[y * s + x] = new Color(c.r, c.g, c.b, a * a * c.a);
                }
            t.SetPixels(px); t.Apply(false);
            return t;
        }

        /// <summary>壁に掛ける絵（抽象的な風景）</summary>
        public static Texture2D Painting(float seed)
        {
            int s = 64;
            var t = new Texture2D(s, s, TextureFormat.RGBA32, true);
            t.wrapMode = TextureWrapMode.Clamp;
            var px = new Color[s * s];
            Color sky = Color.HSVToRGB(Mathf.Repeat(seed * 0.37f, 1f), 0.35f, 0.75f);
            Color land = Color.HSVToRGB(Mathf.Repeat(seed * 0.37f + 0.3f, 1f), 0.45f, 0.45f);
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float u = x / (float)s, v = y / (float)s;
                    float horizon = 0.4f + 0.15f * Mathf.PerlinNoise(u * 3f + seed, seed);
                    Color c = v > horizon ? Color.Lerp(sky, Color.white, (v - horizon) * 0.4f) : Color.Lerp(land * 0.6f, land, v / horizon);
                    c *= 0.85f + 0.15f * Mathf.PerlinNoise(u * 20f + seed, v * 20f);
                    c.a = 1f;
                    px[y * s + x] = c;
                }
            t.SetPixels(px); t.Apply(true);
            return t;
        }

        public static Texture2D Solid(Color c)
        {
            var t = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            var px = new Color[16];
            for (int i = 0; i < 16; i++) px[i] = c;
            t.SetPixels(px); t.Apply(false);
            return t;
        }
    }
}
