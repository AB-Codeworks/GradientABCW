using NUnit.Framework;
using UnityEngine;
using ABCodeworld.Gradients.Tests.Editor.Support;

namespace ABCodeworld.Gradients.Tests.Editor.Runtime
{
    [TestFixture]
    [Category("Runtime")]
    internal sealed class Gradient3DEvaluationTests
    {
        [Test]
        public void EvaluatingAtAKeyReturnsThatKeyExactly()
        {
            var g = Test3DGradients.Lattice27();

            foreach (var key in g.ColorKeys.ToArray())
            {
                var c = g.EvaluateBase(key.position);
                Assert.That(c.r, Is.EqualTo(key.color.r).Within(1e-5f), $"r at {key.position}");
                Assert.That(c.g, Is.EqualTo(key.color.g).Within(1e-5f), $"g at {key.position}");
                Assert.That(c.b, Is.EqualTo(key.color.b).Within(1e-5f), $"b at {key.position}");
            }

            foreach (var key in g.AlphaKeys.ToArray())
                Assert.That(g.EvaluateBase(key.position).a, Is.EqualTo(key.alpha).Within(1e-5f), $"a at {key.position}");
        }

        [Test]
        public void ASingleKeyIsAConstantField()
        {
            var g = Test3DGradients.Single();

            for (float x = 0f; x <= 1f; x += 0.25f)
            {
                for (float y = 0f; y <= 1f; y += 0.25f)
                {
                    for (float z = 0f; z <= 1f; z += 0.25f)
                    {
                        var c = g.EvaluateBase(new Vector3(x, y, z));
                        Assert.That(c.r, Is.EqualTo(1f).Within(1e-5f));
                        Assert.That(c.a, Is.EqualTo(1f).Within(1e-5f));
                    }
                }
            }
        }

        [Test]
        public void CoincidentKeysAverageAwayFromThePointAndTheLowerIndexWinsAtIt()
        {
            var g = GradientABCW3D.CreateDefault();
            var shared = new Vector3(0.5f, 0.5f, 0.5f);
            g.SetKeys(
                new[] { new ColorKey3D(Color.red, shared), new ColorKey3D(Color.blue, shared) },
                new[] { new AlphaKey3D(1f, shared) });

            var atPoint = g.EvaluateBase(shared);
            Assert.That(atPoint.r, Is.EqualTo(1f).Within(1e-5f), "the first of two keys sharing a position wins at it");
            Assert.That(atPoint.b, Is.EqualTo(0f).Within(1e-5f));

            var offset = g.EvaluateBase(shared + new Vector3(0f, 0f, 0.1f));
            Assert.That(offset.r, Is.EqualTo(0.5f).Within(1e-4f), "equal distance means equal weight");
            Assert.That(offset.b, Is.EqualTo(0.5f).Within(1e-4f));
        }

        [Test]
        public void SteppedTakesTheNearestKeyAndBreaksTiesTowardsTheLowerIndex()
        {
            var g = GradientABCW3D.CreateDefault();
            g.SetKeys(
                new[] { new ColorKey3D(Color.red, Vector3.zero), new ColorKey3D(Color.blue, new Vector3(1f, 0f, 0f)) },
                new[] { new AlphaKey3D(1f, Vector3.zero) });
            g.BlendMode = BlendMode.Stepped;

            Assert.That(g.EvaluateBase(new Vector3(0.2f, 0f, 0f)).r, Is.EqualTo(1f).Within(1e-5f));
            Assert.That(g.EvaluateBase(new Vector3(0.8f, 0f, 0f)).b, Is.EqualTo(1f).Within(1e-5f));

            var tie = g.EvaluateBase(new Vector3(0.5f, 0f, 0f));
            Assert.That(tie.r, Is.EqualTo(1f).Within(1e-5f), "an exactly equidistant sample takes the lower-indexed key");
        }

        [Test]
        public void SmoothBlendsMonotonicallyBetweenTwoKeys()
        {
            var g = GradientABCW3D.CreateDefault();
            g.SetKeys(
                new[] { new ColorKey3D(Color.black, Vector3.zero), new ColorKey3D(Color.white, new Vector3(1f, 0f, 0f)) },
                new[] { new AlphaKey3D(1f, Vector3.zero) });

            float previous = -1f;
            for (int i = 0; i <= 20; i++)
            {
                float value = g.EvaluateBase(new Vector3(i / 20f, 0f, 0f)).r;
                Assert.That(value, Is.GreaterThanOrEqualTo(previous - 1e-5f), $"non-monotonic at {i / 20f}");
                previous = value;
            }
        }

