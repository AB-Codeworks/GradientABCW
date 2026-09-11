using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ABCodeworld.Gradients.Editor
{
    /// <summary>
    /// The asset side of the 3D gradient library: finding, saving, overwriting and deleting
    /// <see cref="GradientABCW3DAsset"/> files.
    /// </summary>
    /// <remarks>
    /// Deliberately a sibling of <see cref="GradientLibrary"/> rather than a shared generic. What the two
    /// have in common — validating and creating folders — already lives in <see cref="AssetFolders"/>, and
    /// what is left names a concrete asset type in an <see cref="AssetDatabase"/> type filter and in the
    /// object it creates. Making that generic needs an interface on both asset types and another on both
    /// gradient types, to save about forty lines that read better spelled out.
    /// </remarks>
    internal static class GradientLibrary3D
    {
        public const string DefaultFolder = "Assets/Gradients3D";

        /// <summary>Every 3D gradient asset directly under <paramref name="folder"/>, ordered by name.</summary>
        public static List<GradientABCW3DAsset> Load(string folder)
        {
            var assets = new List<GradientABCW3DAsset>();
            if (!AssetFolders.Exists(folder))
                return assets;

            foreach (var guid in AssetDatabase.FindAssets("t:GradientABCW3DAsset", new[] { folder }))
            {
                var asset = AssetDatabase.LoadAssetAtPath<GradientABCW3DAsset>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null)
                    assets.Add(asset);
            }

            assets.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase));
            return assets;
        }

        /// <summary>Saves a copy of <paramref name="gradient"/> as a new asset. Returns the created asset, or null.</summary>
        public static GradientABCW3DAsset Save(string folder, string name, GradientABCW3D gradient)
        {
            if (!AssetFolders.Exists(folder) || gradient == null)
                return null;

            string baseName = string.IsNullOrWhiteSpace(name) ? "NewGradientABCW3D" : name.Trim();
            string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{baseName}.asset");

            var asset = ScriptableObject.CreateInstance<GradientABCW3DAsset>();
            asset.Gradient = gradient.Clone();
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            return asset;
        }

        public static void Overwrite(GradientABCW3DAsset asset, GradientABCW3D gradient)
        {
            if (asset == null || gradient == null)
                return;

            Undo.RecordObject(asset, "Overwrite 3D Gradient Asset");
            asset.Gradient = gradient.Clone();
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
        }

        public static void Delete(GradientABCW3DAsset asset)
        {
            if (asset != null)
                AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(asset));
        }
    }
}
