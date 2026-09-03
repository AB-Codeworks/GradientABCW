using UnityEngine;

namespace ABCodeworld.Gradients
{
    /// <summary>Asset wrapper for a reusable <see cref="GradientABCW"/>.</summary>
    [CreateAssetMenu(menuName = "ABCodeworld/Gradient ABCW", fileName = "NewGradientABCW")]
    public sealed class GradientABCWAsset : ScriptableObject
    {
        [SerializeField] private GradientABCW gradient = GradientABCW.CreateDefault();

        public GradientABCW Gradient => gradient;
    }
}
