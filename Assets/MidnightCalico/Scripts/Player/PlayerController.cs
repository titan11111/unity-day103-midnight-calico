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
        TableShelter shelter;
        bool shelterTransition;
        public bool IsSheltered { get { return shelter != null; } }
        public bool ProtectedFromDebris { get { return IsSheltered && transform.position.x > 10.45f && transform.position.x < 12.55f && transform.position.z > 2.65f && transform.position.z < 3.75f; } }
        bool busy;            // 捕獲・調べる中は動けない
        float hurtT;
        Vector3 heldCamFwd = Vector3.forward;
        bool heldBasis;
        bool cutBasis;
        Vector2 cutStick;
        float observedCut = -99f;
        int inputVersion = -1;
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
            lg.transform.SetParent(model.torchAim, false);
            lg.transform.localPosition = Vector3.zero;
            lg.transform.localRotation = Quaternion.identity;
            flash = lg.AddComponent<Light>();
            flash.type = LightType.Spot;
            // 手元のレンズから前方へ、足元と周辺も見渡せる広さで照らす。
            flash.range = 21f;
            flash.spotAngle = 80f;
            flash.innerSpotAngle = 36f;
            flash.intensity = 4.5f;
            flash.color = new Color(1f, 0.95f, 0.85f);
            flash.shadows = LightShadows.Soft;
            flash.shadowStrength = 0.9f;
            flash.shadowBias = 0.03f;
            flash.renderMode = LightRenderMode.ForcePixel;
            flash.cookie = Tex.FlashlightCookie();   // 反射板の輪・ホットスポットのある本物っぽい光

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
            if (inputVersion != GameInput.clearVersion)
            {
                inputVersion = GameInput.clearVersion;
                heldBasis = cutBasis = false;
                touchTarget = default(TargetSelection);
            }
            float dt = Time.deltaTime;
            hurtT = Mathf.Max(0f, hurtT - dt);
            IsMoving = false;
            IsRunning = false;
            IsSneaking = false;

            if (gm.state == GState.Play && IsSheltered)
            {
                if (!shelterTransition && Time.frameCount > gm.resumeFrame + 1)
                {
                    if (GameInput.Pressed(GameInput.Action)) StartCoroutine(LeaveShelter());
                    else if (GameInput.Pressed(GameInput.Torch)) UseItem(ItemId.Flashlight);
                }
            }
            else if (gm.state == GState.Play && !busy)
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
            model.toyEquipped = gm.inv.equipped == ItemId.Toy;
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
                var cs = FixedCameraSystem.I;
                if (cs != null && cs.lastCutTime != observedCut)
                {
                    observedCut = cs.lastCutTime;
                    cutBasis = heldBasis && mag >= 0.1f;
                    cutStick = m.normalized;
                }
                if (mag < 0.1f) heldBasis = cutBasis = false;
                else
                {
                    // カット中の継続入力は保ち、30度以上の意図的な倒し直しで新画面へ戻す。
                    bool redirect = cutBasis && Vector2.Angle(cutStick, m) > 30f;
                    if (!heldBasis || redirect)
                    {
                        var cam = cs != null ? cs.cam.transform : null;
                        if (cam != null)
                        {
                            heldCamFwd = Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized;
                            if (heldCamFwd.sqrMagnitude < 0.01f) heldCamFwd = transform.forward;
                        }
                        heldBasis = true;
                        cutBasis = false;
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
        public enum TargetKind { None, Cat, Search, Switch, Shelter, NewCat }

        public struct TargetSelection
        {
            public TargetKind kind;
            public SearchPoint search;
            public LightSwitch lightSwitch;
            public TableShelter shelter;
            public Vector3 position;
            public string name;
        }
        TargetSelection selected, touchTarget;
        float touchTargetAt = -99f;
        int selectionFrame = -1;

        public TargetSelection Selection
        {
            get { RefreshSelection(); return selected; }
        }

        bool InReach(Vector3 pos, float radius)
        {
            Vector3 d = pos - transform.position; d.y = 0;
            return d.magnitude <= radius && (d.magnitude <= 0.3f || Vector3.Angle(transform.forward, d) <= 80f);
        }
        bool CatInReach()
        {
            var cat = CatAI.I;
            if (cat == null || cat.state == CatState.Caught || Mathf.Abs(cat.transform.position.y-transform.position.y)>1.5f) return false;
            if (cat.IsCatchTarget(transform.position, transform.forward, 1.35f)) return true;
            return cat.state == CatState.Hidden && cat.spot != null &&
                InReach(cat.spot.approach, 1.1f) && InReach(cat.transform.position, 1.8f);
        }
        void RefreshSelection()
        {
            if (selectionFrame == Time.frameCount) return;
            selectionFrame = Time.frameCount;
            var previous = selected;
            selected = default(TargetSelection);
            if (IsSheltered)
            {
                selected = new TargetSelection { kind = TargetKind.Shelter, shelter = shelter, name = "テーブルの下", position = transform.position + Vector3.up * 0.4f };
                return;
            }
            var events = GameManager.I != null ? GameManager.I.nightEvents : null;
            if (events != null && events.Quaking)
            {
                TableShelter nearest = null; float distance = 0.9f;
                foreach (var cover in house.shelters)
                {
                    float d = Vector3.Distance(new Vector3(transform.position.x,0,transform.position.z), cover.entrance);
                    if (d < distance && InReach(cover.entrance,0.9f)) { distance = d; nearest = cover; }
                }
                if (nearest != null)
                {
                    selected = new TargetSelection { kind = TargetKind.Shelter, shelter = nearest, name = "ダイニングテーブル", position = nearest.entrance + Vector3.up * 0.5f };
                    return;
                }
            }
            if (CatInReach())
            {
                selected = new TargetSelection { kind = TargetKind.Cat, name = "ミケ", position = CatAI.I.transform.position + Vector3.up * 0.35f };
                return;
            }
            var newcomer = GameManager.I != null ? GameManager.I.newcomer : null;
            if (newcomer != null && newcomer.InReach(this) && InReach(newcomer.transform.position,1.5f))
            {
                selected = new TargetSelection { kind=TargetKind.NewCat, name="新入り猫",position=newcomer.transform.position+Vector3.up*0.25f };
                return;
            }
            float best = float.MaxValue;
            string room = house.RoomName(transform.position);
            foreach (var search in house.searches)
            {
                if (!InReach(search.pos, search.radius) || house.RoomName(search.pos) != room) continue;
                float score = TargetScore(search.pos) - (previous.search == search ? 0.22f : 0f);
                if (score >= best) continue;
                best = score;
                selected = new TargetSelection { kind = TargetKind.Search, search = search, position = search.pos, name = search.name };
            }
            foreach (var sw in house.switches)
            {
                if (!InReach(sw.pos, sw.radius) || house.RoomName(sw.pos) != room) continue;
                float score = TargetScore(sw.pos) - (previous.lightSwitch == sw ? 0.22f : 0f);
                if (score >= best) continue;
                best = score;
                selected = new TargetSelection { kind = TargetKind.Switch, lightSwitch = sw, position = sw.pos, name = sw.name };
            }
        }
        float TargetScore(Vector3 pos)
        {
            Vector3 d = pos - transform.position; d.y = 0;
            return d.magnitude + Vector3.Angle(transform.forward, d) / 180f;
        }
        public TargetKind CurrentTarget(out SearchPoint sp, out LightSwitch sw)
        {
            var target = Selection;
            sp = target.search; sw = target.lightSwitch;
            return target.kind;
        }
        public void CaptureTouchTarget()
        {
            if (GameManager.I == null || GameManager.I.state != GState.Play) return;
            // 画面に示していた対象を押下開始時に固定する。
            touchTarget = Selection;
            touchTargetAt = Time.unscaledTime;
        }
        void DoAction()
        {
            var target = GameInput.lastActionWasTouch && Time.unscaledTime - touchTargetAt < 0.16f ? touchTarget : Selection;
            touchTargetAt = -99f;
            if (target.kind == TargetKind.NewCat && GameManager.I.newcomer != null)
                GameManager.I.newcomer.Soothe();
            else if (target.kind == TargetKind.Shelter && target.shelter != null)
                TryEnterShelter(target.shelter);
            else if (target.kind == TargetKind.Cat && CatInReach())
            {
                GameInput.StopRunning();
                Vector3 d = CatAI.I.transform.position - transform.position; d.y = 0;
                if (d.sqrMagnitude > 0.01f)
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(d), 18f);
                StartCoroutine(CatchRoutine());
            }
            else if (target.kind == TargetKind.Search && target.search != null && InReach(target.search.pos, target.search.radius))
                GameManager.I.DoSearch(target.search);
            else if (target.kind == TargetKind.Switch && target.lightSwitch != null && InReach(target.lightSwitch.pos, target.lightSwitch.radius))
                ToggleSwitch(target.lightSwitch);
        }

        public bool TryEnterShelter(TableShelter cover)
        {
            var gm = GameManager.I;
            if (cover == null || gm == null || gm.state != GState.Play || gm.caught || busy || IsSheltered ||
                gm.nightEvents == null || !gm.nightEvents.Quaking || !house.shelters.Contains(cover)) return false;
            Vector3 delta = cover.entrance - transform.position; delta.y = 0f;
            if (delta.magnitude > 0.95f) return false;
            shelter = cover;
            busy = shelterTransition = true;
            cc.height = 0.5f; cc.center = new Vector3(0,0.25f,0); cc.stepOffset = 0.1f;
            model.sheltering = true;
            GameInput.ClearTouches();
            StartCoroutine(EnterShelter());
            return true;
        }
        IEnumerator EnterShelter()
        {
            // 先に頭を下げてから、短辺の入口を経由して天板の下へ入る。
            float crouch = 0f;
            while (crouch < 0.3f) { if (GameManager.I.state == GState.Play) crouch += Time.deltaTime; yield return null; }
            yield return CrawlTo(shelter.entrance);
            yield return CrawlTo(shelter.inside);
            shelterTransition = false;
            GameManager.I.Toast("テーブルの下で頭を守っている。A／調べるで外へ出る。");
        }
        IEnumerator CrawlTo(Vector3 target)
        {
            float time = 0f;
            while (Vector2.Distance(new Vector2(transform.position.x,transform.position.z),new Vector2(target.x,target.z)) > 0.04f && time < 3f)
            {
                if (GameManager.I.state == GState.Play)
                {
                    float dt = Time.deltaTime; time += dt;
                    Vector3 to = target - transform.position; to.y = 0f;
                    if(to.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(to);
                    cc.Move(Vector3.ClampMagnitude(to,1.6f * dt));
                }
                yield return null;
            }
        }
        public IEnumerator LeaveShelter()
        {
            if (!IsSheltered || shelterTransition) yield break;
            shelterTransition = true;
            yield return CrawlTo(shelter.entrance);
            Vector3 p = transform.position;
            bool clear = !Physics.CheckCapsule(p + Vector3.up * 0.27f,p + Vector3.up * 1.35f,0.24f,~(1<<2),QueryTriggerInteraction.Ignore);
            if (!clear || Vector3.Distance(new Vector3(p.x,0,p.z),shelter.entrance) > 0.15f)
            {
                shelterTransition = false;
                GameManager.I.Toast("出口がふさがっている。もう一度A／調べるで出よう。");
                yield break;
            }
            shelter = null; shelterTransition = busy = false; model.sheltering = false;
            cc.height = 1.62f; cc.center = new Vector3(0,0.81f,0); cc.stepOffset = 0.3f;
            GameInput.ClearTouches();
            GameManager.I.resumeFrame = Time.frameCount;
            GameManager.I.Toast("テーブルの外へ出た。");
        }
        public void HitByFallingProp()
        {
            var gm = GameManager.I;
            if (gm == null || gm.state != GState.Play || ProtectedFromDebris || hurtT > 0f) return;
            hurtT = 2f; hp = Mathf.Max(1,hp-1);
            model.Flinch(); gm.FlashRed();
            if (AudioMan.I != null) AudioMan.I.Play2D(Sfx.Thud,0.7f);
            gm.Toast("いたっ、落下物が当たった！ テーブルの下で身を守ろう。");
        }
        void OnControllerColliderHit(ControllerColliderHit hit)
        {
            var prop = hit.collider.GetComponent<Prop>();
            var rb = hit.collider.attachedRigidbody;
            if (prop != null && prop.IsEarthquakeFall && rb != null && rb.linearVelocity.magnitude > 1.2f) HitByFallingProp();
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
                model.PickUp();
                CatAI.I.AttachTo(model.carryRear, model.carryFront);
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


    }

    /// <summary>
    /// 主人公の体（パジャマにカーディガン、スリッパ、懐中電灯）。
    /// </summary>
    public class PlayerModel : MonoBehaviour
    {
        Transform hips, spine, neck, armL, armR, foreL, foreR, legL, legR, shinL, shinR;
        public enum FaceExpression { Neutral, Scared, Hurt, Relieved }
        public FaceExpression CurrentExpression { get; private set; }
        public Material FaceMaterial { get { return expressionMaterial; } }
        Material expressionMaterial;
        float faceFear, faceSmile, facePain, painUntil, blinkTimer=3f;
        public Transform carry;
        public Transform carryRear;   // 右手＝お尻を下から支える
        public Transform carryFront;  // 左手＝脇（胸側）を支える
        public Transform torchAim { get; private set; }
        GameObject toyObj;
        Transform toyTuft;
        float toySwayT;
        public float moveSpeed;   // 2=走る 1=歩く -1=後退
        public bool danger;
        public bool sneaking;
        public bool sheltering;
        public bool toyEquipped;  // 猫じゃらしを装備中か
        float phase, actionT;
        enum Act { None, Lunge, Wave, Flinch, Crouch, Hold, PickUp }
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
            Geo.Sph(hips, "pelvis", Vector3.zero, new Vector3(0.31f, 0.22f, 0.22f), pajama);
            spine = Geo.Pivot(hips, "spine", new Vector3(0, 0.05f, 0));
            Profile(spine, "knittedCardigan", Vector3.zero, cardigan,
                new Vector4(-0.035f,0.18f,0.125f,0), new Vector4(0.05f,0.175f,0.12f,0),
                new Vector4(0.19f,0.155f,0.105f,0), new Vector4(0.34f,0.17f,0.11f,0),
                new Vector4(0.44f,0.163f,0.085f,0), new Vector4(0.49f,0.095f,0.065f,0), new Vector4(0.51f,0.065f,0.055f,0));
            Profile(spine,"pajamaShirt",Vector3.zero,pajama,
                new Vector4(-0.03f,0.157f,0.110f,0),new Vector4(0.18f,0.14f,0.095f,0),
                new Vector4(0.34f,0.16f,0.10f,0),new Vector4(0.44f,0.16f,0.075f,0),new Vector4(0.50f,0.060f,0.048f,0));
            var trim = Mat.Make("cardiganRib",new Color(0.57f,0.51f,0.42f),null,0.08f);
            for(int side=-1;side<=1;side+=2) {
                var edge=Geo.Sph(spine,"cardiganPlacket",new Vector3(side*0.069f,0.24f,0.107f),new Vector3(0.025f,0.46f,0.018f),trim);
                edge.transform.localRotation=Quaternion.Euler(0,0,side*2f);
                Geo.Sph(spine,"knittedPocket",new Vector3(side*0.115f,0.09f,0.105f),new Vector3(0.085f,0.105f,0.024f),cardigan);
            }
            for(int i=0;i<4;i++) Geo.Sph(spine,"cardiganButton",new Vector3(0.058f,0.08f+i*0.09f,0.139f),new Vector3(0.012f,0.012f,0.005f),dark);
            neck=Geo.Pivot(spine,"neck",new Vector3(0,0.51f,0));
            Geo.Cyl(neck,"neckMesh",new Vector3(0,0.025f,0),0.042f,0.105f,skin,false);
            var faceShader=Resources.Load<Shader>("NatsukiExpression");
            var faceMat=faceShader != null ? new Material(faceShader) : new Material(Shader.Find("Standard"));
            faceMat.name="NatsukiFaceExpression";
            faceMat.mainTexture=Resources.Load<Texture2D>("NatsukiFace");
            expressionMaterial=faceMat;
            Profile(neck,"face",new Vector3(0,0.035f,0.008f),faceMat,
                new Vector4(0,0.036f,0.049f,0.016f),new Vector4(0.032f,0.060f,0.070f,0.012f),
                new Vector4(0.075f,0.080f,0.083f,0.002f),new Vector4(0.13f,0.088f,0.090f,0),
                new Vector4(0.19f,0.084f,0.086f,-0.004f),new Vector4(0.235f,0.065f,0.068f,-0.012f),new Vector4(0.255f,0.012f,0.016f,-0.012f));
            for(int side=-1;side<=1;side+=2)
                Geo.Sph(neck,"ear",new Vector3(side*0.088f,0.162f,-0.003f),new Vector3(0.027f,0.054f,0.030f),skin);
            HairCap(neck,hair);


            armL = Geo.Pivot(spine, "armL", new Vector3(-0.172f, 0.425f, 0));
            armR = Geo.Pivot(spine, "armR", new Vector3(0.172f, 0.435f, 0));
            foreL = Limb(armL, cardigan, skin, out Transform handL, false);
            foreR = Limb(armR, cardigan, skin, out Transform handR, true);
            // 懐中電灯
            torchAim = Geo.Pivot(handR, "torchAim", new Vector3(0, -0.155f, 0.025f));
            torchAim.localRotation = Quaternion.Euler(90f, 0, 0);
            var torch = Geo.Cyl(torchAim, "torch", new Vector3(0, 0, -0.1f), 0.022f, 0.2f, torchMat, false);
            torch.transform.localRotation = Quaternion.Euler(90, 0, 0);
            var lensG = Geo.Cyl(torchAim, "lens", Vector3.zero, 0.026f, 0.01f, lens, false);
            // 猫じゃらし（左手、装備中のみ表示）
            var toyStick = Mat.Make("toyStick", new Color(0.62f, 0.42f, 0.22f), null, 0.3f);
            var toyTuftMat = Mat.Make("toyTuft", new Color(0.92f, 0.65f, 0.18f), null, 0.15f);
            var toyAim = Geo.Pivot(handL, "toyAim", new Vector3(0.01f, -0.03f, 0.03f));
            toyAim.localRotation = Quaternion.Euler(70f, 0f, 20f);
            Geo.Cyl(toyAim, "toyStick", new Vector3(0, 0.23f, 0), 0.009f, 0.46f, toyStick, false);
            toyTuft = Geo.Pivot(toyAim, "toyTuftRoot", new Vector3(0, 0.46f, 0));
            Geo.Sph(toyTuft, "toyTuftCore", Vector3.zero, new Vector3(0.028f, 0.028f, 0.028f), toyTuftMat);
            for (int f = 0; f < 5; f++)
            {
                float ang = f / 5f * 360f;
                var feather = Geo.Sph(toyTuft, "toyFeather",
                    new Vector3(Mathf.Sin(ang * Mathf.Deg2Rad) * 0.018f, 0.02f, Mathf.Cos(ang * Mathf.Deg2Rad) * 0.018f),
                    new Vector3(0.012f, 0.05f, 0.012f), toyTuftMat);
                feather.transform.localRotation = Quaternion.Euler(Random.Range(-15f, 15f), ang, Random.Range(10f, 25f));
            }
            toyObj = toyAim.gameObject;
            toyObj.SetActive(false);
            lensG.transform.localRotation = Quaternion.Euler(90, 0, 0);
            carry = Geo.Pivot(spine, "carry", new Vector3(0, 0.25f, 0.3f));
            // 猫の抱え方：利き手（右）でお尻を下から支え、左手で脇（胸側）を支える。
            // この2点の中間に猫の胴の中心を置き、haunch(-Z側)がcarryRear寄り・chest(+Z側)がcarryFront寄りになるようにする。
            carryRear = Geo.Pivot(spine, "carryRear", new Vector3(0.09f, 0.16f, 0.32f));
            carryFront = Geo.Pivot(spine, "carryFront", new Vector3(-0.07f, 0.30f, 0.29f));

            legL = Geo.Pivot(hips, "legL", new Vector3(-0.09f, -0.03f, 0));
            legR = Geo.Pivot(hips, "legR", new Vector3(0.09f, -0.03f, 0));
            shinL = Leg(legL, pajama, skin, slipper);
            shinR = Leg(legR, pajama, skin, slipper);

            foreach (var r in GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        Transform Limb(Transform shoulder, Material sleeve, Material skin, out Transform hand, bool gripping)
        {
            Profile(shoulder,"upperSleeve",Vector3.zero,sleeve,new Vector4(-0.30f,0.055f,0.055f,0),new Vector4(-0.19f,0.052f,0.055f,0),new Vector4(-0.05f,0.058f,0.055f,0),new Vector4(0.025f,0.050f,0.048f,0),new Vector4(0.045f,0.036f,0.034f,0),new Vector4(0.060f,0.001f,0.001f,0));
            var elbow = Geo.Pivot(shoulder, "elbow", new Vector3(0, -0.29f, 0));
            Profile(elbow,"foreSleeve",Vector3.zero,sleeve,new Vector4(-0.25f,0.038f,0.041f,0),new Vector4(-0.19f,0.050f,0.049f,0),new Vector4(-0.05f,0.060f,0.055f,0),new Vector4(0.025f,0.045f,0.045f,0));
            Geo.Cyl(elbow,"ribbedCuff",new Vector3(0,-0.245f,0),0.040f,0.035f,sleeve,false);
            hand = Geo.Pivot(elbow, "hand", new Vector3(0, -0.27f, 0));
            Geo.Sph(hand,"palm",Vector3.zero,new Vector3(0.060f,0.079f,0.034f),skin);
            if (gripping)
            {
                // 指を筒の周囲に曲げ、掌と指の間でグリップを挟む。
                for (int finger = 0; finger < 4; finger++)
                {
                    float x = -0.021f + finger * 0.014f;
                    Geo.Sph(hand, "knuckle", new Vector3(x, -0.025f, 0.045f), new Vector3(0.016f, 0.038f, 0.022f), skin);
                    Geo.Sph(hand, "curledFinger", new Vector3(x, -0.043f, 0.031f), new Vector3(0.015f, 0.018f, 0.031f), skin);
                }
                var thumb = Geo.Sph(hand, "thumb", new Vector3(0.027f, -0.006f, 0.035f), new Vector3(0.023f, 0.041f, 0.026f), skin);
                thumb.transform.localRotation = Quaternion.Euler(0, 0, -30f);
            }
            else
            {
                for(int finger=0;finger<4;finger++) Geo.Sph(hand,"finger",new Vector3(-0.021f+finger*0.014f,-0.043f,0.011f),new Vector3(0.013f,0.045f,0.018f),skin);
                Geo.Sph(hand,"thumb",new Vector3(0.036f,-0.005f,0.014f),new Vector3(0.020f,0.049f,0.023f),skin);
            }
            return elbow;
        }

        Transform Leg(Transform hip, Material pants, Material skin, Material slipper)
        {
            Profile(hip,"trouserThigh",Vector3.zero,pants,new Vector4(-0.45f,0.066f,0.071f,0),new Vector4(-0.3f,0.078f,0.078f,0),new Vector4(-0.1f,0.09f,0.095f,0),new Vector4(0.045f,0.073f,0.075f,0));
            var knee = Geo.Pivot(hip, "knee", new Vector3(0, -0.43f, 0));
            Profile(knee,"trouserShin",Vector3.zero,pants,new Vector4(-0.42f,0.061f,0.063f,0),new Vector4(-0.34f,0.063f,0.067f,0),new Vector4(-0.17f,0.071f,0.075f,0),new Vector4(0.025f,0.066f,0.072f,0));
            Geo.Sph(knee, "ankle", new Vector3(0, -0.43f, 0), new Vector3(0.07f, 0.06f, 0.07f), skin);
            Geo.Sph(knee,"slipper",new Vector3(0,-0.46f,0.05f),new Vector3(0.115f,0.070f,0.255f),slipper);
            return knee;
        }

        public void Lunge() { act = Act.Lunge; actionT = 0f; }
        public void Wave() { act = Act.Wave; actionT = 0f; }
        public void Flinch() { act = Act.Flinch; actionT = 0f; painUntil=Time.time+1.4f; }
        public void Crouch() { act = Act.Crouch; actionT = 0f; }
        public void Hold() { act = Act.Hold; actionT = 0f; }
        public void PickUp() { act = Act.PickUp; actionT = 0f; }

        // Cross-sections create a continuous silhouette rather than stacked primitives.
        static void Profile(Transform parent,string name,Vector3 offset,Material material,params Vector4[] rings)
        {
            const int sides=32;
            int stride=sides+1;
            float gap=name=="knittedCardigan"?0.43f:0f;
            var vertices=new Vector3[rings.Length*stride];
            var uv=new Vector2[vertices.Length];
            var triangles=new int[(rings.Length-1)*sides*6];
            for(int row=0;row<rings.Length;row++) for(int col=0;col<=sides;col++) {
                float angle=gap+col*(Mathf.PI*2-2*gap)/sides;
                var r=rings[row];
                vertices[row*stride+col]=new Vector3(Mathf.Sin(angle)*r.y,r.x,Mathf.Cos(angle)*r.z+r.w);
                uv[row*stride+col]=new Vector2(col/(float)sides,row/(float)(rings.Length-1));
                if(name=="face") {
                    float fy=r.x;
                    float v=fy<0.076f?Mathf.Lerp(0,0.234f,fy/0.076f):fy<0.144f?Mathf.Lerp(0.234f,0.697f,(fy-0.076f)/0.068f):Mathf.Lerp(0.697f,1f,(fy-0.144f)/0.111f);
                    uv[row*stride+col]=Mathf.Cos(angle)>0?new Vector2(Mathf.Clamp01(0.5f+vertices[row*stride+col].x/0.18f),v):new Vector2(0.03f,0.4f);
                    float nose=Mathf.Exp(-Mathf.Pow((fy-0.105f)/0.035f,2))*Mathf.Pow(Mathf.Max(0,Mathf.Cos(angle)),24)*0.024f;
                    vertices[row*stride+col]+=Vector3.forward*nose;
                }
                if(row==rings.Length-1 || col==sides) continue;
                int a=row*stride+col,b=a+1,c=a+stride,d=b+stride,k=(row*sides+col)*6;
                triangles[k]=a;triangles[k+1]=b;triangles[k+2]=c;triangles[k+3]=b;triangles[k+4]=d;triangles[k+5]=c;
            }
            var mesh=new Mesh{name=name};mesh.vertices=vertices;mesh.uv=uv;mesh.triangles=triangles;mesh.RecalculateNormals();mesh.RecalculateBounds();
            var go=new GameObject(name);go.transform.SetParent(parent,false);go.transform.localPosition=offset;
            go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=material;
        }

        static void HairCap(Transform parent,Material material)
        {
            const int sides=40,rows=9;
            var v=new Vector3[sides*rows];var tris=new int[(rows-1)*sides*6];
            for(int r=0;r<rows;r++) for(int j=0;j<sides;j++) {
                float a=j*2*Mathf.PI/sides,t=r/(float)(rows-1);
                float baseY=Mathf.Lerp(0.055f,0.235f,Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(-0.1f,0.5f,Mathf.Cos(a))))+0.003f*Mathf.Cos(a*9f);
                float radius=Mathf.Sqrt(Mathf.Max(0,1f-t*t));
                v[r*sides+j]=new Vector3(Mathf.Sin(a)*0.115f*radius,Mathf.Lerp(baseY,0.325f,t),Mathf.Cos(a)*0.125f*radius-0.010f);
                if(r==rows-1) continue;
                int x=r*sides+j,y=r*sides+(j+1)%sides,k=(r*sides+j)*6;
                tris[k]=x;tris[k+1]=y;tris[k+2]=x+sides;tris[k+3]=y;tris[k+4]=y+sides;tris[k+5]=x+sides;
            }
            var mesh=new Mesh{name="bobHair"};mesh.vertices=v;mesh.triangles=tris;mesh.RecalculateNormals();mesh.RecalculateBounds();
            var go=new GameObject("bobHair");go.transform.SetParent(parent,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=material;
        }

        void UpdateFace()
        {
            var gm=GameManager.I;
            bool caught=gm!=null && gm.caught;
            bool scared=sheltering || danger || (gm!=null && gm.nightEvents!=null && gm.nightEvents.Quaking);
            CurrentExpression=caught?FaceExpression.Relieved:Time.time<painUntil?FaceExpression.Hurt:scared?FaceExpression.Scared:FaceExpression.Neutral;
            float blend=1f-Mathf.Exp(-Time.deltaTime*9f);
            faceFear=Mathf.Lerp(faceFear,CurrentExpression==FaceExpression.Scared?1f:0f,blend);
            faceSmile=Mathf.Lerp(faceSmile,CurrentExpression==FaceExpression.Relieved?1f:0f,blend);
            facePain=Mathf.Lerp(facePain,CurrentExpression==FaceExpression.Hurt?1f:0f,blend);
            blinkTimer-=Time.deltaTime;
            if(blinkTimer<=0f) blinkTimer=Random.Range(3f,6f);
            if(expressionMaterial==null || !expressionMaterial.HasProperty("_Fear")) return;
            expressionMaterial.SetFloat("_Fear",faceFear);
            expressionMaterial.SetFloat("_Smile",faceSmile);
            expressionMaterial.SetFloat("_Pain",facePain);
            expressionMaterial.SetFloat("_Blink",blinkTimer<0.16f?Mathf.Sin(blinkTimer/0.16f*Mathf.PI):0f);
        }
        void OnDestroy() { if(expressionMaterial!=null) Destroy(expressionMaterial); }

        void LateUpdate()
        {
            if (hips == null) return;
            UpdateFace();
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
            Quaternion armRRot = Quaternion.Euler(-18f + s * 2f, 0f, -6f);
            Quaternion foreRRot = Quaternion.Euler(-65f, 0, 0);
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
                    // 右（利き手）＝猫のお尻を下から支える。左＝脇（胸側）を支える。
                    armRRot = Quaternion.Euler(-70f, -15f, -10f);
                    armLRot = Quaternion.Euler(-65f, 20f, 15f);
                    foreRRot = Quaternion.Euler(-55f, 0, 0);
                    foreLRot = Quaternion.Euler(-55f, 0, 0);
                    spineRot = Quaternion.Euler(5f + breathe, 0, 0);
                    break;
                case Act.PickUp:
                    {
                        // しゃがむ→両手を差し込む→持ち上げる（約1.0秒）
                        float t = Mathf.Clamp01(actionT / 1.0f);
                        float crouchK = Mathf.Sin(Mathf.Clamp01(actionT / 0.6f) * Mathf.PI);
                        float liftK = Mathf.Clamp01((t - 0.3f) / 0.7f);
                        spineRot = Quaternion.Euler(10f + 40f * crouchK - 5f * liftK, 0, 0);
                        hipY -= 0.28f * crouchK;
                        armRRot = Quaternion.Euler(Mathf.Lerp(-20f, -70f, liftK), Mathf.Lerp(0f, -15f, liftK), Mathf.Lerp(0f, -10f, liftK));
                        armLRot = Quaternion.Euler(Mathf.Lerp(-10f, -65f, liftK), Mathf.Lerp(0f, 20f, liftK), Mathf.Lerp(0f, 15f, liftK));
                        foreRRot = Quaternion.Euler(Mathf.Lerp(-10f, -55f, liftK), 0, 0);
                        foreLRot = Quaternion.Euler(Mathf.Lerp(-10f, -55f, liftK), 0, 0);
                        if (actionT > 1.0f) { act = Act.Hold; actionT = 0f; }
                    }
                    break;
            }

            if (sheltering)
            {
                hipY = 0.25f;
                spineRot = Quaternion.Euler(72f,0f,0f);
                neckRot = Quaternion.Euler(15f,0f,0f);
                legLA = legRA = -75f; kneeL = kneeR = 135f;
                armLRot = Quaternion.Euler(-125f,0f,15f); armRRot = Quaternion.Euler(-125f,0f,-15f);
                foreLRot = foreRRot = Quaternion.Euler(-110f,0f,0f);
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

            // 猫じゃらしの先端の揺れ（バネ関節を使わず、腕の動きに追従する簡易ゆれ）
            if (toyObj != null && toyObj.activeSelf != toyEquipped) toyObj.SetActive(toyEquipped);
            if (toyTuft != null && toyEquipped)
            {
                bool waving = act == Act.Wave;
                toySwayT += dt * (waving ? 10f : 3f);
                float sway = Mathf.Sin(toySwayT) * (waving ? 22f : 6f);
                toyTuft.localRotation = Quaternion.Slerp(toyTuft.localRotation, Quaternion.Euler(sway, 0, sway * 0.6f), 1f - Mathf.Exp(-dt * 10f));
            }
        }
    }
}
