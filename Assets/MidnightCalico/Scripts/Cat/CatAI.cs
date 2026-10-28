using System.Collections.Generic;
using UnityEngine;

namespace MidnightCalico
{
    public enum CatState { Idle, Wander, Alert, Flee, Hidden, Lured, Eating, Licking, Playing, Rolling, Zoomies, Hissing, Caught }

    public enum CatchResult { Success, Fail, Unreachable, TooFar }

    /// <summary>
    /// ミケの頭脳。見る・聞く・逃げる・隠れる・つられる・暴れる。
    /// </summary>
    public class CatAI : MonoBehaviour
    {
        public static CatAI I;

        public CatState state = CatState.Idle;
        public float stress;             // 0..100 緊張度
        public HideSpot spot;            // 今隠れている場所
        public CatModel model;

        House house;
        struct WP { public Vector3 p; public bool forceY; }
        readonly List<WP> path = new List<WP>();
        int pathIdx;
        HideSpot targetSpot;
        LureBowl targetBowl;
        float stateT, idleT, nearT, boredom, nextZoomies, smellT, meowT, alertMemory;
        bool unaware = true;
        float curSpeed;
        AudioSource purr;
        Vector3 lastPlayerPos;
        float lastSeenT = -99f;

        const float WalkSpeed = 0.75f, TrotSpeed = 1.8f, FleeSpeed = 4.6f, ZoomSpeed = 5.2f;

        public void Init(House h)
        {
            I = this;
            house = h;
            gameObject.layer = 2;
            model = gameObject.AddComponent<CatModel>();
            model.Build();
            Geo.SetLayerRecursive(gameObject, 2);
            BlobShadow.Attach(transform, 0.34f, 0.6f, 0.26f);
            purr = AudioMan.Loop3D(gameObject, Sfx.Purr, 0.9f, 6f);
            nextZoomies = Time.time + Random.Range(25f, 45f);
            SetState(CatState.Idle);
            idleT = 3f;
        }

        PlayerController P { get { return PlayerController.I; } }
        bool Playing { get { return GameManager.I != null && GameManager.I.state == GState.Play; } }

        public string RoomName { get { return house.RoomName(transform.position); } }

        // ───────────────────────── 感覚 ─────────────────────────
        Vector3 Eye { get { return transform.position + Vector3.up * 0.22f; } }

        bool CanSeePlayer(float maxDist)
        {
            if (P == null) return false;
            Vector3 a = Eye, b = P.transform.position + Vector3.up * 1.2f;
            if ((a - b).sqrMagnitude > maxDist * maxDist) return false;
            return !Physics.Linecast(a, b, ~(1 << 2), QueryTriggerInteraction.Ignore);
        }

        float PlayerDist
        {
            get
            {
                if (P == null) return 99f;
                Vector3 d = P.transform.position - transform.position; d.y = 0;
                return d.magnitude;
            }
        }

        bool PlayerApproaching
        {
            get
            {
                if (P == null) return false;
                Vector3 now = P.transform.position;
                float before = Vector3.Distance(lastPlayerPos, transform.position);
                float after = Vector3.Distance(now, transform.position);
                return after < before - 0.01f;
            }
        }

        /// <summary>主人公の気配（見える・足音が聞こえる）</summary>
        bool Notices(out bool loud)
        {
            loud = false;
            if (P == null) return false;
            float d = PlayerDist;
            if (P.IsRunning && P.IsMoving && d < 9f) { loud = true; return true; }
            // 背後からそっと近づかれると気づきにくい
            Vector3 toP = P.transform.position - transform.position; toP.y = 0;
            float ang = Vector3.Angle(transform.forward, toP);
            bool behind = ang > 125f;
            bool busy = model.pose == CatPose.Groom || model.pose == CatPose.Sit;
            float hear = behind ? 0.9f : (busy ? 1.5f : 2.0f);
            if (P.IsSneaking) hear *= 0.55f;   // しのび足（スティックを少し倒す）は聞こえにくい
            if (P.IsMoving && d < hear) return true;
            float sight = spot != null ? 3.0f : (busy ? 3.5f : 4.6f);
            if (!behind && d < sight && CanSeePlayer(sight)) return true;
            return false;
        }

