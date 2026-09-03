using UnityEngine;

namespace ABCodeworld.Gradients.Tests.Editor.Support
{
    /// <summary>Minimal ScriptableObject host used by serialization, drawer and picker tests.</summary>
    internal sealed class TestHostObject : ScriptableObject
    {
        public GradientABCW gradient = GradientABCW.CreateDefault();
    }
}
