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
        Button[] pauseButtons;
        int pauseSelection;
        Text padStatus;
        Text brightnessSetting;
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
            // メニュー入力は GameInput に集約。標準Submitとの二重実行を防ぐ。
            EventSystem.current.sendNavigationEvents = false;

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
            var module = es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            module.pointerBehavior = UnityEngine.InputSystem.UI.UIPointerBehavior.SingleMouseOrPenButMultiTouchAndTrack;
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
            if (gm.nightEvents != null && gm.nightEvents.Quaking)
                roomLabel.text += "　地震 " + Mathf.CeilToInt(gm.nightEvents.QuakeSecondsLeft) + "秒" + (gm.player.ProtectedFromDebris ? " · テーブルの下" : " · テーブルへ！");
            toastT -= dt;
            var tc = toast.color; tc.a = Mathf.Clamp01(toastT * 1.5f); toast.color = tc;
            redT = Mathf.Max(0f, redT - dt);
            redFlash.color = new Color(0.6f, 0f, 0f, redT * 0.6f);

            if (gm.state == GState.Play)
            {
                SearchPoint sp; LightSwitch sw;
                var k = gm.player.CurrentTarget(out sp, out sw);
                string key = (GameInput.touchUsed || GameInput.padUsed) ? "" : " [E]";
                prompt.text = TargetCaption(gm.player.Selection) + (k != PlayerController.TargetKind.None ? key : "");
                // 体力が少ないと画面の端が赤く脈打つ
                if (gm.player.hp <= 1 && redT <= 0f) redFlash.color = new Color(0.5f, 0f, 0f, 0.08f + 0.06f * Mathf.Sin(Time.time * 4f));
            }
            else prompt.text = "";

            UpdateTargetMarker();
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
                    UpdatePause();
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

        Text aLabel, xLabel, yLabel, runLabel, moveLabel, targetName;
        TouchButton actionButton, itemButton, runButton, lightButton;
        RectTransform controlsCluster, targetMarker;
        VirtualStick moveStick;
        int controlsWidth, controlsHeight;
        float appliedSize = -1f, appliedOffset = -1f;
        Text sizeSetting, posSetting, alphaSetting, runSetting, stickSetting;
        public void SetTouchVisible(bool visible) { touchOverride = true; touchForced = visible; }
        public float ControlSize { get { return PlayerPrefs.GetFloat("mc.controlsSize", 1f); } }
        public float ControlOffset { get { return PlayerPrefs.GetFloat("mc.controlsOffset", 0f); } }
        public float ControlAlpha { get { return PlayerPrefs.GetFloat("mc.controlsAlpha", 0.65f); } }
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
            // 操作判定は固定。押したときに動く表示は子へ分離する。
            var hit = Img(rt, Color.clear, true);
            var visual = Full("visual", rt);
            visual.offsetMin = new Vector2(7, 7); visual.offsetMax = new Vector2(-7, -7);
            var img = Img(visual, new Color(tint.r, tint.g, tint.b, 0.28f));
            img.sprite = discSprite;
            var ring = Img(Full("ring", visual), new Color(tint.r, tint.g, tint.b, 0.65f));
            ring.sprite = ringSprite;
            var outer = Rect("pressHalo", rt, Vector2.zero, Vector2.one, new Vector2(-5,-5), new Vector2(5,5));
            var halo = Img(outer, new Color(tint.r, tint.g, tint.b, 0.35f));
            halo.sprite = ringSprite;
            var tb = rt.gameObject.AddComponent<TouchButton>();
            tb.id = id; tb.img = img; tb.visual = visual; tb.halo = halo;
            tb.normal = img.color;
            tb.pressed = new Color(tint.r, tint.g, tint.b, 0.7f);
            labelText = Txt(Full("label", visual), label, fontSize, new Color(1, 1, 1, 0.96f), TextAnchor.MiddleCenter);
            labelText.horizontalOverflow = HorizontalWrapMode.Wrap;
            return tb;
        }

        /// <summary>
        /// iPhone 横持ち用コントローラー。
        /// 左手：画面の左側どこでも触った所が中心になるスティック（少し倒す＝しのび足）
        /// 右手：主操作（大）・走行切替・アイテム使用・ライト
        /// 右上：ITEM・ポーズ
        /// </summary>
        void BuildTouch()
        {
            if (discSprite == null) { discSprite = MakeCircle(false); ringSprite = MakeCircle(true); }
            var p = Full("Touch", safe);
            touchPanel = p.gameObject;

            // ── 左手スティック ──
            var area = Rect("StickArea", p, Vector2.zero, new Vector2(0.42f, 0.54f), Vector2.zero, Vector2.zero);
            area.pivot = Vector2.zero;
            Img(area, new Color(0, 0, 0, 0), true);
            var baseRt = Rect("StickBase", area, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            baseRt.pivot = new Vector2(0.5f, 0.5f);
            baseRt.sizeDelta = new Vector2(190, 190);
            var bImg = baseRt.gameObject.AddComponent<Image>();
            bImg.sprite = ringSprite; bImg.color = new Color(1, 1, 1, 0.18f); bImg.raycastTarget = false;
            var knob = Rect("StickKnob", area, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            knob.pivot = new Vector2(0.5f, 0.5f);
            knob.sizeDelta = new Vector2(88, 88);
            var kImg = knob.gameObject.AddComponent<Image>();
            kImg.sprite = discSprite; kImg.color = new Color(1, 1, 1, 0.35f); kImg.raycastTarget = false;
            var st = area.gameObject.AddComponent<VirtualStick>();
            st.area = area; st.baseRt = baseRt; st.knob = knob; st.baseImg = bImg; st.knobImg = kImg;
            moveStick = st;
            st.radius = 76f;
            st.restPos = new Vector2(155f, 160f);
            moveLabel = Txt(Rect("movementState", p, Vector2.zero, Vector2.zero, new Vector2(55, 22), new Vector2(355, 60)), "歩行 · 少し倒すとしのび足", 19, Gold, TextAnchor.MiddleCenter);

            // 右親指の定位置に主操作。その周囲へ道具・走行・ライトを配置。
            controlsCluster = Rect("FaceButtons", p, new Vector2(1, 0), new Vector2(1, 0), Vector2.zero, Vector2.zero);
            var cluster = controlsCluster;
            actionButton = RoundBtn(cluster, GameInput.Action, "調べる", "", new Vector2(-148, 145), 176, new Color(0.95f, 0.82f, 0.45f), 28, out aLabel);
            aImg = actionButton.img;
            itemButton = RoundBtn(cluster, GameInput.Item, "道具なし", "", new Vector2(-327, 205), 122, new Color(0.5f, 0.75f, 1f), 23, out xLabel);
            runButton = RoundBtn(cluster, GameInput.Run, "走る", "", new Vector2(-142, 330), 126, new Color(0.95f, 0.67f, 0.4f), 23, out runLabel);
            lightButton = RoundBtn(cluster, GameInput.Torch, "ライト", "", new Vector2(-310, 365), 110, new Color(0.7f, 0.86f, 0.72f), 21, out yLabel);
            Text tmp;

            // ── 右上 ──
            var top = Rect("TopButtons", p, new Vector2(1, 1), new Vector2(1, 1), Vector2.zero, Vector2.zero);
            RoundBtn(top, GameInput.Menu, "ITEM", "", new Vector2(-76, -78), 98, new Color(0.9f, 0.85f, 0.7f), 20, out tmp);
            RoundBtn(top, GameInput.Cancel, "Ⅱ", "", new Vector2(-190, -78), 98, new Color(0.8f, 0.8f, 0.8f), 24, out tmp);
            targetMarker = Rect("TargetMarker", safe, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            targetMarker.pivot = new Vector2(0.5f, 0.5f);
            targetMarker.sizeDelta = new Vector2(38, 38);
            var markerImage = Img(Full("ring", targetMarker), Gold);
            markerImage.sprite = ringSprite;
            targetName = Txt(Rect("targetName", targetMarker, new Vector2(0.5f,1), new Vector2(0.5f,1), new Vector2(-180,12), new Vector2(180,48)), "", 21, Gold, TextAnchor.MiddleCenter);
            targetMarker.gameObject.SetActive(false);
            ApplyControlLayout();
            touchPanel.SetActive(false);
        }

        void ApplyControlLayout()
        {
            if (moveStick == null) return;
            float size = Mathf.Clamp(ControlSize, 0.85f, 1.15f);
            float offset = Mathf.Clamp(ControlOffset, 0, 60);
            if (appliedSize == size && appliedOffset == offset && controlsWidth == Screen.width && controlsHeight == Screen.height) return;
            appliedSize = size; appliedOffset = offset; controlsWidth = Screen.width; controlsHeight = Screen.height;
            controlsCluster.localScale = Vector3.one * size;
            controlsCluster.anchoredPosition = new Vector2(-offset, 0);
            moveStick.radius = 76f * size;
            moveStick.baseRt.sizeDelta = new Vector2(190,190) * size;
            moveStick.knob.sizeDelta = new Vector2(88,88) * size;
            moveStick.SetRest(new Vector2(155 + offset, 160));
        }
        void StyleControl(TouchButton button, bool active, bool available)
        {
            button.interactable = available;
            float alpha = available ? (active ? 0.48f : 0.28f) : 0.10f;
            var normal = button.normal; normal.a = alpha * ControlAlpha;
            button.normal = normal;
            if (!button.IsPressed)
            {
                button.img.color = normal;
                var halo = button.halo.color;
                halo.a = (active ? 0.85f : available ? 0.38f : 0.12f) * ControlAlpha;
                button.halo.color = halo;
            }
        }
        void UpdateTouchLabels()
        {
            ApplyControlLayout();
            moveStick.followCenter = PlayerPrefs.GetInt("mc.followStick", 1) != 0;
            var target = gm.player.Selection;
            bool hot = target.kind != PlayerController.TargetKind.None;
            aLabel.text = target.kind == PlayerController.TargetKind.Cat ? "捕まえる"
                : target.kind == PlayerController.TargetKind.NewCat ? "そっと見守る"
                : target.kind == PlayerController.TargetKind.Shelter ? (gm.player.IsSheltered ? "外へ出る" : "下に隠れる")
                : target.kind == PlayerController.TargetKind.Switch ? (target.lightSwitch.on ? "消す" : "つける") : "調べる";
            StyleControl(actionButton, hot, hot);
            var eq = gm.inv.equipped;
            bool hasTool = eq != ItemId.None && gm.inv.Has(eq) && eq != ItemId.Flashlight;
            xLabel.text = hasTool ? ShortName(eq) : "道具なし";
            StyleControl(itemButton, false, hasTool);
            yLabel.text = gm.player.FlashlightOn ? "ライト\n点灯中" : "ライト\n消灯中";
            StyleControl(lightButton, gm.player.FlashlightOn, true);
            bool running = GameInput.Held(GameInput.Run);
            runLabel.text = GameInput.ToggleRunMode ? (running ? "歩くに\n切替" : "走るに\n切替") : "長押し\n走る";
            StyleControl(runButton, running, true);
            moveLabel.text = running ? "走行 · 足音でミケに気づかれる" : gm.player.IsSneaking ? "しのび足" : "歩行 · 少し倒すとしのび足";
            moveLabel.color = running ? new Color(1f,0.7f,0.4f) : Gold;
            var col = moveStick.baseImg.color;
            col.r = running ? 1f : 0.86f; col.g = running ? 0.58f : 0.74f; col.b = running ? 0.3f : 0.45f;
            moveStick.baseImg.color = col;
        }
        string TargetCaption(PlayerController.TargetSelection target)
        {
            if (target.kind == PlayerController.TargetKind.None) return "";
            string verb = target.kind == PlayerController.TargetKind.Cat ? "捕まえる"
                : target.kind == PlayerController.TargetKind.NewCat ? "そっと見守る"
                : target.kind == PlayerController.TargetKind.Shelter ? (gm.player.IsSheltered ? "外へ出る" : "下に隠れる")
                : target.kind == PlayerController.TargetKind.Switch ? (target.lightSwitch.on ? "消す" : "つける") : "調べる";
            return target.name + " · " + verb;
        }
        void UpdateTargetMarker()
        {
            if (targetMarker == null) return;
            var target = gm.player.Selection;
            bool visible = gm.state == GState.Play && target.kind != PlayerController.TargetKind.None;
            if (visible)
            {
                var cam = FixedCameraSystem.I.cam;
                Vector3 point = cam.WorldToViewportPoint(target.position + Vector3.up * 0.12f);
                visible = point.z > 0 && point.x > 0.03f && point.x < 0.97f && point.y > 0.08f && point.y < 0.9f;
                if (visible)
                {
                    Vector2 local;
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(safe, cam.ViewportToScreenPoint(point), null, out local);
                    targetMarker.anchoredPosition = local - safe.rect.min;
                    targetName.text = target.name;
                }
            }
            targetMarker.gameObject.SetActive(visible);
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
            bool upstairs = gm.player.transform.position.y >= 2.6f;
            foreach(var r in gm.house.rooms)
                if(mapRooms.TryGetValue(r.name,out var image)) image.gameObject.SetActive(r.stairs || (r.floorY>=2.6f)==upstairs);
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
                "【タッチ】左で移動・少し倒すとしのび足。右の大ボタンで対象を調べる／捕まえる。\n" +
                "走行は下段でタップ切替／長押しを選択。ライトと道具は右の小ボタン。\n\n" +
                "【キーボード】\n" +
                "↑↓←→ / WASD ……… 移動（タンク操作：↑前進 ↓後退 ←→旋回）\n" +
                "Shift / X ……… 走る（足音が大きく、ミケに気づかれる）\n" +
                "E / Space ……… 調べる・捕まえる\n" +
                "Q ……… 装備アイテムを使う　　TAB ……… アイテム画面\n" +
                "R ……… レトロ画質　　T ……… カメラ基準の操作に切替\n\n" +
                "【Switch】A：調べる／決定　B：道具　Y：ライト点灯切替　ZR：走る\n【共通】左スティック／十字キー：移動\n【PS／Xbox】下ボタン：調べる／決定\n" +
                "右ボタン／RT：走る　左ボタン：道具　上ボタン：ライト\n" +
                "LB／Select：アイテム　Start：ポーズ／戻る",
                20, new Color(0.88f, 0.86f, 0.8f), TextAnchor.UpperLeft);
            padStatus = Txt(Box("padStatus", p, 100, 210, 1080, 36), "", 20, Gold, TextAnchor.MiddleCenter);
            Btn(p, "再開", 240, 120, 180, 80, () => gm.ClosePause());
            Btn(p, "レトロ画質", 440, 120, 200, 80, () => gm.ToggleRetro(), 24);
            Btn(p, "操作切替", 660, 120, 180, 80, () =>
            {
                gm.player.cameraRelative = !gm.player.cameraRelative;
                gm.Toast(gm.player.cameraRelative ? "操作：カメラ基準" : "操作：タンク式");
            }, 24);
            Btn(p, "タッチ表示", 860, 120, 180, 80, () => { bool cur = ShowTouchRaw(); touchOverride = true; touchForced = !cur; gm.Toast(touchForced ? "タッチ操作ボタン：表示" : "タッチ操作ボタン：非表示"); }, 24);
            Btn(p, "最初から", 1060, 120, 180, 80, () => gm.Restart(), 24);
            runSetting = Btn(p, "", 60, 28, 230, 64, () => { GameInput.SetRunMode(!GameInput.ToggleRunMode); RefreshControlSettings(); }, 21).GetComponentInChildren<Text>();
            sizeSetting = Btn(p, "", 306, 28, 205, 64, () => { CyclePreference("mc.controlsSize", new[] {0.85f,1f,1.15f}, 1f); }, 21).GetComponentInChildren<Text>();
            posSetting = Btn(p, "", 527, 28, 205, 64, () => { CyclePreference("mc.controlsOffset", new[] {0f,30f,60f}, 0f); }, 21).GetComponentInChildren<Text>();
            alphaSetting = Btn(p, "", 748, 28, 205, 64, () => { CyclePreference("mc.controlsAlpha", new[] {0.45f,0.65f,0.9f}, 0.65f); }, 21).GetComponentInChildren<Text>();
            stickSetting = Btn(p, "", 969, 28, 250, 64, () => { int next = PlayerPrefs.GetInt("mc.followStick",1) == 0 ? 1 : 0; PlayerPrefs.SetInt("mc.followStick",next); PlayerPrefs.Save(); RefreshControlSettings(); }, 21).GetComponentInChildren<Text>();
            RefreshControlSettings();
            brightnessSetting = Btn(p, "", 960, 580, 260, 52, () =>
            {
                float next = FilmLook.Brightness < 1.1f ? 1.2f : FilmLook.Brightness < 1.3f ? 1.4f : 1f;
                PlayerPrefs.SetFloat("mc.brightness", next);
                PlayerPrefs.Save();
                gm.ApplyVisibility();
                RefreshBrightness();
            }, 21).GetComponentInChildren<Text>();
            RefreshBrightness();
            pauseButtons = pausePanel.GetComponentsInChildren<Button>(true);
            pausePanel.SetActive(false);
        }

        void UpdatePause()
        {
            padStatus.text = "外付けパッド：" + GameInput.PadName + (GameInput.IsSwitchPad ? "　方向で選択・Aで決定・＋で戻る" : "　方向で選択・下ボタンで決定・Startで戻る");
            if (EventSystem.current != null)
                for (int i = 0; i < pauseButtons.Length; i++)
                    if (pauseButtons[i].gameObject == EventSystem.current.currentSelectedGameObject) { pauseSelection = i; break; }
            if (GameInput.Pressed(GameInput.Cancel) || GameInput.Pressed(GameInput.Menu)) { gm.ClosePause(); return; }
            Vector2 nav = GameInput.NavPressed;
            Vector2 direction = Vector2.zero;
            if (GameInput.Pressed(GameInput.Right) || nav.x > 0) direction = Vector2.right;
            else if (GameInput.Pressed(GameInput.Left) || nav.x < 0) direction = Vector2.left;
            else if (GameInput.Pressed(GameInput.Down) || nav.y < 0) direction = Vector2.down;
            else if (GameInput.Pressed(GameInput.Up) || nav.y > 0) direction = Vector2.up;
            int next = pauseSelection;
            float best = float.MaxValue;
            Vector2 origin = pauseButtons[pauseSelection].transform.localPosition;
            for (int i = 0; i < pauseButtons.Length && direction != Vector2.zero; i++)
            {
                Vector2 offset = (Vector2)pauseButtons[i].transform.localPosition - origin;
                float forward = Vector2.Dot(offset, direction);
                if (forward <= 1f) continue;
                float side = Mathf.Abs(offset.x * direction.y - offset.y * direction.x);
                float score = forward + side * 3f;
                if (score < best) { best = score; next = i; }
            }
            if (next != pauseSelection)
            {
                pauseSelection = next;
                SelectPauseButton();
            }
            if (GameInput.Pressed(GameInput.Confirm)) pauseButtons[pauseSelection].onClick.Invoke();
        }

        void SelectPauseButton()
        {
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(pauseButtons[pauseSelection].gameObject);
        }

        void RefreshBrightness()
        {
            brightnessSetting.text = "明るさ：" + (FilmLook.Brightness < 1.1f ? "標準" : FilmLook.Brightness < 1.3f ? "明るい" : "さらに明るい");
        }

        void CyclePreference(string key, float[] options, float fallback)
        {
            float current = PlayerPrefs.GetFloat(key, fallback);
            int index = 0;
            for (int i = 0; i < options.Length; i++) if (Mathf.Abs(current-options[i]) < 0.01f) index = i;
            PlayerPrefs.SetFloat(key, options[(index+1)%options.Length]);
            PlayerPrefs.Save();
            appliedSize = -1f;
            RefreshControlSettings();
        }
        void RefreshControlSettings()
        {
            if (runSetting == null) return;
            runSetting.text = GameInput.ToggleRunMode ? "走行：タップ切替" : "走行：長押し";
            sizeSetting.text = "サイズ：" + Mathf.RoundToInt(ControlSize*100) + "%";
            posSetting.text = "位置：" + (ControlOffset < 1 ? "標準" : ControlOffset < 40 ? "内側" : "さらに内側");
            alphaSetting.text = "濃さ：" + Mathf.RoundToInt(ControlAlpha*100) + "%";
            stickSetting.text = PlayerPrefs.GetInt("mc.followStick",1) != 0 ? "スティック：中心追従" : "スティック：中心固定";
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
            pauseSelection = 0;
            SelectPauseButton();
            pausePanel.transform.SetAsLastSibling();
            fade.transform.SetAsLastSibling();
        }
        public void ClosePause()
        {
            pausePanel.SetActive(false);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }

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
                "アイテム：TAB　使う：Q　ポーズ：Esc　レトロ画質：R\nスマホはタップ／SwitchはAか＋／他のパッドは下ボタンかStartで開始",
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
        RectTransform damageStrip;
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
            // おうちの被害レポート（撮れた証拠写真をポラロイド風に並べる帯。ボタンの下の余白に収める）
            damageStrip = Box("damageStrip", p, 20, 4, 1240, 72);
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
            BuildDamageStrip();
        }

        /// <summary>おうちの被害レポート：捕まえる/朝を迎える瞬間までに撮れた証拠写真をポラロイド風に並べる</summary>
        void BuildDamageStrip()
        {
            if (damageStrip == null) return;
            for (int i = damageStrip.childCount - 1; i >= 0; i--)
                Destroy(damageStrip.GetChild(i).gameObject);
            var events = gm.damageEvents;
            if (events.Count == 0) return;
            float slotW = 1240f / events.Count;
            float cardW = Mathf.Min(110f, slotW - 8f);
            for (int i = 0; i < events.Count; i++)
            {
                var ev = events[i];
                float cx = i * slotW + slotW * 0.5f;
                var card = Box("photo" + i, damageStrip, cx - cardW * 0.5f, 0, cardW, 72);
                Img(card, new Color(0.96f, 0.95f, 0.9f));
                var photoBox = Box("pic", card, 4, 16, cardW - 8, cardW - 8 - 16 > 0 ? Mathf.Min(44f, cardW - 8f) : 40f);
                if (ev.photo != null)
                {
                    var raw = photoBox.gameObject.AddComponent<RawImage>();
                    raw.texture = ev.photo;
                    raw.uvRect = new UnityEngine.Rect(0.15f, 0.15f, 0.7f, 0.7f); // 中央寄りをクロップして被害箇所を見せやすくする
                }
                else
                {
                    Img(photoBox, new Color(0.6f, 0.58f, 0.55f));
                }
                string caption = ev.room + "：" + ev.propName;
                Txt(Box("cap", card, 2, 0, cardW - 4, 16), caption, 11, new Color(0.15f, 0.13f, 0.1f), TextAnchor.MiddleCenter);
            }
        }
    }
}
