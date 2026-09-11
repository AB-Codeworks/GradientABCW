using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace ABCodeworld.Gradients.Editor
{
    /// <summary>
    /// Resolves paths inside this package, wherever it happens to be installed.
    /// </summary>
    /// <remarks>
    /// The literal below is only correct for the usual installs — registry, git URL, or a folder under
    /// <c>Packages/</c>. Embedded anywhere else, every USS and UXML load returns null and the whole editor
    /// UI renders unstyled with nothing said about why, because the loaders guard on null. Asking the
    /// package manager where this assembly actually lives costs one reflection call at first use and
    /// removes the assumption; the literal stays as the fallback for the case where it cannot say.
    /// </remarks>
    internal static class PackagePaths
    {
        public const string PackageName = "com.abcodeworld.gradientabcw";

        private static string cachedRoot;

        public static string PackageRoot => cachedRoot ??= ResolveRoot();

        private static string ResolveRoot()
        {
            var info = UnityEditor.PackageManager.PackageInfo.FindForAssembly(Assembly.GetExecutingAssembly());
            return string.IsNullOrEmpty(info?.assetPath) ? $"Packages/{PackageName}" : info.assetPath;
        }

        public static T Load<T>(string relativePath) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>($"{PackageRoot}/{relativePath}");
            if (asset == null)
                Debug.LogWarning($"[GradientABCW] Missing packaged asset '{relativePath}' under '{PackageRoot}'. The editor UI will render without it.");
            return asset;
        }
    }
}
