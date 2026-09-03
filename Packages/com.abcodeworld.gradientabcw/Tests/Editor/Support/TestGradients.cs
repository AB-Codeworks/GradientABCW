using UnityEngine;

namespace ABCodeworld.Gradients.Tests.Editor.Support
{
    /// <summary>Named gradient fixtures shared across the test suite.</summary>
    internal static class TestGradients
    {
        public static GradientABCW Default() => GradientABCW.CreateDefault();

        public static GradientABCW TwoKey() => GradientABCW.CreateDefault();

        public static GradientABCW Rainbow7()
        {
            var g = GradientABCW.CreateDefault();
            var colors = new[]
            {
                Color.red, new Color(1f, 0.5f, 0f), Color.yellow, Color.green,
                Color.blue, new Color(0.29f, 0f, 0.51f), new Color(0.56f, 0f, 1f),
            };
            var colorKeys = new ColorKey[colors.Length];
            for (int i = 0; i < colors.Length; i++)
                colorKeys[i] = new ColorKey(colors[i], (float)i / (colors.Length - 1));

            var alphaKeys = new[] { new AlphaKey(1f, 0f), new AlphaKey(0.5f, 0.5f), new AlphaKey(1f, 1f) };
            g.SetKeys(colorKeys, alphaKeys);
            return g;
        }

        public static GradientABCW Max32()
        {
            var g = GradientABCW.CreateDefault();
            var colorKeys = new ColorKey[GradientABCW.MaxKeys];
            var alphaKeys = new AlphaKey[GradientABCW.MaxKeys];
            for (int i = 0; i < GradientABCW.MaxKeys; i++)
            {
                float t = (float)i / (GradientABCW.MaxKeys - 1);
                colorKeys[i] = new ColorKey(new Color(t, 1f - t, 0.5f), t);
                alphaKeys[i] = new AlphaKey(t, t);
            }
            g.SetKeys(colorKeys, alphaKeys);
            return g;
        }

        public static GradientABCW Stepped()
        {
            var g = Rainbow7();
            g.BlendMode = BlendMode.Stepped;
            return g;
        }

        public static GradientABCW WithModulation()
        {
            var g = Rainbow7();
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

        public static GradientABCW Bypassed()
        {
            var g = WithModulation();
            var m = g.Modulation;
            m.bypass = true;
            g.Modulation = m;
            return g;
        }
    }
}
