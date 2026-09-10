using UnityEngine;

namespace ABCodeworld.Gradients
{
    /// <summary>Asset wrapper for a reusable <see cref="GradientABCW3D"/>.</summary>
    [CreateAssetMenu(menuName = "ABCodeworld/Gradient ABCW 3D", fileName = "NewGradientABCW3D")]
    public sealed class GradientABCW3DAsset : ScriptableObject
    {
        [SerializeField] private GradientABCW3D gradient;

        /// <summary>
        /// Created on first access rather than in a field initializer: the initializer runs on every
        /// ScriptableObject construction, including the one deserialization immediately overwrites.
        /// </summary>
        public GradientABCW3D Gradient
        {
            get => gradient ??= GradientABCW3D.CreateDefault();
            set => gradient = value;
        }
    }
}
