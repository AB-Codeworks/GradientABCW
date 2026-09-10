using NUnit.Framework;
using UnityEngine;
using ABCodeworld.Gradients.Tests.Editor.Support;

namespace ABCodeworld.Gradients.Tests.Editor.Runtime
{
    [TestFixture]
    [Category("Runtime")]
    internal sealed class Gradient3DKeyTests
    {
        [Test]
        public void KeyPositionsClampPerAxis()
        {
            var key = new ColorKey3D(Color.red, new Vector3(-2f, 0.5f, 7f));

            Assert.That(key.position, Is.EqualTo(new Vector3(0f, 0.5f, 1f)));
        }

        [Test]
        public void AColourKeysAlphaIsForcedOpaque()
        {
            var key = new ColorKey3D(new Color(1f, 0f, 0f, 0.25f), Vector3.zero);

            Assert.That(key.color.a, Is.EqualTo(1f));
        }

        [Test]
        public void AddingRefusesPastTheKeyLimitAndReturnsMinusOne()
        {
            var g = Test3DGradients.Max64();

            Assert.That(g.CanAddColorKey, Is.False);
            Assert.That(g.AddColorKey(Color.red, Vector3.zero), Is.EqualTo(-1));
            Assert.That(g.ColorKeys.Length, Is.EqualTo(GradientABCW3D.MaxKeys));
        }

        [Test]
        public void RemovingRefusesAtTheMinimum()
        {
            var g = Test3DGradients.Single();

            Assert.That(g.ColorKeys.Length, Is.EqualTo(GradientABCW3D.MinKeys));
            Assert.That(g.RemoveColorKey(0), Is.False);
            Assert.That(g.ColorKeys.Length, Is.EqualTo(1));
        }

        /// <summary>
        /// The one thing 3D keys give up nothing for: a key's index is fixed for as long as it exists,
        /// because there is no order for a move to disturb.
        /// </summary>
        [Test]
        public void MovingAKeyDoesNotChangeAnyIndex()
        {
            var g = Test3DGradients.Corners8();
            var third = g.ColorKeys[2];

            g.SetColorKey(2, new ColorKey3D(third.color, new Vector3(0.5f, 0.5f, 0.5f)));

            Assert.That(g.ColorKeys[2].color, Is.EqualTo(third.color));
            Assert.That(g.ColorKeys[2].position, Is.EqualTo(new Vector3(0.5f, 0.5f, 0.5f)));
        }

        [Test]
        public void WritingBackAnIdenticalKeyDoesNotBumpVersion()
        {
            var g = Test3DGradients.Corners8();
            int version = g.Version;

            var blendMode = g.BlendMode;
            float falloff = g.FalloffPower;
            var modulation = g.Modulation;

            g.SetColorKey(0, g.ColorKeys[0]);
            g.SetAlphaKey(0, g.AlphaKeys[0]);
            g.BlendMode = blendMode;
            g.FalloffPower = falloff;
            g.Modulation = modulation;

            Assert.That(g.Version, Is.EqualTo(version));
        }

        [Test]
        public void ASubTolerantNudgeStillBumpsVersion()
        {
            var g = Test3DGradients.Corners8();
            int version = g.Version;
            var key = g.ColorKeys[0];

            // Smaller than Vector3's own == tolerance, which is exactly the case exact comparison exists
            // to catch: the edit is real, so every cache downstream has to know about it.
            g.SetColorKey(0, new ColorKey3D(key.color, key.position + new Vector3(1e-7f, 0f, 0f)));

            Assert.That(g.Version, Is.GreaterThan(version));
        }

        [Test]
        public void FalloffPowerIsClampedToItsRange()
        {
            var g = Test3DGradients.Corners8();

            g.FalloffPower = -5f;
            Assert.That(g.FalloffPower, Is.EqualTo(GradientABCW3D.MinFalloffPower));

            g.FalloffPower = 500f;
            Assert.That(g.FalloffPower, Is.EqualTo(GradientABCW3D.MaxFalloffPower));
        }

        [Test]
        public void FlippingMirrorsEveryKeyThroughTheCentre()
        {
            var g = Test3DGradients.Corners8();
            var before = g.ColorKeys.ToArray();

            g.FlipKeys();

            for (int i = 0; i < before.Length; i++)
            {
                Assert.That(g.ColorKeys[i].position, Is.EqualTo(Vector3.one - before[i].position), $"key {i}");
                Assert.That(g.ColorKeys[i].color, Is.EqualTo(before[i].color), "flipping moves keys, it does not reorder them");
            }
        }

        [Test]
        public void DistributingAPerfectCubeLandsEveryKeyOnALatticeSite()
        {
            var g = Test3DGradients.Corners8();

            // Bunch them up first, so the distribution has something to undo.
            var bunched = new ColorKey3D[8];
            for (int i = 0; i < 8; i++)
                bunched[i] = new ColorKey3D(g.ColorKeys[i].color, new Vector3(0.5f, 0.5f, 0.5f));
            g.SetKeys(bunched, g.AlphaKeys.ToArray());

            g.DistributeColorKeysEvenly();

            var seen = new System.Collections.Generic.HashSet<Vector3>();
            foreach (var key in g.ColorKeys.ToArray())
            {
                foreach (float axis in new[] { key.position.x, key.position.y, key.position.z })
                    Assert.That(axis, Is.EqualTo(0f).Or.EqualTo(1f), "8 keys should land on the cube's corners");
                Assert.That(seen.Add(key.position), Is.True, "every corner should be used exactly once");
            }
        }

        [Test]
        public void DistributingANonCubeCountStillSpreadsThroughTheVolume()
        {
            var g = GradientABCW3D.CreateDefault();
            var keys = new ColorKey3D[10];
            for (int i = 0; i < keys.Length; i++)
                keys[i] = new ColorKey3D(Color.white, new Vector3(0.5f, 0.5f, 0.5f));
            g.SetKeys(keys, g.AlphaKeys.ToArray());

            g.DistributeColorKeysEvenly();

            // 10 keys need a 3x3x3 lattice, so every coordinate lands on 0, 0.5 or 1 — and, crucially, the
            // spread must reach both extremes on every axis rather than piling onto one face.
            foreach (var axis in new[] { 0, 1, 2 })
            {
                float min = 2f, max = -1f;
                foreach (var key in g.ColorKeys.ToArray())
                {
                    float v = key.position[axis];
                    Assert.That(v, Is.EqualTo(0f).Or.EqualTo(0.5f).Or.EqualTo(1f), $"axis {axis}");
                    min = Mathf.Min(min, v);
                    max = Mathf.Max(max, v);
                }
                Assert.That(min, Is.EqualTo(0f), $"axis {axis} should reach 0");
                Assert.That(max, Is.EqualTo(1f), $"axis {axis} should reach 1");
            }
        }

        [Test]
        public void DistributingOneKeyPutsItInTheMiddle()
        {
            var g = Test3DGradients.Single();

            g.DistributeColorKeysEvenly();

            Assert.That(g.ColorKeys[0].position, Is.EqualTo(new Vector3(0.5f, 0.5f, 0.5f)));
        }
    }
}
