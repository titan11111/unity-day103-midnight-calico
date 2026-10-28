using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

namespace MidnightCalico
{
    /// <summary>
    /// 入力をひとつにまとめる：
    ///  ・iPhone の画面コントローラー（左手スティック＋右手ボタン、マルチタッチ）
    ///  ・Bluetooth ゲームパッド（PS / Xbox / MFi）
    ///  ・キーボード（旧 Input Manager / 新 Input System 両対応）
    /// </summary>
    public static class GameInput
    {
        public const string Up = "up", Down = "down", Left = "left", Right = "right",
            Run = "run", Action = "action", Item = "item", Menu = "menu", Cancel = "cancel", Confirm = "confirm", Torch = "light";

        class TouchState { public bool held; public int presses; public int seen; public float pressTime; }
        static readonly Dictionary<string, TouchState> touch = new Dictionary<string, TouchState>();

        /// <summary>画面コントローラーが一度でも触られた（以後キー表記を出さない）</summary>
        public static bool touchUsed;
        /// <summary>ゲームパッドが使われた</summary>
        public static bool padUsed;
        /// <summary>画面スティックの値（-1..1）</summary>
        public static Vector2 stick;

        static TouchState T(string id)
        {
            TouchState t;
            if (!touch.TryGetValue(id, out t)) { t = new TouchState(); touch[id] = t; }
            return t;
        }

        public static void TouchDown(string id)
        {
            var t = T(id);
            t.held = true; t.presses++; t.pressTime = Time.unscaledTime;
            touchUsed = true;
            Haptics.Tap();
        }
        public static void TouchUp(string id) { T(id).held = false; }

        static bool TouchHeld(string id) { return T(id).held; }
        static bool TouchPressed(string id)
        {
            var t = T(id);
            if (t.presses == t.seen) return false;
            t.seen = t.presses;
            return Time.unscaledTime - t.pressTime < 0.35f;
        }

#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        static bool K(Key k) { var kb = Keyboard.current; return kb != null && kb[k].isPressed; }
        static bool KD(Key k) { var kb = Keyboard.current; return kb != null && kb[k].wasPressedThisFrame; }
        static Gamepad Pad { get { var g = Gamepad.current; if (g != null && g.wasUpdatedThisFrame) padUsed = true; return g; } }

