using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace ABCodeworld.Gradients.Editor
{
    /// <summary>
    /// A labelled key source: drag it onto a <see cref="GradientBarElement"/> to place a new colour or
    /// alpha key at the drop, or click it to add one at the middle of the bar.
    /// </summary>
    /// <remarks>
    /// This used to be a bare coloured rectangle. It carried no label, no tooltip and no text anywhere
    /// in the window, so the only way to discover it was a drag source was to try dragging it — and a
    /// click, which is what most people try first, silently added a key at t = 0, because the drop
    /// handler clamped the swatch's own position into the bar. It now says what it is, and a click
    /// means something.
    /// </remarks>
    internal sealed class KeyPaletteDraggable : VisualElement
    {
        /// <summary>
        /// How far the pointer must travel before a press counts as a drag rather than a click.
        /// </summary>
        /// <remarks>
        /// Without a threshold there is no click at all: the old code latched into dragging on
        /// pointer-down and always raised <see cref="Dropped"/> on pointer-up.
        /// </remarks>
        private const float DragThreshold = 4f;

        public readonly bool IsAlpha;

        private readonly VisualElement swatch;

        public event Action<Vector2, bool> Dragging;
        public event Action<Vector2, bool> Dropped;

        /// <summary>Raised when the press never moved far enough to be a drag.</summary>
        public event Action<bool> ClickAdd;

        private Vector2 pressPosition;
        private bool pressed;
        private bool isDragging;
        private int capturedPointerId = -1;

        public KeyPaletteDraggable(bool isAlpha)
        {
            IsAlpha = isAlpha;
            AddToClassList("abcw-keysource");
            AddToClassList("abcw-palette-key");
            EnableInClassList("abcw-palette-key--alpha", isAlpha);
            pickingMode = PickingMode.Position;
            tooltip = isAlpha
                ? "Drag onto the bar's top lane to place an alpha key, or click to add one at the middle."
                : "Drag onto the bar's bottom lane to place a colour key, or click to add one at the middle.";

            // Every child is ignored by picking: a child that answers the pointer swallows the
            // PointerDownEvent before it reaches this element, and pressing the label would not drag.
            swatch = new VisualElement { name = "swatch", pickingMode = PickingMode.Ignore };
            swatch.AddToClassList("abcw-keysource__swatch");
            Add(swatch);

            var title = new Label(isAlpha ? "Alpha" : "Colour") { pickingMode = PickingMode.Ignore };
            title.AddToClassList("abcw-keysource__title");
            Add(title);

            var hint = new Label("drag → bar") { pickingMode = PickingMode.Ignore };
            hint.AddToClassList("abcw-keysource__hint");
            Add(hint);

            RegisterCallback<PointerDownEvent>(OnPointerDown);
            RegisterCallback<PointerMoveEvent>(OnPointerMove);
            RegisterCallback<PointerUpEvent>(OnPointerUp);
            RegisterCallback<PointerCaptureOutEvent>(_ => EndDrag());
        }

        /// <summary>
        /// Tints the swatch. Alpha sources ignore the colour and show a flat grey, because an alpha key
        /// has no colour of its own.
        /// </summary>
        public void SetColor(Color color) =>
            swatch.style.backgroundColor = IsAlpha ? new Color(0.55f, 0.55f, 0.55f) : color;

        private void OnPointerDown(PointerDownEvent evt)
        {
            pressed = true;
            isDragging = false;
            pressPosition = evt.position;
            capturedPointerId = evt.pointerId;
            this.CapturePointer(evt.pointerId);
            evt.StopPropagation();
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (!pressed)
                return;

            // The ghost preview only appears once this really is a drag, or every click would flash one.
            if (!isDragging && Vector2.Distance(evt.position, pressPosition) < DragThreshold)
                return;

            if (!isDragging)
            {
                isDragging = true;
                AddToClassList("abcw-palette-key--dragging");
            }

            Dragging?.Invoke(evt.position, IsAlpha);
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (!pressed)
                return;

            bool wasDragging = isDragging;
            EndDrag();

            if (wasDragging)
                Dropped?.Invoke(evt.position, IsAlpha);
            else
                ClickAdd?.Invoke(IsAlpha);
        }

        private void EndDrag()
        {
            if (!pressed)
                return;

            pressed = false;
            isDragging = false;
            RemoveFromClassList("abcw-palette-key--dragging");
            if (capturedPointerId >= 0 && this.HasPointerCapture(capturedPointerId))
                this.ReleasePointer(capturedPointerId);
            capturedPointerId = -1;
        }
    }
}
