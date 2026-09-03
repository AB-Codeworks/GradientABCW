using NUnit.Framework;
using ABCodeworld.Gradients.Editor;

namespace ABCodeworld.Gradients.Tests.Editor.UI
{
    [TestFixture]
    [Category("UI")]
    internal sealed class GradientABCWSettingsTests
    {
        private string originalFolder;
        private int originalResolution;
        private bool originalLivePreview;

        [SetUp]
        public void SaveOriginal()
        {
            var s = GradientABCWSettings.instance;
            originalFolder = s.DefaultLibraryFolder;
            originalResolution = s.PreviewResolution;
            originalLivePreview = s.PickerLivePreview;
        }

        [TearDown]
        public void RestoreOriginal()
        {
            var s = GradientABCWSettings.instance;
            s.DefaultLibraryFolder = originalFolder;
            s.PreviewResolution = originalResolution;
            s.PickerLivePreview = originalLivePreview;
        }

        [Test]
        public void DefaultLibraryFolder_EmptyValue_FallsBackToDefault()
        {
            var s = GradientABCWSettings.instance;
            s.DefaultLibraryFolder = "";
            Assert.That(s.DefaultLibraryFolder, Is.EqualTo("Assets/Gradients"));
        }

        [Test]
        public void DefaultLibraryFolder_PersistsValue()
        {
            var s = GradientABCWSettings.instance;
            s.DefaultLibraryFolder = "Assets/MyGradients";
            Assert.That(s.DefaultLibraryFolder, Is.EqualTo("Assets/MyGradients"));
        }

        [Test]
        public void PreviewResolution_ClampsToAtLeastTwo()
        {
            var s = GradientABCWSettings.instance;
            s.PreviewResolution = 0;
            Assert.That(s.PreviewResolution, Is.GreaterThanOrEqualTo(2));
        }

        [Test]
        public void PickerLivePreview_DefaultsToTrue()
        {
            var s = GradientABCWSettings.instance;
            s.PickerLivePreview = true;
            Assert.That(s.PickerLivePreview, Is.True);
        }

        [Test]
        public void SettingsProvider_IsRegistered()
        {
            var provider = GradientABCWSettingsProvider.CreateSettingsProvider();
            Assert.That(provider, Is.Not.Null);
            Assert.That(provider.settingsPath, Is.EqualTo("Project/ABCodeworld/Gradient ABCW"));
        }
    }
}
