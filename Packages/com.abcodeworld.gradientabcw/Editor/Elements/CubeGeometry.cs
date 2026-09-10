using UnityEngine;

namespace ABCodeworld.Gradients.Editor
{
    /// <summary>
    /// The layout maths of the cube viewport: where a point in the unit cube lands on screen at a given
    /// rotation, how far away it is, which wireframe edges are hidden behind the cube, and where a key dot
    /// is drawn.
    /// </summary>
    /// <remarks>
    /// A plain readonly struct with no dependency on VisualElement, so it can be reasoned about and tested
    /// without a panel, a layout pass or an EditorWindow — the same split
    /// <see cref="BarGeometry"/> makes for the 1D bar. <see cref="GradientCubeElement"/> keeps input
    /// handling and element lifetime; this keeps the arithmetic.
    /// <para>
    /// The projection deliberately reuses <see cref="CubePreviewRasterizer"/>'s camera, down to the eye
    /// distance and field of view, and draws into the same centred square. That is what lets the viewport
    /// switch between the wireframe and the solid render without the cube appearing to jump.
    /// </para>
    /// <para>
    /// The screen scale is fixed rather than fitted to the projected corners each frame. Refitting is the
    /// obvious implementation and it makes the cube visibly breathe while being dragged, growing as it
    /// turns face-on and shrinking as it turns corner-on. The field of view is instead set once, wide
    /// enough for the worst rotation, so the cube stays put and simply turns.
    /// </para>
    /// </remarks>
    internal readonly struct CubeGeometry
    {
        /// <summary>Half the extent of the cube's diagonal, and so its bounding-sphere radius.</summary>
        internal const float HalfDiagonal = 0.8660254f;

        /// <summary>Degrees of rotation per pixel dragged.</summary>
        public const float RotateSensitivity = 0.5f;

        /// <summary>Pitch is clamped short of straight up or down, where yaw stops meaning anything.</summary>
        public const float MaxPitch = 89f;

        /// <summary>
        /// Inset around the cube, in pixels. A corner of the cube can come within a few pixels of the
        /// frame at the least forgiving rotation, and a key dot sitting on that corner is drawn centred —
        /// so without this the dot's outer half would be clipped.
        /// </summary>
        public const float Padding = 8f;

        public const float DotSizeNear = 11f;
        public const float DotSizeFar = 6f;

        /// <summary>The twelve edges of a cube, as pairs of corner indices.</summary>
        public const int EdgeCount = 12;

        private readonly Vector2 screenCentre;
        private readonly float halfSide;
        private readonly Vector3 eye;
        private readonly Vector3 forward;
        private readonly Vector3 right;
        private readonly Vector3 up;
        private readonly Vector3 toEye;

        public CubeGeometry(Rect contentRect, float yawDegrees, float pitchDegrees)
        {
            // Before the first layout pass the content rect is empty, so every derived value has to stay
            // finite and usable rather than collapsing to zero or NaN.
            float width = Mathf.Max(2f, contentRect.width);
            float height = Mathf.Max(2f, contentRect.height);

            screenCentre = new Vector2(width * 0.5f, height * 0.5f);
            halfSide = Mathf.Max(2f, 0.5f * Mathf.Min(width, height) - Padding);

            toEye = DirectionFrom(yawDegrees, pitchDegrees);

            var centre = new Vector3(0.5f, 0.5f, 0.5f);
            eye = centre + toEye * CubePreviewRasterizer.EyeDistance;
            forward = -toEye;

            Vector3 worldUp = Mathf.Abs(forward.y) > 0.999f ? new Vector3(0f, 0f, 1f) : new Vector3(0f, 1f, 0f);
            right = Vector3.Normalize(Vector3.Cross(worldUp, forward));
            up = Vector3.Cross(forward, right);
        }

        /// <summary>
        /// The centred square the cube is drawn into. The solid render is stretched over exactly this, so
        /// the two views line up.
        /// </summary>
        public Rect Viewport => new Rect(
            screenCentre.x - halfSide,
            screenCentre.y - halfSide,
            halfSide * 2f,
            halfSide * 2f);

        /// <summary>Projects a point in the unit cube into the element's local space.</summary>
        public Vector2 Project(Vector3 p)
        {
            Vector3 v = p - eye;
            float z = Mathf.Max(1e-4f, Vector3.Dot(v, forward));
            float sx = Vector3.Dot(v, right) / (z * CubePreviewRasterizer.TanHalfFov);
            float sy = Vector3.Dot(v, up) / (z * CubePreviewRasterizer.TanHalfFov);

            // Screen y grows downward in UI Toolkit, view y grows upward.
            return new Vector2(screenCentre.x + sx * halfSide, screenCentre.y - sy * halfSide);
        }

        /// <summary>How far away a point is, as 0 at the nearest possible point and 1 at the furthest.</summary>
        public float Depth(Vector3 p)
        {
            float z = Vector3.Dot(p - eye, forward);
            return Mathf.Clamp01((z - (CubePreviewRasterizer.EyeDistance - HalfDiagonal)) / (2f * HalfDiagonal));
        }

        /// <summary>
        /// Where a key dot is drawn, in the element's local space. Near dots are drawn larger, which is
        /// the only depth cue a dot has once the wireframe stops occluding it.
        /// </summary>
        public Rect DotRect(Vector3 p)
        {
            Vector2 centre = Project(p);
            float size = Mathf.Lerp(DotSizeNear, DotSizeFar, Depth(p));
            return new Rect(centre.x - size * 0.5f, centre.y - size * 0.5f, size, size);
        }

        /// <summary>
        /// True when <paramref name="edgeIndex"/> runs along the back of the cube and should be drawn
        /// faintly.
        /// </summary>
        /// <remarks>
        /// An edge is hidden exactly when both faces meeting at it point away from the eye. Picking the
        /// three edges at the furthest corner instead is the shortcut, and it is wrong for a face-on view,
        /// where four edges are hidden rather than three.
        /// </remarks>
        public bool IsEdgeHidden(int edgeIndex)
        {
            (int a, int b) = EdgeCorners(edgeIndex);
            int varying = CountTrailingAxis(a ^ b);

            bool hidden = true;
            for (int axis = 0; axis < 3; axis++)
            {
                if (axis == varying)
                    continue;

                int side = (a >> axis) & 1;
                float outward = side == 1 ? toEye[axis] : -toEye[axis];
                if (outward > 0f)
                {
                    hidden = false;
                    break;
                }
            }
            return hidden;
        }

        /// <summary>
        /// The rotation as a unit direction from the centre of the cube towards the eye. Yaw turns around
        /// the vertical axis, pitch lifts above or drops below the equator.
        /// </summary>
        public static Vector3 DirectionFrom(float yawDegrees, float pitchDegrees)
        {
            float yaw = yawDegrees * Mathf.Deg2Rad;
            float pitch = pitchDegrees * Mathf.Deg2Rad;
            float cp = Mathf.Cos(pitch);
            return new Vector3(cp * Mathf.Sin(yaw), Mathf.Sin(pitch), cp * Mathf.Cos(yaw));
        }

        /// <summary>Corner <paramref name="index"/> of the unit cube; bit 0 is x, bit 1 is y, bit 2 is z.</summary>
        public static Vector3 Corner(int index) =>
            new Vector3(index & 1, (index >> 1) & 1, (index >> 2) & 1);

        /// <summary>The two corner indices joined by <paramref name="edgeIndex"/>, in 0..11.</summary>
        /// <remarks>
        /// Enumerated as: for each corner, the neighbour along each axis whose index is higher. That
        /// visits every edge exactly once, in a stable order, without a hand-written table to mistype.
        /// </remarks>
        public static (int a, int b) EdgeCorners(int edgeIndex)
        {
            int seen = 0;
            for (int corner = 0; corner < 8; corner++)
            {
                for (int axis = 0; axis < 3; axis++)
                {
                    int other = corner ^ (1 << axis);
                    if (other <= corner)
                        continue;
                    if (seen == edgeIndex)
                        return (corner, other);
                    seen++;
                }
            }
            return (0, 1);
        }

        /// <summary>The endpoints of <paramref name="edgeIndex"/> as positions in the unit cube.</summary>
        public static (Vector3 a, Vector3 b) Edge(int edgeIndex)
        {
            (int a, int b) = EdgeCorners(edgeIndex);
            return (Corner(a), Corner(b));
        }

        /// <summary>
        /// Applies a pointer drag to a yaw and pitch. Dragging turns the cube as though it were being
        /// pushed: rightwards swings its front to the right, downwards tips its top towards the viewer.
        /// </summary>
        public static (float yaw, float pitch) Rotate(float yaw, float pitch, Vector2 delta)
        {
            yaw = Mathf.Repeat(yaw - delta.x * RotateSensitivity, 360f);
            pitch = Mathf.Clamp(pitch + delta.y * RotateSensitivity, -MaxPitch, MaxPitch);
            return (yaw, pitch);
        }

        /// <summary>Index of the single set bit in <paramref name="mask"/>, which is the axis an edge runs along.</summary>
        private static int CountTrailingAxis(int mask) => mask == 1 ? 0 : mask == 2 ? 1 : 2;
    }
}
