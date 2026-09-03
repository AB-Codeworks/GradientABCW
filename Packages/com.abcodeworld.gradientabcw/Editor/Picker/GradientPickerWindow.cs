using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ABCodeworld.Gradients.Editor
{
    /// <summary>
    /// A pure UI Toolkit gradient editor window: bar, palette, key lists, an asset library, and
    /// mesh/texture sampling. Edits a working clone of the gradient it was opened with and reports
    /// back through the <see cref="GradientPickerSession"/> supplied to <see cref="Open"/>.
    /// </summary>
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
        private GradientActionsPanel actionsPanel;

        /// <summary>The gradient being edited (a clone of what the window was opened with).</summary>
        public GradientABCW Working => working;

        public static GradientPickerWindow Open(GradientABCW initial, GradientPickerSession session, string title = "Gradient Picker")
        {
            var window = CreateInstance<GradientPickerWindow>();
            window.titleContent = new GUIContent(title);
            window.minSize = new Vector2(780, 540);
            CenterOnMainWindow(window, 820, 640);
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
            var uxml = PackagePaths.Load<VisualTreeAsset>("Editor/UI/GradientPickerWindow.uxml");
            var pickerUss = PackagePaths.Load<StyleSheet>("Editor/UI/GradientPickerWindow.uss");
            var barUss = PackagePaths.Load<StyleSheet>("Editor/UI/GradientBar.uss");
            var fieldUss = PackagePaths.Load<StyleSheet>("Editor/UI/GradientABCWField.uss");

            rootVisualElement.Clear();
            if (pickerUss != null) rootVisualElement.styleSheets.Add(pickerUss);
            if (barUss != null) rootVisualElement.styleSheets.Add(barUss);
            if (fieldUss != null) rootVisualElement.styleSheets.Add(fieldUss);

            if (uxml != null)
                uxml.CloneTree(rootVisualElement);
            else
                rootVisualElement.Add(new Label("GradientPickerWindow: UXML not found"));

            rootVisualElement.AddToClassList("abcw-picker");
            rootVisualElement.style.flexGrow = 1;

            var barHost = rootVisualElement.Q<VisualElement>("barHost");
            var paletteHost = rootVisualElement.Q<VisualElement>("palette");
            var colorListHost = rootVisualElement.Q<VisualElement>("colorListHost");
            var alphaListHost = rootVisualElement.Q<VisualElement>("alphaListHost");
            var libraryHost = rootVisualElement.Q<VisualElement>("libraryHost");
            var actionsHost = rootVisualElement.Q<VisualElement>("actionsHost");
            var okButton = rootVisualElement.Q<Button>("okBtn");
            var cancelButton = rootVisualElement.Q<Button>("cancelBtn");

            bar = new GradientBarElement();
            barHost?.Add(bar);
            bar.RegisterCallback<GradientChangedEvent>(_ => OnWorkingChanged());

            alphaPalette = new KeyPaletteDraggable(true);
            colorPalette = new KeyPaletteDraggable(false);
            paletteHost?.Add(alphaPalette);
            paletteHost?.Add(colorPalette);
            alphaPalette.Dragging += OnPaletteDragging;
            colorPalette.Dragging += OnPaletteDragging;
            alphaPalette.Dropped += OnPaletteDropped;
            colorPalette.Dropped += OnPaletteDropped;

            colorList = new KeyListElement(isAlpha: false);
            alphaList = new KeyListElement(isAlpha: true);
            colorListHost?.Add(colorList);
            alphaListHost?.Add(alphaList);
            colorList.Changed += OnWorkingChanged;
            alphaList.Changed += OnWorkingChanged;

            libraryPanel = new GradientLibraryPanel { GetCurrentGradient = () => working };
            libraryHost?.Add(libraryPanel);
            libraryPanel.Loaded += g => { working = g; OnWorkingChanged(); };

            actionsPanel = new GradientActionsPanel();
            actionsHost?.Add(actionsPanel);
            actionsPanel.Changed += OnWorkingChanged;
            actionsPanel.Sampled += g => { working = g; OnWorkingChanged(); };

            okButton?.RegisterCallback<ClickEvent>(_ => Accept());
            cancelButton?.RegisterCallback<ClickEvent>(_ => Cancel());

            RefreshAllViews();
        }

        private void RefreshAllViews()
        {
            if (bar == null || working == null)
                return;

            bar.Gradient = working;
            colorList.SetGradient(working);
            alphaList.SetGradient(working);
            actionsPanel.SetGradient(working);
            colorPalette.SetColor(working.ColorKeys.Length > 0 ? working.ColorKeys[0].color : Color.white);
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

        private void OnWorkingChanged()
        {
            bar.Gradient = working;
            colorList.Refresh();
            alphaList.Refresh();

            if (session != null && session.LivePreview)
                session.Changed?.Invoke(working.Clone());
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

        private static void CenterOnMainWindow(EditorWindow window, int width, int height)
        {
            var main = EditorGUIUtility.GetMainWindowPosition();
            window.position = new Rect(
                main.x + (main.width - width) * 0.5f,
                main.y + (main.height - height) * 0.5f,
                width, height);
        }
    }
}
