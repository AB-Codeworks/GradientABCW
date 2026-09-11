using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ABCodeworld.Gradients.Editor
{
    /// <summary>
    /// A pure UI Toolkit 3D gradient editor window: a rotatable cube, mode buttons, key lists, an asset
    /// library, and mesh/texture sampling. Edits a working clone of the gradient it was opened with and
    /// reports back through the <see cref="GradientPicker3DSession"/> supplied to <see cref="Open"/>.
    /// </summary>
    /// <remarks>
    /// Laid out to match the 1D picker so the two are recognisably the same tool, with one substitution:
    /// where that window has a gradient bar you drag keys along, this has a cube you turn and drag keys
    /// inside. A pointer gives two coordinates and a 3D key needs three, so a drag has to choose a plane
    /// and a modifier chooses the other axis — see <see cref="GradientCubeElement"/>. That ambiguity is
    /// also why the column holding the 1D picker's two draggable swatches holds mode buttons here: a dot
    /// in a cube does not say whether you meant a colour key or an alpha one, so the mode says it
    /// instead, and only that kind answers to a drag.
    /// </remarks>
    public sealed class GradientPicker3DWindow : EditorWindow
    {
        /// <summary>
        /// The live 3D picker, if any.
        /// </summary>
        /// <remarks>
        /// Separate from <see cref="GradientPickerWindow.Current"/> on purpose. Beginning a session ends
        /// whatever session this slot holds, so a shared slot would make opening a 3D picker silently
        /// cancel an open 1D one, and the other way round.
        /// </remarks>
        public static GradientPicker3DWindow Current { get; private set; }

        private static readonly Vector3 CubeCentre = new Vector3(0.5f, 0.5f, 0.5f);

        private GradientPicker3DSession session;
        private bool sessionEnded;
        private bool accepted;
        private GradientABCW3D working;

        private GradientCubeElement cube;
        private KeyModeSelector modeSelector;
        private KeyList3DElement colorList;
        private KeyList3DElement alphaList;
        private GradientLibrary3DPanel libraryPanel;
        private GradientAdjust3DPanel adjustPanel;
        private GradientRebuildPanel rebuildPanel;
        private ModulationPanel modulationPanel;
        private CubePreviewStripElement resultStrip;
        private Label keyCount;
        private IVisualElementScheduledItem pendingLivePreview;

        /// <summary>The gradient being edited (a clone of what the window was opened with).</summary>
        public GradientABCW3D Working => working;

        public static GradientPicker3DWindow Open(GradientABCW3D initial, GradientPicker3DSession session, string title = "3D Gradient Picker")
        {
            var window = CreateInstance<GradientPicker3DWindow>();
            window.titleContent = new GUIContent(title);

            // Taller than the flat picker, and for one reason: a cube is square, so its viewport
            // cannot be flattened the way the gradient strip was.
            window.minSize = new Vector2(700, 560);
            EditorWindowPlacement.CenterOnMainWindow(window, 714, 620);
            window.BeginSession(initial, session);
            window.ShowUtility();
            window.Focus();
            return window;
        }

        [MenuItem("Window/ABCodeworld/Gradient ABCW 3D Picker")]
        public static void OpenStandalone() => Open(GradientABCW3D.CreateDefault(), new GradientPicker3DSession(), "3D Gradient Picker");

        [InitializeOnLoadMethod]
        private static void RegisterAsDefaultPickerLauncher() =>
            GradientABCW3DField.PickerLauncher = (initial, session) => Open(initial, session);

        internal void BeginSession(GradientABCW3D initial, GradientPicker3DSession newSession)
        {
            if (Current != null && Current != this)
                Current.EndSession(didAccept: false);

            session = newSession;
            sessionEnded = false;
            accepted = false;
            working = (initial ?? GradientABCW3D.CreateDefault()).Clone();
            Current = this;

            RefreshAllViews();
        }

        private void CreateGUI()
        {
            var uxml = PackagePaths.Load<VisualTreeAsset>("Editor/UI/GradientPickerShell.uxml");
            var pickerUss = PackagePaths.Load<StyleSheet>("Editor/UI/GradientPickerWindow.uss");
            var barUss = PackagePaths.Load<StyleSheet>("Editor/UI/GradientBar.uss");
            var fieldUss = PackagePaths.Load<StyleSheet>("Editor/UI/GradientABCWField.uss");
            var cubeFieldUss = PackagePaths.Load<StyleSheet>("Editor/UI/GradientABCW3DField.uss");
            var tabsUss = PackagePaths.Load<StyleSheet>("Editor/UI/GradientTabs.uss");
            var cubeUss = PackagePaths.Load<StyleSheet>("Editor/UI/GradientCube.uss");

            rootVisualElement.Clear();

            // Order is precedence, and the cube sheet stays last so its rules win ties.
            if (pickerUss != null) rootVisualElement.styleSheets.Add(pickerUss);
            if (barUss != null) rootVisualElement.styleSheets.Add(barUss);
            if (fieldUss != null) rootVisualElement.styleSheets.Add(fieldUss);
            if (cubeFieldUss != null) rootVisualElement.styleSheets.Add(cubeFieldUss);
            if (tabsUss != null) rootVisualElement.styleSheets.Add(tabsUss);
            if (cubeUss != null) rootVisualElement.styleSheets.Add(cubeUss);

            if (uxml != null)
                uxml.CloneTree(rootVisualElement);
            else
                rootVisualElement.Add(new Label("GradientPicker3DWindow: UXML not found"));

            rootVisualElement.AddToClassList("abcw-picker");
            rootVisualElement.style.flexGrow = 1;
            rootVisualElement.RegisterCallback<KeyDownEvent>(OnWindowKeyDown);

            var railHost = rootVisualElement.Q<VisualElement>("railHost");
            var viewportHost = rootVisualElement.Q<VisualElement>("viewportHost");
            var hintsHost = rootVisualElement.Q<VisualElement>("hintsHost");
            var tabHost = rootVisualElement.Q<VisualElement>("tabHost");
            var okButton = rootVisualElement.Q<Button>("okBtn");
            var cancelButton = rootVisualElement.Q<Button>("cancelBtn");

            BuildViewport(viewportHost);
            BuildRail(railHost);
            PickerShell.Hints(hintsHost,
                "right-drag turns the cube",
                "drag a key across the floor of the cube",
                "\u21e7 drag moves it up and down",
                "only keys of the selected mode move");
            BuildTabs(tabHost);

            okButton?.RegisterCallback<ClickEvent>(_ => Accept());
            cancelButton?.RegisterCallback<ClickEvent>(_ => Cancel());

            RefreshAllViews();
        }

        /// <summary>
        /// The cube, and beside it the four fixed-angle renders that stand in for a swatch.
        /// </summary>
        /// <remarks>
        /// The cube is drawn as a centred square sized by the shorter side of its host, so a viewport
        /// as wide as the tab pane below would be mostly gutter — the waste this window already had
        /// removed once. Keeping the host at its own width and filling the rest with
        /// <see cref="CubePreviewStripElement"/> spends that space instead, and answers something the
        /// 3D picker never could: what the gradient looks like from the sides you are not facing.
        /// </remarks>
        private void BuildViewport(VisualElement viewportHost)
        {
            if (viewportHost == null)
                return;

            var cubeHost = new VisualElement { name = "cubeHost" };
            cubeHost.AddToClassList("abcw-cube-host");
            cube = new GradientCubeElement { name = "cube" };
            cubeHost.Add(cube);
            viewportHost.Add(cubeHost);

            cube.RegisterCallback<Gradient3DChangedEvent>(_ => OnWorkingChanged());
            cube.KeySelected += OnCubeKeySelected;

            var side = new VisualElement { name = "cubeSide" };
            side.AddToClassList("abcw-card");
            side.AddToClassList("abcw-cube-side");
            side.Add(new Label("RESULT") { pickingMode = PickingMode.Ignore, name = "resultCap" });
            side.Q<Label>("resultCap").AddToClassList("abcw-rail__cap");

            resultStrip = new CubePreviewStripElement { name = "resultStrip", IncludeModulation = true };
            side.Add(resultStrip);

            var note = new Label("The cube shows the base gradient. These show what it evaluates to.");
            note.AddToClassList("abcw-tab-pane__note");
            side.Add(note);
            viewportHost.Add(side);
        }

        private void BuildRail(VisualElement railHost)
        {
            if (railHost == null)
                return;

            var caption = new Label("EDITING");
            caption.AddToClassList("abcw-rail__cap");
            railHost.Add(caption);

            modeSelector = new KeyModeSelector { name = "modeSelector" };
            railHost.Add(modeSelector);
            modeSelector.ModeChanged += OnModeChanged;
            modeSelector.AddKeyRequested += AddKey;

            railHost.Add(new VisualElement { style = { flexGrow = 1 } });

            keyCount = new Label();
            keyCount.AddToClassList("abcw-rail__count");
            railHost.Add(keyCount);
        }

        private void BuildTabs(VisualElement tabHost)
        {
            if (tabHost == null)
                return;

            colorList = new KeyList3DElement(isAlpha: false);
            alphaList = new KeyList3DElement(isAlpha: true);
            alphaList.AddToClassList("abcw-list-card--spaced");
            colorList.Changed += OnWorkingChanged;
            alphaList.Changed += OnWorkingChanged;
            colorList.KeySelected += OnListKeySelected;
            alphaList.KeySelected += OnListKeySelected;

            var keysPane = new VisualElement { name = "keysPane" };
            keysPane.AddToClassList("abcw-tab-pane");
            keysPane.AddToClassList("abcw-tab-pane--row");
            keysPane.Add(colorList);
            keysPane.Add(alphaList);

            adjustPanel = new GradientAdjust3DPanel { name = "adjustPane" };
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
            rebuildPanel.AddTextureAction("textureScatter", "Scatter 16 keys",
                "Take a palette from the texture and scatter it through the cube. Reroll rearranges the "
                + "positions. (A 1xN strip has no 3D reading, so there is no strict mode.)", SampleTexture);
            rebuildPanel.AddMeshAction("sampleMesh", "Sample vertex colours",
                "Rebuild the gradient from the mesh's coloured vertices, using each vertex's position in "
                + "the mesh bounds.", SampleMesh);

            libraryPanel = new GradientLibrary3DPanel { GetCurrentGradient = () => working };
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

        private void SampleTexture()
        {
            if (ColorSampler3D.TrySampleTexture(rebuildPanel.Texture, ColorSampler3D.DefaultKeyCount,
                    rebuildPanel.Seed, out var sampled, out string error))
                AdoptWorking(sampled);
            else
                EditorUtility.DisplayDialog("Scatter", error, "OK");
        }

        private void SampleMesh()
        {
            if (ColorSampler3D.TrySampleMesh(rebuildPanel.Mesh, ColorSampler3D.DefaultKeyCount,
                    rebuildPanel.Seed, out var sampled, out string error))
                AdoptWorking(sampled);
            else
                EditorUtility.DisplayDialog("Sample Mesh", error, "OK");
        }

        private void RefreshAllViews()
        {
            if (cube == null || working == null)
                return;

            cube.Gradient = working;
            cube.AlphaMode = modeSelector.IsAlpha;
            colorList.SetGradient(working);
            alphaList.SetGradient(working);
            adjustPanel.SetGradient(working);
            modulationPanel.SetValueWithoutNotify(working.Modulation);
            resultStrip.Gradient = working;
            SyncAddKeyEnabled();
            SyncKeyCount();
        }

        private void OnModeChanged(bool isAlpha)
        {
            cube.AlphaMode = isAlpha;
            SyncAddKeyEnabled();
        }

        /// <summary>
        /// Points both lists at the key just grabbed in the cube, so the row carrying its coordinates is
        /// on screen and marked while it is being dragged.
        /// </summary>
        private void OnCubeKeySelected(int index, bool isAlpha)
        {
            colorList.SetSelected(isAlpha ? -1 : index);
            alphaList.SetSelected(isAlpha ? index : -1);
        }

        /// <summary>The same link the other way round: focusing a row rings its dot in the cube.</summary>
        private void OnListKeySelected(int index, bool isAlpha)
        {
            cube.SetSelected(index, isAlpha);
            OnCubeKeySelected(index, isAlpha);
        }

        /// <summary>
        /// Adds a key of the selected kind at the centre of the cube, taking its value from the gradient
        /// there — the 3D reading of what dropping a palette swatch onto the 1D bar does.
        /// </summary>
        private void AddKey()
        {
            if (working == null)
                return;

            if (modeSelector.IsAlpha)
            {
                if (working.CanAddAlphaKey)
                    working.AddAlphaKey(working.EvaluateBase(CubeCentre).a, CubeCentre);
            }
            else if (working.CanAddColorKey)
            {
                working.AddColorKey(working.EvaluateBase(CubeCentre), CubeCentre);
            }

            OnWorkingChanged();
        }

        private void SyncAddKeyEnabled() =>
            modeSelector.SetCanAddKey(working != null && (modeSelector.IsAlpha ? working.CanAddAlphaKey : working.CanAddColorKey));

        /// <summary>
        /// Takes on a gradient that arrived whole — loaded from the library, or rebuilt by sampling — in
        /// place of the one being edited.
        /// </summary>
        /// <remarks>
        /// Distinct from <see cref="OnWorkingChanged"/>, and the distinction is the whole point.
        /// That one announces an edit to the gradient the panels already hold, so re-reading values is
        /// enough. This one hands them a <em>different instance</em>, and every panel caches its own
        /// reference: <see cref="KeyList3DElement.Refresh"/> re-reads the gradient it was last given, and
        /// <see cref="GradientActions3DPanel"/> mutates the one it was last given. Announcing the swap
        /// without <see cref="RefreshAllViews"/> would leave both of them editing an object nobody would
        /// read again, which is exactly what the flat picker did until it was named there too.
        /// </remarks>
        private void AdoptWorking(GradientABCW3D replacement)
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
            cube.Gradient = working;
            colorList.Refresh();
            alphaList.Refresh();
            resultStrip.Gradient = working;
            SyncAddKeyEnabled();
            SyncKeyCount();
            RequestLivePreview();
        }

        /// <summary>
        /// The rail's running total. The key cap used to fail in silence: adding past it simply did
        /// nothing beyond disabling one button, with no count anywhere.
        /// </summary>
        private void SyncKeyCount()
        {
            if (keyCount == null || working == null)
                return;

            int used = working.ColorKeys.Length + working.AlphaKeys.Length;
            keyCount.text = $"{used} / {GradientABCW3D.MaxKeys * 2}";
        }

        /// <summary>
        /// Queues at most one live-preview push per editor frame.
        /// </summary>
        /// <remarks>
        /// Each push clones the whole gradient and, once the property drawer writes it through, costs a
        /// serialize and an undo record too. That is the same reason the 1D picker coalesces, and it
        /// applies more strongly here, where the clone carries twice as many keys.
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
