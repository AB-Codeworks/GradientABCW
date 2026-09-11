using UnityEngine;

namespace ABCodeworld.Gradients.Dev
{
    /// <summary>
    /// Manual smoke-test target for the inspector fields: a bare gradient of each kind plus an asset
    /// reference to each.
    /// </summary>
    public sealed class GradientDevProbe : MonoBehaviour
    {
        [SerializeField] private GradientABCW gradient = GradientABCW.CreateDefault();
        [SerializeField] private GradientABCWAsset asset;

        [SerializeField] private GradientABCW3D volume = GradientABCW3D.CreateDefault();
        [SerializeField] private GradientABCW3DAsset volumeAsset;
    }
}
