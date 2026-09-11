using System.Collections.Generic;
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
        /// Rotation lives on the right button so the left one is free to move keys. Turning the view
        /// still picks nothing out of it.
        /// </summary>
        [Test]
        public void RightDraggingTurnsTheCubeAndChangesNothing()
        {
            var session = new GradientPicker3DSession { LivePreview = true };
            window.BeginSession(Test3DGradients.Corners8(), session);
            simulate.FrameUpdate();

            var cube = window.rootVisualElement.Q<GradientCubeElement>("cube");
            int versionBefore = window.Working.Version;
            float yawBefore = cube.Yaw;

            Vector2 from = cube.worldBound.center;
            simulate.DragAndDrop(from, from + new Vector2(40f, 0f), MouseButton.RightMouse);
            simulate.FrameUpdate();

            Assert.That(cube.Yaw, Is.Not.EqualTo(yawBefore), "the drag should have turned the cube");
            Assert.That(window.Working.Version, Is.EqualTo(versionBefore), "and changed nothing about the gradient");
        }

        [Test]
        public void LeftDraggingAKeyDoesNotTurnTheCube()
        {
            window.BeginSession(Test3DGradients.Single(), new GradientPicker3DSession());
            simulate.FrameUpdate();

            var cube = window.rootVisualElement.Q<GradientCubeElement>("cube");
            float yawBefore = cube.Yaw;
            float pitchBefore = cube.Pitch;

            Vector2 from = DotCentre(cube, alpha: false, index: 0);
            simulate.DragAndDrop(from, from + new Vector2(40f, 20f));
            simulate.FrameUpdate();

            Assert.That(cube.Yaw, Is.EqualTo(yawBefore));
            Assert.That(cube.Pitch, Is.EqualTo(pitchBefore));
        }

        [Test]
        public void LeftDraggingEmptySpaceChangesNothing()
        {
            window.BeginSession(Test3DGradients.Single(), new GradientPicker3DSession());
            simulate.FrameUpdate();

            var cube = window.rootVisualElement.Q<GradientCubeElement>("cube");
            int versionBefore = window.Working.Version;
            float yawBefore = cube.Yaw;

            // A corner of the viewport, well clear of the one key at the centre of the cube.
            Vector2 from = cube.worldBound.min + new Vector2(4f, 4f);
            simulate.DragAndDrop(from, from + new Vector2(40f, 40f));
            simulate.FrameUpdate();

            Assert.That(window.Working.Version, Is.EqualTo(versionBefore));
            Assert.That(cube.Yaw, Is.EqualTo(yawBefore));
        }

        /// <summary>
        /// A pointer gives two coordinates and a key needs three, so a drag moves the key across the XZ
        /// plane it already sits in. The height it was placed at is exactly what must not move.
        /// </summary>
        [Test]
        public void LeftDraggingAKeyMovesItAcrossItsOwnXZPlane()
        {
            window.BeginSession(Test3DGradients.Single(), new GradientPicker3DSession());
            simulate.FrameUpdate();

            var cube = window.rootVisualElement.Q<GradientCubeElement>("cube");
            Vector3 before = window.Working.ColorKeys[0].position;

            Vector2 from = DotCentre(cube, alpha: false, index: 0);
            simulate.DragAndDrop(from, from + new Vector2(30f, 15f));
            simulate.FrameUpdate();

            Vector3 after = window.Working.ColorKeys[0].position;
            Assert.That(after.y, Is.EqualTo(before.y).Within(1e-4f), "the drag should not have changed the key's height");
            Assert.That(new Vector2(after.x, after.z), Is.Not.EqualTo(new Vector2(before.x, before.z)));
        }

        [Test]
        public void ShiftLeftDraggingAKeyMovesItAlongYOnly()
        {
            window.BeginSession(Test3DGradients.Single(), new GradientPicker3DSession());
            simulate.FrameUpdate();

            var cube = window.rootVisualElement.Q<GradientCubeElement>("cube");
            Vector3 before = window.Working.ColorKeys[0].position;

            Vector2 from = DotCentre(cube, alpha: false, index: 0);
            simulate.DragAndDrop(from, from + new Vector2(25f, -40f), MouseButton.LeftMouse, EventModifiers.Shift);
            simulate.FrameUpdate();

            Vector3 after = window.Working.ColorKeys[0].position;
            Assert.That(after.y, Is.GreaterThan(before.y), "dragging up the screen should raise the key");
            Assert.That(after.x, Is.EqualTo(before.x).Within(1e-4f), "pointer x is ignored on a vertical drag");
            Assert.That(after.z, Is.EqualTo(before.z).Within(1e-4f));
        }

        [Test]
        public void AShiftDragAcrossTheScreenMovesNothing()
        {
            window.BeginSession(Test3DGradients.Single(), new GradientPicker3DSession());
            simulate.FrameUpdate();

            var cube = window.rootVisualElement.Q<GradientCubeElement>("cube");
            int versionBefore = window.Working.Version;

            Vector2 from = DotCentre(cube, alpha: false, index: 0);
            simulate.DragAndDrop(from, from + new Vector2(60f, 0f), MouseButton.LeftMouse, EventModifiers.Shift);
            simulate.FrameUpdate();

            // Checked first, because a drag that never started would also have moved nothing: an
            // activation filter matches modifiers exactly, so Shift has to be named as an activator.
            Assert.That(cube.SelectedIndex, Is.EqualTo(0), "the Shift drag should still have grabbed the key");
            Assert.That(window.Working.Version, Is.EqualTo(versionBefore));
        }

        [Test]
        public void ADraggedKeyStaysInsideTheCube()
        {
            window.BeginSession(Test3DGradients.Single(), new GradientPicker3DSession());
            simulate.FrameUpdate();

            var cube = window.rootVisualElement.Q<GradientCubeElement>("cube");
            Vector2 from = DotCentre(cube, alpha: false, index: 0);
            simulate.DragAndDrop(from, from + new Vector2(4000f, 2500f));
            simulate.FrameUpdate();

            Vector3 after = window.Working.ColorKeys[0].position;
            Assert.That(after.x, Is.InRange(0f, 1f));
            Assert.That(after.y, Is.InRange(0f, 1f));
            Assert.That(after.z, Is.InRange(0f, 1f));
        }

        /// <summary>
        /// A dot on its own does not say whether you meant a colour key or an alpha one — the ambiguity
        /// the mode buttons exist to settle. <see cref="Test3DGradients.Single"/> stacks one of each at
        /// the same point, so only the mode can decide which the drag grabbed.
        /// </summary>
        [Test]
        public void OnlyKeysOfTheSelectedModeCanBeDragged()
        {
            window.BeginSession(Test3DGradients.Single(), new GradientPicker3DSession());
            simulate.FrameUpdate();

            var cube = window.rootVisualElement.Q<GradientCubeElement>("cube");
            Vector3 alphaBefore = window.Working.AlphaKeys[0].position;

            Vector2 from = DotCentre(cube, alpha: false, index: 0);
            simulate.DragAndDrop(from, from + new Vector2(30f, 15f));
            simulate.FrameUpdate();

            Assert.That(window.Working.AlphaKeys[0].position, Is.EqualTo(alphaBefore), "colour mode moved an alpha key");
            Assert.That(window.Working.ColorKeys[0].position, Is.Not.EqualTo(alphaBefore));
        }

        [Test]
        public void DraggingAKeyUpdatesItsRowInTheKeyList()
        {
            window.BeginSession(Test3DGradients.Single(), new GradientPicker3DSession());
            simulate.FrameUpdate();

            var cube = window.rootVisualElement.Q<GradientCubeElement>("cube");
            Vector2 from = DotCentre(cube, alpha: false, index: 0);
            simulate.DragAndDrop(from, from + new Vector2(30f, 15f));
            simulate.FrameUpdate();

            var x = window.rootVisualElement.Q<FloatField>("x");
            Assert.That(x.value, Is.EqualTo(window.Working.ColorKeys[0].position.x).Within(1e-4f));
        }

        [Test]
        public void GrabbingAKeyMarksItInTheCubeAndInItsRow()
        {
            window.BeginSession(Test3DGradients.Single(), new GradientPicker3DSession());
            simulate.FrameUpdate();

            var cube = window.rootVisualElement.Q<GradientCubeElement>("cube");
            Vector2 from = DotCentre(cube, alpha: false, index: 0);
            simulate.DragAndDrop(from, from + new Vector2(30f, 15f));
            simulate.FrameUpdate();

            Assert.That(cube.SelectedIndex, Is.EqualTo(0));
            Assert.That(cube.SelectedIsAlpha, Is.False);
            Assert.That(window.rootVisualElement.Query(className: "abcw-key-list__row--selected").ToList(),
                Is.Not.Empty, "the colour key's row should be marked");
        }

        /// <summary>
        /// The centre of the dot standing for key <paramref name="index"/> of the given kind, in panel
        /// coordinates. Read off the element rather than re-derived, so the test cannot quietly agree
        /// with a projection bug.
        /// </summary>
        private static Vector2 DotCentre(GradientCubeElement cube, bool alpha, int index)
        {
            List<VisualElement> dots = cube.Query(className: "abcw-cube-key").ToList()
                .FindAll(d => d.ClassListContains("abcw-cube-key--alpha") == alpha);

            Assume.That(dots.Count, Is.GreaterThan(index));
            return dots[index].worldBound.center;
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
