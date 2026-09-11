using UnityEngine;

namespace ABCodeworld.Gradients.Editor
{
    /// <summary>One direction the preview cube is viewed from, and a label naming the faces it shows.</summary>
    internal readonly struct CubeView
    {
        /// <summary>Direction from the centre of the cube towards the eye. Need not be normalized.</summary>
        public Vector3 Direction { get; }

        /// <summary>The three faces this view shows, e.g. "+X +Y +Z". Used as the element's tooltip.</summary>
        public string Label { get; }

        public CubeView(Vector3 direction, string label)
        {
            Direction = direction;
            Label = label;
        }
    }

    /// <summary>What fills the area a ray misses, and shows through any transparency in the cube.</summary>
    internal enum CubeBackdrop
    {
        /// <summary>
        /// Write straight RGBA and leave the caller to put something behind it. What a rotatable viewport
        /// wants, because its silhouette moves and the backdrop has to cover the whole frame.
        /// </summary>
        Transparent,

        /// <summary>
        /// Composite an opaque checkerboard behind the cube only, leaving everything around it
        /// transparent. What a fixed-angle render wants: the silhouette never moves, so a backdrop
        /// outside it signifies nothing and only competes with the cube.
        /// </summary>
        TrimmedChecker,
    }

    /// <summary>
    /// Renders a 3D gradient as a small solid cube: every surface pixel is the gradient evaluated at that
    /// exact point on the cube's surface, over a transparent background.
    /// </summary>
    /// <remarks>
    /// A cube shows at most three faces from any one direction, so <see cref="DefaultViews"/> holds four,
    /// arranged so every face appears in exactly two of them. Two opposing views would be the minimum, but
    /// four means no face is only ever seen at a grazing angle.
    /// <para>
    /// Rays are cast per pixel against the unit cube rather than rasterising triangles with a depth
    /// buffer. At 32x32 that is 1024 rays of about a dozen operations each, and it gives exact surface
    /// positions, no interpolation error, no depth buffer, and the hit face for free — which is what the
    /// shading below and the face-coverage test both need.
    /// </para>
    /// <para>
    /// Pure static, with no <c>VisualElement</c>, camera or <c>RenderTexture</c> anywhere in it, so a test
    /// can render into an array and assert on pixels without a panel or a layout pass.
    /// </para>
    /// <para>
    /// Colours are written as sRGB bytes and never converted to linear, for the reason
    /// <see cref="GradientPreviewElement"/> already documents: previews are stored in an sRGB texture and
    /// read back through it, so converting here would be undone on sample.
    /// </para>
    /// </remarks>
    internal static class CubePreviewRasterizer
    {
        /// <summary>Distance from the centre of the cube to the eye, in cube widths.</summary>
        internal const float EyeDistance = 3.2f;

        /// <summary>
        /// Half the field of view, as a tangent.
        /// </summary>
        /// <remarks>
        /// Fitted to the cube's actual silhouette rather than to its bounding sphere. From a corner the
        /// silhouette is a hexagon whose widest vertex, at <see cref="EyeDistance"/>, subtends a
        /// half-angle of tangent 0.2805; the sphere would say 0.281 and clip. 0.29 leaves a small margin,
        /// which puts the cube across about half the frame — a hexagon inside a square cannot do much
        /// better. Narrow enough that the view reads as perspective rather than isometric, which is part
        /// of what makes the cube look solid.
        /// </remarks>
        internal const float TanHalfFov = 0.29f;

        /// <summary>How much of a face's brightness is independent of which way it points.</summary>
        internal const float Ambient = 0.55f;

        /// <summary>
        /// The direction the stylised light comes from.
        /// </summary>
        /// <remarks>
        /// Chosen so that the three faces visible from any of the <see cref="DefaultViews"/> differ in
        /// brightness by at least 7%, which is about 19 of 255 levels — enough to read three faces meeting
        /// at a corner as three faces. Most light directions do far worse: any where two components are
        /// close leaves two of the three visible faces nearly identical, and a cube in flat colour reads
        /// as an ambiguous hexagon rather than a solid.
        /// </remarks>
        internal static readonly Vector3 LightDirection = new Vector3(0.2f, 0.9f, 0.55f).normalized;