        static bool HeldKey(string id)
        {
            var p = Pad;
            switch (id)
            {
                case Up: return K(Key.W) || K(Key.UpArrow) || (p != null && p.dpad.up.isPressed);
                case Down: return K(Key.S) || K(Key.DownArrow) || (p != null && p.dpad.down.isPressed);
                case Left: return K(Key.A) || K(Key.LeftArrow) || (p != null && p.dpad.left.isPressed);
                case Right: return K(Key.D) || K(Key.RightArrow) || (p != null && p.dpad.right.isPressed);
                case Run: return K(Key.LeftShift) || K(Key.RightShift) || K(Key.X) || (p != null && (p.buttonEast.isPressed || p.rightTrigger.isPressed));
            }
            return false;
        }
        static bool PressedKey(string id)
        {
            var p = Pad;
            switch (id)
            {
                case Up: return KD(Key.W) || KD(Key.UpArrow) || (p != null && p.dpad.up.wasPressedThisFrame);
                case Down: return KD(Key.S) || KD(Key.DownArrow) || (p != null && p.dpad.down.wasPressedThisFrame);
                case Left: return KD(Key.A) || KD(Key.LeftArrow) || (p != null && p.dpad.left.wasPressedThisFrame);
                case Right: return KD(Key.D) || KD(Key.RightArrow) || (p != null && p.dpad.right.wasPressedThisFrame);
                case Action: return KD(Key.Space) || KD(Key.E) || KD(Key.Z) || (p != null && p.buttonSouth.wasPressedThisFrame);
                case Item: return KD(Key.Q) || KD(Key.C) || (p != null && p.buttonWest.wasPressedThisFrame);
                case Torch: return KD(Key.F) || (p != null && p.buttonNorth.wasPressedThisFrame);
                case Menu: return KD(Key.Tab) || KD(Key.I) || (p != null && p.selectButton.wasPressedThisFrame);
                case Cancel: return KD(Key.Escape) || KD(Key.Backspace) || (p != null && p.startButton.wasPressedThisFrame);
                case Confirm: return KD(Key.Enter) || KD(Key.NumpadEnter) || KD(Key.Space) || KD(Key.E) || KD(Key.Z) || (p != null && p.buttonSouth.wasPressedThisFrame);
            }
            return false;
        }
        static Vector2 PadStick
        {
            get
            {
                var p = Pad;
                if (p == null) return Vector2.zero;
                Vector2 v = p.leftStick.ReadValue();
                return v.magnitude < 0.2f ? Vector2.zero : v;
            }
        }
        public static bool RetroToggle { get { return KD(Key.R); } }
        public static bool SchemeToggle { get { return KD(Key.T); } }
        public static bool DebugToggle { get { return KD(Key.F1); } }
        public static bool AnyKey
        {
            get
            {
                var kb = Keyboard.current;
                var p = Gamepad.current;
                return (kb != null && kb.anyKey.wasPressedThisFrame) || (p != null && (p.buttonSouth.wasPressedThisFrame || p.startButton.wasPressedThisFrame));
            }
        }
#else
        static bool K(KeyCode k) { return Input.GetKey(k); }
        static bool KD(KeyCode k) { return Input.GetKeyDown(k); }
        // ゲームパッドのボタン番号は機種で違うため、よく使われる2通りを両方見る
        //   一般的（Xbox/PS 系）: 0=A 1=B 2=X 3=Y 6=Back 7=Start
        //   iOS MFi           : 14=A 13=B 15=X 12=Y 0=Pause
        static bool PadHeld(params KeyCode[] ks) { foreach (var k in ks) if (Input.GetKey(k)) { padUsed = true; return true; } return false; }
        static bool PadDown(params KeyCode[] ks) { foreach (var k in ks) if (Input.GetKeyDown(k)) { padUsed = true; return true; } return false; }

        static bool HeldKey(string id)
        {
            switch (id)
            {
                case Up: return K(KeyCode.W) || K(KeyCode.UpArrow);
                case Down: return K(KeyCode.S) || K(KeyCode.DownArrow);
                case Left: return K(KeyCode.A) || K(KeyCode.LeftArrow);
                case Right: return K(KeyCode.D) || K(KeyCode.RightArrow);
                case Run: return K(KeyCode.LeftShift) || K(KeyCode.RightShift) || K(KeyCode.X) || PadHeld(KeyCode.JoystickButton1, KeyCode.JoystickButton13);
            }
            return false;
        }
        static bool PressedKey(string id)
        {
            switch (id)
            {
                case Up: return KD(KeyCode.W) || KD(KeyCode.UpArrow);
                case Down: return KD(KeyCode.S) || KD(KeyCode.DownArrow);
                case Left: return KD(KeyCode.A) || KD(KeyCode.LeftArrow);
                case Right: return KD(KeyCode.D) || KD(KeyCode.RightArrow);
                case Action: return KD(KeyCode.Space) || KD(KeyCode.E) || KD(KeyCode.Z) || PadDown(KeyCode.JoystickButton0, KeyCode.JoystickButton14);
                case Item: return KD(KeyCode.Q) || KD(KeyCode.C) || PadDown(KeyCode.JoystickButton2, KeyCode.JoystickButton15);
                case Torch: return KD(KeyCode.F) || PadDown(KeyCode.JoystickButton3, KeyCode.JoystickButton12);
                case Menu: return KD(KeyCode.Tab) || KD(KeyCode.I) || PadDown(KeyCode.JoystickButton6);
                case Cancel: return KD(KeyCode.Escape) || KD(KeyCode.Backspace) || PadDown(KeyCode.JoystickButton7, KeyCode.JoystickButton9);
                case Confirm: return KD(KeyCode.Return) || KD(KeyCode.KeypadEnter) || KD(KeyCode.Space) || KD(KeyCode.E) || KD(KeyCode.Z) || PadDown(KeyCode.JoystickButton0, KeyCode.JoystickButton14);
            }
            return false;
        }
        static bool axesChecked, axesOk;
        static Vector2 PadStick
        {
            get
            {
                if (!axesChecked)
                {
                    axesChecked = true;
                    try { Input.GetAxisRaw("Horizontal"); Input.GetAxisRaw("Vertical"); axesOk = true; }
                    catch (System.Exception) { axesOk = false; }
                }
                if (!axesOk) return Vector2.zero;
                // キーボードの WASD もこの軸に含まれるので、キーが押されていない時だけ採用
                if (K(KeyCode.W) || K(KeyCode.A) || K(KeyCode.S) || K(KeyCode.D) || K(KeyCode.UpArrow) || K(KeyCode.DownArrow) || K(KeyCode.LeftArrow) || K(KeyCode.RightArrow)) return Vector2.zero;
                var v = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
                if (v.magnitude < 0.2f) return Vector2.zero;
                padUsed = true;
                return v;
            }
        }
        public static bool RetroToggle { get { return KD(KeyCode.R); } }
        public static bool SchemeToggle { get { return KD(KeyCode.T); } }
        public static bool DebugToggle { get { return KD(KeyCode.F1); } }
        public static bool AnyKey { get { return Input.anyKeyDown; } }
#endif