        // ───────────────────────── 状態 ─────────────────────────
        void SetState(CatState s)
        {
            state = s;
            stateT = 0f;
            model.puffed = s == CatState.Flee || s == CatState.Hissing;
            model.earsBack = s == CatState.Flee || s == CatState.Hissing || (s == CatState.Hidden && stress > 60f);
            if (purr != null)
            {
                bool p = s == CatState.Eating || s == CatState.Licking || s == CatState.Rolling || s == CatState.Caught;
                if (p && !purr.isPlaying) purr.Play();
                if (!p && purr.isPlaying) purr.Stop();
            }
        }

        void Update()
        {
            if (house == null || P == null) return;
            float dt = Time.deltaTime;
            stateT += dt;
            boredom = Mathf.Max(0f, boredom - dt);
            alertMemory = Mathf.Max(0f, alertMemory - dt);
            curSpeed = 0f;
            if (CanSeePlayer(12f)) lastSeenT = Time.time;

            if (Playing && state != CatState.Caught) Think(dt);
            UpdateModel();
            lastPlayerPos = P.transform.position;
        }

        void Think(float dt)
        {
            bool loud;
            float d = PlayerDist;
            switch (state)
            {
                case CatState.Idle:
                case CatState.Wander:
                    stress = Mathf.Max(0f, stress - dt * 3f);
                    if (Notices(out loud))
                    {
                        if (loud || d < 2.2f) StartFlee(loud ? 25f : 15f);
                        else { SetState(CatState.Alert); unaware = false; }
                        break;
                    }
                    if (TryLures()) break;
                    if (Time.time > nextZoomies && stress < 50f && d > 5f) { StartZoomies(); break; }
                    if (state == CatState.Wander)
                    {
                        if (FollowPath(WalkSpeed)) { SetState(CatState.Idle); idleT = Random.Range(3f, 8f); }
                    }
                    else
                    {
                        idleT -= dt;
                        if (idleT <= 0f) StartWander();
                    }
                    unaware = stress < 20f && alertMemory <= 0f;
                    RandomMeow(0.03f);
                    break;

                case CatState.Alert:
                    FacePlayer(dt);
                    if (stateT > 0.7f)
                    {
                        if (d < 3.3f || PlayerApproaching || (P.IsRunning && P.IsMoving)) StartFlee(18f);
                        else { stress += 8f; alertMemory = 6f; SetState(CatState.Idle); idleT = 2f; }
                    }
                    break;

                case CatState.Hissing:
                    FacePlayer(dt);
                    if (stateT > 0.6f) StartFlee(10f);
                    break;

                case CatState.Flee:
                case CatState.Zoomies:
                    {
                        float spd = state == CatState.Flee ? FleeSpeed : ZoomSpeed;
                        bool done = FollowPath(spd);
                        KnockProps();
                        if (state == CatState.Zoomies && Random.value < dt * 0.8f && AudioMan.I != null)
                            AudioMan.I.PlayAt(Sfx.MeowShort, transform.position, 0.7f, Random.Range(1.0f, 1.2f));
                        if (state == CatState.Zoomies && d < 2.6f && P.IsMoving) { StartFlee(15f); break; }
                        if (done)
                        {
                            if (state == CatState.Flee && targetSpot != null)
                            {
                                spot = targetSpot;
                                targetSpot = null;
                                SetState(CatState.Hidden);
                                nearT = 0f;
                            }
                            else { SetState(CatState.Idle); idleT = Random.Range(2f, 5f); }
                        }
                    }
                    break;

                case CatState.Hidden:
                    {
                        float calm = d > 4f ? 7f : (d > 2.2f ? 2f : -2f);
                        stress = Mathf.Clamp(stress - calm * dt, 0f, 100f);
                        model.earsBack = stress > 60f;
                        if (TryLures()) break;
                        if (d < 2.0f)
                        {
                            nearT += dt;
                            if (nearT > 1.6f)
                            {
                                nearT = 0f;
                                // 近くに居座ると飛び出して逃げる
                                float bolt = spot != null && !spot.reachable ? 0.25f : 0.42f;
                                if (P.IsRunning) bolt += 0.3f;
                                if (Random.value < bolt) { Bolt(); break; }
                            }
                        }
                        else nearT = 0f;
                        if (stress < 22f && d > 5.5f && stateT > 6f)
                        {
                            StartWander();
                        }
                        RandomMeow(0.015f);
                    }
                    break;

                case CatState.Lured:
                    {
                        if (!pasteMode && (targetBowl == null || targetBowl.consumed)) { SetState(CatState.Idle); idleT = 1f; break; }
                        if (P.IsRunning && P.IsMoving && d < 6f) { pasteMode = false; StartFlee(20f); break; }
                        if (!pasteMode && d < 1.6f && P.IsMoving && Vector3.Distance(P.transform.position, targetBowl.transform.position) > 1.2f) { StartFlee(12f); break; }
                        if (pasteMode && stateT > 9f) { pasteMode = false; SetState(CatState.Idle); idleT = 2f; break; }
                        if (FollowPath(TrotSpeed))
                        {
                            if (pasteMode)
                            {
                                pasteMode = false;
                                SetState(CatState.Licking);
                            }
                            else SetState(targetBowl.kind == ItemId.Matatabi ? CatState.Rolling : CatState.Eating);
                            if (AudioMan.I != null) AudioMan.I.PlayAt(Sfx.MeowShort, transform.position, 0.8f, 1.15f);
                        }
                    }
                    break;

                case CatState.Eating:
                case CatState.Rolling:
                    {
                        float dur = state == CatState.Eating ? 11f : 13f;
                        if (P.IsRunning && P.IsMoving && d < 5f && stateT > 0.5f && state == CatState.Eating)
                        {
                            if (Random.value < 0.5f) { StartFlee(12f); break; }
                            stateT += 1.5f;
                        }
                        if (stateT > dur)
                        {
                            if (targetBowl != null) targetBowl.Consume();
                            targetBowl = null;
                            stress = 0f;
                            SetState(CatState.Idle); idleT = 2f;
                        }
                    }
                    break;

                case CatState.Licking:
                    FacePlayer(dt);
                    if (Random.value < dt * 3f && AudioMan.I != null) AudioMan.I.PlayAt(Sfx.Lick, transform.position, 0.6f);
                    if (stateT > 6.5f) { stress = Mathf.Max(0f, stress - 30f); StartWander(); }
                    break;

                case CatState.Playing:
                    {
                        Vector3 target = P.transform.position + P.transform.forward * 0.95f;
                        target.y = 0;
                        Vector3 to = target - transform.position; to.y = 0;
                        if (to.magnitude > 0.25f)
                        {
                            MoveToward(target, TrotSpeed * 1.2f);
                        }
                        else FacePlayer(dt);
                        if (stateT > 6.5f) { boredom = 14f; stress = Mathf.Max(0, stress - 20f); StartWander(); }
                    }
                    break;
            }
        }

