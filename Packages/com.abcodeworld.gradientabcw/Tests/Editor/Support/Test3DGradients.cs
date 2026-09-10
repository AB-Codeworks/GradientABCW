using UnityEngine;

namespace ABCodeworld.Gradients.Tests.Editor.Support
{
    /// <summary>Named 3D gradient fixtures shared across the test suite.</summary>
    internal static class Test3DGradients
    {
        public static GradientABCW3D Default() => GradientABCW3D.CreateDefault();

        /// <summary>A single white key at the centre: the smallest legal 3D gradient.</summary>
        public static GradientABCW3D Single()
        {
            var g = GradientABCW3D.CreateDefault();
            g.SetKeys(
                new[] { new ColorKey3D(Color.white, new Vector3(0.5f, 0.5f, 0.5f)) },
                new[] { new AlphaKey3D(1f, new Vector3(0.5f, 0.5f, 0.5f)) });
            return g;
        }

        /// <summary>The eight corners as a colour cube: black at the origin, white opposite it.</summary>
        public static GradientABCW3D Corners8()
        {
            var g = GradientABCW3D.CreateDefault();
            var colorKeys = new ColorKey3D[8];
            var alphaKeys = new AlphaKey3D[8];

            for (int i = 0; i < 8; i++)
            {
                var p = new Vector3(i & 1, (i >> 1) & 1, (i >> 2) & 1);
                colorKeys[i] = new ColorKey3D(new Color(p.x, p.y, p.z), p);
                alphaKeys[i] = new AlphaKey3D(i / 7f, p);
            }

            g.SetKeys(colorKeys, alphaKeys);
            return g;
        }

        /// <summary>Twenty-seven keys on a 3x3x3 lattice.</summary>
        public static GradientABCW3D Lattice27()
        {
            var g = GradientABCW3D.CreateDefault();
            var colorKeys = new ColorKey3D[27];
            var alphaKeys = new AlphaKey3D[27];

            int index = 0;
            for (int z = 0; z < 3; z++)
            {
                for (int y = 0; y < 3; y++)
                {
                    for (int x = 0; x < 3; x++)
                    {
                        var p = new Vector3(x * 0.5f, y * 0.5f, z * 0.5f);
                        colorKeys[index] = new ColorKey3D(new Color(p.x, p.y, p.z), p);
                        alphaKeys[index] = new AlphaKey3D(index / 26f, p);
                        index++;
                    }
                }
            }

            g.SetKeys(colorKeys, alphaKeys);
            return g;
        }

        public static GradientABCW3D Max64()
        {
            var g = GradientABCW3D.CreateDefault();
            var colorKeys = new ColorKey3D[GradientABCW3D.MaxKeys];
            var alphaKeys = new AlphaKey3D[GradientABCW3D.MaxKeys];

            for (int i = 0; i < GradientABCW3D.MaxKeys; i++)
            {
                var p = new Vector3((i & 3) / 3f, ((i >> 2) & 3) / 3f, ((i >> 4) & 3) / 3f);
                float t = i / (GradientABCW3D.MaxKeys - 1f);
                colorKeys[i] = new ColorKey3D(new Color(t, 1f - t, 0.5f), p);
                alphaKeys[i] = new AlphaKey3D(t, p);
            }

            g.SetKeys(colorKeys, alphaKeys);
            return g;
        }

        public static GradientABCW3D Stepped()
        {
            var g = Corners8();
            g.BlendMode = BlendMode.Stepped;
            return g;
        }

        public static GradientABCW3D WithModulation()
        {
            var g = Corners8();
            g.Modulation = new GradientModulation
            {
                bypass = false,
                reverse = false,
                repeats = 2f,
                repeatMode = RepeatMode.Mirror,
                offset = 0.1f,
                hueShift = 0.2f,
                saturation = 0.3f,
                brightness = -0.2f,
                alpha = 0.1f,
            };
            return g;
        }

        public static GradientABCW3D Bypassed()
        {
            var g = WithModulation();
            var m = g.Modulation;
            m.bypass = true;
            g.Modulation = m;
            return g;
        }

        /// <summary>
        /// A 3D gradient laid out along the x axis at y = z = 0.5, matching <paramref name="source"/> key
        /// for key, so the two can be compared directly.
        /// </summary>
        /// <remarks>
        /// The comparison is only exact under stepped blending. Every key shares y and z with the sample,
        /// so the third dimension contributes the same constant to every distance and drops out of the
        /// nearest-key search — leaving the 3D gradient picking exactly the key the 1D one's midpoint
        /// partition picks. Smooth blending has no such correspondence: inverse-distance weighting is not
        /// piecewise-linear interpolation, and is not meant to be.
        /// </remarks>
        public static GradientABCW3D AxisAlignedFrom(GradientABCW source)
        {
            var g = GradientABCW3D.CreateDefault();
            var colors = source.ColorKeys;
            var alphas = source.AlphaKeys;

            var colorKeys = new ColorKey3D[colors.Length];
            for (int i = 0; i < colors.Length; i++)
                colorKeys[i] = new ColorKey3D(colors[i].color, new Vector3(colors[i].time, 0.5f, 0.5f));

            var alphaKeys = new AlphaKey3D[alphas.Length];
            for (int i = 0; i < alphas.Length; i++)
                alphaKeys[i] = new AlphaKey3D(alphas[i].alpha, new Vector3(alphas[i].time, 0.5f, 0.5f));

            g.SetKeys(colorKeys, alphaKeys);
            g.BlendMode = source.BlendMode;
            g.Modulation = source.Modulation;
            return g;
        }
    }
}
