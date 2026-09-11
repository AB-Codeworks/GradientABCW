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
    /// The 3D reading of <see cref="PreviewTruthfulnessTests"/>: the cube you see first is the cube you
    /// get.
    /// </summary>
    /// <remarks>
    /// The 3D field originally mirrored the 1D field as it stood before that fix, leading with the
    /// unmodulated base. The argument for changing it is stronger here, because 3D modulation is harder
    /// to picture from its numbers: a repeat count of 2 tiles the cube 2x2x2, eight times over rather than
    /// twice, and reverse mirrors all three axes at once.
    /// <para>
    /// These assert on rendered pixels rather than on render counts, for the reason the 1D file records:
    /// a modulation change bumps <c>Version</c>, so even a base-mode strip re-renders on every modulation
    /// edit — it just re-renders identical pixels, and counting renders would call that a pass.
    /// </para>
    /// </remarks>
    [TestFixture]
    [Category("UI")]
    internal sealed class Preview3DTruthfulnessTests : UITestFixture
    {
        private GradientABCW3DField field;

        [SetUp]
        public void SetUpField()
        {
            panelSize = new Vector2(500, 500);
            field = new GradientABCW3DField("Test Gradient 3D");
            field.style.width = 460;
            rootVisualElement.Add(field);
            simulate.FrameUpdate();
        }

        private CubePreviewStripElement FinalPreview() =>
            field.Q<CubePreviewStripElement>(GradientABCW3DField.FinalPreviewName);

        private CubePreviewStripElement BasePreview() =>
            field.Q<CubePreviewStripElement>(GradientABCW3DField.BasePreviewName);

        private Foldout ModulationFoldout() => field.Q<Foldout>("modulation");

        /// <summary>
        /// The rendered pixels of a strip's first view. Each view stacks two images — the checkerboard
        /// behind, the cube render in front — and the render stays CPU-readable because
        /// <see cref="CubePreviewTexture"/> applies it with makeNoLongerReadable false.
        /// </summary>
        private static Color32[] ReadPixels(CubePreviewStripElement strip)
        {
            Assert.That(strip.Views.Count, Is.GreaterThan(0));

            var images = strip.Views[0].Query<Image>().ToList();
            Assert.That(images.Count, Is.EqualTo(2), "expected a checkerboard image and a cube image");

            var texture = images[1].image as Texture2D;
            Assert.That(texture, Is.Not.Null, "the preview has not rendered a texture yet");
            return texture.GetPixels32();
        }

        private static GradientABCW3D WithBrightness(GradientABCW3D gradient, float brightness)
        {
            var m = gradient.Modulation;
            m.brightness = brightness;
            gradient.Modulation = m;
            return gradient;
        }

        [Test]
        public void TheTopStripIsFinalAndTheFoldoutStripIsTheUnmodulatedBase()
        {
            field.value = Test3DGradients.Corners8();
            simulate.FrameUpdate();

            var strips = field.Query<CubePreviewStripElement>().ToList();
            Assert.That(strips.Count, Is.EqualTo(2));

            // Order matters: the first in the tree is the one seen without expanding anything.
            Assert.That(strips[0].name, Is.EqualTo(GradientABCW3DField.FinalPreviewName));
            Assert.That(strips[0].IncludeModulation, Is.True);
            Assert.That(strips[1].name, Is.EqualTo(GradientABCW3DField.BasePreviewName));
            Assert.That(strips[1].IncludeModulation, Is.False);
        }

        [Test]
        public void OnlyTheTopStripIsClickable()
        {
            Assert.That(FinalPreview().Clickable, Is.True, "the top strip opens the picker");
            Assert.That(BasePreview().Clickable, Is.False, "the base strip is a display-only reference");
        }

        [Test]
        public void ModulationChangeWhileCollapsed_ChangesWhatTheUserSees()
        {
            var g = Test3DGradients.Corners8();
            field.value = g;
            simulate.FrameUpdate();

            Assume.That(ModulationFoldout().value, Is.False, "the modulation foldout starts collapsed");

            var top = FinalPreview();
            var before = ReadPixels(top);

            WithBrightness(g, -1f); // drives the whole cube to black
            field.SetValueWithoutNotify(g);
            simulate.FrameUpdate();

            // The bug this change exists to fix: with the foldout shut, the only visible renders used to
            // ignore modulation entirely, so this edit produced no visible difference at all.
            CollectionAssert.AreNotEqual(before, ReadPixels(top));
        }

        [Test]
        public void WithTheFoldoutOpen_OnlyTheTopStripFollowsModulation()
        {
            var g = Test3DGradients.Corners8();
            field.value = g;
            ModulationFoldout().value = true;
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

        /// <summary>
        /// Domain modulation is the case a 3D user is least able to picture from the numbers, and it moves
        /// the sample point rather than the colour — so it exercises a different path through the renderer
        /// than brightness does.
        /// </summary>
        [Test]
        public void RepeatsAreVisibleInTheTopStrip()
        {
            var g = Test3DGradients.Corners8();
            field.value = g;
            simulate.FrameUpdate();

            var top = FinalPreview();
            var before = ReadPixels(top);

            var m = g.Modulation;
            m.repeats = 2f;
            m.repeatMode = RepeatMode.Mirror;
            g.Modulation = m;
            field.SetValueWithoutNotify(g);
            simulate.FrameUpdate();

            CollectionAssert.AreNotEqual(before, ReadPixels(top), "repeats tile the cube and must be visible");
        }

        [Test]
        public void FoldoutHeadingSaysWhenModulationIsDoingSomething()
        {
            var g = Test3DGradients.Corners8();
            field.value = g;
            simulate.FrameUpdate();

            var foldout = ModulationFoldout();
            Assert.That(foldout.text, Is.EqualTo(GradientABCW3DField.ModulationLabel), "identity modulation is not active");

            var m = g.Modulation;
            m.hueShift = 0.3f;
            g.Modulation = m;
            field.SetValueWithoutNotify(g);
            simulate.FrameUpdate();

            Assert.That(foldout.text, Is.EqualTo(GradientABCW3DField.ModulationActiveLabel));

            // Bypass and identity are folded together by IsEffective, so cover bypass separately: a
            // regression in either alone would otherwise leave this test green.
            m.bypass = true;
            g.Modulation = m;
            field.SetValueWithoutNotify(g);
            simulate.FrameUpdate();

            Assert.That(foldout.text, Is.EqualTo(GradientABCW3DField.ModulationLabel), "bypassed modulation is not active");
        }

        [Test]
        public void LibraryTilesShowTheModulatedGradient()
        {
            var settings = GradientABCWSettings.instance;
            string originalFolder = settings.Default3DLibraryFolder;

            try
            {
                using var folder = new TempAssetFolder();
                Assume.That(GradientLibrary3D.Save(folder.Path, "Modulated", Test3DGradients.WithModulation()), Is.Not.Null);

                // The panel reads the default library folder in its constructor.
                settings.Default3DLibraryFolder = folder.Path;

                var panel = new GradientLibrary3DPanel();
                rootVisualElement.Add(panel);
                simulate.FrameUpdate();

                var tile = panel.Q<CubePreviewStripElement>();
                Assert.That(tile, Is.Not.Null, "expected a tile for the saved gradient");
                Assert.That(tile.IncludeModulation, Is.True);
            }
            finally
            {
                settings.Default3DLibraryFolder = originalFolder;
            }
        }
    }
}
