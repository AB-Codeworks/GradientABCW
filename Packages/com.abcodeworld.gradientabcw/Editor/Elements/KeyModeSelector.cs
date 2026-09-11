using System;
using UnityEngine.UIElements;

namespace ABCodeworld.Gradients.Editor
{
    /// <summary>
    /// The 3D picker's colour/alpha mode switch and its Add Key button, filling the column the 1D picker
    /// uses for its two draggable palette swatches.
    /// </summary>
    /// <remarks>
    /// The 1D picker never needs a mode: you drag an alpha swatch into the top lane or a colour swatch
    /// into the bottom one, and the gesture says which you meant. There is no equivalent gesture in a
    /// cube — a point inside it says nothing about whether you wanted colour or alpha — so the choice has
    /// to become explicit before anything can act on it.
    /// </remarks>
    internal sealed class KeyModeSelector : VisualElement
    {
        private const string ActiveClass = "abcw-mode-btn--active";

        private readonly Button alphaButton;
        private readonly Button colorButton;
        private readonly Button addButton;
        private bool isAlpha;

        /// <summary>Raised with the new mode whenever it changes; true for alpha keys.</summary>
        public event Action<bool> ModeChanged;

        /// <summary>Raised when Add Key is pressed. The window decides what to add and where.</summary>
        public event Action AddKeyRequested;

        /// <summary>True when alpha keys are being edited, false for colour keys.</summary>
        public bool IsAlpha
        {
            get => isAlpha;
            set
            {
                if (isAlpha == value)
                    return;

                isAlpha = value;
                SyncButtons();
                ModeChanged?.Invoke(isAlpha);
            }
        }

        public KeyModeSelector()
        {
            AddToClassList("abcw-mode-selector");

            alphaButton = new Button(() => IsAlpha = true)
            {
                name = "alphaMode",
                text = "Alpha Keys",
                tooltip = "Edit alpha keys. Add Key and the cube's highlighted dots follow this choice.",
            };
            alphaButton.AddToClassList("abcw-mode-btn");
            Add(alphaButton);

            colorButton = new Button(() => IsAlpha = false)
            {
                name = "colorMode",
                text = "Color Keys",
                tooltip = "Edit colour keys. Add Key and the cube's highlighted dots follow this choice.",
            };
            colorButton.AddToClassList("abcw-mode-btn");
            Add(colorButton);

            addButton = new Button(() => AddKeyRequested?.Invoke())
            {
                name = "addKey",
                text = "Add Key",
                tooltip = "Add a key of the selected kind at the centre of the cube.",
            };
            addButton.AddToClassList("abcw-btn");
            addButton.style.marginTop = 10;
            Add(addButton);

            SyncButtons();
        }

        /// <summary>Enables or disables Add Key, for when the gradient is already at its key limit.</summary>
        public void SetCanAddKey(bool canAdd) => addButton.SetEnabled(canAdd);

        private void SyncButtons()
        {
            alphaButton.EnableInClassList(ActiveClass, isAlpha);
            colorButton.EnableInClassList(ActiveClass, !isAlpha);
        }
    }
}
