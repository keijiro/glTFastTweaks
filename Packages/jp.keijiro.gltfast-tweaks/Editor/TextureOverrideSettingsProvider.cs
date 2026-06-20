using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace GLTFastTweaks
{
    // Surfaces the settings in Project Settings > glTFast Tweaks > Texture Overrides.
    // Built with UI Toolkit (activateHandler + SerializedObject binding).
    //
    // Model: every override set (the Defaults and each asset row) has a single
    // Enabled switch. A row with Enabled on applies its own settings; a row with
    // Enabled off falls back to the Defaults; the Defaults apply only when their
    // own Enabled switch is on.
    static class TextureOverrideSettingsProvider
    {
        // Column widths for the per-asset override table (shared by header + rows).
        const float kEnabled = 24, kMax = 72, kComp = 130, kFilter = 96, kBtn = 78;
        const float kRowHeight = 22;

        [SettingsProvider]
        static SettingsProvider Create()
        {
            return new SettingsProvider("Project/glTFast Tweaks", SettingsScope.Project)
            {
                label = "glTFast Tweaks",
                activateHandler = BuildUI,
                // Persist once when leaving the page. Bound edits already live in
                // the in-memory singleton; the disk write only needs to happen
                // before the session ends. (Saving inside a per-frame value
                // tracker would feed back into itself and loop.)
                deactivateHandler = () => TextureOverrideSettings.instance.Persist(),
                keywords = new[] { "glTF", "glb", "tweaks", "texture", "override", "compression", "trilinear", "downscale" }
            };
        }

        static void BuildUI(string searchContext, VisualElement root)
        {
            var so = new SerializedObject(TextureOverrideSettings.instance);

            root.style.paddingLeft = 10;
            root.style.paddingRight = 10;
            root.style.paddingTop = 8;

            // Page heading (the module that hosts the glTFast tweaks).
            var pageTitle = new Label("glTFast Tweaks");
            pageTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            pageTitle.style.fontSize = 18;
            pageTitle.style.marginBottom = 10;
            root.Add(pageTitle);

            // Feature section (one of several glTFast tweaks hosted on this page).
            var title = new Label("Texture Overrides");
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.fontSize = 14;
            title.style.marginBottom = 6;
            root.Add(title);

            var body = new VisualElement { style = { marginLeft = 6 } };
            root.Add(body);

            // Column header, then the table. Defaults is the first row inside the
            // ListView (index sentinel -1), so it renders identically to the rest.
            body.Add(BuildColumnHeader());

            var emptyHelp = new HelpBox(
                "No glTF assets imported yet. Import a .glb/.gltf to populate this list.",
                HelpBoxMessageType.Info);
            body.Add(emptyHelp);

            var list = new ListView
            {
                virtualizationMethod = CollectionVirtualizationMethod.FixedHeight,
                fixedItemHeight = kRowHeight,
                selectionType = SelectionType.None,
                showBorder = true,
                reorderable = false,
                makeItem = MakeRow,
            };
            list.bindItem = (element, i) =>
            {
                var entryIndex = (int)list.itemsSource[i];
                if (entryIndex < 0) BindDefaultsRow(so, element);
                else BindRow(so, element, entryIndex);
            };
            body.Add(list);

            var reimportAll = new Button(() => ReimportAll(so.FindProperty("entries")))
            { text = "Reimport All" };
            reimportAll.style.height = 28;
            reimportAll.style.marginTop = 8;
            reimportAll.style.alignSelf = Align.FlexEnd;
            body.Add(reimportAll);

            // Build the index-based source and size the list to its contents.
            void RefreshList()
            {
                var count = so.FindProperty("entries").arraySize;
                var source = new List<int>(count + 1) { -1 };  // -1 = Defaults row
                for (var i = 0; i < count; i++) source.Add(i);
                list.itemsSource = source;
                list.Rebuild();

                emptyHelp.style.display = count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
                list.style.height = Mathf.Clamp(source.Count, 1, 12) * kRowHeight + 6;
            }

            RefreshList();
            root.Bind(so);

            // Rebuild the list when the entry count changes (e.g. a glTF is
            // imported while this page is open). No saving here — see
            // deactivateHandler. The tracker lives on its own element: an element
            // may host only one serialized-object tracker, and root already owns a
            // binding context from Bind() above.
            var tracker = new VisualElement();
            root.Add(tracker);
            var lastCount = so.FindProperty("entries").arraySize;
            tracker.TrackSerializedObjectValue(so, o =>
            {
                var count = o.FindProperty("entries").arraySize;
                if (count == lastCount) return;
                lastCount = count;
                RefreshList();
            });
        }

        // Column titles aligned with the row layout below.
        static VisualElement BuildColumnHeader()
        {
            var header = new VisualElement { style = { flexDirection = FlexDirection.Row, marginBottom = 2 } };
            header.Add(HeaderCell("", kEnabled));
            header.Add(HeaderCell("File", 0, grow: true));
            header.Add(HeaderCell("Size", kMax));
            header.Add(HeaderCell("Compression", kComp));
            header.Add(HeaderCell("Filter", kFilter));
            header.Add(HeaderCell("", kBtn));
            return header;
        }

        static Label HeaderCell(string text, float width, bool grow = false)
        {
            var l = new Label(text);
            l.style.unityFontStyleAndWeight = FontStyle.Bold;
            l.style.fontSize = 10;
            l.style.opacity = 0.7f;
            l.style.marginRight = 4;
            if (grow) { l.style.flexGrow = 1; l.style.minWidth = 120; }
            else { l.style.width = width; l.style.flexShrink = 0; }
            return l;
        }

        // A single override row. Controls are created here (once per recycled
        // element); per-asset values are bound later in BindRow.
        static VisualElement MakeRow()
        {
            var row = new VisualElement
            { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };

            var enabled = Col(new Toggle { name = "enabled" }, kEnabled);
            // Registered once (per created element) to avoid stacking handlers on
            // ListView recycling; reads the row's current children.
            enabled.RegisterValueChangedCallback(e => SetValueColumnsEnabled(row, e.newValue));
            row.Add(enabled);

            var file = new Label { name = "file" };
            file.style.flexGrow = 1;
            file.style.minWidth = 120;
            file.style.marginRight = 4;
            file.style.whiteSpace = WhiteSpace.NoWrap;
            file.style.overflow = Overflow.Hidden;
            file.style.textOverflow = TextOverflow.Ellipsis;
            row.Add(file);

            row.Add(Col(new IntegerField { name = "max" }, kMax));
            row.Add(Col(new EnumField(Compression.NormalQuality) { name = "comp" }, kComp));
            row.Add(Col(new EnumField(FilterMode.Trilinear) { name = "filter" }, kFilter));

            var reimport = Col(new Button { text = "Reimport", name = "reimport" }, kBtn);
            // userData holds the resolved asset path, refreshed in BindRow.
            reimport.clicked += () =>
            {
                if (row.userData is string p && !string.IsNullOrEmpty(p))
                    AssetDatabase.ImportAsset(p, ImportAssetOptions.ForceUpdate);
            };
            row.Add(reimport);

            return row;
        }

        // Bind a row to the global Defaults (the table's first row).
        static void BindDefaultsRow(SerializedObject so, VisualElement row)
        {
            var defaults = so.FindProperty("defaults");

            var enabled = row.Q<Toggle>("enabled");
            enabled.BindProperty(defaults.FindPropertyRelative("enabled"));
            row.Q<IntegerField>("max").BindProperty(defaults.FindPropertyRelative("maxSize"));
            row.Q<EnumField>("comp").BindProperty(defaults.FindPropertyRelative("compression"));
            row.Q<EnumField>("filter").BindProperty(defaults.FindPropertyRelative("filterMode"));

            var file = row.Q<Label>("file");
            file.text = "(Defaults)";
            file.style.unityFontStyleAndWeight = FontStyle.Normal;
            file.tooltip = "Applied to every asset whose switch is off.";

            // The Defaults row has no single asset to reimport.
            row.Q<Button>("reimport").style.visibility = Visibility.Hidden;

            SetValueColumnsEnabled(row, enabled.value);
        }

        static void BindRow(SerializedObject so, VisualElement row, int index)
        {
            var entry = so.FindProperty("entries").GetArrayElementAtIndex(index);
            var overrides = entry.FindPropertyRelative("overrides");

            var enabled = row.Q<Toggle>("enabled");
            enabled.BindProperty(overrides.FindPropertyRelative("enabled"));
            row.Q<IntegerField>("max").BindProperty(overrides.FindPropertyRelative("maxSize"));
            row.Q<EnumField>("comp").BindProperty(overrides.FindPropertyRelative("compression"));
            row.Q<EnumField>("filter").BindProperty(overrides.FindPropertyRelative("filterMode"));

            var path = GetEntryPath(entry);
            var summary = entry.FindPropertyRelative("lastSummary").stringValue;
            var file = row.Q<Label>("file");
            file.style.unityFontStyleAndWeight = FontStyle.Normal;   // reset (row recycled from Defaults)
            file.text = string.IsNullOrEmpty(path) ? "(missing)" : Path.GetFileName(path);
            file.tooltip = string.IsNullOrEmpty(summary) ? path : path + "\n" + summary;

            row.userData = path;
            var reimport = row.Q<Button>("reimport");
            reimport.style.visibility = Visibility.Visible;          // reset (row recycled from Defaults)
            reimport.SetEnabled(!string.IsNullOrEmpty(path));

            // Binding may set the toggle silently, so sync the column state here.
            SetValueColumnsEnabled(row, enabled.value);
        }

        // Grey out a row's value columns when its Enabled switch is off (the row
        // then follows the Defaults).
        static void SetValueColumnsEnabled(VisualElement row, bool enabled)
        {
            row.Q<IntegerField>("max").SetEnabled(enabled);
            row.Q<EnumField>("comp").SetEnabled(enabled);
            row.Q<EnumField>("filter").SetEnabled(enabled);
        }

        static T Col<T>(T element, float width) where T : VisualElement
        {
            element.style.width = width;
            element.style.marginRight = 4;
            element.style.flexShrink = 0;
            return element;
        }

        // Resolve the live asset path from the stored GUID (rename-safe),
        // falling back to the cached path.
        static string GetEntryPath(SerializedProperty entry)
        {
            var guid = entry.FindPropertyRelative("glbGuid").stringValue;
            var path = string.IsNullOrEmpty(guid) ? null : AssetDatabase.GUIDToAssetPath(guid);
            return string.IsNullOrEmpty(path) ? entry.FindPropertyRelative("glbPath").stringValue : path;
        }

        static void Reimport(SerializedProperty entry)
        {
            var path = GetEntryPath(entry);
            if (!string.IsNullOrEmpty(path))
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }

        static void ReimportAll(SerializedProperty entries)
        {
            for (var i = 0; i < entries.arraySize; i++)
                Reimport(entries.GetArrayElementAtIndex(i));
        }
    }
}