        /// <summary>
        /// Four viewing directions that between them show all six faces, each one twice.
        /// </summary>
        internal static readonly CubeView[] DefaultViews =
        {
            new CubeView(new Vector3(1f, 1f, 1f), "+X +Y +Z"),
            new CubeView(new Vector3(-1f, 1f, 1f), "-X +Y +Z"),
            new CubeView(new Vector3(1f, -1f, -1f), "+X -Y -Z"),
            new CubeView(new Vector3(-1f, -1f, -1f), "-X -Y -Z"),
        };

        /// <summary>Renders <paramref name="size"/> by <paramref name="size"/> pixels into <paramref name="dst"/>.</summary>
        internal static void Render(GradientABCW3D gradient, in CubeView view, int size, Color32[] dst, bool includeModulation,
            CubeBackdrop backdrop = CubeBackdrop.Transparent) =>
            Render(gradient, in view, size, dst, null, includeModulation, backdrop);

        /// <summary>
        /// Renders, and records which face each pixel hit in <paramref name="faceIds"/> as
        /// <see cref="FaceId"/>, or -1 where the ray missed the cube.
        /// </summary>
        /// <remarks>
        /// Exists so that "all six faces appear across these views" can be asserted from the render itself
        /// rather than inferred from the geometry, which is the one property of this file that has to hold
        /// for the preview to be worth showing.
        /// </remarks>
        internal static void Render(GradientABCW3D gradient, in CubeView view, int size, Color32[] dst, sbyte[] faceIds, bool includeModulation,
            CubeBackdrop backdrop = CubeBackdrop.Transparent)
        {
            if (gradient == null || dst == null || size <= 0)
                return;

            var centre = new Vector3(0.5f, 0.5f, 0.5f);
            Vector3 toEye = view.Direction.normalized;
            Vector3 eye = centre + toEye * EyeDistance;
            Vector3 forward = -toEye;

            // Any of the default views would do with a Y up-vector; the fallback only matters if a caller
            // supplies a view looking straight down an axis.
            Vector3 worldUp = Mathf.Abs(forward.y) > 0.999f ? new Vector3(0f, 0f, 1f) : new Vector3(0f, 1f, 0f);
            Vector3 right = Vector3.Normalize(Vector3.Cross(worldUp, forward));
            Vector3 up = Vector3.Cross(forward, right);

            for (int py = 0; py < size; py++)
            {
                // Row 0 is the bottom of the image, matching how Unity lays out texture pixel data.
                float sy = (py + 0.5f) / size * 2f - 1f;

                for (int px = 0; px < size; px++)
                {
                    float sx = (px + 0.5f) / size * 2f - 1f;
                    int i = py * size + px;

                    Vector3 dir = (forward + right * (sx * TanHalfFov) + up * (sy * TanHalfFov)).normalized;

                    if (!TryIntersectUnitCube(eye, dir, out float tNear, out int axis, out int sign))
                    {
                        dst[i] = new Color32(0, 0, 0, 0);
                        if (faceIds != null)
                            faceIds[i] = -1;
                        continue;
                    }

                    // Clamped because a ray grazing the silhouette can land a rounding error outside.
                    Vector3 p = eye + dir * tNear;
                    p = new Vector3(Mathf.Clamp01(p.x), Mathf.Clamp01(p.y), Mathf.Clamp01(p.z));

                    Color c = includeModulation ? gradient.Evaluate(p) : gradient.EvaluateBase(p);
                    float shade = FaceShade(axis, sign);

                    float r = c.r * shade;
                    float g = c.g * shade;
                    float b = c.b * shade;

                    if (backdrop == CubeBackdrop.TrimmedChecker)
                    {
                        // Composited here rather than in a pass over the finished pixels, because by then
                        // a miss and a hit on a fully transparent surface are the same (0, 0, 0, 0) — and
                        // filling only one of them would punch a hole through the cube exactly where an
                        // alpha key of 0 is, which is the case a checkerboard exists to show.
                        float alpha = Mathf.Clamp01(c.a);
                        Color checker = CheckerTexture.IsLightCell(px, py) ? CheckerTexture.Light : CheckerTexture.Dark;

                        r = Mathf.Lerp(checker.r, r, alpha);
                        g = Mathf.Lerp(checker.g, g, alpha);
                        b = Mathf.Lerp(checker.b, b, alpha);

                        dst[i] = new Color32(ToByte(r), ToByte(g), ToByte(b), 255);
                    }
                    else
                    {
                        dst[i] = new Color32(ToByte(r), ToByte(g), ToByte(b), ToByte(c.a));
                    }

                    if (faceIds != null)
                        faceIds[i] = (sbyte)FaceId(axis, sign);
                }
            }
        }

