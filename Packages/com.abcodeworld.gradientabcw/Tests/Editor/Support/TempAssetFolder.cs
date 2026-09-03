using UnityEditor;

namespace ABCodeworld.Gradients.Tests.Editor.Support
{
    /// <summary>Creates and tears down a scratch folder under <c>Assets/</c> for tests that need real assets.</summary>
    internal sealed class TempAssetFolder : System.IDisposable
    {
        public const string RootPath = "Assets/_GradientABCWTests";

        public string Path { get; }

        public TempAssetFolder(string subFolder = null)
        {
            if (!AssetDatabase.IsValidFolder(RootPath))
                AssetDatabase.CreateFolder("Assets", "_GradientABCWTests");

            if (string.IsNullOrEmpty(subFolder))
            {
                Path = RootPath;
                return;
            }

            Path = RootPath + "/" + subFolder;
            if (!AssetDatabase.IsValidFolder(Path))
                AssetDatabase.CreateFolder(RootPath, subFolder);
        }

        public void Dispose()
        {
            if (AssetDatabase.IsValidFolder(RootPath))
                AssetDatabase.DeleteAsset(RootPath);
        }
    }
}
