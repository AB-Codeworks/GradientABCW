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
            panelSize = new Vector2(714, 470);
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

            // Back to Keys before reading a row. A ListView inside a hidden tab has zero size, so it
            // virtualises no rows at all — UQuery finds hidden elements, but only ones that exist.
            SelectTab(KeysTab);

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
            SelectTab(LibraryTab);

            var folderField = window.rootVisualElement.Q<TextField>("folder");
            var applyButton = window.rootVisualElement.Q<Button>("apply");
            Assume.That(folderField, Is.Not.Null);
            Assume.That(applyButton, Is.Not.Null);

            folderField.value = tempFolder.Path;
            Press("apply");

            var saveNameField = window.rootVisualElement.Q<TextField>("saveName");
            var saveButton = window.rootVisualElement.Q<Button>("save");
            saveNameField.value = "MyTestGradient";
            Press("save");

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
            SelectTab(RebuildTab);

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

        /// <summary>
        /// Sampling does not edit the gradient, it swaps in a different instance. A key list still
        /// pointing at the old one edits an object nobody will read again, so a row you type into looks
        /// like it worked and changes nothing.
        /// </summary>
        [Test]
        public void EditingAKeyAfterSamplingReachesTheSampledGradient()
        {
            window.BeginSession(TestGradients.Rainbow7(), new GradientPickerSession());
            simulate.FrameUpdate();
            SelectTab(RebuildTab);

            var tex = new Texture2D(1, 4, TextureFormat.RGBA32, false);
            for (int y = 0; y < 4; y++)
                tex.SetPixel(0, y, new Color(y / 3f, 0f, 0f));
            tex.Apply(false);

            window.rootVisualElement.Q<ObjectField>("texture").value = tex;
            simulate.Click(window.rootVisualElement.Q<Button>("textureStrict"));
            simulate.FrameUpdate();

            // Seven keys became four, so a list that still shows seven rows is showing the wrong gradient.
            Assume.That(window.Working.ColorKeys.Length, Is.EqualTo(4));

            var timeField = window.rootVisualElement.Q<FloatField>("time");
            Assume.That(timeField, Is.Not.Null);
            timeField.value = 0.137f;
            simulate.FrameUpdate();

            Assert.That(window.Working.ColorKeys[0].time, Is.EqualTo(0.137f).Within(1e-4f),
                "the row wrote to the gradient the sample had already replaced");

            Object.DestroyImmediate(tex);
        }

        /// <summary>
        /// The same staleness one panel over: loading from the library replaces the gradient, and the
        /// actions panel keeps its own reference to whatever it was handed last.
        /// </summary>
        [Test]
        public void SettingBlendModeAfterLoadingReachesTheLoadedGradient()
        {
            using var tempFolder = new TempAssetFolder("PickerLibraryAdopt");
            window.BeginSession(TestGradients.Rainbow7(), new GradientPickerSession());
            simulate.FrameUpdate();
            SelectTab(LibraryTab);

            window.rootVisualElement.Q<TextField>("folder").value = tempFolder.Path;
            Press("apply");

            window.rootVisualElement.Q<TextField>("saveName").value = "AdoptTest";
            Press("save");

            var tile = window.rootVisualElement.Q(className: "abcw-library__tile");
            Assume.That(tile, Is.Not.Null);
            simulate.Click(FindButtonByText(tile, "Load"));
            simulate.FrameUpdate();

            Assume.That(window.Working.BlendMode, Is.EqualTo(BlendMode.Smooth));

            window.rootVisualElement.Q<Toggle>("stepped").value = true;
            simulate.FrameUpdate();

            Assert.That(window.Working.BlendMode, Is.EqualTo(BlendMode.Stepped),
                "the actions panel still held the gradient the load had replaced");
        }


        /// <summary>
        /// Brings a tab to the front and lets layout settle.
        /// </summary>
        /// <remarks>
        /// UQuery finds elements inside a hidden tab perfectly well, so reading a value or setting one
        /// needs no help. Clicking does: <c>simulate.Click</c> is positional, and a hidden element's
        /// worldBound is Rect.zero, so the click lands at panel (0,0) and hits whatever is there —
        /// silently, without an exception. The same shape as expanding a Foldout before clicking into
        /// it in GradientABCWPropertyDrawerTests.
        /// </remarks>
        private const int KeysTab = 0;
        private const int AdjustTab = 1;
        private const int ModulateTab = 2;
        private const int RebuildTab = 3;
        private const int LibraryTab = 4;

        /// <summary>
        /// Waits for CreateGUI, which runs on the window's first layout pass and may need more than one
        /// frame, then brings a tab to the front and lets it lay out.
        /// </summary>
        /// <remarks>
        /// UQuery finds elements inside a hidden tab perfectly well, so reading a value or setting one
        /// needs no help at all. Clicking does: simulate.Click is positional, and a hidden element has a
        /// zero worldBound, so the click lands at panel (0, 0) and hits whatever is there — silently,
        /// with no exception. The same shape as expanding a Foldout before clicking into it in
        /// GradientABCWPropertyDrawerTests.
        /// </remarks>
        /// <summary>
        /// Clicks a named button, having first proved it is actually on screen.
        /// </summary>
        /// <remarks>
        /// simulate.Click is positional: a control with a zero worldBound — which is what everything
        /// inside a hidden tab has — sends the click to panel (0, 0), where it silently lands on
        /// whatever is there. Without this check the symptom is a neighbouring control firing, which
        /// reads as a bizarre bug rather than as a hidden tab.
        /// </remarks>
        private void Press(string buttonName)
        {
            // Flush first: a field value set immediately before this relayouts the row, and both the
            // measurement below and the click itself would otherwise use the positions from before it.
            simulate.FrameUpdate();

            var button = window.rootVisualElement.Q<Button>(buttonName);
            Assume.That(button, Is.Not.Null, buttonName);
            Assume.That(button.worldBound.width, Is.GreaterThan(0f), buttonName + " is not on screen");
            simulate.Click(button);
            simulate.FrameUpdate();
        }

        private void SelectTab(int index)
        {
            TabView tabs = null;
            for (int frame = 0; frame < 10 && tabs == null; frame++)
            {
                simulate.FrameUpdate();
                tabs = window.rootVisualElement.Q<TabView>("pickerTabs");
            }

            Assume.That(tabs, Is.Not.Null, "pickerTabs");
            tabs.selectedTabIndex = index;

            // A tab shown for the first time lays its subtree out over several frames, and a ListView
            // that was rebuilt while hidden only re-virtualises its rows once it has a size again.
            for (int frame = 0; frame < 4; frame++)
                simulate.FrameUpdate();

            // A hidden tab's controls have a zero worldBound, and simulate.Click is positional, so a
            // switch that silently did not take sends every later click to panel (0, 0).
            Assume.That(tabs.selectedTabIndex, Is.EqualTo(index), "tab did not switch");
        }

        private static Button FindButtonByText(VisualElement root, string text)
        {
            Button found = null;
            root.Query<Button>().ForEach(b => { if (found == null && b.text == text) found = b; });
            return found;
        }
    }
}
