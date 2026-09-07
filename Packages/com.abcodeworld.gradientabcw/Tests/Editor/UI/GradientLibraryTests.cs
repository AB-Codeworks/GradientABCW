using NUnit.Framework;
using ABCodeworld.Gradients.Editor;
using ABCodeworld.Gradients.Tests.Editor.Support;

namespace ABCodeworld.Gradients.Tests.Editor.UI
{
    /// <summary>
    /// The library's asset handling, exercised without building a panel. Previously all of this was
    /// private to <see cref="GradientLibraryPanel"/> and only reachable through its UI.
    /// </summary>
    [TestFixture]
    [Category("UI")]
    internal sealed class GradientLibraryTests
    {
        [Test]
        public void NormalizeFolder_AcceptsPathsInsideAssets()
        {
            Assert.That(GradientLibrary.NormalizeFolder("Assets"), Is.EqualTo("Assets"));
            Assert.That(GradientLibrary.NormalizeFolder("Assets/Gradients"), Is.EqualTo("Assets/Gradients"));
            Assert.That(GradientLibrary.NormalizeFolder("  Assets/Gradients  "), Is.EqualTo("Assets/Gradients"));
            Assert.That(GradientLibrary.NormalizeFolder(@"Assets\Gradients\Warm"), Is.EqualTo("Assets/Gradients/Warm"));
            Assert.That(GradientLibrary.NormalizeFolder("Assets/Gradients/"), Is.EqualTo("Assets/Gradients"));
        }

        [Test]
        public void NormalizeFolder_RejectsAnythingOutsideAssets()
        {
            Assert.That(GradientLibrary.NormalizeFolder(null), Is.Null);
            Assert.That(GradientLibrary.NormalizeFolder(""), Is.Null);
            Assert.That(GradientLibrary.NormalizeFolder("   "), Is.Null);
            Assert.That(GradientLibrary.NormalizeFolder("Packages/com.example"), Is.Null);
            Assert.That(GradientLibrary.NormalizeFolder("C:/Elsewhere"), Is.Null);

            // "AssetsOther" starts with "Assets" as a string but is not inside it.
            Assert.That(GradientLibrary.NormalizeFolder("AssetsOther/Gradients"), Is.Null);
        }

        [Test]
        public void Load_OnAMissingFolder_ReturnsEmptyRatherThanThrowing()
        {
            Assert.That(GradientLibrary.Load("Assets/NoSuchFolderHere"), Is.Empty);
            Assert.That(GradientLibrary.Load(null), Is.Empty);
        }

        [Test]
        public void SaveAndLoadRoundTripThroughTheAssetDatabase()
        {
            using var folder = new TempAssetFolder();

            var gradient = TestGradients.Rainbow7();
            var saved = GradientLibrary.Save(folder.Path, "RoundTrip", gradient);

            Assert.That(saved, Is.Not.Null);
            Assert.That(saved.Gradient.ContentEquals(gradient), Is.True);

            // Saved as a copy, so later edits to the source must not reach the asset.
            gradient.AddColorKey(UnityEngine.Color.magenta, 0.42f);
            Assert.That(saved.Gradient.ContentEquals(gradient), Is.False);

            var loaded = GradientLibrary.Load(folder.Path);
            Assert.That(loaded.Count, Is.EqualTo(1));
            Assert.That(loaded[0].name, Is.EqualTo("RoundTrip"));
        }

        [Test]
        public void Save_RefusesWhenTheFolderDoesNotExist()
        {
            Assert.That(GradientLibrary.Save("Assets/NoSuchFolderHere", "X", TestGradients.Rainbow7()), Is.Null);
        }

        [Test]
        public void Overwrite_ReplacesTheAssetsContentWithACopy()
        {
            using var folder = new TempAssetFolder();

            var original = TestGradients.Default();
            var asset = GradientLibrary.Save(folder.Path, "ToOverwrite", original);
            Assume.That(asset, Is.Not.Null);

            var replacement = TestGradients.Rainbow7();
            GradientLibrary.Overwrite(asset, replacement);

            Assert.That(asset.Gradient.ContentEquals(replacement), Is.True);
            Assert.That(asset.Gradient, Is.Not.SameAs(replacement));
        }
    }
}
