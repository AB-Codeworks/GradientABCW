namespace ABCodeworld.Gradients
{
    /// <summary>How a gradient interpolates between neighbouring keys.</summary>
    public enum BlendMode
    {
        /// <summary>Linear interpolation between neighbouring keys.</summary>
        Smooth = 0,

        /// <summary>Flat colour per key, switching at the midpoint between neighbours.</summary>
        Stepped = 1,
    }

    /// <summary>How the evaluation domain maps back into [0, 1] when <c>repeats</c> is not 1.</summary>
    public enum RepeatMode
    {
        /// <summary>Values beyond the domain clamp to the last key.</summary>
        Clamp = 0,

        /// <summary>Values beyond the domain wrap around (frac).</summary>
        Wrap = 1,

        /// <summary>Values beyond the domain ping-pong (reflect).</summary>
        Mirror = 2,
    }
}
