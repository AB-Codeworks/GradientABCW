using NUnit.Framework;
using UnityEngine;
using ABCodeworld.Gradients.Tests.Editor.Support;

namespace ABCodeworld.Gradients.Tests.Editor.Runtime
{
    [TestFixture]
    [Category("Runtime")]
    internal sealed class GradientTextureTests
    {
        private Texture2D texture;

        [TearDown]
        public void TearDown()
        {
            if (texture != null)
                Object.DestroyImmediate(texture);
            texture = null;
        }

        [Test]
        public void CreateTexture_HasExpectedFormatAndSettings()
        {
            var g = TestGradients.Rainbow7();
            texture = GradientTextureUtility.CreateTexture(g, 128);

            Assert.That(texture.width, Is.EqualTo(128));
            Assert.That(texture.height, Is.EqualTo(1));
            Assert.That(texture.format, Is.EqualTo(TextureFormat.RGBA32));
            Assert.That(texture.wrapMode, Is.EqualTo(TextureWrapMode.Clamp));
            Assert.That(texture.filterMode, Is.EqualTo(FilterMode.Bilinear));
        }

        [Test]
        public void CreateTexture_PixelsMatchLut()
        {
            var g = TestGradients.WithModulation();
            const int width = 64;
            texture = GradientTextureUtility.CreateTexture(g, width, GradientLutOptions.Final);

            var lut = new Color32[width];
            GradientLut.Bake(g, lut, GradientLutOptions.Final);

            var pixels = texture.GetPixels32();
            for (int i = 0; i < width; i++)
                Assert.That(pixels[i], Is.EqualTo(lut[i]));
        }

        [Test]
        public void UpdateTexture_KeepsSameInstance()
        {
            var g = TestGradients.Rainbow7();
            texture = GradientTextureUtility.CreateTexture(g, 32);
            var entityIdBefore = texture.GetEntityId();

            g.BlendMode = BlendMode.Stepped;
            GradientTextureUtility.UpdateTexture(g, texture);

            Assert.That(texture.GetEntityId(), Is.EqualTo(entityIdBefore));
        }
    }
}
