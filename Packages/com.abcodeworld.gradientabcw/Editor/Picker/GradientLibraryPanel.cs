using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace ABCodeworld.Gradients.Editor
{
    /// <summary>Browse, save, load, overwrite and delete <see cref="GradientABCWAsset"/> gradients in a folder.</summary>
    internal sealed class GradientLibraryPanel : VisualElement
    {
        private readonly TextField folderField;
        private readonly TextField saveNameField;
        private readonly VisualElement grid;
        private string folder;

        /// <summary>Supplies the gradient to save or overwrite with.</summary>
        public Func<GradientABCW> GetCurrentGradient { get; set; }

        public event Action<GradientABCW> Loaded;

        public GradientLibraryPanel()
        {
            AddToClassList("abcw-library");
            folder = GradientABCWSettings.instance.DefaultLibraryFolder;

            var folderRow = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
            folderField = new TextField("Folder") { name = "folder", value = folder, style = { flexGrow = 1 } };
            folderRow.Add(folderField);
            folderRow.Add(new Button(BrowseFolder) { name = "browse", text = "Browse..." });
            folderRow.Add(new Button(ApplyFolder) { name = "apply", text = "Apply" });
            folderRow.Add(new Button(SetDefault) { name = "setDefault", text = "Set Default" });
            Add(folderRow);

            var saveRow = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
            saveNameField = new TextField("Name") { name = "saveName", value = "NewGradientABCW", style = { flexGrow = 1 } };
            saveRow.Add(saveNameField);
            saveRow.Add(new Button(SaveCurrent) { name = "save", text = "Save" });
            Add(saveRow);

            var scroll = new ScrollView(ScrollViewMode.Vertical) { style = { flexGrow = 1 } };
            grid = new VisualElement { style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap } };
            scroll.Add(grid);
            Add(scroll);

            GradientAssetWatcher.LibraryChanged += OnAssetFolderChanged;
            RegisterCallback<DetachFromPanelEvent>(_ => GradientAssetWatcher.LibraryChanged -= OnAssetFolderChanged);

            RefreshGrid();
        }

        private void BrowseFolder()
        {
            string start = Application.dataPath;
            string abs = EditorUtility.OpenFolderPanel("Select Gradient Library Folder", start, "");
            if (string.IsNullOrEmpty(abs) || !abs.StartsWith(start))
                return;

            string rel = "Assets" + abs.Substring(start.Length).Replace("\\", "/");
            folderField.value = rel;
        }

        private void ApplyFolder()
        {
            string rel = GradientLibrary.NormalizeFolder(folderField.value);
            if (rel == null)
            {
                EditorUtility.DisplayDialog("Invalid Folder", "Folder must be inside Assets.", "OK");
                return;
            }

            if (!GradientLibrary.Exists(rel))
            {
                if (!EditorUtility.DisplayDialog("Create Folder?", $"Create '{rel}'?", "Create", "Cancel"))
                    return;
                GradientLibrary.CreateFolder(rel);
            }

            folder = rel;
            RefreshGrid();
        }

        private void SetDefault() => GradientABCWSettings.instance.DefaultLibraryFolder = folderField.value;

        private void SaveCurrent()
        {
            if (!GradientLibrary.Exists(folder))
            {
                EditorUtility.DisplayDialog("Invalid Folder", "Choose a valid library folder.", "OK");
                return;
            }

            if (GradientLibrary.Save(folder, saveNameField.value, GetCurrentGradient?.Invoke()) != null)
                RefreshGrid();
        }

        /// <summary>
        /// Rebuilds only when the change landed in the folder this panel is showing. A rebuild recreates a
        /// preview element, and therefore a texture, per gradient in the library, so it is not something to
        /// do because an unrelated ScriptableObject was saved somewhere else in the project.
        /// </summary>
        private void OnAssetFolderChanged(string changedFolder)
        {
            if (GradientAssetWatcher.Affects(folder, changedFolder))
                RefreshGrid();
        }

        private void RefreshGrid()
        {
            grid.Clear();

            foreach (var asset in GradientLibrary.Load(folder))
                grid.Add(CreateTile(asset));
        }

        private VisualElement CreateTile(GradientABCWAsset asset)
        {
            var tile = new VisualElement { name = "tile" };
            tile.AddToClassList("abcw-library__tile");
            tile.Add(new Label(asset.name) { style = { unityFontStyleAndWeight = FontStyle.Bold } });

            // Final, not Base: a saved asset serializes its modulation along with its keys, so a tile drawn
            // from the base answers "what are this gradient's keys" when what a library is asked is "what
            // is this gradient". Matches the drawer, which also leads with the modulated result.
            var preview = new GradientPreviewElement
            {
                Mode = GradientPreviewElement.PreviewMode.Final,
                Gradient = asset.Gradient,
                style = { height = 16 },
            };
            tile.Add(preview);

            var row = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            row.Add(new Button(() => Loaded?.Invoke(asset.Gradient.Clone())) { text = "Load" });
            row.Add(new Button(() => Overwrite(asset, preview)) { text = "Overwrite" });
            row.Add(new Button(() => Delete(asset)) { text = "Delete" });
            tile.Add(row);

            return tile;
        }

        private void Overwrite(GradientABCWAsset asset, GradientPreviewElement preview)
        {
            if (!EditorUtility.DisplayDialog("Overwrite Asset", $"Overwrite '{asset.name}'?", "Overwrite", "Cancel"))
                return;

            var current = GetCurrentGradient?.Invoke();
            if (current == null)
                return;

            GradientLibrary.Overwrite(asset, current);
            preview.Gradient = asset.Gradient;
        }

        private void Delete(GradientABCWAsset asset)
        {
            if (!EditorUtility.DisplayDialog("Delete Asset", $"Delete '{asset.name}'?", "Delete", "Cancel"))
                return;

            GradientLibrary.Delete(asset);
            RefreshGrid();
        }
    }
}