        public static bool Held(string id) { return HeldKey(id) || TouchHeld(id); }

        /// <summary>押した瞬間。タッチの押下は一度だけ消費される。</summary>
        public static bool Pressed(string id)
        {
            bool k = PressedKey(id);
            bool t = TouchPressed(id);
            if (id == Confirm) t |= TouchPressed(Action);
            return k || t;
        }

        /// <summary>アナログ移動量。x = 右、y = 前（上）。大きさ 0..1</summary>
        public static Vector2 Move
        {
            get
            {
                float x = (HeldKey(Right) ? 1f : 0f) - (HeldKey(Left) ? 1f : 0f);
                float y = (HeldKey(Up) ? 1f : 0f) - (HeldKey(Down) ? 1f : 0f);
                Vector2 v = new Vector2(x, y) + stick + PadStick;
                return Vector2.ClampMagnitude(v, 1f);
            }
        }

        /// <summary>メニュー用：スティックを倒した瞬間も方向入力として扱う</summary>
        static Vector2 lastNav;
        static int navFrame = -1;
        static Vector2 navEdge;
        public static Vector2 NavPressed
        {
            get
            {
                if (navFrame == Time.frameCount) return navEdge;
                navFrame = Time.frameCount;
                Vector2 v = stick + PadStick;
                Vector2 d = Vector2.zero;
                if (Mathf.Abs(v.x) > 0.6f && Mathf.Abs(lastNav.x) <= 0.6f) d.x = Mathf.Sign(v.x);
                if (Mathf.Abs(v.y) > 0.6f && Mathf.Abs(lastNav.y) <= 0.6f) d.y = Mathf.Sign(v.y);
                lastNav = v;
                navEdge = d;
                return d;
            }
        }

        public static void ClearTouches()
        {
            foreach (var t in touch.Values) { t.held = false; t.seen = t.presses; }
        }
    }

    /// <summary>iPhone の振動（ネイティブ iOS ビルドのみ。WebGL では何もしない）</summary>
    public static class Haptics
    {
        public static bool enabled = true;
        public static void Tap() { }
        public static void Strong()
        {
#if (UNITY_IOS || UNITY_ANDROID) && !UNITY_EDITOR
            if (enabled) Handheld.Vibrate();
#endif
        }
    }

    /// <summary>押している間だけ有効になる画面ボタン（マルチタッチ対応）</summary>
    public class TouchButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public string id;
        public UnityEngine.UI.Image img;
        public Color normal = new Color(1, 1, 1, 0.16f), pressed = new Color(1, 1, 1, 0.42f);
        public float pressScale = 0.92f;
        int pointers;

