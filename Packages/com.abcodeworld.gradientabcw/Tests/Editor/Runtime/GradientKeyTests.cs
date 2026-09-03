using NUnit.Framework;
using UnityEngine;
using ABCodeworld.Gradients.Tests.Editor.Support;

namespace ABCodeworld.Gradients.Tests.Editor.Runtime
{
    [TestFixture]
    [Category("Runtime")]
    internal sealed class GradientKeyTests
    {
        [Test]
        public void AddColorKey_KeepsSortedOrder()
        {
            var g = TestGradients.Default();
            g.AddColorKey(Color.red, 0.5f);

            var keys = g.ColorKeys;
            for (int i = 1; i < keys.Length; i++)
                Assert.That(keys[i].time, Is.GreaterThanOrEqualTo(keys[i - 1].time));
        }

        [Test]
        public void AddAlphaKey_KeepsSortedOrder()
        {
            var g = TestGradients.Default();
            g.AddAlphaKey(0.25f, 0.3f);

            var keys = g.AlphaKeys;
            for (int i = 1; i < keys.Length; i++)
                Assert.That(keys[i].time, Is.GreaterThanOrEqualTo(keys[i - 1].time));
        }

        [Test]
        public void AddColorKey_RefusesAboveMaxKeys()
        {
            var g = TestGradients.Max32();
            Assert.That(g.CanAddColorKey, Is.False);
            int index = g.AddColorKey(Color.magenta, 0.5f);
            Assert.That(index, Is.EqualTo(-1));
            Assert.That(g.ColorKeys.Length, Is.EqualTo(GradientABCW.MaxKeys));
        }

        [Test]
        public void AddAlphaKey_RefusesAboveMaxKeys()
        {
            var g = TestGradients.Max32();
            Assert.That(g.CanAddAlphaKey, Is.False);
            int index = g.AddAlphaKey(0.5f, 0.5f);
            Assert.That(index, Is.EqualTo(-1));
            Assert.That(g.AlphaKeys.Length, Is.EqualTo(GradientABCW.MaxKeys));
        }

        [Test]
        public void RemoveColorKey_RefusesAtMinKeys()
        {
            var g = TestGradients.TwoKey();
            Assert.That(g.ColorKeys.Length, Is.EqualTo(GradientABCW.MinKeys));
            bool removed = g.RemoveColorKey(0);
            Assert.That(removed, Is.False);
            Assert.That(g.ColorKeys.Length, Is.EqualTo(GradientABCW.MinKeys));
        }

        [Test]
        public void RemoveAlphaKey_RefusesAtMinKeys()
        {
            var g = TestGradients.TwoKey();
            bool removed = g.RemoveAlphaKey(1);
            Assert.That(removed, Is.False);
            Assert.That(g.AlphaKeys.Length, Is.EqualTo(GradientABCW.MinKeys));
        }

        [Test]
        public void RemoveColorKey_AboveMinKeys_Succeeds()
        {
            var g = TestGradients.Rainbow7();
            int before = g.ColorKeys.Length;
            bool removed = g.RemoveColorKey(0);
            Assert.That(removed, Is.True);
            Assert.That(g.ColorKeys.Length, Is.EqualTo(before - 1));
        }

        [Test]
        public void SetColorKey_TimeChange_ReturnsNewIndex()
        {
            var g = TestGradients.Rainbow7();
            // Move the first key (time 0) to just before the last key (time 1), landing
            // one slot from the end without colliding with any existing key's time.
            int newIndex = g.SetColorKey(0, new ColorKey(Color.white, 0.95f));

            Assert.That(newIndex, Is.EqualTo(g.ColorKeys.Length - 2));
            Assert.That(g.ColorKeys[newIndex].time, Is.EqualTo(0.95f).Within(1e-6f));
        }

        [Test]
        public void SetAlphaKey_TimeChange_ReturnsNewIndex()
        {
            var g = TestGradients.Rainbow7();
            // Rainbow7's alpha keys sit at 0, 0.5, 1; move the last (time 1) to 0.1 so it
            // lands between the first two without colliding with either one's time.
            int newIndex = g.SetAlphaKey(g.AlphaKeys.Length - 1, new AlphaKey(0.2f, 0.1f));

            Assert.That(newIndex, Is.EqualTo(1));
            Assert.That(g.AlphaKeys[newIndex].time, Is.EqualTo(0.1f).Within(1e-6f));
        }

        [Test]
        public void FlipKeys_InvertsTimes()
        {
            var g = TestGradients.Rainbow7();
            var originalTimes = new float[g.ColorKeys.Length];
            for (int i = 0; i < originalTimes.Length; i++)
                originalTimes[i] = g.ColorKeys[i].time;

            g.FlipKeys();

            var flipped = g.ColorKeys;
            for (int i = 0; i < flipped.Length; i++)
                Assert.That(flipped[i].time, Is.EqualTo(1f - originalTimes[originalTimes.Length - 1 - i]).Within(1e-6f));
        }

