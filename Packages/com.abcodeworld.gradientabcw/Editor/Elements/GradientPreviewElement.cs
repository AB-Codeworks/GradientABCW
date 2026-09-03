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

        public GradientABCW Gradient
        {
            get => gradient;
            set
            {
                gradient = value;
                lastVersion = -1;
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

            checkerImage = new Image { pickingMode = PickingMode.Ignore, scaleMode = ScaleMode.StretchToFill, image = EditorTextures.Checker };
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

            int width = Mathf.Max(2, Mathf.RoundToInt(resolvedStyle.width));
            if (width <= 2)
                width = 256; // not yet laid out; bake at a sane default and refresh again once GeometryChangedEvent fires

            bool dirty = lastVersion != gradient.Version || lastWidth != width || lastMode != mode;
            if (!dirty)
                return;

            previewTexture.Ensure(width);
            var options = GradientLutOptions.Project(final: mode == PreviewMode.Final);
            previewTexture.Update(gradient, options);
            gradientImage.image = previewTexture.Texture;

            lastVersion = gradient.Version;
            lastWidth = width;
            lastMode = mode;
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
