using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace MidnightCalico.EditorTools
{
    /// <summary>
    /// プロジェクトを開いたら自動でシーンを作り、ビルド設定・WebGL設定まで済ませる。
    /// メニュー「MidnightCalico」からやり直し・WebGLビルドもできる。
    /// </summary>
    [InitializeOnLoad]
    public static class MidnightCalicoSetup
    {
        const string ScenePath = "Assets/MidnightCalico/Scenes/MidnightCalico.unity";
        const string RefDir = "Assets/MidnightCalico/Resources/ShaderRefs";

        static MidnightCalicoSetup()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                if (File.Exists(ScenePath)) return;
                if (EditorSceneManager.GetActiveScene().isDirty)
                {
                    Debug.Log("[MidnightCalico] 未保存のシーンがあるため自動セットアップを保留しました。メニュー『MidnightCalico > ① セットアップ』を実行してください。");
                    return;
                }
                Setup(false);
            };
        }

        [MenuItem("MidnightCalico/① セットアップ（シーン作成・ビルド設定）", false, 1)]
        public static void SetupMenu() { Setup(true); }

        [MenuItem("MidnightCalico/② シーンを開いて再生", false, 2)]
        public static void OpenAndPlay()
        {
            Setup(true);
            EditorApplication.isPlaying = true;
        }

        [MenuItem("MidnightCalico/③ WebGLビルド（Builds/WebGL へ出力）", false, 20)]
        public static void BuildWebGL()
        {
            Setup(true);
            string outDir = "Builds/WebGL";
            var opts = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = outDir,
                target = BuildTarget.WebGL,
                options = BuildOptions.None
            };
            var report = BuildPipeline.BuildPlayer(opts);
            if (report.summary.result == BuildResult.Succeeded)
            {
                File.WriteAllText(Path.Combine(outDir, ".nojekyll"), "");
                PatchIndexForIPhone(Path.Combine(outDir, "index.html"));
                Debug.Log("[MidnightCalico] WebGLビルド完了: " + Path.GetFullPath(outDir) + "  （中身を GitHub リポジトリの直下に置けば GitHub Pages で遊べます）");
                EditorUtility.RevealInFinder(Path.Combine(outDir, "index.html"));
            }
            else Debug.LogError("[MidnightCalico] WebGLビルド失敗: " + report.summary.result);
        }

        [MenuItem("MidnightCalico/④ iOSビルド（Xcodeプロジェクトを Builds/iOS へ出力）", false, 21)]
        public static void BuildIOS()
        {
            Setup(true);
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.iOS, BuildTarget.iOS))
            {
                EditorUtility.DisplayDialog("MidnightCalico", "iOS Build Support モジュールが入っていません。Unity Hub からこのUnityに追加してください。\n（GitHub Pages で iPhone の Safari から遊ぶだけなら ③ WebGLビルドで OK）", "OK");
                return;
            }
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = "Builds/iOS",
                target = BuildTarget.iOS,
                options = BuildOptions.None
            });
            if (report.summary.result == BuildResult.Succeeded)
            {
                Debug.Log("[MidnightCalico] iOSビルド完了。Builds/iOS の Unity-iPhone.xcodeproj を Xcode で開いて実機へ。");
                EditorUtility.RevealInFinder("Builds/iOS");
            }
            else Debug.LogError("[MidnightCalico] iOSビルド失敗: " + report.summary.result);
        }

        /// <summary>
        /// WebGL の index.html を iPhone の Safari 向けに調整する：
        /// 全画面・ズーム禁止・長押しメニュー禁止・「モバイル非対応」バナーを消す・縦持ちなら「横にしてね」
        /// </summary>
        public static void PatchIndexForIPhone(string indexPath)
        {
            if (!File.Exists(indexPath)) return;
            string html = File.ReadAllText(indexPath);
            if (html.Contains("midnight-calico-ios")) return;
            const string head =
                "<meta name=\"viewport\" content=\"width=device-width, height=device-height, initial-scale=1.0, maximum-scale=1.0, user-scalable=no, viewport-fit=cover\">\n" +
                "<meta name=\"apple-mobile-web-app-capable\" content=\"yes\">\n" +
                "<meta name=\"mobile-web-app-capable\" content=\"yes\">\n" +
                "<meta name=\"apple-mobile-web-app-status-bar-style\" content=\"black-translucent\">\n" +
                "<style id=\"midnight-calico-ios\">\n" +
                "html,body{margin:0;padding:0;width:100%;height:100%;background:#000;overflow:hidden;overscroll-behavior:none;touch-action:none;-webkit-user-select:none;user-select:none;-webkit-touch-callout:none;-webkit-tap-highlight-color:transparent}\n" +
                "#unity-container{position:fixed!important;left:0!important;top:0!important;width:100vw!important;height:100vh!important;height:100dvh!important;transform:none!important}\n" +
                "#unity-canvas{width:100vw!important;height:100vh!important;height:100dvh!important;background:#000}\n" +
                "#unity-footer,#unity-warning{display:none!important}\n" +
                "#mc-rotate{display:none;position:fixed;inset:0;z-index:99;background:#000;color:#d8cfb8;font:20px sans-serif;align-items:center;justify-content:center;text-align:center;line-height:1.8}\n" +
                "@media (orientation:portrait){#mc-rotate{display:flex}}\n" +
                "</style>\n";
            html = Regex.Replace(html, "<meta name=\"viewport\"[^>]*>", "");
            html = Regex.Replace(html, "</head>", head + "</head>", RegexOptions.IgnoreCase);
            html = Regex.Replace(html, "<body([^>]*)>", "<body$1>\n<div id=\"mc-rotate\">📱 iPhoneを横向きにしてね<br>深夜二時のミケ</div>", RegexOptions.IgnoreCase);
            // 「WebGL はモバイル非対応」バナーを出さない
            html = Regex.Replace(html, "unityShowBanner\\('WebGL builds are not supported on mobile devices\\.'\\);", "");
            // Retina でぼやけすぎないように（重くならない範囲で 2 倍まで）
            html = html.Replace("config.devicePixelRatio = 1;", "config.devicePixelRatio = Math.min(window.devicePixelRatio || 1, 2);");
            const string tail =
                "<script>\n" +
                "document.addEventListener('gesturestart',function(e){e.preventDefault();});\n" +
                "document.addEventListener('touchmove',function(e){e.preventDefault();},{passive:false});\n" +
                "document.addEventListener('contextmenu',function(e){e.preventDefault();});\n" +
                "</script>\n";
            html = Regex.Replace(html, "</body>", tail + "</body>", RegexOptions.IgnoreCase);
            File.WriteAllText(indexPath, html);
        }

        static void Setup(bool interactive)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));

            CreateShaderReferenceMaterials();

            if (!File.Exists(ScenePath))
            {
                if (interactive && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var go = new GameObject("MidnightCalico");
                go.AddComponent<GameManager>();
                // フォグの設定をシーンに保存（ビルド時にフォグのシェーダーが削られないように）
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.Exponential;
                RenderSettings.fogDensity = 0.03f;
                RenderSettings.fogColor = new Color(0.01f, 0.012f, 0.02f);
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(0.075f, 0.08f, 0.10f);
                RenderSettings.skybox = null;
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene, ScenePath);
                AssetDatabase.Refresh();
            }
            else if (EditorSceneManager.GetActiveScene().path != ScenePath)
            {
                if (!interactive || EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            // ビルド設定の先頭に登録
            var list = EditorBuildSettings.scenes.ToList();
            list.RemoveAll(s => s.path == ScenePath);
            list.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = list.ToArray();

            // WebGL：GitHub Pages で確実に動くよう圧縮なし
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
            PlayerSettings.productName = "深夜二時のミケ";
            PlayerSettings.runInBackground = true;
            // iPhone：横画面のみ・ステータスバー非表示
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.statusBarHidden = true;
#if UNITY_2021_2_OR_NEWER
            if (PlayerSettings.GetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.iOS).StartsWith("com.DefaultCompany"))
                PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.iOS, "com.titan.midnightcalico");
