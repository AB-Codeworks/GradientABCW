using NUnit.Framework;
using UnityEditor.UIElements;
using UnityEditor.UIElements.TestFramework;
using UnityEngine;
using UnityEngine.UIElements;
using ABCodeworld.Gradients.Editor;
using ABCodeworld.Gradients.Tests.Editor.Support;

namespace ABCodeworld.Gradients.Tests.Editor.UI
{
    [TestFixture]
    [Category("UI")]
    internal sealed class GradientPicker3DWindowTests : EditorWindowUITestFixture<GradientPicker3DWindow>
    {
        [SetUp]
        public void SetUpWindow()
        {
            panelSize = new Vector2(960, 720);
        }

        [Test]
        public void BeginSessionClonesTheInputGradient()
        {
            var input = Test3DGradients.Corners8();
            window.BeginSession(input, new GradientPicker3DSession());

            Assert.That(window.Working, Is.Not.SameAs(input));
            Assert.That(window.Working.ContentEquals(input), Is.True);
        }

        [Test]
        public void TheCubeShowsOneDotPerKey()
        {
            window.BeginSession(Test3DGradients.Corners8(), new GradientPicker3DSession());
            simulate.FrameUpdate();

            var cube = window.rootVisualElement.Q<GradientCubeElement>("cube");
            Assume.That(cube, Is.Not.Null);

            int expected = window.Working.ColorKeys.Length + window.Working.AlphaKeys.Length;
            Assert.That(cube.Query(className: "abcw-cube-key").ToList().Count, Is.EqualTo(expected));
        }

        [Test]
        public void AlphaDotsAreMarkedApartFromColourDots()
        {
            window.BeginSession(Test3DGradients.Corners8(), new GradientPicker3DSession());
            simulate.FrameUpdate();

            var cube = window.rootVisualElement.Q<GradientCubeElement>("cube");
            int alphaDots = cube.Query(className: "abcw-cube-key--alpha").ToList().Count;

            Assert.That(alphaDots, Is.EqualTo(window.Working.AlphaKeys.Length));
        }

        /// <summary>
        /// The cube shows keys; it never edits them. A pointer gives two coordinates and a key needs
        /// three, so there is nothing sensible for dragging a dot to mean.
        /// </summary>
        [Test]
        public void RotatingTheCubeDoesNotChangeTheGradient()
        {
            var session = new GradientPicker3DSession { LivePreview = true };
            window.BeginSession(Test3DGradients.Corners8(), session);
            simulate.FrameUpdate();

            var cube = window.rootVisualElement.Q<GradientCubeElement>("cube");
            int versionBefore = window.Working.Version;
            float yawBefore = cube.Yaw;

            Vector2 from = cube.worldBound.center;
            simulate.DragAndDrop(from, from + new Vector2(40f, 0f));
            simulate.FrameUpdate();

            Assert.That(cube.Yaw, Is.Not.EqualTo(yawBefore), "the drag should have turned the cube");
            Assert.That(window.Working.Version, Is.EqualTo(versionBefore), "and changed nothing about the gradient");
        }

        [Test]
        public void AddKeyAddsAColourKeyAtTheCentreByDefault()
        {
            window.BeginSession(Test3DGradients.Corners8(), new GradientPicker3DSession());
            simulate.FrameUpdate();

            int before = window.Working.ColorKeys.Length;
            simulate.Click(window.rootVisualElement.Q<Button>("addKey"));
            simulate.FrameUpdate();

            Assert.That(window.Working.ColorKeys.Length, Is.EqualTo(before + 1));
            Assert.That(window.Working.ColorKeys[before].position, Is.EqualTo(new Vector3(0.5f, 0.5f, 0.5f)));
        }

        [Test]
        public void AddKeyFollowsTheSelectedMode()
        {
            window.BeginSession(Test3DGradients.Corners8(), new GradientPicker3DSession());
            simulate.FrameUpdate();

            simulate.Click(window.rootVisualElement.Q<Button>("alphaMode"));
            simulate.FrameUpdate();

            int colorsBefore = window.Working.ColorKeys.Length;
            int alphasBefore = window.Working.AlphaKeys.Length;

            simulate.Click(window.rootVisualElement.Q<Button>("addKey"));
            simulate.FrameUpdate();

            Assert.That(window.Working.AlphaKeys.Length, Is.EqualTo(alphasBefore + 1));
            Assert.That(window.Working.ColorKeys.Length, Is.EqualTo(colorsBefore));
        }

