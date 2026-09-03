using NUnit.Framework;
using UnityEngine.UIElements;
using UnityEngine.UIElements.TestFramework;
using ABCodeworld.Gradients.Editor;

namespace ABCodeworld.Gradients.Tests.Editor.UI
{
    [TestFixture]
    [Category("UI")]
    internal sealed class ModulationPanelTests : UITestFixture
    {
        private ModulationPanel modulationPanel;

        [SetUp]
        public void SetUpPanel()
        {
            modulationPanel = new ModulationPanel();
            rootVisualElement.Add(modulationPanel);
            simulate.FrameUpdate();
        }

        [Test]
        public void Bypass_DisablesRows()
        {
            var rows = modulationPanel.Q<VisualElement>("rows");
            Assert.That(rows.enabledSelf, Is.True);

            var m = modulationPanel.value;
            m.bypass = true;
            modulationPanel.value = m;

            Assert.That(rows.enabledSelf, Is.False);
        }

        [Test]
        public void ReverseToggle_UpdatesReverseField()
        {
            GradientModulation? changed = null;
            modulationPanel.RegisterValueChangedCallback(evt => changed = evt.newValue);

            var toggle = modulationPanel.Q<Toggle>("reverse");
            simulate.Click(toggle);
            simulate.FrameUpdate();

            Assert.That(changed.HasValue, Is.True);
            Assert.That(changed.Value.reverse, Is.True);
        }

        [Test]
        public void RepeatsRow_UpdatesRepeatsField()
        {
            var row = modulationPanel.Q<ResettableSliderRow>("repeats");
            row.value = 3.5f;

            Assert.That(modulationPanel.value.repeats, Is.EqualTo(3.5f).Within(1e-5f));
        }

        [Test]
        public void OffsetRow_UpdatesOffsetField()
        {
            var row = modulationPanel.Q<ResettableSliderRow>("offset");
            row.value = 0.4f;

            Assert.That(modulationPanel.value.offset, Is.EqualTo(0.4f).Within(1e-5f));
        }

        [Test]
        public void HueRow_UpdatesHueShiftField()
        {
            var row = modulationPanel.Q<ResettableSliderRow>("hueShift");
            row.value = -0.3f;

            Assert.That(modulationPanel.value.hueShift, Is.EqualTo(-0.3f).Within(1e-5f));
        }

        [Test]
        public void SaturationRow_UpdatesSaturationField()
        {
            var row = modulationPanel.Q<ResettableSliderRow>("saturation");
            row.value = 0.6f;

            Assert.That(modulationPanel.value.saturation, Is.EqualTo(0.6f).Within(1e-5f));
        }

        [Test]
        public void BrightnessRow_UpdatesBrightnessField()
        {
            var row = modulationPanel.Q<ResettableSliderRow>("brightness");
            row.value = -0.6f;

            Assert.That(modulationPanel.value.brightness, Is.EqualTo(-0.6f).Within(1e-5f));
        }

        [Test]
        public void AlphaRow_UpdatesAlphaField()
        {
            var row = modulationPanel.Q<ResettableSliderRow>("alpha");
            row.value = 0.2f;

            Assert.That(modulationPanel.value.alpha, Is.EqualTo(0.2f).Within(1e-5f));
        }

        [Test]
        public void RepeatModeField_UpdatesRepeatModeField()
        {
            var field = modulationPanel.Q<EnumField>("repeatMode");
            field.value = RepeatMode.Mirror;

            Assert.That(modulationPanel.value.repeatMode, Is.EqualTo(RepeatMode.Mirror));
        }

        [Test]
        public void SetValueWithoutNotify_SyncsAllRows()
        {
            var m = new GradientModulation
            {
                reverse = true,
                repeats = 2f,
                repeatMode = RepeatMode.Wrap,
                offset = 0.3f,
                hueShift = 0.1f,
                saturation = -0.2f,
                brightness = 0.3f,
                alpha = -0.4f,
            };
            modulationPanel.SetValueWithoutNotify(m);

            Assert.That(modulationPanel.Q<Toggle>("reverse").value, Is.True);
            Assert.That(modulationPanel.Q<ResettableSliderRow>("repeats").value, Is.EqualTo(2f).Within(1e-5f));
            Assert.That(modulationPanel.Q<EnumField>("repeatMode").value, Is.EqualTo(RepeatMode.Wrap));
            Assert.That(modulationPanel.Q<ResettableSliderRow>("offset").value, Is.EqualTo(0.3f).Within(1e-5f));
        }
    }
}
