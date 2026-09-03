using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using ABCodeworld.Gradients.Tests.Editor.Support;

namespace ABCodeworld.Gradients.Tests.Editor.Runtime
{
    [TestFixture]
    [Category("Runtime")]
    internal sealed class GradientSerializationTests
    {
        private TestHostObject host;

        [SetUp]
        public void SetUp() => host = ScriptableObject.CreateInstance<TestHostObject>();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(host);

        [Test]
        public void JsonUtility_RoundTrip_PreservesValues()
        {
            host.gradient = TestGradients.WithModulation();
            string json = JsonUtility.ToJson(host);

            var reloaded = ScriptableObject.CreateInstance<TestHostObject>();
            JsonUtility.FromJsonOverwrite(json, reloaded);

            Assert.That(reloaded.gradient.ContentEquals(host.gradient), Is.True);
            Object.DestroyImmediate(reloaded);
        }

        [Test]
        public void SerializedObject_BoxedValue_RoundTrip()
        {
            var so = new SerializedObject(host);
            var prop = so.FindProperty(nameof(TestHostObject.gradient));

            var edited = TestGradients.Rainbow7();
            prop.boxedValue = edited;
            so.ApplyModifiedProperties();

            Assert.That(host.gradient.ContentEquals(edited), Is.True);

            var readBack = (GradientABCW)prop.boxedValue;
            Assert.That(readBack.ContentEquals(edited), Is.True);
        }

        [Test]
        public void Deserialize_EmptyColorKeys_PadsToMinKeys()
        {
            string json = "{\"gradient\":{\"colorKeys\":[],\"alphaKeys\":[{\"alpha\":1,\"time\":0},{\"alpha\":1,\"time\":1}]," +
                          "\"blendMode\":0,\"modulation\":{\"bypass\":false,\"reverse\":false,\"repeats\":1,\"repeatMode\":0," +
                          "\"offset\":0,\"hueShift\":0,\"saturation\":0,\"brightness\":0,\"alpha\":0}}}";

            JsonUtility.FromJsonOverwrite(json, host);

            Assert.That(host.gradient.ColorKeys.Length, Is.EqualTo(GradientABCW.MinKeys));
        }

        [Test]
        public void Deserialize_TooManyColorKeys_TruncatesToMaxKeys()
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("{\"gradient\":{\"colorKeys\":[");
            for (int i = 0; i < 40; i++)
            {
                if (i > 0) sb.Append(',');
                float t = i / 39f;
                sb.Append($"{{\"color\":{{\"r\":1,\"g\":1,\"b\":1,\"a\":1}},\"time\":{t.ToString(System.Globalization.CultureInfo.InvariantCulture)}}}");
            }
            sb.Append("],\"alphaKeys\":[{\"alpha\":1,\"time\":0},{\"alpha\":1,\"time\":1}],\"blendMode\":0," +
                       "\"modulation\":{\"bypass\":false,\"reverse\":false,\"repeats\":1,\"repeatMode\":0," +
                       "\"offset\":0,\"hueShift\":0,\"saturation\":0,\"brightness\":0,\"alpha\":0}}}");

            JsonUtility.FromJsonOverwrite(sb.ToString(), host);

            Assert.That(host.gradient.ColorKeys.Length, Is.EqualTo(GradientABCW.MaxKeys));
        }

        [Test]
        public void Deserialize_UnsortedColorKeys_SortsOnLoad()
        {
            string json = "{\"gradient\":{\"colorKeys\":[" +
                          "{\"color\":{\"r\":0,\"g\":0,\"b\":1,\"a\":1},\"time\":1}," +
                          "{\"color\":{\"r\":1,\"g\":0,\"b\":0,\"a\":1},\"time\":0}," +
                          "{\"color\":{\"r\":0,\"g\":1,\"b\":0,\"a\":1},\"time\":0.5}" +
                          "],\"alphaKeys\":[{\"alpha\":1,\"time\":0},{\"alpha\":1,\"time\":1}],\"blendMode\":0," +
                          "\"modulation\":{\"bypass\":false,\"reverse\":false,\"repeats\":1,\"repeatMode\":0," +
                          "\"offset\":0,\"hueShift\":0,\"saturation\":0,\"brightness\":0,\"alpha\":0}}}";

            JsonUtility.FromJsonOverwrite(json, host);

            var keys = host.gradient.ColorKeys;
            for (int i = 1; i < keys.Length; i++)
                Assert.That(keys[i].time, Is.GreaterThanOrEqualTo(keys[i - 1].time));
        }

        [Test]
        public void Deserialize_OutOfRangeTimes_ClampToUnitRange()
        {
            string json = "{\"gradient\":{\"colorKeys\":[" +
                          "{\"color\":{\"r\":1,\"g\":0,\"b\":0,\"a\":1},\"time\":-3.5}," +
                          "{\"color\":{\"r\":0,\"g\":0,\"b\":1,\"a\":1},\"time\":7.2}" +
                          "],\"alphaKeys\":[{\"alpha\":1,\"time\":0},{\"alpha\":1,\"time\":1}],\"blendMode\":0," +
                          "\"modulation\":{\"bypass\":false,\"reverse\":false,\"repeats\":1,\"repeatMode\":0," +
                          "\"offset\":0,\"hueShift\":0,\"saturation\":0,\"brightness\":0,\"alpha\":0}}}";

            JsonUtility.FromJsonOverwrite(json, host);

            foreach (var key in host.gradient.ColorKeys)
                Assert.That(key.time, Is.InRange(0f, 1f));
        }
    }
}