#endif
            AssetDatabase.SaveAssets();
            Debug.Log("[MidnightCalico] セットアップ完了。▶ 再生ボタンで遊べます。");
        }

        /// <summary>
        /// 実行時に Shader.Find で使うシェーダーがビルドから削られないよう、
        /// 参照用マテリアルを Resources に置いておく（発光あり・なし）。
        /// </summary>
        static void CreateShaderReferenceMaterials()
        {
            Directory.CreateDirectory(RefDir);
            Shader lit = null;
            if (GraphicsSettings.currentRenderPipeline != null) lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null) lit = Shader.Find("Standard");
            if (lit == null) return;
            MakeRef("ref_lit", lit, false, false);
            MakeRef("ref_lit_tex", lit, true, false);
            MakeRef("ref_lit_emission", lit, false, true);
            MakeRef("ref_lit_tex_emission", lit, true, true);
            MakeRef("ref_lit_normal", lit, true, false, true);
            // 半透明（接地影・ほこり）とフィルム調エフェクト
            var sprite = Shader.Find("Sprites/Default");
            if (sprite != null) MakeRef("ref_sprite", sprite, true, false);
            var film = Shader.Find("Hidden/MidnightCalico/FilmLook");
            if (film != null) MakeRef("ref_film", film, false, false);
        }

        static void MakeRef(string name, Shader shader, bool tex, bool emission, bool normal = false)
        {
            string path = RefDir + "/" + name + "_" + shader.name.Replace("/", "_").Replace(" ", "") + ".mat";
            if (File.Exists(path)) return;
            var m = new Material(shader);
            if (tex) m.mainTexture = Texture2D.whiteTexture;
            if (normal)
            {
                m.SetTexture("_BumpMap", Texture2D.normalTexture);
                m.EnableKeyword("_NORMALMAP");
            }
            if (emission)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", Color.white);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            AssetDatabase.CreateAsset(m, path);
        }
    }
}
