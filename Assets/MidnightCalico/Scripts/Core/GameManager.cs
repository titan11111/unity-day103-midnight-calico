using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace MidnightCalico
{
    public enum GState { Title, Intro, Play, Message, Inventory, Pause, Ending }

    /// <summary>リザルトの「おうちの被害レポート」1件分（撮影した写真・部屋名・被害にあった物）</summary>
    public class DamageEvent
    {
        public Texture2D photo;
        public string room;
        public string propName;
    }

    /// <summary>
    /// ゲーム全体の進行。シーンに置くのはこれ1つだけで、家・猫・主人公・UIをすべて生成する。
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager I;

        public GState state = GState.Title;
        public House house;
        public Inventory inv = new Inventory();
        public PlayerController player;
        public CatAI cat;
        public TimidCat newcomer;
        public UIManager ui;
        public NightEvents nightEvents;

        public float elapsed;
        public const float GameLength = 900f;   // 15分で 午前2時→午前6時
        public int damage;
        public int scratches;
        public readonly List<DamageEvent> damageEvents = new List<DamageEvent>();
        const int MaxDamageShots = 8;
        public bool caught;
        public bool showDebug;
        public int resumeFrame;   // 画面を閉じたフレームは操作を受け付けない（同じキーで再度開かないように）

        void Awake()
        {
            I = this;
            GameInput.ClearTouches();
            // iPhone：横画面固定・スリープ防止・負荷を抑えた画質
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Screen.autorotateToPortrait = false;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = true;
            Screen.autorotateToLandscapeRight = true;
            Time.timeScale = 1f;
            Application.targetFrameRate = 60;
            QualitySettings.pixelLightCount = 4;
            QualitySettings.shadowDistance = 18f;
            QualitySettings.shadows = ShadowQuality.All;
            QualitySettings.shadowResolution = ShadowResolution.Medium;

            // 天井側はやや青く、床側は暗く（夜の室内の回り込み光）
            // 懐中電灯の外側も真っ暗にはせず、輪郭だけ薄ぼんやり見える明るさまで持ち上げている（2026-10-29 調整）
            RenderSettings.ambientMode = AmbientMode.Trilight;
            ApplyVisibility();
            if (Application.isMobilePlatform)
            {
                QualitySettings.antiAliasing = 0;
                QualitySettings.shadowResolution = ShadowResolution.Low;
            }
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = 0.03f;
            RenderSettings.fogColor = new Color(0.01f, 0.012f, 0.02f);
            RenderSettings.skybox = null;
            RenderSettings.reflectionIntensity = 0.35f;

            // シーンに残っている既定のカメラ・ライトは使わない
            foreach (var c in FindObjectsByTypeCompat<Camera>()) if (c.gameObject != gameObject) c.gameObject.SetActive(false);
            foreach (var l in FindObjectsByTypeCompat<Light>()) if (l.type == LightType.Directional) l.gameObject.SetActive(false);
            foreach (var a in FindObjectsByTypeCompat<AudioListener>()) a.enabled = false;

            new GameObject("Audio").AddComponent<AudioMan>();
            house = HouseBuilder.Build();

            var camSys = new GameObject("CameraSystem").AddComponent<FixedCameraSystem>();
            camSys.Init(house);

            var pg = new GameObject("ナツキ（主人公）");
            pg.transform.position = house.playerSpawn;
            player = pg.AddComponent<PlayerController>();
            player.Init(house);

            var cg = new GameObject("ミケ（三毛猫）");
            cg.transform.position = house.catSpawn;
            cg.transform.rotation = Quaternion.Euler(0, 140f, 0);
            cat = cg.AddComponent<CatAI>();
            cat.Init(house);
            var newcomerObject = new GameObject("新入り猫（怯える灰白猫）");
            newcomerObject.transform.position = new Vector3(13.8f,3f,12.7f);
            newcomer = newcomerObject.AddComponent<TimidCat>();
            newcomer.Init(house);

            // 柱時計の音
            var clock = new GameObject("柱時計の音");
            clock.transform.position = new Vector3(7.8f, 1.5f, 4.35f);
            AudioMan.Loop3D(clock, Sfx.ClockTick, 0.35f, 9f).Play();

            ui = gameObject.AddComponent<UIManager>();
            ui.Build(this);
            ui.ShowTitle();
            nightEvents = gameObject.AddComponent<NightEvents>();
            nightEvents.Init(this);
        }

        static T[] FindObjectsByTypeCompat<T>() where T : Object
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindObjectsByType<T>(FindObjectsSortMode.None);
#else
            return Object.FindObjectsOfType<T>();
#endif
        }

        void Update()
        {
            switch (state)
            {
                case GState.Play:
                    Time.timeScale = 1f;
                    elapsed += Time.deltaTime;
                    if (elapsed >= GameLength) { TimeUp(); break; }
                    if (Time.frameCount <= resumeFrame + 1) break;
                    if (GameInput.Pressed(GameInput.Menu)) OpenInventory();
                    else if (GameInput.Pressed(GameInput.Cancel)) OpenPause();
                    break;
                case GState.Message:
                case GState.Inventory:
                case GState.Pause:
                    Time.timeScale = 0f;
                    break;
                default:
                    Time.timeScale = 1f;
                    break;
            }
            if (GameInput.RetroToggle && state != GState.Ending) ToggleRetro();
            if (GameInput.DebugToggle) showDebug = !showDebug;
        }

        void OnApplicationFocus(bool focused)
        {
            if (!focused) SuspendControls();
        }
        void OnApplicationPause(bool paused)
        {
            if (paused) SuspendControls();
        }
        void SuspendControls()
        {
            GameInput.ClearTouches();
            if (state == GState.Play && ui != null) OpenPause();
        }

        public void ToggleRetro()
        {
            var cs = FixedCameraSystem.I;
            cs.SetRetro(!cs.retro);
            Toast(cs.retro ? "画質：レトロ（低解像度）" : "画質：高精細");
        }

        public void ApplyVisibility()
        {
            float gain = FilmLook.Brightness;
            RenderSettings.ambientSkyColor = new Color(0.21f, 0.235f, 0.29f) * gain;
            RenderSettings.ambientEquatorColor = new Color(0.18f, 0.18f, 0.205f) * gain;
            RenderSettings.ambientGroundColor = new Color(0.11f, 0.105f, 0.10f) * gain;
        }

        public float GameMinutes { get { return 120f + Mathf.Clamp01(elapsed / GameLength) * 240f; } }

        public string ClockText
        {
            get
            {
                int minutes = 120 + Mathf.FloorToInt(Mathf.Clamp01(elapsed / GameLength) * 240f);
                return string.Format("AM {0}:{1:00}", minutes / 60, minutes % 60);
            }
        }

        public void BeginPlay()
        {
            state = GState.Play;
            resumeFrame = Time.frameCount;
            GameInput.ClearTouches();
            if (AudioMan.I != null) AudioMan.I.StartAmbient();
            FixedCameraSystem.I.ForceCut();
            if (ui.ShowTouch || GameInput.padUsed)
            {
                // スマホ・ゲームパッドはスティックを倒した方向へ進む操作が標準（ポーズ画面でタンク操作に切替可）
                player.cameraRelative = true;
                Toast("スティックを少し倒すと しのび足。走るボタンで歩行／走行を切替。足音でミケが逃げる。");
            }
            else Toast("↑で前進　↓で後退　←→で向きを変える　Shiftで走る（足音でミケが逃げる）");
        }

        public void Toast(string s) { if (ui != null) ui.Toast(s); }
        public void FlashRed() { if (ui != null) ui.FlashRed(); }

        public void AddDamage()
        {
            damage++;
        }

        /// <summary>小物が倒れた/壊れた瞬間を記録し、最大8枚まで証拠写真を撮る（リザルトの被害レポート用）</summary>
        public void RecordDamage(string propName, Vector3 pos)
        {
            damage++;
            if (damageEvents.Count >= MaxDamageShots) return;
            StartCoroutine(CaptureDamageShot(propName, pos));
        }

        IEnumerator CaptureDamageShot(string propName, Vector3 pos)
        {
            yield return new WaitForEndOfFrame();
            // ScreenCaptureモジュール（WebGLでは不安定）を使わず、現在のゲームカメラを
            // 小さめのRenderTextureへ再レンダーしてReadPixelsする（どのプラットフォームでも動く方法）
            Texture2D tex = null;
            var cam = FixedCameraSystem.I != null ? FixedCameraSystem.I.cam : null;
            if (cam != null)
            {
                int w = Mathf.Max(64, Screen.width / 3);
                int h = Mathf.Max(64, Screen.height / 3);
                var rt = RenderTexture.GetTemporary(w, h, 24);
                var prevTarget = cam.targetTexture;
                var prevActive = RenderTexture.active;
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                tex = new Texture2D(w, h, TextureFormat.RGB24, false);
                tex.ReadPixels(new UnityEngine.Rect(0, 0, w, h), 0, 0);
                tex.Apply();
                cam.targetTexture = prevTarget;
                RenderTexture.active = prevActive;
                RenderTexture.ReleaseTemporary(rt);
            }
            damageEvents.Add(new DamageEvent
            {
                photo = tex,
                room = house != null ? house.RoomName(pos) : "",
                propName = propName,
            });
        }

        public void OpenInventory()
        {
            GameInput.ClearTouches();
            state = GState.Inventory;
            ui.OpenInventory();
            if (AudioMan.I != null) AudioMan.I.Play2D(Sfx.Cursor, 0.6f, 0.8f);
        }

        public void CloseInventory()
        {
            ui.CloseInventory();
            state = GState.Play;
            resumeFrame = Time.frameCount;
            GameInput.ClearTouches();
        }

        public void OpenPause()
        {
            GameInput.ClearTouches();
            state = GState.Pause;
            ui.OpenPause();
        }

        public void ClosePause()
        {
            ui.ClosePause();
            state = GState.Play;
            resumeFrame = Time.frameCount;
            GameInput.ClearTouches();
        }

        /// <summary>調べる</summary>
        public void DoSearch(SearchPoint sp)
        {
            if (nightEvents != null && nightEvents.TryExtinguish(sp)) return;
            var lines = new List<string>();
            if (sp.locked && AudioMan.I != null) AudioMan.I.Play2D(Sfx.Locked, 0.8f);
            if (sp.item != ItemId.None && !sp.taken)
            {
                lines.AddRange(sp.lines);
                var info = Inventory.Info[sp.item];
                lines.Add("《" + info.name + "》を手に入れた。" + (sp.qty > 1 ? "（×" + sp.qty + "）" : ""));
                inv.Add(sp.item, sp.qty);
                sp.taken = true;
                if (sp.glint != null) Destroy(sp.glint);
                if (inv.equipped == ItemId.None || inv.equipped == ItemId.Flashlight)
                {
                    if (sp.item != ItemId.Bandage && sp.item != ItemId.Net) inv.equipped = sp.item;
                }
                if (AudioMan.I != null) AudioMan.I.Play2D(Sfx.Pickup, 0.8f);
            }
            else if (sp.item != ItemId.None && sp.taken)
            {
                lines.Add("もう何も入っていない。");
            }
            else
            {
                lines.AddRange(sp.lines);
                // 近くにミケが隠れていたら気配を描写
                if (cat.state == CatState.Hidden && cat.spot != null && Vector3.Distance(cat.spot.pos, sp.pos) < 2.2f)
                    lines.Add("……すぐ近くで、何かが息をひそめている気配がする。");
            }
            ShowMessage(lines.ToArray(), null);
        }

        public void ShowMessage(string[] lines, System.Action onDone)
        {
            state = GState.Message;
            ui.ShowMessage(lines, () =>
            {
                state = GState.Play;
                resumeFrame = Time.frameCount;
                GameInput.ClearTouches();
                if (onDone != null) onDone();
            });
        }

        public void OnCatCaught()
        {
            if (caught) return;
            caught = true;
            state = GState.Ending;
            StartCoroutine(CaughtSequence());
        }

        IEnumerator CaughtSequence()
        {
            if (AudioMan.I != null)
            {
                AudioMan.I.Play2D(Sfx.Catch, 0.9f);
                AudioMan.I.PlayAt(Sfx.MeowLong, player.transform.position + Vector3.up, 1f, 0.95f);
            }
            ui.Toast("つかまえた！！");
            Haptics.Strong();
            var p = player.transform;
            FixedCameraSystem.I.Shot(p.position + p.forward * 1.6f + Vector3.up * 1.5f + p.right * 0.4f, p.position + Vector3.up * 1.15f);
            yield return new WaitForSeconds(3.2f);
            ui.ShowEnding(true, Rank(), elapsed, damage, scratches);
        }

        void TimeUp()
        {
            state = GState.Ending;
            ui.ShowEnding(false, "-", elapsed, damage, scratches);
        }

        string Rank()
        {
            if (elapsed < 240f && damage <= 3 && scratches == 0) return "S";
            if (elapsed < 420f && damage <= 8) return "A";
            if (elapsed < 660f) return "B";
            return "C";
        }

        public void Restart()
        {
            Time.timeScale = 1f;
            GameInput.ClearTouches();
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex >= 0 ? SceneManager.GetActiveScene().buildIndex : 0);
        }

        void OnGUI()
        {
            if (!showDebug || cat == null) return;
            GUI.Label(new Rect(10, 120, 600, 24), "[DEBUG] ミケ: " + cat.DebugLabel() + "  部屋: " + cat.RoomName);
        }
    }
}
