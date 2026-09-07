using NUnit.Framework;
using UnityEngine;
using ABCodeworld.Gradients.Tests.Editor.Support;

namespace ABCodeworld.Gradients.Tests.Editor.Runtime
{
    /// <summary>
    /// Pins the behaviours that the key array used to get wrong when two keys share, or nearly share, a
    /// time: which index an insert or move reports, whether equal-time keys keep their relative order, and
    /// whether a write that changes nothing still invalidates every downstream cache.
    /// </summary>
    [TestFixture]
    [Category("Runtime")]
    internal sealed class GradientKeyIdentityTests
    {
        [Test]
        public void AddColorKey_OnAnExistingKeyTime_ReturnsIndexOfTheKeyJustAdded()
        {
            var g = TestGradients.Rainbow7();
            float occupied = g.ColorKeys[3].time;

            int index = g.AddColorKey(Color.magenta, occupied);

            // The returned index used to come from a Mathf.Approximately search for the time, which found
            // the pre-existing key at that time instead of the new one.
            Assert.That(g.ColorKeys[index].color, Is.EqualTo((Color)Color.magenta));
            Assert.That(g.ColorKeys[index].time, Is.EqualTo(occupied).Within(1e-6f));
        }

        [Test]
        public void AddColorKey_AtATimeOneEpsilonFromAnExistingKey_ReturnsTheNewKey()
        {
            var g = TestGradients.Rainbow7();
            float nearlyOccupied = g.ColorKeys[2].time + 1e-7f;

            int index = g.AddColorKey(Color.magenta, nearlyOccupied);

            Assert.That(g.ColorKeys[index].color, Is.EqualTo((Color)Color.magenta));
        }

        [Test]
        public void SetColorKey_MovedOntoAnotherKeysTime_ReturnsIndexOfTheMovedKey()
        {
            var g = TestGradients.Rainbow7();
            float destination = g.ColorKeys[5].time;

            int newIndex = g.SetColorKey(0, new ColorKey(Color.magenta, destination));

            // A drag depends on this: the manipulator re-latches onto the returned index, so pointing it
            // at the wrong key makes the drag jump to a neighbour.
            Assert.That(g.ColorKeys[newIndex].color, Is.EqualTo((Color)Color.magenta));
        }

        [Test]
        public void SetAlphaKey_MovedOntoAnotherKeysTime_ReturnsIndexOfTheMovedKey()
        {
            var g = TestGradients.Rainbow7();
            float destination = g.AlphaKeys[2].time;

            int newIndex = g.SetAlphaKey(0, new AlphaKey(0.125f, destination));

            Assert.That(g.AlphaKeys[newIndex].alpha, Is.EqualTo(0.125f).Within(1e-6f));
        }

        [Test]
        public void EqualTimeColorKeys_KeepTheirRelativeOrderAcrossOperations()
        {
            var g = TestGradients.Default();
            g.SetKeys(
                new[]
                {
                    new ColorKey(Color.red, 0f),
                    new ColorKey(Color.green, 0.5f),
                    new ColorKey(Color.blue, 0.5f),
                    new ColorKey(Color.white, 1f),
                },
                new[] { new AlphaKey(1f, 0f), new AlphaKey(1f, 1f) });

            Assert.That(g.ColorKeys[1].color, Is.EqualTo((Color)Color.green));
            Assert.That(g.ColorKeys[2].color, Is.EqualTo((Color)Color.blue));

            // Distribute rewrites every time and previously re-sorted through an unstable Array.Sort,
            // which was free to swap the two 0.5 keys.
            g.DistributeColorKeysEvenly();

            Assert.That(g.ColorKeys[1].color, Is.EqualTo((Color)Color.green));
            Assert.That(g.ColorKeys[2].color, Is.EqualTo((Color)Color.blue));
        }

        [Test]
        public void FlipKeys_ReversesOrderExactly()
        {
            var g = TestGradients.Rainbow7();
            var before = g.ColorKeys.ToArray();

            g.FlipKeys();

            var after = g.ColorKeys;
            for (int i = 0; i < after.Length; i++)
                Assert.That(after[i].color, Is.EqualTo(before[before.Length - 1 - i].color));
        }

        [Test]
        public void SetColorKey_WithAnIdenticalKey_DoesNotBumpVersion()
        {
            var g = TestGradients.Rainbow7();
            int before = g.Version;

            g.SetColorKey(2, g.ColorKeys[2]);

            Assert.That(g.Version, Is.EqualTo(before));
        }

        [Test]
        public void SetAlphaKey_WithAnIdenticalKey_DoesNotBumpVersion()
        {
            var g = TestGradients.Rainbow7();
            int before = g.Version;

            g.SetAlphaKey(1, g.AlphaKeys[1]);

            Assert.That(g.Version, Is.EqualTo(before));
        }

        [Test]
        public void Modulation_WrittenBackOverItself_DoesNotBumpVersion()
        {
            var g = TestGradients.WithModulation();
            int before = g.Version;

            g.Modulation = g.Modulation;

            // Every LUT cache, native snapshot and preview texture keys off Version, so a slider
            // re-sending the value it already holds must not invalidate all of them.
            Assert.That(g.Version, Is.EqualTo(before));
        }

        [Test]
        public void BlendMode_WrittenBackOverItself_DoesNotBumpVersion()
        {
            var g = TestGradients.Rainbow7();
            int before = g.Version;

            g.BlendMode = g.BlendMode;

            Assert.That(g.Version, Is.EqualTo(before));
        }

        [Test]
        public void SetKeys_WithOneColorKey_KeepsThatColourAcrossTheDomain()
        {
            var g = TestGradients.Rainbow7();

            g.SetKeys(new[] { new ColorKey(Color.magenta, 0.5f) }, new[] { new AlphaKey(0.25f, 0.5f) });

            // Short input used to be thrown away wholesale in favour of the default black-to-white ramp.
            Assert.That(g.ColorKeys.Length, Is.EqualTo(GradientABCW.MinKeys));
            Assert.That(g.ColorKeys[0].color, Is.EqualTo((Color)Color.magenta));
            Assert.That(g.ColorKeys[1].color, Is.EqualTo((Color)Color.magenta));
            Assert.That(g.ColorKeys[0].time, Is.EqualTo(0f).Within(1e-6f));
            Assert.That(g.ColorKeys[1].time, Is.EqualTo(1f).Within(1e-6f));
            Assert.That(g.AlphaKeys[0].alpha, Is.EqualTo(0.25f).Within(1e-6f));
        }

        [Test]
        public void SetKeys_OverMaxKeys_KeepsBothEndsOfTheGradient()
        {
            const int supplied = GradientABCW.MaxKeys + 20;
            var colorKeys = new ColorKey[supplied];
            var alphaKeys = new AlphaKey[supplied];
            for (int i = 0; i < supplied; i++)
            {
                float t = (float)i / (supplied - 1);
                colorKeys[i] = new ColorKey(new Color(t, 0f, 1f - t), t);
                alphaKeys[i] = new AlphaKey(1f, t);
            }

            var g = TestGradients.Default();
            g.SetKeys(colorKeys, alphaKeys);

            // Over-long input used to be truncated from the tail, silently deleting the high-time end.
            Assert.That(g.ColorKeys.Length, Is.EqualTo(GradientABCW.MaxKeys));
            Assert.That(g.ColorKeys[0].time, Is.EqualTo(0f).Within(1e-6f));
            Assert.That(g.ColorKeys[^1].time, Is.EqualTo(1f).Within(1e-6f));
        }
    }
}