        [Test]
        public void HigherFalloffPullsAMidpointTowardsTheNearerKey()
        {
            var g = GradientABCW3D.CreateDefault();
            g.SetKeys(
                new[] { new ColorKey3D(Color.black, Vector3.zero), new ColorKey3D(Color.white, new Vector3(1f, 0f, 0f)) },
                new[] { new AlphaKey3D(1f, Vector3.zero) });

            // A third of the way along, so the nearer key is unambiguous.
            var sample = new Vector3(1f / 3f, 0f, 0f);

            g.FalloffPower = 1f;
            float soft = g.EvaluateBase(sample).r;

            g.FalloffPower = 8f;
            float sharp = g.EvaluateBase(sample).r;

            Assert.That(sharp, Is.LessThan(soft), "a higher power should weigh the nearer (black) key more");
            Assert.That(sharp, Is.GreaterThanOrEqualTo(0f));
        }

        [Test]
        public void FalloffHasNoEffectUnderSteppedBlending()
        {
            var g = Test3DGradients.Stepped();
            var sample = new Vector3(0.3f, 0.7f, 0.2f);

            g.FalloffPower = 1f;
            var soft = g.EvaluateBase(sample);
            g.FalloffPower = 8f;
            var sharp = g.EvaluateBase(sample);

            Assert.That(sharp, Is.EqualTo(soft));
        }

        [Test]
        public void ColourAndAlphaKeyCountsAreIndependent()
        {
            var g = GradientABCW3D.CreateDefault();
            g.SetKeys(
                new[] { new ColorKey3D(Color.green, new Vector3(0.5f, 0.5f, 0.5f)) },
                new[]
                {
                    new AlphaKey3D(0f, Vector3.zero),
                    new AlphaKey3D(1f, Vector3.one),
                    new AlphaKey3D(0.5f, new Vector3(1f, 0f, 0f)),
                });

            Assert.That(g.ColorKeys.Length, Is.EqualTo(1));
            Assert.That(g.AlphaKeys.Length, Is.EqualTo(3));

            var c = g.EvaluateBase(Vector3.zero);
            Assert.That(c.g, Is.EqualTo(1f).Within(1e-5f), "the lone colour key covers the whole cube");
            Assert.That(c.a, Is.EqualTo(0f).Within(1e-5f), "alpha still varies");
        }

        /// <summary>
        /// The property the whole weight formulation exists to guarantee. Inverse-distance weighting
        /// written the obvious way overflows to infinity near a key at a high power, and infinity over
        /// infinity is NaN; normalising by the nearest distance makes that unreachable. This sweep is what
        /// would catch a regression back to the obvious form.
        /// </summary>
        [Test]
        public void EveryOutputIsFiniteAndInRangeAtEveryFalloffPower()
        {
            var g = Test3DGradients.Max64();

            // Two keys a hair apart, which is the input the naive weighting cannot survive.
            var keys = new ColorKey3D[GradientABCW3D.MaxKeys];
            g.ColorKeys.CopyTo(keys);
            keys[1] = new ColorKey3D(Color.magenta, keys[0].position + new Vector3(1e-6f, 0f, 0f));
            g.SetKeys(keys, g.AlphaKeys.ToArray());

            foreach (float power in new[] { 1f, 2f, 4f, 8f })
            {
                g.FalloffPower = power;

                for (int i = 0; i <= 16; i++)
                {
                    for (int j = 0; j <= 16; j++)
                    {
                        for (int k = 0; k <= 16; k++)
                        {
                            var c = g.Evaluate(new Vector3(i / 16f, j / 16f, k / 16f));
                            Assert.That(float.IsNaN(c.r) || float.IsNaN(c.g) || float.IsNaN(c.b) || float.IsNaN(c.a),
                                Is.False, $"NaN at power {power}, ({i}, {j}, {k})");
                            Assert.That(float.IsInfinity(c.r) || float.IsInfinity(c.a),
                                Is.False, $"infinity at power {power}, ({i}, {j}, {k})");
                            Assert.That(c.r, Is.InRange(-1e-4f, 1f + 1e-4f));
                            Assert.That(c.a, Is.InRange(-1e-4f, 1f + 1e-4f));
                        }
                    }
                }
            }
        }

        [Test]
        public void EvaluateMatchesEvaluateBaseWhenModulationIsIdentityOrBypassed()
        {
            var identity = Test3DGradients.Corners8();
            var bypassed = Test3DGradients.Bypassed();

            for (int i = 0; i <= 4; i++)
            {
                var p = new Vector3(i / 4f, 1f - i / 4f, 0.3f);
                Assert.That(identity.Evaluate(p), Is.EqualTo(identity.EvaluateBase(p)));
                Assert.That(bypassed.Evaluate(p), Is.EqualTo(bypassed.EvaluateBase(p)));
            }
        }

        [Test]
        public void PositionsOutsideTheCubeClampOntoIt()
        {
            var g = Test3DGradients.Corners8();

            Assert.That(g.EvaluateBase(new Vector3(-5f, -5f, -5f)), Is.EqualTo(g.EvaluateBase(Vector3.zero)));
            Assert.That(g.EvaluateBase(new Vector3(5f, 5f, 5f)), Is.EqualTo(g.EvaluateBase(Vector3.one)));
        }
    }
}
