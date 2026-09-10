using System;
using UnityEngine;

namespace ABCodeworld.Gradients
{
    /// <summary>
    /// A gradient key positioned inside the unit cube. Implemented by <see cref="ColorKey3D"/> and
    /// <see cref="AlphaKey3D"/>.
    /// </summary>
    /// <remarks>
    /// The 1D <c>IGradientKey&lt;TSelf&gt;</c> exposes a single <c>Time</c>, and everything built on it —
    /// sorting, bracketing, insertion order — assumes that one scalar orders the keys. Points in a cube
    /// have no such order, so this is a separate interface rather than a generalisation of that one.
    /// </remarks>
    internal interface IGradientKey3D<TSelf> where TSelf : struct, IGradientKey3D<TSelf>
    {
        Vector3 Position { get; }
        TSelf WithPosition(Vector3 position);

        /// <summary>
        /// A copy with every field forced back into its valid range, as the key's own constructor does.
        /// Lets validation run generically over both key kinds instead of once per kind.
        /// </summary>
        TSelf Normalized();
    }

    /// <summary>
    /// A colour at a position in the unit cube. The colour's alpha channel is ignored; alpha comes from
    /// <see cref="AlphaKey3D"/>.
    /// </summary>
    [Serializable]
    public struct ColorKey3D : IGradientKey3D<ColorKey3D>
    {
        public Color color;
        public Vector3 position;

        public ColorKey3D(Color color, Vector3 position)
        {
            color.a = 1f;
            this.color = color;
            this.position = ClampToUnitCube(position);
        }

        public readonly Vector3 Position => position;

        public readonly ColorKey3D WithPosition(Vector3 newPosition)
        {
            var copy = this;
            copy.position = ClampToUnitCube(newPosition);
            return copy;
        }

        public readonly ColorKey3D Normalized() => new ColorKey3D(color, position);

        /// <summary>Clamps each axis into [0, 1] independently.</summary>
        internal static Vector3 ClampToUnitCube(Vector3 p) => new Vector3(
            Mathf.Clamp01(p.x), Mathf.Clamp01(p.y), Mathf.Clamp01(p.z));
    }

    /// <summary>An alpha value at a position in the unit cube.</summary>
    [Serializable]
    public struct AlphaKey3D : IGradientKey3D<AlphaKey3D>
    {
        [Range(0f, 1f)] public float alpha;
        public Vector3 position;

        public AlphaKey3D(float alpha, Vector3 position)
        {
            this.alpha = Mathf.Clamp01(alpha);
            this.position = ColorKey3D.ClampToUnitCube(position);
        }

        public readonly Vector3 Position => position;

        public readonly AlphaKey3D WithPosition(Vector3 newPosition)
        {
            var copy = this;
            copy.position = ColorKey3D.ClampToUnitCube(newPosition);
            return copy;
        }

        public readonly AlphaKey3D Normalized() => new AlphaKey3D(alpha, position);
    }
}