        /// <summary>A face as a number in 0..5: two per axis, the negative face first.</summary>
        internal static int FaceId(int axis, int sign) => axis * 2 + (sign > 0 ? 1 : 0);

        /// <summary>Brightness multiplier for the face on <paramref name="axis"/> facing <paramref name="sign"/>.</summary>
        internal static float FaceShade(int axis, int sign)
        {
            float d = sign > 0 ? LightDirection[axis] : -LightDirection[axis];

            // Remapped from [-1, 1] rather than clamped at zero: clamping would leave all three faces of
            // the away-facing half at exactly the ambient level, so the views that show them would render
            // a flat hexagon.
            return Ambient + (1f - Ambient) * (0.5f + 0.5f * d);
        }

        /// <summary>
        /// Intersects a ray with the axis-aligned unit cube, reporting the nearest hit at or in front of
        /// the origin and which face it landed on.
        /// </summary>
        /// <remarks>
        /// The standard slab test. A ray that starts inside the cube reports its exit face instead of its
        /// entry face, so the result is always a real surface point the caller can evaluate.
        /// </remarks>
        /// <param name="faceAxis">0, 1 or 2 for x, y or z.</param>
        /// <param name="faceSign">-1 for the face at 0 on that axis, +1 for the face at 1.</param>
        internal static bool TryIntersectUnitCube(Vector3 origin, Vector3 direction, out float tNear, out int faceAxis, out int faceSign)
        {
            tNear = 0f;
            faceAxis = 0;
            faceSign = -1;

            float tEnter = float.NegativeInfinity;
            float tExit = float.PositiveInfinity;
            int enterAxis = 0, enterSign = -1, exitAxis = 0, exitSign = 1;

            for (int a = 0; a < 3; a++)
            {
                float d = direction[a];
                float o = origin[a];

                if (Mathf.Abs(d) < 1e-9f)
                {
                    // Parallel to this pair of faces: either inside the slab for every t, or never.
                    if (o < 0f || o > 1f)
                        return false;
                    continue;
                }

                float inv = 1f / d;
                float t1 = (0f - o) * inv;
                float t2 = (1f - o) * inv;
                int sign = -1;

                if (t1 > t2)
                {
                    (t1, t2) = (t2, t1);
                    sign = 1;
                }

                if (t1 > tEnter)
                {
                    tEnter = t1;
                    enterAxis = a;
                    enterSign = sign;
                }

                if (t2 < tExit)
                {
                    tExit = t2;
                    exitAxis = a;
                    exitSign = -sign;
                }

                if (tEnter > tExit)
                    return false;
            }

            if (tExit < 0f)
                return false;

            if (tEnter >= 0f)
            {
                tNear = tEnter;
                faceAxis = enterAxis;
                faceSign = enterSign;
            }
            else
            {
                tNear = tExit;
                faceAxis = exitAxis;
                faceSign = exitSign;
            }

            return true;
        }

        private static byte ToByte(float value) => (byte)(Mathf.Clamp01(value) * 255f + 0.5f);
    }
}
