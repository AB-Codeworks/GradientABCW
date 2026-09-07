using UnityEngine;

namespace ABCodeworld.Gradients
{
    /// <summary>Asset wrapper for a reusable <see cref="GradientABCW"/>.</summary>
    [CreateAssetMenu(menuName = "ABCodeworld/Gradient ABCW", fileName = "NewGradientABCW")]
    public sealed class GradientABCWAsset : ScriptableObject
    {
        [SerializeField] private GradientABCW gradient;

        /// <summary>
        /// Created on first access rather than in a field initializer: the initializer ran on every
        /// ScriptableObject construction, including the one deserialization immediately overwrites.
        /// </summary>
        public GradientABCW Gradient
        {
            get => gradient ??= GradientABCW.CreateDefault();
            set => gradient = value;
        }
    }
}
