using NUnit.Framework;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.UIElements.TestFramework;
using ABCodeworld.Gradients.Editor;
using ABCodeworld.Gradients.Tests.Editor.Support;

namespace ABCodeworld.Gradients.Tests.Editor.UI
{
    [TestFixture]
    [Category("UI")]
    internal sealed class GradientABCW3DFieldTests : UITestFixture
    {
        private GradientABCW3DField field;

        [SetUp]
        public void SetUpField()
        {
            panelSize = new Vector2(500, 400);
            field = new GradientABCW3DField("Test Gradient 3D");
            field.style.width = 460;
            rootVisualElement.Add(field);
            simulate.FrameUpdate();
        }

        private static void ExecuteMenuAction(ToolbarMenu menu, string actionName)
        {
            foreach (var item in menu.menu.MenuItems())
            {
                if (item is DropdownMenuAction action && action.name == actionName)
                {
                    action.Execute();
                    return;
                }
            }
            Assert.Fail($"Menu action '{actionName}' not found.");
        }

        [Test]
        public void TheSwatchShowsOneRenderPerCoveringView()
        {
            var strip = field.Q<CubePreviewStripElement>("basePreview");

            Assert.That(strip, Is.Not.Null);
            Assert.That(strip.Views.Count, Is.EqualTo(CubePreviewRasterizer.DefaultViews.Length));
        }

        [Test]
        public void SettingValueRefreshesBothPreviews()
        {
            var g = Test3DGradients.Corners8();
            field.value = g;
            simulate.FrameUpdate();

            Assert.That(field.Q<CubePreviewStripElement>("basePreview").Gradient, Is.SameAs(g));
            Assert.That(field.Q<CubePreviewStripElement>("finalPreview").Gradient, Is.SameAs(g));
        }

        [Test]
        public void AnInPlaceEditRaisesTheChangedEventButNotAChangeEvent()
        {
            field.value = Test3DGradients.Corners8();
            simulate.FrameUpdate();

            int changedEvents = 0;
            int valueChanges = 0;
            field.RegisterCallback<Gradient3DChangedEvent>(_ => changedEvents++);
            field.RegisterValueChangedCallback(_ => valueChanges++);

            ExecuteMenuAction(field.Q<ToolbarMenu>("actions"), "Flip Keys");
            simulate.FrameUpdate();

            // Raising both is what once made the property drawer serialize twice and leave two undo
            // records for a single edit.
            Assert.That(changedEvents, Is.EqualTo(1));
            Assert.That(valueChanges, Is.EqualTo(0));
        }

        [Test]
        public void PointingTheFieldAtANewInstanceRaisesAChangeEvent()
        {
            field.value = Test3DGradients.Corners8();
            simulate.FrameUpdate();

            int valueChanges = 0;
            field.RegisterValueChangedCallback(_ => valueChanges++);

            field.value = Test3DGradients.Lattice27();
            simulate.FrameUpdate();

            Assert.That(valueChanges, Is.EqualTo(1));
        }

        [Test]
        public void FlipKeysMirrorsEveryKey()
        {
            var g = Test3DGradients.Corners8();
            field.value = g;
            var before = g.ColorKeys[0].position;

            ExecuteMenuAction(field.Q<ToolbarMenu>("actions"), "Flip Keys");

            Assert.That(g.ColorKeys[0].position, Is.EqualTo(Vector3.one - before));
        }

        [Test]
        public void ResetToDefaultPointsTheFieldAtAFreshGradient()
        {
            var g = Test3DGradients.Corners8();
            field.value = g;

            ExecuteMenuAction(field.Q<ToolbarMenu>("actions"), "Reset to Default");

            Assert.That(field.value, Is.Not.SameAs(g));
            Assert.That(field.value.ColorKeys.Length, Is.EqualTo(2));
        }

        [Test]
        public void EditingFalloffPowerWritesThroughToTheGradient()
        {
            var g = Test3DGradients.Corners8();
            field.value = g;
            simulate.FrameUpdate();

            var row = field.Q<ResettableSliderRow>("falloffPower");
            Assume.That(row, Is.Not.Null);
            row.value = 6f;
            simulate.FrameUpdate();

            Assert.That(g.FalloffPower, Is.EqualTo(6f).Within(1e-4f));
        }

        /// <summary>
        /// Falloff does nothing under stepped blending, so leaving it live would present a control that
        /// silently has no effect.
        /// </summary>
        [Test]
        public void FalloffPowerIsDisabledUnderSteppedBlending()
        {
            var g = Test3DGradients.Corners8();
            field.value = g;
            simulate.FrameUpdate();

            var row = field.Q<ResettableSliderRow>("falloffPower");
            Assume.That(row.enabledSelf, Is.True);

            field.Q<EnumField>("blendMode").value = BlendMode.Stepped;
            simulate.FrameUpdate();

            Assert.That(row.enabledSelf, Is.False);
        }

        [Test]
        public void ResizingTheFieldDoesNotReRenderTheSwatch()
        {
            field.value = Test3DGradients.Corners8();
            simulate.FrameUpdate();

            var strip = field.Q<CubePreviewStripElement>("basePreview");
            int rendersAfterFirstShow = strip.BakeCount;
            Assume.That(rendersAfterFirstShow, Is.GreaterThan(0));

            foreach (int width in new[] { 200, 300, 420, 460 })
            {
                field.style.width = width;
                simulate.FrameUpdate();
            }

            // Every cube render costs a ray cast and a pass over every key per pixel, so re-rendering on
            // layout would be far more expensive here than the 1D strip it replaces.
            Assert.That(strip.BakeCount, Is.EqualTo(rendersAfterFirstShow));
        }

        [Test]
        public void ReAssigningTheSameGradientDoesNotReRenderTheSwatch()
        {
            var g = Test3DGradients.Corners8();
            field.value = g;
            simulate.FrameUpdate();

            var strip = field.Q<CubePreviewStripElement>("basePreview");
            int renders = strip.BakeCount;

            field.value = g;
            simulate.FrameUpdate();

            Assert.That(strip.BakeCount, Is.EqualTo(renders));
        }

        [Test]
        public void ARealEditReRendersEveryViewExactlyOnce()
        {
            var g = Test3DGradients.Corners8();
            field.value = g;
            simulate.FrameUpdate();

            var strip = field.Q<CubePreviewStripElement>("basePreview");
            int renders = strip.BakeCount;

            ExecuteMenuAction(field.Q<ToolbarMenu>("actions"), "Flip Keys");
            simulate.FrameUpdate();

            Assert.That(strip.BakeCount, Is.EqualTo(renders + CubePreviewRasterizer.DefaultViews.Length));
        }
    }
}
