using System;
using UnityEngine;

namespace ABCodeworld.Gradients
{
    /// <summary>
    /// A gradient of up to <see cref="MaxKeys"/> colour keys and <see cref="MaxKeys"/> alpha keys, with
    /// evaluation-time modulation (reverse, repeat domain mapping, hue/saturation/brightness/alpha adjustment).
    /// Modulation never mutates key data; use <see cref="FlipKeys"/> to permanently flip key times instead.
    /// </summary>
    /// <remarks>
    /// A reference type rather than a struct so that editors and consumers share one instance and one
    /// evaluation cache without the aliasing pitfalls of copying a struct that owns arrays. Use
    /// <see cref="Clone"/> when an independent copy is required.
    /// <para>
    /// Every mutator is a no-op when handed the value the gradient already holds. That matters well beyond
    /// tidiness: <see cref="Version"/> is the invalidation signal for the native snapshot, every
    /// <see cref="GradientLutCache"/>, and every editor preview texture, so a control that re-sends its
    /// current value would otherwise force all of them to rebuild.
    /// </para>
    /// </remarks>
    /// <remarks>
    /// Split across three files by concern: this one holds state and the mutation API,
    /// GradientABCW.Serialization.cs holds validation and defaults, and GradientABCW.Hashing.cs holds
    /// content comparison.
    /// </remarks>
    [Serializable]
    public sealed partial class GradientABCW : ISerializationCallbackReceiver
    {
        public const int MinKeys = 2;
        public const int MaxKeys = 32;

        private const float NeighbourClampEpsilon = 1e-3f;

        [SerializeField] private ColorKey[] colorKeys;
        [SerializeField] private AlphaKey[] alphaKeys;
        [SerializeField] private BlendMode blendMode;
        [SerializeField] private GradientModulation modulation;

        [NonSerialized] private int version;
        [NonSerialized] private NativeGradient nativeCache;
        [NonSerialized] private int nativeVersion = -1;

        private GradientABCW()
        {
            colorKeys = DefaultColorKeys();
            alphaKeys = DefaultAlphaKeys();
            blendMode = BlendMode.Smooth;
            modulation = GradientModulation.Identity;
        }

        public static GradientABCW CreateDefault() => new GradientABCW();

        public GradientABCW Clone()
        {
            var clone = new GradientABCW();
            clone.CopyFrom(this);
            return clone;
        }

        public void CopyFrom(GradientABCW other)
        {
            if (other is null)
                throw new ArgumentNullException(nameof(other));

            colorKeys = (ColorKey[])other.colorKeys.Clone();
            alphaKeys = (AlphaKey[])other.alphaKeys.Clone();
            blendMode = other.blendMode;
            modulation = other.modulation;
            version++;
        }

        /// <summary>Bumped on every mutation that actually changes content; caches key their state on this to know when to rebuild.</summary>
        public int Version => version;

        /// <summary>
        /// An unmanaged snapshot of this gradient for Burst evaluation, rebuilt only when
        /// <see cref="Version"/> changes.
        /// </summary>
        /// <remarks>Rebuilds lazily and is therefore not safe to touch from multiple threads at once.</remarks>
        public ref readonly NativeGradient Native
        {
            get
            {
                if (nativeVersion != version)
                {
                    nativeCache = NativeGradient.From(this);
                    nativeVersion = version;
                }
                return ref nativeCache;
            }
        }

        public ReadOnlySpan<ColorKey> ColorKeys => colorKeys;
        public ReadOnlySpan<AlphaKey> AlphaKeys => alphaKeys;

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

        /// <summary>Adds a colour key. Returns its sorted index, or -1 when already at <see cref="MaxKeys"/>.</summary>
        public int AddColorKey(Color color, float time)
        {
            int index = KeyArray<ColorKey>.InsertSorted(ref colorKeys, new ColorKey(color, time), MaxKeys);
            if (index >= 0)
                version++;
            return index;
        }

        /// <summary>Adds an alpha key. Returns its sorted index, or -1 when already at <see cref="MaxKeys"/>.</summary>
        public int AddAlphaKey(float alpha, float time)
        {
            int index = KeyArray<AlphaKey>.InsertSorted(ref alphaKeys, new AlphaKey(alpha, time), MaxKeys);
            if (index >= 0)
                version++;
            return index;
        }

