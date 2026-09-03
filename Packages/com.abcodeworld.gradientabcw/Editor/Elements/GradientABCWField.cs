using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace ABCodeworld.Gradients.Editor
{
    /// <summary>
    /// The inspector control for a <see cref="GradientABCW"/>: a clickable base preview, an Edit button
    /// that opens the picker, a quick-actions menu, and a collapsible modulation section with its own
    /// final preview. Pure UI Toolkit — no IMGUI anywhere in the control.
    /// </summary>
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

            basePreview = new GradientPreviewElement
            {
                Mode = GradientPreviewElement.PreviewMode.Base,
                Clickable = true,
                tooltip = "Base gradient (ignores modulation). Click to open the picker.",
                style = { flexGrow = 1, height = 20, marginRight = 4 },
            };
            basePreview.Clicked += OpenPicker;
            header.Add(basePreview);

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

            modulationFoldout = new Foldout { text = "Modulation", value = false, viewDataKey = "abcw-modulation-foldout" };
            Add(modulationFoldout);

            blendModeField = new EnumField("Blend Mode", BlendMode.Smooth);
            blendModeField.RegisterValueChangedCallback(evt =>
                Mutate(g => g.BlendMode = (BlendMode)evt.newValue, GradientChangedEvent.ChangeKind.BlendMode));
            modulationFoldout.Add(blendModeField);

            modulationPanel = new ModulationPanel();
            modulationPanel.RegisterValueChangedCallback(evt =>
                Mutate(g => g.Modulation = evt.newValue, GradientChangedEvent.ChangeKind.Modulation));
            modulationFoldout.Add(modulationPanel);

            modulationFoldout.Add(new Label("Final Preview") { style = { marginTop = 4 } });
            finalPreview = new GradientPreviewElement { Mode = GradientPreviewElement.PreviewMode.Final, style = { height = 20 } };
            modulationFoldout.Add(finalPreview);

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
        }

        private void Mutate(Action<GradientABCW> mutation, GradientChangedEvent.ChangeKind kind)
        {
            if (currentValue == null)
                return;

            mutation(currentValue);
            RefreshDisplays();

            using var changedEvt = GradientChangedEvent.GetPooled(currentValue, kind);
            changedEvt.target = this;
            SendEvent(changedEvt);

            using var valueEvt = ChangeEvent<GradientABCW>.GetPooled(currentValue, currentValue);
            valueEvt.target = this;
            SendEvent(valueEvt);
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
