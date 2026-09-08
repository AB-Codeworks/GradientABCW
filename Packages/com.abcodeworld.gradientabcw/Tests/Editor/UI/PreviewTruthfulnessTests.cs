using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.UIElements.TestFramework;
using ABCodeworld.Gradients.Editor;
using ABCodeworld.Gradients.Tests.Editor.Support;

namespace ABCodeworld.Gradients.Tests.Editor.UI
{
    /// <summary>
    /// Pins the promise the inspector makes: the gradient you see first is the gradient you get.
    /// </summary>
    /// <remarks>
    /// The control used to lead with the unmodulated base and hide the modulated result at the end of a
    /// foldout that is collapsed by default, so a hue shift or a repeat count was invisible unless you
    /// knew to go looking. These tests assert on baked pixels rather than on bake counts: a modulation
    /// change bumps <c>Version</c>, so even the old base-mode strip re-baked on every modulation edit — it
    /// just re-baked identical pixels. Counting bakes would have called that a pass.
    /// </remarks>
    [TestFixture]
    [Category("UI")]
    internal sealed class PreviewTruthfulnessTests : UITestFixture
    {
        private GradientABCWField field;

        [SetUp]
        public void SetUpField()
        {
            panelSize = new Vector2(400, 400);
            field = new GradientABCWField("Test Gradient");
            field.style.width = 380;
            rootVisualElement.Add(field);
            simulate.FrameUpdate();
        }

        private GradientPreviewElement FinalPreview() =>
            field.Q<GradientPreviewElement>(GradientABCWField.FinalPreviewName);

        private GradientPreviewElement BasePreview() =>
            field.Q<GradientPreviewElement>(GradientABCWField.BasePreviewName);

        /// <summary>
        /// The baked pixels of a preview. The gradient strip is the second Image; the first is the
        /// checkerboard behind it. The texture stays CPU-readable because GradientTextureUtility applies
        /// it with makeNoLongerReadable false.
        /// </summary>
        private static Color32[] ReadPixels(GradientPreviewElement preview)
        {
            var images = preview.Query<Image>().ToList();
            Assert.That(images.Count, Is.EqualTo(2), "expected a checkerboard image and a gradient image");

            var texture = images[1].image as Texture2D;
            Assert.That(texture, Is.Not.Null, "the preview has not baked a texture yet");
            return texture.GetPixels32();
        }

        private static GradientABCW WithBrightness(GradientABCW gradient, float brightness)
        {
            var m = gradient.Modulation;
            m.brightness = brightness;
            gradient.Modulation = m;
            return gradient;
        }

        [Test]
        public void TopPreviewIsFinalAndTheFoldoutPreviewIsTheUnmodulatedBase()
        {
            field.value = TestGradients.Rainbow7();
            simulate.FrameUpdate();

            var previews = field.Query<GradientPreviewElement>().ToList();
            Assert.That(previews.Count, Is.EqualTo(2));

            // Order matters: the first one in the tree is the one the user sees without expanding anything.
            Assert.That(previews[0].name, Is.EqualTo(GradientABCWField.FinalPreviewName));
            Assert.That(previews[0].Mode, Is.EqualTo(GradientPreviewElement.PreviewMode.Final));
            Assert.That(previews[1].name, Is.EqualTo(GradientABCWField.BasePreviewName));
            Assert.That(previews[1].Mode, Is.EqualTo(GradientPreviewElement.PreviewMode.Base));
        }

        [Test]
        public void OnlyTheTopPreviewIsClickable()
        {
            Assert.That(FinalPreview().Clickable, Is.True, "the top strip opens the picker");
            Assert.That(BasePreview().Clickable, Is.False, "the base strip is a display-only reference");
        }

        [Test]
        public void ModulationChangeWhileCollapsed_ChangesWhatTheUserSees()
        {
            var g = TestGradients.Rainbow7();
            field.value = g;
            simulate.FrameUpdate();

            Assume.That(field.Q<Foldout>().value, Is.False, "the modulation foldout starts collapsed");

            var top = FinalPreview();
            var before = ReadPixels(top);

            WithBrightness(g, -1f); // drives the whole gradient to black
            field.SetValueWithoutNotify(g);
            simulate.FrameUpdate();

            // This is the bug this change exists to fix: with the foldout shut, the only visible strip
            // used to ignore modulation entirely, so this edit produced no visible difference at all.
            CollectionAssert.AreNotEqual(before, ReadPixels(top));
        }

        [Test]
        public void WithTheFoldoutOpen_OnlyTheTopPreviewFollowsModulation()
        {
            var g = TestGradients.Rainbow7();
            field.value = g;
            field.Q<Foldout>().value = true;
            simulate.FrameUpdate();

            var top = FinalPreview();
            var unmodulated = BasePreview();
            var topBefore = ReadPixels(top);
            var baseBefore = ReadPixels(unmodulated);

            WithBrightness(g, -1f);
            field.SetValueWithoutNotify(g);
            simulate.FrameUpdate();

            CollectionAssert.AreNotEqual(topBefore, ReadPixels(top), "the final strip must follow modulation");
            CollectionAssert.AreEqual(baseBefore, ReadPixels(unmodulated), "the base strip must ignore modulation");
        }

        [Test]
        public void FoldoutHeadingSaysWhenModulationIsDoingSomething()
        {
            var g = TestGradients.Rainbow7();
            field.value = g;
            simulate.FrameUpdate();

            var foldout = field.Q<Foldout>();
            Assert.That(foldout.text, Is.EqualTo(GradientABCWField.ModulationLabel), "identity modulation is not active");

            var m = g.Modulation;
            m.hueShift = 0.3f;
            g.Modulation = m;
            field.SetValueWithoutNotify(g);
            simulate.FrameUpdate();

            Assert.That(foldout.text, Is.EqualTo(GradientABCWField.ModulationActiveLabel));

            // Bypass and identity are folded together by IsEffective, so cover bypass separately: a
            // regression in either alone would otherwise leave this test green.
            m.bypass = true;
            g.Modulation = m;
            field.SetValueWithoutNotify(g);
            simulate.FrameUpdate();

            Assert.That(foldout.text, Is.EqualTo(GradientABCWField.ModulationLabel), "bypassed modulation is not active");
        }

        [Test]
        public void LibraryTilesShowTheModulatedGradient()
        {
            var settings = GradientABCWSettings.instance;
            string originalFolder = settings.DefaultLibraryFolder;

            try
            {
                using var folder = new TempAssetFolder();
                Assume.That(GradientLibrary.Save(folder.Path, "Modulated", TestGradients.WithModulation()), Is.Not.Null);

                // The panel reads the default library folder in its constructor.
                settings.DefaultLibraryFolder = folder.Path;

                var panel = new GradientLibraryPanel();
                rootVisualElement.Add(panel);
                simulate.FrameUpdate();

                var tile = panel.Q<GradientPreviewElement>();
                Assert.That(tile, Is.Not.Null, "expected a tile for the saved gradient");
                Assert.That(tile.Mode, Is.EqualTo(GradientPreviewElement.PreviewMode.Final));
            }
            finally
            {
                settings.DefaultLibraryFolder = originalFolder;
            }
        }
    }
}
