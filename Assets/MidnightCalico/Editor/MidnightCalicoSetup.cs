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
            EditorApplication.isPaused = false;
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
        /// WebGL の index.html をスマホ横画面向けに整える（GitHub Pages 公開用）：
        /// ・Unityの読み込みロゴを消し、起動時の「Made with Unity」表示が終わるまで黒幕（タイトル）で隠す
        /// ・キャンバスを visualViewport からノッチ/ホームバー（safe-area）を引いた範囲に収め、操作ボタンの見切れを防ぐ
        /// ・ズーム禁止・長押しメニュー禁止・「モバイル非対応」バナーを消す・縦持ちなら「横にしてね」
        /// 何度実行しても同じ結果になる（旧版の注入は置き換える）。
        /// </summary>
        const string MobileHead = @"<meta name=""viewport"" content=""width=device-width, initial-scale=1.0, maximum-scale=1.0, user-scalable=no, viewport-fit=cover"">
<meta name=""apple-mobile-web-app-capable"" content=""yes"">
<meta name=""mobile-web-app-capable"" content=""yes"">
<meta name=""apple-mobile-web-app-status-bar-style"" content=""black-translucent"">
<style id=""midnight-calico-ios-v2"">
html,body{margin:0;padding:0;width:100%;height:100%;background:#000;overflow:hidden;overscroll-behavior:none;touch-action:none;-webkit-user-select:none;user-select:none;-webkit-touch-callout:none;-webkit-tap-highlight-color:transparent}
#unity-container{position:fixed!important;left:0;top:0;width:100vw;height:100vh;transform:none!important;background:#000}
#unity-canvas{display:block;width:100%!important;height:100%!important;background:#000}
#unity-footer,#unity-warning,#unity-loading-bar,#unity-logo{display:none!important}
#mc-safe-probe{position:fixed;left:0;top:0;width:0;height:0;visibility:hidden;pointer-events:none;padding:env(safe-area-inset-top) env(safe-area-inset-right) env(safe-area-inset-bottom) env(safe-area-inset-left)}
#mc-cover{position:fixed;inset:0;z-index:50;background:#000;color:#d8cfb8;display:flex;flex-direction:column;align-items:center;justify-content:center;font:24px serif;letter-spacing:.25em;transition:opacity .6s}
#mc-cover small{margin-top:14px;font:14px sans-serif;letter-spacing:.1em;opacity:.7}
#mc-cover.mc-hide{opacity:0;pointer-events:none}
#mc-rotate{display:none;position:fixed;inset:0;z-index:99;background:#000;color:#d8cfb8;font:20px sans-serif;align-items:center;justify-content:center;text-align:center;line-height:1.8}
@media (orientation:portrait) and (pointer:coarse){#mc-rotate{display:flex}}
</style>
";
        const string MobileBody = @"<div id=""mc-rotate"">📱 スマホを横向きにしてね<br>深夜二時のミケ</div>
<div id=""mc-cover"">深夜二時のミケ<small id=""mc-cover-text"">読み込み中…</small></div>
<div id=""mc-safe-probe""></div>";
        const string MobileTail = @"<script>
(function(){
  var touch=/iPhone|iPad|iPod|Android/i.test(navigator.userAgent)||(navigator.maxTouchPoints>1&&/Macintosh/.test(navigator.userAgent));
  function px(v){return parseFloat(v)||0;}
  function fit(){
    var c=document.getElementById('unity-container'),p=document.getElementById('mc-safe-probe');
    if(!c||!p)return;
    var vv=window.visualViewport,W=vv?vv.width:window.innerWidth,H=vv?vv.height:window.innerHeight;
    var cs=getComputedStyle(p),t=px(cs.paddingTop),r=px(cs.paddingRight),b=px(cs.paddingBottom),l=px(cs.paddingLeft);
    if(touch){l=Math.max(l,8);r=Math.max(r,8);t=Math.max(t,4);b=Math.max(b,6);}
    var s=c.style;
    s.setProperty('left',l+'px','important');s.setProperty('top',t+'px','important');
    s.setProperty('width',Math.max(1,Math.floor(W-l-r))+'px','important');
    s.setProperty('height',Math.max(1,Math.floor(H-t-b))+'px','important');
    window.scrollTo(0,0);
  }
  window.mcFit=fit;
  window.mcProgress=function(v){var e=document.getElementById('mc-cover-text');if(e)e.textContent='読み込み中… '+Math.round(v*100)+'%';};
  // 起動直後のUnityロゴ表示が終わるまで黒幕で隠し、タイトル画面から見せる
  window.mcReady=function(){fit();var e=document.getElementById('mc-cover-text');if(e)e.textContent='まもなく開始';setTimeout(function(){var c=document.getElementById('mc-cover');if(c)c.className='mc-hide';},3800);};
  window.addEventListener('resize',fit);
  window.addEventListener('orientationchange',function(){fit();setTimeout(fit,250);setTimeout(fit,800);});
  if(window.visualViewport){visualViewport.addEventListener('resize',fit);visualViewport.addEventListener('scroll',fit);}
  fit();
})();
document.addEventListener('gesturestart',function(e){e.preventDefault();});
document.addEventListener('touchmove',function(e){e.preventDefault();},{passive:false});
document.addEventListener('contextmenu',function(e){e.preventDefault();});
</script>
";

        public static void PatchIndexForIPhone(string indexPath)
        {
            if (!File.Exists(indexPath)) return;
            string html = File.ReadAllText(indexPath).Replace("\r\n", "\n");
            // 旧版の注入を除去
            html = Regex.Replace(html, "<meta name=\"(apple-mobile-web-app-capable|mobile-web-app-capable|apple-mobile-web-app-status-bar-style)\"[^>]*>\n?", "");
            html = Regex.Replace(html, "<style id=\"midnight-calico-ios[^\"]*\">[\\s\\S]*?</style>\n?", "");
            html = Regex.Replace(html, "<div id=\"mc-rotate\">[\\s\\S]*?</div>\n?", "");
            html = Regex.Replace(html, "<div id=\"mc-cover\">[\\s\\S]*?</div>\n?", "");
            html = Regex.Replace(html, "<div id=\"mc-safe-probe\"></div>\n?", "");
            html = Regex.Replace(html, "<script>\n(\\(function\\(\\)\\{\n  var touch[\\s\\S]*?\\}\\)\\(\\);\n)?document\\.addEventListener\\('gesturestart'[\\s\\S]*?</script>\n?", "");
            // 新しい注入
            html = Regex.Replace(html, "\\s*<meta name=\"viewport\"[^>]*>", "");
            html = new Regex("</head>", RegexOptions.IgnoreCase).Replace(html, MobileHead + "</head>", 1);
            html = new Regex("<body([^>]*)>", RegexOptions.IgnoreCase).Replace(html, m => "<body" + m.Groups[1].Value + ">\n" + MobileBody, 1);
            // テンプレートがモバイル時に viewport を上書きする処理を除去（viewport-fit=cover が消えるため）
            html = Regex.Replace(html, "var meta = document\\.createElement\\('meta'\\);[\\s\\S]*?appendChild\\(meta\\);", "");
            html = html.Replace("unityShowBanner('WebGL builds are not supported on mobile devices.');", "");
            html = html.Replace("config.devicePixelRatio = 1;", "config.devicePixelRatio = Math.min(window.devicePixelRatio || 1, 2);");
            const string pb = "document.querySelector(\"#unity-progress-bar-full\").style.width = 100 * progress + \"%\";";
            if (html.Contains(pb) && !html.Contains("mcProgress(progress)")) html = html.Replace(pb, pb + " if (window.mcProgress) mcProgress(progress);");
            const string lb = "document.querySelector(\"#unity-loading-bar\").style.display = \"none\";";
            if (html.Contains(lb) && !html.Contains("mcReady()")) html = html.Replace(lb, lb + " if (window.mcReady) mcReady();");
            html = new Regex("</body>", RegexOptions.IgnoreCase).Replace(html, MobileTail + "</body>", 1);
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
            // GitHub Pages側のContent-Encoding設定に依存せず、Unity標準ローダーでgzipを展開する。
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.productName = "深夜二時のミケ";
            PlayerSettings.runInBackground = true;
            // iPhone：横画面のみ・ステータスバー非表示
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.statusBarHidden = true;
            // スマホで横にした時などに出る「Made with Unity」を出さない（Unity 6 は Personal でも無効化可）
            PlayerSettings.SplashScreen.show = false;
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
