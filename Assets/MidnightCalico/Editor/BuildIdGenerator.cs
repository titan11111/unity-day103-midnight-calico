using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;

namespace MidnightCalico.EditorTools
{
    /// <summary>
    /// スクリプトの再コンパイルごとに、今のgitコミット（短縮ハッシュ）を BuildInfo.Id へ書き込む。
    /// 画面隅に表示することで「古いビルド・古い公開ページを見ている」ことに気づけるようにする
    /// （2026-10-29 タイタンの提案: AI編集→Unity反映検証プロトコルの③に対応）。
    /// </summary>
    [InitializeOnLoad]
    public static class BuildIdGenerator
    {
        const string OutPath = "Assets/MidnightCalico/Scripts/Core/BuildInfo.cs";

        static BuildIdGenerator()
        {
            try
            {
                string hash = RunGit("rev-parse --short HEAD");
                bool dirty = !string.IsNullOrEmpty(RunGit("status --porcelain"));
                string id = string.IsNullOrEmpty(hash) ? "unknown" : hash + (dirty ? "+" : "");
                string content =
                    "// 自動生成（Assets/MidnightCalico/Editor/BuildIdGenerator.cs が書き換える）。手で編集しない。\n" +
                    "namespace MidnightCalico\n{\n" +
                    "    public static class BuildInfo\n    {\n" +
                    "        public const string Id = \"day103-" + id + "\";\n" +
                    "    }\n}\n";
                string existing = File.Exists(OutPath) ? File.ReadAllText(OutPath) : "";
                if (existing != content)
                {
                    File.WriteAllText(OutPath, content);
                    AssetDatabase.ImportAsset(OutPath);
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning("[BuildIdGenerator] gitのコミットIDを取得できませんでした: " + e.Message);
            }
        }

        static string RunGit(string args)
        {
            var psi = new ProcessStartInfo("git", args)
            {
                WorkingDirectory = Directory.GetCurrentDirectory(),
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using (var p = Process.Start(psi))
            {
                string output = p.StandardOutput.ReadToEnd().Trim();
                p.WaitForExit(2000);
                return output;
            }
        }
    }
}
