using NUnit.Framework;
using UnityEngine;
using ABCodeworld.Gradients.Editor;

namespace ABCodeworld.Gradients.Tests.Editor.UI
{
    /// <summary>
    /// The cube viewport's projection maths, exercised directly — no panel, no layout pass, no window.
    /// The same split <see cref="BarGeometry"/> makes for the 1D bar.
    /// </summary>
    [TestFixture]
    [Category("UI")]
    internal sealed class CubeGeometryTests
    {
        private const float Side = 220f;

        private static CubeGeometry Geometry(float yaw, float pitch, float width = Side, float height = Side) =>
            new CubeGeometry(new Rect(0f, 0f, width, height), yaw, pitch);

        [Test]
        public void ThereAreTwelveDistinctEdgesTouchingAllEightCorners()
        {
            var pairs = new System.Collections.Generic.HashSet<(int, int)>();
            var corners = new System.Collections.Generic.HashSet<int>();

            for (int i = 0; i < CubeGeometry.EdgeCount; i++)
            {
                (int a, int b) = CubeGeometry.EdgeCorners(i);
                Assert.That(a, Is.Not.EqualTo(b));
                Assert.That(pairs.Add((Mathf.Min(a, b), Mathf.Max(a, b))), Is.True, $"edge {i} is a duplicate");

                // Adjacent corners differ in exactly one axis.
                int differing = a ^ b;
                Assert.That(differing == 1 || differing == 2 || differing == 4, Is.True, $"edge {i} is not axis-aligned");

                corners.Add(a);
                corners.Add(b);
            }

            Assert.That(pairs.Count, Is.EqualTo(12));
            Assert.That(corners.Count, Is.EqualTo(8));
        }

        [Test]
        public void EveryCornerStaysInsideTheFrameAtEveryRotation()
        {
            for (float yaw = 0f; yaw < 360f; yaw += 5f)
            {
                for (float pitch = -CubeGeometry.MaxPitch; pitch <= CubeGeometry.MaxPitch; pitch += 5f)
                {
                    var g = Geometry(yaw, pitch);
                    for (int i = 0; i < 8; i++)
                    {
                        Vector2 p = g.Project(CubeGeometry.Corner(i));
                        Assert.That(p.x, Is.InRange(0f, Side), $"corner {i} at yaw {yaw}, pitch {pitch}");
                        Assert.That(p.y, Is.InRange(0f, Side), $"corner {i} at yaw {yaw}, pitch {pitch}");
                    }
                }
            }
        }

        /// <summary>
        /// The cube must not visibly breathe while it is dragged. Fitting the scale to the projected
        /// corners each frame is the obvious implementation and it does exactly that, growing as the cube
        /// turns face-on and shrinking as it turns corner-on.
        /// </summary>
        [Test]
        public void TheProjectedSizeBarelyChangesAsTheCubeTurns()
        {
            float min = float.MaxValue;
            float max = float.MinValue;
            var centre = new Vector2(Side * 0.5f, Side * 0.5f);

            for (float yaw = 0f; yaw < 360f; yaw += 3f)
            {
                var g = Geometry(yaw, 30f);
                float radius = 0f;
                for (int i = 0; i < 8; i++)
                    radius = Mathf.Max(radius, Vector2.Distance(g.Project(CubeGeometry.Corner(i)), centre));

                min = Mathf.Min(min, radius);
                max = Mathf.Max(max, radius);
            }

            Assert.That((max - min) / max, Is.LessThan(0.05f), "the silhouette should stay put as the cube turns");
        }

        [Test]
        public void DepthRunsFromZeroAtTheNearestCornerToOneAtTheFurthest()
        {
            var g = Geometry(45f, 30f);

            float nearest = float.MaxValue;
            float furthest = float.MinValue;
            for (int i = 0; i < 8; i++)
            {
                float d = g.Depth(CubeGeometry.Corner(i));
                Assert.That(d, Is.InRange(0f, 1f));
                nearest = Mathf.Min(nearest, d);
                furthest = Mathf.Max(furthest, d);
            }

            // Not exactly 0 and 1: the view axis at this rotation is close to, but not exactly, the
            // cube's body diagonal, and the depth range is normalized against that diagonal.
            Assert.That(nearest, Is.LessThan(0.05f));
            Assert.That(furthest, Is.GreaterThan(0.95f));
        }

