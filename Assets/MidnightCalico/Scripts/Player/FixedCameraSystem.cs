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

        void LateUpdate()
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
            if (current != null && current.Contains(pp)) z = current;
            else
            {
                foreach (var c in house.zones) if (c.Contains(pp)) { z = c; break; }
            }
            if (z == null) z = current;
            if (z == null) return;

            Vector3 target = pp + Vector3.up * 1.0f;
            if (z != current)
            {
                current = z;
                baseRot = Quaternion.LookRotation(z.look - z.pos);
                cam.transform.position = z.pos;
                cam.fieldOfView = z.fov;
                cam.transform.rotation = Quaternion.Slerp(baseRot, Quaternion.LookRotation(target - z.pos), z.track);
                return; // カット
            }
            Quaternion want = Quaternion.Slerp(baseRot, Quaternion.LookRotation(target - z.pos), z.track);
            cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, want, 1f - Mathf.Exp(-Time.deltaTime * 4f));
        }

        public void ForceCut() { current = null; }

        public void Shot(Vector3 pos, Vector3 look)
        {
            overrideShot = true;
            overridePos = pos;
            overrideLook = look;
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
