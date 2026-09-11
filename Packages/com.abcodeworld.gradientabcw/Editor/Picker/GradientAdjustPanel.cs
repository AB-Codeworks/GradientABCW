using System;
using UnityEngine.UIElements;

namespace ABCodeworld.Gradients.Editor
{
    /// <summary>
    /// The Adjust tab: blend mode, the three whole-gradient rearrangements, and a readout of what the
    /// gradient actually evaluates to.
    /// </summary>
    /// <remarks>
    /// What used to be <c>GradientActionsPanel</c>, less its sampling half. The two are separated
    /// because they are not the same kind of thing: everything here edits the gradient in place and
    /// needs a reference to it, while rebuilding discards it and hands back another — which is why
    /// that half could move to the type-free <see cref="GradientRebuildPanel"/> and be shared with
    /// the 3D picker, and why this half stays a sibling of <see cref="GradientAdjust3DPanel"/>.
    /// <para>
    /// The Result strip is new. The bar previews the <em>base</em> gradient and always has, while the
    /// library tiles preview the final one, and until now nothing in the window said so or let you
    /// see the difference.
    /// </para>
    /// </remarks>
    internal sealed class GradientAdjustPanel : VisualElement
    {
        private readonly Toggle steppedToggle;
        private readonly GradientPreviewElement resultPreview;

        private GradientABCW gradient;

        /// <summary>Raised after an in-place mutation (blend mode, flip, distribute).</summary>
        public event Action Changed;

        public GradientAdjustPanel()
        {
            AddToClassList("abcw-tab-pane");
            style.flexDirection = FlexDirection.Column;

            steppedToggle = new Toggle("Stepped Blend")
            {
                name = "stepped",
                tooltip = "Stepped (banded) interpolation using midpoint partition.",
            };
            steppedToggle.RegisterValueChangedCallback(evt =>
            {
                if (gradient == null) return;
                gradient.BlendMode = evt.newValue ? BlendMode.Stepped : BlendMode.Smooth;
                Changed?.Invoke();
            });
            Add(steppedToggle);

            Add(Section("Rearrange every key"));

            var buttons = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            buttons.Add(Grow(new Button(() => Mutate(g => g.FlipKeys()))
            {
                name = "flipKeys",
                text = "Flip Keys",
                tooltip = "Permanently flip (1 - t) key times.",
            }));
            buttons.Add(Grow(new Button(() => Mutate(g => g.DistributeColorKeysEvenly()))
            {
                name = "distributeColors",
                text = "Distribute Colours",
                tooltip = "Space every colour key evenly across the bar.",
            }));
            buttons.Add(Grow(new Button(() => Mutate(g => g.DistributeAlphaKeysEvenly()))
            {
                name = "distributeAlphas",
                text = "Distribute Alphas",
                tooltip = "Space every alpha key evenly across the bar.",
            }));
            Add(buttons);

            Add(Section("Result"));
            resultPreview = new GradientPreviewElement
            {
                name = "resultPreview",
                Mode = GradientPreviewElement.PreviewMode.Final,
                pickingMode = PickingMode.Ignore,
            };
            resultPreview.style.height = 18;
            Add(resultPreview);

            var note = new Label("The bar shows the base gradient. This is what it evaluates to, modulation included.");
            note.AddToClassList("abcw-tab-pane__note");
            Add(note);
        }

        public void SetGradient(GradientABCW value)
        {
            gradient = value;
            if (gradient == null)
                return;

            steppedToggle.SetValueWithoutNotify(gradient.BlendMode == BlendMode.Stepped);
            resultPreview.Gradient = gradient;
        }

        /// <summary>Re-reads the gradient without re-pointing at a different one.</summary>
        public void Refresh() => resultPreview.Gradient = gradient;

        private static Button Grow(Button button)
        {
            button.style.flexGrow = 1;
            button.style.flexBasis = 0;
            return button;
        }

        private static Label Section(string text)
        {
            var label = new Label(text);
            label.AddToClassList("abcw-tab-pane__section");
            return label;
        }

        private void Mutate(Action<GradientABCW> mutation)
        {
            if (gradient == null)
                return;
            mutation(gradient);
            Changed?.Invoke();
        }
    }
}
