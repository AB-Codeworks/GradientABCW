using UnityEngine;

namespace ABCodeworld.Gradients.Editor
{
    /// <summary>
    /// The layout maths of the gradient bar: where the strip sits inside the control, how a pointer
    /// position maps to a normalized time, and where a key handle is drawn for a given time.
    /// </summary>
    /// <remarks>
    /// A plain readonly struct with no dependency on VisualElement, so it can be reasoned about and
    /// tested without a panel, a layout pass or an EditorWindow. <see cref="GradientBarElement"/> keeps
    /// input handling and element lifetime; this keeps the arithmetic.
    /// </remarks>
    internal readonly struct BarGeometry
    {
        /// <summary>Height of the key lane above and below the gradient strip.</summary>
        public const float LaneHeight = 22f;

        /// <summary>Horizontal inset, leaving room for a handle centred on time 0 or 1.</summary>
        public const float PadX = 12f;

        public const float HandleWidth = 15f;
        public const float HandleHeight = 18f;

        /// <summary>
        /// Gap between a lane's edge and the handle inside it. Named because it is the constraint on
        /// <see cref="LaneHeight"/>: a lane must be at least <c>HandleInset + HandleHeight</c> tall, or
        /// handles paint outside the bar, which has no <c>overflow: hidden</c> to clip them.
        /// </summary>
        public const float HandleInset = 3f;

        /// <summary>How far past a lane a key must be dragged before it counts as removed.</summary>
        public const float RemoveThreshold = 5f;

        private readonly Rect strip;
        private readonly float contentHeight;

        public BarGeometry(Rect contentRect)
        {
            contentHeight = contentRect.height;
            strip = new Rect(
                PadX,
                LaneHeight,
                Mathf.Max(2f, contentRect.width - 2f * PadX),
                Mathf.Max(2f, contentRect.height - 2f * LaneHeight));
        }

        /// <summary>The gradient strip's rect, excluding the padding and the two key lanes.</summary>
        public Rect Strip => strip;

        /// <summary>Normalized time under a position in the bar's local space.</summary>
        public float TimeAt(Vector2 localPosition) =>
            Mathf.Clamp01((localPosition.x - strip.x) / strip.width);

        /// <summary>Where a handle for <paramref name="time"/> is drawn, in the bar's local space.</summary>
        public Rect HandleRect(float time, bool isAlpha) =>
            new Rect(
                strip.x + time * strip.width - HandleWidth / 2f,
                isAlpha ? HandleInset : contentHeight - LaneHeight + HandleInset,
                HandleWidth,
                HandleHeight);

        /// <summary>True when a drag has left its lane far enough that the key should be removed.</summary>
        public bool IsOutsideLane(bool isAlpha, Vector2 localPosition) =>
            isAlpha
                ? localPosition.y < -RemoveThreshold
                : localPosition.y > contentHeight + RemoveThreshold;
    }
}
