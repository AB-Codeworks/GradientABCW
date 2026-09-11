using System;
using System.Collections.Generic;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace ABCodeworld.Gradients.Editor
{
    /// <summary>
    /// A virtualised list of colour or alpha key rows for a 3D gradient. One class serves both key kinds —
    /// the row layout (colour field vs. alpha slider) is the only thing that differs.
    /// </summary>
    /// <remarks>
    /// Separate from <see cref="KeyListElement"/> rather than a generalisation of it: a 1D row edits one
    /// time, a 3D row edits three coordinates, and the fields are named <c>x</c>, <c>y</c> and <c>z</c>
    /// rather than <c>time</c> so a UI test can tell the two windows' rows apart.
    /// <para>
    /// It does keep that class's two measured optimisations: a per-row context holding direct references
    /// to the row's controls, because looking them up with <c>row.Q&lt;T&gt;()</c> on every bind cost
    /// several tree queries per visible row per refresh; and rebinding rather than rebuilding when only
    /// values changed.
    /// </para>
    /// <para>
    /// What it drops is the re-sort after an edit. The 1D list has to refresh itself when a time changes,
    /// because that can reorder the array and move the key out from under the row. Positions in a cube
    /// have no order, so a key's index is fixed for as long as it exists.
    /// </para>
    /// </remarks>
    internal sealed class KeyList3DElement : VisualElement
    {
        /// <summary>
        /// Preferred width of one axis field, used as its flex basis rather than as a fixed size: the
        /// three of them grow to share a wide column and shrink to fit a narrow one, down to
        /// <see cref="AxisFieldMinWidth"/>.
        /// </summary>
        private const float AxisFieldWidth = 58f;

        /// <summary>Narrowest an axis field may get and still show <c>0.000</c> after its label.</summary>
        private const float AxisFieldMinWidth = 44f;

        private sealed class RowContext
        {
            public int KeyIndex;
            public FloatField X;
            public FloatField Y;
            public FloatField Z;
            public Slider Alpha;
            public ColorField Color;
        }

        /// <summary>Class marking the row of the key selected in the cube.</summary>
        private const string SelectedRowClass = "abcw-key-list__row--selected";

        private readonly bool isAlpha;
        private readonly ListView listView;
        private readonly List<int> indices = new();

        // Held rather than converted from a method group each time the mark moves, which a click into a
        // row does.
        private readonly Action<VisualElement> markRow;

        private GradientABCW3D gradient;
        private int selectedIndex = -1;

        public event Action<int, bool> KeySelected;
        public event Action Changed;

        public KeyList3DElement(bool isAlpha)
        {
            this.isAlpha = isAlpha;
            markRow = MarkRow;
            AddToClassList("abcw-key-list");

            listView = new ListView
            {
                name = isAlpha ? "alphaKeyList" : "colorKeyList",
                fixedItemHeight = 22,
                makeItem = MakeItem,
                bindItem = BindItem,
                selectionType = SelectionType.None,
                itemsSource = indices,
                horizontalScrollingEnabled = false,
            };
            listView.style.flexGrow = 1;
            Add(listView);
        }

        public void SetGradient(GradientABCW3D value)
        {
            gradient = value;
            Refresh();
        }

        /// <summary>
        /// Marks the row for <paramref name="index"/> and brings it into view, or clears the mark with a
        /// negative index. Called when a key is grabbed in the cube, so the row carrying its coordinates
        /// is the one on screen rather than one scroll away.
        /// </summary>
        public void SetSelected(int index)
        {
            if (selectedIndex == index)
                return;

            selectedIndex = index;

            if (index >= 0 && index < indices.Count)
                listView.ScrollToItem(index);

            // Marked in place rather than through RefreshItems. Focusing a row is one of the things that
            // moves the mark, and rebinding a row as the pointer lands in one of its fields would reset
            // the text under the caret.
            listView.Query(className: "abcw-key-list__row").ForEach(markRow);
        }

        /// <summary>Applies the mark to one already-bound row, from the key index its context holds.</summary>
        private void MarkRow(VisualElement row)
        {
            if (row.userData is RowContext context)
                row.EnableInClassList(SelectedRowClass, context.KeyIndex == selectedIndex);
        }

        /// <summary>
        /// Re-reads key values into the visible rows, rebuilding the row elements only when the number of
        /// keys actually changed.
        /// </summary>
        public void Refresh()
        {
            if (gradient == null)
                return;

            int count = isAlpha ? gradient.AlphaKeys.Length : gradient.ColorKeys.Length;

            if (indices.Count != count)
            {
                // A key added or removed shifts every later index, so the mark no longer names what it
                // did. The cube drops its selection on the same reasoning.
                selectedIndex = -1;

                indices.Clear();
                for (int i = 0; i < count; i++)
                    indices.Add(i);

                listView.Rebuild();
                return;
            }

            listView.RefreshItems();
        }

        private VisualElement MakeItem()
        {
            var context = new RowContext();
            var row = new VisualElement { userData = context };
            row.AddToClassList("abcw-key-list__row");
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;

            if (isAlpha)
            {
                var slider = new Slider(0f, 1f) { name = "value" };
                slider.style.flexGrow = 1;
                slider.style.minWidth = 50;
                slider.style.marginRight = 4;
                slider.RegisterValueChangedCallback(evt =>
                {
                    int i = context.KeyIndex;
                    gradient.SetAlphaKey(i, new AlphaKey3D(evt.newValue, gradient.AlphaKeys[i].position));
                    NotifyChanged();
                });
                context.Alpha = slider;
                row.Add(slider);
                row.RegisterCallback<FocusInEvent>(_ => KeySelected?.Invoke(context.KeyIndex, true));
            }
            else
            {
                var colorField = new ColorField { name = "value", showAlpha = false, showEyeDropper = true };
                colorField.style.width = 60;
                colorField.style.flexShrink = 0;
                colorField.style.marginRight = 4;
                colorField.RegisterValueChangedCallback(evt =>
                {
                    int i = context.KeyIndex;
                    gradient.SetColorKey(i, new ColorKey3D(evt.newValue, gradient.ColorKeys[i].position));
                    NotifyChanged();
                });
                context.Color = colorField;
                row.Add(colorField);
                row.RegisterCallback<FocusInEvent>(_ => KeySelected?.Invoke(context.KeyIndex, false));
            }

            context.X = AddAxisField(row, "x", context, 0);
            context.Y = AddAxisField(row, "y", context, 1);
            context.Z = AddAxisField(row, "z", context, 2);

            var deleteButton = new Button(() =>
            {
                int i = context.KeyIndex;
                bool removed = isAlpha ? gradient.RemoveAlphaKey(i) : gradient.RemoveColorKey(i);
                if (removed)
                {
                    Refresh();
                    NotifyChanged();
                }
            })
            { name = "delete", tooltip = "Delete" };
            deleteButton.AddToClassList("abcw-key-list__delete");
            deleteButton.style.width = 22;
            deleteButton.style.height = 20;
            deleteButton.style.flexShrink = 0;
            deleteButton.style.marginLeft = 4;
            var icon = EditorIcons.Delete;
            if (icon != null)
                deleteButton.Add(new Image { image = icon, pickingMode = PickingMode.Ignore, scaleMode = ScaleMode.ScaleToFit });
            row.Add(deleteButton);

            return row;
        }

        private FloatField AddAxisField(VisualElement row, string axisName, RowContext context, int axis)
        {
            var field = new FloatField(axisName) { name = axisName, isDelayed = true, formatString = "0.000" };
            field.style.width = AxisFieldWidth;
            field.style.minWidth = AxisFieldMinWidth;
            field.style.flexGrow = 1;
            FieldLabels.PinLabel(field.labelElement, FieldLabels.SingleCharacterWidth);
            field.RegisterValueChangedCallback(evt => SetAxis(context.KeyIndex, axis, evt.newValue));
            row.Add(field);
            return field;
        }

        private void SetAxis(int index, int axis, float value)
        {
            if (isAlpha)
            {
                var key = gradient.AlphaKeys[index];
                gradient.SetAlphaKey(index, new AlphaKey3D(key.alpha, WithAxis(key.position, axis, value)));
            }
            else
            {
                var key = gradient.ColorKeys[index];
                gradient.SetColorKey(index, new ColorKey3D(key.color, WithAxis(key.position, axis, value)));
            }

            // Re-bound rather than only notifying, so a value the key clamped shows the clamped number.
            Refresh();
            NotifyChanged();
        }

        private static Vector3 WithAxis(Vector3 position, int axis, float value)
        {
            position[axis] = value;
            return position;
        }

        private void BindItem(VisualElement row, int listIndex)
        {
            var context = (RowContext)row.userData;
            context.KeyIndex = indices[listIndex];
            int keyIndex = context.KeyIndex;

            Vector3 position;
            if (isAlpha)
            {
                var key = gradient.AlphaKeys[keyIndex];
                position = key.position;
                context.Alpha.SetValueWithoutNotify(key.alpha);
            }
            else
            {
                var key = gradient.ColorKeys[keyIndex];
                position = key.position;
                context.Color.SetValueWithoutNotify(key.color);
            }

            context.X.SetValueWithoutNotify(position.x);
            context.Y.SetValueWithoutNotify(position.y);
            context.Z.SetValueWithoutNotify(position.z);

            row.EnableInClassList(SelectedRowClass, keyIndex == selectedIndex);
        }

        private void NotifyChanged() => Changed?.Invoke();
    }
}