        public void OnPointerDown(PointerEventData e)
        {
            pointers++;
            GameInput.TouchDown(id);
            if (img != null) img.color = pressed;
            transform.localScale = Vector3.one * pressScale;
        }
        public void OnPointerUp(PointerEventData e) { Release(); }
        public void OnPointerExit(PointerEventData e) { if (pointers > 0 && id != GameInput.Run) Release(); }

        void Release()
        {
            pointers = 0;
            GameInput.TouchUp(id);
            if (img != null) img.color = normal;
            transform.localScale = Vector3.one;
        }

        void OnDisable() { if (pointers > 0) Release(); }
    }

    /// <summary>
    /// 左手のフローティング・スティック。画面の左側どこを触っても、そこが中心になる。
    /// </summary>
    public class VirtualStick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public RectTransform area, baseRt, knob;
        public UnityEngine.UI.Image baseImg, knobImg;
        public float radius = 100f;
        public Vector2 restPos = new Vector2(230f, 210f);
        int pointerId = int.MinValue;
        Vector2 center;

        void Start() { ResetStick(); }

        Vector2 Local(PointerEventData e)
        {
            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(area, e.position, e.pressEventCamera, out local);
            // area の pivot は (0,0)
            return local;
        }

        public void OnPointerDown(PointerEventData e)
        {
            if (pointerId != int.MinValue) return;
            pointerId = e.pointerId;
            center = Local(e);
            baseRt.anchoredPosition = center;
            knob.anchoredPosition = center;
            SetAlpha(0.5f, 0.85f);
            GameInput.touchUsed = true;
            OnDrag(e);
        }

        public void OnDrag(PointerEventData e)
        {
            if (e.pointerId != pointerId) return;
            Vector2 d = Local(e) - center;
            // 指が大きく離れたら中心もついてくる（ずっと倒し続けやすい）
            if (d.magnitude > radius * 1.6f) { center += d.normalized * (d.magnitude - radius * 1.6f); baseRt.anchoredPosition = center; d = Local(e) - center; }
            Vector2 c = Vector2.ClampMagnitude(d, radius);
            knob.anchoredPosition = center + c;
            Vector2 v = c / radius;
            float m = v.magnitude;
            const float dead = 0.14f;
            GameInput.stick = m < dead ? Vector2.zero : v.normalized * Mathf.Clamp01((m - dead) / (1f - dead));
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (e.pointerId != pointerId) return;
            ResetStick();
        }

        void OnDisable() { ResetStick(); }

        void ResetStick()
        {
            pointerId = int.MinValue;
            GameInput.stick = Vector2.zero;
            if (baseRt != null) baseRt.anchoredPosition = restPos;
            if (knob != null) knob.anchoredPosition = restPos;
            SetAlpha(0.18f, 0.35f);
        }

        void SetAlpha(float b, float k)
        {
            if (baseImg != null) { var c = baseImg.color; c.a = b; baseImg.color = c; }
            if (knobImg != null) { var c = knobImg.color; c.a = k; knobImg.color = c; }
        }
    }

    /// <summary>iPhone のノッチ・ホームバーを避ける（Screen.safeArea に合わせてアンカーを調整）</summary>
    public class SafeArea : MonoBehaviour
    {
        Rect last;
        Vector2Int lastSize;
        RectTransform rt;
        void Awake() { rt = GetComponent<RectTransform>(); Apply(); }
        void Update()
        {
            if (Screen.safeArea != last || lastSize.x != Screen.width || lastSize.y != Screen.height) Apply();
        }
        void Apply()
        {
            last = Screen.safeArea;
            lastSize = new Vector2Int(Screen.width, Screen.height);
            if (Screen.width <= 0 || Screen.height <= 0) return;
            Vector2 min = last.position, max = last.position + last.size;
            min.x /= Screen.width; min.y /= Screen.height;
            max.x /= Screen.width; max.y /= Screen.height;
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
