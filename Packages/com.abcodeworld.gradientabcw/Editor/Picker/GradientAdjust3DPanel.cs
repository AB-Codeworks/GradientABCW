using System;
using UnityEngine.UIElements;

namespace ABCodeworld.Gradients.Editor
{
    /// <summary>
    /// The 3D picker's Adjust tab: blend mode, falloff, and the three whole-gradient rearrangements.
    /// </summary>
    /// <remarks>
    /// A sibling of <see cref="GradientAdjustPanel"/>, not a generalisation of it. Everything here
    /// names a concrete <see cref="GradientABCW3D"/> and mutates it in place, and one control —
    /// falloff — has no 1D meaning at all. The part the two genuinely shared, rebuilding from a
    /// texture or a mesh, moved out to <see cref="GradientRebuildPanel"/> instead, which could be
    /// shared precisely because it never touches a gradient.
    /// <para>
    /// There is no Result strip here, unlike the flat panel: the cube viewport already carries a row
    /// of fixed-angle renders beside it that show the modulated result from four directions.
    /// </para>
    /// </remarks>
    internal sealed class GradientAdjust3DPanel : VisualElement
    {
        private readonly Toggle steppedToggle;
        private readonly ResettableSliderRow falloffRow;

        private GradientABCW3D gradient;

        /// <summary>Raised after an in-place mutation (blend mode, falloff, flip, distribute).</summary>
        public event Action Changed;

        public GradientAdjust3DPanel()
        {
            AddToClassList("abcw-tab-pane");
            style.flexDirection = FlexDirection.Column;

            steppedToggle = new Toggle("Stepped Blend")
            {
                name = "stepped",
                tooltip = "Each point takes the colour of its nearest key outright, partitioning the cube into cells.",
            };
            steppedToggle.RegisterValueChangedCallback(evt =>
            {
                if (gradient == null) return;
                gradient.BlendMode = evt.newValue ? BlendMode.Stepped : BlendMode.Smooth;
                SyncFalloffEnabled();
                Changed?.Invoke();
            });
            Add(steppedToggle);

            falloffRow = new ResettableSliderRow(
                "Falloff Power",
                GradientABCW3D.MinFalloffPower,
                GradientABCW3D.MaxFalloffPower,
                GradientABCW3D.DefaultFalloffPower)
            {
                name = "falloffPower",
                tooltip = "How sharply a key's influence falls off with distance. Higher pulls each point "
                        + "towards its nearest key. No effect under stepped blending.",
            };
            falloffRow.RegisterValueChangedCallback(evt =>
            {
                if (gradient == null) return;
                gradient.FalloffPower = evt.newValue;
                Changed?.Invoke();
            });
            Add(falloffRow);

            Add(Section("Rearrange every key"));

            var buttons = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            buttons.Add(Grow(new Button(() => Mutate(g => g.FlipKeys()))
            {
                name = "flipKeys",
                text = "Flip Keys",
                tooltip = "Permanently mirror every key through the centre of the cube, on all three axes.",
            }));
            buttons.Add(Grow(new Button(() => Mutate(g => g.DistributeColorKeysEvenly()))
            {
                name = "distributeColors",
                text = "Distribute Colours",
                tooltip = "Spread every colour key evenly through the cube.",
            }));
            buttons.Add(Grow(new Button(() => Mutate(g => g.DistributeAlphaKeysEvenly()))
            {
                name = "distributeAlphas",
                text = "Distribute Alphas",
                tooltip = "Spread every alpha key evenly through the cube.",
            }));
            Add(buttons);
        }

        public void SetGradient(GradientABCW3D value)
        {
            gradient = value;
            if (gradient == null)
                return;

            steppedToggle.SetValueWithoutNotify(gradient.BlendMode == BlendMode.Stepped);
            falloffRow.SetValueWithoutNotify(gradient.FalloffPower);
            SyncFalloffEnabled();
        }

        /// <summary>
        /// Falloff has no meaning under stepped blending, where the nearest key wins outright. Leaving it
        /// live would present a control that silently does nothing.
        /// </summary>
        private void SyncFalloffEnabled() =>
            falloffRow.SetEnabled(gradient != null && gradient.BlendMode == BlendMode.Smooth);

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

        private void Mutate(Action<GradientABCW3D> mutation)
        {
            if (gradient == null)
                return;
            mutation(gradient);
            Changed?.Invoke();
        }
    }
}
