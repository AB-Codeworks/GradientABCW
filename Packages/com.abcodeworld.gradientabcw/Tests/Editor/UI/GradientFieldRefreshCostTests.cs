using System.Linq;
using NUnit.Framework;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.UIElements.TestFramework;
using ABCodeworld.Gradients.Editor;
using ABCodeworld.Gradients.Tests.Editor.Support;

namespace ABCodeworld.Gradients.Tests.Editor.UI
{
    /// <summary>
    /// Guards how much work a visible inspector field does. These are correctness tests as much as
    /// performance ones: the field used to announce one edit twice, and to keep re-baking previews that
    /// nothing had invalidated — including the final preview inside a collapsed foldout.
    /// </summary>
    [TestFixture]
    [Category("UI")]
    internal sealed class GradientFieldRefreshCostTests : UITestFixture
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

        // By name, not by position. These were First()/Last() over the element tree, so when the two
        // previews swapped places every assertion below silently inverted and stayed green.
        private GradientPreviewElement FinalPreview() =>
            field.Q<GradientPreviewElement>(GradientABCWField.FinalPreviewName);

        private GradientPreviewElement BasePreview() =>
            field.Q<GradientPreviewElement>(GradientABCWField.BasePreviewName);

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
        public void InPlaceMutation_RaisesGradientChangedOnceAndNoValueChange()
        {
            field.value = TestGradients.Rainbow7();
            simulate.FrameUpdate();

            int gradientChanged = 0, valueChanged = 0;
            field.RegisterCallback<GradientChangedEvent>(_ => gradientChanged++);
            field.RegisterValueChangedCallback(_ => valueChanged++);

            ExecuteMenuAction(field.Q<ToolbarMenu>(), "Flip Keys");
            simulate.FrameUpdate();

            // The field used to raise both events for one in-place edit, so any listener handling both --
            // the property drawer does -- serialized twice and left two undo records per change.
            Assert.That(gradientChanged, Is.EqualTo(1));
            Assert.That(valueChanged, Is.EqualTo(0), "in-place edits are not value changes; the instance did not change");
        }

        [Test]
        public void PointingTheFieldAtANewGradient_StillRaisesValueChanged()
        {
            field.value = TestGradients.Rainbow7();
            simulate.FrameUpdate();

            int valueChanged = 0;
            field.RegisterValueChangedCallback(_ => valueChanged++);

            field.value = TestGradients.Default();
            simulate.FrameUpdate();

            Assert.That(valueChanged, Is.EqualTo(1));
        }

        [Test]
        public void ReassigningTheSameGradient_DoesNotRebakeThePreview()
        {
            var g = TestGradients.Rainbow7();
            field.value = g;
            simulate.FrameUpdate();

            var preview = FinalPreview();
            int bakesAfterFirstShow = preview.BakeCount;
            Assume.That(bakesAfterFirstShow, Is.GreaterThan(0), "the visible preview should have baked at least once");

            for (int i = 0; i < 5; i++)
            {
                field.SetValueWithoutNotify(g);
                simulate.FrameUpdate();
            }

            // Re-assigning the same instance is how callers request a refresh; it used to clear the
            // version stamp and force a full re-bake every time.
            Assert.That(preview.BakeCount, Is.EqualTo(bakesAfterFirstShow));
        }

        [Test]
        public void EditingWhileTheModulationFoldoutIsCollapsed_DoesNotBakeTheUnmodulatedBasePreview()
        {
            var g = TestGradients.Rainbow7();
            field.value = g;
            simulate.FrameUpdate();

            var unmodulatedBase = BasePreview();
            Assume.That(field.Q<Foldout>().value, Is.False, "the modulation foldout starts collapsed");

            for (int i = 0; i < 5; i++)
            {
                ExecuteMenuAction(field.Q<ToolbarMenu>(), "Flip Keys");
                simulate.FrameUpdate();
            }

            // A collapsed Foldout hides its children by setting display:none on a parent, which leaves the
            // preview's own resolved width at zero. That used to fall through to a 256px default, so the
            // hidden preview re-baked on every edit. The base preview is the hidden one now that the final
            // preview leads the control.
            Assert.That(unmodulatedBase.BakeCount, Is.EqualTo(0));
        }

        [Test]
        public void ExpandingTheModulationFoldout_BakesTheUnmodulatedBasePreviewOnce()
        {
            field.value = TestGradients.Rainbow7();
            simulate.FrameUpdate();

            var unmodulatedBase = BasePreview();
            Assume.That(unmodulatedBase.BakeCount, Is.EqualTo(0));

            field.Q<Foldout>().value = true;
            simulate.FrameUpdate();

            Assert.That(unmodulatedBase.BakeCount, Is.EqualTo(1));
        }
    }
}
