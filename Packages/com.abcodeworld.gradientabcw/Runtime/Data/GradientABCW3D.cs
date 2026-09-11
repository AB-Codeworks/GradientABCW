using System;
using Unity.Mathematics;
using UnityEngine;

namespace ABCodeworld.Gradients
{
    /// <summary>
    /// A gradient of up to <see cref="MaxKeys"/> colour keys and <see cref="MaxKeys"/> alpha keys
    /// positioned anywhere inside the unit cube, with the same evaluation-time modulation as
    /// <see cref="GradientABCW"/>. Modulation never mutates key data; use <see cref="FlipKeys"/> to
    /// permanently mirror key positions instead.
    /// </summary>
    /// <remarks>
    /// The 1D gradient orders its keys by time and interpolates between the two that bracket the sample.
    /// Points in a cube cannot be ordered that way, so <see cref="BlendMode.Smooth"/> here means
    /// inverse-distance weighting over every key, sharpened by <see cref="FalloffPower"/>, and
    /// <see cref="BlendMode.Stepped"/> means the nearest key wins outright. See
    /// <see cref="GradientMath3D"/> for the exact formulation.
    /// <para>
    /// A reference type rather than a struct so that editors and consumers share one instance and one
    /// evaluation cache without the aliasing pitfalls of copying a struct that owns arrays. Use
    /// <see cref="Clone"/> when an independent copy is required.
    /// </para>
    /// <para>
    /// Every mutator is a no-op when handed the value the gradient already holds. That matters well beyond
    /// tidiness: <see cref="Version"/> is the invalidation signal for the native snapshot, every
    /// <see cref="GradientLut3DCache"/>, and every editor preview texture, so a control that re-sends its
    /// current value would otherwise force all of them to rebuild.
    /// </para>
    /// </remarks>
    /// <remarks>
    /// Split across three files by concern: this one holds state and the mutation API,
    /// GradientABCW3D.Serialization.cs holds validation and defaults, and GradientABCW3D.Hashing.cs holds
    /// content comparison.
    /// </remarks>
    [Serializable]
    public sealed partial class GradientABCW3D : ISerializationCallbackReceiver
    {
        /// <summary>
        /// One key is enough for a 3D gradient, where the 1D one needs two.
        /// </summary>
        /// <remarks>
        /// A single key describes a constant field, which is a perfectly usable gradient. The 1D minimum
        /// of two exists because a ramp needs two ends to interpolate between; padding a lone 3D key up to
        /// two would only produce a second key stacked somewhere arbitrary.
        /// </remarks>
        public const int MinKeys = 1;

        /// <summary>Twice the 1D limit: a volume needs more keys than a line to describe the same detail.</summary>
        public const int MaxKeys = 64;

        public const float MinFalloffPower = 1f;
        public const float MaxFalloffPower = 8f;
        public const float DefaultFalloffPower = 2f;

        [SerializeField] private ColorKey3D[] colorKeys;
        [SerializeField] private AlphaKey3D[] alphaKeys;
        [SerializeField] private BlendMode blendMode;
        [SerializeField] private float falloffPower;
        [SerializeField] private GradientModulation modulation;

        [NonSerialized] private int version;
        [NonSerialized] private NativeGradient3D nativeCache;
        [NonSerialized] private int nativeVersion = -1;

        private GradientABCW3D()
        {
            colorKeys = DefaultColorKeys();
            alphaKeys = DefaultAlphaKeys();
            blendMode = BlendMode.Smooth;
            falloffPower = DefaultFalloffPower;
            modulation = GradientModulation.Identity;
        }

        public static GradientABCW3D CreateDefault() => new GradientABCW3D();

        public GradientABCW3D Clone()
        {
            var clone = new GradientABCW3D();
            clone.CopyFrom(this);
            return clone;
        }

        public void CopyFrom(GradientABCW3D other)
        {
            if (other is null)
                throw new ArgumentNullException(nameof(other));

            colorKeys = (ColorKey3D[])other.colorKeys.Clone();
            alphaKeys = (AlphaKey3D[])other.alphaKeys.Clone();
            blendMode = other.blendMode;
            falloffPower = other.falloffPower;
            modulation = other.modulation;
            version++;
        }

        /// <summary>Bumped on every mutation that actually changes content; caches key their state on this to know when to rebuild.</summary>
        public int Version => version;

