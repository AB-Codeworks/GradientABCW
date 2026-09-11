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
    internal sealed class KeyList3DElementTests : UITestFixture
    {
        private KeyList3DElement list;
        private GradientABCW3D gradient;

        private void Build(bool isAlpha, float width = 380f)
        {
            panelSize = new Vector2(width + 20f, 400);
            gradient = Test3DGradients.Corners8();
            list = new KeyList3DElement(isAlpha);
            list.style.width = width;
            list.style.height = 300;
            rootVisualElement.Add(list);
            list.SetGradient(gradient);
            simulate.FrameUpdate();
        }

        [Test]
        public void ARowCarriesAllThreeCoordinates()
        {
            Build(isAlpha: false);

            Assert.That(list.Q<FloatField>("x"), Is.Not.Null);
            Assert.That(list.Q<FloatField>("y"), Is.Not.Null);
            Assert.That(list.Q<FloatField>("z"), Is.Not.Null);
        }

        /// <summary>
        /// The 1D row's field is named "time"; naming these differently is what lets a UI test tell the
        /// two windows' rows apart when both are open.
        /// </summary>
        [Test]
        public void ARowHasNoTimeField()
        {
            Build(isAlpha: false);

            Assert.That(list.Q<FloatField>("time"), Is.Null);
        }

        [Test]
        public void EditingACoordinateWritesThroughAndReportsAChange()
        {
            Build(isAlpha: false);

            bool changed = false;
            list.Changed += () => changed = true;

            var y = list.Q<FloatField>("y");
            y.value = 0.375f;
            simulate.FrameUpdate();

            bool found = false;
            foreach (var key in gradient.ColorKeys.ToArray())
                found |= Mathf.Abs(key.position.y - 0.375f) < 1e-4f;

            Assert.That(found, Is.True, "no key picked up the edited coordinate");
            Assert.That(changed, Is.True);
        }

        [Test]
        public void ACoordinateOutsideTheCubeComesBackClamped()
        {
            Build(isAlpha: false);

            var x = list.Q<FloatField>("x");
            x.value = 5f;
            simulate.FrameUpdate();

            foreach (var key in gradient.ColorKeys.ToArray())
                Assert.That(key.position.x, Is.InRange(0f, 1f));

            Assert.That(x.value, Is.InRange(0f, 1f), "the row should show the value the key actually took");
        }

        [Test]
        public void AnAlphaRowCarriesASliderRatherThanAColourField()
        {
            Build(isAlpha: true);

            Assert.That(list.Q<Slider>("value"), Is.Not.Null);
            Assert.That(list.Q<ColorField>("value"), Is.Null);
        }

        [Test]
        public void DeletingRemovesAKey()
        {
            Build(isAlpha: false);
            int before = gradient.ColorKeys.Length;

            simulate.Click(list.Q<Button>(className: "abcw-key-list__delete"));
            simulate.FrameUpdate();

            Assert.That(gradient.ColorKeys.Length, Is.EqualTo(before - 1));
        }

        /// <summary>
        /// A row that runs out of room used to shrink its children unevenly, and the axis labels — one
        /// character each — were the first thing to collapse to an ellipsis, so rows read
        /// <c>x 0.500 … 0.000 z 0.500</c>. Fixed size is only a suggestion in UI Toolkit, which defaults
        /// <c>flex-shrink</c> to 1, so the row is made to adapt rather than re-tuned in pixels.
        /// </summary>
        [Test]
        public void AxisFieldsStaySquareInANarrowColumn()
        {
            // Narrow enough that the preferred widths (60 + 3x58 + 22 plus margins) do not all fit.
            Build(isAlpha: false, width: 240f);

            foreach (string axis in new[] { "x", "y", "z" })
            {
                var field = list.Q<FloatField>(axis);
                Assume.That(field, Is.Not.Null, $"no {axis} field");

                float labelWidth = field.labelElement.resolvedStyle.width;
                // A hair under the pin, not exactly it: resolved widths come back rounded to the panel's
                // pixel grid, so an exact boundary would be a coin flip on a fractional DPI scale.
                Assert.That(labelWidth, Is.GreaterThan(FieldLabels.SingleCharacterWidth - 1f),
                    $"the {axis} label was squeezed, so it renders as an ellipsis");
                Assert.That(field.resolvedStyle.width - labelWidth, Is.GreaterThan(20f),
                    $"the {axis} input has no width left to draw in");
            }
        }

        [Test]
        public void TheListShowsOneRowPerKey()
        {
            Build(isAlpha: false);

            var listView = list.Q<ListView>("colorKeyList");
            Assert.That(listView, Is.Not.Null);
            Assert.That(listView.itemsSource.Count, Is.EqualTo(gradient.ColorKeys.Length));
        }
    }
}
