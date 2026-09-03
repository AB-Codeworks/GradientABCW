using NUnit.Framework;
using UnityEngine;
using ABCodeworld.Gradients.Tests.Editor.Support;

namespace ABCodeworld.Gradients.Tests.Editor.Runtime
{
    [TestFixture]
    [Category("Runtime")]
    internal sealed class GradientHashTests
    {
        [Test]
        public void ComputeContentHash_StableAcrossCalls()
        {
            var g = TestGradients.Rainbow7();
            Assert.That(g.ComputeContentHash(), Is.EqualTo(g.ComputeContentHash()));
        }

        [Test]
        public void ComputeContentHash_EqualForClone()
        {
            var g = TestGradients.WithModulation();
            var clone = g.Clone();
            Assert.That(clone.ComputeContentHash(), Is.EqualTo(g.ComputeContentHash()));
            Assert.That(g.ContentEquals(clone), Is.True);
        }

        [Test]
        public void ComputeContentHash_UnaffectedByVersion()
        {
            var g = TestGradients.Rainbow7();
            int hashBefore = g.ComputeContentHash();
            int versionBefore = g.Version;

            // Force a version bump with no content change (re-clamp a modulation to itself).
            g.Modulation = g.Modulation;

            Assert.That(g.Version, Is.GreaterThan(versionBefore));
            Assert.That(g.ComputeContentHash(), Is.EqualTo(hashBefore));
        }

        [Test]
        public void ComputeContentHash_ChangesWithColorKey()
        {
            var g = TestGradients.Rainbow7();
            int before = g.ComputeContentHash();
            g.SetColorKey(0, new ColorKey(Color.magenta, g.ColorKeys[0].time));
            Assert.That(g.ComputeContentHash(), Is.Not.EqualTo(before));
        }

        [Test]
        public void ComputeContentHash_ChangesWithAlphaKey()
        {
            var g = TestGradients.Rainbow7();
            int before = g.ComputeContentHash();
            g.SetAlphaKey(0, new AlphaKey(0.1f, g.AlphaKeys[0].time));
            Assert.That(g.ComputeContentHash(), Is.Not.EqualTo(before));
        }

        [Test]
        public void ComputeContentHash_ChangesWithBlendMode()
        {
            var g = TestGradients.Rainbow7();
            int before = g.ComputeContentHash();
            g.BlendMode = BlendMode.Stepped;
            Assert.That(g.ComputeContentHash(), Is.Not.EqualTo(before));
        }

        [TestCase(nameof(GradientModulation.reverse))]
        [TestCase(nameof(GradientModulation.repeats))]
        [TestCase(nameof(GradientModulation.repeatMode))]
        [TestCase(nameof(GradientModulation.offset))]
        [TestCase(nameof(GradientModulation.hueShift))]
        [TestCase(nameof(GradientModulation.saturation))]
        [TestCase(nameof(GradientModulation.brightness))]
        [TestCase(nameof(GradientModulation.alpha))]
        [TestCase(nameof(GradientModulation.bypass))]
        public void ComputeContentHash_ChangesForEveryModulationField(string field)
        {
            var g = TestGradients.Rainbow7();
            int before = g.ComputeContentHash();

            var m = g.Modulation;
            switch (field)
            {
                case nameof(GradientModulation.reverse): m.reverse = !m.reverse; break;
                case nameof(GradientModulation.repeats): m.repeats = 4.2f; break;
                case nameof(GradientModulation.repeatMode): m.repeatMode = RepeatMode.Wrap; break;
                case nameof(GradientModulation.offset): m.offset = 0.66f; break;
                case nameof(GradientModulation.hueShift): m.hueShift = 0.4f; break;
                case nameof(GradientModulation.saturation): m.saturation = 0.4f; break;
                case nameof(GradientModulation.brightness): m.brightness = 0.4f; break;
                case nameof(GradientModulation.alpha): m.alpha = 0.4f; break;
                case nameof(GradientModulation.bypass): m.bypass = true; break;
            }
            g.Modulation = m;

            Assert.That(g.ComputeContentHash(), Is.Not.EqualTo(before));
        }

        [Test]
        public void ContentEquals_FalseWhenKeyCountDiffers()
        {
            var a = TestGradients.Rainbow7();
            var b = TestGradients.Default();
            Assert.That(a.ContentEquals(b), Is.False);
        }
    }
}
