using NUnit.Framework;
using UnityEngine;
using ABCodeworld.Gradients.Editor;
using ABCodeworld.Gradients.Tests.Editor.Support;

namespace ABCodeworld.Gradients.Tests.Editor.UI
{
    /// <summary>
    /// The inspector swatch's cube renderer. Pure static and panel-free, so these assert on pixels
    /// directly rather than on anything a layout pass produced.
    /// </summary>
    [TestFixture]
    [Category("UI")]
    internal sealed class CubePreviewRasterizerTests
    {
        private const int Size = 32;

        /// <summary>
        /// The invariant the whole four-view arrangement exists for: a cube shows at most three faces from
        /// any one direction, so unless every face turns up somewhere the swatch is hiding part of the
        /// gradient.
        /// </summary>
        [Test]
        public void EverySixFacesAppearsInAtLeastOneOfTheDefaultViews()
        {
            var g = Test3DGradients.Corners8();
            var seen = new bool[6];

            foreach (var view in CubePreviewRasterizer.DefaultViews)
            {
                var pixels = new Color32[Size * Size];
                var faceIds = new sbyte[Size * Size];
                CubePreviewRasterizer.Render(g, in view, Size, pixels, faceIds, includeModulation: false);

                foreach (sbyte id in faceIds)
                {
                    if (id >= 0)
                        seen[id] = true;
                }
            }

            for (int axis = 0; axis < 3; axis++)
            {
                Assert.That(seen[CubePreviewRasterizer.FaceId(axis, -1)], Is.True, $"the negative face of axis {axis} never appears");
                Assert.That(seen[CubePreviewRasterizer.FaceId(axis, 1)], Is.True, $"the positive face of axis {axis} never appears");
            }
        }

        [Test]
        public void EachDefaultViewShowsExactlyThreeFaces()
        {
            var g = Test3DGradients.Corners8();

            foreach (var view in CubePreviewRasterizer.DefaultViews)
            {
                var pixels = new Color32[Size * Size];
                var faceIds = new sbyte[Size * Size];
                CubePreviewRasterizer.Render(g, in view, Size, pixels, faceIds, includeModulation: false);

                var faces = new System.Collections.Generic.HashSet<sbyte>();
                foreach (sbyte id in faceIds)
                {
                    if (id >= 0)
                        faces.Add(id);
                }

                Assert.That(faces.Count, Is.EqualTo(3), $"view {view.Label}");
            }
        }

        /// <summary>
        /// A cube seen from a corner is a hexagon, so the frame's own corners must be empty. If they are
        /// not, the field of view is too tight and the silhouette is being clipped.
        /// </summary>
        [Test]
        public void TheSilhouetteIsAHexagonInsideTheFrame()
        {
            var g = Test3DGradients.Corners8();

            foreach (var view in CubePreviewRasterizer.DefaultViews)
            {
                var pixels = new Color32[Size * Size];
                CubePreviewRasterizer.Render(g, in view, Size, pixels, includeModulation: false);

                foreach (int index in new[] { 0, Size - 1, (Size - 1) * Size, Size * Size - 1 })
                    Assert.That(pixels[index].a, Is.EqualTo(0), $"corner pixel {index} of view {view.Label} is not empty");

                int centre = Size / 2 * Size + Size / 2;
                Assert.That(pixels[centre].a, Is.GreaterThan(0), $"the centre of view {view.Label} should be on the cube");
            }
        }

        [Test]
        public void EdgePixelsAreNeverPartOfTheCube()
        {
            var g = Test3DGradients.Corners8();
            var view = CubePreviewRasterizer.DefaultViews[0];
            var pixels = new Color32[Size * Size];
            CubePreviewRasterizer.Render(g, in view, Size, pixels, includeModulation: false);

            for (int i = 0; i < Size; i++)
            {
                Assert.That(pixels[i].a, Is.EqualTo(0), $"bottom row, column {i}");
                Assert.That(pixels[(Size - 1) * Size + i].a, Is.EqualTo(0), $"top row, column {i}");
                Assert.That(pixels[i * Size].a, Is.EqualTo(0), $"left column, row {i}");
                Assert.That(pixels[i * Size + Size - 1].a, Is.EqualTo(0), $"right column, row {i}");
            }
        }

