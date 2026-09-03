using ABCodeworld.Gradients;
using UnityEngine;

namespace ABCodeworld.Gradients.Samples
{
    /// <summary>Bakes a gradient to a texture and tints a light by evaluating it over time.</summary>
    [RequireComponent(typeof(Renderer))]
    public sealed class GradientABCWSample : MonoBehaviour
    {
        [SerializeField] private GradientABCW gradient = GradientABCW.CreateDefault();
        [SerializeField] private Light tintedLight;
        [SerializeField] private float cyclesPerSecond = 0.2f;

        private Texture2D bakedTexture;
        private MaterialPropertyBlock propertyBlock;
        private Renderer targetRenderer;

        private void Awake()
        {
            targetRenderer = GetComponent<Renderer>();
            propertyBlock = new MaterialPropertyBlock();

            bakedTexture = GradientTextureUtility.CreateTexture(gradient, 256, GradientLutOptions.Project(final: true));
            targetRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetTexture("_BaseMap", bakedTexture);
            targetRenderer.SetPropertyBlock(propertyBlock);
        }

        private void Update()
        {
            if (tintedLight == null)
                return;

            float t = Mathf.Repeat(Time.time * cyclesPerSecond, 1f);
            tintedLight.color = gradient.Evaluate(t);
        }

        private void OnDestroy()
        {
            if (bakedTexture != null)
                Destroy(bakedTexture);
        }
    }
}
