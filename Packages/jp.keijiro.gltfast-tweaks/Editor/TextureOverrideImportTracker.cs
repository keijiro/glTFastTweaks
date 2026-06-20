using UnityEditor;

namespace GLTFastTweaks
{
    // Captures the .glb/.gltf path currently being imported so the add-on (which
    // only receives raw image bytes) can resolve the per-asset override. The entry
    // list itself is built by TextureOverrideSettings.Scan(), not by imports.
    class TextureOverrideImportTracker : AssetPostprocessor
    {
        // Set on the import worker thread in OnPreprocessAsset and read by the
        // add-on on the same thread during the same import. (Worker and add-on
        // share a process/thread per asset, so a thread-static is sufficient.)
        [System.ThreadStatic] static string s_CurrentGlbPath;
        public static string CurrentGlbPath => s_CurrentGlbPath;

        void OnPreprocessAsset()
        {
            s_CurrentGlbPath = TextureOverrideSettings.IsGltf(assetPath) ? assetPath : null;
        }
    }
}
