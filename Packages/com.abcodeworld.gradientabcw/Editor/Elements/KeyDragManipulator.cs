using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace ABCodeworld.Gradients.Editor
{
    /// <summary>
    /// Owns pointer capture and drag state for dragging a key handle. The element supplies hit-testing
    /// and mutation through the delegate hooks below, keeping the drag mechanics reusable and
    /// independent of how the keys are laid out, converted or stored.
    /// </summary>
    /// <remarks>
    /// Serves both the 1D bar, where a drag is one time along a strip, and the 3D cube, where it is a
    /// point in a plane and the modifier held decides which plane. That is why the hooks carry the raw
    /// local position and the modifiers rather than an already-converted value: the two elements read
    /// the same pointer very differently, and the conversion is theirs, not this class's.
    /// </remarks>
    internal sealed class KeyDragManipulator : PointerManipulator
    {
        public Func<Vector2, (int index, bool isAlpha)?> HitTest;
        public Action<int, bool> OnSelect;

        /// <summary>
        /// Raised once, with the position the drag was started from. Lets an element anchor a drag —
        /// the offset between the pointer and the key it grabbed, say — instead of inferring it later.
        /// </summary>
        public Action<int, bool, Vector2, EventModifiers> OnDragStart;

        public Action<int, bool, Vector2, EventModifiers> OnDrag;
        public Func<int, bool, Vector2, bool> ShouldRemove;
        public Action<int, bool> OnRemove;
        public Action OnDragEnd;

        private int dragIndex = -1;
        private bool dragIsAlpha;
        private bool dragging;

        /// <summary>
        /// Starts a left-button drag under each of <paramref name="modifierSets"/>, or under no modifier
        /// at all when none are given.
        /// </summary>
        /// <remarks>
        /// An activation filter matches an event's modifiers exactly, so a manipulator that should also
        /// answer to Shift has to say so — and one that must leave Shift to its element has to stay
        /// quiet. Both cases are real here: the cube reads Shift as "move along Y instead", while on the
        /// 1D bar Shift-clicking adds a colour key and must not be swallowed as a drag.
        /// </remarks>
        public KeyDragManipulator(params EventModifiers[] modifierSets)
        {
            if (modifierSets == null || modifierSets.Length == 0)
                modifierSets = new[] { EventModifiers.None };

            foreach (EventModifiers modifiers in modifierSets)
                activators.Add(new ManipulatorActivationFilter { button = MouseButton.LeftMouse, modifiers = modifiers });
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

            // An element may carry its own controls over the draggable area — the cube viewport has a
            // view toggle in one corner — and their pointer events bubble through here. Starting a drag
            // from one would mean pressing a button also moved whatever key sat behind it. Both elements'
            // own children are PickingMode.Ignore, so this only ever rejects a real control.
            if (evt.target != target)
                return;

            var hit = HitTest?.Invoke(evt.localPosition);
            if (!hit.HasValue)
                return;

            dragIndex = hit.Value.index;
            dragIsAlpha = hit.Value.isAlpha;
            dragging = true;

            OnSelect?.Invoke(dragIndex, dragIsAlpha);
            OnDragStart?.Invoke(dragIndex, dragIsAlpha, evt.localPosition, evt.modifiers);
            target.CapturePointer(evt.pointerId);
            evt.StopImmediatePropagation();
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (!dragging || !target.HasPointerCapture(evt.pointerId))
                return;

            OnDrag?.Invoke(dragIndex, dragIsAlpha, evt.localPosition, evt.modifiers);

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
