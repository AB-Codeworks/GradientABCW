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
        /// <summary>Side of one square, in pixels.</summary>
        internal const int CellSize = 8;

        internal static readonly Color Light = new Color(0.82f, 0.82f, 0.82f);

        internal static readonly Color Dark = new Color(0.67f, 0.67f, 0.67f);

        /// <summary>True when the square covering a pixel is the lighter of the two.</summary>
        /// <remarks>
        /// Exposed so a renderer that composites the backdrop itself draws the same checkerboard as the
        /// one stacked behind everything else, rather than a second set of greys that drift from these.
        /// <see cref="CubePreviewRasterizer"/> needs that: it trims the backdrop to the cube's silhouette,
        /// which cannot be done by putting a texture behind the render.
        /// </remarks>
        internal static bool IsLightCell(int x, int y) => (((x / CellSize) + (y / CellSize)) & 1) == 0;

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
            const int size = 2 * CellSize;
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
                for (int x = 0; x < size; x++)
                    pixels[y * size + x] = IsLightCell(x, y) ? Light : Dark;
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            return tex;
        }
    }
}
