using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ABCodeworld.Gradients.Editor
{
    /// <summary>
    /// A pure UI Toolkit gradient editor window: a rail of key sources, the gradient bar, and
    /// everything else behind tabs. Edits a working clone of the gradient it was opened with and
    /// reports back through the <see cref="GradientPickerSession"/> supplied to <see cref="Open"/>.
    /// </summary>
    /// <remarks>
    /// Shares its skeleton with the 3D picker through <c>GradientPickerShell.uxml</c>; the two differ
    /// only in what fills the rail and the viewport. Everything that is not the gradient — the
    /// library, the utilities, modulation, rebuilding — sits in a <see cref="TabView"/> so it costs
    /// no space until it is asked for. The library used to hold 220px at the top of the window for
    /// something used once a session.
    /// </remarks>
    public sealed class GradientPickerWindow : EditorWindow
    {
        public static GradientPickerWindow Current { get; private set; }

        private GradientPickerSession session;
        private bool sessionEnded;
        private bool accepted;
        private GradientABCW working;

        private GradientBarElement bar;
        private KeyPaletteDraggable colorPalette;
        private KeyPaletteDraggable alphaPalette;
        private KeyListElement colorList;
        private KeyListElement alphaList;
        private GradientLibraryPanel libraryPanel;
        private GradientAdjustPanel adjustPanel;
        private GradientRebuildPanel rebuildPanel;
        private ModulationPanel modulationPanel;
        private Label keyCount;
        private IVisualElementScheduledItem pendingLivePreview;

        /// <summary>The gradient being edited (a clone of what the window was opened with).</summary>
        public GradientABCW Working => working;

        public static GradientPickerWindow Open(GradientABCW initial, GradientPickerSession session, string title = "Gradient Picker")
        {
            var window = CreateInstance<GradientPickerWindow>();
            window.titleContent = new GUIContent(title);
            window.minSize = new Vector2(700, 430);
            EditorWindowPlacement.CenterOnMainWindow(window, 714, 470);
            window.BeginSession(initial, session);
            window.ShowUtility();
            window.Focus();
            return window;
        }

        [MenuItem("Window/ABCodeworld/Gradient ABCW Picker")]
        public static void OpenStandalone() => Open(GradientABCW.CreateDefault(), new GradientPickerSession(), "Gradient Picker");

        [InitializeOnLoadMethod]
        private static void RegisterAsDefaultPickerLauncher() =>
            GradientABCWField.PickerLauncher = (initial, session) => Open(initial, session);

        internal void BeginSession(GradientABCW initial, GradientPickerSession newSession)
        {
            if (Current != null && Current != this)
                Current.EndSession(didAccept: false);

            session = newSession;
            sessionEnded = false;
            accepted = false;
            working = (initial ?? GradientABCW.CreateDefault()).Clone();
            Current = this;

            RefreshAllViews();
        }

        private void CreateGUI()
        {
            var uxml = PackagePaths.Load<VisualTreeAsset>("Editor/UI/GradientPickerShell.uxml");
            var pickerUss = PackagePaths.Load<StyleSheet>("Editor/UI/GradientPickerWindow.uss");
            var barUss = PackagePaths.Load<StyleSheet>("Editor/UI/GradientBar.uss");
            var fieldUss = PackagePaths.Load<StyleSheet>("Editor/UI/GradientABCWField.uss");
            var tabsUss = PackagePaths.Load<StyleSheet>("Editor/UI/GradientTabs.uss");

            rootVisualElement.Clear();

            // Order is precedence: the shell sheet is added last so its rules win ties against the
            // chrome sheet, the same discipline GradientCube.uss documents.
            if (pickerUss != null) rootVisualElement.styleSheets.Add(pickerUss);
            if (barUss != null) rootVisualElement.styleSheets.Add(barUss);
            if (fieldUss != null) rootVisualElement.styleSheets.Add(fieldUss);
            if (tabsUss != null) rootVisualElement.styleSheets.Add(tabsUss);

            if (uxml != null)
                uxml.CloneTree(rootVisualElement);
            else
                rootVisualElement.Add(new Label("GradientPickerWindow: UXML not found"));

            rootVisualElement.AddToClassList("abcw-picker");
            rootVisualElement.style.flexGrow = 1;
            rootVisualElement.RegisterCallback<KeyDownEvent>(OnWindowKeyDown);

            var railHost = rootVisualElement.Q<VisualElement>("railHost");
            var viewportHost = rootVisualElement.Q<VisualElement>("viewportHost");
            var hintsHost = rootVisualElement.Q<VisualElement>("hintsHost");
            var tabHost = rootVisualElement.Q<VisualElement>("tabHost");
            var okButton = rootVisualElement.Q<Button>("okBtn");
            var cancelButton = rootVisualElement.Q<Button>("cancelBtn");

            bar = new GradientBarElement { name = "bar" };
            bar.style.flexGrow = 1;
            viewportHost?.Add(bar);
            bar.RegisterCallback<GradientChangedEvent>(_ => OnWorkingChanged());

            BuildRail(railHost);
            PickerShell.Hints(hintsHost,
                "\u21e7 click adds a colour key",
                "\u2325 click adds an alpha key",
                "drag a key off its lane to remove",
                "Del removes the selection");
            BuildTabs(tabHost);

            okButton?.RegisterCallback<ClickEvent>(_ => Accept());
            cancelButton?.RegisterCallback<ClickEvent>(_ => Cancel());

            RefreshAllViews();
        }

        /// <summary>
        /// The rail: where keys come from, said out loud. The two swatches used to sit unlabelled in a
        /// 100px column with no text anywhere in the window explaining what they were for.
        /// </summary>
        private void BuildRail(VisualElement railHost)
        {
            if (railHost == null)
                return;

            var caption = new Label("ADD A KEY");
            caption.AddToClassList("abcw-rail__cap");
            railHost.Add(caption);

            colorPalette = new KeyPaletteDraggable(false) { name = "colorSource" };
            alphaPalette = new KeyPaletteDraggable(true) { name = "alphaSource" };
            railHost.Add(colorPalette);
            railHost.Add(alphaPalette);

            foreach (var palette in new[] { colorPalette, alphaPalette })
            {
                palette.Dragging += OnPaletteDragging;
                palette.Dropped += OnPaletteDropped;
                palette.ClickAdd += OnPaletteClicked;
            }

            railHost.Add(new VisualElement { style = { flexGrow = 1 } });

            keyCount = new Label();
            keyCount.AddToClassList("abcw-rail__count");
            railHost.Add(keyCount);
        }

        private void BuildTabs(VisualElement tabHost)
        {
            if (tabHost == null)
                return;

            colorList = new KeyListElement(isAlpha: false);
            alphaList = new KeyListElement(isAlpha: true);
            alphaList.AddToClassList("abcw-list-card--spaced");
            colorList.Changed += OnWorkingChanged;
            alphaList.Changed += OnWorkingChanged;

            var keysPane = new VisualElement { name = "keysPane" };
            keysPane.AddToClassList("abcw-tab-pane");
            keysPane.AddToClassList("abcw-tab-pane--row");
            keysPane.Add(colorList);
            keysPane.Add(alphaList);

            adjustPanel = new GradientAdjustPanel { name = "adjustPane" };
            adjustPanel.Changed += OnWorkingChanged;

            modulationPanel = new ModulationPanel { name = "modulatePane" };
            modulationPanel.AddToClassList("abcw-tab-pane");
            modulationPanel.RegisterValueChangedCallback(evt =>
            {
                if (working == null) return;
                working.Modulation = evt.newValue;
                OnWorkingChanged();
            });

            rebuildPanel = new GradientRebuildPanel { name = "rebuildPane" };
            rebuildPanel.AddTextureAction("textureStrict", "Strict LUT",
                "Build keys from a 1xN vertical LUT (first column), up to 16 entries.", SampleTextureStrict);
            rebuildPanel.AddTextureAction("textureRandom", "Scatter",
                "Build a 16-key gradient by sampling and organizing colors from the entire texture.", SampleTextureRandom);
            rebuildPanel.AddMeshAction("sampleMesh", "Sample vertex colours",
                "Rebuild the gradient from the mesh's coloured vertices.", SampleMesh);

            libraryPanel = new GradientLibraryPanel { GetCurrentGradient = () => working };
            libraryPanel.AddToClassList("abcw-tab-pane");
            libraryPanel.Loaded += AdoptWorking;

            // Deliberately no viewDataKey: a remembered tab would make a freshly opened picker land
            // wherever the last one was left, which is neither predictable nor what a session editing a
            // different gradient wants.
            var tabs = new TabView { name = "pickerTabs" };
            tabs.AddToClassList("abcw-tabs");
            PickerShell.AddTab(tabs, "Keys", "keysTab", keysPane);
            PickerShell.AddTab(tabs, "Adjust", "adjustTab", adjustPanel);
            PickerShell.AddTab(tabs, "Modulate", "modulateTab", modulationPanel);
            PickerShell.AddTab(tabs, "Rebuild", "rebuildTab", rebuildPanel);
            PickerShell.AddTab(tabs, "Library", "libraryTab", libraryPanel);
            tabHost.Add(tabs);
            tabs.selectedTabIndex = 0;

            // A key list rebuilt while its tab was hidden has no rows at all: a ListView with no size
            // virtualises nothing. Sampling on the Rebuild tab and switching back to Keys is exactly
            // that sequence, so the newly shown tab re-reads the gradient.
            tabs.activeTabChanged += (_, _) => RefreshAllViews();
        }

        /// <summary>Escape cancels and Enter accepts, which the window has never answered to.</summary>
        private void OnWindowKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.Escape)
            {
                Cancel();
                evt.StopPropagation();
                return;
            }

            // Not while a text field has focus: Enter there means "commit this number".
            if ((evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                && rootVisualElement.panel?.focusController?.focusedElement is not ITextEdition)
            {
                Accept();
                evt.StopPropagation();
            }
        }

        private void SampleTextureStrict()
        {
            if (!ColorSampler.TrySampleStrict(rebuildPanel.Texture, out var result, out string error))
            {
                EditorUtility.DisplayDialog("Strict LUT", error, "OK");
                return;
            }
            AdoptWorking(result);
        }

        private void SampleTextureRandom()
        {
            var source = new TextureColorSource(rebuildPanel.Texture);
            if (source.Error != null)
            {
                EditorUtility.DisplayDialog("Scatter", source.Error, "OK");
                return;
            }
            if (!ColorSampler.TrySampleRandom(source, 16, rebuildPanel.Seed, out var result, out string error))
            {
                EditorUtility.DisplayDialog("Scatter", error, "OK");
                return;
            }
            AdoptWorking(result);
        }

        private void SampleMesh()
        {
            var source = new MeshColorSource(rebuildPanel.Mesh);
            if (!ColorSampler.TrySampleRandom(source, 16, rebuildPanel.Seed, out var result, out string error))
            {
                EditorUtility.DisplayDialog("Sample Mesh", error, "OK");
                return;
            }
            AdoptWorking(result);
        }

        /// <summary>
        /// Clicking a key source adds its kind at the centre of the bar. The swatches were drag-only,
        /// and nothing said they were draggable, so a click did nothing a user could understand.
        /// </summary>
        private void OnPaletteClicked(bool isAlpha)
        {
            if (working == null)
                return;

            const float centre = 0.5f;
            if (isAlpha)
            {
                if (working.CanAddAlphaKey)
                    working.AddAlphaKey(working.EvaluateBase(centre).a, centre);
            }
            else if (working.CanAddColorKey)
            {
                working.AddColorKey(working.EvaluateBase(centre), centre);
            }

            OnWorkingChanged();
        }

        private void RefreshAllViews()
        {
            if (bar == null || working == null)
                return;

            bar.Gradient = working;
            colorList.SetGradient(working);
            alphaList.SetGradient(working);
            adjustPanel.SetGradient(working);
            modulationPanel.SetValueWithoutNotify(working.Modulation);
            colorPalette.SetColor(working.ColorKeys.Length > 0 ? working.ColorKeys[0].color : Color.white);
            SyncKeyCount();
        }

        private void OnPaletteDragging(Vector2 panelPosition, bool isAlpha) =>
            bar.SetGhost(true, isAlpha, bar.GetTimeFromPanelPosition(panelPosition));

        private void OnPaletteDropped(Vector2 panelPosition, bool isAlpha)
        {
            bar.SetGhost(false, isAlpha, 0f);
            float t = bar.GetTimeFromPanelPosition(panelPosition);

            if (isAlpha)
            {
                if (working.CanAddAlphaKey)
                    working.AddAlphaKey(working.EvaluateBase(t).a, t);
            }
            else if (working.CanAddColorKey)
            {
                working.AddColorKey(working.EvaluateBase(t), t);
            }

            OnWorkingChanged();
        }

        /// <summary>
        /// Takes on a gradient that arrived whole — loaded from the library, or rebuilt by sampling — in
        /// place of the one being edited.
        /// </summary>
        /// <remarks>
        /// Distinct from <see cref="OnWorkingChanged"/>, and the distinction is the whole point.
        /// That one announces an edit to the gradient the panels already hold, so re-reading values is
        /// enough. This one hands them a <em>different instance</em>, and every panel caches its own
        /// reference: <see cref="KeyListElement.Refresh"/> re-reads the gradient it was last given, and
        /// <see cref="GradientActionsPanel"/> mutates the one it was last given. Announcing the swap
        /// without <see cref="RefreshAllViews"/> left both of them editing an object nobody would read
        /// again — a key row you typed into after a Load looked like it worked and changed nothing.
        /// </remarks>
        private void AdoptWorking(GradientABCW replacement)
        {
            working = replacement;
            RefreshAllViews();
            OnWorkingChanged();
        }

        /// <summary>
        /// Announces an edit to the gradient the panels already hold. For a replacement, use
        /// <see cref="AdoptWorking"/>.
        /// </summary>
        private void OnWorkingChanged()
        {
            bar.Gradient = working;
            colorList.Refresh();
            alphaList.Refresh();
            adjustPanel.Refresh();
            SyncKeyCount();
            RequestLivePreview();
        }

        /// <summary>
        /// The rail's running total. The 32-key cap used to fail in silence: adding past it simply
        /// did nothing, with no count, no disabled control and no message.
        /// </summary>
        private void SyncKeyCount()
        {
            if (keyCount == null || working == null)
                return;

            int used = working.ColorKeys.Length + working.AlphaKeys.Length;
            keyCount.text = $"{used} / {GradientABCW.MaxKeys * 2}";
        }

        /// <summary>
        /// Queues at most one live-preview push per editor frame.
        /// </summary>
        /// <remarks>
        /// A drag raises <see cref="OnWorkingChanged"/> on every pointer-move, and each push is far from
        /// free: it clones the whole gradient and, once the property drawer writes it through, costs a
        /// serialize and an undo record too. Pointer-moves arrive faster than frames, so coalescing here
        /// drops the redundant ones without changing what the caller eventually sees.
        /// </remarks>
        private void RequestLivePreview()
        {
            if (session == null || !session.LivePreview || session.Changed == null)
                return;

            pendingLivePreview ??= rootVisualElement.schedule.Execute(PushLivePreview);
            pendingLivePreview.ExecuteLater(0);
        }

        private void PushLivePreview()
        {
            if (sessionEnded || working == null)
                return;
            session?.Changed?.Invoke(working.Clone());
        }

        private void Accept()
        {
            accepted = true;
            Close();
        }

        private void Cancel()
        {
            accepted = false;
            Close();
        }

        private void OnDestroy() => EndSession(accepted);

        private void EndSession(bool didAccept)
        {
            if (sessionEnded)
                return;
            sessionEnded = true;
            if (Current == this)
                Current = null;

            var finalGradient = working?.Clone();
            var endingSession = session;
            if (didAccept)
                endingSession?.Accepted?.Invoke(finalGradient);
            else
                endingSession?.Cancelled?.Invoke();
        }
    }
}