        // ───────────────────────── 行動 ─────────────────────────
        void StartWander()
        {
            var g = house.graph;
            int from = g.Nearest(transform.position, RoomName);
            // 同じ部屋か隣の部屋の点へ
            var candidates = new List<int>();
            candidates.Add(from);
            foreach (int n in g.adj[from]) { candidates.Add(n); foreach (int m in g.adj[n]) candidates.Add(m); }
            int to = candidates[Random.Range(0, candidates.Count)];
            Vector3 jitter = new Vector3(Random.Range(-0.5f, 0.5f), 0, Random.Range(-0.5f, 0.5f));
            BuildPath(from, to, g.nodes[to] + jitter, false, false, 0f);
            spot = null;
            SetState(CatState.Wander);
        }

        void StartZoomies()
        {
            nextZoomies = Time.time + Random.Range(40f, 75f);
            var g = house.graph;
            int from = g.Nearest(transform.position, RoomName);
            int best = from; float bd = 0f;
            for (int k = 0; k < 6; k++)
            {
                int c = Random.Range(0, g.nodes.Count);
                float dd = Vector3.Distance(g.nodes[c], transform.position);
                if (dd > bd && Vector3.Distance(g.nodes[c], P.transform.position) > 4f) { bd = dd; best = c; }
            }
            BuildPath(from, best, g.nodes[best], false, false, 6f);
            SetState(CatState.Zoomies);
            if (AudioMan.I != null) AudioMan.I.PlayAt(Sfx.MeowShort, transform.position, 1f, 1.25f);
            if (GameManager.I != null && PlayerDist < 12f) GameManager.I.Toast("ドタドタドタッ……！ ミケが家の中を走り回っている！");
        }

