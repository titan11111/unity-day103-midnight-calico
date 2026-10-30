using System.Collections.Generic;
using UnityEngine;

namespace MidnightCalico
{
    public enum CatPose { Stand, Sit, Groom, Crouch, Hide, Eat, Play, Roll, Hiss, Held }

    /// <summary>
    /// 三毛猫「ミケ」の体をプリミティブで組み立て、関節を手続き的に動かす。
    /// 体長およそ 45cm、体高 25cm の成猫サイズ。
    /// </summary>
    public class CatModel : MonoBehaviour
    {
        Transform body, chest, haunch, neck, head, earL, earR, tailRoot;
        readonly Transform[] legUpper = new Transform[4];
        readonly Transform[] legLower = new Transform[4];
        readonly List<Transform> tail = new List<Transform>();
        Material eyeMat;
        readonly List<Transform> pupils = new List<Transform>();
        float earTwitchL, earTwitchR, tipFlick, breathT, headTilt;
        Renderer[] eyeRenderers;
        float gait, tailT, blinkT, pounceT;
        float eyeGlow;
        public float EyeEmission { get; private set; }
        Vector3 bodyBase = new Vector3(0f, 0.2f, 0f);

        public CatPose pose = CatPose.Stand;
        public float speed;          // 移動速度 m/s
        public Vector3? lookTarget;  // 頭を向ける先（ワールド）
        public float glowTarget;     // 懐中電灯で目が光る量
        public bool puffed;          // しっぽが太くなる（驚き）
        public bool earsBack;        // 耳を伏せる（怖い・怒り）