        [Test]
        public void DistributeColorKeysEvenly_SpacesKeysUniformly()
        {
            var g = TestGradients.Rainbow7();
            g.DistributeColorKeysEvenly();

            var keys = g.ColorKeys;
            for (int i = 0; i < keys.Length; i++)
                Assert.That(keys[i].time, Is.EqualTo((float)i / (keys.Length - 1)).Within(1e-6f));
        }

        [Test]
        public void DistributeAlphaKeysEvenly_SpacesKeysUniformly()
        {
            var g = TestGradients.Rainbow7();
            g.DistributeAlphaKeysEvenly();

            var keys = g.AlphaKeys;
            for (int i = 0; i < keys.Length; i++)
                Assert.That(keys[i].time, Is.EqualTo((float)i / (keys.Length - 1)).Within(1e-6f));
        }

        [Test]
        public void Version_BumpsOncePerMutation()
        {
            var g = TestGradients.Default();
            int before = g.Version;
            g.AddColorKey(Color.red, 0.4f);
            Assert.That(g.Version, Is.EqualTo(before + 1));
        }

        [Test]
        public void Version_DoesNotChangeOnReadOnlyAccess()
        {
            var g = TestGradients.Rainbow7();
            int before = g.Version;
            _ = g.ColorKeys.Length;
            _ = g.Evaluate(0.5f);
            Assert.That(g.Version, Is.EqualTo(before));
        }

        [Test]
        public void Clone_IsIndependent()
        {
            var g = TestGradients.Rainbow7();
            var clone = g.Clone();

            clone.AddColorKey(Color.black, 0.5f);

            Assert.That(clone.ColorKeys.Length, Is.Not.EqualTo(g.ColorKeys.Length));
        }

        [Test]
        public void SetKeys_SortsUnsortedInput()
        {
            var g = TestGradients.Default();
            g.SetKeys(
                new[] { new ColorKey(Color.blue, 1f), new ColorKey(Color.red, 0f), new ColorKey(Color.green, 0.5f) },
                new[] { new AlphaKey(1f, 0f), new AlphaKey(1f, 1f) });

            var keys = g.ColorKeys;
            for (int i = 1; i < keys.Length; i++)
                Assert.That(keys[i].time, Is.GreaterThanOrEqualTo(keys[i - 1].time));
        }

        [Test]
        public void SetKeys_PadsBelowMinKeys()
        {
            var g = TestGradients.Default();
            g.SetKeys(new[] { new ColorKey(Color.red, 0.5f) }, new[] { new AlphaKey(1f, 0f) });

            Assert.That(g.ColorKeys.Length, Is.EqualTo(GradientABCW.MinKeys));
            Assert.That(g.AlphaKeys.Length, Is.EqualTo(GradientABCW.MinKeys));
        }

        [Test]
        public void SetKeys_TruncatesAboveMaxKeys()
        {
            var colorKeys = new ColorKey[GradientABCW.MaxKeys + 8];
            var alphaKeys = new AlphaKey[GradientABCW.MaxKeys + 8];
            for (int i = 0; i < colorKeys.Length; i++)
            {
                float t = (float)i / (colorKeys.Length - 1);
                colorKeys[i] = new ColorKey(Color.white, t);
                alphaKeys[i] = new AlphaKey(1f, t);
            }

            var g = TestGradients.Default();
            g.SetKeys(colorKeys, alphaKeys);

            Assert.That(g.ColorKeys.Length, Is.EqualTo(GradientABCW.MaxKeys));
            Assert.That(g.AlphaKeys.Length, Is.EqualTo(GradientABCW.MaxKeys));
        }

        [Test]
        public void ColorKey_ForcesOpaqueAlpha()
        {
            var key = new ColorKey(new Color(1f, 0f, 0f, 0.2f), 0.5f);
            Assert.That(key.color.a, Is.EqualTo(1f));
        }

        [Test]
        public void ClampColorKeyTime_RespectsNeighbours()
        {
            var g = TestGradients.Rainbow7();
            // Attempting to drag the middle key onto (or past) its lower neighbour must clamp
            // it to just above that neighbour, never allowing the two to coincide or cross.
            int middle = g.ColorKeys.Length / 2;
            float neighbourBefore = g.ColorKeys[middle - 1].time;
            float clamped = g.ClampColorKeyTime(middle, neighbourBefore);
            Assert.That(clamped, Is.GreaterThan(neighbourBefore));
        }
    }
}
