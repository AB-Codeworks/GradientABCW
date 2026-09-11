using ABCodeworld.Gradients;
using UnityEngine;

namespace ABCodeworld.Gradients.Samples
{
    /// <summary>Bakes a gradient to a texture and tints a light by evaluating it over time.</summary>
    [RequireComponent(typeof(Renderer))]
    public sealed class GradientABCWSample : MonoBehaviour
    {
        /// <summary>
        /// The texture property to tint. <c>_BaseMap</c> is URP/HDRP's name; the Built-in pipeline's
        /// Standard shader calls it <c>_MainTex</c>. Set it to whatever the material's shader declares —
        /// a name no shader has binds nothing, silently.
        /// </summary>
        [SerializeField] private string textureProperty = "_BaseMap";

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
            propertyBlock.SetTexture(Shader.PropertyToID(textureProperty), bakedTexture);
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
