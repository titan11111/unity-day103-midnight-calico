using System.Collections;
using UnityEngine;

namespace MidnightCalico
{
    /// <summary>
    /// 主人公「ナツキ」。バイオハザード風のタンク操作（↑前進 ↓後退 ←→旋回）。
    /// </summary>
    public class PlayerController : MonoBehaviour
    {
        public static PlayerController I;

        public int hp = 5;
        public const int MaxHp = 5;
        public bool cameraRelative;   // 「T」でカメラ基準操作に切り替え

        CharacterController cc;
        PlayerModel model;
        Light flash;
        float vy;
        float stepDist;
        bool busy;            // 捕獲・調べる中は動けない
        float hurtT;
        Vector3 heldCamFwd = Vector3.forward;
        bool heldBasis;
        House house;

        public bool IsMoving { get; private set; }
        public bool IsRunning { get; private set; }
        public bool IsSneaking { get; private set; }
        public bool FlashlightOn { get { return flash != null && flash.enabled; } }
        public Vector3 LightOrigin { get { return flash.transform.position; } }
        public Vector3 LightForward { get { return flash.transform.forward; } }
        public Transform Hands { get { return model.carry; } }
        public string Condition { get { return hp >= 4 ? "Fine" : (hp >= 2 ? "Caution" : "Danger"); } }

        public void Init(House h)
        {
            I = this;
            house = h;
            gameObject.layer = 2;
            cc = gameObject.AddComponent<CharacterController>();
            cc.height = 1.62f;
            cc.radius = 0.24f;
            cc.center = new Vector3(0, 0.81f, 0);
            cc.stepOffset = 0.3f;
            cc.slopeLimit = 50f;
            cc.skinWidth = 0.03f;
            model = gameObject.AddComponent<PlayerModel>();
            model.Build();
            Geo.SetLayerRecursive(gameObject, 2);

            // 懐中電灯
            var lg = new GameObject("懐中電灯の光");
            lg.transform.SetParent(transform, false);
            lg.transform.localPosition = new Vector3(0.2f, 1.2f, 0.35f);
            lg.transform.localRotation = Quaternion.Euler(9f, 0f, 0f);
            flash = lg.AddComponent<Light>();
            flash.type = LightType.Spot;
            // 2026-10-29: 懐中電灯の性能アップ（届く距離・明るさ・ホットスポットの広さ）
            flash.range = 17f;
            flash.spotAngle = 60f;
            flash.innerSpotAngle = 24f;
            flash.intensity = 4.5f;
            flash.color = new Color(1f, 0.95f, 0.85f);
            flash.shadows = LightShadows.Soft;
            flash.shadowStrength = 0.9f;
            flash.shadowBias = 0.03f;
            flash.renderMode = LightRenderMode.ForcePixel;
            flash.cookie = Tex.FlashlightCookie();   // 反射板の輪・ホットスポットのある本物っぽい光
            AddDust(lg);

            // 足もとの影（接地感）
            BlobShadow.Attach(transform, 0.75f, 0.55f);

            // 音を聞く耳は主人公の頭
            var ears = new GameObject("耳");
            ears.transform.SetParent(transform, false);
            ears.transform.localPosition = new Vector3(0, 1.55f, 0);
            ears.AddComponent<AudioListener>();
        }

        void Update()
        {
            var gm = GameManager.I;
            if (gm == null) return;
            float dt = Time.deltaTime;
            hurtT = Mathf.Max(0f, hurtT - dt);
            IsMoving = false;
            IsRunning = false;

            if (gm.state == GState.Play && !busy)
            {
                if (GameInput.SchemeToggle)
                {
                    cameraRelative = !cameraRelative;
                    gm.Toast(cameraRelative ? "操作：カメラ基準（スティック方向に進む）" : "操作：タンク式（↑前進 ↓後退 ←→旋回）");
                }
                Move(dt);
                if (Time.frameCount > gm.resumeFrame + 1)
                {
                    if (GameInput.Pressed(GameInput.Action)) DoAction();
                    else if (GameInput.Pressed(GameInput.Item)) UseEquipped();
                    else if (GameInput.Pressed(GameInput.Torch)) UseItem(ItemId.Flashlight);
                }
            }
            // 重力
            if (cc.isGrounded) vy = -1f; else vy -= 9.8f * dt;
            if (gm.state == GState.Play || gm.state == GState.Ending)
                cc.Move(Vector3.up * vy * dt);

            model.moveSpeed = IsMoving ? (IsRunning ? 2f : (backward ? -1f : 1f)) : 0f;
            model.danger = hp <= 1;
        }

