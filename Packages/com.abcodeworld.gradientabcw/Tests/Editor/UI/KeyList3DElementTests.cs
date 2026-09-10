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

        private void Build(bool isAlpha)
        {
            panelSize = new Vector2(400, 400);
            gradient = Test3DGradients.Corners8();
            list = new KeyList3DElement(isAlpha);
            list.style.width = 380;
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
