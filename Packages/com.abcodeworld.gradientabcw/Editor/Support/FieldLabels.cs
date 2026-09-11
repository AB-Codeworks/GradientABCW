using UnityEngine.UIElements;

namespace ABCodeworld.Gradients.Editor
{
    /// <summary>
    /// Layout fixes for labelled fields that are narrower than an inspector row.
    /// </summary>
    internal static class FieldLabels
    {
        /// <summary>
        /// Width that comfortably fits a one-character label such as <c>t</c>, <c>x</c> or <c>y</c>,
        /// including the small gap the default stylesheet puts between label and input.
        /// </summary>
        internal const float SingleCharacterWidth = 14f;

        /// <summary>
        /// Forces a <see cref="BaseField{T}"/>'s label to an explicit width.
        /// </summary>
        /// <remarks>
        /// A <c>BaseField</c> lays its label and its input out as a row <em>inside</em> the field's own
        /// width, and the default editor stylesheet gives <c>.unity-base-field__label</c> a
        /// <c>min-width</c> sized for an inspector's label column. Narrow the field below that — as both
        /// key lists do — and the label wins the whole width, leaving the input laid out at zero and
        /// nothing drawn after the label. The field still takes keyboard input, so it looks like a
        /// rendering bug rather than a layout one.
        /// <para>
        /// Both <c>min-width</c> and <c>width</c> are set, because setting <c>width</c> alone loses to the
        /// stylesheet's <c>min-width</c>; both are set inline, which beats a stylesheet rule of any
        /// specificity. <c>flex-shrink</c> goes to 0 so the pin holds when the row runs out of room —
        /// otherwise the label is free to shrink again and a single character collapses to an ellipsis.
        /// </para>
        /// </remarks>
        internal static void PinLabel(Label label, float width)
        {
            if (label == null)
                return;

            label.style.minWidth = width;
            label.style.width = width;
            label.style.flexShrink = 0;
        }
    }
}
