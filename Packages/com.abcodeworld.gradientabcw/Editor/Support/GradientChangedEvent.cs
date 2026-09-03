using UnityEngine.UIElements;

namespace ABCodeworld.Gradients.Editor
{
    /// <summary>Bubbling event raised whenever a gradient-editing element mutates a <see cref="GradientABCW"/>.</summary>
    internal sealed class GradientChangedEvent : EventBase<GradientChangedEvent>
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
