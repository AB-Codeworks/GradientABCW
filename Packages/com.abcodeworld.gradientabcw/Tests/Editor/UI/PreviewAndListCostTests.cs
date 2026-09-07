using NUnit.Framework;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.UIElements.TestFramework;
using ABCodeworld.Gradients.Editor;
using ABCodeworld.Gradients.Tests.Editor.Support;

namespace ABCodeworld.Gradients.Tests.Editor.UI
{
    /// <summary>
    /// Pins the Phase 3 editor-cost behaviour: previews bake at a fixed resolution rather than churning a
    /// texture per resize, the key list stops rebuilding its rows when only values changed, and the
    /// library watcher ignores assets outside the folder being shown.
    /// </summary>
    [TestFixture]
    [Category("UI")]
    internal sealed class PreviewAndListCostTests : UITestFixture
    {
        [Test]
        public void ResizingTheField_DoesNotRebakeThePreview()
        {
            panelSize = new Vector2(600, 300);
            var field = new GradientABCWField("Test Gradient");
            field.style.width = 380;
            rootVisualElement.Add(field);
            simulate.FrameUpdate();

            field.value = TestGradients.Rainbow7();
            simulate.FrameUpdate();

            var preview = field.Q<GradientPreviewElement>();
            int bakesAfterFirstShow = preview.BakeCount;
            Assume.That(bakesAfterFirstShow, Is.GreaterThan(0));

            foreach (int width in new[] { 200, 300, 420, 560 })
            {
                field.style.width = width;
                simulate.FrameUpdate();
            }

            // The bake width no longer tracks the element width, so a resize is pure layout: no re-bake,
            // and no Texture2D destroyed and recreated behind it.
            Assert.That(preview.BakeCount, Is.EqualTo(bakesAfterFirstShow));
        }

        [Test]
        public void PreviewBakesAtTheConfiguredResolution()
        {
            var settings = GradientABCWSettings.instance;
            int original = settings.PreviewResolution;
            try
            {
                settings.PreviewResolution = 128;

                panelSize = new Vector2(400, 300);
                var field = new GradientABCWField("Test Gradient");
                field.style.width = 380; // deliberately not 128, so matching the setting proves the point
                rootVisualElement.Add(field);
                field.value = TestGradients.Rainbow7();
                simulate.FrameUpdate();

                var preview = field.Q<GradientPreviewElement>();
                Assume.That(preview.BakeCount, Is.GreaterThan(0));

                // The gradient strip is the second Image; the first is the checkerboard behind it.
                var images = preview.Query<Image>().ToList();
                Assume.That(images.Count, Is.EqualTo(2));
                var baked = images[1].image;

                Assert.That(baked, Is.Not.Null);
                Assert.That(baked.width, Is.EqualTo(128), "the preview should bake at the configured resolution, not the element width");
                Assert.That(baked.height, Is.EqualTo(1));
            }
            finally
            {
                settings.PreviewResolution = original;
            }
        }

        [Test]
        public void KeyList_ValueOnlyChange_DoesNotRebuildRows()
        {
            panelSize = new Vector2(500, 400);
            var gradient = TestGradients.Rainbow7();
            var list = new KeyListElement(isAlpha: false);
            list.style.height = 300;
            rootVisualElement.Add(list);
            list.SetGradient(gradient);
            simulate.FrameUpdate();

            var firstRow = list.Q<ColorField>("value");
            Assume.That(firstRow, Is.Not.Null, "expected at least one bound row");

            // Same key count, different colour: the rows should be re-bound, not rebuilt.
            gradient.SetColorKey(0, new ColorKey(Color.magenta, gradient.ColorKeys[0].time));
            list.Refresh();
            simulate.FrameUpdate();

            Assert.That(list.Q<ColorField>("value"), Is.SameAs(firstRow),
                "the row element was recreated, so ListView pooling was defeated");
        }

        [Test]
        public void KeyList_KeyCountChange_DoesRebuild()
        {
            panelSize = new Vector2(500, 400);
            var gradient = TestGradients.Rainbow7();
            var list = new KeyListElement(isAlpha: false);
            list.style.height = 300;
            rootVisualElement.Add(list);
            list.SetGradient(gradient);
            simulate.FrameUpdate();

            int before = list.Query<ColorField>().ToList().Count;
            Assume.That(before, Is.GreaterThan(0));

            gradient.RemoveColorKey(0);
            list.Refresh();
            simulate.FrameUpdate();

            Assert.That(list.Query<ColorField>().ToList().Count, Is.LessThan(before));
        }

        [Test]
        public void AssetWatcher_OnlyMatchesTheWatchedFolderAndItsChildren()
        {
            Assert.That(GradientAssetWatcher.Affects("Assets/Gradients", "Assets/Gradients"), Is.True);
            Assert.That(GradientAssetWatcher.Affects("Assets/Gradients", "Assets/Gradients/Warm"), Is.True);

            // The cases that used to rebuild the whole library grid for no reason.
            Assert.That(GradientAssetWatcher.Affects("Assets/Gradients", "Assets/Settings"), Is.False);
            Assert.That(GradientAssetWatcher.Affects("Assets/Gradients", "Assets"), Is.False);
            Assert.That(GradientAssetWatcher.Affects("Assets/Gradients", "Assets/GradientsOther"), Is.False);
        }
    }
}
