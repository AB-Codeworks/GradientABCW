using NUnit.Framework;
using UnityEngine.TestTools.Constraints;
using ABCodeworld.Gradients.Tests.Editor.Support;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace ABCodeworld.Gradients.Tests.Editor.Runtime
{
    [TestFixture]
    [Category("Runtime")]
    internal sealed class GradientLutCacheTests
    {
        [Test]
        public void Get_SameVersion_DoesNotAllocate()
        {
            var g = TestGradients.WithModulation();
            var cache = new GradientLutCache();
            cache.Get(g, GradientLutOptions.Final); // warm

            Assert.That(() => cache.Get(g, GradientLutOptions.Final), Is.Not.AllocatingGCMemory());
        }

        [Test]
        public void Get_AfterMutation_RebakesWithNewValues()
        {
            var g = TestGradients.Rainbow7();
            var cache = new GradientLutCache();
            var before = cache.Get(g, GradientLutOptions.Base, 16)[0];

            g.SetColorKey(0, new ColorKey(UnityEngine.Color.magenta, 0f));
            var after = cache.Get(g, GradientLutOptions.Base, 16)[0];

            Assert.That(after, Is.Not.EqualTo(before));
        }

        [Test]
        public void Get_DifferentOptions_RebakesIndependently()
        {
            var g = TestGradients.WithModulation();
            var cache = new GradientLutCache();

            var baseLut = cache.Get(g, GradientLutOptions.Base, 16).ToArray();
            var finalLut = cache.Get(g, GradientLutOptions.Final, 16).ToArray();

            CollectionAssert.AreNotEqual(baseLut, finalLut);
        }
    }
}
