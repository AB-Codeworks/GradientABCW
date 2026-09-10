using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ABCodeworld.Gradients.Editor
{
    /// <summary>
    /// The asset side of the gradient library: validating and creating folders, and finding, saving,
    /// overwriting and deleting <see cref="GradientABCWAsset"/> files.
    /// </summary>
    /// <remarks>
    /// Separated from <see cref="GradientLibraryPanel"/> so that none of this needs a VisualElement, a
    /// panel or a layout pass to exercise. The panel is now only responsible for showing what this
    /// returns and for asking the user before anything destructive.
    /// </remarks>
    internal static class GradientLibrary
    {
        public const string DefaultFolder = "Assets/Gradients";

        // Folder handling lives in AssetFolders, shared with the 3D library. These stay as forwarders so
        // that callers — and the tests that already cover them — keep one entry point per library.

        /// <inheritdoc cref="AssetFolders.Normalize"/>
        public static string NormalizeFolder(string folder) => AssetFolders.Normalize(folder);

        /// <inheritdoc cref="AssetFolders.Exists"/>
        public static bool Exists(string folder) => AssetFolders.Exists(folder);

        /// <inheritdoc cref="AssetFolders.Create"/>
        public static void CreateFolder(string folder) => AssetFolders.Create(folder);

        /// <summary>Every gradient asset directly under <paramref name="folder"/>, ordered by name.</summary>
        public static List<GradientABCWAsset> Load(string folder)
        {
            var assets = new List<GradientABCWAsset>();
            if (!Exists(folder))
                return assets;

            foreach (var guid in AssetDatabase.FindAssets("t:GradientABCWAsset", new[] { folder }))
            {
                var asset = AssetDatabase.LoadAssetAtPath<GradientABCWAsset>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null)
                    assets.Add(asset);
            }

            assets.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase));
            return assets;
        }

        /// <summary>Saves a copy of <paramref name="gradient"/> as a new asset. Returns the created asset, or null.</summary>
        public static GradientABCWAsset Save(string folder, string name, GradientABCW gradient)
        {
            if (!Exists(folder) || gradient == null)
                return null;

            string baseName = string.IsNullOrWhiteSpace(name) ? "NewGradientABCW" : name.Trim();
            string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{baseName}.asset");

            var asset = ScriptableObject.CreateInstance<GradientABCWAsset>();
            asset.Gradient = gradient.Clone();
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            return asset;
        }

        public static void Overwrite(GradientABCWAsset asset, GradientABCW gradient)
        {
            if (asset == null || gradient == null)
                return;

            Undo.RecordObject(asset, "Overwrite Gradient Asset");
            asset.Gradient = gradient.Clone();
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
        }

        public static void Delete(GradientABCWAsset asset)
        {
            if (asset != null)
                AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(asset));
        }
    }
}
