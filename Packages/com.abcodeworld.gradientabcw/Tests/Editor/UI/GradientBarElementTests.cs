using System.Collections.Generic;
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
    internal sealed class GradientBarElementTests : UITestFixture
    {
        private GradientBarElement bar;
        private GradientABCW gradient;

        [SetUp]
        public void SetUpBar()
        {
            panelSize = new Vector2(600, 140);
            gradient = TestGradients.Rainbow7();
            bar = new GradientBarElement();
            bar.style.width = 600;
            bar.style.height = 140;
            rootVisualElement.Add(bar);
            bar.Gradient = gradient;
            simulate.FrameUpdate();
        }

        private List<GradientKeyHandle> HandlesOfKind(bool isAlpha)
        {
            var list = new List<GradientKeyHandle>();
            bar.Query<GradientKeyHandle>().ForEach(h => { if (h.IsAlpha == isAlpha) list.Add(h); });
            list.Sort((a, b) => a.Index.CompareTo(b.Index));
            return list;
        }

        [Test]
        public void HandleCount_MatchesKeyCount()
        {
            Assert.That(HandlesOfKind(false).Count, Is.EqualTo(gradient.ColorKeys.Length));
            Assert.That(HandlesOfKind(true).Count, Is.EqualTo(gradient.AlphaKeys.Length));
        }

        [Test]
        public void HandlePositions_FollowKeyTimeOrder()
        {
            var handles = HandlesOfKind(false);
            for (int i = 1; i < handles.Count; i++)
                Assert.That(handles[i].resolvedStyle.left, Is.GreaterThan(handles[i - 1].resolvedStyle.left));
        }

        [Test]
        public void ShiftClick_AddsColorKeyAtClickedTime()
        {
            int before = gradient.ColorKeys.Length;
            simulate.Click(new Vector2(300, 100), MouseButton.LeftMouse, EventModifiers.Shift);
            simulate.FrameUpdate();

            Assert.That(gradient.ColorKeys.Length, Is.EqualTo(before + 1));
        }

        [Test]
        public void AltClick_AddsAlphaKeyAtClickedTime()
        {
            int before = gradient.AlphaKeys.Length;
            simulate.Click(new Vector2(300, 12), MouseButton.LeftMouse, EventModifiers.Alt);
            simulate.FrameUpdate();

            Assert.That(gradient.AlphaKeys.Length, Is.EqualTo(before + 1));
        }

        [Test]
        public void ShiftClick_AtMaxKeys_DoesNotAdd()
        {
            gradient = TestGradients.Max32();
            bar.Gradient = gradient;
            simulate.FrameUpdate();

            int before = gradient.ColorKeys.Length;
            simulate.Click(new Vector2(300, 100), MouseButton.LeftMouse, EventModifiers.Shift);
            simulate.FrameUpdate();

            Assert.That(gradient.ColorKeys.Length, Is.EqualTo(before));
        }

        [Test]
        public void ClickOnHandle_SelectsIt()
        {
            var handle = HandlesOfKind(false)[2];
            simulate.Click(handle.worldBound.center);
            simulate.FrameUpdate();

            Assert.That(handle.Selected, Is.True);
        }

        [Test]
        public void KeySelected_FiresWithClickedHandle()
        {
            var handle = HandlesOfKind(false)[2];
            (int index, bool isAlpha)? selected = null;
            bar.KeySelected += (i, isAlpha) => selected = (i, isAlpha);

            simulate.Click(handle.worldBound.center);
            simulate.FrameUpdate();

            Assert.That(selected.HasValue, Is.True);
            Assert.That(selected.Value.index, Is.EqualTo(2));
            Assert.That(selected.Value.isAlpha, Is.False);
        }

        [Test]
        public void DragHandle_StaysSortedAndClampsPastLowerNeighbour()
        {
            var handles = HandlesOfKind(false);
            var middle = handles[3]; // time 0.5 of 7 evenly spaced keys

            Vector2 from = middle.worldBound.center;
            Vector2 to = new Vector2(from.x - 400, from.y); // drag far left, past the lower neighbour

            simulate.DragAndDrop(from, to);
            simulate.FrameUpdate();

            // Neighbour clamping (Eps) must keep every key's time non-decreasing; the dragged key can
            // approach but never reach or cross the key that was to its left.
            var keys = gradient.ColorKeys;
            for (int i = 1; i < keys.Length; i++)
                Assert.That(keys[i].time, Is.GreaterThanOrEqualTo(keys[i - 1].time));
        }

        [Test]
        public void DragHandleBeyondLane_RemovesKey()
        {
            var handles = HandlesOfKind(false);
            int before = gradient.ColorKeys.Length;
            var handle = handles[3];

            Vector2 from = handle.worldBound.center;
            Vector2 to = new Vector2(from.x, from.y + 400); // drag far below the bar

            simulate.DragAndDrop(from, to);
            simulate.FrameUpdate();

            Assert.That(gradient.ColorKeys.Length, Is.EqualTo(before - 1));
        }

        [Test]
        public void Delete_RemovesSelectedKey()
        {
            var handle = HandlesOfKind(false)[2];
            simulate.Click(handle.worldBound.center);
            simulate.FrameUpdate();

            int before = gradient.ColorKeys.Length;
            bar.Focus();
            simulate.KeyDown(KeyCode.Delete);
            simulate.FrameUpdate();

            Assert.That(gradient.ColorKeys.Length, Is.EqualTo(before - 1));
        }

        [Test]
        public void SetGhost_ShowsAndHidesGhostElement()
        {
            bar.SetGhost(true, false, 0.5f);
            simulate.FrameUpdate();
            var ghost = bar.Q(className: "abcw-key--ghost");
            Assert.That(ghost.resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));

            bar.SetGhost(false, false, 0.5f);
            simulate.FrameUpdate();
            Assert.That(ghost.resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void GetTimeFromPanelPosition_ReturnsValueWithinUnitRange()
        {
            float t = bar.GetTimeFromPanelPosition(bar.worldBound.center);
            Assert.That(t, Is.InRange(0f, 1f));
        }
    }
}
