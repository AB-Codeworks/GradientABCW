using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ABCodeworld.Gradients.Editor
{
    /// <summary>
    /// The 3D picker's main viewport: a wireframe cube showing every key as a coloured dot inside it,
    /// turned by right-dragging and authored by left-dragging its dots, or the same cube rendered solid
    /// at the same rotation.
    /// </summary>
    /// <remarks>
    /// This is what stands in for the 1D picker's gradient bar, and it now does the same two jobs: it is
    /// the live preview, because the dots carry their own keys' colours and turning the cube reads as a
    /// point cloud of the gradient, and it is where keys are placed.
    /// <para>
    /// A pointer gives two coordinates and a key needs three, so a drag has to choose a plane. It moves
    /// the key across the cube-local XZ plane it already sits in, following the pointer exactly through
    /// <see cref="CubeGeometry.TryUnprojectOntoPlaneY"/>, and Shift switches to the Y axis alone. Only
    /// keys of the kind being edited answer to a drag, because a dot alone does not say which kind you
    /// meant — the same ambiguity the mode buttons exist to settle. The key lists are still the way to
    /// type an exact coordinate, and they track a drag as it happens.
    /// </para>
    /// <para>
    /// Rotation is on the right button so the left is free for the keys. The two manipulators sit on
    /// this one element and ignore each other's presses through their own activation filters.
    /// </para>
    /// <para>
    /// The solid toggle covers the rest, rendering the same cube through
    /// <see cref="CubePreviewRasterizer"/> at the same rotation and the same projection, so the two views
    /// line up exactly rather than jumping. Keys cannot be dragged there, because none are drawn.
    /// </para>
    /// <para>
    /// Drawn with UI Toolkit's own vector API rather than IMGUI Handles or a PreviewRenderUtility camera.
    /// The package is pure UI Toolkit by design, a render camera would bring a RenderTexture lifetime into
    /// an editor window that has already had one texture leak fixed, and neither alternative can be driven
    /// headlessly the way twelve stroked lines and a few positioned elements can.
    /// </para>
    /// </remarks>
    internal sealed class GradientCubeElement : VisualElement
    {
        /// <summary>A corner-on default, so all three visible faces and the cube's depth read immediately.</summary>
        public const float DefaultYaw = 45f;
        public const float DefaultPitch = 30f;

        /// <summary>
        /// Pixel size the solid view is rendered at before being stretched over the viewport.
        /// </summary>
        /// <remarks>
        /// Capped well below the viewport's own size on purpose. Every pixel of a solid render costs a ray
        /// cast plus a pass over every key, and a rotate drag asks for a fresh render on every pointer
        /// move; rendering at the viewport's full size would make dragging in solid view crawl. Stretched
        /// up by the panel, which is exactly what a preview can afford to do.
        /// </remarks>
        internal const int SolidRenderSize = 128;

        private static readonly Color FrontEdgeColor = new Color(0.84f, 0.91f, 1f, 0.95f);
        private static readonly Color BackEdgeColor = new Color(0.84f, 0.91f, 1f, 0.30f);

        private readonly Image checkerImage;
        private readonly Image solidImage;
        private readonly VisualElement wireLayer;
        private readonly VisualElement dotLayer;
        private readonly Button viewToggle;
        private readonly CubePreviewTexture solidTexture = new();
        private readonly List<VisualElement> dots = new();
        private readonly List<int> dotOrder = new();

        // Held rather than allocated per layout: a rotate drag lays the dots out on every pointer move,
        // and both the depth buffer and the comparison delegate would otherwise be garbage each time.
        private float[] dotDepths = Array.Empty<float>();
        private readonly Comparison<int> farthestFirst;

        private GradientABCW3D gradient;
        private float yaw = DefaultYaw;
        private float pitch = DefaultPitch;
        private bool solidView;
        private bool alphaMode;
        private bool includeModulation;

        private int lastSyncedVersion = -1;
        private int lastColorCount = -1;
        private int lastAlphaCount = -1;
        private IVisualElementScheduledItem pendingSolidRender;

        private int selectedIndex = -1;
        private bool selectedIsAlpha;

        // Everything a drag needs to map pointer movement to cube movement, captured when the drag
        // starts and whenever Shift flips mid-drag. Held rather than re-derived per move so that a key
        // clamped against a cube wall cannot feed its clamped position back into the mapping and drift.
        private bool dragVertical;
        private float dragPlaneY;
        private Vector3 dragGrabOffset;
        private float dragAnchorPointerY;
        private float dragAnchorKeyY;
        private float dragPixelsPerUnitY;

        /// <summary>How many times the dot elements have been torn down and rebuilt. Exposed for tests.</summary>
        internal int RebuildCount { get; private set; }

        /// <summary>How many times the solid view has been re-rendered. Exposed for tests.</summary>
        internal int SolidRenderCount { get; private set; }

        public float Yaw => yaw;
        public float Pitch => pitch;

        /// <summary>The key a pointer last grabbed, or -1. Read with <see cref="SelectedIsAlpha"/>.</summary>
        public int SelectedIndex => selectedIndex;

        public bool SelectedIsAlpha => selectedIsAlpha;

        /// <summary>Raised when a drag grabs a key, so the key lists can follow the selection.</summary>
        public event Action<int, bool> KeySelected;

        /// <summary>
        /// The gradient shown. Assigning the instance already held is a refresh, not an invalidation —
        /// the same contract <see cref="GradientPreviewElement.Gradient"/> documents, and for the same
        /// reason: callers refresh by re-assigning.
        /// </summary>
        public GradientABCW3D Gradient
        {
            get => gradient;
            set
            {
                if (!ReferenceEquals(gradient, value))
                {
                    gradient = value;
                    lastSyncedVersion = -1;
                }
                Refresh();
            }
        }

        /// <summary>
        /// Which kind of key the picker is editing. The other kind's dots are drawn muted, and only this
        /// kind's answer to a drag.
        /// </summary>
        public bool AlphaMode
        {
            get => alphaMode;
            set
            {
                if (alphaMode == value)
                    return;
                alphaMode = value;
                lastSyncedVersion = -1;
                Refresh();
            }
        }

        /// <summary>True to show the modulated result in the solid view, false to show the base gradient.</summary>
        public bool IncludeModulation
        {
            get => includeModulation;
            set
            {
                if (includeModulation == value)
                    return;
                includeModulation = value;
                RequestSolidRender();
            }
        }

        /// <summary>True when the viewport shows the solid cube instead of the wireframe and its keys.</summary>
        public bool SolidView
        {
            get => solidView;
            set
            {
                if (solidView == value)
                    return;

                solidView = value;
                ApplyViewMode();
            }
        }

        public GradientCubeElement()
        {
            AddToClassList("abcw-cube");
            style.position = Position.Relative;
            farthestFirst = (a, b) => dotDepths[b].CompareTo(dotDepths[a]);

            checkerImage = new Image { name = "checker", pickingMode = PickingMode.Ignore, scaleMode = ScaleMode.StretchToFill, image = CheckerTexture.Image };
            checkerImage.style.position = Position.Absolute;
            Add(checkerImage);

            solidImage = new Image { name = "solid", pickingMode = PickingMode.Ignore, scaleMode = ScaleMode.StretchToFill };
            solidImage.style.position = Position.Absolute;
            Add(solidImage);

            wireLayer = new VisualElement { name = "wireframe", pickingMode = PickingMode.Ignore };
            StretchToParent(wireLayer);
            wireLayer.generateVisualContent += DrawWireframe;
            Add(wireLayer);

            dotLayer = new VisualElement { name = "dots", pickingMode = PickingMode.Ignore };
            StretchToParent(dotLayer);
            Add(dotLayer);

            viewToggle = new Button(() => SolidView = !SolidView) { name = "viewToggle", text = "Solid" };
            viewToggle.AddToClassList("abcw-btn");
            viewToggle.AddToClassList("abcw-cube__toggle");
            Add(viewToggle);

            this.AddManipulator(new CubeRotateManipulator
            {
                OnRotate = delta =>
                {
                    (yaw, pitch) = CubeGeometry.Rotate(yaw, pitch, delta);
                    OnRotationChanged();
                },
            });

            // Shift is a modifier the drag reads rather than one it should decline, so it is named as an
            // activator too; without it a Shift-drag would never start.
            this.AddManipulator(new KeyDragManipulator(EventModifiers.None, EventModifiers.Shift)
            {
                HitTest = TryHitDot,
                OnSelect = Select,
                OnDragStart = BeginKeyDrag,
                OnDrag = DragKey,
            });

            RegisterCallback<GeometryChangedEvent>(_ => OnRotationChanged());
            RegisterCallback<DetachFromPanelEvent>(_ => solidTexture.Dispose());

            ApplyViewMode();
        }

        /// <summary>
        /// Marks a key as the selected one, or clears the selection with a negative index. Kept in step
        /// with the key lists by the picker window, in both directions.
        /// </summary>
        public void SetSelected(int index, bool isAlpha)
        {
            if (selectedIndex == index && selectedIsAlpha == isAlpha)
                return;

            selectedIndex = index;
            selectedIsAlpha = isAlpha;

            // Selection is not part of the gradient, so the version stamp has not moved and Refresh
            // would style nothing. Restyling outright is the whole of the work either way.
            if (gradient != null && dots.Count == gradient.ColorKeys.Length + gradient.AlphaKeys.Length)
                StyleDots();
        }

        public void Refresh()
        {
            if (gradient == null)
                return;

            int colorCount = gradient.ColorKeys.Length;
            int alphaCount = gradient.AlphaKeys.Length;

            if (colorCount != lastColorCount || alphaCount != lastAlphaCount)
            {
                RebuildDots(colorCount + alphaCount);
                lastColorCount = colorCount;
                lastAlphaCount = alphaCount;
                lastSyncedVersion = -1;
            }

            if (lastSyncedVersion != gradient.Version)
            {
                StyleDots();
                lastSyncedVersion = gradient.Version;
            }

            LayoutDots();
            wireLayer.MarkDirtyRepaint();
            RequestSolidRender();
        }

        private static void StretchToParent(VisualElement e)
        {
            e.style.position = Position.Absolute;
            e.style.left = 0; e.style.right = 0; e.style.top = 0; e.style.bottom = 0;
        }

        private void ApplyViewMode()
        {
            checkerImage.style.display = solidView ? DisplayStyle.Flex : DisplayStyle.None;
            solidImage.style.display = solidView ? DisplayStyle.Flex : DisplayStyle.None;
            wireLayer.style.display = solidView ? DisplayStyle.None : DisplayStyle.Flex;
            dotLayer.style.display = solidView ? DisplayStyle.None : DisplayStyle.Flex;
            viewToggle.text = solidView ? "Keys" : "Solid";
            viewToggle.tooltip = solidView
                ? "Show the wireframe cube and its keys."
                : "Show the gradient as a solid cube at this rotation.";

            if (solidView)
                RequestSolidRender();
        }

        private void OnRotationChanged()
        {
            LayoutDots();
            wireLayer.MarkDirtyRepaint();
            RequestSolidRender();
        }

        private void DrawWireframe(MeshGenerationContext ctx)
        {
            var geometry = new CubeGeometry(wireLayer.contentRect, yaw, pitch);
            var painter = ctx.painter2D;

            // Back edges first, so the front ones stroke over them where they cross.
            StrokeEdges(painter, in geometry, hidden: true, color: BackEdgeColor, width: 1f);
            StrokeEdges(painter, in geometry, hidden: false, color: FrontEdgeColor, width: 1.5f);
        }

        private static void StrokeEdges(Painter2D painter, in CubeGeometry geometry, bool hidden, Color color, float width)
        {
            painter.strokeColor = color;
            painter.lineWidth = width;
            painter.BeginPath();

            for (int i = 0; i < CubeGeometry.EdgeCount; i++)
            {
                if (geometry.IsEdgeHidden(i) != hidden)
                    continue;

                (Vector3 a, Vector3 b) = CubeGeometry.Edge(i);
                painter.MoveTo(geometry.Project(a));
                painter.LineTo(geometry.Project(b));
            }

            painter.Stroke();
        }

        private void RebuildDots(int total)
        {
            // A key added or removed shifts every later index, so the stored one no longer names what
            // it did. Cheaper and safer to drop the selection than to guess where it went.
            selectedIndex = -1;

            dotLayer.Clear();
            dots.Clear();
            if (dotDepths.Length != total)
                dotDepths = new float[total];

            for (int i = 0; i < total; i++)
            {
                var dot = new VisualElement { pickingMode = PickingMode.Ignore };
                dot.AddToClassList("abcw-cube-key");
                dot.style.position = Position.Absolute;
                dots.Add(dot);
                dotLayer.Add(dot);
            }

            RebuildCount++;
        }

        /// <summary>Applies each key's own colour, kind and mode highlighting. Only depends on key values.</summary>
        private void StyleDots()
        {
            var colorKeys = gradient.ColorKeys;
            var alphaKeys = gradient.AlphaKeys;

            for (int i = 0; i < colorKeys.Length; i++)
            {
                var dot = dots[i];
                dot.EnableInClassList("abcw-cube-key--alpha", false);
                dot.EnableInClassList("abcw-cube-key--muted", alphaMode);
                dot.EnableInClassList("abcw-cube-key--selected", !selectedIsAlpha && selectedIndex == i);
                dot.style.backgroundColor = colorKeys[i].color;
                dot.tooltip = $"Colour key {i} — {Format(colorKeys[i].position)}";
            }

            for (int i = 0; i < alphaKeys.Length; i++)
            {
                var dot = dots[colorKeys.Length + i];
                float a = alphaKeys[i].alpha;
                dot.EnableInClassList("abcw-cube-key--alpha", true);
                dot.EnableInClassList("abcw-cube-key--muted", !alphaMode);
                dot.EnableInClassList("abcw-cube-key--selected", selectedIsAlpha && selectedIndex == i);

                // Alpha as luminance rather than as transparency: a dot drawn at its own alpha would
                // vanish exactly where it most needs to be visible.
                dot.style.backgroundColor = new Color(a, a, a, 1f);
                dot.tooltip = $"Alpha key {i} — {a:0.00} at {Format(alphaKeys[i].position)}";
            }
        }

        /// <summary>Re-projects every dot and re-sorts them by depth. Depends on rotation and on key positions.</summary>
        private void LayoutDots()
        {
            if (gradient == null || dots.Count == 0)
                return;

            var geometry = DotGeometry;
            var colorKeys = gradient.ColorKeys;
            var alphaKeys = gradient.AlphaKeys;

            dotOrder.Clear();
            for (int i = 0; i < dots.Count; i++)
                dotOrder.Add(i);

            for (int i = 0; i < colorKeys.Length && i < dots.Count; i++)
            {
                Place(dots[i], geometry, colorKeys[i].position);
                dotDepths[i] = geometry.Depth(colorKeys[i].position);
            }

            for (int i = 0; i < alphaKeys.Length; i++)
            {
                int index = colorKeys.Length + i;
                if (index >= dots.Count)
                    break;
                Place(dots[index], geometry, alphaKeys[i].position);
                dotDepths[index] = geometry.Depth(alphaKeys[i].position);
            }

            // Painter's algorithm: bring the furthest forward first, so nearer dots end up on top.
            dotOrder.Sort(farthestFirst);
            foreach (int index in dotOrder)
                dots[index].BringToFront();
        }

        private static void Place(VisualElement dot, in CubeGeometry geometry, Vector3 position)
        {
            Rect rect = geometry.DotRect(position);
            dot.style.left = rect.x;
            dot.style.top = rect.y;
            dot.style.width = rect.width;
            dot.style.height = rect.height;
        }

        /// <summary>The projection the dots are laid out with, and so the one a pointer is read against.</summary>
        private CubeGeometry DotGeometry => new CubeGeometry(dotLayer.contentRect, yaw, pitch);

        /// <summary>
        /// Pointer events arrive in this element's own space; the dots are laid out in the dot layer's,
        /// one border width away. A far dot is six pixels across and cannot spare that, so the two spaces
        /// are reconciled rather than assumed equal.
        /// </summary>
        private Vector2 ToDotLayerSpace(Vector2 elementLocal) => dotLayer.WorldToLocal(this.LocalToWorld(elementLocal));

        /// <summary>
        /// The key under the pointer, of the kind currently being edited, or null.
        /// </summary>
        /// <remarks>
        /// Only the edited kind answers, because a dot on its own does not say which kind you meant —
        /// the ambiguity the mode buttons exist to settle. Nothing answers in the solid view, where no
        /// dots are drawn.
        /// </remarks>
        private (int index, bool isAlpha)? TryHitDot(Vector2 elementLocal)
        {
            if (gradient == null || solidView)
                return null;

            Vector2 local = ToDotLayerSpace(elementLocal);
            var geometry = DotGeometry;

            int index = alphaMode
                ? NearestDotAt(gradient.AlphaKeys, in geometry, local)
                : NearestDotAt(gradient.ColorKeys, in geometry, local);

            if (index < 0)
                return null;

            return (index, alphaMode);
        }

        /// <summary>
        /// The nearest key whose dot contains <paramref name="local"/>, or -1.
        /// </summary>
        /// <remarks>
        /// Generic over the key kind rather than written out twice, which is what
        /// <see cref="IGradientKey3D{TSelf}"/> is for; the struct constraint keeps it free of boxing.
        /// Nearest wins so that grabbing where two dots overlap takes the one drawn on top, which is the
        /// one the painter's algorithm in <see cref="LayoutDots"/> put there.
        /// </remarks>
        private static int NearestDotAt<TKey>(ReadOnlySpan<TKey> keys, in CubeGeometry geometry, Vector2 local)
            where TKey : struct, IGradientKey3D<TKey>
        {
            int best = -1;
            float bestDepth = float.MaxValue;

            for (int i = 0; i < keys.Length; i++)
            {
                Vector3 position = keys[i].Position;
                if (!geometry.GrabRect(position).Contains(local))
                    continue;

                float depth = geometry.Depth(position);
                if (depth >= bestDepth)
                    continue;

                bestDepth = depth;
                best = i;
            }

            return best;
        }

        private void Select(int index, bool isAlpha)
        {
            SetSelected(index, isAlpha);
            KeySelected?.Invoke(index, isAlpha);
        }

        private static bool IsVertical(EventModifiers modifiers) => (modifiers & EventModifiers.Shift) != 0;

        private void BeginKeyDrag(int index, bool isAlpha, Vector2 elementLocal, EventModifiers modifiers) =>
            AnchorDrag(index, isAlpha, ToDotLayerSpace(elementLocal), IsVertical(modifiers));

        /// <summary>
        /// Fixes how the rest of this drag reads the pointer: which axis it moves along, and where the
        /// key sat relative to the pointer when it was grabbed.
        /// </summary>
        private void AnchorDrag(int index, bool isAlpha, Vector2 local, bool vertical)
        {
            if (!TryKeyPosition(index, isAlpha, out Vector3 position))
                return;

            dragVertical = vertical;
            var geometry = DotGeometry;

            if (vertical)
            {
                dragAnchorPointerY = local.y;
                dragAnchorKeyY = position.y;
                dragPixelsPerUnitY = geometry.PixelsPerUnitY(position);
                return;
            }

            dragPlaneY = position.y;

            // Without the offset the key would jump so that its centre sat under the pointer, which is
            // never what grabbing the edge of a dot was meant to say.
            dragGrabOffset = geometry.TryUnprojectOntoPlaneY(local, dragPlaneY, out Vector3 grabbed)
                ? position - grabbed
                : Vector3.zero;
        }

        private void DragKey(int index, bool isAlpha, Vector2 elementLocal, EventModifiers modifiers)
        {
            if (gradient == null)
                return;

            Vector2 local = ToDotLayerSpace(elementLocal);
            bool vertical = IsVertical(modifiers);

            // Pressing or releasing Shift mid-drag swaps which axis is moving. Re-anchoring on the swap
            // is what keeps the key under the pointer rather than jumping to wherever the stale anchors
            // would have put it.
            if (vertical != dragVertical)
                AnchorDrag(index, isAlpha, local, vertical);

            if (!TryKeyPosition(index, isAlpha, out Vector3 position))
                return;
            if (!TryDraggedPosition(local, position, out Vector3 moved))
                return;

            WriteKeyPosition(index, isAlpha, moved);
            Refresh();
            RaiseChanged();
        }

        /// <summary>
        /// Where the key should go for a pointer at <paramref name="local"/>, or false when this rotation
        /// cannot answer — see <see cref="CubeGeometry.TryUnprojectOntoPlaneY"/> and
        /// <see cref="CubeGeometry.MinPixelsPerUnitY"/>. Holding the key still is the honest response
        /// there: the cube can be turned and the drag carried on.
        /// </summary>
        private bool TryDraggedPosition(Vector2 local, Vector3 current, out Vector3 moved)
        {
            moved = current;

            if (dragVertical)
            {
                if (dragPixelsPerUnitY < CubeGeometry.MinPixelsPerUnitY)
                    return false;

                // Pointer x is ignored outright: a drag up the Y axis should not slide the key sideways
                // because the hand wandered.
                moved.y = dragAnchorKeyY - (local.y - dragAnchorPointerY) / dragPixelsPerUnitY;
                return true;
            }

            if (!DotGeometry.TryUnprojectOntoPlaneY(local, dragPlaneY, out Vector3 hit))
                return false;

            // The offset was taken within this same plane, so it carries no height and the key stays on it.
            moved = hit + dragGrabOffset;
            return true;
        }

        private bool TryKeyPosition(int index, bool isAlpha, out Vector3 position)
        {
            position = default;
            if (gradient == null || index < 0)
                return false;

            if (isAlpha)
            {
                var alphaKeys = gradient.AlphaKeys;
                if (index >= alphaKeys.Length)
                    return false;
                position = alphaKeys[index].position;
                return true;
            }

            var colorKeys = gradient.ColorKeys;
            if (index >= colorKeys.Length)
                return false;
            position = colorKeys[index].position;
            return true;
        }

        /// <summary>
        /// Writes a dragged position back. Both key constructors run their position through
        /// <c>ClampToUnitCube</c>, so a drag cannot take a key outside the cube it is drawn in, and a
        /// position change keeps the key's index, so there is nothing to re-sort and no drag index to fix.
        /// </summary>
        private void WriteKeyPosition(int index, bool isAlpha, Vector3 position)
        {
            if (isAlpha)
                gradient.SetAlphaKey(index, new AlphaKey3D(gradient.AlphaKeys[index].alpha, position));
            else
                gradient.SetColorKey(index, new ColorKey3D(gradient.ColorKeys[index].color, position));
        }

        /// <summary>
        /// Announces a key move the way <see cref="GradientBarElement"/> announces one, so the picker
        /// window and the inspector field react to a cube drag exactly as they do to a bar drag.
        /// </summary>
        private void RaiseChanged()
        {
            using var changed = Gradient3DChangedEvent.GetPooled(gradient, Gradient3DChangedEvent.ChangeKind.Keys);
            changed.target = this;
            SendEvent(changed);
        }

        /// <summary>
        /// Queues at most one solid render per editor frame.
        /// </summary>
        /// <remarks>
        /// A rotate drag raises a change on every pointer move, and pointer moves arrive faster than
        /// frames. A solid render costs a ray cast and a pass over every key for every one of its pixels,
        /// so dropping the redundant ones is the difference between a drag that tracks the pointer and one
        /// that does not.
        /// </remarks>
        private void RequestSolidRender()
        {
            if (!solidView || gradient == null || panel == null)
                return;

            pendingSolidRender ??= schedule.Execute(RenderSolid);
            pendingSolidRender.ExecuteLater(0);
        }

        private void RenderSolid()
        {
            if (!solidView || gradient == null)
                return;

            var geometry = new CubeGeometry(contentRect, yaw, pitch);
            Rect viewport = geometry.Viewport;

            FitToViewport(checkerImage, viewport);
            FitToViewport(solidImage, viewport);

            solidTexture.Ensure(SolidRenderSize);
            solidTexture.Render(gradient, new CubeView(CubeGeometry.DirectionFrom(yaw, pitch), "solid"), includeModulation);
            solidImage.image = solidTexture.Texture;
            SolidRenderCount++;
        }

        private static void FitToViewport(VisualElement element, Rect viewport)
        {
            element.style.left = viewport.x;
            element.style.top = viewport.y;
            element.style.width = viewport.width;
            element.style.height = viewport.height;
        }

        private static string Format(Vector3 p) => $"({p.x:0.00}, {p.y:0.00}, {p.z:0.00})";
    }
}
