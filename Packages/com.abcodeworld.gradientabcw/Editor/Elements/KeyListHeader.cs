using UnityEngine.UIElements;

namespace ABCodeworld.Gradients.Editor
{
    /// <summary>
    /// The bar across the top of a key list: a pip in the shape the rest of the package uses for that
    /// kind of key, the list's name, and how many keys it holds against the cap.
    /// </summary>
    /// <remarks>
    /// The two lists used to render no title, no header and no count at all, so the colour column and
    /// the alpha column were distinguishable only by what happened to be inside their rows. The count
    /// does a second job: the key cap used to fail in silence, with adding past it simply doing
    /// nothing.
    /// <para>
    /// Type-free, so both the flat and the cube lists use it — the cap is passed in, because the two
    /// gradients do not share one.
    /// </para>
    /// </remarks>
    internal sealed class KeyListHeader : VisualElement
    {
        private readonly Label count;

        public KeyListHeader(bool isAlpha, string title)
        {
            AddToClassList("abcw-key-list__header");

            var pip = new VisualElement { name = "pip", pickingMode = PickingMode.Ignore };
            pip.AddToClassList("abcw-key-list__pip");
            pip.EnableInClassList("abcw-key-list__pip--alpha", isAlpha);
            Add(pip);

            var label = new Label(title) { pickingMode = PickingMode.Ignore };
            label.AddToClassList("abcw-key-list__title");
            Add(label);

            Add(new VisualElement { style = { flexGrow = 1 }, pickingMode = PickingMode.Ignore });

            count = new Label { name = "keyCount", pickingMode = PickingMode.Ignore };
            count.AddToClassList("abcw-key-list__count");
            Add(count);
        }

        /// <summary>Colours the pip to match the first key, so the header reads as that list's kind.</summary>
        public void SetPipColor(UnityEngine.Color color) =>
            this.Q<VisualElement>("pip").style.backgroundColor = color;

        public void SetCount(int used, int cap) => count.text = $"{used} / {cap}";
    }
}
