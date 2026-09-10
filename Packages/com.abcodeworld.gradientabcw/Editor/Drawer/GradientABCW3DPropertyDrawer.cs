using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace ABCodeworld.Gradients.Editor
{
    /// <summary>
    /// Draws a serialized <see cref="GradientABCW3D"/> as a <see cref="GradientABCW3DField"/>.
    /// </summary>
    /// <remarks>
    /// <c>CreatePropertyGUI</c> only, like its 1D counterpart, so it renders in UI Toolkit inspectors. An
    /// inspector still using <c>OnInspectorGUI</c> will not draw a <c>CreatePropertyGUI</c>-only drawer's
    /// content at all.
    /// </remarks>
    [CustomPropertyDrawer(typeof(GradientABCW3D))]
    public sealed class GradientABCW3DPropertyDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            if (property.hasMultipleDifferentValues)
            {
                var multiValue = new Label($"{property.displayName} — Multiple Values");
                multiValue.SetEnabled(false);
                return multiValue;
            }

            var field = new GradientABCW3DField(property.displayName);
            field.SetValueWithoutNotify((GradientABCW3D)property.boxedValue);

            void WriteThrough()
            {
                property.serializedObject.Update();
                property.boxedValue = field.value;
                property.serializedObject.ApplyModifiedProperties();
            }

            field.RegisterValueChangedCallback(_ => WriteThrough());
            field.RegisterCallback<Gradient3DChangedEvent>(_ => WriteThrough());

            // Follow external changes (undo/redo, another inspector) back into the field.
            field.TrackPropertyValue(property, p => field.SetValueWithoutNotify((GradientABCW3D)p.boxedValue));

            field.PickerOpening += (_, session) =>
            {
                var undo = UndoGroupScope.Begin("Edit 3D Gradient");

                var innerAccepted = session.Accepted;
                session.Accepted = g =>
                {
                    innerAccepted?.Invoke(g);
                    undo.Collapse();
                };

                var innerCancelled = session.Cancelled;
                session.Cancelled = () =>
                {
                    undo.RevertDownTo();
                    field.SetValueWithoutNotify((GradientABCW3D)property.boxedValue);
                    innerCancelled?.Invoke();
                };
            };

            return field;
        }
    }
}
