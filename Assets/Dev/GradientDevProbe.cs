using ABCodeworld.Gradients;
using UnityEngine;

namespace ABCodeworld.Gradients.Dev
{
    /// <summary>Manual smoke-test target for the inspector field: a bare gradient plus an asset reference.</summary>
    public sealed class GradientDevProbe : MonoBehaviour
    {
        [SerializeField] private GradientABCW gradient = GradientABCW.CreateDefault();
        [SerializeField] private GradientABCWAsset asset;
    }
}
