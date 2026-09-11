using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.UIElements.TestFramework;
using ABCodeworld.Gradients.Editor;
using ABCodeworld.Gradients.Tests.Editor.Support;

namespace ABCodeworld.Gradients.Tests.Editor.UI
{
    [TestFixture]
    [Category("UI")]
    internal sealed class KeyListElementTests : UITestFixture
    {
        private KeyListElement colorList;
        private GradientABCW gradient;

        [SetUp]
        public void SetUpList()
        {
            panelSize = new Vector2(300, 400);
            gradient = TestGradients.Rainbow7();
            colorList = new KeyListElement(isAlpha: false);
            colorList.style.height = 300;
            rootVisualElement.Add(colorList);
            colorList.SetGradient(gradient);
            simulate.FrameUpdate();
        }

        [Test]
        public void RowCount_FollowsKeyCount()
        {
            var listView = colorList.Q<ListView>();
            Assert.That(listView.itemsSource.Count, Is.EqualTo(gradient.ColorKeys.Length));
        }

        [Test]
        public void Refresh_AfterAddingKey_GrowsList()
        {
            gradient.AddColorKey(Color.magenta, 0.5f);
            colorList.Refresh();

            var listView = colorList.Q<ListView>();
            Assert.That(listView.itemsSource.Count, Is.EqualTo(gradient.ColorKeys.Length));
        }

        [Test]
        public void DeleteButton_RemovesKeyAndFiresChanged()
        {
            bool changed = false;
            colorList.Changed += () => changed = true;
            simulate.FrameUpdate();

            int before = gradient.ColorKeys.Length;
            var deleteButton = colorList.Q<Button>(className: "abcw-key-list__delete");
            Assume.That(deleteButton, Is.Not.Null);

            simulate.Click(deleteButton);
            simulate.FrameUpdate();

            Assert.That(gradient.ColorKeys.Length, Is.EqualTo(before - 1));
            Assert.That(changed, Is.True);
        }

        [Test]
        public void DeleteButton_RefusesAtMinKeys()
        {
            gradient = TestGradients.TwoKey();
            colorList.SetGradient(gradient);
            simulate.FrameUpdate();

            int before = gradient.ColorKeys.Length;
            var deleteButton = colorList.Q<Button>(className: "abcw-key-list__delete");
            simulate.Click(deleteButton);
            simulate.FrameUpdate();

            Assert.That(gradient.ColorKeys.Length, Is.EqualTo(before));
        }

        /// <summary>
        /// The numeric half of the "t" field used to be laid out at zero width, so every key row showed
        /// its "t" label and nothing after it. The field still took typed input, which is what made it
        /// read as a rendering fault rather than a layout one.
        /// </summary>
        /// <remarks>
        /// Asserted on resolved geometry rather than on Unity's internal class names, so the test stays
        /// meaningful if the default editor stylesheet is reorganised: what matters is that there is
        /// room left over for the input after the label has taken its share.
        /// </remarks>
        [Test]
        public void TimeField_LeavesRoomForItsInput()
        {
            var timeField = colorList.Q<FloatField>("time");
            Assume.That(timeField, Is.Not.Null);
            simulate.FrameUpdate();

            float fieldWidth = timeField.resolvedStyle.width;
            float labelWidth = timeField.labelElement.resolvedStyle.width;

            Assert.That(labelWidth, Is.LessThan(20f), "the one-character 't' label is claiming the whole field");
            Assert.That(fieldWidth - labelWidth, Is.GreaterThan(40f), "the numeric input has no width to draw in");
        }

        [Test]
        public void EditingTime_ResortsAndKeepsCorrectData()
        {
            var timeField = colorList.Q<FloatField>("time");
            Assume.That(timeField, Is.Not.Null);

            timeField.value = 0.99f;
            simulate.FrameUpdate();

            var keys = gradient.ColorKeys;
            for (int i = 1; i < keys.Length; i++)
                Assert.That(keys[i].time, Is.GreaterThanOrEqualTo(keys[i - 1].time));
        }
    }
}
