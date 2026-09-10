using UnityEngine;

namespace ABCodeworld.Gradients.Samples
{
    /// <summary>
    /// Bakes a 3D gradient to a <see cref="Texture3D"/> for a shader, and tints a light by evaluating the
    /// same gradient along a path through the cube.
    /// </summary>
    /// <remarks>
    /// The two halves show the two ways a 3D gradient is meant to be consumed. A shader wants the baked
    /// volume, where the hardware's own trilinear filtering makes 32 cubed granular enough to sample
    /// directly — which is why the scale and bias go across as well, since the bake puts cells on the
    /// domain's endpoints while a GPU samples cell centres. Script wants
    /// <see cref="GradientABCW3D.Evaluate"/>, which is exact and needs no bake at all.
    /// </remarks>
    [RequireComponent(typeof(Renderer))]
    public sealed class GradientABCW3DSample : MonoBehaviour
    {
        private static readonly int VolumeId = Shader.PropertyToID("_GradientVolume");
        private static readonly int ScaleOffsetId = Shader.PropertyToID("_GradientVolumeScaleOffset");

        [SerializeField] private GradientABCW3D gradient = GradientABCW3D.CreateDefault();
        [SerializeField] private Light tintedLight;
        [SerializeField] private float cyclesPerSecond = 0.2f;

        [Tooltip("Cells per axis. 32 is 32768 cells and 128 KB; granularity comes from filtering, not from this.")]
        [SerializeField] private int volumeSize = GradientLut3D.DefaultSize;

        private Texture3D bakedVolume;
        private MaterialPropertyBlock propertyBlock;
        private Renderer targetRenderer;

        private void Awake()
        {
            targetRenderer = GetComponent<Renderer>();
            propertyBlock = new MaterialPropertyBlock();

            bakedVolume = GradientTexture3DUtility.CreateTexture(gradient, volumeSize);

            targetRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetTexture(VolumeId, bakedVolume);
            propertyBlock.SetVector(ScaleOffsetId, GradientLut3D.ShaderScaleOffset(bakedVolume.width));
            targetRenderer.SetPropertyBlock(propertyBlock);
        }

        private void Update()
        {
            if (tintedLight == null)
                return;

            // A diagonal sweep through the cube, so the path passes through every axis rather than
            // sampling one face of the gradient over and over.
            float t = Mathf.Repeat(Time.time * cyclesPerSecond, 1f);
            var position = new Vector3(t, Mathf.PingPong(t * 2f, 1f), 1f - t);

            tintedLight.color = gradient.Evaluate(position);
        }

        private void OnDestroy()
        {
            if (bakedVolume != null)
                Destroy(bakedVolume);
        }
    }
}
