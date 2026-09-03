using NUnit.Framework;
using UnityEngine.UIElements;
using UnityEngine.UIElements.TestFramework;
using ABCodeworld.Gradients.Editor;

namespace ABCodeworld.Gradients.Tests.Editor.UI
{
    [TestFixture]
    [Category("UI")]
    internal sealed class ResettableSliderRowTests : UITestFixture
    {
        private ResettableSliderRow row;

        [SetUp]
        public void SetUpRow()
        {
            row = new ResettableSliderRow("Test", -1f, 1f, 0f);
            rootVisualElement.Add(row);
            simulate.FrameUpdate();
        }

        [Test]
        public void SettingFieldValue_CommitsToRow()
        {
            var field = row.Q<FloatField>();
            field.value = 0.5f;
            Assert.That(row.value, Is.EqualTo(0.5f).Within(1e-5f));
        }

        [Test]
        public void SetValueWithoutNotify_DoesNotRaiseChangeEvent()
        {
            bool raised = false;
            row.RegisterValueChangedCallback(_ => raised = true);
            row.SetValueWithoutNotify(0.7f);

            Assert.That(raised, Is.False);
            Assert.That(row.value, Is.EqualTo(0.7f).Within(1e-5f));
        }

        [Test]
        public void SettingValue_RaisesChangeEvent()
        {
            float? newValue = null;
            row.RegisterValueChangedCallback(evt => newValue = evt.newValue);
            row.value = 0.42f;

            Assert.That(newValue, Is.EqualTo(0.42f).Within(1e-5f));
        }

        [Test]
        public void ResetButton_RestoresDefaultAndDisablesItself()
        {
            row.value = 0.5f;
            var resetButton = row.Q<Button>(className: "abcw-slider-row__reset");
            Assert.That(resetButton.enabledSelf, Is.True);

            simulate.Click(resetButton);
            simulate.FrameUpdate();

            Assert.That(row.value, Is.EqualTo(0f).Within(1e-5f));
            Assert.That(resetButton.enabledSelf, Is.False);
        }

        [Test]
        public void ResetButton_AtDefault_StartsDisabled()
        {
            var resetButton = row.Q<Button>(className: "abcw-slider-row__reset");
            Assert.That(resetButton.enabledSelf, Is.False);
        }

        [Test]
        public void Value_ClampsToRange()
        {
            row.value = 5f;
            Assert.That(row.value, Is.EqualTo(1f));

            row.value = -5f;
            Assert.That(row.value, Is.EqualTo(-1f));
        }
    }
}