        /// <summary>
        /// Every visible face must be distinguishable from the other two in the same view, or a cube in a
        /// flat colour reads as an ambiguous hexagon rather than a solid.
        /// </summary>
        [Test]
        public void TheThreeFacesOfEachViewAreShadedDistinctly()
        {
            const float MinimumSeparation = 0.05f;

            foreach (var view in CubePreviewRasterizer.DefaultViews)
            {
                var shades = new System.Collections.Generic.List<float>();
                for (int axis = 0; axis < 3; axis++)
                {
                    int sign = view.Direction[axis] > 0f ? 1 : -1;
                    shades.Add(CubePreviewRasterizer.FaceShade(axis, sign));
                }

                shades.Sort();
                for (int i = 1; i < shades.Count; i++)
                    Assert.That(shades[i] - shades[i - 1], Is.GreaterThan(MinimumSeparation), $"view {view.Label}");
            }
        }

        [Test]
        public void AConstantGradientRendersAConstantPerFace()
        {
            var g = GradientABCW3D.CreateDefault();
            g.SetKeys(
                new[] { new ColorKey3D(Color.white, new Vector3(0.5f, 0.5f, 0.5f)) },
                new[] { new AlphaKey3D(1f, new Vector3(0.5f, 0.5f, 0.5f)) });

            var view = CubePreviewRasterizer.DefaultViews[0];
            var pixels = new Color32[Size * Size];
            var faceIds = new sbyte[Size * Size];
            CubePreviewRasterizer.Render(g, in view, Size, pixels, faceIds, includeModulation: false);

            var perFace = new System.Collections.Generic.Dictionary<sbyte, byte>();
            for (int i = 0; i < pixels.Length; i++)
            {
                if (faceIds[i] < 0)
                    continue;

                if (perFace.TryGetValue(faceIds[i], out byte expected))
                    Assert.That(pixels[i].r, Is.EqualTo(expected).Within(1), $"pixel {i}");
                else
                    perFace[faceIds[i]] = pixels[i].r;
            }

            Assert.That(perFace.Count, Is.EqualTo(3));
        }

        [Test]
        public void ARayStraightAtAFaceHitsIt()
        {
            bool hit = CubePreviewRasterizer.TryIntersectUnitCube(
                new Vector3(0.5f, 0.5f, 3.7f), new Vector3(0f, 0f, -1f), out float t, out int axis, out int sign);

            Assert.That(hit, Is.True);
            Assert.That(t, Is.EqualTo(2.7f).Within(1e-4f));
            Assert.That(axis, Is.EqualTo(2));
            Assert.That(sign, Is.EqualTo(1));
        }

        [Test]
        public void ARayStartingInsideReportsItsExitFace()
        {
            bool hit = CubePreviewRasterizer.TryIntersectUnitCube(
                new Vector3(0.5f, 0.5f, 0.5f), new Vector3(1f, 0f, 0f), out float t, out int axis, out int sign);

            Assert.That(hit, Is.True);
            Assert.That(t, Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(axis, Is.EqualTo(0));
            Assert.That(sign, Is.EqualTo(1));
        }

        [Test]
        public void ARayParallelToASlabAndOutsideItMisses()
        {
            bool hit = CubePreviewRasterizer.TryIntersectUnitCube(
                new Vector3(2f, 0.5f, 3.7f), new Vector3(0f, 0f, -1f), out _, out _, out _);

            Assert.That(hit, Is.False);
        }

        [Test]
        public void ARayPointingAwayMisses()
        {
            bool hit = CubePreviewRasterizer.TryIntersectUnitCube(
                new Vector3(0.5f, 0.5f, 3.7f), new Vector3(0f, 0f, 1f), out _, out _, out _);

            Assert.That(hit, Is.False);
        }
    }
}
