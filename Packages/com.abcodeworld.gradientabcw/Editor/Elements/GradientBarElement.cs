using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ABCodeworld.Gradients.Editor
{
    /// <summary>
    /// The interactive gradient strip: an alpha lane (top) and colour lane (bottom) of draggable key
    /// handles over a base preview. Shift-click adds a colour key, Alt-click adds an alpha key, Delete
    /// removes the selection, and dragging a key beyond its lane removes it.
    /// </summary>
    internal sealed class GradientBarElement : VisualElement
    {
        // Layout constants and the arithmetic over them live in BarGeometry; this class keeps input
        // handling, handle elements and mutation. NeighbourClampEpsilon used to be declared here too and
        // was never read: the clamping it named is done by GradientABCW.
        private const float LaneHeight = BarGeometry.LaneHeight;
        private const float PadX = BarGeometry.PadX;

        private readonly GradientPreviewElement preview;
        private readonly VisualElement handleLayer;
        private readonly VisualElement ghost;
        private readonly List<GradientKeyHandle> colorHandles = new();
        private readonly List<GradientKeyHandle> alphaHandles = new();
        private readonly KeyDragManipulator dragManipulator;

        private GradientABCW gradient;
        private int selectedIndex = -1;
        private bool selectedIsAlpha;
        private int lastSyncedVersion = -1;

        /// <remarks>
        /// Re-assigning the same instance is a refresh request, not an invalidation — the picker does
        /// exactly that after every edit. Clearing <c>lastSyncedVersion</c> unconditionally defeated the
        /// version check in <see cref="SyncHandles"/>, so every pointer-move of a drag rebuilt all handle
        /// styles and forced a preview re-bake even when nothing had changed.
        /// </remarks>
        public GradientABCW Gradient
        {
            get => gradient;
            set
            {
                if (!ReferenceEquals(gradient, value))
                {
                    gradient = value;
                    lastSyncedVersion = -1;
                }
                SyncHandles();
            }
        }

        public float HoverTime { get; private set; }

        public event Action<int, bool> KeySelected;

        public GradientBarElement()
        {
            AddToClassList("abcw-bar");
            focusable = true;
            pickingMode = PickingMode.Position;
            style.position = Position.Relative;

            preview = new GradientPreviewElement { Mode = GradientPreviewElement.PreviewMode.Base, pickingMode = PickingMode.Ignore };
            preview.style.position = Position.Absolute;
            preview.style.left = PadX;
            preview.style.right = PadX;
            preview.style.top = LaneHeight;
            preview.style.bottom = LaneHeight;
            Add(preview);

            handleLayer = new VisualElement { pickingMode = PickingMode.Ignore };
            handleLayer.style.position = Position.Absolute;
            handleLayer.style.left = 0; handleLayer.style.right = 0; handleLayer.style.top = 0; handleLayer.style.bottom = 0;
            Add(handleLayer);

            ghost = new VisualElement { pickingMode = PickingMode.Ignore };
            ghost.AddToClassList("abcw-key");
            ghost.AddToClassList("abcw-key--ghost");
            ghost.style.position = Position.Absolute;
            ghost.style.display = DisplayStyle.None;
            Add(ghost);

            dragManipulator = new KeyDragManipulator
            {
                HitTest = TryHitHandle,
                OnSelect = Select,
                OnDrag = (index, isAlpha, position, _) => HandleDrag(index, isAlpha, GetTimeFromLocalPosition(position)),
                ShouldRemove = ShouldRemoveAt,
                OnRemove = (index, isAlpha) => RemoveKey(index, isAlpha),
            };
            this.AddManipulator(dragManipulator);

            RegisterCallback<PointerDownEvent>(OnBarPointerDown);
            RegisterCallback<PointerMoveEvent>(evt => HoverTime = GetTimeFromLocalPosition(evt.localPosition));
            RegisterCallback<KeyDownEvent>(OnKeyDown);
            // A resize moves handles but changes no key data, so it takes the cheap reposition path rather
            // than re-deriving every handle's colour and selection state and re-assigning the preview.
            RegisterCallback<GeometryChangedEvent>(_ => RepositionHandles());
        }

        public float GetTimeFromPanelPosition(Vector2 panelPosition)
        {
            Vector2 local = this.WorldToLocal(panelPosition);
            return GetTimeFromLocalPosition(local);
        }

        public void SetSelected(int index, bool isAlpha)
        {
            selectedIndex = index;
            selectedIsAlpha = isAlpha;
            SyncHandles(force: true);
        }

        public void SetGhost(bool active, bool isAlpha, float time)
        {
            if (!active)
            {
                ghost.style.display = DisplayStyle.None;
                return;
            }
            ghost.style.display = DisplayStyle.Flex;
            var r = HandleRect(Mathf.Clamp01(time), isAlpha);
            ghost.style.left = r.x; ghost.style.top = r.y; ghost.style.width = r.width; ghost.style.height = r.height;
            ghost.style.backgroundColor = isAlpha ? new Color(0.55f, 0.55f, 0.55f) : Color.black;
        }

        private BarGeometry Geometry => new BarGeometry(contentRect);

        private float GetTimeFromLocalPosition(Vector2 localPosition) => Geometry.TimeAt(localPosition);

        private Rect HandleRect(float time, bool isAlpha) => Geometry.HandleRect(time, isAlpha);

        private void SyncHandles(bool force = false)
        {
            if (gradient == null)
                return;
            if (!force && lastSyncedVersion == gradient.Version)
                return;

            EnsureHandleCount(colorHandles, gradient.ColorKeys.Length, isAlpha: false);
            EnsureHandleCount(alphaHandles, gradient.AlphaKeys.Length, isAlpha: true);

            var colorKeys = gradient.ColorKeys;
            for (int i = 0; i < colorHandles.Count; i++)
            {
                colorHandles[i].Index = i;
                colorHandles[i].SetColor(colorKeys[i].color);
                colorHandles[i].Selected = !selectedIsAlpha && selectedIndex == i;
                PositionHandle(colorHandles[i], colorKeys[i].time, false);
            }

            var alphaKeys = gradient.AlphaKeys;
            for (int i = 0; i < alphaHandles.Count; i++)
            {
                alphaHandles[i].Index = i;
                alphaHandles[i].SetAlphaSwatch(alphaKeys[i].alpha);
                alphaHandles[i].Selected = selectedIsAlpha && selectedIndex == i;
                PositionHandle(alphaHandles[i], alphaKeys[i].time, true);
            }

            preview.Gradient = gradient;
            lastSyncedVersion = gradient.Version;
        }

        /// <summary>
        /// Moves the existing handles to match the current layout, without touching key data, colours,
        /// selection or the preview. Safe to call on every <see cref="GeometryChangedEvent"/>.
        /// </summary>
        private void RepositionHandles()
        {
            if (gradient == null)
                return;

            var colorKeys = gradient.ColorKeys;
            for (int i = 0; i < colorHandles.Count && i < colorKeys.Length; i++)
                PositionHandle(colorHandles[i], colorKeys[i].time, false);

            var alphaKeys = gradient.AlphaKeys;
            for (int i = 0; i < alphaHandles.Count && i < alphaKeys.Length; i++)
                PositionHandle(alphaHandles[i], alphaKeys[i].time, true);

            // The first layout pass is also the first chance the preview has to bake, so give it one
            // refresh; it early-outs on its own version stamp from then on.
            preview.Refresh();
        }

        private void PositionHandle(GradientKeyHandle handle, float time, bool isAlpha)
        {
            var r = HandleRect(time, isAlpha);
            handle.style.left = r.x; handle.style.top = r.y; handle.style.width = r.width; handle.style.height = r.height;
        }

        private void EnsureHandleCount(List<GradientKeyHandle> list, int count, bool isAlpha)
        {
            while (list.Count < count)
            {
                var handle = new GradientKeyHandle(isAlpha);
                list.Add(handle);
                handleLayer.Add(handle);
            }
            while (list.Count > count)
            {
                handleLayer.Remove(list[^1]);
                list.RemoveAt(list.Count - 1);
            }
        }

        private (int index, bool isAlpha)? TryHitHandle(Vector2 localPosition)
        {
            if (gradient == null)
                return null;

            // Resolved once rather than rebuilt inside the loop for each of up to 64 keys.
            var geometry = Geometry;

            var alphaKeys = gradient.AlphaKeys;
            for (int i = 0; i < alphaKeys.Length; i++)
            {
                if (geometry.HandleRect(alphaKeys[i].time, true).Contains(localPosition))
                    return (i, true);
            }
            var colorKeys = gradient.ColorKeys;
            for (int i = 0; i < colorKeys.Length; i++)
            {
                if (geometry.HandleRect(colorKeys[i].time, false).Contains(localPosition))
                    return (i, false);
            }
            return null;
        }

        private void Select(int index, bool isAlpha)
        {
            selectedIndex = index;
            selectedIsAlpha = isAlpha;
            SyncHandles(force: true);
            KeySelected?.Invoke(index, isAlpha);
        }

        private void HandleDrag(int index, bool isAlpha, float time)
        {
            if (gradient == null)
                return;

            int newIndex;
            if (isAlpha)
            {
                float clamped = gradient.ClampAlphaKeyTime(index, time);
                newIndex = gradient.SetAlphaKey(index, new AlphaKey(gradient.AlphaKeys[index].alpha, clamped));
            }
            else
            {
                float clamped = gradient.ClampColorKeyTime(index, time);
                newIndex = gradient.SetColorKey(index, new ColorKey(gradient.ColorKeys[index].color, clamped));
            }

            selectedIndex = newIndex;
            selectedIsAlpha = isAlpha;
            dragManipulator.UpdateDragIndex(newIndex);
            SyncHandles(force: true);
            RaiseChanged();
        }

        private bool ShouldRemoveAt(int index, bool isAlpha, Vector2 localPosition) =>
            Geometry.IsOutsideLane(isAlpha, localPosition);

        private void RemoveKey(int index, bool isAlpha)
        {
            if (gradient == null)
                return;

            bool removed = isAlpha ? gradient.RemoveAlphaKey(index) : gradient.RemoveColorKey(index);
            if (!removed)
                return;

            selectedIndex = -1;
            SyncHandles(force: true);
            RaiseChanged();
        }

        private void OnBarPointerDown(PointerDownEvent evt)
        {
            if (gradient == null)
                return;

            float t = GetTimeFromLocalPosition(evt.localPosition);

            if ((evt.modifiers & EventModifiers.Shift) != 0)
            {
                if (gradient.CanAddColorKey)
                {
                    var sampled = gradient.EvaluateBase(t);
                    int index = gradient.AddColorKey(sampled, t);
                    Select(index, false);
                    RaiseChanged();
                }
                evt.StopPropagation();
                return;
            }

            if ((evt.modifiers & EventModifiers.Alt) != 0)
            {
                if (gradient.CanAddAlphaKey)
                {
                    float a = gradient.EvaluateBase(t).a;
                    int index = gradient.AddAlphaKey(a, t);
                    Select(index, true);
                    RaiseChanged();
                }
                evt.StopPropagation();
                return;
            }

            // No handle was hit (the manipulator would have consumed it otherwise): select the nearest key.
            var (index2, isAlpha2) = FindNearestKey(t);
            if (index2 >= 0)
                Select(index2, isAlpha2);
        }

        private (int index, bool isAlpha) FindNearestKey(float t)
        {
            int index = -1;
            bool isAlpha = false;
            float best = float.MaxValue;

            var colorKeys = gradient.ColorKeys;
            for (int i = 0; i < colorKeys.Length; i++)
            {
                float d = Mathf.Abs(colorKeys[i].time - t);
                if (d < best) { best = d; index = i; isAlpha = false; }
            }
            var alphaKeys = gradient.AlphaKeys;
            for (int i = 0; i < alphaKeys.Length; i++)
            {
                float d = Mathf.Abs(alphaKeys[i].time - t);
                if (d < best) { best = d; index = i; isAlpha = true; }
            }
            return (index, isAlpha);
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode != KeyCode.Delete && evt.keyCode != KeyCode.Backspace)
                return;
            if (selectedIndex < 0 || gradient == null)
                return;

            bool removed = selectedIsAlpha ? gradient.RemoveAlphaKey(selectedIndex) : gradient.RemoveColorKey(selectedIndex);
            if (removed)
            {
                selectedIndex = -1;
                SyncHandles(force: true);
                RaiseChanged();
                evt.StopPropagation();
            }
        }

        private void RaiseChanged()
        {
            using var evt = GradientChangedEvent.GetPooled(gradient, GradientChangedEvent.ChangeKind.Keys);
            evt.target = this;
            SendEvent(evt);
        }
    }
}
