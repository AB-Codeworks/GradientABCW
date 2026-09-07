using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ABCodeworld.Gradients.Editor
{
    /// <summary>A shared, repeating checkerboard used as the transparency backdrop behind gradient previews.</summary>
    /// <remarks>
    /// The texture is <see cref="HideFlags.HideAndDontSave"/>, so Unity will not collect it — but a domain
    /// reload resets the static field that points at it. Without the explicit teardown below, every reload
    /// stranded one more unreachable checkerboard in memory for the rest of the session.
    /// </remarks>
    [InitializeOnLoad]
    internal static class CheckerTexture
    {
        private static Texture2D checker;

        static CheckerTexture() =>
            AssemblyReloadEvents.beforeAssemblyReload += DisposeChecker;

        public static Texture2D Image => checker != null ? checker : (checker = BuildChecker());

        private static void DisposeChecker()
        {
            if (checker != null)
                Object.DestroyImmediate(checker);
            checker = null;
        }

        private static Texture2D BuildChecker()
        {
            const int size = 16, cell = 8;
            // sRGB storage, matching the gradient preview it sits behind.
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false, linear: false)
            {
                name = "GradientABCW_Checker",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Point,
                hideFlags = HideFlags.HideAndDontSave,
            };

            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                int cy = (y / cell) % 2;
                for (int x = 0; x < size; x++)
                {
                    int cx = (x / cell) % 2;
                    bool even = ((cx + cy) & 1) == 0;
                    Color c = even ? new Color(0.82f, 0.82f, 0.82f) : new Color(0.67f, 0.67f, 0.67f);
                    pixels[y * size + x] = c;
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            return tex;
        }
    }
}
