using UnityEngine;
using UnityEngine.UIElements;

namespace ABCodeworld.Gradients.Editor
{
    /// <summary>A label + slider + delayed numeric field + reset button, replacing six duplicated IMGUI rows.</summary>
    internal sealed class ResettableSliderRow : VisualElement, INotifyValueChanged<float>
    {
        private float currentValue;
        private readonly Slider slider;
        private readonly FloatField field;
        private readonly Button resetButton;

        public float DefaultValue { get; }
        public float LowValue { get; }
        public float HighValue { get; }

        public float value
        {
            get => currentValue;
            set
            {
                float clamped = Mathf.Clamp(value, LowValue, HighValue);
                if (Mathf.Approximately(currentValue, clamped))
                    return;

                using var evt = ChangeEvent<float>.GetPooled(currentValue, clamped);
                evt.target = this;
                SetValueWithoutNotify(clamped);
                SendEvent(evt);
            }
        }

        public void SetValueWithoutNotify(float newValue)
        {
            currentValue = Mathf.Clamp(newValue, LowValue, HighValue);
            slider.SetValueWithoutNotify(currentValue);
            field.SetValueWithoutNotify(currentValue);
            resetButton.SetEnabled(!Mathf.Approximately(currentValue, DefaultValue));
        }

        public ResettableSliderRow(string labelText, float lowValue, float highValue, float defaultValue)
        {
            LowValue = lowValue;
            HighValue = highValue;
            DefaultValue = defaultValue;

            AddToClassList("abcw-slider-row");
            style.flexDirection = FlexDirection.Row;
            style.alignItems = Align.Center;
            style.marginBottom = 2;

            var label = new Label(labelText);
            label.AddToClassList("abcw-slider-row__label");
            Add(label);

            slider = new Slider(lowValue, highValue) { showInputField = false };
            slider.AddToClassList("abcw-slider-row__slider");
            slider.style.flexGrow = 1;
            slider.style.marginRight = 4;
            slider.RegisterValueChangedCallback(evt => value = evt.newValue);
            Add(slider);

            field = new FloatField { isDelayed = true };
            field.AddToClassList("abcw-slider-row__field");
            field.style.width = 60;
            field.style.marginRight = 4;
            field.RegisterValueChangedCallback(evt => value = evt.newValue);
            Add(field);

            resetButton = new Button(() => value = DefaultValue) { tooltip = "Reset to default" };
            resetButton.AddToClassList("abcw-slider-row__reset");
            resetButton.style.width = 18;
            resetButton.style.height = 18;
            var icon = EditorIcons.Reset;
            if (icon != null)
                resetButton.Add(new Image { image = icon, pickingMode = PickingMode.Ignore, scaleMode = ScaleMode.ScaleToFit });
            Add(resetButton);

            SetValueWithoutNotify(defaultValue);
        }
    }
}
