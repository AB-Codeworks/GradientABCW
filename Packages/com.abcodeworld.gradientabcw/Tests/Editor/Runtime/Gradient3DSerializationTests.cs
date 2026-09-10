using NUnit.Framework;
using UnityEngine;
using ABCodeworld.Gradients.Tests.Editor.Support;

namespace ABCodeworld.Gradients.Tests.Editor.Runtime
{
    [TestFixture]
    [Category("Runtime")]
    internal sealed class Gradient3DSerializationTests
    {
        private static GradientABCW3D RoundTrip(GradientABCW3D g) =>
            JsonUtility.FromJson<GradientABCW3D>(JsonUtility.ToJson(g));

        /// <summary>
        /// Rewrites the serialized falloff power, without assuming how JsonUtility formats a float.
        /// </summary>
        private static GradientABCW3D WithSerializedFalloff(GradientABCW3D g, string replacement)
        {
            string json = System.Text.RegularExpressions.Regex.Replace(
                JsonUtility.ToJson(g),
                "\"falloffPower\":[-0-9.eE+]+",
                "\"falloffPower\":" + replacement);

            Assume.That(json, Does.Contain("\"falloffPower\":" + replacement));
            return JsonUtility.FromJson<GradientABCW3D>(json);
        }

        [Test]
        public void ARoundTripPreservesContent()
        {
            var g = Test3DGradients.WithModulation();
            g.FalloffPower = 5.5f;

            var restored = RoundTrip(g);

            Assert.That(restored.ContentEquals(g), Is.True);
        }

        [Test]
        public void OverLongKeyDataIsDecimatedRatherThanTruncated()
        {
            var g = GradientABCW3D.CreateDefault();
            var keys = new ColorKey3D[GradientABCW3D.MaxKeys + 40];
            for (int i = 0; i < keys.Length; i++)
            {
                float t = i / (keys.Length - 1f);
                keys[i] = new ColorKey3D(new Color(t, t, t), new Vector3(t, t, t));
            }

            g.SetKeys(keys, g.AlphaKeys.ToArray());

            Assert.That(g.ColorKeys.Length, Is.EqualTo(GradientABCW3D.MaxKeys));

            // Decimation selects by position, so both ends of the diagonal must survive rather than the
            // high end being lopped off.
            float minAxis = 2f, maxAxis = -1f;
            foreach (var key in g.ColorKeys.ToArray())
            {
                minAxis = Mathf.Min(minAxis, key.position.x);
                maxAxis = Mathf.Max(maxAxis, key.position.x);
            }
            Assert.That(minAxis, Is.EqualTo(0f).Within(1e-5f));
            Assert.That(maxAxis, Is.EqualTo(1f).Within(1e-5f));
        }

        [Test]
        public void ASingleKeyIsNotPaddedOut()
        {
            var g = Test3DGradients.Single();

            var restored = RoundTrip(g);

            Assert.That(restored.ColorKeys.Length, Is.EqualTo(1), "one key is a legal 3D gradient");
            Assert.That(restored.ColorKeys[0].position, Is.EqualTo(new Vector3(0.5f, 0.5f, 0.5f)));
        }

        [Test]
        public void EmptyKeyDataFallsBackToTheDefaultDiagonal()
        {
            var g = GradientABCW3D.CreateDefault();
            g.SetKeys(System.Array.Empty<ColorKey3D>(), System.Array.Empty<AlphaKey3D>());

            Assert.That(g.ColorKeys.Length, Is.EqualTo(2));
            Assert.That(g.ColorKeys[0].position, Is.EqualTo(Vector3.zero));
            Assert.That(g.ColorKeys[1].position, Is.EqualTo(Vector3.one));
        }

        /// <summary>
        /// A gradient written before the falloff field existed deserializes with a zero there, which is
        /// not a value the author ever chose. Clamping it to the minimum would silently soften every such
        /// gradient; it has to come back as the default instead.
        /// </summary>
        [Test]
        public void AMissingFalloffPowerRestoresTheDefaultRatherThanTheMinimum()
        {
            var restored = WithSerializedFalloff(Test3DGradients.Corners8(), "0");

            Assert.That(restored.FalloffPower, Is.EqualTo(GradientABCW3D.DefaultFalloffPower));
        }

        [Test]
        public void OutOfRangeValuesAreBroughtBackIntoRange()
        {
            var restored = WithSerializedFalloff(Test3DGradients.Corners8(), "99");

            Assert.That(restored.FalloffPower, Is.EqualTo(GradientABCW3D.MaxFalloffPower));
        }
    }
}
