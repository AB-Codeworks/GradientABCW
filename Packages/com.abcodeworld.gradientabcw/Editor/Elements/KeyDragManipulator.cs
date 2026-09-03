using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace ABCodeworld.Gradients.Editor
{
    /// <summary>
    /// Owns pointer capture and drag state for dragging a key handle within its bar. The bar supplies
    /// hit-testing, time conversion and mutation through the delegate hooks below, keeping the drag
    /// mechanics reusable and independent of how the bar renders or stores keys.
    /// </summary>
    internal sealed class KeyDragManipulator : PointerManipulator
    {
        public Func<Vector2, (int index, bool isAlpha)?> HitTest;
        public Func<Vector2, float> TimeFromPosition;
        public Action<int, bool> OnSelect;
        public Action<int, bool, float> OnDrag;
        public Func<int, bool, Vector2, bool> ShouldRemove;
        public Action<int, bool> OnRemove;
        public Action OnDragEnd;

        private int dragIndex = -1;
        private bool dragIsAlpha;
        private bool dragging;

        public KeyDragManipulator()
        {
            activators.Add(new ManipulatorActivationFilter { button = MouseButton.LeftMouse });
        }

        /// <summary>Called by the bar after a mutation re-sorts keys and the dragged key's index changes.</summary>
        public void UpdateDragIndex(int newIndex) => dragIndex = newIndex;

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

            var hit = HitTest?.Invoke(evt.localPosition);
            if (!hit.HasValue)
                return;

            dragIndex = hit.Value.index;
            dragIsAlpha = hit.Value.isAlpha;
            dragging = true;

            OnSelect?.Invoke(dragIndex, dragIsAlpha);
            target.CapturePointer(evt.pointerId);
            evt.StopImmediatePropagation();
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (!dragging || !target.HasPointerCapture(evt.pointerId))
                return;

            float t = TimeFromPosition?.Invoke(evt.localPosition) ?? 0f;
            OnDrag?.Invoke(dragIndex, dragIsAlpha, t);

            if (ShouldRemove != null && ShouldRemove(dragIndex, dragIsAlpha, evt.localPosition))
            {
                OnRemove?.Invoke(dragIndex, dragIsAlpha);
                EndDrag(evt.pointerId);
            }
            evt.StopPropagation();
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (!dragging)
                return;
            EndDrag(evt.pointerId);
        }

        private void EndDrag(int pointerId)
        {
            dragging = false;
            if (target.HasPointerCapture(pointerId))
                target.ReleasePointer(pointerId);
            OnDragEnd?.Invoke();
        }
    }
}
