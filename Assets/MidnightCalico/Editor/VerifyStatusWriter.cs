using System;
using System.IO;
using UnityEditor;
using UnityEditor.Compilation;

namespace MidnightCalico.EditorTools
{
    /// <summary>
    /// コンパイルが終わるたびに、プロジェクトパス・変更ID・確認日時・コンパイル結果を
    /// プロジェクト直下の verify-status.json（Assets外、gitignore対象）へ書き出す。
    /// 「保存コマンドが成功した」と「Unityに反映・コンパイル成功した」を別に確認するための出力先
    /// （2026-10-29 タイタンの指示: AI編集→Unity反映検証プロトコルの③）。
    /// 失敗・未更新をこの仕組み自身が成功扱いしないよう、常に「今の状態」を正直に書く
    /// （コンパイル失敗時も必ず書く。この処理自体が例外で落ちた場合はファイルを更新しない＝
    /// 読む側は「古いままのファイル」から未確認と判定できる）。
    /// </summary>
    [InitializeOnLoad]
    public static class VerifyStatusWriter
    {
        static readonly string OutPath = Path.Combine(Directory.GetCurrentDirectory(), "verify-status.json");

        static VerifyStatusWriter()
        {
            CompilationPipeline.compilationFinished -= OnCompilationFinished;
            CompilationPipeline.compilationFinished += OnCompilationFinished;
        }

        static void OnCompilationFinished(object context)
        {
            try
            {
                bool failed = EditorUtility.scriptCompilationFailed;
                string json =
                    "{\n" +
                    "  \"projectPath\": " + Json(Directory.GetCurrentDirectory()) + ",\n" +
                    "  \"buildId\": " + Json(BuildInfo.Id) + ",\n" +
                    "  \"checkedAtUtc\": " + Json(DateTime.UtcNow.ToString("o")) + ",\n" +
                    "  \"compileResult\": " + Json(failed ? "failed" : "success") + "\n" +
                    "}\n";
                File.WriteAllText(OutPath, json);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning("[VerifyStatusWriter] verify-status.jsonの書き出しに失敗しました（この失敗自体はファイルに書かない。古いファイルが残る）: " + e.Message);
            }
        }

        static string Json(string s)
        {
            return "\"" + (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }
    }
}
