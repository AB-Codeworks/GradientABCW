using System;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace ABCodeworld.Gradients
{
    /// <summary>
    /// Bakes a 3D gradient into a volumetric lookup table, on the managed thread or across a job, and
    /// samples one back with trilinear filtering.
    /// </summary>
    /// <remarks>
    /// The table is a flat array of <paramref name="size"/> cubed cells, indexed x-fastest — see
    /// <see cref="VoxelIndex"/>. That is deliberately the layout <see cref="Texture3D"/> expects from
    /// <c>GetPixelData</c>, so a baked table uploads to the GPU with no reshuffling at all.
    /// <para>
    /// At the default size of 32 that is 32768 cells, 128 KB as 8-bit colour, and granularity comes from
    /// filtering rather than from resolution: <see cref="SampleTrilinear"/> on the CPU, or a
    /// <see cref="Texture3D"/>'s own hardware filtering on the GPU, where it costs nothing. Raising the
    /// size to buy smoothness instead would grow the table cubically for the same result.
    /// </para>
    /// <para>
    /// Cells map onto the cube by <c>i / (size - 1)</c> per axis, so the first and last cell of each axis
    /// sit exactly on 0 and 1 and a baked table agrees with <see cref="GradientABCW3D.Evaluate"/> at the
    /// domain boundary. This matches the 1D <see cref="GradientLut"/>'s <c>t = i / (count - 1)</c>. It is
    /// not the half-texel convention a GPU samples with, so a shader reading the baked
    /// <see cref="Texture3D"/> needs the scale and bias from <see cref="ShaderScaleOffset"/>.
    /// </para>
    /// </remarks>
    public static class GradientLut3D
    {
        /// <summary>32 cubed: 32768 cells, 128 KB as <see cref="Color32"/>.</summary>
        public const int DefaultSize = 32;

        public const int MinSize = 2;

        /// <summary>128 cubed is 8 MB. Past that a caller almost certainly wants a different structure.</summary>
        public const int MaxSize = 128;

        /// <summary>
        /// Smallest bake worth handing to Burst, in cells. Shares the 1D threshold, which was measured
        /// against a far cheaper inner loop and is therefore conservative here: a 3D cell costs a pass
        /// over every key, so the crossover sits lower still. Every bake at <see cref="MinSize"/> or above
        /// clears it anyway, since 2 cubed is 8 and 3 cubed is 27 — only a size-2 bake takes the managed
        /// loop, and that is 8 cells.
        /// </summary>
        internal const int BurstBakeThreshold = GradientLut.BurstBakeThreshold;

        internal const int DefaultBatchSize = GradientLut.DefaultBatchSize;

        /// <summary>Cells in a table of the given size.</summary>
        public static int VoxelCount(int size) => size * size * size;

        /// <summary>
        /// Flat index of the cell at <paramref name="x"/>, <paramref name="y"/>, <paramref name="z"/>.
        /// X varies fastest, matching <see cref="Texture3D"/>'s own layout.
        /// </summary>
        public static int VoxelIndex(int x, int y, int z, int size) => x + size * (y + size * z);

        /// <summary>Position in the unit cube that the cell at <paramref name="index"/> samples.</summary>
        internal static float3 VoxelPosition(int index, int size)
        {
            int x = index % size;
            int y = (index / size) % size;
            int z = index / (size * size);
            float scale = size > 1 ? 1f / (size - 1) : 0f;
            return new float3(x * scale, y * scale, z * scale);
        }

        /// <summary>
        /// Scale and bias a shader must apply to a position in the unit cube before sampling the
        /// <see cref="Texture3D"/> this class bakes: <c>uvw = p * result.x + result.y</c>.
        /// </summary>
        /// <remarks>
        /// The bake puts cell 0 at p = 0 and cell size-1 at p = 1, while a GPU samples cell centres at
        /// <c>(i + 0.5) / size</c>. Without this correction a shader reads half a cell off along every
        /// axis, which at size 32 is a visible shift. Returned as a <see cref="Vector4"/> so it can go
        /// straight into a material property; z and w are zero.
        /// </remarks>
        public static Vector4 ShaderScaleOffset(int size) =>
            new Vector4((size - 1f) / size, 0.5f / size, 0f, 0f);

        /// <summary>
        /// Bakes into managed memory. A <see cref="Span{T}"/> cannot be handed to a job, so anything large
        /// enough is staged through a temporary native array and copied back — the same trade the 1D bake
        /// makes, and for the same measured reason.
        /// </summary>
        public static void Bake(GradientABCW3D gradient, Span<Color32> dst, int size, GradientLutOptions options)
        {
            if (gradient is null)
                throw new ArgumentNullException(nameof(gradient));
            ValidateDestination(dst.Length, size);

            ref readonly var native = ref gradient.Native;
            int count = dst.Length;

            if (count >= BurstBakeThreshold)
            {
                using var staging = new NativeArray<Color32>(count, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                new BakeLut3DColor32Job { Gradient = native, Options = options, Size = size, Result = staging }
                    .Schedule(count, DefaultBatchSize)
                    .Complete();
                staging.AsReadOnlySpan().CopyTo(dst);
                return;
            }

            for (int i = 0; i < count; i++)
                dst[i] = GradientLut.ToColor32(SampleAt(in native, VoxelPosition(i, size), in options));
        }

        public static void Bake(GradientABCW3D gradient, NativeArray<Color32> dst, int size, GradientLutOptions options)
        {
            if (gradient is null)
                throw new ArgumentNullException(nameof(gradient));

            Bake(in gradient.Native, dst, size, options);
        }

        /// <summary>Bakes into a native array, using Burst for anything large enough to be worth scheduling.</summary>
        public static void Bake(in NativeGradient3D native, NativeArray<Color32> dst, int size, GradientLutOptions options)
        {
            ValidateDestination(dst.Length, size);

            if (dst.Length >= BurstBakeThreshold)
            {
                new BakeLut3DColor32Job { Gradient = native, Options = options, Size = size, Result = dst }
                    .Schedule(dst.Length, DefaultBatchSize)
                    .Complete();
                return;
            }

            BakeManaged(in native, dst, size, options);
        }

        /// <summary>The scalar fallback, for tables too small to be worth a job.</summary>
        internal static void BakeManaged(in NativeGradient3D native, NativeArray<Color32> dst, int size, GradientLutOptions options)
        {
            for (int i = 0; i < dst.Length; i++)
                dst[i] = GradientLut.ToColor32(SampleAt(in native, VoxelPosition(i, size), in options));
        }

        internal static void Bake(in NativeGradient3D native, NativeArray<float4> dst, int size, GradientLutOptions options)
        {
            ValidateDestination(dst.Length, size);

            for (int i = 0; i < dst.Length; i++)
                dst[i] = SampleAt(in native, VoxelPosition(i, size), in options);
        }

        internal static float4 SampleAt(in NativeGradient3D native, float3 p, in GradientLutOptions options)
        {
            float4 c = options.includeModulation ? GradientMath3D.Evaluate(in native, p) : GradientMath3D.EvaluateBase(in native, p);
            if (options.toLinear)
                c = new float4(ColorSpaceMath.SrgbToLinear(c.xyz), c.w);
            return c;
        }

        /// <summary>
        /// Reads <paramref name="lut"/> at any position in the unit cube, blending the eight surrounding
        /// cells. This is what makes a 32-cubed table granular enough to use directly.
        /// </summary>
        /// <remarks>
        /// Cells are converted to float before blending. Lerping the bytes themselves would quantise every
        /// intermediate step back to 1/255 and reintroduce exactly the banding the filtering is there to
        /// remove.
        /// </remarks>
        public static Color SampleTrilinear(ReadOnlySpan<Color32> lut, int size, Vector3 p)
        {
            float fx = Mathf.Clamp01(p.x) * (size - 1);
            float fy = Mathf.Clamp01(p.y) * (size - 1);
            float fz = Mathf.Clamp01(p.z) * (size - 1);

            int x0 = (int)fx, y0 = (int)fy, z0 = (int)fz;

            // Clamping the upper cell rather than the coordinate is what keeps p == 1 working without a
            // special case: it lands on the last cell with a weight of zero towards a neighbour that is
            // itself.
            int x1 = Mathf.Min(x0 + 1, size - 1);
            int y1 = Mathf.Min(y0 + 1, size - 1);
            int z1 = Mathf.Min(z0 + 1, size - 1);

            float tx = fx - x0, ty = fy - y0, tz = fz - z0;

            var c000 = ToColor(lut[VoxelIndex(x0, y0, z0, size)]);
            var c100 = ToColor(lut[VoxelIndex(x1, y0, z0, size)]);
            var c010 = ToColor(lut[VoxelIndex(x0, y1, z0, size)]);
            var c110 = ToColor(lut[VoxelIndex(x1, y1, z0, size)]);
            var c001 = ToColor(lut[VoxelIndex(x0, y0, z1, size)]);
            var c101 = ToColor(lut[VoxelIndex(x1, y0, z1, size)]);
            var c011 = ToColor(lut[VoxelIndex(x0, y1, z1, size)]);
            var c111 = ToColor(lut[VoxelIndex(x1, y1, z1, size)]);

            var c00 = math.lerp(c000, c100, tx);
            var c10 = math.lerp(c010, c110, tx);
            var c01 = math.lerp(c001, c101, tx);
            var c11 = math.lerp(c011, c111, tx);

            var c0 = math.lerp(c00, c10, ty);
            var c1 = math.lerp(c01, c11, ty);

            var c = math.lerp(c0, c1, tz);
            return new Color(c.x, c.y, c.z, c.w);
        }

        /// <summary>Reads the single cell nearest <paramref name="p"/>, without blending.</summary>
        /// <remarks>Like <see cref="SampleTrilinear"/>, trusts <paramref name="size"/> to describe
        /// <paramref name="lut"/>. Both are per-sample entry points, so neither can afford to re-derive
        /// that; a mismatch surfaces as the span's own bounds check.</remarks>
        public static Color32 SampleNearest(ReadOnlySpan<Color32> lut, int size, Vector3 p)
        {
            int x = Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(p.x) * (size - 1)), 0, size - 1);
            int y = Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(p.y) * (size - 1)), 0, size - 1);
            int z = Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(p.z) * (size - 1)), 0, size - 1);
            return lut[VoxelIndex(x, y, z, size)];
        }

        private static float4 ToColor(Color32 c) =>
            new float4(c.r, c.g, c.b, c.a) * (1f / 255f);

        private static void ValidateDestination(int length, int size)
        {
            if (size < MinSize || size > MaxSize)
                throw new ArgumentOutOfRangeException(nameof(size), size, $"Size must be between {MinSize} and {MaxSize}.");
            if (length != VoxelCount(size))
                throw new ArgumentException($"A {size}-cubed table needs {VoxelCount(size)} cells, but {length} were supplied.");
        }
    }
}
