using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ABCodeworld.Gradients.Editor
{
    /// <summary>
    /// The 3D picker's main viewport: a drag-rotatable wireframe cube showing every key as a coloured dot
    /// inside it, or the same cube rendered solid at the same rotation.
    /// </summary>
    /// <remarks>
    /// This is what stands in for the 1D picker's gradient bar, and it is deliberately not an equivalent
    /// of it. The bar is where keys are authored: you drag them along it, and it doubles as the live
    /// preview. A cube can be neither — a pointer gives two coordinates and a key needs three, so dragging
    /// a dot could only ever move it within one arbitrary plane. So the cube shows and the key lists edit,
    /// and this element raises no change events at all.
    /// <para>
    /// What it does still give you is a preview, because the dots carry their own keys' colours: rotating
    /// the cube reads as a point cloud of the gradient. The solid toggle covers the rest, rendering the
    /// same cube through <see cref="CubePreviewRasterizer"/> at the same rotation and the same projection,
    /// so the two views line up exactly rather than jumping.
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

        /// <summary>How many times the dot elements have been torn down and rebuilt. Exposed for tests.</summary>
        internal int RebuildCount { get; private set; }

        /// <summary>How many times the solid view has been re-rendered. Exposed for tests.</summary>
        internal int SolidRenderCount { get; private set; }

        public float Yaw => yaw;
        public float Pitch => pitch;

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

        /// <summary>Which kind of key the picker is editing. The other kind's dots are drawn muted.</summary>
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

            RegisterCallback<GeometryChangedEvent>(_ => OnRotationChanged());
            RegisterCallback<DetachFromPanelEvent>(_ => solidTexture.Dispose());

            ApplyViewMode();
        }

        /// <summary>Returns the cube to the rotation it opens at.</summary>
        public void ResetRotation()
        {
            yaw = DefaultYaw;
            pitch = DefaultPitch;
            OnRotationChanged();
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
                dot.style.backgroundColor = colorKeys[i].color;
                dot.tooltip = $"Colour key {i} — {Format(colorKeys[i].position)}";
            }

            for (int i = 0; i < alphaKeys.Length; i++)
            {
                var dot = dots[colorKeys.Length + i];
                float a = alphaKeys[i].alpha;
                dot.EnableInClassList("abcw-cube-key--alpha", true);
                dot.EnableInClassList("abcw-cube-key--muted", !alphaMode);

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

            var geometry = new CubeGeometry(dotLayer.contentRect, yaw, pitch);
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
