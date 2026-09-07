using UnityEngine.UIElements;

namespace ABCodeworld.Gradients.Editor
{
    /// <summary>All evaluation-time modulation controls: bypass, reverse, repeats, repeat mode, offset and HSBA.</summary>
    internal sealed class ModulationPanel : VisualElement, INotifyValueChanged<GradientModulation>
    {
        private GradientModulation currentValue = GradientModulation.Identity;

        private readonly Toggle bypassToggle;
        private readonly VisualElement rowsContainer;
        private readonly Toggle reverseToggle;
        private readonly ResettableSliderRow repeatsRow;
        private readonly EnumField repeatModeField;
        private readonly ResettableSliderRow offsetRow;
        private readonly ResettableSliderRow hueRow;
        private readonly ResettableSliderRow saturationRow;
        private readonly ResettableSliderRow brightnessRow;
        private readonly ResettableSliderRow alphaRow;

        public GradientModulation value
        {
            get => currentValue;
            set
            {
                if (currentValue.Equals(value))
                    return;

                using var evt = ChangeEvent<GradientModulation>.GetPooled(currentValue, value);
                evt.target = this;
                SetValueWithoutNotify(value);
                SendEvent(evt);
            }
        }

        public void SetValueWithoutNotify(GradientModulation newValue)
        {
            currentValue = newValue;
            bypassToggle.SetValueWithoutNotify(newValue.bypass);
            reverseToggle.SetValueWithoutNotify(newValue.reverse);
            repeatsRow.SetValueWithoutNotify(newValue.repeats);
            repeatModeField.SetValueWithoutNotify(newValue.repeatMode);
            offsetRow.SetValueWithoutNotify(newValue.offset);
            hueRow.SetValueWithoutNotify(newValue.hueShift);
            saturationRow.SetValueWithoutNotify(newValue.saturation);
            brightnessRow.SetValueWithoutNotify(newValue.brightness);
            alphaRow.SetValueWithoutNotify(newValue.alpha);
            rowsContainer.SetEnabled(!newValue.bypass);
        }

        public ModulationPanel()
        {
            AddToClassList("abcw-modulation-panel");

            bypassToggle = new Toggle("Bypass Modulation")
            {
                name = "bypass",
                tooltip = "Skip modulation entirely; evaluation takes the cheap base path without discarding the stored values.",
            };
            bypassToggle.RegisterValueChangedCallback(evt =>
            {
                var m = currentValue;
                m.bypass = evt.newValue;
                value = m;
            });
            Add(bypassToggle);

            rowsContainer = new VisualElement { name = "rows" };
            Add(rowsContainer);

            reverseToggle = new Toggle("Reverse Eval")
            {
                name = "reverse",
                tooltip = "Transient inversion after repeats & offset; use Flip Keys to permanently flip key times.",
            };
            reverseToggle.RegisterValueChangedCallback(evt => { var m = currentValue; m.reverse = evt.newValue; value = m; });
            rowsContainer.Add(reverseToggle);

            repeatsRow = new ResettableSliderRow("Repeats", 1e-5f, 128f, 1f) { name = "repeats", tooltip = "Domain repeats (>1 tiles the gradient). Default = 1." };
            repeatsRow.RegisterValueChangedCallback(evt => { var m = currentValue; m.repeats = evt.newValue; value = m; });
            rowsContainer.Add(repeatsRow);

            repeatModeField = new EnumField("Repeat Mode", RepeatMode.Clamp)
            {
                name = "repeatMode",
                tooltip = "Clamp, Wrap, Mirror domain mapping. Only effective when Repeats > 1.",
            };
            repeatModeField.RegisterValueChangedCallback(evt => { var m = currentValue; m.repeatMode = (RepeatMode)evt.newValue; value = m; });
            rowsContainer.Add(repeatModeField);

            offsetRow = new ResettableSliderRow("Eval Offset", 0f, 1f, 0f) { name = "offset", tooltip = "Pre-repeat domain shift (wraps except single Clamp). Default = 0." };
            offsetRow.RegisterValueChangedCallback(evt => { var m = currentValue; m.offset = evt.newValue; value = m; });
            rowsContainer.Add(offsetRow);

            hueRow = new ResettableSliderRow("Hue Shift", -1f, 1f, 0f) { name = "hueShift", tooltip = "Hue shift in turns (-1..1)." };
            hueRow.RegisterValueChangedCallback(evt => { var m = currentValue; m.hueShift = evt.newValue; value = m; });
            rowsContainer.Add(hueRow);

            saturationRow = new ResettableSliderRow("Saturation", -1f, 1f, 0f) { name = "saturation", tooltip = "-1 = greyscale, 0 = original, 1 = fully (over)saturated." };
            saturationRow.RegisterValueChangedCallback(evt => { var m = currentValue; m.saturation = evt.newValue; value = m; });
            rowsContainer.Add(saturationRow);

            brightnessRow = new ResettableSliderRow("Brightness", -1f, 1f, 0f) { name = "brightness", tooltip = "-1 = black, 0 = original, 1 = white (RGB lerp)." };
            brightnessRow.RegisterValueChangedCallback(evt => { var m = currentValue; m.brightness = evt.newValue; value = m; });
            rowsContainer.Add(brightnessRow);

            alphaRow = new ResettableSliderRow("Alpha", -1f, 1f, 0f) { name = "alpha", tooltip = "-1 = transparent, 0 = original, 1 = opaque." };
            alphaRow.RegisterValueChangedCallback(evt => { var m = currentValue; m.alpha = evt.newValue; value = m; });
            rowsContainer.Add(alphaRow);

            SetValueWithoutNotify(GradientModulation.Identity);
        }
    }
}
