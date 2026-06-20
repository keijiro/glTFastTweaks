using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace GLTFastTweaks
{
    // GPU compression level. Mirrors Unity's TextureImporterCompression levels
    // but with the labels requested for this tool. Maps to a desktop BC format
    // and compression quality in TextureOverrideAddon.
    //   None          -> uncompressed (RGBA32)
    //   LowQuality    -> DXT1/DXT5, Fast quality
    //   NormalQuality -> DXT1/DXT5, Normal quality
    //   HighQuality   -> BC7, Best quality
    enum Compression { None, LowQuality, NormalQuality, HighQuality }

    // A set of texture override options. Used both as the global default and as a
    // per-asset override. 'enabled' is the on/off switch for the whole set: when
    // off, the owner falls back to the Defaults (and the Defaults, when off, apply
    // nothing).
    [Serializable]
    struct TextureOverride
    {
        public bool enabled;
        public int maxSize;                          // longest-edge clamp; 0 = no downscale
        public Compression compression;
        public FilterMode filterMode;                // texture filtering mode

        // Built-in fallback used when no settings asset exists.
        public static TextureOverride Default => new TextureOverride
        {
            enabled = true,
            maxSize = 1024,
            compression = Compression.NormalQuality,
            filterMode = FilterMode.Trilinear
        };
    }

    // One auto-collected entry per imported glTF asset, keyed by GUID.
    [Serializable]
    class Entry
    {
        public string glbGuid;
        public string glbPath;       // cached for display; resolved from GUID on use
        public TextureOverride overrides;   // overrides.enabled is the per-row switch
        public string lastSummary;   // informational, e.g. "3 tex -> DXT1 @1024"
    }

    // Global, editor-only settings stored under ProjectSettings/ and surfaced
    // in the Project Settings window (see TextureOverrideSettingsProvider).
    [FilePath("ProjectSettings/glTFastTweaksSettings.asset", FilePathAttribute.Location.ProjectFolder)]
    class TextureOverrideSettings : ScriptableSingleton<TextureOverrideSettings>
    {
        [SerializeField] TextureOverride defaults = TextureOverride.Default;
        [SerializeField] List<Entry> entries = new List<Entry>();

        public TextureOverride Defaults { get => defaults; set => defaults = value; }
        public List<Entry> Entries => entries;

        // Persist to the ProjectSettings/ file.
        public void Persist() => Save(true);

        // Effective override for a given glTF asset GUID. A row applies its own
        // settings only when its switch (overrides.enabled) is on; otherwise it
        // falls back to the Defaults.
        public TextureOverride Resolve(string guid)
        {
            var entry = FindEntry(guid);
            return entry != null && entry.overrides.enabled ? entry.overrides : defaults;
        }

        // Adds or refreshes the entry for an imported asset. Must be called
        // outside of import (via EditorApplication.delayCall).
        public void RegisterImport(string guid, string path, string summary)
        {
            var entry = FindEntry(guid);
            if (entry == null)
            {
                // Pre-fill with the current defaults, but switched off so the new
                // asset follows the Defaults until the user turns its switch on.
                var overrides = defaults;
                overrides.enabled = false;
                entry = new Entry { glbGuid = guid, overrides = overrides };
                entries.Add(entry);
            }
            entry.glbPath = path;
            entry.lastSummary = summary;
        }

        Entry FindEntry(string guid)
        {
            if (string.IsNullOrEmpty(guid)) return null;
            foreach (var e in entries)
                if (e.glbGuid == guid) return e;
            return null;
        }
    }
}
