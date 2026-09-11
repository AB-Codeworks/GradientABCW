using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace ABCodeworld.Gradients.Editor
{
    /// <summary>
    /// One small render of a 3D gradient as a solid cube, over a checkerboard backdrop, re-rendering only
    /// when its inputs actually change.
    /// </summary>
    /// <remarks>
    /// The 3D counterpart of <see cref="GradientPreviewElement"/>, and it copies that element's three
    /// hard-won behaviours deliberately: the <c>ReferenceEquals</c> early-out in <see cref="Gradient"/>
    /// (re-assigning the same instance is how callers request a refresh, so treating it as an
    /// invalidation would defeat the version check entirely), the ancestor-walking
    /// <see cref="IsVisible"/> (so a collapsed foldout pays nothing), and the <see cref="BakeCount"/>
    /// instrumentation (so a test can tell a skipped render from a redundant one that produced the same
    /// pixels). All three matter more here: a cube render costs a ray cast and a full pass over every key
    /// per pixel, where the 1D strip costs a binary search.
    /// <para>
    /// The checkerboard is composited into the render rather than stacked behind it as its own
    /// <see cref="Image"/>, which is what the 1D preview does. A strip's backdrop has to cover the whole
    /// element because the gradient does; a cube's does not, and these renders are fixed-angle, so a
    /// backdrop outside the silhouette is a square of noise around a hexagon. Compositing is what lets it
    /// be trimmed to the cube — you cannot clip a texture to a shape by putting it behind one.
    /// </para>
    /// </remarks>
    internal sealed class CubePreviewElement : VisualElement
    {
        /// <summary>Pixel size of one render. Small on purpose: four of these sit in one inspector row.</summary>
        internal const int DefaultRenderSize = 32;

        private readonly Image cubeImage;
        private readonly CubePreviewTexture previewTexture = new();

        private GradientABCW3D gradient;
        private CubeView view;
        private bool includeModulation;
        private int renderSize = DefaultRenderSize;
        private bool clickable;

        private int lastVersion = -1;
        private int lastRenderSize = -1;
        private bool lastIncludeModulation;

        public event Action Clicked;

        /// <summary>
        /// How many times this element has actually re-rendered. Exposed for tests, which otherwise have
        /// no way to tell a skipped render from a redundant one that produced the same pixels.
        /// </summary>
        internal int BakeCount { get; private set; }

        /// <summary>
        /// The gradient shown. Assigning the instance already held is a refresh, not an invalidation:
        /// callers refresh by re-assigning the same gradient, so clearing the version stamp here
        /// unconditionally would mean the check in <see cref="Refresh"/> could never early-out.
        /// </summary>
        public GradientABCW3D Gradient
        {
            get => gradient;
            set
            {
                if (!ReferenceEquals(gradient, value))
                {
                    gradient = value;
                    lastVersion = -1;
                }
                Refresh();
            }
        }

        /// <summary>Which direction the cube is seen from, and the tooltip naming the faces it shows.</summary>
        public CubeView View
        {
            get => view;
            set
            {
                view = value;
                tooltip = $"Faces {value.Label}";
                lastVersion = -1;
                Refresh();
            }
        }

        /// <summary>True to show the modulated result, false to show the base gradient.</summary>
        public bool IncludeModulation
        {
            get => includeModulation;
            set
            {
                if (includeModulation == value)
                    return;
                includeModulation = value;
                Refresh();
            }
        }

        /// <summary>Pixel size the cube is rendered at. Also sets the element's own size.</summary>
        public int RenderSize
        {
            get => renderSize;
            set
            {
                int clamped = Mathf.Max(4, value);
                if (renderSize == clamped)
                    return;

                renderSize = clamped;
                style.width = clamped;
                style.height = clamped;
                Refresh();
            }
        }

        public bool Clickable
        {
            get => clickable;
            set
            {
                clickable = value;

                // UI Toolkit cursor styling is USS-driven; toggle the class rather than reaching for IMGUI.
                EnableInClassList("abcw-gradient-preview--clickable", clickable);
            }
        }

        public CubePreviewElement()
        {
            AddToClassList("abcw-gradient-preview");
            AddToClassList("abcw-cube-preview");
            style.position = Position.Relative;
            style.width = renderSize;
            style.height = renderSize;
            style.flexShrink = 0;

            cubeImage = new Image { pickingMode = PickingMode.Ignore, scaleMode = ScaleMode.StretchToFill };
            StretchToParent(cubeImage);
            Add(cubeImage);

            RegisterCallback<GeometryChangedEvent>(_ => Refresh());
            RegisterCallback<DetachFromPanelEvent>(_ => previewTexture.Dispose());
            RegisterCallback<PointerDownEvent>(OnPointerDown);
        }

        private static void StretchToParent(VisualElement e)
        {
            e.style.position = Position.Absolute;
            e.style.left = 0; e.style.right = 0; e.style.top = 0; e.style.bottom = 0;
        }

        public void Refresh()
        {
            if (gradient == null || !IsVisible())
                return;

            bool dirty = lastVersion != gradient.Version ||
                         lastRenderSize != renderSize ||
                         lastIncludeModulation != includeModulation;
            if (!dirty)
                return;

            previewTexture.Ensure(renderSize);
            previewTexture.Render(gradient, in view, includeModulation, CubeBackdrop.TrimmedChecker);
            cubeImage.image = previewTexture.Texture;

            lastVersion = gradient.Version;
            lastRenderSize = renderSize;
            lastIncludeModulation = includeModulation;
            BakeCount++;
        }

        /// <summary>
        /// True when this element is displayed and actually occupies space. Walks the ancestor chain
        /// because a <c>Foldout</c> hides its content by setting <c>display: none</c> on a parent, which
        /// leaves this element's own display style untouched.
        /// </summary>
        private bool IsVisible()
        {
            for (VisualElement e = this; e != null; e = e.hierarchy.parent)
            {
                if (e.resolvedStyle.display == DisplayStyle.None)
                    return false;
            }

            float resolved = resolvedStyle.width;
            return !float.IsNaN(resolved) && resolved >= 2f;
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            if (clickable && evt.button == 0)
            {
                Clicked?.Invoke();
                evt.StopPropagation();
            }
        }
    }
}
