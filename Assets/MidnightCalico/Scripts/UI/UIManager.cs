using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MidnightCalico
{
    /// <summary>
    /// 画面まわり一式（タイトル／導入／メッセージ／アイテム画面／ポーズ／エンディング／タッチ操作）。
    /// 1280×720 基準で組み立て、画面サイズに合わせて拡縮する。
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        GameManager gm;
        Font font, titleFont;
        Canvas canvas;
        RectTransform root;
        RectTransform safe;
        CanvasScaler scaler;   // ノッチ・ホームバーを避けた領域

        RawImage retroImage;
        Image fade, redFlash;
        Text clock, roomLabel, prompt, toast;
        float toastT, redT;
        CanvasGroup hud;

        // メッセージ
        GameObject msgPanel;
        Text msgText, msgArrow;
        string[] msgLines;
        int msgIndex;
        float msgChars;
        Action msgDone;
        GameObject msgTap;

        // タイトル・導入・エンディング・ポーズ
        GameObject titlePanel, introPanel, endingPanel, pausePanel;
        Text pressStart, introText;
        int introPage;
        float introChars;
        static readonly string[] IntroPages =
        {
            "午前2時。\nガシャン、と何かが割れる音で目が覚めた。",
            "暗い家の中を、何かが駆け回っている。\n……三毛猫のミケだ。",
            "明日の朝9時は、動物病院の予約。\nなのにミケは、キャリーを見た瞬間に姿を消した。",
            "朝6時までに、ミケを捕まえなければ。",
            "足音を立てれば、ミケは逃げる。\n家のどこかに、ミケの好きなものがあったはずだ……。",
        };

        // アイテム画面
        GameObject invPanel;
        readonly List<Image> slotBg = new List<Image>();
        readonly List<Image> slotIcon = new List<Image>();
        readonly List<Text> slotName = new List<Text>();
        readonly List<Text> slotGlyph = new List<Text>();
        Text invDesc, invEquip, condText;
        RawImage ecg;
        int invSel;
        readonly Dictionary<string, Image> mapRooms = new Dictionary<string, Image>();
        RectTransform mapPlayer;
        float mapScale;

        // タッチ
        GameObject touchPanel;
        int openedFrame;

        static readonly Color Gold = new Color(0.86f, 0.74f, 0.45f);
        static readonly Color PanelBg = new Color(0.02f, 0.025f, 0.035f, 0.94f);
        static readonly Color Frame = new Color(0.45f, 0.40f, 0.30f, 1f);

        // ───────────────────────── 構築 ─────────────────────────
        public void Build(GameManager g)
        {
            gm = g;
            LoadFonts();
            EnsureEventSystem();

            var cg = new GameObject("UI");
            canvas = cg.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            scaler = cg.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 0.5f;
            cg.AddComponent<GraphicRaycaster>();
            root = cg.GetComponent<RectTransform>();

            // レトロ画質用の表示面
            retroImage = Full("Retro").gameObject.AddComponent<RawImage>();
            retroImage.raycastTarget = false;
            retroImage.enabled = false;
            FixedCameraSystem.I.onRetroChanged = on =>
            {
                retroImage.enabled = on;
                retroImage.texture = FixedCameraSystem.I.RetroTexture;
            };

            // ビネット
            var vig = Full("Vignette").gameObject.AddComponent<RawImage>();
            vig.texture = Tex.Vignette();
            vig.raycastTarget = false;

            redFlash = Full("RedFlash").gameObject.AddComponent<Image>();
            redFlash.color = new Color(0.6f, 0f, 0f, 0f);
            redFlash.raycastTarget = false;

            safe = Full("SafeArea");
            safe.gameObject.AddComponent<SafeArea>();
            BuildHud();
            BuildTouch();
            BuildMessage();
            BuildInventory();
            BuildPause();
            BuildTitle();
            BuildIntro();
            BuildEnding();

            fade = Full("Fade").gameObject.AddComponent<Image>();
            fade.color = new Color(0, 0, 0, 0);
            fade.raycastTarget = false;

            // 変更ID（画面左下に薄く表示。古いビルド・古い公開ページを見ていないかの目印。2026-10-29）
            Txt(Rect("BuildId", root, Vector2.zero, Vector2.zero, new Vector2(8, 4), new Vector2(240, 22)), BuildInfo.Id, 14, new Color(1f, 1f, 1f, 0.3f), TextAnchor.LowerLeft);
        }

        void LoadFonts()
        {
            font = Resources.Load<Font>("Fonts/MC-Sans");
            titleFont = Resources.Load<Font>("Fonts/MC-Serif");
            if (font == null)
            {
                try { font = Font.CreateDynamicFontFromOSFont(new[] { "Meiryo", "Yu Gothic UI", "Hiragino Sans", "Hiragino Kaku Gothic ProN", "Noto Sans CJK JP", "MS Gothic" }, 32); }
                catch (Exception) { font = null; }
            }
            if (font == null)
            {
                try { font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch (Exception) { }
            }
            if (font == null)
            {
                try { font = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch (Exception) { }
            }
            if (titleFont == null) titleFont = font;
        }

        void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
            es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            es.AddComponent<StandaloneInputModule>();
#endif
        }

        // ── 部品 ──
        RectTransform Rect(string name, Transform parent, Vector2 aMin, Vector2 aMax, Vector2 offMin, Vector2 offMax)
        {
            var g = new GameObject(name, typeof(RectTransform));
            var rt = g.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = aMin; rt.anchorMax = aMax;
            rt.offsetMin = offMin; rt.offsetMax = offMax;
            return rt;
        }

        RectTransform Center(RectTransform parent)
        {
            var rt = Rect("content", parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(1280, 720);
            rt.anchoredPosition = Vector2.zero;
            return rt;
        }

        RectTransform Full(string name, Transform parent = null)
        {
            return Rect(name, parent != null ? parent : root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        }

        /// <summary>左下原点の座標指定（1280×720 基準）</summary>
        RectTransform Box(string name, Transform parent, float x, float y, float w, float h)
        {
            var rt = Rect(name, parent, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            rt.pivot = Vector2.zero;
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        Image Img(RectTransform rt, Color c, bool raycast = false)
        {
            var i = rt.gameObject.AddComponent<Image>();
            i.color = c;
            i.raycastTarget = raycast;
            return i;
        }

        Text Txt(RectTransform rt, string s, int size, Color c, TextAnchor align, Font f = null)
        {
            var t = rt.gameObject.AddComponent<Text>();
            t.font = f != null ? f : font;
            t.text = s;
            t.fontSize = size;
            t.color = c;
            t.alignment = align;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            t.supportRichText = true;
            var sh = rt.gameObject.AddComponent<Shadow>();
            sh.effectColor = new Color(0, 0, 0, 0.8f);
            sh.effectDistance = new Vector2(2, -2);
            return t;
        }

        void Framed(RectTransform rt, Color bg)
        {
            Img(rt, Frame);
            var inner = Rect("inner", rt, Vector2.zero, Vector2.one, new Vector2(2, 2), new Vector2(-2, -2));
            Img(inner, bg);
            inner.SetAsFirstSibling();
            inner.SetSiblingIndex(0);
        }

        Button Btn(Transform parent, string label, float x, float y, float w, float h, Action onClick, int size = 26)
        {
            var rt = Box("btn_" + label, parent, x, y, w, h);
            var img = Img(rt, new Color(0.12f, 0.11f, 0.09f, 0.95f), true);
            var outline = rt.gameObject.AddComponent<Outline>();
            outline.effectColor = Frame;
            outline.effectDistance = new Vector2(2, 2);
            var b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            var colors = b.colors;
            colors.highlightedColor = new Color(1.3f, 1.25f, 1.1f);
            colors.pressedColor = new Color(0.7f, 0.7f, 0.7f);
            b.colors = colors;
            b.onClick.AddListener(() =>
            {
                if (AudioMan.I != null) AudioMan.I.Play2D(Sfx.Cursor, 0.6f);
                onClick();
            });
            var t = Txt(Full("label", rt), label, size, Gold, TextAnchor.MiddleCenter);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            return b;
        }

        TouchButton TouchBtn(Transform parent, string id, string label, float x, float y, float w, float h, int size = 30)
        {
            var rt = Box("touch_" + id, parent, x, y, w, h);
            var img = Img(rt, new Color(1, 1, 1, 0.18f), true);
            var tb = rt.gameObject.AddComponent<TouchButton>();
            tb.id = id;
            tb.img = img;
            var t = Txt(Full("label", rt), label, size, new Color(1, 1, 1, 0.85f), TextAnchor.MiddleCenter);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            return tb;
        }

        // ───────────────────────── HUD ─────────────────────────
        void BuildHud()
        {
            var h = Full("HUD", safe);
            hud = h.gameObject.AddComponent<CanvasGroup>();
            hud.blocksRaycasts = false;
            clock = Txt(Rect("Clock", h, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-260, -60), new Vector2(-24, -14)), "AM 2:00", 30, new Color(0.85f, 0.85f, 0.8f), TextAnchor.UpperRight, titleFont);
            roomLabel = Txt(Rect("Room", h, new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -60), new Vector2(400, -14)), "", 26, new Color(0.75f, 0.72f, 0.62f), TextAnchor.UpperLeft, titleFont);
            prompt = Txt(Rect("Prompt", h, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-300, 40), new Vector2(300, 80)), "", 26, Gold, TextAnchor.MiddleCenter);
            toast = Txt(Rect("Toast", h, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-560, -150), new Vector2(560, -70)), "", 26, Color.white, TextAnchor.MiddleCenter);
            hud.alpha = 0f;
        }

        public void Toast(string s)
        {
            toast.text = s;
            toastT = 3.6f;
        }

        public void FlashRed() { redT = 0.6f; }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            // 横長（iPhone）は高さ基準、4:3（iPad）は幅基準で 1280×720 が必ず収まるように
            if (Screen.height > 0) scaler.matchWidthOrHeight = (Screen.width / (float)Screen.height) >= 1.77f ? 1f : 0f;
            // HUD
            bool playing = gm.state == GState.Play || gm.state == GState.Ending;
            hud.alpha = Mathf.MoveTowards(hud.alpha, playing || gm.state == GState.Message ? 1f : 0f, dt * 4f);
            clock.text = gm.ClockText;
            roomLabel.text = gm.house.RoomName(gm.player.transform.position);
            toastT -= dt;
            var tc = toast.color; tc.a = Mathf.Clamp01(toastT * 1.5f); toast.color = tc;
            redT = Mathf.Max(0f, redT - dt);
            redFlash.color = new Color(0.6f, 0f, 0f, redT * 0.6f);

            if (gm.state == GState.Play)
            {
                SearchPoint sp; LightSwitch sw;
                var k = gm.player.CurrentTarget(out sp, out sw);
                string key = (GameInput.touchUsed || GameInput.padUsed) ? "" : " [E]";
                prompt.text = k == PlayerController.TargetKind.Cat ? "▶ 捕まえる" + key
                    : k == PlayerController.TargetKind.Search ? "▶ 調べる" + key
                    : k == PlayerController.TargetKind.Switch ? "▶ " + (sw.on ? "消す" : "つける") + key
                    : "";
                // 体力が少ないと画面の端が赤く脈打つ
                if (gm.player.hp <= 1 && redT <= 0f) redFlash.color = new Color(0.5f, 0f, 0f, 0.08f + 0.06f * Mathf.Sin(Time.time * 4f));
            }
            else prompt.text = "";

            if (touchPanel != null)
            {
                touchPanel.SetActive(gm.state == GState.Play && ShowTouch);
                if (touchPanel.activeSelf) UpdateTouchLabels();
            }
            if (Time.frameCount <= openedFrame) return;   // 開いたフレームでは同じキーで閉じない

            switch (gm.state)
            {
                case GState.Title: UpdateTitle(); break;
                case GState.Intro: UpdateIntro(dt); break;
                case GState.Message: UpdateMessage(dt); break;
                case GState.Inventory: UpdateInventory(); break;
                case GState.Pause:
                    if (GameInput.Pressed(GameInput.Cancel) || GameInput.Pressed(GameInput.Menu)) gm.ClosePause();
                    break;
                case GState.Ending:
                    if (endingPanel.activeSelf && endingShownT > 1.5f && GameInput.Pressed(GameInput.Confirm)) gm.Restart();
                    endingShownT += dt;
                    break;
            }
        }

        // ───────────────────────── タッチ操作 ─────────────────────────
        bool touchOverride;
        bool touchForced;
        public bool ShowTouch
        {
            get
            {
                if (touchOverride) return touchForced;
                return Application.isMobilePlatform || GameInput.touchUsed || SystemInfo.deviceType == DeviceType.Handheld;
            }
        }

        Text aLabel, xLabel, yLabel;
        Image aImg;
        static Sprite discSprite, ringSprite;

        /// <summary>なめらかな円（ボタン・スティック用）</summary>
        static Sprite MakeCircle(bool ring)
        {
            int n = 128;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false);
            t.wrapMode = TextureWrapMode.Clamp;
            var px = new Color[n * n];
            float r = n * 0.5f - 1.5f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n * 0.5f, n * 0.5f));
                    float a = Mathf.Clamp01(r - d + 0.5f);
                    if (ring) a *= Mathf.Clamp01(d - (r - 6f) + 0.5f) * 1f + Mathf.Clamp01(1f - d / r) * 0.25f;
                    else a *= 0.75f + 0.25f * (1f - d / r);
                    px[y * n + x] = new Color(1, 1, 1, a);
                }
            t.SetPixels(px); t.Apply(false);
            return Sprite.Create(t, new UnityEngine.Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
        }

        TouchButton RoundBtn(RectTransform parent, string id, string label, string corner, Vector2 pos, float size, Color tint, int fontSize, out Text labelText)
        {
            var rt = Rect("pad_" + id, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            rt.sizeDelta = new Vector2(size, size);
            rt.anchoredPosition = pos;
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = discSprite;
            var normal = new Color(tint.r, tint.g, tint.b, 0.20f);
            img.color = normal;
            img.raycastTarget = true;
            var ring = Img(Full("ring", rt), new Color(tint.r, tint.g, tint.b, 0.55f));
            ring.sprite = ringSprite;
            var tb = rt.gameObject.AddComponent<TouchButton>();
            tb.id = id;
            tb.img = img;
            tb.normal = normal;
            tb.pressed = new Color(tint.r, tint.g, tint.b, 0.55f);
            labelText = Txt(Full("label", rt), label, fontSize, new Color(1, 1, 1, 0.92f), TextAnchor.MiddleCenter);
            labelText.horizontalOverflow = HorizontalWrapMode.Overflow;
            if (!string.IsNullOrEmpty(corner))
            {
                var c = Txt(Rect("corner", rt, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-30, -34), new Vector2(30, -4)), corner, 18, new Color(tint.r, tint.g, tint.b, 0.9f), TextAnchor.MiddleCenter, titleFont);
                c.horizontalOverflow = HorizontalWrapMode.Overflow;
            }
            return tb;
        }

        /// <summary>
        /// iPhone 横持ち用コントローラー。
        /// 左手：画面の左側どこでも触った所が中心になるスティック（少し倒す＝しのび足）
        /// 右手：A 調べる/捕まえる（大）・B 走る（押しっぱなし）・X アイテム使用・Y ライト
        /// 右上：ITEM・ポーズ
        /// </summary>
        void BuildTouch()
        {
            if (discSprite == null) { discSprite = MakeCircle(false); ringSprite = MakeCircle(true); }
            var p = Full("Touch", safe);
            touchPanel = p.gameObject;

            // ── 左手スティック ──
            var area = Rect("StickArea", p, Vector2.zero, new Vector2(0.45f, 0.8f), Vector2.zero, Vector2.zero);
            area.pivot = Vector2.zero;
            Img(area, new Color(0, 0, 0, 0), true);
            var baseRt = Rect("StickBase", area, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            baseRt.pivot = new Vector2(0.5f, 0.5f);
            baseRt.sizeDelta = new Vector2(240, 240);
            var bImg = baseRt.gameObject.AddComponent<Image>();
            bImg.sprite = ringSprite; bImg.color = new Color(1, 1, 1, 0.18f); bImg.raycastTarget = false;
            var knob = Rect("StickKnob", area, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            knob.pivot = new Vector2(0.5f, 0.5f);
            knob.sizeDelta = new Vector2(112, 112);
            var kImg = knob.gameObject.AddComponent<Image>();
            kImg.sprite = discSprite; kImg.color = new Color(1, 1, 1, 0.35f); kImg.raycastTarget = false;
            var st = area.gameObject.AddComponent<VirtualStick>();
            st.area = area; st.baseRt = baseRt; st.knob = knob; st.baseImg = bImg; st.knobImg = kImg;
            st.radius = 100f;
            st.restPos = new Vector2(200f, 190f);
            var hint = Txt(Rect("hint", area, Vector2.zero, Vector2.zero, new Vector2(80, 26), new Vector2(330, 56)), "少し倒す＝しのび足", 18, new Color(1, 1, 1, 0.35f), TextAnchor.MiddleCenter);
            hint.horizontalOverflow = HorizontalWrapMode.Overflow;

            // ── 右手ボタン（ゲームパッドと同じひし形配置） ──
            var cluster = Rect("FaceButtons", p, new Vector2(1, 0), new Vector2(1, 0), Vector2.zero, Vector2.zero);
            Vector2 c = new Vector2(-230f, 228f);
            Text tmp;
            var a = RoundBtn(cluster, GameInput.Action, "調べる", "A", c + new Vector2(0, -122), 158, new Color(0.95f, 0.82f, 0.45f), 26, out aLabel);
            aImg = a.img;
            RoundBtn(cluster, GameInput.Run, "走る", "B", c + new Vector2(124, 0), 122, new Color(0.95f, 0.45f, 0.4f), 24, out tmp);
            RoundBtn(cluster, GameInput.Item, "使う", "X", c + new Vector2(-124, 0), 122, new Color(0.5f, 0.75f, 1f), 20, out xLabel);
            RoundBtn(cluster, GameInput.Torch, "ライト", "Y", c + new Vector2(0, 118), 108, new Color(0.6f, 0.95f, 0.55f), 20, out yLabel);

            // ── 右上 ──
            var top = Rect("TopButtons", p, new Vector2(1, 1), new Vector2(1, 1), Vector2.zero, Vector2.zero);
            RoundBtn(top, GameInput.Menu, "ITEM", "", new Vector2(-74, -112), 92, new Color(0.9f, 0.85f, 0.7f), 20, out tmp);
            RoundBtn(top, GameInput.Cancel, "Ⅱ", "", new Vector2(-176, -112), 78, new Color(0.8f, 0.8f, 0.8f), 24, out tmp);
            touchPanel.SetActive(false);
        }

        void UpdateTouchLabels()
        {
            SearchPoint sp; LightSwitch sw;
            var k = gm.player.CurrentTarget(out sp, out sw);
            bool hot = k != PlayerController.TargetKind.None;
            aLabel.text = k == PlayerController.TargetKind.Cat ? "捕まえる"
                : k == PlayerController.TargetKind.Switch ? (sw.on ? "消す" : "つける")
                : "調べる";
            float pulse = hot ? 0.35f + 0.2f * Mathf.Sin(Time.unscaledTime * 6f) : 0.16f;
            if (!GameInput.Held(GameInput.Action)) { var col = aImg.color; col.a = pulse; aImg.color = col; }
            var eq = gm.inv.equipped;
            xLabel.text = (eq != ItemId.None && gm.inv.Has(eq) && eq != ItemId.Flashlight) ? ShortName(eq) : "使う";
            yLabel.text = gm.player.FlashlightOn ? "ライト\nOFF" : "ライト\nON";
        }

        static string ShortName(ItemId id)
        {
            switch (id)
            {
                case ItemId.Toy: return "じゃらし";
                case ItemId.Food: return "カリカリ";
                case ItemId.Paste: return "おやつ";
                case ItemId.Net: return "ネット";
                case ItemId.Matatabi: return "またたび";
                case ItemId.Bandage: return "絆創膏";
            }
            return "使う";
        }

        // ───────────────────────── メッセージ ─────────────────────────
        void BuildMessage()
        {
            // 画面のどこをタップしても文章を送れるように
            var tapAll = Full("MessageTap");
            Img(tapAll, new Color(0, 0, 0, 0), true);
            var tapBtn = tapAll.gameObject.AddComponent<Button>();
            tapBtn.transition = Selectable.Transition.None;
            tapBtn.onClick.AddListener(() => GameInput.TouchDown(GameInput.Confirm));
            msgTap = tapAll.gameObject;
            msgTap.SetActive(false);
            var rt = Rect("Message", root, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-520, 30), new Vector2(520, 200));
            Framed(rt, new Color(0.01f, 0.01f, 0.015f, 0.92f));
            msgPanel = rt.gameObject;
            var tb = Img(Full("tap", rt), new Color(0, 0, 0, 0), true);
            var btn = tb.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() => GameInput.TouchDown(GameInput.Confirm));
            msgText = Txt(Rect("text", rt, Vector2.zero, Vector2.one, new Vector2(36, 26), new Vector2(-36, -26)), "", 30, new Color(0.92f, 0.9f, 0.84f), TextAnchor.UpperLeft);
            msgArrow = Txt(Rect("arrow", rt, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-60, 10), new Vector2(-20, 50)), "▼", 26, Gold, TextAnchor.MiddleCenter);
            msgPanel.SetActive(false);
        }

        public void ShowMessage(string[] lines, Action done)
        {
            msgLines = lines;
            msgIndex = 0;
            msgChars = 0f;
            msgDone = done;
            openedFrame = Time.frameCount;
            msgTap.SetActive(true);
            msgTap.transform.SetAsLastSibling();
            msgPanel.SetActive(true);
            msgPanel.transform.SetAsLastSibling();
            fade.transform.SetAsLastSibling();
        }

        void UpdateMessage(float dt)
        {
            if (msgLines == null) return;
            string line = msgLines[msgIndex];
            int before = Mathf.FloorToInt(msgChars);
            msgChars = Mathf.Min(line.Length, msgChars + dt * 38f);
            int after = Mathf.FloorToInt(msgChars);
            if (after > before && after % 2 == 0 && AudioMan.I != null) AudioMan.I.Play2D(Sfx.Type, 0.25f);
            msgText.text = line.Substring(0, after);
            bool full = after >= line.Length;
            msgArrow.enabled = full && Mathf.Repeat(Time.unscaledTime, 0.8f) < 0.5f;
            if (GameInput.Pressed(GameInput.Confirm) || GameInput.Pressed(GameInput.Cancel))
            {
                if (!full) msgChars = line.Length;
                else
                {
                    msgIndex++;
                    msgChars = 0f;
                    if (msgIndex >= msgLines.Length)
                    {
                        msgPanel.SetActive(false);
                        msgTap.SetActive(false);
                        msgLines = null;
                        var d = msgDone; msgDone = null;
                        if (d != null) d();
                    }
                }
            }
        }

        // ───────────────────────── アイテム画面 ─────────────────────────
        void BuildInventory()
        {
            var p = Full("Inventory");
            Img(p, PanelBg, true);
            invPanel = p.gameObject;
            p = Center(p);   // 中身は 1280×720 の枠に収めて中央へ（iPhone の横長画面対策）
            Txt(Box("title", p, 40, 650, 400, 50), "ITEM", 40, Gold, TextAnchor.MiddleLeft, titleFont);

            // ステータス（心電図）
            var st = Box("status", p, 40, 480, 400, 150);
            Framed(st, new Color(0, 0.02f, 0.01f, 1f));
            Txt(Box("lbl", st, 14, 112, 300, 30), "CONDITION", 20, new Color(0.6f, 0.6f, 0.55f), TextAnchor.MiddleLeft);
            ecg = Box("ecg", st, 10, 30, 380, 80).gameObject.AddComponent<RawImage>();
            ecg.texture = Tex.Ecg();
            ecg.raycastTarget = false;
            condText = Txt(Box("cond", st, 200, 106, 186, 36), "Fine", 30, Color.green, TextAnchor.MiddleRight, titleFont);

            var eq = Box("equip", p, 40, 380, 400, 84);
            Framed(eq, new Color(0.03f, 0.03f, 0.03f, 1f));
            Txt(Box("lbl", eq, 14, 46, 200, 30), "装備中", 20, new Color(0.6f, 0.6f, 0.55f), TextAnchor.MiddleLeft);
            invEquip = Txt(Box("name", eq, 14, 8, 370, 40), "", 28, Color.white, TextAnchor.MiddleLeft);

            // 地図
            var map = Box("map", p, 40, 40, 400, 324);
            Framed(map, new Color(0.02f, 0.03f, 0.04f, 1f));
            Txt(Box("lbl", map, 14, 290, 200, 30), "MAP", 20, new Color(0.6f, 0.6f, 0.55f), TextAnchor.MiddleLeft);
            mapScale = Mathf.Min(360f / 16f, 260f / 14f);
            float ox = 20 + (360f - 16f * mapScale) * 0.5f, oy = 18;
            var mapArea = Box("area", map, ox, oy, 16f * mapScale, 14f * mapScale);
            foreach (var r in gm.house.rooms)
            {
                var rr = Box(r.name, mapArea, r.rect.xMin * mapScale + 1, r.rect.yMin * mapScale + 1, r.rect.width * mapScale - 2, r.rect.height * mapScale - 2);
                mapRooms[r.name] = Img(rr, new Color(0.15f, 0.25f, 0.35f, 1f));
                var lbl = Txt(Full("lbl", rr), r.name, 15, new Color(0.85f, 0.9f, 0.95f), TextAnchor.MiddleCenter);
                lbl.horizontalOverflow = HorizontalWrapMode.Overflow;
            }
            mapPlayer = Box("player", mapArea, 0, 0, 10, 10);
            mapPlayer.pivot = new Vector2(0.5f, 0.5f);
            Img(mapPlayer, new Color(1f, 0.25f, 0.2f));

            // アイテム欄 4×2
            for (int i = 0; i < 8; i++)
            {
                int cx = i % 4, cy = i / 4;
                var slot = Box("slot" + i, p, 480 + cx * 192, 470 - cy * 160, 180, 150);
                var bg = Img(slot, new Color(0.08f, 0.08f, 0.07f, 1f), true);
                var ol = slot.gameObject.AddComponent<Outline>();
                ol.effectColor = new Color(0.3f, 0.28f, 0.22f);
                ol.effectDistance = new Vector2(2, 2);
                int idx = i;
                var b = slot.gameObject.AddComponent<Button>();
                b.transition = Selectable.Transition.None;
                b.onClick.AddListener(() => OnSlotClicked(idx));
                slotBg.Add(bg);
                var icon = Img(Box("icon", slot, 50, 52, 80, 80), Color.white);
                slotIcon.Add(icon);
                slotGlyph.Add(Txt(Full("g", icon.rectTransform), "", 40, new Color(0.1f, 0.08f, 0.06f), TextAnchor.MiddleCenter, titleFont));
                slotName.Add(Txt(Box("name", slot, 4, 6, 172, 40), "", 20, Color.white, TextAnchor.MiddleCenter));
            }

            var desc = Box("desc", p, 480, 140, 756, 150);
            Framed(desc, new Color(0.03f, 0.03f, 0.03f, 1f));
            invDesc = Txt(Rect("t", desc, Vector2.zero, Vector2.one, new Vector2(20, 14), new Vector2(-20, -14)), "", 24, new Color(0.9f, 0.88f, 0.82f), TextAnchor.UpperLeft);

            Btn(p, "使う／装備", 480, 40, 260, 80, UseSelected, 28);
            Btn(p, "閉じる", 760, 40, 200, 80, () => gm.CloseInventory(), 28);
            Txt(Box("help", p, 980, 40, 260, 80), "矢印：選択\nE/Enter：使う\nTAB/Esc：閉じる", 18, new Color(0.6f, 0.6f, 0.55f), TextAnchor.MiddleLeft);
            invPanel.SetActive(false);
        }

        public void OpenInventory()
        {
            invSel = Mathf.Max(0, gm.inv.order.IndexOf(gm.inv.equipped));
            openedFrame = Time.frameCount;
            invPanel.SetActive(true);
            invPanel.transform.SetAsLastSibling();
            fade.transform.SetAsLastSibling();
            RefreshInventory();
        }

        public void CloseInventory() { invPanel.SetActive(false); }

        void OnSlotClicked(int i)
        {
            if (i >= gm.inv.order.Count) return;
            if (invSel == i) { UseSelected(); return; }
            invSel = i;
            if (AudioMan.I != null) AudioMan.I.Play2D(Sfx.Cursor, 0.5f);
            RefreshInventory();
        }

        void UseSelected()
        {
            if (invSel >= gm.inv.order.Count) return;
            var id = gm.inv.order[invSel];
            gm.inv.equipped = id;
            gm.CloseInventory();
            gm.player.UseItem(id);
        }

        void UpdateInventory()
        {
            int n = gm.inv.order.Count;
            int prev = invSel;
            Vector2 nav = GameInput.NavPressed;
            if (GameInput.Pressed(GameInput.Right) || nav.x > 0) invSel = Mathf.Min(n - 1, invSel + 1);
            if (GameInput.Pressed(GameInput.Left) || nav.x < 0) invSel = Mathf.Max(0, invSel - 1);
            if (GameInput.Pressed(GameInput.Down) || nav.y < 0) invSel = Mathf.Min(n - 1, invSel + 4);
            if (GameInput.Pressed(GameInput.Up) || nav.y > 0) invSel = Mathf.Max(0, invSel - 4);
            if (invSel != prev && AudioMan.I != null) AudioMan.I.Play2D(Sfx.Cursor, 0.5f);
            if (GameInput.Pressed(GameInput.Cancel) || GameInput.Pressed(GameInput.Menu)) { gm.CloseInventory(); return; }
            if (GameInput.Pressed(GameInput.Confirm)) { UseSelected(); return; }
            RefreshInventory();
            // 心電図を流す
            var uv = ecg.uvRect;
            float speed = gm.player.hp >= 4 ? 0.35f : (gm.player.hp >= 2 ? 0.5f : 0.8f);
            uv.x = Mathf.Repeat(uv.x + Time.unscaledDeltaTime * speed, 1f);
            ecg.uvRect = uv;
        }

        void RefreshInventory()
        {
            var inv = gm.inv;
            for (int i = 0; i < 8; i++)
            {
                bool has = i < inv.order.Count;
                slotIcon[i].enabled = has;
                slotGlyph[i].text = "";
                slotName[i].text = "";
                slotBg[i].color = (has && i == invSel) ? new Color(0.35f, 0.3f, 0.18f, 1f) : new Color(0.08f, 0.08f, 0.07f, 1f);
                if (!has) continue;
                var info = Inventory.Info[inv.order[i]];
                slotIcon[i].color = info.color;
                slotGlyph[i].text = info.name.Substring(0, 1);
                int c = inv.Count(inv.order[i]);
                slotName[i].text = info.name + (info.consumable ? " ×" + c : "") + (inv.order[i] == inv.equipped ? " ◆" : "");
            }
            if (invSel < inv.order.Count)
            {
                var info = Inventory.Info[inv.order[invSel]];
                invDesc.text = "<b>" + info.name + "</b>\n" + info.desc;
            }
            else invDesc.text = "";
            invEquip.text = inv.equipped != ItemId.None && inv.Has(inv.equipped) ? Inventory.Info[inv.equipped].name : "なし";
            string cond = gm.player.Condition;
            Color cc = cond == "Fine" ? new Color(0.3f, 1f, 0.4f) : (cond == "Caution" ? new Color(1f, 0.8f, 0.2f) : new Color(1f, 0.25f, 0.2f));
            condText.text = cond;
            condText.color = cc;
            ecg.color = cc;
            // 地図
            string here = gm.house.RoomName(gm.player.transform.position);
            foreach (var kv in mapRooms)
                kv.Value.color = kv.Key == here ? new Color(0.55f, 0.35f, 0.15f, 1f) : new Color(0.15f, 0.25f, 0.35f, 1f);
            Vector3 pp = gm.player.transform.position;
            mapPlayer.anchoredPosition = new Vector2(pp.x * mapScale, pp.z * mapScale);
        }

        // ───────────────────────── ポーズ ─────────────────────────
        void BuildPause()
        {
            var p = Full("Pause");
            Img(p, new Color(0, 0, 0, 0.85f), true);
            pausePanel = p.gameObject;
            p = Center(p);   // 中身は 1280×720 の枠に収めて中央へ（iPhone の横長画面対策）
            Txt(Box("t", p, 0, 560, 1280, 80), "PAUSE", 54, Gold, TextAnchor.MiddleCenter, titleFont);
            Txt(Box("help", p, 240, 250, 800, 300),
                "【操作】\n" +
                "↑↓←→ / WASD ……… 移動（タンク操作：↑前進 ↓後退 ←→旋回）\n" +
                "Shift / X ……… 走る（足音が大きく、ミケに気づかれる）\n" +
                "E / Space ……… 調べる・捕まえる\n" +
                "Q ……… 装備アイテムを使う　　TAB ……… アイテム画面\n" +
                "R ……… レトロ画質　　T ……… カメラ基準の操作に切替\n\n" +
                "【コツ】手の届かない所に隠れたら、好きなもので誘い出そう。",
                22, new Color(0.88f, 0.86f, 0.8f), TextAnchor.UpperLeft);
            Btn(p, "再開", 240, 120, 180, 80, () => gm.ClosePause());
            Btn(p, "レトロ画質", 440, 120, 200, 80, () => gm.ToggleRetro(), 24);
            Btn(p, "操作切替", 660, 120, 180, 80, () =>
            {
                gm.player.cameraRelative = !gm.player.cameraRelative;
                gm.Toast(gm.player.cameraRelative ? "操作：カメラ基準" : "操作：タンク式");
            }, 24);
            Btn(p, "タッチ表示", 860, 120, 180, 80, () => { bool cur = ShowTouchRaw(); touchOverride = true; touchForced = !cur; gm.Toast(touchForced ? "タッチ操作ボタン：表示" : "タッチ操作ボタン：非表示"); }, 24);
            Btn(p, "最初から", 1060, 120, 180, 80, () => gm.Restart(), 24);
            pausePanel.SetActive(false);
        }

        bool ShowTouchRaw()
        {
            if (touchOverride) return touchForced;
            return Application.isMobilePlatform || GameInput.touchUsed || SystemInfo.deviceType == DeviceType.Handheld;
        }

        public void OpenPause()
        {
            openedFrame = Time.frameCount;
            pausePanel.SetActive(true);
            pausePanel.transform.SetAsLastSibling();
            fade.transform.SetAsLastSibling();
        }
        public void ClosePause() { pausePanel.SetActive(false); }

        // ───────────────────────── タイトル ─────────────────────────
        void BuildTitle()
        {
            var p = Full("Title");
            Img(p, new Color(0, 0, 0, 1f), true);
            titlePanel = p.gameObject;
            var b = p.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            b.onClick.AddListener(StartIntro);
            p = Center(p);
            Txt(Box("sub", p, 0, 520, 1280, 40), "─ 午前二時、三毛猫捕獲作戦 ─", 24, new Color(0.6f, 0.55f, 0.45f), TextAnchor.MiddleCenter, titleFont);
            Txt(Box("title", p, 0, 400, 1280, 120), "深夜二時のミケ", 92, new Color(0.88f, 0.84f, 0.74f), TextAnchor.MiddleCenter, titleFont);
            Txt(Box("en", p, 0, 350, 1280, 50), "M I D N I G H T   C A L I C O", 26, new Color(0.65f, 0.15f, 0.12f), TextAnchor.MiddleCenter, titleFont);
            pressStart = Txt(Box("press", p, 0, 210, 1280, 50), "PRESS START", 30, Gold, TextAnchor.MiddleCenter, titleFont);
            Txt(Box("guide", p, 0, 40, 1280, 120),
                "移動：↑↓←→ / WASD（タンク操作）　走る：Shift　調べる・捕まえる：E / Space\n" +
                "アイテム：TAB　使う：Q　ポーズ：Esc　レトロ画質：R\nスマホはタップで開始（画面のボタンで操作）",
                20, new Color(0.55f, 0.53f, 0.48f), TextAnchor.MiddleCenter);
            titlePanel.SetActive(false);
        }

        public void ShowTitle()
        {
            titlePanel.SetActive(true);
            gm.state = GState.Title;
        }

        void UpdateTitle()
        {
            pressStart.enabled = Mathf.Repeat(Time.unscaledTime, 1.2f) < 0.8f;
            if (GameInput.Pressed(GameInput.Confirm) || GameInput.AnyKey) StartIntro();
        }

        void StartIntro()
        {
            if (gm.state != GState.Title) return;
            if (AudioMan.I != null) AudioMan.I.Play2D(Sfx.Cursor, 0.7f, 0.5f);
            titlePanel.SetActive(false);
            introPanel.SetActive(true);
            introPage = 0;
            introChars = 0;
            gm.state = GState.Intro;
        }

        // ───────────────────────── 導入 ─────────────────────────
        void BuildIntro()
        {
            var p = Full("Intro");
            Img(p, Color.black, true);
            introPanel = p.gameObject;
            var b = p.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            b.onClick.AddListener(() => GameInput.TouchDown(GameInput.Confirm));
            p = Center(p);
            introText = Txt(Box("text", p, 140, 220, 1000, 280), "", 36, new Color(0.88f, 0.86f, 0.8f), TextAnchor.MiddleCenter, titleFont);
            Btn(p, "スキップ", 1280 - 220, 30, 190, 64, BeginFromIntro, 22);
            introPanel.SetActive(false);
        }

        void UpdateIntro(float dt)
        {
            string page = IntroPages[introPage];
            introChars = Mathf.Min(page.Length, introChars + dt * 22f);
            introText.text = page.Substring(0, Mathf.FloorToInt(introChars));
            if (GameInput.Pressed(GameInput.Confirm))
            {
                if (introChars < page.Length) introChars = page.Length;
                else
                {
                    introPage++;
                    introChars = 0;
                    if (introPage >= IntroPages.Length) BeginFromIntro();
                }
            }
        }

        void BeginFromIntro()
        {
            introPanel.SetActive(false);
            gm.BeginPlay();
        }

        // ───────────────────────── エンディング ─────────────────────────
        Text endTitle, endBody, endRank;
        float endingShownT;

        void BuildEnding()
        {
            var p = Full("Ending");
            Img(p, new Color(0, 0, 0, 0.88f), true);
            endingPanel = p.gameObject;
            p = Center(p);   // 中身は 1280×720 の枠に収めて中央へ（iPhone の横長画面対策）
            endTitle = Txt(Box("title", p, 0, 540, 1280, 100), "", 64, new Color(0.9f, 0.86f, 0.76f), TextAnchor.MiddleCenter, titleFont);
            endBody = Txt(Box("body", p, 240, 230, 800, 290), "", 28, new Color(0.85f, 0.83f, 0.78f), TextAnchor.UpperLeft);
            endRank = Txt(Box("rank", p, 880, 280, 300, 200), "", 150, new Color(0.85f, 0.2f, 0.15f), TextAnchor.MiddleCenter, titleFont);
            Btn(p, "もう一度あそぶ", 490, 80, 300, 90, () => gm.Restart(), 30);
            endingPanel.SetActive(false);
        }

        public void ShowEnding(bool success, string rank, float time, int damage, int scratches)
        {
            endingPanel.SetActive(true);
            endingPanel.transform.SetAsLastSibling();
            endingShownT = 0f;
            int m = Mathf.FloorToInt(time / 60f), s = Mathf.FloorToInt(time % 60f);
            if (success)
            {
                endTitle.text = "ミケ、確保。";
                endBody.text =
                    "腕の中で、ミケがゴロゴロとのどを鳴らしている。\n……さっきまでの大暴れが嘘のようだ。\n\n" +
                    "捕獲時刻　　" + gm.ClockText + "\n" +
                    "かかった時間　" + m + "分" + s.ToString("00") + "秒\n" +
                    "家の被害　　" + damage + "か所\n" +
                    "ひっかき傷　" + scratches + "回";
                endRank.text = rank;
            }
            else
            {
                endTitle.text = "夜が明けてしまった……";
                endBody.text =
                    "窓の外が白んでいく。\nミケはどこかで、すやすやと眠っているらしい。\n\n" +
                    "家の被害　　" + damage + "か所\n" +
                    "ひっかき傷　" + scratches + "回\n\n" +
                    "ヒント：好きなもので誘い出し、夢中になっているうちに捕まえよう。";
                endRank.text = "";
            }
        }
    }
}
