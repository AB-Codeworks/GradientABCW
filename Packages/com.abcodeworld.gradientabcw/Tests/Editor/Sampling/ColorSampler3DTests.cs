using NUnit.Framework;
using UnityEngine;
using ABCodeworld.Gradients.Editor;
using Object = UnityEngine.Object;

namespace ABCodeworld.Gradients.Tests.Editor.Sampling
{
    [TestFixture]
    [Category("Sampling")]
    internal sealed class ColorSampler3DTests
    {
        private static Mesh ColouredMesh(int width, Vector3 offset, Vector3 scale)
        {
            var vertices = new Vector3[width * width * width];
            var colors = new Color32[vertices.Length];

            int index = 0;
            for (int z = 0; z < width; z++)
            {
                for (int y = 0; y < width; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        var unit = new Vector3(x / (width - 1f), y / (width - 1f), z / (width - 1f));
                        vertices[index] = offset + Vector3.Scale(unit, scale);
                        colors[index] = new Color(unit.x, unit.y, unit.z, unit.x);
                        index++;
                    }
                }
            }

            var mesh = new Mesh { name = "SamplerTestMesh", hideFlags = HideFlags.HideAndDontSave };
            mesh.SetVertices(vertices);
            mesh.colors32 = colors;
            mesh.RecalculateBounds();
            return mesh;
        }

        [Test]
        public void MeshSamplingNormalizesVertexPositionsIntoTheCube()
        {
            // Deliberately off-centre and non-uniform, so a missing normalization shows up.
            var mesh = ColouredMesh(4, new Vector3(-13f, 7f, 100f), new Vector3(4f, 0.25f, 20f));
            try
            {
                Assert.That(ColorSampler3D.TrySampleMesh(mesh, 12, 1u, out var g, out string error), Is.True, error);

                foreach (var key in g.ColorKeys.ToArray())
                {
                    Assert.That(key.position.x, Is.InRange(0f, 1f));
                    Assert.That(key.position.y, Is.InRange(0f, 1f));
                    Assert.That(key.position.z, Is.InRange(0f, 1f));
                }

                Assert.That(g.ColorKeys.Length, Is.EqualTo(12));
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void MeshSamplingIsDeterministicGivenTheSameSeed()
        {
            var mesh = ColouredMesh(4, Vector3.zero, Vector3.one);
            try
            {
                Assume.That(ColorSampler3D.TrySampleMesh(mesh, 10, 7u, out var a, out _), Is.True);
                Assume.That(ColorSampler3D.TrySampleMesh(mesh, 10, 7u, out var b, out _), Is.True);

                Assert.That(a.ContentEquals(b), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void MeshSamplingSpreadsKeysThroughTheVolume()
        {
            var mesh = ColouredMesh(4, Vector3.zero, Vector3.one);
            try
            {
                Assume.That(ColorSampler3D.TrySampleMesh(mesh, 8, 3u, out var g, out _), Is.True);

                for (int axis = 0; axis < 3; axis++)
                {
                    float min = 2f, max = -1f;
                    foreach (var key in g.ColorKeys.ToArray())
                    {
                        min = Mathf.Min(min, key.position[axis]);
                        max = Mathf.Max(max, key.position[axis]);
                    }

                    // Farthest-point selection takes the extremes first, so both ends of every axis should
                    // be represented rather than eight vertices from one corner.
                    Assert.That(min, Is.LessThan(0.1f), $"axis {axis}");
                    Assert.That(max, Is.GreaterThan(0.9f), $"axis {axis}");
                }
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void MeshSamplingTakesAlphaFromTheVertexColour()
        {
            var mesh = ColouredMesh(4, Vector3.zero, Vector3.one);
            try
            {
                Assume.That(ColorSampler3D.TrySampleMesh(mesh, 8, 5u, out var g, out _), Is.True);

                // The fixture writes each vertex's x into its alpha, so alpha should track position.
                foreach (var key in g.AlphaKeys.ToArray())
                    Assert.That(key.alpha, Is.EqualTo(key.position.x).Within(0.01f));
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void AMeshWithoutVertexColoursIsReportedRatherThanSampled()
        {
            var mesh = new Mesh { name = "Colourless", hideFlags = HideFlags.HideAndDontSave };
            mesh.SetVertices(new[] { Vector3.zero, Vector3.one });
            try
            {
                Assert.That(ColorSampler3D.TrySampleMesh(mesh, 8, 1u, out var g, out string error), Is.False);
                Assert.That(g, Is.Null);
                Assert.That(error, Is.Not.Null.And.Not.Empty);
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void ANullMeshIsReportedRatherThanThrowing()
        {
            Assert.That(ColorSampler3D.TrySampleMesh(null, 8, 1u, out _, out string error), Is.False);
            Assert.That(error, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public void ANullTextureIsReportedRatherThanThrowing()
        {
            Assert.That(ColorSampler3D.TrySampleTexture(null, 8, 1u, out _, out string error), Is.False);
            Assert.That(error, Is.Not.Null.And.Not.Empty);
        }
    }
}
