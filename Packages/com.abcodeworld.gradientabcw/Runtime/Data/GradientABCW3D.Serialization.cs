using UnityEngine;

namespace ABCodeworld.Gradients
{
    /// <summary>
    /// Serialization callbacks and the validation that guards them, so data arriving from disk, from a
    /// caller, or from an older version of the package is always brought into a usable shape.
    /// </summary>
    public sealed partial class GradientABCW3D
    {
        void ISerializationCallbackReceiver.OnBeforeSerialize() { }

        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            colorKeys = ValidateColorKeys(colorKeys);
            alphaKeys = ValidateAlphaKeys(alphaKeys);
            falloffPower = ValidateFalloffPower(falloffPower);
            modulation = modulation.Clamped();
            version++;
        }

        /// <summary>
        /// Clamps a deserialized falloff power, restoring the default rather than the minimum when the
        /// stored value is missing.
        /// </summary>
        /// <remarks>
        /// Unity zero-fills a field it has no serialized value for, and this gradient is only ever
        /// constructed through <see cref="CreateDefault"/> on the authoring side — so a zero here means
        /// "written by something that did not know about this field", not "the author asked for zero".
        /// Clamping it to <see cref="MinFalloffPower"/> would silently soften every such gradient;
        /// restoring <see cref="DefaultFalloffPower"/> leaves it looking the way it was authored.
        /// </remarks>
        private static float ValidateFalloffPower(float value) =>
            value <= 0f ? DefaultFalloffPower : Mathf.Clamp(value, MinFalloffPower, MaxFalloffPower);

        // Both lanes share one implementation in KeyCloud<T>.Validate. These wrappers exist only to supply
        // the per-kind fallback, so the default is built only when it is actually needed.

        private static ColorKey3D[] ValidateColorKeys(ColorKey3D[] keys) =>
            KeyCloud<ColorKey3D>.Validate(keys, MaxKeys) ?? DefaultColorKeys();

        private static AlphaKey3D[] ValidateAlphaKeys(AlphaKey3D[] keys) =>
            KeyCloud<AlphaKey3D>.Validate(keys, MaxKeys) ?? DefaultAlphaKeys();

        /// <summary>
        /// Black at the origin corner to white at the opposite one: the diagonal reading of the 1D
        /// default's black-to-white ramp.
        /// </summary>
        private static ColorKey3D[] DefaultColorKeys() => new[]
        {
            new ColorKey3D(Color.black, Vector3.zero),
            new ColorKey3D(Color.white, Vector3.one),
        };

        private static AlphaKey3D[] DefaultAlphaKeys() => new[]
        {
            new AlphaKey3D(1f, Vector3.zero),
            new AlphaKey3D(1f, Vector3.one),
        };
    }
}