        public void StartFlee(float addStress)
        {
            stress = Mathf.Clamp(stress + addStress, 0f, 100f);
            unaware = false;
            alertMemory = 10f;
            var g = house.graph;
            int from = g.Nearest(transform.position, RoomName);
            // 主人公から遠い隠れ場所を選ぶ（緊張が高いほど手の届かない場所へ）
            HideSpot best = null; float bestScore = float.MinValue;
            foreach (var h in house.hides)
            {
                if (h == spot) continue;
                if (h.node < 0) continue;
                float dp = Vector3.Distance(h.pos, P.transform.position);
                float dc = Vector3.Distance(h.pos, transform.position);
                float score = dp * 1.0f - dc * 0.25f + Random.Range(0f, 3.5f);
                if (!h.reachable) score += stress > 60f ? 4f : (stress > 35f ? 1.5f : -1.5f);
                if (dp < 3f) score -= 10f;
                if (score > bestScore) { bestScore = score; best = h; }
            }
            if (best == null) return;
            targetSpot = best;
            BuildPath(from, best.node, best.pos, true, true, 5f, best);
            spot = null;
            SetState(CatState.Flee);
            if (AudioMan.I != null) AudioMan.I.PlayAt(Random.value < 0.5f ? Sfx.MeowShort : Sfx.Meow, transform.position, 1f, Random.Range(1.05f, 1.3f));
        }

        void Bolt()
        {
            if (GameManager.I != null) GameManager.I.Toast("ミケが飛び出した！");
            StartFlee(15f);
        }

        void FacePlayer(float dt)
        {
            Vector3 to = P.transform.position - transform.position; to.y = 0;
            if (to.sqrMagnitude < 0.001f) return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(to), 360f * dt);
        }

