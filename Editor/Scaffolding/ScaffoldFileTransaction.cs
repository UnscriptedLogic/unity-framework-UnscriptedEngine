using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;

namespace UnscriptedEngine.Editor.Scaffolding
{
    /// <summary>Restore overwritten content and metadata if an asset-generation attempt fails.</summary>
    internal sealed class ScaffoldFileTransaction : IDisposable
    {
        private readonly Dictionary<string, byte[]> originals = new Dictionary<string, byte[]>();
        private bool committed;
        private bool restored;

        public void Capture(ScaffoldPlan plan, string path)
        {
            if (originals.ContainsKey(path)) return;
            if (Directory.Exists(path)) throw new IOException("A folder occupies output path: " + path);
            if ((File.Exists(path) || File.Exists(path + ".meta")) && !plan.ShouldReplace(path))
                throw new IOException("An output appeared after confirmation. Retry generation: " + path);
            originals.Add(path, File.Exists(path) ? File.ReadAllBytes(path) : null);
            originals.Add(path + ".meta", File.Exists(path + ".meta") ? File.ReadAllBytes(path + ".meta") : null);
        }

        public void Commit() { committed = true; }

        public void Dispose()
        {
            if (committed || restored) return;
            restored = true;
            foreach (var entry in originals)
            {
                if (entry.Value == null) File.Delete(entry.Key);
                else File.WriteAllBytes(entry.Key, entry.Value);
            }
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            foreach (var entry in originals)
                if (entry.Value != null && !entry.Key.EndsWith(".meta", StringComparison.Ordinal))
                    AssetDatabase.ImportAsset(entry.Key, ImportAssetOptions.ForceUpdate);
        }
    }
}
