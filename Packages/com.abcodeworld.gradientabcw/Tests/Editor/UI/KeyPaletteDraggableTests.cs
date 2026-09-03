using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.UIElements.TestFramework;
using ABCodeworld.Gradients.Editor;
using ABCodeworld.Gradients.Tests.Editor.Support;

namespace ABCodeworld.Gradients.Tests.Editor.UI
{
    [TestFixture]
    [Category("UI")]
    internal sealed class KeyPaletteDraggableTests : UITestFixture
    {
        private GradientBarElement bar;
        private KeyPaletteDraggable colorPalette;
        private KeyPaletteDraggable alphaPalette;
        private GradientABCW gradient;

        [SetUp]
        public void SetUpPalette()
        {
            panelSize = new Vector2(900, 200);
            gradient = TestGradients.Rainbow7();

            var root = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            colorPalette = new KeyPaletteDraggable(false);
            colorPalette.style.width = 84;
            colorPalette.style.height = 40;
            colorPalette.SetColor(Color.red);
            alphaPalette = new KeyPaletteDraggable(true);
            alphaPalette.style.width = 84;
            alphaPalette.style.height = 40;
            root.Add(colorPalette);
            root.Add(alphaPalette);

            bar = new GradientBarElement();
            bar.style.width = 600;
            bar.style.height = 140;
            root.Add(bar);
            bar.Gradient = gradient;

            rootVisualElement.Add(root);
            simulate.FrameUpdate();

            colorPalette.Dropped += OnDropped;
            alphaPalette.Dropped += OnDropped;
        }

        private void OnDropped(Vector2 panelPosition, bool isAlpha)
        {
            float t = bar.GetTimeFromPanelPosition(panelPosition);
            if (isAlpha)
            {
                if (gradient.CanAddAlphaKey)
                    gradient.AddAlphaKey(gradient.EvaluateBase(t).a, t);
            }
            else if (gradient.CanAddColorKey)
            {
                gradient.AddColorKey(gradient.EvaluateBase(t), t);
            }
        }

        [Test]
        public void DragFromColorPalette_ToBar_AddsColorKey()
        {
            int before = gradient.ColorKeys.Length;
            simulate.DragAndDrop(colorPalette.worldBound.center, bar.worldBound.center);
            simulate.FrameUpdate();

            Assert.That(gradient.ColorKeys.Length, Is.EqualTo(before + 1));
        }

        [Test]
        public void DragFromAlphaPalette_ToBar_AddsAlphaKey()
        {
            int before = gradient.AlphaKeys.Length;
            simulate.DragAndDrop(alphaPalette.worldBound.center, bar.worldBound.center);
            simulate.FrameUpdate();

            Assert.That(gradient.AlphaKeys.Length, Is.EqualTo(before + 1));
        }

        [Test]
        public void Dragging_FiresDuringDrag()
        {
            bool dragged = false;
            colorPalette.Dragging += (_, _) => dragged = true;

            simulate.DragAndDrop(colorPalette.worldBound.center, bar.worldBound.center);
            simulate.FrameUpdate();

            Assert.That(dragged, Is.True);
        }
    }
}
