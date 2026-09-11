using NUnit.Framework;
using UnityEngine;
using ABCodeworld.Gradients.Tests.Editor.Support;
using Object = UnityEngine.Object;

namespace ABCodeworld.Gradients.Tests.Editor.Runtime
{
    [TestFixture]
    [Category("Runtime")]
    internal sealed class GradientTexture3DTests
    {
        [Test]
        public void CreateTextureProducesACubeOfTheRequestedSize()
        {
            var g = Test3DGradients.Corners8();
            var tex = GradientTexture3DUtility.CreateTexture(g, 16);
            try
            {
                Assert.That(tex.width, Is.EqualTo(16));
                Assert.That(tex.height, Is.EqualTo(16));
                Assert.That(tex.depth, Is.EqualTo(16));
                Assert.That(tex.wrapMode, Is.EqualTo(TextureWrapMode.Clamp));
                Assert.That(tex.filterMode, Is.EqualTo(FilterMode.Bilinear));
            }
            finally
            {
                Object.DestroyImmediate(tex);
            }
        }

        [Test]
        public void TheRequestedSizeIsClampedToTheSupportedRange()
        {
            var g = Test3DGradients.Corners8();
            var tex = GradientTexture3DUtility.CreateTexture(g, 1);
            try
            {
                Assert.That(tex.width, Is.EqualTo(GradientLut3D.MinSize));
            }
            finally
            {
                Object.DestroyImmediate(tex);
            }
        }

        /// <summary>
        /// The texture's linear layout has to match the flat table's, or a bake would have to be
        /// reshuffled on its way to the GPU.
        /// </summary>
        [Test]
        public void TheTextureCarriesTheSamePixelsAsTheFlatTable()
        {
            var g = Test3DGradients.Lattice27();
            const int size = 8;

            var expected = new Color32[GradientLut3D.VoxelCount(size)];
            GradientLut3D.Bake(g, expected, size, GradientLutOptions.Final);

            var tex = GradientTexture3DUtility.CreateTexture(g, size);
            try
            {
                var actual = tex.GetPixelData<Color32>(0);
                for (int i = 0; i < expected.Length; i++)
                    Assert.That(actual[i], Is.EqualTo(expected[i]), $"cell {i}");
            }
            finally
            {
                Object.DestroyImmediate(tex);
            }
        }

        [Test]
        public void UpdateTextureRebakesInPlace()
        {
            var g = Test3DGradients.Corners8();
            const int size = 8;
            var tex = GradientTexture3DUtility.CreateTexture(g, size);
            try
            {
                var before = tex.GetPixelData<Color32>(0)[0];

                g.SetColorKey(0, new ColorKey3D(Color.magenta, g.ColorKeys[0].position));
                GradientTexture3DUtility.UpdateTexture(g, tex);

                Assert.That(tex.width, Is.EqualTo(size), "the texture must not be reallocated");
                Assert.That(tex.GetPixelData<Color32>(0)[0], Is.Not.EqualTo(before));
            }
            finally
            {
                Object.DestroyImmediate(tex);
            }
        }
    }
}
