using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace ABCodeworld.Gradients.Editor
{
    [CustomPropertyDrawer(typeof(GradientABCW))]
    public sealed class GradientABCWPropertyDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            if (property.hasMultipleDifferentValues)
            {
                var multiValue = new Label($"{property.displayName} — Multiple Values");
                multiValue.SetEnabled(false);
                return multiValue;
            }

            var field = new GradientABCWField(property.displayName);
            field.SetValueWithoutNotify((GradientABCW)property.boxedValue);

            void WriteThrough()
            {
                property.serializedObject.Update();
                property.boxedValue = field.value;
                property.serializedObject.ApplyModifiedProperties();
            }

            field.RegisterValueChangedCallback(_ => WriteThrough());
            field.RegisterCallback<GradientChangedEvent>(_ => WriteThrough());

            // Follow external changes (undo/redo, another inspector) back into the field.
            field.TrackPropertyValue(property, p => field.SetValueWithoutNotify((GradientABCW)p.boxedValue));

            field.PickerOpening += (_, session) =>
            {
                Undo.IncrementCurrentGroup();
                Undo.SetCurrentGroupName("Edit Gradient");
                int group = Undo.GetCurrentGroup();

                var innerAccepted = session.Accepted;
                session.Accepted = g =>
                {
                    innerAccepted?.Invoke(g);
                    Undo.CollapseUndoOperations(group);
                };

                var innerCancelled = session.Cancelled;
                session.Cancelled = () =>
                {
                    Undo.RevertAllDownToGroup(group);
                    field.SetValueWithoutNotify((GradientABCW)property.boxedValue);
                    innerCancelled?.Invoke();
                };
            };

            return field;
        }
    }
}
