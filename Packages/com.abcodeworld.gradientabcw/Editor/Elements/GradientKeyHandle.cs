using UnityEngine;
using UnityEngine.UIElements;

namespace ABCodeworld.Gradients.Editor
{
    /// <summary>A single draggable colour or alpha key handle, absolutely positioned by its owning bar.</summary>
    internal sealed class GradientKeyHandle : VisualElement
    {
        public bool IsAlpha { get; }
        public int Index { get; set; }

        public bool Selected
        {
            get => ClassListContains("abcw-key--selected");
            set => EnableInClassList("abcw-key--selected", value);
        }

        public GradientKeyHandle(bool isAlpha)
        {
            IsAlpha = isAlpha;
            AddToClassList("abcw-key");
            EnableInClassList("abcw-key--alpha", isAlpha);
            pickingMode = PickingMode.Ignore; // hit-testing is done by the owning bar/manipulator, not the handle itself
            style.position = Position.Absolute;
        }

        public void SetColor(Color color) => style.backgroundColor = color;

        public void SetAlphaSwatch(float alpha) => style.backgroundColor = new Color(alpha, alpha, alpha, 1f);
    }
}
