using System;
using System.Collections.Generic;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace ABCodeworld.Gradients.Editor
{
    /// <summary>
    /// A virtualised list of colour or alpha key rows. One class serves both key kinds — the row
    /// layout (colour field vs. alpha slider) is the only thing that differs.
    /// </summary>
    internal sealed class KeyListElement : VisualElement
    {
        /// <summary>
        /// Per-row state, including direct references to the row's controls. Looking them up with
        /// <c>row.Q&lt;T&gt;()</c> on every bind meant three tree queries per visible row per refresh, and a
        /// refresh happens on every pointer-move of a drag.
        /// </summary>
        private sealed class RowContext
        {
            public int KeyIndex;
            public FloatField Time;
            public Slider Alpha;
            public ColorField Color;
        }

        private readonly bool isAlpha;
        private readonly ListView listView;
        private readonly List<int> indices = new();
        private GradientABCW gradient;

        public event Action<int, bool> KeySelected;
        public event Action Changed;

        public KeyListElement(bool isAlpha)
        {
            this.isAlpha = isAlpha;
            AddToClassList("abcw-key-list");

            listView = new ListView
            {
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

        public void SetGradient(GradientABCW value)
        {
            gradient = value;
            Refresh();
        }

        /// <summary>
        /// Re-reads key values into the visible rows, rebuilding the row elements only when the number of
        /// keys actually changed.
        /// </summary>
        /// <remarks>
        /// This used to call <c>Rebuild()</c> unconditionally, which tears down and recreates every row
        /// element, defeating the ListView's own pooling. Dragging a key in the picker calls this on both
        /// lists on every pointer-move, so the common case — same key count, changed values — now takes
        /// the cheap path.
        /// </remarks>
        public void Refresh()
        {
            if (gradient == null)
                return;

            int count = isAlpha ? gradient.AlphaKeys.Length : gradient.ColorKeys.Length;

            if (indices.Count != count)
            {
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
                slider.style.marginRight = 4;
                slider.RegisterValueChangedCallback(evt =>
                {
                    int i = context.KeyIndex;
                    gradient.SetAlphaKey(i, new AlphaKey(evt.newValue, gradient.AlphaKeys[i].time));
                    NotifyChanged();
                });
                context.Alpha = slider;
                row.Add(slider);
                row.RegisterCallback<FocusInEvent>(_ => KeySelected?.Invoke(context.KeyIndex, true));
            }
            else
            {
                var colorField = new ColorField { name = "value", showAlpha = false, showEyeDropper = true };
                colorField.style.width = 80;
                colorField.style.marginRight = 4;
                colorField.RegisterValueChangedCallback(evt =>
                {
                    int i = context.KeyIndex;
                    gradient.SetColorKey(i, new ColorKey(evt.newValue, gradient.ColorKeys[i].time));
                    NotifyChanged();
                });
                context.Color = colorField;
                row.Add(colorField);
                row.RegisterCallback<FocusInEvent>(_ => KeySelected?.Invoke(context.KeyIndex, false));
            }

            var timeField = new FloatField("t") { name = "time", isDelayed = true, formatString = "0.0000" };
            timeField.style.width = 90;
            timeField.RegisterValueChangedCallback(evt =>
            {
                int i = context.KeyIndex;
                if (isAlpha)
                    gradient.SetAlphaKey(i, new AlphaKey(gradient.AlphaKeys[i].alpha, evt.newValue));
                else
                    gradient.SetColorKey(i, new ColorKey(gradient.ColorKeys[i].color, evt.newValue));
                Refresh(); // re-sort may have reordered keys
                NotifyChanged();
            });
            context.Time = timeField;
            row.Add(timeField);

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
            deleteButton.style.marginLeft = 4;
            var icon = EditorIcons.Delete;
            if (icon != null)
                deleteButton.Add(new Image { image = icon, pickingMode = PickingMode.Ignore, scaleMode = ScaleMode.ScaleToFit });
            row.Add(deleteButton);

            return row;
        }

        private void BindItem(VisualElement row, int listIndex)
        {
            var context = (RowContext)row.userData;
            context.KeyIndex = indices[listIndex];
            int keyIndex = context.KeyIndex;

            if (isAlpha)
            {
                var key = gradient.AlphaKeys[keyIndex];
                context.Time.SetValueWithoutNotify(key.time);
                context.Alpha.SetValueWithoutNotify(key.alpha);
            }
            else
            {
                var key = gradient.ColorKeys[keyIndex];
                context.Time.SetValueWithoutNotify(key.time);
                context.Color.SetValueWithoutNotify(key.color);
            }
        }

        private void NotifyChanged() => Changed?.Invoke();
    }
}
