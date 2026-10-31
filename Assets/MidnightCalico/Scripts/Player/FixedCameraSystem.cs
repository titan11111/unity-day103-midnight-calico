using UnityEngine;

namespace MidnightCalico
{
    /// <summary>
    /// 部屋ごとに決められた固定アングルのカメラ。主人公がエリアをまたぐとカットで切り替わる。
    /// 少しだけ主人公の方へ首を振る（track）ことで見失わないようにしている。
    /// </summary>
    public class FixedCameraSystem : MonoBehaviour
    {
        public static FixedCameraSystem I;
        public Camera cam;
        public CamZone current;
        House house;
        Quaternion baseRot;
        /// <summary>直前のカメラカットが起きた時刻（Time.time）。カット直後の一瞬のスティック離しで移動方向が暴れないようにするための猶予判定に使う。</summary>
        public float lastCutTime = -99f;
        RenderTexture retroRT;
        public bool retro;
        public System.Action<bool> onRetroChanged;

        // 演出用（エンディングなど）
        bool overrideShot;
        Vector3 overridePos, overrideLook;

        public void Init(House h)
        {
            I = this;
            house = h;
            var g = new GameObject("FixedCamera");
            cam = g.AddComponent<Camera>();
            cam.tag = "MainCamera";
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 40f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.allowHDR = false;
            cam.allowMSAA = true;
            cam.depth = 0;
            if (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline == null) g.AddComponent<FilmLook>();
            // レトロ画質時（メインカメラがテクスチャに描くとき）も画面をクリアするための空カメラ
            var bg = new GameObject("BackgroundCamera").AddComponent<Camera>();
            bg.cullingMask = 0;
            bg.clearFlags = CameraClearFlags.SolidColor;
            bg.backgroundColor = Color.black;
            bg.depth = -100;
        }

        Vector3 quakeOffset;
        Quaternion quakeRotation = Quaternion.identity;
        void LateUpdate()
        {
            if (cam == null) return;
            // 前フレームの揺れを戻してから通常カメラを計算し、ずれの蓄積を防ぐ。
            cam.transform.position -= quakeOffset;
            cam.transform.rotation *= Quaternion.Inverse(quakeRotation);
            quakeOffset = Vector3.zero;
            quakeRotation = Quaternion.identity;
            UpdateCamera();
            var gm = GameManager.I;
            if (gm == null || gm.state != GState.Play || gm.nightEvents == null) return;
            float strength = gm.nightEvents.QuakeStrength;
            if (strength <= 0f) return;
            float t = gm.elapsed;
            quakeOffset = new Vector3(Mathf.Sin(t * 39f), Mathf.Sin(t * 47f) * 0.4f, 0f) * (0.035f * strength);
            quakeRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 32f) * 0.7f * strength);
            cam.transform.position += quakeOffset;
            cam.transform.rotation *= quakeRotation;
        }

        void UpdateCamera()
        {
            var p = PlayerController.I;
            if (p == null || cam == null) return;
            if (overrideShot)
            {
                cam.transform.position = Vector3.Lerp(cam.transform.position, overridePos, 1f - Mathf.Exp(-Time.deltaTime * 2f));
                cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, Quaternion.LookRotation(overrideLook - cam.transform.position), 1f - Mathf.Exp(-Time.deltaTime * 3f));
                return;
            }
            Vector3 pp = p.transform.position;
            CamZone z = null;
            foreach (var candidate in house.zones)
                if (candidate.Contains(pp) && (z == null || candidate.priority > z.priority)) z = candidate;
            if (current != null && current.HeightContains(pp) && ContainsWithMargin(current.rect, pp, 0.12f) &&
                (z == null || current.priority >= z.priority)) z = current;
            if (z == null) z = current;
            if (z == null) return;

            Vector3 target = pp + Vector3.up * 1.0f;
            if (z != current)
            {
                current = z;
                lastCutTime = Time.time;
                baseRot = Quaternion.LookRotation(z.look - z.pos);
                cam.transform.position = z.pos;
                cam.fieldOfView = z.fov;
                cam.transform.rotation = Quaternion.Slerp(baseRot, Quaternion.LookRotation(target - z.pos), z.track);
                return; // カット
            }
            Quaternion want = Quaternion.Slerp(baseRot, Quaternion.LookRotation(target - z.pos), z.track);
            cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, want, 1f - Mathf.Exp(-Time.deltaTime * 4f));
        }

        static bool ContainsWithMargin(Rect rect, Vector3 p, float margin)
        {
            return p.x >= rect.xMin - margin && p.x <= rect.xMax + margin &&
                p.z >= rect.yMin - margin && p.z <= rect.yMax + margin;
        }

        public void ForceCut() { current = null; }

        public void Shot(Vector3 pos, Vector3 look)
        {
            overrideShot = true;
            overridePos = pos;
            overrideLook = look;
        }

        /// <summary>Shot()による演出カットを終え、通常の固定カメラへ戻す（エンディング以外の一時演出用）</summary>
        public void CancelShot()
        {
            overrideShot = false;
            current = null;
        }

        public void SetRetro(bool on)
        {
            retro = on;
            if (on)
            {
                if (retroRT == null)
                {
                    retroRT = new RenderTexture(426, 240, 16);
                    retroRT.filterMode = FilterMode.Point;
                    retroRT.name = "RetroRT";
                }
                cam.targetTexture = retroRT;
            }
            else cam.targetTexture = null;
            if (onRetroChanged != null) onRetroChanged(on);
        }

        public RenderTexture RetroTexture { get { return retroRT; } }
    }
}
