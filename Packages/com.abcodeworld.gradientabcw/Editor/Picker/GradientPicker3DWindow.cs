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
        private GradientActions3DPanel actionsPanel;
        private IVisualElementScheduledItem pendingLivePreview;

        /// <summary>The gradient being edited (a clone of what the window was opened with).</summary>
        public GradientABCW3D Working => working;

        public static GradientPicker3DWindow Open(GradientABCW3D initial, GradientPicker3DSession session, string title = "3D Gradient Picker")
        {
            var window = CreateInstance<GradientPicker3DWindow>();
            window.titleContent = new GUIContent(title);

            // Wider than the 1D picker: a key row carries three coordinate fields where that one carries a
            // single time, and three of those rows sit side by side.
            window.minSize = new Vector2(880, 600);
            EditorWindowPlacement.CenterOnMainWindow(window, 960, 720);
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
            var uxml = PackagePaths.Load<VisualTreeAsset>("Editor/UI/GradientPicker3DWindow.uxml");
            var pickerUss = PackagePaths.Load<StyleSheet>("Editor/UI/GradientPickerWindow.uss");
            var barUss = PackagePaths.Load<StyleSheet>("Editor/UI/GradientBar.uss");
            var cubeUss = PackagePaths.Load<StyleSheet>("Editor/UI/GradientCube.uss");
            var fieldUss = PackagePaths.Load<StyleSheet>("Editor/UI/GradientABCWField.uss");
            var cubeFieldUss = PackagePaths.Load<StyleSheet>("Editor/UI/GradientABCW3DField.uss");

            rootVisualElement.Clear();
            if (pickerUss != null) rootVisualElement.styleSheets.Add(pickerUss);
            if (barUss != null) rootVisualElement.styleSheets.Add(barUss);
            if (fieldUss != null) rootVisualElement.styleSheets.Add(fieldUss);
            if (cubeFieldUss != null) rootVisualElement.styleSheets.Add(cubeFieldUss);
            if (cubeUss != null) rootVisualElement.styleSheets.Add(cubeUss);

            if (uxml != null)
                uxml.CloneTree(rootVisualElement);
            else
                rootVisualElement.Add(new Label("GradientPicker3DWindow: UXML not found"));

            rootVisualElement.AddToClassList("abcw-picker");
            rootVisualElement.style.flexGrow = 1;

            var cubeHost = rootVisualElement.Q<VisualElement>("cubeHost");
            var modeHost = rootVisualElement.Q<VisualElement>("modeHost");
            var colorListHost = rootVisualElement.Q<VisualElement>("colorListHost");
            var alphaListHost = rootVisualElement.Q<VisualElement>("alphaListHost");
            var libraryHost = rootVisualElement.Q<VisualElement>("libraryHost");
            var actionsHost = rootVisualElement.Q<VisualElement>("actionsHost");
            var okButton = rootVisualElement.Q<Button>("okBtn");
            var cancelButton = rootVisualElement.Q<Button>("cancelBtn");

            cube = new GradientCubeElement { name = "cube" };
            cubeHost?.Add(cube);
            cube.RegisterCallback<Gradient3DChangedEvent>(_ => OnWorkingChanged());
            cube.KeySelected += OnCubeKeySelected;

            modeSelector = new KeyModeSelector { name = "modeSelector" };
            modeHost?.Add(modeSelector);
            modeSelector.ModeChanged += OnModeChanged;
            modeSelector.AddKeyRequested += AddKey;

            colorList = new KeyList3DElement(isAlpha: false);
            alphaList = new KeyList3DElement(isAlpha: true);
            colorListHost?.Add(colorList);
            alphaListHost?.Add(alphaList);
            colorList.Changed += OnWorkingChanged;
            alphaList.Changed += OnWorkingChanged;
            colorList.KeySelected += OnListKeySelected;
            alphaList.KeySelected += OnListKeySelected;

            libraryPanel = new GradientLibrary3DPanel { GetCurrentGradient = () => working };
            libraryHost?.Add(libraryPanel);
            libraryPanel.Loaded += g => { working = g; RefreshAllViews(); OnWorkingChanged(); };

            actionsPanel = new GradientActions3DPanel();
            actionsHost?.Add(actionsPanel);
            actionsPanel.Changed += OnWorkingChanged;
            actionsPanel.Sampled += g => { working = g; RefreshAllViews(); OnWorkingChanged(); };

            okButton?.RegisterCallback<ClickEvent>(_ => Accept());
            cancelButton?.RegisterCallback<ClickEvent>(_ => Cancel());

            RefreshAllViews();
        }

        private void RefreshAllViews()
        {
            if (cube == null || working == null)
                return;

            cube.Gradient = working;
            cube.AlphaMode = modeSelector.IsAlpha;
            colorList.SetGradient(working);
            alphaList.SetGradient(working);
            actionsPanel.SetGradient(working);
            SyncAddKeyEnabled();
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

        private void OnWorkingChanged()
        {
            cube.Gradient = working;
            colorList.Refresh();
            alphaList.Refresh();
            SyncAddKeyEnabled();
            RequestLivePreview();
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
