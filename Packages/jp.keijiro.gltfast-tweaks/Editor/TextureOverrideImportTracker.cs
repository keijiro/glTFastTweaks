using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace GLTFastTweaks
{
    // Bridges the asset import pipeline and the texture-override add-on:
    //  - Captures the .glb path currently being imported so the add-on (which
    //    only receives raw bytes) can resolve per-asset settings.
    //  - Registers/refreshes a settings entry for each imported glTF asset,
    //    *after* import, on the main thread.
    class TextureOverrideImportTracker : AssetPostprocessor
    {
        // Set on the import worker thread in OnPreprocessAsset and read by the
        // add-on on the same thread during the same import. (Worker and add-on
        // share a process/thread per asset, so a thread-static is sufficient;
        // it is NOT shared with the main-process postprocess below.)
        [System.ThreadStatic] static string s_CurrentGlbPath;
        public static string CurrentGlbPath => s_CurrentGlbPath;

        static bool IsGltf(string path) =>
            path.EndsWith(".glb", System.StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".gltf", System.StringComparison.OrdinalIgnoreCase);

        void OnPreprocessAsset()
        {
            s_CurrentGlbPath = IsGltf(assetPath) ? assetPath : null;
        }

        static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            var pending = new List<(string guid, string path)>();
            foreach (var path in importedAssets)
            {
                if (!IsGltf(path)) continue;
                var guid = AssetDatabase.AssetPathToGUID(path);
                if (!string.IsNullOrEmpty(guid))
                    pending.Add((guid, path));
            }

            if (pending.Count == 0) return;

            // The settings live in ProjectSettings/ (not the AssetDatabase), so
            // editing/saving them does not trigger another import; no reentrancy
            // guard is needed. delayCall keeps the edit off the import callback.
            EditorApplication.delayCall += () =>
            {
                var settings = TextureOverrideSettings.instance;
                foreach (var (guid, path) in pending)
                    settings.RegisterImport(guid, path, BuildSummary(path));
                settings.Persist();
            };
        }

        // Build a short summary from the overridden texture sub-assets (read on the
        // main thread after import).
        static string BuildSummary(string path)
        {
            var reps = AssetDatabase.LoadAllAssetRepresentationsAtPath(path);
            var lines = new List<string>();
            foreach (var rep in reps)
                if (rep is Texture2D t)
                    lines.Add($"{t.width}x{t.height} {t.format}");
            return lines.Count > 0 ? $"{lines.Count} tex: {string.Join("; ", lines)}" : null;
        }
    }
}
