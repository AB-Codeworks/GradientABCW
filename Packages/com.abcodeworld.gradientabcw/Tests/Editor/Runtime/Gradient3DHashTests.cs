using NUnit.Framework;
using UnityEngine;
using ABCodeworld.Gradients.Tests.Editor.Support;

namespace ABCodeworld.Gradients.Tests.Editor.Runtime
{
    [TestFixture]
    [Category("Runtime")]
    internal sealed class Gradient3DHashTests
    {
        [Test]
        public void IdenticalContentHashesTheSameAndComparesEqual()
        {
            var a = Test3DGradients.WithModulation();
            var b = a.Clone();

            Assert.That(b.ComputeContentHash(), Is.EqualTo(a.ComputeContentHash()));
            Assert.That(a.ContentEquals(b), Is.True);
        }

        [Test]
        public void TheHashIgnoresVersion()
        {
            var g = Test3DGradients.Corners8();
            int hash = g.ComputeContentHash();

            var key = g.ColorKeys[0];
            g.SetColorKey(0, new ColorKey3D(Color.magenta, key.position));
            g.SetColorKey(0, key);

            Assert.That(g.Version, Is.GreaterThan(0));
            Assert.That(g.ComputeContentHash(), Is.EqualTo(hash));
        }

        [Test]
        public void EveryPositionComponentIsPartOfTheHash([Values(0, 1, 2)] int axis)
        {
            var a = Test3DGradients.Corners8();
            var b = a.Clone();

            var key = b.ColorKeys[3];
            var moved = key.position;
            moved[axis] = moved[axis] > 0.5f ? 0.25f : 0.75f;
            b.SetColorKey(3, new ColorKey3D(key.color, moved));

            Assert.That(b.ComputeContentHash(), Is.Not.EqualTo(a.ComputeContentHash()), $"axis {axis}");
            Assert.That(a.ContentEquals(b), Is.False, $"axis {axis}");
        }

        [Test]
        public void FalloffPowerIsPartOfContentIdentity()
        {
            var a = Test3DGradients.Corners8();
            var b = a.Clone();
            b.FalloffPower = 6f;

            Assert.That(b.ComputeContentHash(), Is.Not.EqualTo(a.ComputeContentHash()));
            Assert.That(a.ContentEquals(b), Is.False);
        }

        /// <summary>
        /// Two keys sharing a position resolve to the lower-indexed one at that position, so a reordering
        /// really can change what the gradient evaluates to — which means order has to be part of its
        /// identity, not just of its layout.
        /// </summary>
        [Test]
        public void KeyOrderIsPartOfContentIdentity()
        {
            var a = Test3DGradients.Corners8();
            var b = a.Clone();

            var keys = b.ColorKeys.ToArray();
            (keys[0], keys[1]) = (keys[1], keys[0]);
            b.SetKeys(keys, b.AlphaKeys.ToArray());

            Assert.That(a.ContentEquals(b), Is.False);
        }
    }
}
