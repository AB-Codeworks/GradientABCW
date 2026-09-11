using UnityEngine;
using Random = Unity.Mathematics.Random;

namespace ABCodeworld.Gradients.Editor
{
    /// <summary>
    /// Builds a <see cref="GradientABCW3D"/> by sampling a mesh's coloured vertices, or a texture's
    /// palette scattered through the cube. Deterministic given the same seed.
    /// </summary>
    /// <remarks>
    /// The two sources need genuinely different treatment, which is why this is not simply
    /// <see cref="ColorSampler"/> with a third dimension bolted on. A mesh already carries positions, so
    /// sampling it is a question of which vertices to keep. A texture does not, so its positions have to
    /// be invented — and inventing them is all this adds, because the hard part, choosing which colours
    /// out of a whole image are worth keeping, is exactly what <see cref="ColorSampler"/> already does.
    /// </remarks>
    internal static class ColorSampler3D
    {
        /// <summary>Keys produced by a sample, matching the 1D sampler's count.</summary>
        public const int DefaultKeyCount = 16;

        /// <summary>
        /// How many random positions are drawn per key before thinning. Scattering exactly as many points
        /// as keys leaves visible clumps and gaps; drawing a surplus and keeping the best-spread subset
        /// gives an even cloud while staying reproducible from the seed.
        /// </summary>
        private const int ScatterOversample = 8;

        /// <summary>
        /// Builds a gradient from a mesh: vertex position normalized into the mesh's bounds becomes the
        /// key position, vertex colour becomes the key.
        /// </summary>
        /// <remarks>
        /// Vertices are chosen by farthest-point selection in <em>position</em> space, starting from a
        /// seeded random vertex. <see cref="ColorSampler"/>'s strategies all select in colour space, which
        /// is the right thing when the gradient's domain has to be invented from the palette and the wrong
        /// thing here, where the geometry already is the domain: picking sixteen vertices that happen to
        /// be distinct colours would happily take them all from one corner of the mesh.
        /// </remarks>
        public static bool TrySampleMesh(Mesh mesh, int keyCount, uint seed, out GradientABCW3D result, out string error)
        {
            result = null;

            var source = new MeshPositionColorSource(mesh);
            if (source.Error != null)
            {
                error = source.Error;
                return false;
            }

            if (source.Count == 0)
            {
                error = "Mesh has no sampleable vertices.";
                return false;
            }

            keyCount = ClampKeyCount(keyCount, source.Count);

            var positions = new Vector3[source.Count];
            for (int i = 0; i < source.Count; i++)
                positions[i] = source.GetPosition(i);

            var rng = new Random(seed == 0u ? 1u : seed);
            int start = rng.NextInt(0, source.Count);
            var chosen = PointSpread.SelectSpreadOut(positions, keyCount, start);

            var colorKeys = new ColorKey3D[chosen.Length];
            var alphaKeys = new AlphaKey3D[chosen.Length];

            for (int i = 0; i < chosen.Length; i++)
            {
                Color32 c = source[chosen[i]];
                Vector3 p = positions[chosen[i]];
                colorKeys[i] = new ColorKey3D(new Color32(c.r, c.g, c.b, 255), p);
                alphaKeys[i] = new AlphaKey3D(c.a / 255f, p);
            }

            result = GradientABCW3D.CreateDefault();
            result.SetKeys(colorKeys, alphaKeys);
            error = null;
            return true;
        }

        /// <summary>
        /// Builds a gradient from a texture's palette, scattered through the cube.
        /// </summary>
        /// <remarks>
        /// Colour selection is delegated wholesale to <see cref="ColorSampler.TrySampleRandom"/>, so a
        /// texture produces the same palette whether it is sampled into a 1D or a 3D gradient. Only the
        /// positions are new, and they are drawn from the same seed the Reroll button already drives — so
        /// rerolling rearranges the cube, which is the only meaningful thing it can do here.
        /// </remarks>
        public static bool TrySampleTexture(Texture2D texture, int keyCount, uint seed, out GradientABCW3D result, out string error)
        {
            result = null;

            var source = new TextureColorSource(texture);
            if (source.Error != null)
            {
                error = source.Error;
                return false;
            }

            if (!ColorSampler.TrySampleRandom(source, keyCount, seed, out var flat, out error))
                return false;

            var flatColors = flat.ColorKeys;
            var flatAlphas = flat.AlphaKeys;

            var rng = new Random(seed == 0u ? 1u : seed);
            var colorPositions = Scatter(flatColors.Length, ref rng);
            var alphaPositions = Scatter(flatAlphas.Length, ref rng);

            var colorKeys = new ColorKey3D[flatColors.Length];
            for (int i = 0; i < flatColors.Length; i++)
                colorKeys[i] = new ColorKey3D(flatColors[i].color, colorPositions[i]);

            var alphaKeys = new AlphaKey3D[flatAlphas.Length];
            for (int i = 0; i < flatAlphas.Length; i++)
                alphaKeys[i] = new AlphaKey3D(flatAlphas[i].alpha, alphaPositions[i]);

            result = GradientABCW3D.CreateDefault();
            result.SetKeys(colorKeys, alphaKeys);
            error = null;
            return true;
        }

        /// <summary>
        /// Draws <paramref name="count"/> positions spread through the cube, reproducibly from
        /// <paramref name="rng"/>.
        /// </summary>
        private static Vector3[] Scatter(int count, ref Random rng)
        {
            if (count <= 0)
                return System.Array.Empty<Vector3>();

            var candidates = new Vector3[count * ScatterOversample];
            for (int i = 0; i < candidates.Length; i++)
                candidates[i] = new Vector3(rng.NextFloat(), rng.NextFloat(), rng.NextFloat());

            var chosen = PointSpread.SelectSpreadOut(candidates, count);

            var picked = new Vector3[count];
            for (int i = 0; i < count; i++)
                picked[i] = candidates[chosen[i]];
            return picked;
        }

        private static int ClampKeyCount(int keyCount, int available) => Mathf.Clamp(
            keyCount <= 0 ? DefaultKeyCount : keyCount,
            GradientABCW3D.MinKeys,
            Mathf.Min(GradientABCW3D.MaxKeys, available));
    }
}
