using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace ABCodeworld.Gradients.Editor
{
    /// <summary>
    /// Owns pointer capture and drag state for rotating the cube viewport. The element supplies what to do
    /// with the movement through the hooks below, keeping the drag mechanics independent of how the cube
    /// is projected or drawn.
    /// </summary>
    /// <remarks>
    /// Shaped after <see cref="KeyDragManipulator"/>, but far simpler: there is nothing to hit-test and
    /// nothing to remove, because the cube has no draggable keys. Rotation is all it does.
    /// </remarks>
    internal sealed class CubeRotateManipulator : PointerManipulator
    {
        /// <summary>Raised with the pointer movement since the last event, in pixels.</summary>
        public Action<Vector2> OnRotate;

        public Action OnRotateEnd;

        private Vector2 lastPosition;
        private bool dragging;

        public CubeRotateManipulator()
        {
            activators.Add(new ManipulatorActivationFilter { button = MouseButton.LeftMouse });
        }

        protected override void RegisterCallbacksOnTarget()
        {
            target.RegisterCallback<PointerDownEvent>(OnPointerDown);
            target.RegisterCallback<PointerMoveEvent>(OnPointerMove);
            target.RegisterCallback<PointerUpEvent>(OnPointerUp);
        }

        protected override void UnregisterCallbacksFromTarget()
        {
            target.UnregisterCallback<PointerDownEvent>(OnPointerDown);
            target.UnregisterCallback<PointerMoveEvent>(OnPointerMove);
            target.UnregisterCallback<PointerUpEvent>(OnPointerUp);
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            if (!CanStartManipulation(evt))
                return;

            // The viewport carries its own controls — a view toggle sits over one corner of it — and their
            // pointer events bubble through here. Starting a rotate from one would mean pressing a button
            // also spun the cube.
            if (evt.target != target)
                return;

            lastPosition = evt.localPosition;
            dragging = true;
            target.CapturePointer(evt.pointerId);
            evt.StopImmediatePropagation();
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (!dragging || !target.HasPointerCapture(evt.pointerId))
                return;

            Vector2 position = evt.localPosition;
            OnRotate?.Invoke(position - lastPosition);
            lastPosition = position;
            evt.StopPropagation();
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (!dragging)
                return;

            dragging = false;
            if (target.HasPointerCapture(evt.pointerId))
                target.ReleasePointer(evt.pointerId);
            OnRotateEnd?.Invoke();
        }
    }
}