        [Test]
        public void NearerDotsAreDrawnLarger()
        {
            var g = Geometry(45f, 30f);

            // From this corner, (1,1,1) faces the eye and (0,0,0) is behind the cube.
            Rect near = g.DotRect(Vector3.one);
            Rect far = g.DotRect(Vector3.zero);

            Assert.That(near.width, Is.GreaterThan(far.width));
            Assert.That(near.width, Is.EqualTo(CubeGeometry.DotSizeNear).Within(0.5f));
            Assert.That(far.width, Is.EqualTo(CubeGeometry.DotSizeFar).Within(0.5f));
        }

        [Test]
        public void ACornerViewHidesExactlyTheThreeEdgesAtItsFarthestCorner()
        {
            var g = Geometry(45f, 30f);

            int hidden = 0;
            for (int i = 0; i < CubeGeometry.EdgeCount; i++)
            {
                if (g.IsEdgeHidden(i))
                    hidden++;
            }

            Assert.That(hidden, Is.EqualTo(3));
        }

        /// <summary>
        /// The case the "three edges at the furthest corner" shortcut gets wrong: seen face-on, the whole
        /// back face and all four side edges are behind the front face, so nine edges of twelve are hidden
        /// and only the front face's four are drawn bright.
        /// </summary>
        [Test]
        public void AFaceOnViewHidesEverythingButTheFrontFace()
        {
            var g = Geometry(0f, 0f);

            int visible = 0;
            for (int i = 0; i < CubeGeometry.EdgeCount; i++)
            {
                if (!g.IsEdgeHidden(i))
                    visible++;
            }

            Assert.That(visible, Is.EqualTo(4));
        }

        [Test]
        public void RotationIsClampedInPitchAndWrappedInYaw()
        {
            Assert.That(CubeGeometry.Rotate(0f, 0f, new Vector2(0f, 10000f)).pitch, Is.EqualTo(CubeGeometry.MaxPitch));
            Assert.That(CubeGeometry.Rotate(0f, 0f, new Vector2(0f, -10000f)).pitch, Is.EqualTo(-CubeGeometry.MaxPitch));
            Assert.That(CubeGeometry.Rotate(0f, 0f, new Vector2(10000f, 0f)).yaw, Is.InRange(0f, 360f));
        }

        [Test]
        public void DraggingRightAndLeftTurnTheCubeOppositeWays()
        {
            var right = CubeGeometry.Rotate(180f, 0f, new Vector2(20f, 0f));
            var left = CubeGeometry.Rotate(180f, 0f, new Vector2(-20f, 0f));

            Assert.That(right.yaw, Is.LessThan(180f));
            Assert.That(left.yaw, Is.GreaterThan(180f));
        }

        /// <summary>
        /// Before the first layout pass the content rect is empty. Every derived value still has to be
        /// finite and usable — the same guard <see cref="BarGeometry"/> needs, for the same reason.
        /// </summary>
        [Test]
        public void ADegenerateRectStillProducesAUsableProjection()
        {
            var g = new CubeGeometry(new Rect(0f, 0f, 0f, 0f), 45f, 30f);

            for (int i = 0; i < 8; i++)
            {
                Vector2 p = g.Project(CubeGeometry.Corner(i));
                Assert.That(float.IsNaN(p.x) || float.IsNaN(p.y), Is.False, $"corner {i}");
            }

            Assert.That(g.Viewport.width, Is.GreaterThan(0f));
        }

        [Test]
        public void TheViewportIsASquareCentredInTheFrame()
        {
            var g = Geometry(45f, 30f, width: 400f, height: 200f);
            Rect viewport = g.Viewport;

            Assert.That(viewport.width, Is.EqualTo(viewport.height));
            Assert.That(viewport.center.x, Is.EqualTo(200f).Within(1e-3f));
            Assert.That(viewport.center.y, Is.EqualTo(100f).Within(1e-3f));
            Assert.That(viewport.height, Is.LessThanOrEqualTo(200f));
        }
    }
}
