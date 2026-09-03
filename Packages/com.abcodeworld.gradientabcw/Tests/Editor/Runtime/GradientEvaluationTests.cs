using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using ABCodeworld.Gradients.Tests.Editor.Support;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace ABCodeworld.Gradients.Tests.Editor.Runtime
{
    [TestFixture]
    [Category("Runtime")]
    internal sealed class GradientEvaluationTests
    {
        [Test]
        public void Smooth_AtKeyTimes_ReturnsKeyColor()
        {
            var g = TestGradients.Rainbow7();
            var keys = g.ColorKeys;
            for (int i = 0; i < keys.Length; i++)
            {
                var c = g.EvaluateBase(keys[i].time);
                Assert.That(c.r, Is.EqualTo(keys[i].color.r).Within(1e-5f));
                Assert.That(c.g, Is.EqualTo(keys[i].color.g).Within(1e-5f));
                Assert.That(c.b, Is.EqualTo(keys[i].color.b).Within(1e-5f));
            }
        }

        [Test]
        public void Smooth_BetweenKeys_Interpolates()
        {
            var g = GradientABCW.CreateDefault();
            g.SetKeys(
                new[] { new ColorKey(Color.black, 0f), new ColorKey(Color.white, 1f) },
                new[] { new AlphaKey(1f, 0f), new AlphaKey(1f, 1f) });

            var c = g.EvaluateBase(0.5f);
            Assert.That(c.r, Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(c.g, Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(c.b, Is.EqualTo(0.5f).Within(1e-5f));
        }

        [Test]
        public void Smooth_OutsideKeyRange_ClampsToEndpoints()
        {
            var g = TestGradients.Rainbow7();
            var first = g.ColorKeys[0].color;
            var last = g.ColorKeys[g.ColorKeys.Length - 1].color;

            Assert.That(g.EvaluateBase(-0.5f).r, Is.EqualTo(first.r).Within(1e-5f));
            Assert.That(g.EvaluateBase(1.5f).r, Is.EqualTo(last.r).Within(1e-5f));
        }

        [Test]
        public void Stepped_AtMidpoint_SwitchesToNextKey()
        {
            var g = GradientABCW.CreateDefault();
            g.BlendMode = BlendMode.Stepped;
            g.SetKeys(
                new[] { new ColorKey(Color.red, 0f), new ColorKey(Color.blue, 1f) },
                new[] { new AlphaKey(1f, 0f), new AlphaKey(1f, 1f) });

            Assert.That(g.EvaluateBase(0.4f).r, Is.EqualTo(1f).Within(1e-5f)); // still red
            Assert.That(g.EvaluateBase(0.6f).b, Is.EqualTo(1f).Within(1e-5f)); // now blue
        }

        [Test]
        public void TwoKeyGradient_EvaluatesWithoutError()
        {
            var g = TestGradients.TwoKey();
            Assert.DoesNotThrow(() => g.Evaluate(0.37f));
        }

        [Test]
        public void MaxKeyGradient_EvaluatesWithoutError()
        {
            var g = TestGradients.Max32();
            Assert.DoesNotThrow(() => g.Evaluate(0.5f));
        }

        [Test]
        public void DuplicateTimes_DoNotThrow()
        {
            var g = GradientABCW.CreateDefault();
            g.SetKeys(
                new[] { new ColorKey(Color.red, 0.5f), new ColorKey(Color.blue, 0.5f) },
                new[] { new AlphaKey(1f, 0.5f), new AlphaKey(0.5f, 0.5f) });

            Assert.DoesNotThrow(() => g.Evaluate(0.5f));
        }

        [Test]
        public void Alpha_IndependentOfColor()
        {
            var g = GradientABCW.CreateDefault();
            g.SetKeys(
                new[] { new ColorKey(Color.red, 0f), new ColorKey(Color.red, 1f) },
                new[] { new AlphaKey(0f, 0f), new AlphaKey(1f, 1f) });

            Assert.That(g.EvaluateBase(0.25f).a, Is.EqualTo(0.25f).Within(1e-5f));
            Assert.That(g.EvaluateBase(0.25f).r, Is.EqualTo(1f).Within(1e-5f));
        }

        [Test]
        public void Evaluate_EqualsEvaluateBase_WhenModulationIsIdentity()
        {
            var g = TestGradients.Rainbow7();
            for (float t = 0f; t <= 1f; t += 0.1f)
            {
                var a = g.Evaluate(t);
                var b = g.EvaluateBase(t);
                Assert.That(a.r, Is.EqualTo(b.r).Within(1e-6f));
                Assert.That(a.g, Is.EqualTo(b.g).Within(1e-6f));
                Assert.That(a.b, Is.EqualTo(b.b).Within(1e-6f));
                Assert.That(a.a, Is.EqualTo(b.a).Within(1e-6f));
            }
        }

        [Test]
        public void Evaluate_EqualsEvaluateBase_WhenBypassed()
        {
            var g = TestGradients.Bypassed();
            for (float t = 0f; t <= 1f; t += 0.1f)
            {
                var a = g.Evaluate(t);
                var b = g.EvaluateBase(t);
                Assert.That(a.r, Is.EqualTo(b.r).Within(1e-6f));
            }
        }

        [Test]
        public void Evaluate_NoAllocations()
        {
            var g = TestGradients.WithModulation();
            g.Evaluate(0.5f); // warm the native cache first
            Assert.That(() => { g.Evaluate(0.5f); }, Is.Not.AllocatingGCMemory());
        }
    }
}