        /// <summary>Removes the colour key at <paramref name="index"/>. Refuses when at or below <see cref="MinKeys"/>.</summary>
        public bool RemoveColorKey(int index)
        {
            bool removed = KeyArray<ColorKey>.RemoveAt(ref colorKeys, index, MinKeys);
            if (removed)
                version++;
            return removed;
        }

        /// <summary>Removes the alpha key at <paramref name="index"/>. Refuses when at or below <see cref="MinKeys"/>.</summary>
        public bool RemoveAlphaKey(int index)
        {
            bool removed = KeyArray<AlphaKey>.RemoveAt(ref alphaKeys, index, MinKeys);
            if (removed)
                version++;
            return removed;
        }

        /// <summary>
        /// Replaces the colour key at <paramref name="index"/> and re-sorts. Returns the key's new index.
        /// Writing back an identical key is a no-op and does not bump <see cref="Version"/>.
        /// </summary>
        public int SetColorKey(int index, ColorKey key)
        {
            var replacement = key.WithTime(Mathf.Clamp01(key.time));
            var existing = colorKeys[index];
            if (existing.time == replacement.time && existing.color == replacement.color)
                return index;

            int newIndex = KeyArray<ColorKey>.SetAndResort(colorKeys, index, replacement);
            version++;
            return newIndex;
        }

        /// <summary>
        /// Replaces the alpha key at <paramref name="index"/> and re-sorts. Returns the key's new index.
        /// Writing back an identical key is a no-op and does not bump <see cref="Version"/>.
        /// </summary>
        public int SetAlphaKey(int index, AlphaKey key)
        {
            var replacement = key.WithTime(Mathf.Clamp01(key.time));
            var existing = alphaKeys[index];
            if (existing.time == replacement.time && existing.alpha == replacement.alpha)
                return index;

            int newIndex = KeyArray<AlphaKey>.SetAndResort(alphaKeys, index, replacement);
            version++;
            return newIndex;
        }

        /// <summary>
        /// Clamps a candidate time for the colour key at <paramref name="index"/> into the gap between the
        /// keys bracketing it. The key may still be dragged past its neighbours into another gap; it just
        /// cannot land exactly on another key's time.
        /// </summary>
        public float ClampColorKeyTime(int index, float candidateTime) =>
            KeyArray<ColorKey>.ClampTimeIntoGap(colorKeys, index, candidateTime, NeighbourClampEpsilon);

        /// <summary>
        /// Clamps a candidate time for the alpha key at <paramref name="index"/> into the gap between the
        /// keys bracketing it. See <see cref="ClampColorKeyTime"/> for the reordering caveat.
        /// </summary>
        public float ClampAlphaKeyTime(int index, float candidateTime) =>
            KeyArray<AlphaKey>.ClampTimeIntoGap(alphaKeys, index, candidateTime, NeighbourClampEpsilon);

        /// <summary>
        /// Replaces both key arrays: validates, sorts, decimates evenly down to <see cref="MaxKeys"/> when
        /// over-long, and pads out to <see cref="MinKeys"/> when short. Only genuinely empty input falls
        /// back to the default black-to-white ramp.
        /// </summary>
        public void SetKeys(ReadOnlySpan<ColorKey> colors, ReadOnlySpan<AlphaKey> alphas)
        {
            colorKeys = ValidateColorKeys(colors.ToArray());
            alphaKeys = ValidateAlphaKeys(alphas.ToArray());
            version++;
        }

        public void FlipKeys()
        {
            KeyArray<ColorKey>.FlipTimes(colorKeys);
            KeyArray<AlphaKey>.FlipTimes(alphaKeys);
            version++;
        }

        public void DistributeColorKeysEvenly()
        {
            KeyArray<ColorKey>.Distribute(colorKeys);
            version++;
        }

        public void DistributeAlphaKeysEvenly()
        {
            KeyArray<AlphaKey>.Distribute(alphaKeys);
            version++;
        }

        /// <summary>Evaluates the gradient at <paramref name="t"/>, applying modulation unless bypassed or at its identity value.</summary>
        public Color Evaluate(float t) => Evaluate(t, applyModulation: true);

        /// <summary>Evaluates the gradient at <paramref name="t"/> ignoring modulation entirely.</summary>
        public Color EvaluateBase(float t) => Evaluate(t, applyModulation: false);

        private Color Evaluate(float t, bool applyModulation)
        {
            t = Mathf.Clamp01(t);
            var c = applyModulation
                ? GradientMath.Evaluate(in Native, t)
                : GradientMath.EvaluateBase(in Native, t);
            return new Color(c.x, c.y, c.z, c.w);
        }

    }
}
