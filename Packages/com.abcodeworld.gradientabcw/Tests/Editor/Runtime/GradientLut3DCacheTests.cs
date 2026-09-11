using NUnit.Framework;
using UnityEngine.TestTools.Constraints;
using ABCodeworld.Gradients.Tests.Editor.Support;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace ABCodeworld.Gradients.Tests.Editor.Runtime
{
    [TestFixture]
    [Category("Runtime")]
    internal sealed class GradientLut3DCacheTests
    {
        [Test]
        public void AWarmGetAllocatesNothing()
        {
            var g = Test3DGradients.Corners8();
            var cache = new GradientLut3DCache();
            cache.Get(g, GradientLutOptions.Final, 8);

            Assert.That(() => cache.Get(g, GradientLutOptions.Final, 8), Is.Not.AllocatingGCMemory());
        }

        [Test]
        public void EditingTheGradientRebakes()
        {
            var g = Test3DGradients.Corners8();
            var cache = new GradientLut3DCache();
            var before = cache.Get(g, GradientLutOptions.Base, 8).ToArray();

            g.SetColorKey(0, new ColorKey3D(UnityEngine.Color.magenta, g.ColorKeys[0].position));
            var after = cache.Get(g, GradientLutOptions.Base, 8).ToArray();

            Assert.That(after, Is.Not.EqualTo(before));
        }

        [Test]
        public void ChangingSizeReallocates()
        {
            var g = Test3DGradients.Corners8();
            var cache = new GradientLut3DCache();

            Assert.That(cache.Get(g, GradientLutOptions.Base, 4).Length, Is.EqualTo(GradientLut3D.VoxelCount(4)));
            Assert.That(cache.Get(g, GradientLutOptions.Base, 8).Length, Is.EqualTo(GradientLut3D.VoxelCount(8)));
            Assert.That(cache.Get(g, GradientLutOptions.Base, 4).Length, Is.EqualTo(GradientLut3D.VoxelCount(4)));
        }

        [Test]
        public void BaseAndFinalAreCachedSeparately()
        {
            var g = Test3DGradients.WithModulation();
            var cache = new GradientLut3DCache();

            var basic = cache.Get(g, GradientLutOptions.Base, 4).ToArray();
            var final = cache.Get(g, GradientLutOptions.Final, 4).ToArray();

            Assert.That(basic, Is.Not.EqualTo(final));
        }
    }
}
