using UnityEditor;
using UnityEngine;

namespace ABCodeworld.Gradients.Editor
{
    internal static class PackagePaths
    {
        public const string PackageRoot = "Packages/com.abcodeworld.gradientabcw";

        public static T Load<T>(string relativePath) where T : Object =>
            AssetDatabase.LoadAssetAtPath<T>($"{PackageRoot}/{relativePath}");
    }
}
