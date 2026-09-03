using UnityEngine;

namespace ABCodeworld.Gradients.Editor
{
    /// <summary>A flat sequence of colours to sample from — a mesh's vertex colours or a texture's pixels.</summary>
    internal interface IColorSource
    {
        int Count { get; }
        Color32 this[int index] { get; }
    }
}
