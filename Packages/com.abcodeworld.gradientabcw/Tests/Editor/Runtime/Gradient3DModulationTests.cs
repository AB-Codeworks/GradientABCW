using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using ABCodeworld.Gradients.Tests.Editor.Support;

namespace ABCodeworld.Gradients.Tests.Editor.Runtime
{
    /// <summary>
    /// Modulation of the 3D domain, which is the 1D transform applied independently to x, y and z.
    /// </summary>
    [TestFixture]
    [Category("Runtime")]
    internal sealed class Gradient3DModulationTests
    {
        private static GradientABCW3D WithDomain(bool reverse, float repeats, RepeatMode mode, float offset)
        {
            var g = Test3DGradients.Corners8();
            g.Modulation = new GradientModulation
            {
                bypass = false,
                reverse = reverse,
                repeats = repeats,
                repeatMode = mode,
                offset = offset,
            };
            return g;
        }

        /// <summary>
        /// The defining property: modulating the domain and then sampling is the same as transforming the
        /// point yourself and sampling the unmodulated gradient there.
        /// </summary>
        [TestCase(false, 1f, RepeatMode.Clamp, 0f)]
        [TestCase(true, 1f, RepeatMode.Clamp, 0f)]
        [TestCase(false, 2f, RepeatMode.Wrap, 0f)]
        [TestCase(false, 2f, RepeatMode.Mirror, 0f)]
        [TestCase(false, 3.5f, RepeatMode.Mirror, 0.25f)]
        [TestCase(true, 2f, RepeatMode.Wrap, 0.1f)]
        [TestCase(false, 1f, RepeatMode.Clamp, 0.3f)]
        public void DomainModulationIsThePerAxisTransformOfThePoint(bool reverse, float repeats, RepeatMode mode, float offset)
        {
            var modulated = WithDomain(reverse, repeats, mode, offset);
            var plain = Test3DGradients.Corners8();

            for (int i = 0; i <= 4; i++)
            {
                for (int j = 0; j <= 4; j++)
                {
                    for (int k = 0; k <= 4; k++)
                    {
                        var p = new Vector3(i / 4f, j / 4f, k / 4f);
                        float3 transformed = GradientMath3D.TransformP(in modulated.Native, new float3(p.x, p.y, p.z));

                        var actual = modulated.Evaluate(p);
                        var expected = plain.EvaluateBase(new Vector3(transformed.x, transformed.y, transformed.z));

                        Assert.That(actual.r, Is.EqualTo(expected.r).Within(1e-4f), $"r at {p}");
                        Assert.That(actual.g, Is.EqualTo(expected.g).Within(1e-4f), $"g at {p}");
                        Assert.That(actual.b, Is.EqualTo(expected.b).Within(1e-4f), $"b at {p}");
                        Assert.That(actual.a, Is.EqualTo(expected.a).Within(1e-4f), $"a at {p}");
                    }
                }
            }
        }

        /// <summary>
        /// Reverse mirrors all three axes at once, which is a point reflection through the centre of the
        /// cube rather than a rotation of it. Worth pinning, because it flips handedness and that is not
        /// what "reverse" suggests to someone coming from the 1D gradient.
        /// </summary>
        [Test]
        public void ReverseIsAPointReflectionThroughTheCentre()
        {
            var reversed = WithDomain(reverse: true, repeats: 1f, RepeatMode.Clamp, offset: 0f);
            var plain = Test3DGradients.Corners8();

            for (int i = 1; i < 4; i++)
            {
                var p = new Vector3(i / 4f, 1f - i / 4f, 0.25f);
                var mirrored = Vector3.one - p;

                var actual = reversed.Evaluate(p);
                var expected = plain.EvaluateBase(mirrored);

                Assert.That(actual.r, Is.EqualTo(expected.r).Within(1e-3f), $"r at {p}");
                Assert.That(actual.b, Is.EqualTo(expected.b).Within(1e-3f), $"b at {p}");
            }
        }

        /// <summary>
        /// Repeats apply per axis, so a count of 2 tiles the cube 2x2x2 — eight times over, not twice.
        /// </summary>
        [Test]
        public void RepeatsTileEveryAxis()
        {
            var g = WithDomain(reverse: false, repeats: 2f, RepeatMode.Wrap, offset: 0f);

            for (int i = 0; i < 4; i++)
            {
                var inFirstTile = new Vector3(0.1f + i * 0.05f, 0.15f, 0.2f);
                var inLastTile = inFirstTile + new Vector3(0.5f, 0.5f, 0.5f);

                Assert.That(g.Evaluate(inLastTile).r, Is.EqualTo(g.Evaluate(inFirstTile).r).Within(1e-4f));
                Assert.That(g.Evaluate(inLastTile).b, Is.EqualTo(g.Evaluate(inFirstTile).b).Within(1e-4f));
            }
        }

        [Test]
        public void BypassSkipsEveryModulationParameter()
        {
            var g = Test3DGradients.Bypassed();
            var plain = Test3DGradients.Corners8();

            for (int i = 0; i <= 4; i++)
            {
                var p = new Vector3(i / 4f, 0.5f, 0.25f);
                Assert.That(g.Evaluate(p), Is.EqualTo(plain.EvaluateBase(p)));
            }
        }

        [Test]
        public void ColourModulationDoesNotMoveTheSamplePoint()
        {
            var g = Test3DGradients.Corners8();
            g.Modulation = new GradientModulation
            {
                repeats = 1f,
                repeatMode = RepeatMode.Clamp,
                brightness = 0.5f,
            };

            // Brightness lerps towards white, so a key's own colour should move exactly halfway there and
            // nowhere else — proving the point being sampled is untouched.
            var key = g.ColorKeys[0];
            var c = g.Evaluate(key.position);

            Assert.That(c.r, Is.EqualTo(Mathf.Lerp(key.color.r, 1f, 0.5f)).Within(1e-4f));
            Assert.That(c.g, Is.EqualTo(Mathf.Lerp(key.color.g, 1f, 0.5f)).Within(1e-4f));
            Assert.That(c.b, Is.EqualTo(Mathf.Lerp(key.color.b, 1f, 0.5f)).Within(1e-4f));
        }

        /// <summary>
        /// The transform pulls a sample at exactly 1 just inside the domain, so the far corner of the cube
        /// lands a hair short of (1, 1, 1) on every axis. Harmless, but non-obvious enough to pin.
        /// </summary>
        [Test]
        public void TheFarCornerIsPulledJustInsideTheDomain()
        {
            var g = WithDomain(reverse: false, repeats: 1f, RepeatMode.Wrap, offset: 0f);
            float3 transformed = GradientMath3D.TransformP(in g.Native, new float3(1f, 1f, 1f));

            Assert.That(transformed.x, Is.LessThan(1f));
            Assert.That(transformed.x, Is.GreaterThan(0.999f));
            Assert.That(transformed.y, Is.EqualTo(transformed.x));
            Assert.That(transformed.z, Is.EqualTo(transformed.x));
        }
    }
}
