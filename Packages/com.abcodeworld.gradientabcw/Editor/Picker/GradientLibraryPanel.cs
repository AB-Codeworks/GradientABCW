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

            GradientAssetWatcher.LibraryChanged += RefreshGrid;
            RegisterCallback<DetachFromPanelEvent>(_ => GradientAssetWatcher.LibraryChanged -= RefreshGrid);

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
            string rel = folderField.value?.Trim().Replace("\\", "/");
            if (string.IsNullOrEmpty(rel) || !rel.StartsWith("Assets"))
            {
                EditorUtility.DisplayDialog("Invalid Folder", "Folder must be inside Assets.", "OK");
                return;
            }

            if (!AssetDatabase.IsValidFolder(rel))
            {
                if (!EditorUtility.DisplayDialog("Create Folder?", $"Create '{rel}'?", "Create", "Cancel"))
                    return;
                CreateNestedFolders(rel);
            }

            folder = rel;
            RefreshGrid();
        }

        private void SetDefault() => GradientABCWSettings.instance.DefaultLibraryFolder = folderField.value;

        private static void CreateNestedFolders(string fullPath)
        {
            var parts = fullPath.Split('/');
            string cur = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = cur + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(cur, parts[i]);
                cur = next;
            }
        }

        private void SaveCurrent()
        {
            if (!AssetDatabase.IsValidFolder(folder))
            {
                EditorUtility.DisplayDialog("Invalid Folder", "Choose a valid library folder.", "OK");
                return;
            }

            var current = GetCurrentGradient?.Invoke();
            if (current == null)
                return;

            string baseName = string.IsNullOrWhiteSpace(saveNameField.value) ? "NewGradientABCW" : saveNameField.value.Trim();
            string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{baseName}.asset");

            var asset = ScriptableObject.CreateInstance<GradientABCWAsset>();
            asset.Gradient = current.Clone();
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();

            RefreshGrid();
        }

        private void RefreshGrid()
        {
            grid.Clear();
            if (!AssetDatabase.IsValidFolder(folder))
                return;

            var assets = new List<GradientABCWAsset>();
            foreach (var guid in AssetDatabase.FindAssets("t:GradientABCWAsset", new[] { folder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<GradientABCWAsset>(path);
                if (asset != null)
                    assets.Add(asset);
            }
            assets.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase));

            foreach (var asset in assets)
                grid.Add(CreateTile(asset));
        }

        private VisualElement CreateTile(GradientABCWAsset asset)
        {
            var tile = new VisualElement { name = "tile" };
            tile.AddToClassList("abcw-library__tile");
            tile.Add(new Label(asset.name) { style = { unityFontStyleAndWeight = FontStyle.Bold } });

            var preview = new GradientPreviewElement
            {
                Mode = GradientPreviewElement.PreviewMode.Base,
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

            Undo.RecordObject(asset, "Overwrite Gradient Asset");
            asset.Gradient = current.Clone();
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            preview.Gradient = asset.Gradient;
        }

        private void Delete(GradientABCWAsset asset)
        {
            if (!EditorUtility.DisplayDialog("Delete Asset", $"Delete '{asset.name}'?", "Delete", "Cancel"))
                return;

            AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(asset));
            RefreshGrid();
        }
    }
}
