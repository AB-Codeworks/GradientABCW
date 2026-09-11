using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ABCodeworld.Gradients.Editor
{
    /// <summary>Browse, save, load, overwrite and delete <see cref="GradientABCW3DAsset"/> gradients in a folder.</summary>
    /// <remarks>
    /// A sibling of <see cref="GradientLibraryPanel"/>. The two share their folder handling through
    /// <see cref="AssetFolders"/> and their change notification through <see cref="GradientAssetWatcher"/>,
    /// which needed no changes at all — it was already type-agnostic. What is left is the tile, and the
    /// tiles genuinely differ: a 1D gradient previews as a strip, a 3D one as a row of cube renders.
    /// </remarks>
    internal sealed class GradientLibrary3DPanel : VisualElement
    {
        private readonly TextField folderField;
        private readonly TextField saveNameField;
        private readonly VisualElement grid;
        private string folder;

        /// <summary>Supplies the gradient to save or overwrite with.</summary>
        public Func<GradientABCW3D> GetCurrentGradient { get; set; }

        public event Action<GradientABCW3D> Loaded;

        public GradientLibrary3DPanel()
        {
            AddToClassList("abcw-library");
            folder = GradientABCWSettings.instance.Default3DLibraryFolder;

            var folderRow = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
            folderField = new TextField("Folder") { name = "folder", value = folder, style = { flexGrow = 1 } };
            folderRow.Add(folderField);
            folderRow.Add(new Button(BrowseFolder) { name = "browse", text = "Browse..." });
            folderRow.Add(new Button(ApplyFolder) { name = "apply", text = "Apply" });
            folderRow.Add(new Button(SetDefault) { name = "setDefault", text = "Set Default" });
            Add(folderRow);

            var saveRow = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
            saveNameField = new TextField("Name") { name = "saveName", value = "NewGradientABCW3D", style = { flexGrow = 1 } };
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
            string abs = EditorUtility.OpenFolderPanel("Select 3D Gradient Library Folder", start, "");
            if (string.IsNullOrEmpty(abs) || !abs.StartsWith(start))
                return;

            string rel = "Assets" + abs.Substring(start.Length).Replace("\\", "/");
            folderField.value = rel;
        }

        private void ApplyFolder()
        {
            string rel = AssetFolders.Normalize(folderField.value);
            if (rel == null)
            {
                EditorUtility.DisplayDialog("Invalid Folder", "Folder must be inside Assets.", "OK");
                return;
            }

            if (!AssetFolders.Exists(rel))
            {
                if (!EditorUtility.DisplayDialog("Create Folder?", $"Create '{rel}'?", "Create", "Cancel"))
                    return;
                AssetFolders.Create(rel);
            }

            folder = rel;
            RefreshGrid();
        }

        private void SetDefault() => GradientABCWSettings.instance.Default3DLibraryFolder = folderField.value;

        private void SaveCurrent()
        {
            if (!AssetFolders.Exists(folder))
            {
                EditorUtility.DisplayDialog("Invalid Folder", "Choose a valid library folder.", "OK");
                return;
            }

            if (GradientLibrary3D.Save(folder, saveNameField.value, GetCurrentGradient?.Invoke()) != null)
                RefreshGrid();
        }

        /// <summary>
        /// Rebuilds only when the change landed in the folder this panel is showing. A rebuild recreates a
        /// preview element, and therefore a texture, per gradient in the library — four of them per tile
        /// here — so it is not something to do because an unrelated ScriptableObject was saved elsewhere.
        /// </summary>
        private void OnAssetFolderChanged(string changedFolder)
        {
            if (GradientAssetWatcher.Affects(folder, changedFolder))
                RefreshGrid();
        }

        private void RefreshGrid()
        {
            grid.Clear();

            foreach (var asset in GradientLibrary3D.Load(folder))
                grid.Add(CreateTile(asset));
        }

        private VisualElement CreateTile(GradientABCW3DAsset asset)
        {
            var tile = new VisualElement { name = "tile" };
            tile.AddToClassList("abcw-library__tile");
            tile.Add(new Label(asset.name) { style = { unityFontStyleAndWeight = FontStyle.Bold } });

            // Modulated, like the 1D library's tiles: an asset serializes its modulation alongside its
            // keys, so a tile drawn from the base answers a different question from the one a library is
            // asked — which of these saved gradients is the one I want.
            var preview = new CubePreviewStripElement(TilePreviewSize)
            {
                IncludeModulation = true,
                Gradient = asset.Gradient,
            };
            tile.Add(preview);

            var row = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            row.Add(new Button(() => Loaded?.Invoke(asset.Gradient.Clone())) { text = "Load" });
            row.Add(new Button(() => Overwrite(asset, preview)) { text = "Overwrite" });
            row.Add(new Button(() => Delete(asset)) { text = "Delete" });
            tile.Add(row);

            return tile;
        }

        /// <summary>
        /// Smaller than the inspector's renders. A tile carries four of them and the library grid carries
        /// many tiles, and every pixel of every one costs a ray cast and a pass over the gradient's keys.
        /// </summary>
        private const int TilePreviewSize = 24;

        private void Overwrite(GradientABCW3DAsset asset, CubePreviewStripElement preview)
        {
            if (!EditorUtility.DisplayDialog("Overwrite Asset", $"Overwrite '{asset.name}'?", "Overwrite", "Cancel"))
                return;

            var current = GetCurrentGradient?.Invoke();
            if (current == null)
                return;

            GradientLibrary3D.Overwrite(asset, current);
            preview.Gradient = asset.Gradient;
        }

        private void Delete(GradientABCW3DAsset asset)
        {
            if (!EditorUtility.DisplayDialog("Delete Asset", $"Delete '{asset.name}'?", "Delete", "Cancel"))
                return;

            GradientLibrary3D.Delete(asset);
            RefreshGrid();
        }
    }
}
