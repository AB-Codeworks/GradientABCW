using NUnit.Framework;
using UnityEngine;
using ABCodeworld.Gradients.Editor;

namespace ABCodeworld.Gradients.Tests.Editor.UI
{
    /// <summary>
    /// The gradient bar's layout arithmetic, exercised directly. This used to be private to
    /// <see cref="GradientBarElement"/> and reachable only through a panel and a layout pass.
    /// </summary>
    [TestFixture]
    [Category("UI")]
    internal sealed class BarGeometryTests
    {
        private static BarGeometry Geometry(float width = 400f, float height = 100f) =>
            new BarGeometry(new Rect(0f, 0f, width, height));

        [Test]
        public void StripIsInsetByThePaddingAndTheTwoLanes()
        {
            var g = Geometry();

            Assert.That(g.Strip.x, Is.EqualTo(BarGeometry.PadX));
            Assert.That(g.Strip.width, Is.EqualTo(400f - 2f * BarGeometry.PadX));
            Assert.That(g.Strip.y, Is.EqualTo(BarGeometry.LaneHeight));
            Assert.That(g.Strip.height, Is.EqualTo(100f - 2f * BarGeometry.LaneHeight));
        }

        [Test]
        public void TimeAtMapsTheStripEdgesToZeroAndOne()
        {
            var g = Geometry();

            Assert.That(g.TimeAt(new Vector2(g.Strip.xMin, 50f)), Is.EqualTo(0f).Within(1e-6f));
            Assert.That(g.TimeAt(new Vector2(g.Strip.xMax, 50f)), Is.EqualTo(1f).Within(1e-6f));
            Assert.That(g.TimeAt(new Vector2(g.Strip.center.x, 50f)), Is.EqualTo(0.5f).Within(1e-6f));
        }

        [Test]
        public void TimeAtClampsOutsideTheStrip()
        {
            var g = Geometry();

            Assert.That(g.TimeAt(new Vector2(-500f, 50f)), Is.EqualTo(0f));
            Assert.That(g.TimeAt(new Vector2(5000f, 50f)), Is.EqualTo(1f));
        }

        [Test]
        public void HandlesAreCentredOnTheirTime()
        {
            var g = Geometry();

            var atHalf = g.HandleRect(0.5f, isAlpha: false);
            Assert.That(atHalf.center.x, Is.EqualTo(g.Strip.center.x).Within(1e-4f));
            Assert.That(atHalf.width, Is.EqualTo(BarGeometry.HandleWidth));
        }

        [Test]
        public void AlphaAndColourHandlesSitInOppositeLanes()
        {
            var g = Geometry();

            var alpha = g.HandleRect(0.5f, isAlpha: true);
            var colour = g.HandleRect(0.5f, isAlpha: false);

            Assert.That(alpha.y, Is.LessThan(g.Strip.yMin), "the alpha lane is above the strip");
            Assert.That(colour.y, Is.GreaterThan(g.Strip.yMax - BarGeometry.HandleHeight), "the colour lane is below the strip");
        }

        [Test]
        public void ADragIsOnlyOutsideItsOwnLane()
        {
            var g = Geometry();

            // Dragging an alpha key upward off the top removes it; dragging it down does not.
            Assert.That(g.IsOutsideLane(isAlpha: true, new Vector2(200f, -BarGeometry.RemoveThreshold - 1f)), Is.True);
            Assert.That(g.IsOutsideLane(isAlpha: true, new Vector2(200f, 150f)), Is.False);

            // And the mirror image for a colour key.
            Assert.That(g.IsOutsideLane(isAlpha: false, new Vector2(200f, 100f + BarGeometry.RemoveThreshold + 1f)), Is.True);
            Assert.That(g.IsOutsideLane(isAlpha: false, new Vector2(200f, -50f)), Is.False);
        }

        [Test]
        public void ADegenerateRectStillProducesAUsableStrip()
        {
            // Before the first layout pass the content rect is empty; the maths must not divide by zero.
            var g = new BarGeometry(new Rect(0f, 0f, 0f, 0f));

            Assert.That(g.Strip.width, Is.GreaterThan(0f));
            Assert.That(g.Strip.height, Is.GreaterThan(0f));
            Assert.That(g.TimeAt(new Vector2(0f, 0f)), Is.InRange(0f, 1f));
        }
    }
}