        public void Build()
        {
            var fur = Mat.WithNormal(Mat.Make("calicoFur", Color.white, Tex.Calico(), 0.12f, 0f, 1f, 1f), 3.5f);   // 毛並みの凹凸
            var white = Mat.Make("catWhite", new Color(0.95f, 0.94f, 0.9f), null, 0.08f);
            var black = Mat.Make("catBlack", new Color(0.09f, 0.08f, 0.07f), null, 0.1f);
            var orange = Mat.Make("catOrange", new Color(0.85f, 0.5f, 0.2f), null, 0.08f);
            var pink = Mat.Make("catPink", new Color(0.92f, 0.6f, 0.62f), null, 0.4f);
            eyeMat = new Material(Mat.Make("catEyeBase", new Color(0.75f, 0.78f, 0.25f), null, 0.95f));
            eyeMat.name = "catEye";
            Mat.SetEmission(eyeMat, Color.black);
            var pupil = Mat.Make("catPupil", new Color(0.02f, 0.02f, 0.02f), null, 0.95f);

            body = Geo.Pivot(transform, "body", bodyBase);
            // 胴（胸・腰・お腹）
            Geo.Sph(body, "torso", new Vector3(0, 0.01f, -0.01f), new Vector3(0.17f, 0.155f, 0.36f), fur);
            chest = Geo.Pivot(body, "chestP", new Vector3(0, 0.01f, 0.12f));
            Geo.Sph(chest, "chest", Vector3.zero, new Vector3(0.16f, 0.17f, 0.17f), fur);
            Geo.Sph(chest, "bib", new Vector3(0, -0.03f, 0.05f), new Vector3(0.11f, 0.12f, 0.09f), white);
            haunch = Geo.Pivot(body, "haunchP", new Vector3(0, 0.01f, -0.14f));
            Geo.Sph(haunch, "haunch", Vector3.zero, new Vector3(0.17f, 0.17f, 0.19f), fur);
            Geo.Sph(body, "belly", new Vector3(0, -0.05f, -0.02f), new Vector3(0.13f, 0.08f, 0.28f), white);
            // 毛のふくらみ（体の輪郭をなめらかにつなぐ）
            Geo.Sph(body, "back", new Vector3(0, 0.055f, -0.02f), new Vector3(0.13f, 0.07f, 0.34f), fur);
            Geo.Sph(body, "neckFill", new Vector3(0, 0.055f, 0.17f), new Vector3(0.12f, 0.12f, 0.12f), fur);
            Geo.Sph(chest, "ruff", new Vector3(0, -0.01f, 0.07f), new Vector3(0.12f, 0.1f, 0.07f), white);
            Geo.Sph(haunch, "fluffL", new Vector3(-0.06f, -0.03f, -0.03f), new Vector3(0.07f, 0.11f, 0.13f), fur);
            Geo.Sph(haunch, "fluffR", new Vector3(0.06f, -0.03f, -0.03f), new Vector3(0.07f, 0.11f, 0.13f), fur);

            // 首・頭
            neck = Geo.Pivot(body, "neck", new Vector3(0, 0.07f, 0.19f));
            head = Geo.Pivot(neck, "head", new Vector3(0, 0.035f, 0.035f));
            Geo.Sph(head, "skull", Vector3.zero, new Vector3(0.14f, 0.12f, 0.13f), fur);
            Geo.Sph(head, "cheekL", new Vector3(-0.035f, -0.025f, 0.03f), new Vector3(0.07f, 0.06f, 0.07f), white);
            Geo.Sph(head, "cheekR", new Vector3(0.035f, -0.025f, 0.03f), new Vector3(0.07f, 0.06f, 0.07f), white);
            Geo.Sph(head, "muzzle", new Vector3(0, -0.028f, 0.058f), new Vector3(0.055f, 0.04f, 0.045f), white);
            Geo.Sph(head, "chin", new Vector3(0, -0.048f, 0.045f), new Vector3(0.035f, 0.025f, 0.035f), white);
            Geo.Sph(head, "nose", new Vector3(0, -0.012f, 0.079f), new Vector3(0.018f, 0.012f, 0.012f), pink);
            Geo.Sph(head, "blaze", new Vector3(0, 0.02f, 0.055f), new Vector3(0.03f, 0.06f, 0.03f), white);
            Geo.Sph(head, "patchO", new Vector3(-0.04f, 0.035f, 0.02f), new Vector3(0.07f, 0.06f, 0.08f), orange);
            Geo.Sph(head, "patchB", new Vector3(0.04f, 0.035f, 0.02f), new Vector3(0.07f, 0.06f, 0.08f), black);
            // 目（懐中電灯を当てると光る）
            var eyes = new List<Renderer>();
            for (int s = -1; s <= 1; s += 2)
            {
                var e = Geo.Sph(head, "eye", new Vector3(s * 0.03f, 0.012f, 0.066f), new Vector3(0.026f, 0.024f, 0.016f), eyeMat);
                e.GetComponent<Renderer>().sharedMaterial = eyeMat;
                eyes.Add(e.GetComponent<Renderer>());
                var pp = Geo.Sph(head, "pupil", new Vector3(s * 0.03f, 0.012f, 0.0745f), new Vector3(0.007f, 0.02f, 0.004f), pupil);
                pupils.Add(pp.transform);
                // 目のハイライト
                var hl = Geo.Sph(head, "catchlight", new Vector3(s * 0.03f + 0.005f, 0.019f, 0.076f), Vector3.one * 0.004f, Mat.Emissive("catchlight", Color.white, Color.white * 0.8f));
                hl.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            eyeRenderers = eyes.ToArray();
            // 頬の毛・眉のひげ
            Geo.Sph(head, "cheekFurL", new Vector3(-0.062f, -0.02f, 0.0f), new Vector3(0.04f, 0.05f, 0.06f), white);
            Geo.Sph(head, "cheekFurR", new Vector3(0.062f, -0.02f, 0.0f), new Vector3(0.04f, 0.05f, 0.06f), white);
            // 耳
            earL = Geo.Pivot(head, "earL", new Vector3(-0.042f, 0.05f, 0.0f));
            earR = Geo.Pivot(head, "earR", new Vector3(0.042f, 0.05f, 0.0f));
            Geo.MeshObj(earL, "ear", Geo.Pyramid(), Vector3.zero, Vector3.zero, new Vector3(0.05f, 0.055f, 0.02f), orange);
            Geo.MeshObj(earL, "earIn", Geo.Pyramid(), new Vector3(0, 0.004f, 0.006f), Vector3.zero, new Vector3(0.032f, 0.04f, 0.01f), pink);
            Geo.MeshObj(earR, "ear", Geo.Pyramid(), Vector3.zero, Vector3.zero, new Vector3(0.05f, 0.055f, 0.02f), black);
            Geo.MeshObj(earR, "earIn", Geo.Pyramid(), new Vector3(0, 0.004f, 0.006f), Vector3.zero, new Vector3(0.032f, 0.04f, 0.01f), pink);
            // ひげ
            var whisker = Mat.Make("whisker", new Color(0.95f, 0.95f, 0.95f), null, 0.6f);
            for (int s = -1; s <= 1; s += 2)
                for (int k = 0; k < 3; k++)
                {
                    var w = Geo.Box(head, "whisker", new Vector3(s * 0.065f, -0.025f + k * 0.006f, 0.062f), new Vector3(0.09f, 0.0015f, 0.0015f), whisker, false);
                    w.transform.localRotation = Quaternion.Euler(0, s * (-15f + k * 6f), s * (8f - k * 8f));
                    w.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }

            // 脚: 0=左前 1=右前 2=左後 3=右後
            Vector3[] hips = { new Vector3(-0.05f, -0.02f, 0.14f), new Vector3(0.05f, -0.02f, 0.14f), new Vector3(-0.055f, -0.02f, -0.15f), new Vector3(0.055f, -0.02f, -0.15f) };
            for (int i = 0; i < 4; i++)
            {
                bool back = i >= 2;
                var legMat = (i == 2) ? orange : (i == 3 ? fur : white);
                legUpper[i] = Geo.Pivot(body, "leg" + i, hips[i]);
                Geo.Sph(legUpper[i], "thigh", new Vector3(0, -0.045f, back ? -0.01f : 0f), back ? new Vector3(0.055f, 0.12f, 0.075f) : new Vector3(0.042f, 0.11f, 0.05f), legMat);
                legLower[i] = Geo.Pivot(legUpper[i], "knee", new Vector3(0, -0.09f, back ? -0.02f : 0f));
                Geo.Cyl(legLower[i], "shin", new Vector3(0, -0.04f, 0), 0.016f, 0.09f, white, false);
                Geo.Sph(legLower[i], "paw", new Vector3(0, -0.085f, 0.012f), new Vector3(0.036f, 0.022f, 0.045f), white);
            }

            // しっぽ（8節）
            tailRoot = Geo.Pivot(body, "tailRoot", new Vector3(0, 0.05f, -0.3f));
            Transform parent = tailRoot;
            for (int i = 0; i < 8; i++)
            {
                var seg = Geo.Pivot(parent, "tail" + i, i == 0 ? Vector3.zero : new Vector3(0, 0, -0.042f));
                float r = Mathf.Lerp(0.022f, 0.014f, i / 7f);
                var tm = (i % 3 == 1) ? black : (i % 3 == 2 ? orange : fur);
                var g = Geo.Sph(seg, "t", new Vector3(0, 0, -0.021f), new Vector3(r * 2f, r * 2f, 0.05f), tm);
                g.name = "tailMesh";
                tail.Add(seg);
                parent = seg;
            }

            foreach (var r in GetComponentsInChildren<Renderer>()) r.receiveShadows = true;
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (body == null) return;
            gait += dt * Mathf.Lerp(2.5f, 14f, Mathf.Clamp01(speed / 5f)) * (speed > 0.05f ? 1f : 0f) * Mathf.PI;
            tailT += dt;
            blinkT -= dt;

            bool moving = speed > 0.05f;
            bool running = speed > 2.5f;
            float amp = running ? 55f : 28f;

            // ── 脚 ──
            for (int i = 0; i < 4; i++)
            {
                float phase;
                if (running) phase = gait + (i >= 2 ? Mathf.PI : 0f) + (i % 2 == 1 ? 0.35f : 0f);   // 跳ねるように走る
                else phase = gait + ((i == 0 || i == 3) ? 0f : Mathf.PI);                         // 対角の脚が同時
                float upper = 0f, lower = 0f;
                if (moving)
                {
                    upper = Mathf.Sin(phase) * amp;
                    lower = Mathf.Max(0f, -Mathf.Cos(phase)) * (i >= 2 ? -45f : 40f);
                }
                switch (pose)
                {
                    case CatPose.Sit:
                    case CatPose.Groom:
                        if (i >= 2) { upper = -70f; lower = 120f; } else { upper = -12f; lower = 0f; }
                        break;
                    case CatPose.Crouch:
                    case CatPose.Hide:
                    case CatPose.Eat:
                        if (!moving) { upper = i >= 2 ? -55f : 50f; lower = i >= 2 ? 100f : -95f; }
                        break;
                    case CatPose.Play:
                        if (!moving)
                        {
                            float swipe = Mathf.Max(0f, Mathf.Sin(tailT * 9f));
                            if (i == 1) { upper = -60f - 60f * swipe; lower = 30f; }
                            else if (i >= 2) { upper = -55f; lower = 100f; }
                            else { upper = 30f; lower = -60f; }
                        }
                        break;
                    case CatPose.Roll:
                        upper = Mathf.Sin(tailT * 5f + i) * 40f - 20f;
                        lower = 40f;
                        break;
                    case CatPose.Held:
                        upper = 15f; lower = 10f;
                        break;
                }
                legUpper[i].localRotation = Quaternion.Slerp(legUpper[i].localRotation, Quaternion.Euler(upper, 0, 0), 1f - Mathf.Exp(-dt * 18f));
                legLower[i].localRotation = Quaternion.Slerp(legLower[i].localRotation, Quaternion.Euler(lower, 0, 0), 1f - Mathf.Exp(-dt * 18f));
            }

            // ── 胴体の姿勢 ──
            Vector3 bPos = bodyBase;
            Vector3 bRot = Vector3.zero;
            if (moving)
            {
                bPos.y += Mathf.Abs(Mathf.Sin(gait)) * (running ? 0.035f : 0.008f);
                bRot.x = running ? Mathf.Sin(gait) * 8f : 0f;
            }
            switch (pose)
            {
                case CatPose.Sit:
                case CatPose.Groom:
                    bPos.y = 0.16f; bPos.z = -0.04f; bRot.x = -32f; break;
                case CatPose.Crouch:
                case CatPose.Hide:
                    if (!moving) { bPos.y = 0.11f; } else { bPos.y -= 0.04f; }
                    break;
                case CatPose.Eat:
                    bPos.y = 0.12f; bRot.x = 8f; break;
                case CatPose.Play:
                    pounceT += dt;
                    if (!moving) { bPos.y = 0.13f + Mathf.Max(0f, Mathf.Sin(pounceT * 4f)) * 0.06f; bRot.x = -10f + Mathf.Sin(pounceT * 4f) * 12f; }
                    break;
                case CatPose.Roll:
                    bPos.y = 0.1f; bRot.z = 90f + Mathf.Sin(tailT * 2.2f) * 60f; break;
                case CatPose.Hiss:
                    bPos.y = 0.24f; bRot.x = 6f; break;
                case CatPose.Held:
                    bPos.y = 0.0f; bRot.x = -60f; break;
            }
            body.localPosition = Vector3.Lerp(body.localPosition, bPos, 1f - Mathf.Exp(-dt * 10f));
            body.localRotation = Quaternion.Slerp(body.localRotation, Quaternion.Euler(bRot), 1f - Mathf.Exp(-dt * 8f));

            // ── 頭 ──
            Quaternion headRot = Quaternion.identity;
            if (pose == CatPose.Groom)
            {
                headRot = Quaternion.Euler(35f + Mathf.Sin(tailT * 6f) * 10f, -50f, -20f);
            }
            else if (pose == CatPose.Eat)
            {
                headRot = Quaternion.Euler(55f + Mathf.Sin(tailT * 8f) * 6f, 0, 0);
            }
            else if (lookTarget.HasValue)
            {
                Vector3 dir = lookTarget.Value - head.position;
                Quaternion world = Quaternion.LookRotation(dir.sqrMagnitude > 0.0001f ? dir : head.forward);
                Quaternion local = Quaternion.Inverse(neck.rotation) * world;
                Vector3 e = local.eulerAngles;
                float yaw = Mathf.Clamp(Mathf.DeltaAngle(0, e.y), -75f, 75f);
                float pitch = Mathf.Clamp(Mathf.DeltaAngle(0, e.x), -40f, 40f);
                headRot = Quaternion.Euler(pitch, yaw, 0);
            }
            else if (!moving)
            {
                headRot = Quaternion.Euler(Mathf.Sin(tailT * 0.7f) * 5f, Mathf.Sin(tailT * 0.4f) * 25f, 0);
            }
            neck.localRotation = Quaternion.Slerp(neck.localRotation, headRot, 1f - Mathf.Exp(-dt * 7f));

            // ── 耳 ──
            // 耳：ときどき片耳だけピクッと動く。怖いと伏せる（イカ耳）
            if (Random.value < dt * 0.4f) earTwitchL = 1f;
            if (Random.value < dt * 0.4f) earTwitchR = 1f;
            earTwitchL = Mathf.MoveTowards(earTwitchL, 0f, dt * 5f);
            earTwitchR = Mathf.MoveTowards(earTwitchR, 0f, dt * 5f);
            float back = earsBack ? 1f : 0f;
            earL.localRotation = Quaternion.Slerp(earL.localRotation, Quaternion.Euler(back * 25f, -earTwitchL * 35f + back * -40f, -12f - back * 55f + earTwitchL * 10f), 1f - Mathf.Exp(-dt * 14f));
            earR.localRotation = Quaternion.Slerp(earR.localRotation, Quaternion.Euler(back * 25f, earTwitchR * 35f + back * 40f, 12f + back * 55f - earTwitchR * 10f), 1f - Mathf.Exp(-dt * 14f));

            // 呼吸（止まっている時ほど見える）
            breathT += dt * (moving ? 3.2f : 1.7f) * (pose == CatPose.Hide ? 1.6f : 1f);
            float br = Mathf.Sin(breathT) * (moving ? 0.01f : 0.025f);
            chest.localScale = new Vector3(1f + br, 1f + br, 1f);
            // 首をかしげる（警戒・興味）
            float tiltTarget = (pose == CatPose.Crouch || pose == CatPose.Play) && !moving ? Mathf.Sin(tailT * 0.6f) * 14f : 0f;
            headTilt = Mathf.Lerp(headTilt, tiltTarget, 1f - Mathf.Exp(-dt * 3f));
            head.localRotation = Quaternion.Euler(0, 0, headTilt);

            // ── しっぽ ──
            float baseUp, sway, curl;
            if (running) { baseUp = 5f; sway = 10f; curl = 4f; }
            else if (pose == CatPose.Hiss) { baseUp = 65f; sway = 3f; curl = -10f; }
            else if (pose == CatPose.Sit || pose == CatPose.Groom) { baseUp = -60f; sway = 25f; curl = 10f; }
            else if (pose == CatPose.Hide || pose == CatPose.Crouch) { baseUp = -15f; sway = 35f; curl = 6f; }
            else if (pose == CatPose.Play) { baseUp = 10f; sway = 45f; curl = 6f; }
            else { baseUp = 55f; sway = 12f; curl = -14f; }
            float swaySpeed = pose == CatPose.Play || pose == CatPose.Hide ? 6f : 1.8f;
            for (int i = 0; i < tail.Count; i++)
            {
                float k = i / (float)(tail.Count - 1);
                float x = (i == 0 ? baseUp : curl) + 0f;
                float y = Mathf.Sin(tailT * swaySpeed - i * 0.6f) * sway * (0.3f + k);
                // しっぽの先だけピクピク（狩りの集中・いらだち）
                if (i >= tail.Count - 3) y += tipFlick * Mathf.Sin(tailT * 22f) * 30f;
                tail[i].localRotation = Quaternion.Slerp(tail[i].localRotation, Quaternion.Euler(x, y, 0), 1f - Mathf.Exp(-dt * 10f));
                float fat = puffed ? 1.9f : 1f;
                tail[i].localScale = Vector3.Lerp(tail[i].localScale, new Vector3(fat, fat, 1f), 1f - Mathf.Exp(-dt * 8f));
            }

            if ((pose == CatPose.Hide || pose == CatPose.Crouch || pose == CatPose.Play) && Random.value < dt * 0.7f) tipFlick = 1f;
            tipFlick = Mathf.MoveTowards(tipFlick, 0f, dt * 1.5f);

            // 瞳孔：暗い所・興奮で丸く、ライトを当てると細く
            float dil = (pose == CatPose.Play || pose == CatPose.Hide || pose == CatPose.Hiss) ? 1f : 0.55f;
            dil = Mathf.Lerp(dil, 0.15f, Mathf.Clamp01(eyeGlow));
            foreach (var p in pupils)
            {
                var ps = p.localScale;
                ps.x = Mathf.Lerp(ps.x, Mathf.Lerp(0.003f, 0.019f, dil), 1f - Mathf.Exp(-dt * 6f));
                p.localScale = ps;
            }

            // ── 目の反射（タペタム） ──
            // 暗い部屋では弱い黄緑の反射。カメラ側へ顔を向けると見え、点灯した部屋では控えめに。
            bool roomLit = false;
            var gm = GameManager.I;
            if (gm != null && gm.house != null)
            {
                string room = gm.house.RoomName(transform.position);
                foreach (var sw in gm.house.switches) if (sw.on && sw.room == room) { roomLit = true; break; }
            }
            float facing = 0f;
            var cs = FixedCameraSystem.I;
            if (cs != null && cs.cam != null)
            {
                Vector3 toCamera = cs.cam.transform.position - head.position;
                facing = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.05f, 0.7f, Vector3.Dot(head.forward, toCamera.normalized)));
                facing *= 1f - Mathf.InverseLerp(8f, 18f, toCamera.magnitude);
            }
            float darkReflection = (roomLit ? 0.06f : 0.42f) * facing;
            eyeGlow = Mathf.MoveTowards(eyeGlow, Mathf.Max(darkReflection, glowTarget * facing), dt * 4f);
            if (blinkT < 0f) blinkT = Random.Range(2.5f, 6f);
            bool blink = blinkT < 0.12f;
            EyeEmission = blink ? 0f : eyeGlow * 2.2f;
            Mat.SetEmission(eyeMat, new Color(0.65f, 0.85f, 0.3f) * EyeEmission);
            foreach (var r in eyeRenderers)
            {
                var s = r.transform.localScale;
                s.y = Mathf.Lerp(s.y, blink ? 0.003f : 0.024f, 1f - Mathf.Exp(-dt * 30f));
                r.transform.localScale = s;
            }
            foreach (var p in pupils)
            {
                var q = p.localScale;
                q.y = Mathf.Lerp(q.y, blink ? 0.002f : 0.02f, 1f - Mathf.Exp(-dt * 30f));
                p.localScale = q;
            }
        }
    }
}
