using NUnit.Framework;
using UnityEngine;
using ABCodeworld.Gradients.Editor;

namespace ABCodeworld.Gradients.Tests.Editor.Sampling
{
    [TestFixture]
    [Category("Sampling")]
    internal sealed class ColorSamplerTests
    {
        private readonly struct ArrayColorSource : IColorSource
        {
            private readonly Color32[] colors;
            public ArrayColorSource(Color32[] colors) => this.colors = colors;
            public int Count => colors.Length;
            public Color32 this[int index] => colors[index];
        }

        private static Color32[] RandomColors(int count, uint seed)
        {
            var rng = new Unity.Mathematics.Random(seed);
            var colors = new Color32[count];
            for (int i = 0; i < count; i++)
                colors[i] = new Color32((byte)rng.NextInt(0, 256), (byte)rng.NextInt(0, 256), (byte)rng.NextInt(0, 256), 255);
            return colors;
        }

        [Test]
        public void SampleRandom_SameSeed_ProducesIdenticalResult()
        {
            var source = new ArrayColorSource(RandomColors(64, 1));

            ColorSampler.TrySampleRandom(source, 8, 12345, out var a, out _);
            ColorSampler.TrySampleRandom(source, 8, 12345, out var b, out _);

            Assert.That(a.ContentEquals(b), Is.True);
        }

        [Test]
        public void SampleRandom_DifferentSeed_UsuallyDiffers()
        {
            var source = new ArrayColorSource(RandomColors(64, 1));

            ColorSampler.TrySampleRandom(source, 8, 1, out var a, out _);
            ColorSampler.TrySampleRandom(source, 8, 2, out var b, out _);

            Assert.That(a.ContentEquals(b), Is.False);
        }

        [Test]
        public void SampleRandom_RespectsKeyCount()
        {
            var source = new ArrayColorSource(RandomColors(64, 1));
            ColorSampler.TrySampleRandom(source, 12, 42, out var result, out _);

            Assert.That(result.ColorKeys.Length, Is.EqualTo(12));
            Assert.That(result.AlphaKeys.Length, Is.EqualTo(12));
        }

        [Test]
        public void SampleRandom_ResultIsSorted()
        {
            var source = new ArrayColorSource(RandomColors(64, 1));
            ColorSampler.TrySampleRandom(source, 16, 7, out var result, out _);

            var keys = result.ColorKeys;
            for (int i = 1; i < keys.Length; i++)
                Assert.That(keys[i].time, Is.GreaterThanOrEqualTo(keys[i - 1].time));
        }

        [Test]
        public void SampleRandom_EmptySource_Fails()
        {
            var source = new ArrayColorSource(System.Array.Empty<Color32>());
            bool ok = ColorSampler.TrySampleRandom(source, 8, 1, out _, out string error);

            Assert.That(ok, Is.False);
            Assert.That(error, Is.Not.Null);
        }

        [Test]
        public void SampleRandom_SingleColorSource_FallsBackToTwoKeys()
        {
            var source = new ArrayColorSource(new[] { new Color32(255, 0, 0, 255) });
            bool ok = ColorSampler.TrySampleRandom(source, 8, 1, out _, out string error);

            Assert.That(ok, Is.False); // fewer than 2 distinct source colors
            Assert.That(error, Is.Not.Null);
        }

        [Test]
        public void SampleRandom_TwoColorSource_Succeeds()
        {
            var source = new ArrayColorSource(new[] { new Color32(255, 0, 0, 255), new Color32(0, 0, 255, 255) });
            bool ok = ColorSampler.TrySampleRandom(source, 4, 1, out var result, out string error);

            Assert.That(ok, Is.True);
            Assert.That(error, Is.Null);
            Assert.That(result.ColorKeys.Length, Is.EqualTo(4));
        }

        [Test]
        public void SampleStrict_SingleRowTexture_ProducesTwoIdenticalKeys()
        {
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, Color.green);
            tex.Apply(false);

            bool ok = ColorSampler.TrySampleStrict(tex, out var result, out string error);

            Assert.That(ok, Is.True);
            Assert.That(error, Is.Null);
            Assert.That(result.ColorKeys.Length, Is.EqualTo(2));
            Assert.That(result.ColorKeys[0].color.g, Is.EqualTo(1f).Within(1e-3f));
            Assert.That(result.ColorKeys[1].color.g, Is.EqualTo(1f).Within(1e-3f));

            Object.DestroyImmediate(tex);
        }

        [Test]
        public void SampleStrict_MultiRowTexture_ProducesOrderedKeys()
        {
            const int height = 4;
            var tex = new Texture2D(1, height, TextureFormat.RGBA32, false);
            for (int y = 0; y < height; y++)
                tex.SetPixel(0, y, new Color(y / (float)(height - 1), 0f, 0f));
            tex.Apply(false);

            bool ok = ColorSampler.TrySampleStrict(tex, out var result, out _);

            Assert.That(ok, Is.True);
            Assert.That(result.ColorKeys.Length, Is.EqualTo(height));

            var keys = result.ColorKeys;
            for (int i = 1; i < keys.Length; i++)
                Assert.That(keys[i].time, Is.GreaterThan(keys[i - 1].time));

            Object.DestroyImmediate(tex);
        }

        [Test]
        public void SampleStrict_CapsAtSixteenKeys()
        {
            const int height = 40;
            var tex = new Texture2D(1, height, TextureFormat.RGBA32, false);
            for (int y = 0; y < height; y++)
                tex.SetPixel(0, y, Color.white);
            tex.Apply(false);

            bool ok = ColorSampler.TrySampleStrict(tex, out var result, out _);

            Assert.That(ok, Is.True);
            Assert.That(result.ColorKeys.Length, Is.EqualTo(16));

            Object.DestroyImmediate(tex);
        }

        [Test]
        public void SampleStrict_UnreadableTexture_ReportsError()
        {
            bool ok = ColorSampler.TrySampleStrict(null, out _, out string error);
            Assert.That(ok, Is.False);
            Assert.That(error, Is.Not.Null);
        }
    }
}
