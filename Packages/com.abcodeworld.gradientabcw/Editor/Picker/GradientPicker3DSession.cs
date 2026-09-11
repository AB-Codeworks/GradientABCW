using System;

namespace ABCodeworld.Gradients.Editor
{
    /// <summary>
    /// Coordinates one 3D-gradient-editing session between whatever opened the picker (a field, a drawer,
    /// a menu item) and the <see cref="GradientPicker3DWindow"/> itself.
    /// </summary>
    /// <remarks>
    /// A near-copy of <see cref="GradientPickerSession"/> rather than a generic base it shares. Making the
    /// 1D one generic would change the signature of the public
    /// <see cref="GradientABCWField.PickerLauncher"/>, which is API a consumer may already be substituting
    /// into. Twenty lines of duplication is the cheaper side of that trade.
    /// </remarks>
    public sealed class GradientPicker3DSession
    {
        /// <summary>Raised on every edit while the picker is open (only when <see cref="LivePreview"/> is true).</summary>
        public Action<GradientABCW3D> Changed { get; set; }

        /// <summary>Raised once when the user accepts (OK) with the final edited gradient.</summary>
        public Action<GradientABCW3D> Accepted { get; set; }

        /// <summary>Raised once when the user cancels or closes the window without accepting.</summary>
        public Action Cancelled { get; set; }

        /// <summary>When true, edits apply to the caller immediately via <see cref="Changed"/>; otherwise only on Accept.</summary>
        public bool LivePreview { get; set; } = true;
    }
}
