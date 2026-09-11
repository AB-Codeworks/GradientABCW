using UnityEngine;

namespace ABCodeworld.Gradients.Editor
{
    /// <summary>A colour source whose entries also carry a position inside the unit cube.</summary>
    /// <remarks>
    /// Extends <see cref="IColorSource"/> by composition rather than widening it. A texture's pixels have
    /// no position in a cube, and forcing <see cref="TextureColorSource"/> to invent one would put that
    /// decision in the wrong place — it belongs to whoever is building a 3D gradient, not to the thing
    /// reading pixels.
    /// </remarks>
    internal interface IPositionedColorSource : IColorSource
    {
        /// <summary>Position of entry <paramref name="index"/>, already normalized into [0, 1] per axis.</summary>
        Vector3 GetPosition(int index);
    }
}
