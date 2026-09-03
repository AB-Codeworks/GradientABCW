using NUnit.Framework;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.UIElements.TestFramework;
using ABCodeworld.Gradients.Editor;
using ABCodeworld.Gradients.Tests.Editor.Support;

namespace ABCodeworld.Gradients.Tests.Editor.UI
{
    [TestFixture]
    [Category("UI")]
    internal sealed class GradientABCWFieldTests : UITestFixture
    {
        private GradientABCWField field;

        [SetUp]
        public void SetUpField()
        {
            panelSize = new Vector2(400, 300);
            field = new GradientABCWField("Test Gradient");
            field.style.width = 380;
            rootVisualElement.Add(field);
            simulate.FrameUpdate();
        }

        private static void ExecuteMenuAction(ToolbarMenu menu, string actionName)
        {
            foreach (var item in menu.menu.MenuItems())
            {
                if (item is DropdownMenuAction action && action.name == actionName)
                {
                    action.Execute();
                    return;
                }
            }
            Assert.Fail($"Menu action '{actionName}' not found.");
        }

        [Test]
        public void SettingValue_RefreshesBasePreview()
        {
            var g = TestGradients.Rainbow7();
            field.value = g;
            simulate.FrameUpdate();

            var preview = field.Q<GradientPreviewElement>();
            Assert.That(preview, Is.Not.Null);
            Assert.That(preview.Gradient, Is.SameAs(g));
        }

        [Test]
        public void FlipKeysAction_MutatesValueAndRaisesGradientChanged()
        {
            var g = TestGradients.Rainbow7();
            field.value = g;
            float firstTimeBefore = g.ColorKeys[0].time;

            bool changed = false;
            field.RegisterCallback<GradientChangedEvent>(_ => changed = true);

            var menu = field.Q<ToolbarMenu>();
            ExecuteMenuAction(menu, "Flip Keys");

            Assert.That(changed, Is.True);
            Assert.That(g.ColorKeys[^1].time, Is.EqualTo(1f - firstTimeBefore).Within(1e-5f));
        }

        [Test]
        public void DistributeColorKeysAction_SpacesKeysEvenly()
        {
            var g = TestGradients.Rainbow7();
            field.value = g;

            var menu = field.Q<ToolbarMenu>();
            ExecuteMenuAction(menu, "Distribute Colour Keys");

            var keys = g.ColorKeys;
            for (int i = 0; i < keys.Length; i++)
                Assert.That(keys[i].time, Is.EqualTo((float)i / (keys.Length - 1)).Within(1e-5f));
        }

        [Test]
        public void ResetToDefaultAction_RaisesValueChangedWithDefaultGradient()
        {
            field.value = TestGradients.Rainbow7();

            GradientABCW newValue = null;
            field.RegisterValueChangedCallback(evt => newValue = evt.newValue);

            var menu = field.Q<ToolbarMenu>();
            ExecuteMenuAction(menu, "Reset to Default");

            Assert.That(newValue, Is.Not.Null);
            Assert.That(newValue.ColorKeys.Length, Is.EqualTo(2));
        }

        [Test]
        public void BlendModeField_ChangingIt_UpdatesGradientAndRaisesChanged()
        {
            var g = TestGradients.Rainbow7();
            field.value = g;

            bool changed = false;
            field.RegisterCallback<GradientChangedEvent>(_ => changed = true);

            var blendField = field.Q<EnumField>();
            blendField.value = BlendMode.Stepped;
            simulate.FrameUpdate();

            Assert.That(g.BlendMode, Is.EqualTo(BlendMode.Stepped));
            Assert.That(changed, Is.True);
        }

        [Test]
        public void ModulationFoldout_HasPersistentViewDataKey()
        {
            var foldout = field.Q<Foldout>();
            Assert.That(foldout, Is.Not.Null);
            Assert.That(string.IsNullOrEmpty(foldout.viewDataKey), Is.False);
        }

        [Test]
        public void ModulationPanel_ChangingRow_UpdatesGradientModulation()
        {
            var g = TestGradients.Rainbow7();
            field.value = g;

            var repeatsRow = field.Q<ResettableSliderRow>("repeats");
            Assert.That(repeatsRow, Is.Not.Null);

            repeatsRow.value = 3f;
            simulate.FrameUpdate();

            Assert.That(g.Modulation.repeats, Is.EqualTo(3f).Within(1e-5f));
        }

        [Test]
        public void SetValueWithoutNotify_DoesNotRaiseValueChanged()
        {
            bool raised = false;
            field.RegisterValueChangedCallback(_ => raised = true);
            field.SetValueWithoutNotify(TestGradients.Rainbow7());

            Assert.That(raised, Is.False);
        }
    }
}
