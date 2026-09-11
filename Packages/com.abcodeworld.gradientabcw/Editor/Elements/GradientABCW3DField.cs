using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace ABCodeworld.Gradients.Editor
{
    /// <summary>
    /// The inspector control for a <see cref="GradientABCW3D"/>: a clickable row of small renders of the
    /// final gradient, an Edit button that opens the 3D picker, a quick-actions menu, and a collapsible
    /// modulation section holding the modulation controls and the unmodulated base. Pure UI Toolkit — no
    /// IMGUI anywhere in the control.
    /// </summary>
    /// <remarks>
    /// Structurally the 3D reading of <see cref="GradientABCWField"/>, including its decision to lead with
    /// the <em>final</em>, modulated gradient rather than the base: the foldout is collapsed by default,
    /// so leading with the base means the one thing most users ever see is not what the object renders.
    /// <para>
    /// That argument is stronger here than in 1D, because 3D modulation is less legible from its numbers.
    /// Domain modulation applies to every axis at once, so a repeat count of 2 tiles the cube 2x2x2 —
    /// eight times over, not twice — and reverse mirrors all three axes, reflecting the gradient rather
    /// than rotating it. Neither is something a user can picture from a collapsed foldout.
    /// </para>
    /// <para>
    /// The one visible difference from the 1D field remains the swatch: a 1D gradient fits in a strip, a
    /// cube does not, so the swatch is four small renders chosen between them to show all six faces.
    /// </para>
    /// </remarks>
    public sealed class GradientABCW3DField : BindableElement, INotifyValueChanged<GradientABCW3D>
    {
        /// <summary>
        /// Opens the picker for a gradient/session pair and returns the window. Wired to
        /// <c>GradientPicker3DWindow.Open</c> at load time; tests may substitute a fake.
        /// </summary>
        public static Func<GradientABCW3D, GradientPicker3DSession, EditorWindow> PickerLauncher { get; set; }

        /// <summary>
        /// Raised just before <see cref="PickerLauncher"/> is invoked, with the session already populated
        /// with this field's default Changed/Accepted wiring. Lets callers that need extra session
        /// lifecycle behaviour (the property drawer's undo grouping) wrap the existing delegates without
        /// the field needing to know about them.
        /// </summary>
        public event Action<GradientABCW3D, GradientPicker3DSession> PickerOpening;

        /// <summary>
        /// Element names for the two preview strips, deliberately the same as
        /// <see cref="GradientABCWField"/>'s so that the two fields read alike and their tests query
        /// alike. Named rather than found by position, for the reason that field records: a positional
        /// lookup silently inverts what a test asserts the moment the two swap over.
        /// </summary>
        internal const string FinalPreviewName = "final-preview";

        internal const string BasePreviewName = "base-preview";

        /// <summary>Foldout heading when modulation is at identity or bypassed.</summary>
        internal const string ModulationLabel = "Modulation";

        /// <summary>
        /// Foldout heading when modulation actually changes the output, so a collapsed foldout still
        /// says why the cube above it looks the way it does.
        /// </summary>
        internal const string ModulationActiveLabel = "Modulation — active";

        private GradientABCW3D currentValue;

        private readonly CubePreviewStripElement basePreview;
        private readonly Foldout modulationFoldout;
        private readonly ModulationPanel modulationPanel;
        private readonly EnumField blendModeField;
        private readonly ResettableSliderRow falloffRow;
        private readonly CubePreviewStripElement finalPreview;

        public GradientABCW3D value
        {
            get => currentValue;
            set
            {
                if (ReferenceEquals(currentValue, value))
                {
                    RefreshDisplays();
                    return;
                }

                var previous = currentValue;
                using var evt = ChangeEvent<GradientABCW3D>.GetPooled(previous, value);
                evt.target = this;
                SetValueWithoutNotify(value);
                SendEvent(evt);
            }
        }

        public void SetValueWithoutNotify(GradientABCW3D newValue)
        {
            currentValue = newValue;
            RefreshDisplays();
        }

        public GradientABCW3DField(string label = "Gradient 3D")
        {
            AddToClassList("abcw-field");
            AddToClassList("abcw-field--3d");

            var fieldStyle = PackagePaths.Load<StyleSheet>("Editor/UI/GradientABCWField.uss");
            if (fieldStyle != null)
                styleSheets.Add(fieldStyle);

            var cubeStyle = PackagePaths.Load<StyleSheet>("Editor/UI/GradientABCW3DField.uss");
            if (cubeStyle != null)
                styleSheets.Add(cubeStyle);

            var header = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
            header.Add(new Label(label) { style = { minWidth = 120 } });

            finalPreview = new CubePreviewStripElement
            {
                name = FinalPreviewName,
                IncludeModulation = true,
                Clickable = true,
            };
            finalPreview.Clicked += OpenPicker;
            header.Add(finalPreview);

            header.Add(new VisualElement { style = { flexGrow = 1 } });
            header.Add(new Button(OpenPicker) { name = "edit", text = "Edit", tooltip = "Open the 3D gradient picker." });

            var menu = new ToolbarMenu { name = "actions", text = "⋯" };
            menu.menu.AppendAction("Sample from Selected Mesh", _ => SampleFromMesh());
            menu.menu.AppendSeparator();
            menu.menu.AppendAction("Flip Keys", _ => Mutate(g => g.FlipKeys(), Gradient3DChangedEvent.ChangeKind.Keys));
            menu.menu.AppendAction("Distribute Colour Keys", _ => Mutate(g => g.DistributeColorKeysEvenly(), Gradient3DChangedEvent.ChangeKind.Keys));
            menu.menu.AppendAction("Distribute Alpha Keys", _ => Mutate(g => g.DistributeAlphaKeysEvenly(), Gradient3DChangedEvent.ChangeKind.Keys));
            menu.menu.AppendSeparator();
            menu.menu.AppendAction("Reset to Default", _ => value = GradientABCW3D.CreateDefault());
            header.Add(menu);

            Add(header);

            modulationFoldout = new Foldout { name = "modulation", text = ModulationLabel, value = false, viewDataKey = "abcw-3d-modulation-foldout" };
            Add(modulationFoldout);

            blendModeField = new EnumField("Blend Mode", BlendMode.Smooth) { name = "blendMode" };
            blendModeField.RegisterValueChangedCallback(evt =>
                Mutate(g => g.BlendMode = (BlendMode)evt.newValue, Gradient3DChangedEvent.ChangeKind.BlendMode));
            modulationFoldout.Add(blendModeField);

            falloffRow = new ResettableSliderRow(
                "Falloff Power",
                GradientABCW3D.MinFalloffPower,
                GradientABCW3D.MaxFalloffPower,
                GradientABCW3D.DefaultFalloffPower)
            {
                name = "falloffPower",
                tooltip = "How sharply a key's influence falls off with distance. Higher pulls each point "
                        + "towards its nearest key. Smooth blending only — stepped blending always takes the nearest key.",
            };
            falloffRow.RegisterValueChangedCallback(evt =>
                Mutate(g => g.FalloffPower = evt.newValue, Gradient3DChangedEvent.ChangeKind.Falloff));
            modulationFoldout.Add(falloffRow);

            modulationPanel = new ModulationPanel();
            modulationPanel.RegisterValueChangedCallback(evt =>
                Mutate(g => g.Modulation = evt.newValue, Gradient3DChangedEvent.ChangeKind.Modulation));
            modulationFoldout.Add(modulationPanel);

            modulationFoldout.Add(new Label("Unmodulated Base") { style = { marginTop = 4 } });
            basePreview = new CubePreviewStripElement
            {
                name = BasePreviewName,
                IncludeModulation = false,
                tooltip = "Base gradient before modulation. Shown for comparison; the renders at the top are what this gradient actually evaluates to.",
            };
            modulationFoldout.Add(basePreview);

            SetValueWithoutNotify(GradientABCW3D.CreateDefault());
        }

        private void RefreshDisplays()
        {
            basePreview.Gradient = currentValue;
            finalPreview.Gradient = currentValue;
            if (currentValue == null)
                return;

            blendModeField.SetValueWithoutNotify(currentValue.BlendMode);
            falloffRow.SetValueWithoutNotify(currentValue.FalloffPower);
            modulationPanel.SetValueWithoutNotify(currentValue.Modulation);

            // Falloff has no meaning under stepped blending, where the nearest key wins outright. Leaving
            // it live would present a control that silently does nothing.
            bool falloffApplies = currentValue.BlendMode == BlendMode.Smooth;
            falloffRow.SetEnabled(falloffApplies);
            falloffRow.EnableInClassList("abcw-falloff-row--disabled", !falloffApplies);

            // IsEffective is exactly "modulation changes the output" — not bypassed and not at identity —
            // so the heading tracks the bypass toggle and a slider being returned to its default alike.
            modulationFoldout.text = currentValue.Modulation.IsEffective ? ModulationActiveLabel : ModulationLabel;
        }

        /// <summary>
        /// Applies an in-place edit and announces it with a <see cref="Gradient3DChangedEvent"/> only.
        /// </summary>
        /// <remarks>
        /// Deliberately not also a <c>ChangeEvent</c>. The 1D field used to raise both for one in-place
        /// edit, and listeners that reasonably handle both — the property drawer does — ran their
        /// write-through twice, serializing twice and leaving two undo records per change.
        /// <c>ChangeEvent</c> means what its name says: the field was pointed at a different gradient
        /// instance.
        /// </remarks>
        private void Mutate(Action<GradientABCW3D> mutation, Gradient3DChangedEvent.ChangeKind kind)
        {
            if (currentValue == null)
                return;

            mutation(currentValue);
            RefreshDisplays();

            using var changedEvt = Gradient3DChangedEvent.GetPooled(currentValue, kind);
            changedEvt.target = this;
            SendEvent(changedEvt);
        }

        /// <summary>
        /// Rebuilds the gradient from the selected mesh's coloured vertices, each vertex's position in the
        /// mesh's bounds becoming a key position.
        /// </summary>
        private void SampleFromMesh()
        {
            if (!MeshSelectionUtility.TryGetSelectedMesh(out var mesh, out string error))
            {
                EditorUtility.DisplayDialog("Sample From Mesh", error, "OK");
                return;
            }

            uint seed = unchecked((uint)Environment.TickCount) | 1u;
            if (!ColorSampler3D.TrySampleMesh(mesh, ColorSampler3D.DefaultKeyCount, seed, out var sampled, out string sampleError))
            {
                EditorUtility.DisplayDialog("Sample From Mesh", sampleError, "OK");
                return;
            }

            value = sampled;
        }

        private void OpenPicker()
        {
            if (currentValue == null || PickerLauncher == null)
                return;

            var session = new GradientPicker3DSession { LivePreview = GradientABCWSettings.instance.PickerLivePreview };
            session.Changed = g => value = g;
            session.Accepted = g => value = g;
            PickerOpening?.Invoke(currentValue, session);
            PickerLauncher(currentValue, session);
        }
    }
}
