using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;

namespace MidnightCalico.EditorTools
{
    /// <summary>AssetsとPackagesの内容から変更IDを生成。未コミットの調整も識別し、生成ファイル自身はハッシュから除く。</summary>
    [InitializeOnLoad]
    public static class BuildIdGenerator
    {
        const string OutPath = "Assets/MidnightCalico/Scripts/Core/BuildInfo.cs";
        static BuildIdGenerator()
        {
            try
            {
                string id;
                using (var hash = SHA256.Create())
                {
                    var files = new[] { "Assets", "Packages" }.Where(Directory.Exists)
                        .SelectMany(root => Directory.GetFiles(root, "*", SearchOption.AllDirectories))
                        .Select(path => path.Replace('\\', '/'))
                        .Where(path => path != OutPath && !Path.GetFileName(path).StartsWith("."))
                        .OrderBy(path => path, StringComparer.Ordinal);
                    foreach (string path in files)
                    {
                        byte[] name = Encoding.UTF8.GetBytes(path + "\0");
                        hash.TransformBlock(name, 0, name.Length, name, 0);
                        byte[] bytes = File.ReadAllBytes(path);
                        hash.TransformBlock(bytes, 0, bytes.Length, bytes, 0);
                    }
                    hash.TransformFinalBlock(new byte[0], 0, 0);
                    id = BitConverter.ToString(hash.Hash).Replace("-", "").ToLowerInvariant().Substring(0, 12);
                }
                string content = "// 自動生成。Assets・Packagesの内容ID（生成ファイル自身を除く）。\n" +
                    "namespace MidnightCalico\n{\n    public static class BuildInfo\n    {\n" +
                    "        public const string Id = \"day103-" + id + "\";\n    }\n}\n";
                if (!File.Exists(OutPath) || File.ReadAllText(OutPath) != content)
                {
                    File.WriteAllText(OutPath, content);
                    AssetDatabase.ImportAsset(OutPath);
                }
            }
            catch (Exception e) { UnityEngine.Debug.LogWarning("[BuildIdGenerator] 変更IDを更新できませんでした: " + e.Message); }
        }
    }
}
