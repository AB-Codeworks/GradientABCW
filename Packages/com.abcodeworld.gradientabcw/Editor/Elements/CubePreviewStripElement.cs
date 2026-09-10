using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace ABCodeworld.Gradients.Editor
{
    /// <summary>
    /// The row of small cube renders that stands in for a 3D gradient's swatch: one
    /// <see cref="CubePreviewElement"/> per view in <see cref="CubePreviewRasterizer.DefaultViews"/>, so
    /// every face of the cube is visible in at least one of them.
    /// </summary>
    /// <remarks>
    /// A 1D gradient's swatch can show the whole gradient at once, because a line fits in a strip. A cube
    /// cannot: three faces is all any single view can show. Hence a row rather than a single image.
    /// </remarks>
    internal sealed class CubePreviewStripElement : VisualElement
    {
        private readonly List<CubePreviewElement> views = new();
        private GradientABCW3D gradient;
        private bool clickable;

        public event Action Clicked;

        /// <summary>Total renders across every view. The 3D reading of <see cref="CubePreviewElement.BakeCount"/>.</summary>
        internal int BakeCount
        {
            get
            {
                int total = 0;
                foreach (var view in views)
                    total += view.BakeCount;
                return total;
            }
        }

        /// <summary>The individual renders, in the order of <see cref="CubePreviewRasterizer.DefaultViews"/>.</summary>
        internal IReadOnlyList<CubePreviewElement> Views => views;

        public GradientABCW3D Gradient
        {
            get => gradient;
            set
            {
                gradient = value;
                foreach (var view in views)
                    view.Gradient = value;
            }
        }

        public bool IncludeModulation
        {
            get => views.Count > 0 && views[0].IncludeModulation;
            set
            {
                foreach (var view in views)
                    view.IncludeModulation = value;
            }
        }

        public bool Clickable
        {
            get => clickable;
            set
            {
                clickable = value;
                foreach (var view in views)
                    view.Clickable = value;
            }
        }

        public CubePreviewStripElement(int renderSize = CubePreviewElement.DefaultRenderSize)
        {
            AddToClassList("abcw-cube-strip");
            style.flexDirection = FlexDirection.Row;

            var definitions = CubePreviewRasterizer.DefaultViews;
            for (int i = 0; i < definitions.Length; i++)
            {
                var element = new CubePreviewElement
                {
                    name = $"cubeView{i}",
                    View = definitions[i],
                    RenderSize = renderSize,
                };
                element.Clicked += () => Clicked?.Invoke();
                views.Add(element);
                Add(element);
            }
        }

        public void Refresh()
        {
            foreach (var view in views)
                view.Refresh();
        }
    }
}
