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
    /// </remarks>
    [Serializable]
    public sealed class GradientABCW : ISerializationCallbackReceiver
    {
        public const int MinKeys = 2;
        public const int MaxKeys = 32;

        private const float NeighbourClampEpsilon = 1e-3f;

        [SerializeField] private ColorKey[] colorKeys;
        [SerializeField] private AlphaKey[] alphaKeys;
        [SerializeField] private BlendMode blendMode;
        [SerializeField] private GradientModulation modulation;

        [NonSerialized] private int version;

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

        /// <summary>Bumped on every mutation; caches key their state on this to know when to rebuild.</summary>
        public int Version => version;

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
                modulation = value.Clamped();
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

        /// <summary>Replaces the colour key at <paramref name="index"/> and re-sorts. Returns the key's new index.</summary>
        public int SetColorKey(int index, ColorKey key)
        {
            int newIndex = KeyArray<ColorKey>.SetAndResort(colorKeys, index, key.WithTime(Mathf.Clamp01(key.time)));
            version++;
            return newIndex;
        }

        /// <summary>Replaces the alpha key at <paramref name="index"/> and re-sorts. Returns the key's new index.</summary>
        public int SetAlphaKey(int index, AlphaKey key)
        {
            int newIndex = KeyArray<AlphaKey>.SetAndResort(alphaKeys, index, key.WithTime(Mathf.Clamp01(key.time)));
            version++;
            return newIndex;
        }

        /// <summary>Clamps a candidate time for the colour key at <paramref name="index"/> against its neighbours.</summary>
        public float ClampColorKeyTime(int index, float candidateTime) =>
            KeyArray<ColorKey>.ClampTimeBetweenNeighbours(colorKeys, index, candidateTime, NeighbourClampEpsilon);

        /// <summary>Clamps a candidate time for the alpha key at <paramref name="index"/> against its neighbours.</summary>
        public float ClampAlphaKeyTime(int index, float candidateTime) =>
            KeyArray<AlphaKey>.ClampTimeBetweenNeighbours(alphaKeys, index, candidateTime, NeighbourClampEpsilon);

        /// <summary>Replaces both key arrays, validating, sorting, truncating to <see cref="MaxKeys"/> and padding to <see cref="MinKeys"/>.</summary>
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
            bool modulate = applyModulation && modulation.IsEffective;

            float sampleT = modulate ? GradientMathTemp.TransformT(modulation, t) : t;
            Color c = blendMode == BlendMode.Stepped
                ? GradientMathTemp.SampleStepped(colorKeys, alphaKeys, sampleT)
                : GradientMathTemp.SampleSmooth(colorKeys, alphaKeys, sampleT);

            if (modulate)
                c = GradientMathTemp.ApplyHsba(modulation, c);

            return c;
        }

        /// <summary>Deterministic content hash covering keys, blend mode and modulation. Does not depend on <see cref="Version"/>.</summary>
        public int ComputeContentHash()
        {
            unchecked
            {
                int h = 17;
                h = h * 31 + colorKeys.Length;
                for (int i = 0; i < colorKeys.Length; i++)
                {
                    var k = colorKeys[i];
                    h = h * 31 + BitConverter.SingleToInt32Bits(k.time);
                    h = h * 31 + BitConverter.SingleToInt32Bits(k.color.r);
                    h = h * 31 + BitConverter.SingleToInt32Bits(k.color.g);
                    h = h * 31 + BitConverter.SingleToInt32Bits(k.color.b);
                }
                h = h * 31 + alphaKeys.Length;
                for (int i = 0; i < alphaKeys.Length; i++)
                {
                    var k = alphaKeys[i];
                    h = h * 31 + BitConverter.SingleToInt32Bits(k.time);
                    h = h * 31 + BitConverter.SingleToInt32Bits(k.alpha);
                }
                h = h * 31 + (int)blendMode;
                h = h * 31 + modulation.bypass.GetHashCode();
                h = h * 31 + modulation.reverse.GetHashCode();
                h = h * 31 + BitConverter.SingleToInt32Bits(modulation.repeats);
                h = h * 31 + (int)modulation.repeatMode;
                h = h * 31 + BitConverter.SingleToInt32Bits(modulation.offset);
                h = h * 31 + BitConverter.SingleToInt32Bits(modulation.hueShift);
                h = h * 31 + BitConverter.SingleToInt32Bits(modulation.saturation);
                h = h * 31 + BitConverter.SingleToInt32Bits(modulation.brightness);
                h = h * 31 + BitConverter.SingleToInt32Bits(modulation.alpha);
                return h;
            }
        }

        public bool ContentEquals(GradientABCW other)
        {
            if (other is null)
                return false;
            if (blendMode != other.blendMode)
                return false;
            if (!ModulationEquals(modulation, other.modulation))
                return false;
            if (colorKeys.Length != other.colorKeys.Length || alphaKeys.Length != other.alphaKeys.Length)
                return false;

            for (int i = 0; i < colorKeys.Length; i++)
            {
                if (colorKeys[i].time != other.colorKeys[i].time || colorKeys[i].color != other.colorKeys[i].color)
                    return false;
            }
            for (int i = 0; i < alphaKeys.Length; i++)
            {
                if (alphaKeys[i].time != other.alphaKeys[i].time || alphaKeys[i].alpha != other.alphaKeys[i].alpha)
                    return false;
            }
            return true;
        }

        private static bool ModulationEquals(in GradientModulation a, in GradientModulation b) =>
            a.bypass == b.bypass && a.reverse == b.reverse && a.repeats == b.repeats && a.repeatMode == b.repeatMode &&
            a.offset == b.offset && a.hueShift == b.hueShift && a.saturation == b.saturation &&
            a.brightness == b.brightness && a.alpha == b.alpha;

        void ISerializationCallbackReceiver.OnBeforeSerialize() { }

        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            colorKeys = ValidateColorKeys(colorKeys);
            alphaKeys = ValidateAlphaKeys(alphaKeys);
            modulation = modulation.Clamped();
            version++;
        }

        private static ColorKey[] ValidateColorKeys(ColorKey[] keys)
        {
            if (keys == null || keys.Length < MinKeys)
                return DefaultColorKeys();

            if (keys.Length > MaxKeys)
            {
                KeyArray<ColorKey>.SortInPlace(keys);
                Array.Resize(ref keys, MaxKeys);
            }

            for (int i = 0; i < keys.Length; i++)
                keys[i] = new ColorKey(keys[i].color, keys[i].time);

            KeyArray<ColorKey>.SortInPlace(keys);
            return keys;
        }

        private static AlphaKey[] ValidateAlphaKeys(AlphaKey[] keys)
        {
            if (keys == null || keys.Length < MinKeys)
                return DefaultAlphaKeys();

            if (keys.Length > MaxKeys)
            {
                KeyArray<AlphaKey>.SortInPlace(keys);
                Array.Resize(ref keys, MaxKeys);
            }

            for (int i = 0; i < keys.Length; i++)
                keys[i] = new AlphaKey(keys[i].alpha, keys[i].time);

            KeyArray<AlphaKey>.SortInPlace(keys);
            return keys;
        }

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
