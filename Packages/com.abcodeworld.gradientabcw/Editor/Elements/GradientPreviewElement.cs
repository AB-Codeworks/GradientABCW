using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace ABCodeworld.Gradients.Editor
{
    /// <summary>A gradient strip over a checkerboard backdrop, re-baking only when its inputs actually change.</summary>
    internal sealed class GradientPreviewElement : VisualElement
    {
        public enum PreviewMode { Base, Final }

        private readonly Image checkerImage;
        private readonly Image gradientImage;
        private readonly GradientPreviewTexture previewTexture = new();

        private GradientABCW gradient;
        private PreviewMode mode = PreviewMode.Final;
        private bool clickable;

        private int lastVersion = -1;
        private int lastWidth = -1;
        private PreviewMode lastMode;

        public event Action Clicked;

        /// <summary>
        /// How many times this element has actually re-baked its texture. Exposed for tests, which
        /// otherwise have no way to tell a skipped bake from a redundant one that produced the same pixels.
        /// </summary>
        internal int BakeCount { get; private set; }

        /// <remarks>
        /// Assigning the instance the element already holds is not an invalidation. Callers refresh by
        /// re-assigning the same gradient (the field and the picker both do so on every edit), and
        /// unconditionally clearing <c>lastVersion</c> here meant the version check below could never
        /// early-out — every refresh re-baked the texture whether or not anything had changed.
        /// </remarks>
        public GradientABCW Gradient
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

        public PreviewMode Mode
        {
            get => mode;
            set
            {
                if (mode == value)
                    return;
                mode = value;
                lastVersion = -1;
                Refresh();
            }
        }

        public bool Clickable
        {
            get => clickable;
            set
            {
                clickable = value;
                EditorGuiCursor();
            }
        }

        public GradientPreviewElement()
        {
            AddToClassList("abcw-gradient-preview");
            style.minHeight = 20;
            style.position = Position.Relative;

            checkerImage = new Image { pickingMode = PickingMode.Ignore, scaleMode = ScaleMode.StretchToFill, image = CheckerTexture.Image };
            StretchToParent(checkerImage);
            Add(checkerImage);

            gradientImage = new Image { pickingMode = PickingMode.Ignore, scaleMode = ScaleMode.StretchToFill };
            StretchToParent(gradientImage);
            Add(gradientImage);

            RegisterCallback<GeometryChangedEvent>(_ => Refresh());
            RegisterCallback<DetachFromPanelEvent>(_ => previewTexture.Dispose());
            RegisterCallback<PointerDownEvent>(OnPointerDown);
        }

        private static void StretchToParent(VisualElement e)
        {
            e.style.position = Position.Absolute;
            e.style.left = 0; e.style.right = 0; e.style.top = 0; e.style.bottom = 0;
        }

        private void EditorGuiCursor()
        {
            // UI Toolkit cursor styling is USS-driven (see abcw-gradient-preview--clickable); toggle the class instead of IMGUI cursor rects.
            EnableInClassList("abcw-gradient-preview--clickable", clickable);
        }

        public void Refresh()
        {
            if (gradient == null)
                return;

            // A preview that is not laid out yet, or is genuinely hidden — most often inside the collapsed
            // modulation foldout — is not worth baking. This previously fell back to a 256px default, so a
            // collapsed foldout still paid for a full final preview on every edit. GeometryChangedEvent
            // fires when it becomes visible and brings us straight back here.
            if (!IsVisible())
                return;

            // Fixed resolution, stretched to fit, rather than matching the element's pixel width. Matching
            // meant destroying and recreating the texture on every inspector resize; at a 1-pixel-tall
            // strip the extra resolution is far cheaper than that churn.
            int width = GradientABCWSettings.instance.PreviewResolution;

            bool dirty = lastVersion != gradient.Version || lastWidth != width || lastMode != mode;
            if (!dirty)
                return;

            previewTexture.Ensure(width);

            // Deliberately not GradientLutOptions.Project: previews are stored in an sRGB texture and read
            // back through it, so converting to linear here would be undone on sample. Writing sRGB bytes
            // also avoids the banding that 8-bit linear storage causes in dark gradient regions, and skips
            // three pow() calls per pixel.
            var options = mode == PreviewMode.Final ? GradientLutOptions.Final : GradientLutOptions.Base;
            previewTexture.Update(gradient, options);
            gradientImage.image = previewTexture.Texture;

            lastVersion = gradient.Version;
            lastWidth = width;
            lastMode = mode;
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
