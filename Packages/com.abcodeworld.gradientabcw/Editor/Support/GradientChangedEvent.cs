using UnityEngine.UIElements;

namespace ABCodeworld.Gradients.Editor
{
    /// <summary>Bubbling event raised whenever a gradient-editing element mutates a <see cref="GradientABCW"/>.</summary>
    /// <remarks>
    /// Public because <see cref="GradientABCWField"/> and <see cref="GradientABCW3DField"/> are, and this
    /// is the only signal either of them gives for an edit made in place. UI Toolkit's own
    /// <c>ChangeEvent&lt;T&gt;</c> fires when the field is handed a different gradient instance, by design
    /// — it cannot fire for a key dragged inside the instance the field already holds, which is most of
    /// what anyone does to a gradient. An internal event type here would have left every consumer of the
    /// public field unable to observe that at all.
    /// </remarks>
    public sealed class GradientChangedEvent : EventBase<GradientChangedEvent>
    {
        public enum ChangeKind { Keys, BlendMode, Modulation, Replaced }

        public GradientABCW Gradient { get; private set; }
        public ChangeKind Kind { get; private set; }

        public static GradientChangedEvent GetPooled(GradientABCW gradient, ChangeKind kind)
        {
            var evt = GetPooled();
            evt.Gradient = gradient;
            evt.Kind = kind;
            return evt;
        }

        public GradientChangedEvent() => LocalInit();

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
