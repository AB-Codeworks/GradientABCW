using System;

namespace ABCodeworld.Gradients.Editor
{
    /// <summary>
    /// Coordinates one gradient-editing session between whatever opened the picker (a field, a drawer,
    /// a menu item) and the <see cref="GradientPickerWindow"/> itself.
    /// </summary>
    public sealed class GradientPickerSession
    {
        /// <summary>Raised on every edit while the picker is open (only when <see cref="LivePreview"/> is true).</summary>
        public Action<GradientABCW> Changed { get; set; }

        /// <summary>Raised once when the user accepts (OK) with the final edited gradient.</summary>
        public Action<GradientABCW> Accepted { get; set; }

        /// <summary>Raised once when the user cancels or closes the window without accepting.</summary>
        public Action Cancelled { get; set; }

        /// <summary>When true, edits apply to the caller immediately via <see cref="Changed"/>; otherwise only on Accept.</summary>
        public bool LivePreview { get; set; } = true;
    }
}