        bool backward;

        void Move(float dt)
        {
            Vector2 m = GameInput.Move;
            float mag = Mathf.Clamp01(m.magnitude);
            bool run = GameInput.Held(GameInput.Run);
            float slow = hp <= 1 ? 0.6f : 1f;
            float walk = 1.55f * slow, runSpd = 3.5f * slow, back = 1.0f * slow, sneak = 0.7f * slow;
            backward = false;
            IsSneaking = false;

            if (!cameraRelative)
            {
                float turn = (run && m.y > 0.1f ? 170f : 135f) * m.x;
                transform.Rotate(0f, turn * dt, 0f);
                if (Mathf.Abs(m.y) > 0.1f)
                {
                    float a = Mathf.Abs(m.y);
                    float spd = m.y > 0 ? (run ? runSpd : Mathf.Lerp(sneak, walk, Mathf.InverseLerp(0.35f, 0.8f, a))) : back;
                    backward = m.y < 0;
                    Vector3 v = transform.forward * Mathf.Sign(m.y) * spd;
                    cc.Move(v * dt);
                    IsMoving = true;
                    IsRunning = run && m.y > 0;
                    IsSneaking = !IsRunning && !backward && a < 0.55f;
                    Footsteps(spd * dt, IsRunning);
                }
            }
            else
            {
                // カメラ基準：カメラが切り替わっても、指を離すまでは前の向きを保つ（バイオ系の定番対策）
                if (mag < 0.1f) heldBasis = false;
                else if (!heldBasis)
                {
                    heldBasis = true;
                    // カット直後（0.5秒以内）は、スティックを一瞬離して倒し直しても方向を再取得しない。
                    // カメラカットに驚いて指を離す→倒し直す、という自然な反応で移動方向が反転して見える問題への対策。
                    bool justCut = FixedCameraSystem.I != null && (Time.time - FixedCameraSystem.I.lastCutTime) < 0.5f;
                    if (!justCut)
                    {
                        var cam = FixedCameraSystem.I != null ? FixedCameraSystem.I.cam.transform : null;
                        if (cam != null) { heldCamFwd = cam.forward; heldCamFwd.y = 0; heldCamFwd.Normalize(); }
                    }
                }
                if (mag > 0.12f)
                {
                    Vector3 right = Vector3.Cross(Vector3.up, heldCamFwd);
                    Vector3 dir = (heldCamFwd * m.y + right * m.x).normalized;
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir), 600f * dt);
                    // 少し倒す＝しのび足、大きく倒す＝歩く、走るボタン＝走る
                    float spd = run ? runSpd : Mathf.Lerp(sneak, walk, Mathf.InverseLerp(0.35f, 0.85f, mag));
                    // 向きが大きく違う間は踏み出さない（その場で振り向く）
                    float facing = Vector3.Dot(transform.forward, dir);
                    if (facing > 0.3f)
                    {
                        cc.Move(transform.forward * spd * Mathf.Clamp01(facing) * dt);
                        IsMoving = true;
                        IsRunning = run;
                        IsSneaking = !run && mag < 0.55f;
                        Footsteps(spd * dt, run);
                    }
                }
            }
            model.sneaking = IsSneaking;
        }

        void Footsteps(float dist, bool running)
        {
            stepDist += dist;
            float stride = running ? 0.75f : 0.55f;
            if (stepDist < stride) return;
            stepDist = 0f;
            string room = house.RoomName(transform.position);
            AudioClip c = Sfx.StepWood;
            if (room == "和室" || room == "寝室") c = Sfx.StepSoft;
            else if (room == "浴室" || room == "トイレ" || transform.position.y < -0.05f) c = Sfx.StepTile;
            if (AudioMan.I != null) AudioMan.I.Play2D(c, running ? 0.55f : (IsSneaking ? 0.12f : 0.3f), Random.Range(0.9f, 1.1f));
        }

        // ───────────────────────── 調べる・捕まえる・電気をつける ─────────────────────────
        public enum TargetKind { None, Cat, Search, Switch }

        public TargetKind CurrentTarget(out SearchPoint sp, out LightSwitch sw)
        {
            sp = null;
            sw = null;
            var cat = CatAI.I;
            if (cat != null && cat.state != CatState.Caught)
            {
                if (cat.IsCatchTarget(transform.position, transform.forward, 1.35f)) return TargetKind.Cat;
                if (cat.state == CatState.Hidden && cat.spot != null)
                {
                    Vector3 d = cat.spot.approach - transform.position; d.y = 0;
                    Vector3 d2 = cat.transform.position - transform.position; d2.y = 0;
                    if (d.magnitude < 1.1f && Vector3.Angle(transform.forward, d2) < 85f) return TargetKind.Cat;
                }
            }
            float best = 99f;
            foreach (var s in GameManager.I.house.searches)
            {
                Vector3 d = s.pos - transform.position; d.y = 0;
                float dist = d.magnitude;
                if (dist > s.radius) continue;
                if (dist > 0.3f && Vector3.Angle(transform.forward, d) > 80f) continue;
                if (dist < best) { best = dist; sp = s; }
            }
            if (sp != null) return TargetKind.Search;
            best = 99f;
            foreach (var s in GameManager.I.house.switches)
            {
                Vector3 d = s.pos - transform.position; d.y = 0;
                float dist = d.magnitude;
                if (dist > s.radius) continue;
                if (dist > 0.3f && Vector3.Angle(transform.forward, d) > 80f) continue;
                if (dist < best) { best = dist; sw = s; }
            }
            return sw != null ? TargetKind.Switch : TargetKind.None;
        }

        void DoAction()
        {
            SearchPoint sp; LightSwitch sw;
            var k = CurrentTarget(out sp, out sw);
            if (k == TargetKind.Cat) StartCoroutine(CatchRoutine());
            else if (k == TargetKind.Search) GameManager.I.DoSearch(sp);
            else if (k == TargetKind.Switch) ToggleSwitch(sw);
        }

        void ToggleSwitch(LightSwitch sw)
        {
            sw.on = !sw.on;
            if (sw.light != null) sw.light.intensity = sw.on ? sw.onIntensity : 0f;
            if (AudioMan.I != null) AudioMan.I.Play2D(Sfx.Cursor, 0.5f, sw.on ? 1.1f : 0.8f);
            GameManager.I.Toast(sw.on ? (sw.name + "をつけた。") : (sw.name + "を消した。"));
        }

        IEnumerator CatchRoutine()
        {
            busy = true;
            model.Lunge();
            if (AudioMan.I != null) AudioMan.I.Play2D(Sfx.Thud, 0.5f, 1.3f);
            yield return new WaitForSeconds(0.32f);
            var gm = GameManager.I;
            string msg;
            var r = CatAI.I.TryCatch(gm.inv.Has(ItemId.Net), out msg);
            if (r == CatchResult.Success)
            {
                model.Hold();
                CatAI.I.AttachTo(model.carry);
                gm.OnCatCaught();
                yield break;
            }
            if (!string.IsNullOrEmpty(msg)) gm.Toast(msg);
            if (r == CatchResult.Fail && Random.value < 0.5f) Hurt();
            yield return new WaitForSeconds(0.45f);
            busy = false;
        }

        public void Hurt()
        {
            if (hurtT > 0f) return;
            hurtT = 1.5f;
            hp = Mathf.Max(1, hp - 1);
            GameManager.I.scratches++;
            model.Flinch();
            if (AudioMan.I != null) AudioMan.I.Play2D(Sfx.Scratch, 0.9f);
            Haptics.Strong();
            GameManager.I.Toast(hp <= 1 ? "いたっ！ ひっかかれた……！ 傷が痛む（Danger）" : "いたっ！ ひっかかれた！");
            GameManager.I.FlashRed();
        }

        // ───────────────────────── アイテム ─────────────────────────
        public void UseEquipped()
        {
            var gm = GameManager.I;
            var inv = gm.inv;
            var id = inv.equipped;
            if (id == ItemId.None || !inv.Has(id))
            {
                gm.Toast("使うアイテムが選ばれていない。アイテム画面（TAB）で選ぼう。");
                return;
            }
            UseItem(id);
        }

        public void UseItem(ItemId id)
        {
            var gm = GameManager.I;
            var inv = gm.inv;
            switch (id)
            {
                case ItemId.Flashlight:
                    flash.enabled = !flash.enabled;
                    {
                        var dust = flash.GetComponentInChildren<ParticleSystem>();
                        if (dust != null) { if (flash.enabled) dust.Play(); else dust.Stop(); }
                    }
                    if (AudioMan.I != null) AudioMan.I.Play2D(Sfx.Cursor, 0.6f, 0.6f);
                    gm.Toast(flash.enabled ? "懐中電灯をつけた。" : "懐中電灯を消した。暗闇でもミケの気配はわかる……かもしれない。");
                    break;
                case ItemId.Toy:
                    model.Wave();
                    if (AudioMan.I != null) AudioMan.I.Play2D(Sfx.Shake, 0.25f, 2f);
                    CatAI.I.OnToy();
                    break;
                case ItemId.Food:
                case ItemId.Matatabi:
                    PlaceBowl(id);
                    inv.Remove(id);
                    break;
                case ItemId.Paste:
                    if (CatAI.I.OnPaste()) inv.Remove(id);
                    break;
                case ItemId.Bandage:
                    if (hp >= MaxHp) { gm.Toast("けがはしていない。今は使わなくていい。"); break; }
                    hp = Mathf.Min(MaxHp, hp + 2);
                    inv.Remove(id);
                    if (AudioMan.I != null) AudioMan.I.Play2D(Sfx.Pickup, 0.5f, 0.8f);
                    gm.Toast("絆創膏を貼った。少し楽になった。");
                    break;
                case ItemId.Net:
                    gm.Toast("洗濯ネットは、捕まえるときに自動で使う。持っているだけで成功しやすくなる。");
                    break;
            }
        }

        void PlaceBowl(ItemId kind)
        {
            Vector3 p = transform.position + transform.forward * 0.6f;
            p.y = 0f;
            RaycastHit hit;
            if (Physics.Raycast(p + Vector3.up * 1.0f, Vector3.down, out hit, 2f, ~(1 << 2), QueryTriggerInteraction.Ignore))
                p.y = hit.point.y;
            var g = new GameObject(kind == ItemId.Food ? "カリカリの皿" : "またたび");
            g.transform.position = p;
            if (kind == ItemId.Food)
            {
                Geo.Cyl(g.transform, "皿", new Vector3(0, 0.02f, 0), 0.08f, 0.04f, Mat.Make("bowlBlue", new Color(0.3f, 0.45f, 0.75f), null, 0.8f), false);
                var kib = Mat.Make("kibble", new Color(0.55f, 0.32f, 0.16f), null, 0.3f);
                for (int i = 0; i < 9; i++)
                    Geo.Sph(g.transform, "粒", new Vector3(Random.Range(-0.04f, 0.04f), 0.045f, Random.Range(-0.04f, 0.04f)), Vector3.one * 0.022f, kib);
                if (AudioMan.I != null) AudioMan.I.Play2D(Sfx.Shake, 0.9f);
                GameManager.I.Toast("カリカリを皿に出して、袋をシャカシャカ振った。……離れて様子を見よう。");
            }
            else
            {
                var pw = Mat.Make("matatabiPowder", new Color(0.6f, 0.55f, 0.35f), null, 0.1f);
                Geo.Cyl(g.transform, "粉", new Vector3(0, 0.003f, 0), 0.12f, 0.006f, pw, false);
                GameManager.I.Toast("またたびの粉をまいた。……離れて様子を見よう。");
            }
            Geo.SetLayerRecursive(g, 2);
            var b = g.AddComponent<LureBowl>();
            b.kind = kind;
            model.Crouch();
        }

        public void SetBusy(bool b) { busy = b; }

        /// <summary>懐中電灯の光の中に舞うほこり</summary>
        static void AddDust(GameObject host)
        {
            var g = new GameObject("ほこり");
            g.transform.SetParent(host.transform, false);
            g.transform.localPosition = new Vector3(0, 0, 0.3f);
            var ps = g.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 7f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.0f, 0.03f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.006f, 0.018f);
            main.startColor = new Color(1f, 0.96f, 0.86f, 0.55f);
            main.maxParticles = 140;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0.002f;
            var em = ps.emission;
            em.rateOverTime = 22f;
            var sh = ps.shape;
            sh.enabled = true;
            sh.shapeType = ParticleSystemShapeType.ConeVolume;
            sh.angle = 22f;
            sh.radius = 0.05f;
            sh.length = 4.5f;
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.04f;
            noise.frequency = 0.25f;
            noise.scrollSpeed = 0.1f;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(grad);
            var r = g.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.sharedMaterial = Mat.Unlit("dust", Tex.SoftDot(Color.white), Color.white);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            ps.Play();
        }
    }

    /// <summary>
    /// 主人公の体（パジャマにカーディガン、スリッパ、懐中電灯）。
    /// </summary>
    public class PlayerModel : MonoBehaviour
    {
        Transform hips, spine, neck, armL, armR, foreL, foreR, legL, legR, shinL, shinR;
        public Transform carry;
        public float moveSpeed;   // 2=走る 1=歩く -1=後退
        public bool danger;
        public bool sneaking;
        float phase, actionT;
        enum Act { None, Lunge, Wave, Flinch, Crouch, Hold }
        Act act;

        public void Build()
        {
            var skin = Mat.Make("skin", new Color(0.93f, 0.78f, 0.66f), null, 0.35f);
            var hair = Mat.Make("hair", new Color(0.07f, 0.06f, 0.06f), null, 0.55f);
            var pajama = Mat.Make("pajama", Color.white, Tex.Fabric(new Color(0.20f, 0.25f, 0.42f), 0.25f), 0.1f);
            var cardigan = Mat.Make("cardigan", Color.white, Tex.Fabric(new Color(0.80f, 0.74f, 0.62f), 0.45f), 0.05f);
            var slipper = Mat.Make("slipper", new Color(0.55f, 0.35f, 0.25f), null, 0.2f);
            var dark = Mat.Make("eyeDark", new Color(0.05f, 0.04f, 0.04f), null, 0.8f);
            var torchMat = Mat.Make("torch", new Color(0.12f, 0.12f, 0.13f), null, 0.7f, 0.6f);
            var lens = Mat.Emissive("torchLens", Color.white, new Color(1.5f, 1.45f, 1.3f));

            hips = Geo.Pivot(transform, "hips", new Vector3(0, 0.92f, 0));
            Geo.Sph(hips, "pelvis", new Vector3(0, 0, 0), new Vector3(0.32f, 0.2f, 0.21f), pajama);
            spine = Geo.Pivot(hips, "spine", new Vector3(0, 0.05f, 0));
            Geo.Sph(spine, "torso", new Vector3(0, 0.27f, 0), new Vector3(0.36f, 0.52f, 0.23f), cardigan);
            Geo.Box(spine, "pajamaFront", new Vector3(0, 0.27f, 0.105f), new Vector3(0.1f, 0.42f, 0.02f), pajama, false);
            Geo.Box(spine, "cardiganHem", new Vector3(0, 0.05f, 0), new Vector3(0.36f, 0.08f, 0.24f), cardigan, false);
            neck = Geo.Pivot(spine, "neck", new Vector3(0, 0.55f, 0));
            Geo.Cyl(neck, "neckMesh", new Vector3(0, 0.02f, 0), 0.05f, 0.08f, skin, false);
            Geo.Sph(neck, "head", new Vector3(0, 0.15f, 0.01f), new Vector3(0.19f, 0.23f, 0.21f), skin);
            Geo.Sph(neck, "hair", new Vector3(0, 0.19f, -0.015f), new Vector3(0.215f, 0.21f, 0.225f), hair);
            Geo.Sph(neck, "hairBack", new Vector3(0, 0.1f, -0.05f), new Vector3(0.22f, 0.2f, 0.16f), hair);
            Geo.Box(neck, "bangs", new Vector3(0, 0.235f, 0.085f), new Vector3(0.17f, 0.06f, 0.05f), hair, false);
            Geo.Sph(neck, "ahoge", new Vector3(0.03f, 0.3f, 0.0f), new Vector3(0.04f, 0.06f, 0.02f), hair); // 寝ぐせ
            Geo.Sph(neck, "eyeL", new Vector3(-0.045f, 0.16f, 0.1f), new Vector3(0.025f, 0.018f, 0.01f), dark);
            Geo.Sph(neck, "eyeR", new Vector3(0.045f, 0.16f, 0.1f), new Vector3(0.025f, 0.018f, 0.01f), dark);
            Geo.Sph(neck, "nose", new Vector3(0, 0.125f, 0.112f), new Vector3(0.02f, 0.025f, 0.02f), skin);

            armL = Geo.Pivot(spine, "armL", new Vector3(-0.2f, 0.47f, 0));
            armR = Geo.Pivot(spine, "armR", new Vector3(0.2f, 0.47f, 0));
            foreL = Limb(armL, cardigan, skin, out Transform handL, false);
            foreR = Limb(armR, cardigan, skin, out Transform handR, false);
            // 懐中電灯
            var torch = Geo.Cyl(handR, "torch", new Vector3(0, -0.02f, 0.08f), 0.022f, 0.2f, torchMat, false);
            torch.transform.localRotation = Quaternion.Euler(90, 0, 0);
            var lensG = Geo.Cyl(handR, "lens", new Vector3(0, -0.02f, 0.18f), 0.026f, 0.01f, lens, false);
            lensG.transform.localRotation = Quaternion.Euler(90, 0, 0);
            carry = Geo.Pivot(spine, "carry", new Vector3(0, 0.25f, 0.3f));

            legL = Geo.Pivot(hips, "legL", new Vector3(-0.09f, -0.03f, 0));
            legR = Geo.Pivot(hips, "legR", new Vector3(0.09f, -0.03f, 0));
            shinL = Leg(legL, pajama, skin, slipper);
            shinR = Leg(legR, pajama, skin, slipper);

            foreach (var r in GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        Transform Limb(Transform shoulder, Material sleeve, Material skin, out Transform hand, bool unused)
        {
            Geo.Cyl(shoulder, "upper", new Vector3(0, -0.14f, 0), 0.055f, 0.3f, sleeve, false);
            var elbow = Geo.Pivot(shoulder, "elbow", new Vector3(0, -0.29f, 0));
            Geo.Cyl(elbow, "fore", new Vector3(0, -0.12f, 0), 0.045f, 0.26f, sleeve, false);
            hand = Geo.Pivot(elbow, "hand", new Vector3(0, -0.27f, 0));
            Geo.Sph(hand, "palm", Vector3.zero, new Vector3(0.07f, 0.09f, 0.05f), skin);
            return elbow;
        }

        Transform Leg(Transform hip, Material pants, Material skin, Material slipper)
        {
            Geo.Cyl(hip, "thigh", new Vector3(0, -0.21f, 0), 0.075f, 0.44f, pants, false);
            var knee = Geo.Pivot(hip, "knee", new Vector3(0, -0.43f, 0));
            Geo.Cyl(knee, "shin", new Vector3(0, -0.2f, 0), 0.06f, 0.42f, pants, false);
            Geo.Sph(knee, "ankle", new Vector3(0, -0.43f, 0), new Vector3(0.07f, 0.06f, 0.07f), skin);
            Geo.Box(knee, "slipper", new Vector3(0, -0.46f, 0.05f), new Vector3(0.1f, 0.05f, 0.25f), slipper, false);
            return knee;
        }

        public void Lunge() { act = Act.Lunge; actionT = 0f; }
        public void Wave() { act = Act.Wave; actionT = 0f; }
        public void Flinch() { act = Act.Flinch; actionT = 0f; }
        public void Crouch() { act = Act.Crouch; actionT = 0f; }
        public void Hold() { act = Act.Hold; actionT = 0f; }

        void LateUpdate()
        {
            if (hips == null) return;
            float dt = Time.deltaTime;
            actionT += dt;
            float spd = Mathf.Abs(moveSpeed);
            phase += dt * (spd > 1.5f ? 11f : (spd > 0 ? 6.5f : 0f)) * (moveSpeed < 0 ? -1f : 1f);

            float swing = spd > 1.5f ? 45f : (spd > 0 ? 25f : 0f);
            float s = Mathf.Sin(phase);
            float legLA = s * swing, legRA = -s * swing;
            float kneeL = Mathf.Max(0f, Mathf.Sin(phase + 1.2f)) * (swing * 1.4f);
            float kneeR = Mathf.Max(0f, Mathf.Sin(phase + 1.2f + Mathf.PI)) * (swing * 1.4f);
            float armLA = -s * swing * 0.8f;
            float spinePitch = spd > 1.5f ? 14f : (sneaking ? 22f : 3f);
            float hipY = 0.92f - Mathf.Abs(Mathf.Cos(phase)) * (spd > 1.5f ? 0.04f : 0.015f) * (spd > 0 ? 1f : 0f);
            if (sneaking) { hipY -= 0.12f; kneeL += 25f; kneeR += 25f; legLA += 12f; legRA += 12f; }
            float limp = danger ? 1f : 0f;
            spinePitch += limp * 10f;
            float breathe = Mathf.Sin(Time.time * 1.6f) * 1.5f;

            // 右手は懐中電灯を前に構える
            Quaternion armRRot = Quaternion.Euler(-75f + s * 3f, 0f, -6f);
            Quaternion foreRRot = Quaternion.Euler(-10f, 0, 0);
            Quaternion armLRot = Quaternion.Euler(armLA, 0, 6f);
            Quaternion foreLRot = Quaternion.Euler(-20f, 0, 0);
            Quaternion spineRot = Quaternion.Euler(spinePitch + breathe, 0, 0);
            Quaternion neckRot = Quaternion.Euler(-spinePitch * 0.6f, 0, 0);

            switch (act)
            {
                case Act.Lunge:
                    {
                        float k = Mathf.Sin(Mathf.Clamp01(actionT / 0.6f) * Mathf.PI);
                        spineRot = Quaternion.Euler(10f + 45f * k, 0, 0);
                        armLRot = Quaternion.Euler(-80f * k - 10f, 0, 10f);
                        armRRot = Quaternion.Euler(-80f * k - 10f, 0, -10f);
                        foreLRot = foreRRot = Quaternion.Euler(-10f, 0, 0);
                        hipY -= 0.18f * k;
                        if (actionT > 0.6f) act = Act.None;
                    }
                    break;
                case Act.Wave:
                    {
                        float k = Mathf.Sin(actionT * 18f);
                        armLRot = Quaternion.Euler(-60f, 0, 20f + k * 35f);
                        foreLRot = Quaternion.Euler(-30f + k * 20f, 0, 0);
                        spineRot = Quaternion.Euler(20f, 0, 0);
                        hipY -= 0.15f;
                        if (actionT > 1.2f) act = Act.None;
                    }
                    break;
                case Act.Flinch:
                    {
                        float k = Mathf.Sin(Mathf.Clamp01(actionT / 0.5f) * Mathf.PI);
                        spineRot = Quaternion.Euler(-20f * k, 15f * k, 0);
                        armLRot = Quaternion.Euler(-100f * k, 0, 30f * k);
                        if (actionT > 0.5f) act = Act.None;
                    }
                    break;
                case Act.Crouch:
                    {
                        float k = Mathf.Sin(Mathf.Clamp01(actionT / 0.9f) * Mathf.PI);
                        spineRot = Quaternion.Euler(50f * k, 0, 0);
                        armLRot = Quaternion.Euler(-60f * k, 0, 0);
                        hipY -= 0.35f * k;
                        legLA = legRA = -60f * k; kneeL = kneeR = 110f * k;
                        if (actionT > 0.9f) act = Act.None;
                    }
                    break;
                case Act.Hold:
                    armLRot = Quaternion.Euler(-55f, 25f, 10f);
                    armRRot = Quaternion.Euler(-55f, -25f, -10f);
                    foreLRot = Quaternion.Euler(-60f, 0, 0);
                    foreRRot = Quaternion.Euler(-60f, 0, 0);
                    spineRot = Quaternion.Euler(-4f + breathe, 0, 0);
                    break;
            }

            float l = 1f - Mathf.Exp(-dt * 14f);
            hips.localPosition = Vector3.Lerp(hips.localPosition, new Vector3(0, hipY, 0), l);
            spine.localRotation = Quaternion.Slerp(spine.localRotation, spineRot, l);
            neck.localRotation = Quaternion.Slerp(neck.localRotation, neckRot, l);
            armL.localRotation = Quaternion.Slerp(armL.localRotation, armLRot, l);
            armR.localRotation = Quaternion.Slerp(armR.localRotation, armRRot, l);
            foreL.localRotation = Quaternion.Slerp(foreL.localRotation, foreLRot, l);
            foreR.localRotation = Quaternion.Slerp(foreR.localRotation, foreRRot, l);
            legL.localRotation = Quaternion.Slerp(legL.localRotation, Quaternion.Euler(-legLA, 0, 0), l);
            legR.localRotation = Quaternion.Slerp(legR.localRotation, Quaternion.Euler(-legRA - limp * 5f, 0, 0), l);
            shinL.localRotation = Quaternion.Slerp(shinL.localRotation, Quaternion.Euler(kneeL, 0, 0), l);
            shinR.localRotation = Quaternion.Slerp(shinR.localRotation, Quaternion.Euler(kneeR + limp * 15f, 0, 0), l);
        }
    }
}
