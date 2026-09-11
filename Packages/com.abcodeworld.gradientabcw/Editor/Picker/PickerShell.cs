using UnityEngine.UIElements;

namespace ABCodeworld.Gradients.Editor
{
    /// <summary>
    /// The two bits of shell assembly both picker windows do identically: filling the gesture hint
    /// strip and hanging a pane off a <see cref="TabView"/>.
    /// </summary>
    /// <remarks>
    /// Type-free, like <see cref="AssetFolders"/> and <see cref="ResettableSliderRow"/> — it names no
    /// gradient of either dimension, so sharing it invents no abstraction. The windows keep
    /// everything that does name one.
    /// </remarks>
    internal static class PickerShell
    {
        /// <summary>
        /// Writes the gesture hints under the viewport.
        /// </summary>
        /// <remarks>
        /// Both windows carry gestures that appear nowhere else in the UI — Shift-click and Alt-click
        /// to add, dragging a key off its lane to remove it, right-drag to turn the cube. Until this
        /// strip existed the only way to learn any of them was to be told.
        /// </remarks>
        public static void Hints(VisualElement host, params string[] hints)
        {
            if (host == null)
                return;

            foreach (string hint in hints)
            {
                var label = new Label(hint);
                label.AddToClassList("abcw-hint");
                host.Add(label);
            }
        }

        /// <summary>Adds one named tab wrapping <paramref name="content"/>.</summary>
        public static void AddTab(TabView tabs, string label, string elementName, VisualElement content)
        {
            var tab = new Tab(label) { name = elementName };
            tab.Add(content);
            tabs.Add(tab);
        }
    }
}