        [Test]
        public void AddKeyIsDisabledAtTheKeyLimit()
        {
            window.BeginSession(Test3DGradients.Max64(), new GradientPicker3DSession());
            simulate.FrameUpdate();

            Assert.That(window.rootVisualElement.Q<Button>("addKey").enabledSelf, Is.False);
        }

        [Test]
        public void EditingACoordinateFiresSessionChanged()
        {
            GradientABCW3D changed = null;
            var session = new GradientPicker3DSession { LivePreview = true, Changed = g => changed = g };
            window.BeginSession(Test3DGradients.Corners8(), session);
            simulate.FrameUpdate();

            var x = window.rootVisualElement.Q<FloatField>("x");
            Assume.That(x, Is.Not.Null);
            x.value = 0.42f;
            simulate.FrameUpdate();

            Assert.That(changed, Is.Not.Null);
        }

        [Test]
        public void SteppedBlendDisablesFalloff()
        {
            window.BeginSession(Test3DGradients.Corners8(), new GradientPicker3DSession());
            simulate.FrameUpdate();

            var falloff = window.rootVisualElement.Q<ResettableSliderRow>("falloffPower");
            Assume.That(falloff.enabledSelf, Is.True);

            window.rootVisualElement.Q<Toggle>("stepped").value = true;
            simulate.FrameUpdate();

            Assert.That(window.Working.BlendMode, Is.EqualTo(BlendMode.Stepped));
            Assert.That(falloff.enabledSelf, Is.False);
        }

        [Test]
        public void FlipKeysMirrorsEveryKey()
        {
            window.BeginSession(Test3DGradients.Corners8(), new GradientPicker3DSession());
            simulate.FrameUpdate();

            var before = window.Working.ColorKeys[0].position;
            simulate.Click(window.rootVisualElement.Q<Button>("flipKeys"));
            simulate.FrameUpdate();

            Assert.That(window.Working.ColorKeys[0].position, Is.EqualTo(Vector3.one - before));
        }

        [Test]
        public void AcceptReportsTheEditedGradientAndDoesNotCancel()
        {
            GradientABCW3D accepted = null;
            bool cancelled = false;
            var session = new GradientPicker3DSession
            {
                Accepted = g => accepted = g,
                Cancelled = () => cancelled = true,
            };

            window.BeginSession(Test3DGradients.Corners8(), session);
            simulate.FrameUpdate();

            simulate.Click(window.rootVisualElement.Q<Button>("okBtn")); // closes the window
            Assert.That(accepted, Is.Not.Null);
            Assert.That(cancelled, Is.False);
        }

        [Test]
        public void CancelReportsCancelledAndNotAccepted()
        {
            GradientABCW3D accepted = null;
            bool cancelled = false;
            var session = new GradientPicker3DSession
            {
                Accepted = g => accepted = g,
                Cancelled = () => cancelled = true,
            };

            window.BeginSession(Test3DGradients.Corners8(), session);
            simulate.FrameUpdate();

            simulate.Click(window.rootVisualElement.Q<Button>("cancelBtn")); // closes the window
            Assert.That(cancelled, Is.True);
            Assert.That(accepted, Is.Null);
        }

        /// <summary>
        /// The two pickers keep separate Current slots, because beginning a session ends whatever the slot
        /// holds — a shared one would make opening a 3D picker silently cancel an open 1D one.
        /// </summary>
        [Test]
        public void The3DPickerDoesNotShareItsCurrentSlotWithThe1DPicker()
        {
            var flatBefore = GradientPickerWindow.Current;

            window.BeginSession(Test3DGradients.Corners8(), new GradientPicker3DSession());

            Assert.That(GradientPicker3DWindow.Current, Is.SameAs(window));
            Assert.That(GradientPickerWindow.Current, Is.SameAs(flatBefore), "the 1D picker's session was disturbed");
        }
    }
}