        /// <summary>
        /// An unmanaged snapshot of this gradient for Burst evaluation, rebuilt only when
        /// <see cref="Version"/> changes.
        /// </summary>
        /// <remarks>Rebuilds lazily and is therefore not safe to touch from multiple threads at once.
        /// Call <see cref="PrepareNative"/> on the main thread before scheduling work that reads it.</remarks>
        public ref readonly NativeGradient3D Native
        {
            get
            {
                PrepareNative();
                return ref nativeCache;
            }
        }

        /// <summary>
        /// Brings <see cref="Native"/> up to date, so a later read of it cannot trigger a rebuild.
        /// </summary>
        /// <remarks>
        /// Baking a 32-cubed LUT is exactly the kind of work a caller wants to hand to the job system, and
        /// the lazy rebuild behind <see cref="Native"/> is not thread-safe. Touching this first, on the
        /// main thread, makes the subsequent read a plain field access.
        /// </remarks>
        public void PrepareNative()
        {
            if (nativeVersion == version)
                return;

            nativeCache = NativeGradient3D.From(this);
            nativeVersion = version;
        }

        public ReadOnlySpan<ColorKey3D> ColorKeys => colorKeys;
        public ReadOnlySpan<AlphaKey3D> AlphaKeys => alphaKeys;

        public BlendMode BlendMode
        {
            get => blendMode;
            set
            {
                if (blendMode == value)
                    return;
                blendMode = value;
                version++;
            }
        }

        /// <summary>
        /// How sharply a key's influence falls off with distance under <see cref="BlendMode.Smooth"/>.
        /// Clamped to [<see cref="MinFalloffPower"/>, <see cref="MaxFalloffPower"/>]; 2 is the classic
        /// inverse-square weighting. Higher values pull the result towards the nearest key, approaching —
        /// but never reaching — what <see cref="BlendMode.Stepped"/> does outright.
        /// </summary>
        /// <remarks>
        /// Gradient content, not modulation: it defines what the base gradient is rather than adjusting
        /// the result, so it sits beside <see cref="BlendMode"/>, is covered by
        /// <see cref="ComputeContentHash"/>, and is unaffected by <c>GradientModulation.bypass</c>.
        /// It has no effect at all under <see cref="BlendMode.Stepped"/>.
        /// </remarks>
        public float FalloffPower
        {
            get => falloffPower;
            set
            {
                float clamped = Mathf.Clamp(value, MinFalloffPower, MaxFalloffPower);
                if (falloffPower == clamped)
                    return;
                falloffPower = clamped;
                version++;
            }
        }

        public GradientModulation Modulation
        {
            get => modulation;
            set
            {
                var clamped = value.Clamped();
                if (modulation.Equals(clamped))
                    return;
                modulation = clamped;
                version++;
            }
        }

        public bool CanAddColorKey => colorKeys.Length < MaxKeys;
        public bool CanAddAlphaKey => alphaKeys.Length < MaxKeys;

        /// <summary>Adds a colour key. Returns its index, or -1 when already at <see cref="MaxKeys"/>.</summary>
        public int AddColorKey(Color color, Vector3 position)
        {
            int index = KeyCloud<ColorKey3D>.Append(ref colorKeys, new ColorKey3D(color, position), MaxKeys);
            if (index >= 0)
                version++;
            return index;
        }

        /// <summary>Adds an alpha key. Returns its index, or -1 when already at <see cref="MaxKeys"/>.</summary>
        public int AddAlphaKey(float alpha, Vector3 position)
        {
            int index = KeyCloud<AlphaKey3D>.Append(ref alphaKeys, new AlphaKey3D(alpha, position), MaxKeys);
            if (index >= 0)
                version++;
            return index;
        }

        /// <summary>Removes the colour key at <paramref name="index"/>. Refuses when at or below <see cref="MinKeys"/>.</summary>
        public bool RemoveColorKey(int index)
        {
            bool removed = KeyCloud<ColorKey3D>.RemoveAt(ref colorKeys, index, MinKeys);
            if (removed)
                version++;
            return removed;
        }

        /// <summary>Removes the alpha key at <paramref name="index"/>. Refuses when at or below <see cref="MinKeys"/>.</summary>
        public bool RemoveAlphaKey(int index)
        {
            bool removed = KeyCloud<AlphaKey3D>.RemoveAt(ref alphaKeys, index, MinKeys);
            if (removed)
                version++;
            return removed;
        }

