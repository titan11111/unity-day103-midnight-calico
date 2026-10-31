using UnityEngine;

namespace MidnightCalico
{
    /// <summary>
    /// カメラに付ける画面エフェクト（Built-in レンダーパイプライン用）。
    /// URP では OnRenderImage が呼ばれないので、何もしない。
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class FilmLook : MonoBehaviour
    {
        public static bool enabledByUser = true;
        public static float Brightness { get { return PlayerPrefs.GetFloat("mc.brightness", Application.isMobilePlatform ? 1.2f : 1f); } }
        Material mat;
        bool tried;

        void OnRenderImage(RenderTexture src, RenderTexture dst)
        {
            if (!tried)
            {
                tried = true;
                var sh = Shader.Find("Hidden/MidnightCalico/FilmLook");
                if (sh != null && sh.isSupported) mat = new Material(sh);
            }
            if (mat == null || !enabledByUser)
            {
                Graphics.Blit(src, dst);
                return;
            }
            mat.SetFloat("_Grain", 0.035f);
            mat.SetFloat("_Seed", Random.value * 100f);
            mat.SetFloat("_CA", 0.018f);
            mat.SetFloat("_Sat", 0.8f);
            mat.SetFloat("_Contrast", 1.02f);
            mat.SetFloat("_Exposure", 1.22f * Mathf.Sqrt(Brightness));
            mat.SetColor("_ShadowTint", new Color(0.86f, 0.97f, 1.06f));
            mat.SetColor("_LightTint", new Color(1.06f, 1.0f, 0.9f));
            Graphics.Blit(src, dst, mat);
        }
    }
}
