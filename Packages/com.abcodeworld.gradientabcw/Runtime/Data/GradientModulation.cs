using System;
using UnityEngine;

namespace ABCodeworld.Gradients
{
    /// <summary>
    /// Parameters applied only at evaluation time: they never mutate key data.
    /// Set <see cref="bypass"/> to skip modulation entirely (evaluation takes the cheap base path)
    /// without discarding the stored values, replacing the legacy static "Activate/Deactivate Mods" toggle.
    /// </summary>
    [Serializable]
    public struct GradientModulation : IEquatable<GradientModulation>
    {
        /// <summary>Skips modulation entirely without discarding the values below.</summary>
        public bool bypass;

        /// <summary>Samples the gradient back to front. For a 3D gradient this mirrors all three axes at
        /// once, which flips handedness rather than spinning the cube.</summary>
        public bool reverse;

        /// <summary>How many times the gradient tiles across the domain, under <see cref="repeatMode"/>.
        /// For a 3D gradient the count applies per axis, so 2 tiles the cube eight times over.</summary>
        [Min(1e-5f)] public float repeats;

        /// <summary>What happens past the end of one tile: hold, wrap round, or fold back.</summary>
        public RepeatMode repeatMode;

        /// <summary>Slides the sampling position along the domain before repeating is applied.</summary>
        [Range(0f, 1f)] public float offset;

        /// <summary>Hue shift in turns (-1..1 = a full spectrum shift backward/forward).</summary>
        [Range(-1f, 1f)] public float hueShift;

        /// <summary>-1 = greyscale, 0 = unchanged, 1 = fully (over)saturated.</summary>
        /// <remarks>A grey is left alone in both directions: it has no hue to widen or collapse, and the
        /// one <c>RgbToHsv</c> reports for it is an artefact of which channel rounded highest.</remarks>
        [Range(-1f, 1f)] public float saturation;

        /// <summary>-1 = black, 0 = unchanged, 1 = white.</summary>
        [Range(-1f, 1f)] public float brightness;

        /// <summary>-1 = transparent, 0 = unchanged, 1 = opaque.</summary>
        [Range(-1f, 1f)] public float alpha;

        /// <summary>Every parameter at its neutral value — modulation that changes nothing.</summary>
        public static GradientModulation Identity => new GradientModulation
        {
            bypass = false,
            reverse = false,
            repeats = 1f,
            repeatMode = RepeatMode.Clamp,
            offset = 0f,
            hueShift = 0f,
            saturation = 0f,
            brightness = 0f,
            alpha = 0f,
        };

        /// <summary>True when every parameter is at its neutral (no-op) value, ignoring <see cref="bypass"/>.</summary>
        public readonly bool IsIdentity =>
            !reverse &&
            Mathf.Abs(repeats - 1f) < 1e-6f &&
            repeatMode == RepeatMode.Clamp &&
            offset <= 1e-6f &&
            Mathf.Abs(hueShift) <= 1e-6f &&
            Mathf.Abs(saturation) <= 1e-6f &&
            Mathf.Abs(brightness) <= 1e-6f &&
            Mathf.Abs(alpha) <= 1e-6f;

        /// <summary>True when modulation actually changes evaluation output: not bypassed and not identity.</summary>
        public readonly bool IsEffective => !bypass && !IsIdentity;

        /// <summary>
        /// Exact field-by-field equality. Used to suppress no-op writes: a UI control re-sending the value
        /// it already holds must not bump <see cref="GradientABCW.Version"/>, or every cached LUT, native
        /// snapshot and preview texture downstream would rebuild for nothing.
        /// </summary>
        public readonly bool Equals(GradientModulation other) =>
            bypass == other.bypass && reverse == other.reverse && repeats == other.repeats &&
            repeatMode == other.repeatMode && offset == other.offset && hueShift == other.hueShift &&
            saturation == other.saturation && brightness == other.brightness && alpha == other.alpha;

        public readonly override bool Equals(object obj) => obj is GradientModulation other && Equals(other);

        public readonly override int GetHashCode() =>
            HashCode.Combine(
                HashCode.Combine(bypass, reverse, repeats, repeatMode),
                offset, hueShift, saturation, brightness, alpha);

        public static bool operator ==(GradientModulation a, GradientModulation b) => a.Equals(b);

        public static bool operator !=(GradientModulation a, GradientModulation b) => !a.Equals(b);

        /// <summary>Returns a copy with every field clamped to its valid range.</summary>
        public readonly GradientModulation Clamped()
        {
            var m = this;
            m.repeats = Mathf.Max(1e-5f, m.repeats);
            m.offset = Mathf.Clamp01(m.offset);
            m.hueShift = Mathf.Clamp(m.hueShift, -1f, 1f);
            m.saturation = Mathf.Clamp(m.saturation, -1f, 1f);
            m.brightness = Mathf.Clamp(m.brightness, -1f, 1f);
            m.alpha = Mathf.Clamp(m.alpha, -1f, 1f);
            if ((int)m.repeatMode < 0 || (int)m.repeatMode > 2)
                m.repeatMode = RepeatMode.Clamp;
            return m;
        }
    }
}