        /// <summary>
        /// Replaces the colour key at <paramref name="index"/>. Writing back an identical key is a no-op
        /// and does not bump <see cref="Version"/>.
        /// </summary>
        /// <remarks>
        /// Returns nothing, and the key keeps its index. The 1D equivalent has to re-sort and hand back a
        /// new index because moving a key along t reorders the array; moving a key through the cube
        /// reorders nothing.
        /// </remarks>
        public void SetColorKey(int index, ColorKey3D key)
        {
            var replacement = key.Normalized();
            var existing = colorKeys[index];
            if (ExactlyEqual(existing.position, replacement.position) && ExactlyEqual(existing.color, replacement.color))
                return;

            colorKeys[index] = replacement;
            version++;
        }

        /// <summary>
        /// Replaces the alpha key at <paramref name="index"/>. Writing back an identical key is a no-op
        /// and does not bump <see cref="Version"/>. See <see cref="SetColorKey"/> for why no index is
        /// returned.
        /// </summary>
        public void SetAlphaKey(int index, AlphaKey3D key)
        {
            var replacement = key.Normalized();
            var existing = alphaKeys[index];
            if (ExactlyEqual(existing.position, replacement.position) && existing.alpha == replacement.alpha)
                return;

            alphaKeys[index] = replacement;
            version++;
        }

        /// <summary>
        /// Replaces both key arrays: validates, and decimates down to <see cref="MaxKeys"/> when
        /// over-long, keeping the keys that best span the volume. Only genuinely empty input falls back to
        /// the default black-to-white diagonal.
        /// </summary>
        public void SetKeys(ReadOnlySpan<ColorKey3D> colors, ReadOnlySpan<AlphaKey3D> alphas)
        {
            colorKeys = ValidateColorKeys(colors.ToArray());
            alphaKeys = ValidateAlphaKeys(alphas.ToArray());
            version++;
        }

        /// <summary>
        /// Permanently mirrors every key through the centre of the cube, on all three axes at once.
        /// </summary>
        /// <remarks>
        /// Mirroring three axes flips handedness, so this is a reflection of the gradient rather than a
        /// rotation of it — the 3D reading of what "flip" means in 1D, and the permanent counterpart to
        /// <c>GradientModulation.reverse</c>.
        /// </remarks>
        public void FlipKeys()
        {
            KeyCloud<ColorKey3D>.FlipPositions(colorKeys);
            KeyCloud<AlphaKey3D>.FlipPositions(alphaKeys);
            version++;
        }

        public void DistributeColorKeysEvenly()
        {
            KeyCloud<ColorKey3D>.Distribute(colorKeys);
            version++;
        }

        public void DistributeAlphaKeysEvenly()
        {
            KeyCloud<AlphaKey3D>.Distribute(alphaKeys);
            version++;
        }

        // Vector3 and Color both define == as an approximate comparison, with a tolerance of about 1e-5
        // per component. Neither the no-op checks above nor ContentEquals can use that: a key nudged by
        // less than the tolerance would leave Version unchanged, so no cache would rebuild and the edit
        // would simply not appear — while ComputeContentHash, which hashes raw bits, would report the two
        // gradients as different. Comparing exactly keeps the hash and the equality check in agreement.

        private static bool ExactlyEqual(Vector3 a, Vector3 b) => a.x == b.x && a.y == b.y && a.z == b.z;

        /// <remarks>Alpha is not compared: <see cref="ColorKey3D"/> forces it to 1, and a colour key's
        /// transparency comes from the alpha keys.</remarks>
        private static bool ExactlyEqual(Color a, Color b) => a.r == b.r && a.g == b.g && a.b == b.b;

        /// <summary>Evaluates the gradient at <paramref name="position"/>, applying modulation unless bypassed or at its identity value.</summary>
        public Color Evaluate(Vector3 position) => Evaluate(position, applyModulation: true);

        /// <summary>Evaluates the gradient at <paramref name="position"/> ignoring modulation entirely.</summary>
        public Color EvaluateBase(Vector3 position) => Evaluate(position, applyModulation: false);

        private Color Evaluate(Vector3 position, bool applyModulation)
        {
            var p = new float3(position.x, position.y, position.z);
            var c = applyModulation
                ? GradientMath3D.Evaluate(in Native, p)
                : GradientMath3D.EvaluateBase(in Native, p);
            return new Color(c.x, c.y, c.z, c.w);
        }
    }
}