        void MoveToward(Vector3 target, float spd)
        {
            Vector3 pos = transform.position;
            Vector3 to = target - pos; to.y = 0;
            float d = to.magnitude;
            float step = spd * Time.deltaTime;
            if (d > 0.001f)
            {
                pos += to / d * Mathf.Min(step, d);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(to), 540f * Time.deltaTime);
            }
            pos.y = Mathf.MoveTowards(pos.y, GroundAt(pos, pos.y), 3.5f * Time.deltaTime);
            transform.position = pos;
            curSpeed = spd;
        }

        void KnockProps()
        {
            foreach (var p in Prop.all)
            {
                if (p == null) continue;
                Vector3 d = p.transform.position - transform.position;
                if (Mathf.Abs(d.y) > 0.6f) continue;
                d.y = 0;
                if (d.sqrMagnitude < 0.45f * 0.45f)
                    p.Knock(transform.forward + d.normalized * 0.5f, 1f);
            }
        }

        void RandomMeow(float rate)
        {
            meowT -= Time.deltaTime;
            if (meowT <= 0f && Random.value < rate * Time.deltaTime * 60f * 0.02f)
            {
                meowT = 6f;
                if (AudioMan.I != null) AudioMan.I.PlayAt(Random.value < 0.3f ? Sfx.MeowLong : Sfx.Meow, transform.position, 0.8f, Random.Range(0.92f, 1.1f));
            }
        }

        // ───────────────────────── 誘い（アイテム） ─────────────────────────
        bool TryLures()
        {
            if (Time.time < smellT) return false;
            smellT = Time.time + 1.0f;
            LureBowl best = null; float bd = float.MaxValue;
            foreach (var b in LureBowl.all)
            {
                if (b == null || b.consumed) continue;
                float limit = b.kind == ItemId.Matatabi ? 95f : 80f;
                if (stress > limit) continue;
                if (Vector3.Distance(P.transform.position, b.transform.position) < 3.0f) continue;
                float dd = Vector3.Distance(b.transform.position, transform.position);
                if (dd < bd) { bd = dd; best = b; }
            }
            if (best == null) return false;
            targetBowl = best;
            var g = house.graph;
            int from = g.Nearest(transform.position, RoomName);
            string bowlRoom = house.RoomName(best.transform.position);
            int to = g.Nearest(best.transform.position, bowlRoom);
            Vector3 eatPos = best.transform.position + (transform.position - best.transform.position).normalized * 0.25f;
            eatPos.y = 0;
            BuildPath(from, to, eatPos, false, false, 0f);
            spot = null;
            SetState(CatState.Lured);
            if (GameManager.I != null) GameManager.I.Toast(best.kind == ItemId.Matatabi ? "……ミケがにおいに気づいたようだ。" : "……カリカリの音に、ミケが反応した。");
            return true;
        }

        /// <summary>猫じゃらしを振った</summary>
        public bool OnToy()
        {
            if (state == CatState.Caught) return false;
            float d = PlayerDist;
            if (boredom > 0f) { GameManager.I.Toast("ミケは猫じゃらしに飽きてしまったようだ。少し時間をおこう。"); return false; }
            bool hidden = state == CatState.Hidden;
            float range = hidden ? 4.5f : 6.5f;
            if (d > range) { GameManager.I.Toast("猫じゃらしを振った。……近くにミケはいないようだ。"); return false; }
            if (!hidden && !CanSeePlayer(range)) { GameManager.I.Toast("猫じゃらしを振った。……ミケからは見えていないようだ。"); return false; }
            if (stress > (hidden ? 70f : 85f)) { GameManager.I.Toast("ミケは警戒していて、遊ぶどころではない。"); return false; }
            spot = null;
            SetState(CatState.Playing);
            model.lookTarget = null;
            if (AudioMan.I != null) AudioMan.I.PlayAt(Sfx.MeowShort, transform.position, 1f, 1.3f);
            GameManager.I.Toast("ミケの目の色が変わった！ 猫じゃらしに夢中だ！");
            return true;
        }

        /// <summary>おやつペーストを差し出した</summary>
        public bool OnPaste()
        {
            if (state == CatState.Caught) return false;
            float d = PlayerDist;
            if (d > 5f) { GameManager.I.Toast("ペーストのにおいが届く範囲に、ミケはいないようだ。"); return false; }
            if (stress > 90f) { GameManager.I.Toast("ミケはおびえきっていて、出てこない……。"); return false; }
            Vector3 front = P.transform.position + P.transform.forward * 0.55f;
            front.y = 0f;
            var g = house.graph;
            int from = g.Nearest(transform.position, RoomName);
            int to = g.Nearest(front, house.RoomName(front));
            BuildPath(from, to, front, false, false, 0f);
            spot = null;
            pasteMode = true;
            SetState(CatState.Lured);
            targetBowl = null;
            GameManager.I.Toast("ペーストの封を切った。……ミケがそろりと近づいてくる。");
            return true;
        }
        bool pasteMode;

        // ───────────────────────── 捕獲判定 ─────────────────────────
        public bool IsCatchTarget(Vector3 from, Vector3 forward, float reach)
        {
            Vector3 d = transform.position - from; d.y = 0;
            if (d.magnitude > reach) return false;
            if (d.magnitude < 0.5f) return true;
            return Vector3.Angle(forward, d) < 80f;
        }

        public CatchResult TryCatch(bool hasNet, out string message)
        {
            message = "";
            float chance;
            switch (state)
            {
                case CatState.Eating:
                case CatState.Licking:
                case CatState.Playing:
                case CatState.Rolling:
                    chance = 1f; break;
                case CatState.Hidden:
                    if (spot != null && !spot.reachable)
                    {
                        message = spot.name + "に潜り込んでいて、手が届かない……！";
                        if (Random.value < 0.3f) Bolt();
                        else stress = Mathf.Min(100f, stress + 10f);
                        return CatchResult.Unreachable;
                    }
                    chance = hasNet ? 0.7f : 0.35f; break;
                case CatState.Idle:
                case CatState.Wander:
                    chance = unaware ? (hasNet ? 0.85f : 0.5f) : (hasNet ? 0.5f : 0.25f); break;
                case CatState.Alert:
                case CatState.Lured:
                    chance = hasNet ? 0.5f : 0.25f; break;
                default:
                    chance = hasNet ? 0.3f : 0.12f; break;
            }
            if (state == CatState.Lured && pasteMode) chance = 1f;
            if (Random.value < chance)
            {
                SetState(CatState.Caught);
                spot = null;
                return CatchResult.Success;
            }
            // 失敗：シャーッ！
            if (AudioMan.I != null) AudioMan.I.PlayAt(Sfx.Hiss, transform.position, 1f);
            SetState(CatState.Hissing);
            stress = Mathf.Min(100f, stress + 30f);
            message = "シャーッ！ ミケに逃げられた！";
            return CatchResult.Fail;
        }

        // ───────────────────────── 移動 ─────────────────────────
        void BuildPath(int from, int to, Vector3 final, bool fleeing, bool spotFinal, float avoidWeight, HideSpot target = null)
        {
            path.Clear();
            pathIdx = 0;
            var g = house.graph;
            // 隠れ場所から出るときはまず入り口へ
            if (spot != null)
            {
                path.Add(new WP { p = spot.approach, forceY = true });
            }
            var nodes = g.Path(from, to, P != null ? P.transform.position : Vector3.zero, avoidWeight);
            for (int i = 0; i < nodes.Count; i++)
            {
                // 最初の点が自分より後ろ側なら飛ばす
                if (i == 0 && nodes.Count > 1)
                {
                    Vector3 a = g.nodes[nodes[0]], b = g.nodes[nodes[1]];
                    if (Vector3.Distance(transform.position, b) < Vector3.Distance(a, b)) continue;
                }
                path.Add(new WP { p = g.nodes[nodes[i]], forceY = false });
            }
            if (target != null)
            {
                path.Add(new WP { p = target.approach, forceY = false });
                path.Add(new WP { p = target.pos, forceY = true });
            }
            else path.Add(new WP { p = final, forceY = false });
            if (!fleeing) pasteMode = false;
        }

        /// <summary>道をたどる。到着したら true</summary>
        bool FollowPath(float spd)
        {
            if (pathIdx >= path.Count) return true;
            var wp = path[pathIdx];
            Vector3 pos = transform.position;
            Vector3 to = new Vector3(wp.p.x - pos.x, 0f, wp.p.z - pos.z);
            float d = to.magnitude;
            float step = spd * Time.deltaTime;
            bool arrived = d <= step;
            if (arrived) { pos.x = wp.p.x; pos.z = wp.p.z; }
            else pos += to / d * step;
            if (d > 0.01f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(to), 720f * Time.deltaTime);
            float gy = wp.forceY && d < 0.9f ? wp.p.y : GroundAt(pos, pos.y);
            pos.y = Mathf.MoveTowards(pos.y, gy, 4.5f * Time.deltaTime);
            transform.position = pos;
            if (arrived && (!wp.forceY || Mathf.Abs(pos.y - wp.p.y) < 0.03f)) pathIdx++;
            curSpeed = spd;
            return pathIdx >= path.Count;
        }

        /// <summary>足もとの高さ（ソファやベッド、テーブルの上も歩ける）</summary>
        float GroundAt(Vector3 p, float current)
        {
            RaycastHit hit;
            if (Physics.Raycast(new Vector3(p.x, 1.2f, p.z), Vector3.down, out hit, 2f, ~(1 << 2), QueryTriggerInteraction.Ignore))
            {
                if (hit.point.y <= 0.95f) return Mathf.Max(0f, hit.point.y);
                return current;
            }
            return 0f;
        }

        // ───────────────────────── 見た目 ─────────────────────────
        void UpdateModel()
        {
            model.speed = curSpeed;
            switch (state)
            {
                case CatState.Idle:
                    model.pose = (Mathf.FloorToInt(Time.time / 7f + GetInstanceID()) % 3 == 0) ? CatPose.Groom : CatPose.Sit;
                    break;
                case CatState.Wander: model.pose = CatPose.Stand; break;
                case CatState.Alert: model.pose = CatPose.Crouch; break;
                case CatState.Hissing: model.pose = CatPose.Hiss; break;
                case CatState.Flee:
                case CatState.Zoomies: model.pose = CatPose.Stand; break;
                case CatState.Hidden: model.pose = CatPose.Hide; break;
                case CatState.Lured: model.pose = CatPose.Stand; break;
                case CatState.Eating:
                case CatState.Licking: model.pose = CatPose.Eat; break;
                case CatState.Playing: model.pose = CatPose.Play; break;
                case CatState.Rolling: model.pose = CatPose.Roll; break;
                case CatState.Caught: model.pose = CatPose.Held; break;
            }
            bool look = state == CatState.Alert || state == CatState.Hidden || state == CatState.Hissing || state == CatState.Playing || state == CatState.Licking
                        || (state == CatState.Idle && Time.time - lastSeenT < 1f && PlayerDist < 6f);
            model.lookTarget = look && P != null ? (Vector3?)(P.transform.position + Vector3.up * 1.3f) : null;

            // 懐中電灯の光が当たると目が光る
            float glow = 0f;
            if (P != null && P.FlashlightOn)
            {
                Vector3 to = (transform.position + Vector3.up * 0.25f) - P.LightOrigin;
                float ang = Vector3.Angle(P.LightForward, to);
                if (ang < 24f && to.magnitude < 11f && CanSeePlayer(11f)) glow = 1f - ang / 24f * 0.5f;
            }
            model.glowTarget = glow;
        }

        /// <summary>捕まえた後、主人公の腕の中へ</summary>
        public void AttachTo(Transform hand)
        {
            SetState(CatState.Caught);
            transform.SetParent(hand, false);
            transform.localPosition = new Vector3(0f, -0.15f, 0.1f);
            transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
        }

        public string DebugLabel()
        {
            return state + (spot != null ? " @" + spot.name : "") + " stress=" + stress.ToString("0");
        }
    }
}
