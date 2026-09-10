using System;

namespace ABCodeworld.Gradients
{
    /// <summary>
    /// Content identity: a hash and an equality check that both look only at what the gradient is,
    /// never at <see cref="Version"/>, which counts edits rather than describing them.
    /// </summary>
    public sealed partial class GradientABCW3D
    {
        /// <summary>
        /// Deterministic content hash covering keys, blend mode, falloff power and modulation. Does not
        /// depend on <see cref="Version"/>.
        /// </summary>
        /// <remarks>
        /// Key order is part of the hash, as it is for the 1D gradient. That is not merely convenient:
        /// two keys sharing a position resolve to the lower-indexed one exactly at that position, so a
        /// reordering of the array can genuinely change what the gradient evaluates to.
        /// </remarks>
        public int ComputeContentHash()
        {
            unchecked
            {
                int h = 17;
                h = h * 31 + colorKeys.Length;
                for (int i = 0; i < colorKeys.Length; i++)
                {
                    var k = colorKeys[i];
                    h = h * 31 + BitConverter.SingleToInt32Bits(k.position.x);
                    h = h * 31 + BitConverter.SingleToInt32Bits(k.position.y);
                    h = h * 31 + BitConverter.SingleToInt32Bits(k.position.z);
                    h = h * 31 + BitConverter.SingleToInt32Bits(k.color.r);
                    h = h * 31 + BitConverter.SingleToInt32Bits(k.color.g);
                    h = h * 31 + BitConverter.SingleToInt32Bits(k.color.b);
                }
                h = h * 31 + alphaKeys.Length;
                for (int i = 0; i < alphaKeys.Length; i++)
                {
                    var k = alphaKeys[i];
                    h = h * 31 + BitConverter.SingleToInt32Bits(k.position.x);
                    h = h * 31 + BitConverter.SingleToInt32Bits(k.position.y);
                    h = h * 31 + BitConverter.SingleToInt32Bits(k.position.z);
                    h = h * 31 + BitConverter.SingleToInt32Bits(k.alpha);
                }
                h = h * 31 + (int)blendMode;
                h = h * 31 + BitConverter.SingleToInt32Bits(falloffPower);
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

        public bool ContentEquals(GradientABCW3D other)
        {
            if (other is null)
                return false;
            if (blendMode != other.blendMode)
                return false;
            if (falloffPower != other.falloffPower)
                return false;
            if (!modulation.Equals(other.modulation))
                return false;
            if (colorKeys.Length != other.colorKeys.Length || alphaKeys.Length != other.alphaKeys.Length)
                return false;

            for (int i = 0; i < colorKeys.Length; i++)
            {
                if (!ExactlyEqual(colorKeys[i].position, other.colorKeys[i].position) || !ExactlyEqual(colorKeys[i].color, other.colorKeys[i].color))
                    return false;
            }
            for (int i = 0; i < alphaKeys.Length; i++)
            {
                if (!ExactlyEqual(alphaKeys[i].position, other.alphaKeys[i].position) || alphaKeys[i].alpha != other.alphaKeys[i].alpha)
                    return false;
            }
            return true;
        }
    }
}
