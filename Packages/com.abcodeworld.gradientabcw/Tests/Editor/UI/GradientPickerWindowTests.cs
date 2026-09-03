using NUnit.Framework;
using UnityEditor.UIElements;
using UnityEditor.UIElements.TestFramework;
using UnityEngine;
using UnityEngine.UIElements;
using ABCodeworld.Gradients.Editor;
using ABCodeworld.Gradients.Tests.Editor.Support;
using Object = UnityEngine.Object;

namespace ABCodeworld.Gradients.Tests.Editor.UI
{
    [TestFixture]
    [Category("UI")]
    internal sealed class GradientPickerWindowTests : EditorWindowUITestFixture<GradientPickerWindow>
    {
        [SetUp]
        public void SetUpWindow()
        {
            panelSize = new Vector2(820, 640);
        }

        [Test]
        public void BeginSession_ClonesTheInputGradient()
        {
            var input = TestGradients.Rainbow7();
            window.BeginSession(input, new GradientPickerSession());

            Assert.That(window.Working, Is.Not.SameAs(input));
            Assert.That(window.Working.ContentEquals(input), Is.True);
        }

        [Test]
        public void EditingTimeField_FiresSessionChanged()
        {
            GradientABCW changed = null;
            var session = new GradientPickerSession { LivePreview = true, Changed = g => changed = g };
            window.BeginSession(TestGradients.Rainbow7(), session);
            simulate.FrameUpdate();

            var timeField = window.rootVisualElement.Q<FloatField>("time");
            Assume.That(timeField, Is.Not.Null);
            timeField.value = 0.42f;
            simulate.FrameUpdate();

            Assert.That(changed, Is.Not.Null);
        }

        [Test]
        public void Accept_InvokesAcceptedWithEditedGradientAndNotCancelled()
        {
            GradientABCW accepted = null;
            bool cancelled = false;
            var session = new GradientPickerSession { Accepted = g => accepted = g, Cancelled = () => cancelled = true };
            window.BeginSession(TestGradients.Rainbow7(), session);
            simulate.FrameUpdate();

            var okButton = window.rootVisualElement.Q<Button>("okBtn");
            Assume.That(okButton, Is.Not.Null);
            simulate.Click(okButton); // closes the window; do not FrameUpdate afterwards, the panel is gone

            Assert.That(accepted, Is.Not.Null);
            Assert.That(cancelled, Is.False);
        }

        [Test]
        public void Cancel_InvokesCancelledOnly()
        {
            GradientABCW accepted = null;
            bool cancelled = false;
            var session = new GradientPickerSession { Accepted = g => accepted = g, Cancelled = () => cancelled = true };
            window.BeginSession(TestGradients.Rainbow7(), session);
            simulate.FrameUpdate();

            var cancelButton = window.rootVisualElement.Q<Button>("cancelBtn");
            Assume.That(cancelButton, Is.Not.Null);
            simulate.Click(cancelButton); // closes the window; do not FrameUpdate afterwards, the panel is gone

            Assert.That(cancelled, Is.True);
            Assert.That(accepted, Is.Null);
        }

        [Test]
        public void SaveThenLoad_RoundTripsThroughLibrary()
        {
            using var tempFolder = new TempAssetFolder("PickerLibrary");
            window.BeginSession(TestGradients.Rainbow7(), new GradientPickerSession());
            simulate.FrameUpdate();

            var folderField = window.rootVisualElement.Q<TextField>("folder");
            var applyButton = window.rootVisualElement.Q<Button>("apply");
            Assume.That(folderField, Is.Not.Null);
            Assume.That(applyButton, Is.Not.Null);

            folderField.value = tempFolder.Path;
            simulate.Click(applyButton);
            simulate.FrameUpdate();

            var saveNameField = window.rootVisualElement.Q<TextField>("saveName");
            var saveButton = window.rootVisualElement.Q<Button>("save");
            saveNameField.value = "MyTestGradient";
            simulate.Click(saveButton);
            simulate.FrameUpdate();

            var tile = window.rootVisualElement.Q(className: "abcw-library__tile");
            Assert.That(tile, Is.Not.Null);

            var loadButton = FindButtonByText(tile, "Load");
            Assume.That(loadButton, Is.Not.Null);
            simulate.Click(loadButton);
            simulate.FrameUpdate();
        }

        [Test]
        public void TextureStrict_FourRowTexture_YieldsFourOrderedKeys()
        {
            window.BeginSession(TestGradients.Default(), new GradientPickerSession());
            simulate.FrameUpdate();

            var tex = new Texture2D(1, 4, TextureFormat.RGBA32, false);
            for (int y = 0; y < 4; y++)
                tex.SetPixel(0, y, new Color(y / 3f, 0f, 0f));
            tex.Apply(false);

            var textureField = window.rootVisualElement.Q<ObjectField>("texture");
            var strictButton = window.rootVisualElement.Q<Button>("textureStrict");
            Assume.That(textureField, Is.Not.Null);
            Assume.That(strictButton, Is.Not.Null);

            textureField.value = tex;
            simulate.Click(strictButton);
            simulate.FrameUpdate();

            Assert.That(window.Working.ColorKeys.Length, Is.EqualTo(4));
            var keys = window.Working.ColorKeys;
            for (int i = 1; i < keys.Length; i++)
                Assert.That(keys[i].time, Is.GreaterThan(keys[i - 1].time));

            Object.DestroyImmediate(tex);
        }

        [Test]
        public void OpeningSecondSession_CancelsTheFirst()
        {
            bool firstCancelled = false;
            window.BeginSession(TestGradients.Default(), new GradientPickerSession { Cancelled = () => firstCancelled = true });
            simulate.FrameUpdate();

            var secondWindow = ScriptableObject.CreateInstance<GradientPickerWindow>();
            secondWindow.BeginSession(TestGradients.Rainbow7(), new GradientPickerSession());

            Assert.That(firstCancelled, Is.True);
            Assert.That(GradientPickerWindow.Current, Is.SameAs(secondWindow));

            Object.DestroyImmediate(secondWindow);
        }

        [Test]
        public void RepeatedOpenClose_DoesNotLeakPreviewTextures()
        {
            for (int i = 0; i < 20; i++)
            {
                RecreatePanel();
                window.BeginSession(TestGradients.Rainbow7(), new GradientPickerSession());
                simulate.FrameUpdate();
            }

            int liveCount = 0;
            foreach (var tex in Resources.FindObjectsOfTypeAll<Texture2D>())
            {
                if (tex != null && tex.name == "GradientABCWPreview")
                    liveCount++;
            }

            // Only the currently-open window's own preview elements should still be alive; if
            // GradientPreviewTexture failed to Dispose() on detach, this would grow by roughly
            // one set of previews per iteration (well past this threshold at 20 iterations).
            Assert.That(liveCount, Is.LessThan(10));
        }

        private static Button FindButtonByText(VisualElement root, string text)
        {
            Button found = null;
            root.Query<Button>().ForEach(b => { if (found == null && b.text == text) found = b; });
            return found;
        }
    }
}
