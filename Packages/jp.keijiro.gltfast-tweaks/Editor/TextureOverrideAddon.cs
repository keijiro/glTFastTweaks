using System;
using System.Threading;
using System.Threading.Tasks;
using GLTFast;
using GLTFast.Addons;
using GLTFast.Schema;
using Unity.Collections;
using UnityEditor;
using UnityEngine;

namespace GLTFastTweaks
{
    // Intercepts embedded glTF PNG/JPEG textures on import and rewrites them
    // (downscale + GPU compression) according to the per-asset settings
    // resolved from TextureOverrideSettings.
    static class TextureOverrideRegistration
    {
        [InitializeOnLoadMethod]
        static void Register()
        {
            ImportAddonRegistry.RegisterImportAddon(new TextureOverrideAddon());
        }
    }

    class TextureOverrideAddon : ImportAddon<TextureOverrideAddonInstance> { }

    class TextureOverrideAddonInstance : ImportAddonInstance, ITextureImageLoader
    {
        // --- ImportAddonInstance -------------------------------------------
        public override bool SupportsGltfExtension(string extensionName) => false;

        public override void Inject(GltfImportBase gltfImport) => gltfImport.AddImportAddonInstance(this);

        public override void Inject(IInstantiator instantiator) { }

        public override void Dispose() { }

        // --- ITextureImageLoader -------------------------------------------

        // We do not add support for any glTF texture extension.
        public bool IsAbleToLoad(TextureBase texture, out int imageIndex)
        {
            imageIndex = -1;
            return false;
        }

        // Content-based detection: this is the only hook reached for textures
        // embedded in a .glb (buffer view, no URI).
        public bool IsAbleToLoad(ReadOnlySpan<byte> data) => ImageFormatDetection.IsPngOrJpeg(data);

        public Task<ImageResult> LoadImage(
            NativeArray<byte>.ReadOnly data,
            bool linear,
            bool readable,
            bool generateMipMaps,
            CancellationToken cancellationToken)
        {
            var profile = ResolveProfile();

            // Mipmaps are left to glTFast's request (no interference).
            var wantMips = generateMipMaps;

            var bytes = data.ToArray();
            var hasAlpha = ImageFormatDetection.IsPng(bytes) && !linear; // JPEG never has alpha; linear maps drop alpha

            // 1. Decode at full resolution into a temporary readable texture.
            var src = new Texture2D(2, 2, TextureFormat.RGBA32, false, linear);
            if (!src.LoadImage(bytes, false))
            {
                UnityEngine.Object.DestroyImmediate(src);
                return Task.FromResult(ImageResult.Null);
            }

            int sw = src.width, sh = src.height;
            var longest = Mathf.Max(sw, sh);

            var apply = profile.enabled;
            var doDownscale = apply && profile.maxSize > 0 && longest > profile.maxSize;
            var doCompress = apply && profile.compression != Compression.None;

            // 2. Target size (rounded to a multiple of 4 when compressing).
            int tw = sw, th = sh;
            if (doDownscale)
            {
                var scale = profile.maxSize / (float)longest;
                tw = Mathf.RoundToInt(sw * scale);
                th = Mathf.RoundToInt(sh * scale);
            }
            if (doCompress)
            {
                tw = RoundToMultipleOf4(tw);
                th = RoundToMultipleOf4(th);
            }

            // 3. Resample through a RenderTexture (matching color space).
            //    Skip the blit only when nothing about the texture changes.
            Texture2D dst;
            if (tw == sw && th == sh && !wantMips)
            {
                dst = src;
            }
            else
            {
                var rt = RenderTexture.GetTemporary(
                    tw, th, 0, RenderTextureFormat.ARGB32,
                    linear ? RenderTextureReadWrite.Linear : RenderTextureReadWrite.sRGB);
                var prev = RenderTexture.active;
                Graphics.Blit(src, rt);
                RenderTexture.active = rt;

                dst = new Texture2D(tw, th, TextureFormat.RGBA32, wantMips, linear);
                dst.ReadPixels(new Rect(0, 0, tw, th), 0, 0);
                dst.Apply(wantMips, false);

                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);
                UnityEngine.Object.DestroyImmediate(src);
            }

            // 4. GPU-compress (editor). Format and quality are derived from the
            //    standard TextureImporterCompression level.
            string formatLabel;
            if (doCompress)
            {
                var format = SelectFormat(profile.compression, hasAlpha);
                EditorUtility.CompressTexture(dst, format, SelectQuality(profile.compression));
                formatLabel = format.ToString();
            }
            else
            {
                formatLabel = "RGBA32";
            }
            dst.Apply(false, !readable);

            // Filtering. glTFast only applies the glTF sampler to "variant"
            // textures (an image used by multiple samplers); for the common
            // single-sampler case it leaves filterMode untouched, so setting it
            // here survives.
            if (apply)
                dst.filterMode = profile.filterMode;

            Debug.Log($"[glTFastTweaks] {sw}x{sh}->{tw}x{th} {formatLabel} (linear={linear}, mips={wantMips}, filter={(apply ? profile.filterMode.ToString() : "untouched")})");

            return Task.FromResult(new ImageResult(dst));
        }

        static TextureOverride ResolveProfile()
        {
            // AssetDatabase access during import can be restricted depending on
            // the import worker context; degrade gracefully to built-in defaults.
            try
            {
                var guid = AssetDatabase.AssetPathToGUID(TextureOverrideImportTracker.CurrentGlbPath ?? string.Empty);
                return TextureOverrideSettings.instance.Resolve(guid);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[glTFastTweaks] Falling back to default override: {e.Message}");
                return TextureOverride.Default;
            }
        }

        // Map the compression level to a desktop BC format. High quality uses
        // BC7; the others use DXT1/DXT5 depending on whether alpha is present.
        static TextureFormat SelectFormat(Compression compression, bool hasAlpha) => compression switch
        {
            Compression.HighQuality => TextureFormat.BC7,
            _ => hasAlpha ? TextureFormat.DXT5 : TextureFormat.DXT1
        };

        static TextureCompressionQuality SelectQuality(Compression compression) => compression switch
        {
            Compression.LowQuality => TextureCompressionQuality.Fast,
            Compression.HighQuality => TextureCompressionQuality.Best,
            _ => TextureCompressionQuality.Normal
        };

        static int RoundToMultipleOf4(int v) => Mathf.Max(4, (v + 2) / 4 * 4);
    }
}
