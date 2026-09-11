using UnityEngine.UIElements;

namespace ABCodeworld.Gradients.Editor
{
    /// <summary>Bubbling event raised whenever an editing element mutates a <see cref="GradientABCW3D"/> in place.</summary>
    /// <remarks>
    /// A separate event type from <see cref="GradientChangedEvent"/> rather than a shared one carrying an
    /// untyped payload. UI Toolkit dispatches by type, so this is what lets a container hold both a 1D and
    /// a 3D field without either one's listeners waking for the other's edits.
    /// </remarks>
    /// <remarks>
    /// Public because <see cref="GradientABCWField"/> and <see cref="GradientABCW3DField"/> are, and this
    /// is the only signal either of them gives for an edit made in place. UI Toolkit's own
    /// <c>ChangeEvent&lt;T&gt;</c> fires when the field is handed a different gradient instance, by design
    /// — it cannot fire for a key dragged inside the instance the field already holds, which is most of
    /// what anyone does to a gradient. An internal event type here would have left every consumer of the
    /// public field unable to observe that at all.
    /// </remarks>
    public sealed class Gradient3DChangedEvent : EventBase<Gradient3DChangedEvent>
    {
        public enum ChangeKind { Keys, BlendMode, Falloff, Modulation, Replaced }

        public GradientABCW3D Gradient { get; private set; }
        public ChangeKind Kind { get; private set; }

        public static Gradient3DChangedEvent GetPooled(GradientABCW3D gradient, ChangeKind kind)
        {
            var evt = GetPooled();
            evt.Gradient = gradient;
            evt.Kind = kind;
            return evt;
        }

        public Gradient3DChangedEvent() => LocalInit();

        protected override void Init()
        {
            base.Init();
            LocalInit();
        }

        private void LocalInit()
        {
            bubbles = true;
            tricklesDown = false;
            Gradient = null;
            Kind = ChangeKind.Replaced;
        }
    }
}
