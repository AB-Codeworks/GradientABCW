using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace ABCodeworld.Gradients.Editor
{
    /// <summary>A drag source that spawns a new colour or alpha key when dropped onto a <see cref="GradientBarElement"/>.</summary>
    internal sealed class KeyPaletteDraggable : VisualElement
    {
        public readonly bool IsAlpha;

        public event Action<Vector2, bool> Dragging;
        public event Action<Vector2, bool> Dropped;

        private bool isDragging;
        private int capturedPointerId = -1;

        public KeyPaletteDraggable(bool isAlpha)
        {
            IsAlpha = isAlpha;
            AddToClassList("abcw-palette-key");
            EnableInClassList("abcw-palette-key--alpha", isAlpha);
            pickingMode = PickingMode.Position;

            RegisterCallback<PointerDownEvent>(OnPointerDown);
            RegisterCallback<PointerMoveEvent>(OnPointerMove);
            RegisterCallback<PointerUpEvent>(OnPointerUp);
            RegisterCallback<PointerCaptureOutEvent>(_ => EndDrag());
        }

        public void SetColor(Color color) => style.backgroundColor = IsAlpha ? new Color(0.55f, 0.55f, 0.55f) : color;

        private void OnPointerDown(PointerDownEvent evt)
        {
            isDragging = true;
            capturedPointerId = evt.pointerId;
            this.CapturePointer(evt.pointerId);
            AddToClassList("abcw-palette-key--dragging");
            evt.StopPropagation();
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (isDragging)
                Dragging?.Invoke(evt.position, IsAlpha);
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (!isDragging)
                return;
            EndDrag();
            Dropped?.Invoke(evt.position, IsAlpha);
        }

        private void EndDrag()
        {
            if (!isDragging)
                return;
            isDragging = false;
            RemoveFromClassList("abcw-palette-key--dragging");
            if (capturedPointerId >= 0 && this.HasPointerCapture(capturedPointerId))
                this.ReleasePointer(capturedPointerId);
            capturedPointerId = -1;
        }
    }
}
