using UnityEngine;

namespace ABCodeworld.Gradients
{
    /// <summary>
    /// Serialization callbacks and the validation that guards them, so data arriving from disk, from a
    /// caller, or from an older version of the package is always brought into a usable shape.
    /// </summary>
    public sealed partial class GradientABCW
    {
        void ISerializationCallbackReceiver.OnBeforeSerialize() { }

        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            colorKeys = ValidateColorKeys(colorKeys);
            alphaKeys = ValidateAlphaKeys(alphaKeys);
            modulation = modulation.Clamped();
            version++;
        }

        // Both lanes share one implementation in KeyArray<T>.Validate. These wrappers exist only to supply
        // the per-kind fallback; the validation itself used to be written out twice, byte for byte.

        private static ColorKey[] ValidateColorKeys(ColorKey[] keys) =>
            KeyArray<ColorKey>.Validate(keys, MinKeys, MaxKeys) ?? DefaultColorKeys();

        private static AlphaKey[] ValidateAlphaKeys(AlphaKey[] keys) =>
            KeyArray<AlphaKey>.Validate(keys, MinKeys, MaxKeys) ?? DefaultAlphaKeys();

        private static ColorKey[] DefaultColorKeys() => new[]
        {
            new ColorKey(Color.black, 0f),
            new ColorKey(Color.white, 1f),
        };

        private static AlphaKey[] DefaultAlphaKeys() => new[]
        {
            new AlphaKey(1f, 0f),
            new AlphaKey(1f, 1f),
        };
    }
}
