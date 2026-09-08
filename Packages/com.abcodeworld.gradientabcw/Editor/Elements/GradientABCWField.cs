using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace ABCodeworld.Gradients.Editor
{
    /// <summary>
    /// The inspector control for a <see cref="GradientABCW"/>: a clickable preview of the final gradient,
    /// an Edit button that opens the picker, a quick-actions menu, and a collapsible modulation section
    /// holding the modulation controls and the unmodulated base gradient. Pure UI Toolkit — no IMGUI
    /// anywhere in the control.
    /// </summary>
    /// <remarks>
    /// The top preview deliberately shows the <em>final</em>, modulated gradient rather than the base.
    /// The modulation foldout is collapsed by default, so leading with the base meant the one thing most
    /// users ever saw was a gradient that is not what the object renders — a hue shift or a repeat count
    /// was invisible until you knew to expand a section you had no reason to suspect. The base is still
    /// available, at the end of the foldout, where it is useful for comparison while you are actually
    /// editing modulation.
    /// </remarks>
    public sealed class GradientABCWField : BindableElement, INotifyValueChanged<GradientABCW>
    {
        /// <summary>
        /// Opens the picker for a gradient/session pair and returns the window. Wired to
        /// <see cref="GradientPickerWindow.Open"/> once that type exists; tests may substitute a fake.
        /// </summary>
        public static Func<GradientABCW, GradientPickerSession, EditorWindow> PickerLauncher { get; set; }

        /// <summary>
        /// Raised just before <see cref="PickerLauncher"/> is invoked, with the session already populated
        /// with this field's default Changed/Accepted wiring. Lets callers that need extra session
        /// lifecycle behaviour (the property drawer's undo grouping) wrap the existing delegates without
        /// the field needing to know about them.
        /// </summary>
        public event Action<GradientABCW, GradientPickerSession> PickerOpening;

        /// <summary>
        /// Element names for the two preview strips.
        /// </summary>
        /// <remarks>
        /// Named rather than found by position. Tests used to reach these with First()/Last() over the
        /// element tree, which meant swapping their order silently inverted what those tests asserted
        /// while leaving them green.
        /// </remarks>
        internal const string FinalPreviewName = "final-preview";

        internal const string BasePreviewName = "base-preview";

        /// <summary>Foldout heading when modulation is at identity or bypassed.</summary>
        internal const string ModulationLabel = "Modulation";

        /// <summary>
        /// Foldout heading when modulation actually changes the output, so a collapsed foldout still
        /// says why the gradient above it looks the way it does.
        /// </summary>
        internal const string ModulationActiveLabel = "Modulation — active";

        private GradientABCW currentValue;

        private readonly GradientPreviewElement basePreview;
        private readonly Foldout modulationFoldout;
        private readonly ModulationPanel modulationPanel;
        private readonly EnumField blendModeField;
        private readonly GradientPreviewElement finalPreview;

        public GradientABCW value
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
                using var evt = ChangeEvent<GradientABCW>.GetPooled(previous, value);
                evt.target = this;
                SetValueWithoutNotify(value);
                SendEvent(evt);
            }
        }

        public void SetValueWithoutNotify(GradientABCW newValue)
        {
            currentValue = newValue;
            RefreshDisplays();
        }

        public GradientABCWField(string label = "Gradient")
        {
            AddToClassList("abcw-field");
            var styleSheet = PackagePaths.Load<StyleSheet>("Editor/UI/GradientABCWField.uss");
            if (styleSheet != null)
                styleSheets.Add(styleSheet);

            var header = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
            header.Add(new Label(label) { style = { minWidth = 120 } });

            finalPreview = new GradientPreviewElement
            {
                name = FinalPreviewName,
                Mode = GradientPreviewElement.PreviewMode.Final,
                Clickable = true,
                tooltip = "Final gradient, including modulation. Click to open the picker.",
                style = { flexGrow = 1, height = 20, marginRight = 4 },
            };
            finalPreview.Clicked += OpenPicker;
            header.Add(finalPreview);

            var editButton = new Button(OpenPicker) { text = "Edit" };
            header.Add(editButton);

            var menu = new ToolbarMenu { text = "⋯" }; // ⋯
            menu.menu.AppendAction("Sample from Selected Mesh", _ => SampleFromMesh());
            menu.menu.AppendSeparator();
            menu.menu.AppendAction("Flip Keys", _ => Mutate(g => g.FlipKeys(), GradientChangedEvent.ChangeKind.Keys));
            menu.menu.AppendAction("Distribute Colour Keys", _ => Mutate(g => g.DistributeColorKeysEvenly(), GradientChangedEvent.ChangeKind.Keys));
            menu.menu.AppendAction("Distribute Alpha Keys", _ => Mutate(g => g.DistributeAlphaKeysEvenly(), GradientChangedEvent.ChangeKind.Keys));
            menu.menu.AppendSeparator();
            menu.menu.AppendAction("Reset to Default", _ => value = GradientABCW.CreateDefault());
            header.Add(menu);

            Add(header);

            modulationFoldout = new Foldout { text = ModulationLabel, value = false, viewDataKey = "abcw-modulation-foldout" };
            Add(modulationFoldout);

            blendModeField = new EnumField("Blend Mode", BlendMode.Smooth);
            blendModeField.RegisterValueChangedCallback(evt =>
                Mutate(g => g.BlendMode = (BlendMode)evt.newValue, GradientChangedEvent.ChangeKind.BlendMode));
            modulationFoldout.Add(blendModeField);

            modulationPanel = new ModulationPanel();
            modulationPanel.RegisterValueChangedCallback(evt =>
                Mutate(g => g.Modulation = evt.newValue, GradientChangedEvent.ChangeKind.Modulation));
            modulationFoldout.Add(modulationPanel);

            modulationFoldout.Add(new Label("Unmodulated Base") { style = { marginTop = 4 } });
            basePreview = new GradientPreviewElement
            {
                name = BasePreviewName,
                Mode = GradientPreviewElement.PreviewMode.Base,
                tooltip = "Base gradient before modulation. Shown for comparison; the strip at the top is what this gradient actually evaluates to.",
                style = { height = 20 },
            };
            modulationFoldout.Add(basePreview);

            SetValueWithoutNotify(GradientABCW.CreateDefault());
        }

        private void RefreshDisplays()
        {
            basePreview.Gradient = currentValue;
            finalPreview.Gradient = currentValue;
            if (currentValue == null)
                return;

            blendModeField.SetValueWithoutNotify(currentValue.BlendMode);
            modulationPanel.SetValueWithoutNotify(currentValue.Modulation);

            // IsEffective is exactly "modulation changes the output" — not bypassed and not at identity —
            // so the heading tracks the bypass toggle and a slider being returned to its default alike.
            modulationFoldout.text = currentValue.Modulation.IsEffective ? ModulationActiveLabel : ModulationLabel;
        }

        /// <summary>
        /// Applies an in-place edit and announces it with a <see cref="GradientChangedEvent"/> only.
        /// </summary>
        /// <remarks>
        /// This used to also raise a <c>ChangeEvent</c> carrying the same instance as both previous and
        /// new value. Listeners that (reasonably) handle both events — the property drawer does — then ran
        /// their write-through twice for one edit, serializing twice and leaving two undo records per
        /// change. <c>ChangeEvent</c> now means what its name says: the field was pointed at a different
        /// gradient instance. In-place edits are <see cref="GradientChangedEvent"/>.
        /// </remarks>
        private void Mutate(Action<GradientABCW> mutation, GradientChangedEvent.ChangeKind kind)
        {
            if (currentValue == null)
                return;

            mutation(currentValue);
            RefreshDisplays();

            using var changedEvt = GradientChangedEvent.GetPooled(currentValue, kind);
            changedEvt.target = this;
            SendEvent(changedEvt);
        }

        private void OpenPicker()
        {
            if (currentValue == null || PickerLauncher == null)
                return;

            var session = new GradientPickerSession { LivePreview = GradientABCWSettings.instance.PickerLivePreview };
            session.Changed = g => value = g;
            session.Accepted = g => value = g;
            PickerOpening?.Invoke(currentValue, session);
            PickerLauncher(currentValue, session);
        }

        private void SampleFromMesh()
        {
            if (!MeshSelectionUtility.TryGetSelectedMesh(out var mesh, out string error))
            {
                EditorUtility.DisplayDialog("Sample From Mesh", error, "OK");
                return;
            }

            var source = new MeshColorSource(mesh);
            uint seed = unchecked((uint)Environment.TickCount) | 1u;
            if (!ColorSampler.TrySampleRandom(source, 16, seed, out var sampled, out string sampleError))
            {
                EditorUtility.DisplayDialog("Sample From Mesh", sampleError, "OK");
                return;
            }

            value = sampled;
        }
    }
}
