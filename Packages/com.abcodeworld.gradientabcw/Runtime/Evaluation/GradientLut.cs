using System;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace ABCodeworld.Gradients
{
    /// <summary>Options controlling how a gradient LUT or texture is baked.</summary>
    public struct GradientLutOptions
    {
        /// <summary>True = final (modulated) output, false = base output ignoring modulation.</summary>
        public bool includeModulation;

        /// <summary>Convert sRGB (authoring space) to linear per channel.</summary>
        public bool toLinear;

        public static readonly GradientLutOptions Base = new GradientLutOptions { includeModulation = false };
        public static readonly GradientLutOptions Final = new GradientLutOptions { includeModulation = true };

        /// <summary>Final or base output, converted to linear only when the active colour space requires it.</summary>
        public static GradientLutOptions Project(bool final) => new GradientLutOptions
        {
            includeModulation = final,
            toLinear = GradientTextureUtility.ProjectIsLinear,
        };
    }

    /// <summary>Bakes a gradient into a lookup table, on the managed thread or across a job.</summary>
    public static class GradientLut
    {
        // Both managed entry points go through gradient.Native rather than NativeGradient.From: the
        // snapshot is already cached against GradientABCW.Version, so rebuilding it per bake threw away
        // the cache this type exists to exploit and rebuilt ~800 bytes of key data on every preview
        // repaint.
        /// <summary>
        /// Bakes into managed memory. A <see cref="Span{T}"/> cannot be handed to a job, so anything large
        /// enough is staged through a temporary native array and copied back.
        /// </summary>
        /// <remarks>
        /// The staging allocation and copy are not free, but they are nowhere near the cost they save:
        /// measured against the straight managed loop, staging is 9x faster at 256 entries, 30x at 4096
        /// and 146x at 65536.
        /// </remarks>
        public static void Bake(GradientABCW gradient, Span<Color32> dst, GradientLutOptions options)
        {
            ref readonly var native = ref gradient.Native;
            int count = dst.Length;

            if (count >= BurstBakeThreshold)
            {
                using var staging = new NativeArray<Color32>(count, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                new BakeLutColor32Job { Gradient = native, Options = options, Count = count, Result = staging }
                    .Schedule(count, DefaultBatchSize)
                    .Complete();
                staging.AsReadOnlySpan().CopyTo(dst);
                return;
            }

            for (int i = 0; i < count; i++)
            {
                float t = count > 1 ? (float)i / (count - 1) : 0f;
                dst[i] = ToColor32(SampleAt(in native, t, in options));
            }
        }

        public static void Bake(GradientABCW gradient, NativeArray<Color32> dst, GradientLutOptions options) =>
            Bake(in gradient.Native, dst, options);

        /// <summary>
        /// Smallest LUT worth handing to Burst. Below this, job dispatch dominates; above it, Burst wins
        /// by a margin wide enough that there is no reason to run the managed loop.
        /// </summary>
        /// <remarks>
        /// Measured, not guessed. With Burst warm, dispatch costs a roughly flat 5-15us while the managed
        /// loop costs about 0.7us per entry, so the true crossover sits around 8 entries: 4.0x at 32
        /// entries, 7.1x at 64, 10.7x at 256, 11.5x at 4096, 182x at 65536. The threshold is set well
        /// above the crossover because the margin below it is worth nothing in absolute terms (every size
        /// under 32 completes in under 25us either way) and because Burst compiles asynchronously, so the
        /// first bake of a session can fall back to a slower interpreted path. See GradientBakeBenchmarks.
        /// </remarks>
        internal const int BurstBakeThreshold = 32;

        internal const int DefaultBatchSize = 64;

        /// <summary>
        /// Bakes into a native array, using Burst for anything large enough to be worth scheduling.
        /// </summary>
        public static void Bake(in NativeGradient native, NativeArray<Color32> dst, GradientLutOptions options)
        {
            int count = dst.Length;
            if (count >= BurstBakeThreshold)
            {
                new BakeLutColor32Job { Gradient = native, Options = options, Count = count, Result = dst }
                    .Schedule(count, DefaultBatchSize)
                    .Complete();
                return;
            }

            BakeManaged(in native, dst, options);
        }

        /// <summary>The scalar fallback, for LUTs too small to be worth a job.</summary>
        internal static void BakeManaged(in NativeGradient native, NativeArray<Color32> dst, GradientLutOptions options)
        {
            int count = dst.Length;
            for (int i = 0; i < count; i++)
            {
                float t = count > 1 ? (float)i / (count - 1) : 0f;
                dst[i] = ToColor32(SampleAt(in native, t, in options));
            }
        }

        internal static void Bake(in NativeGradient native, NativeArray<float4> dst, GradientLutOptions options)
        {
            int count = dst.Length;
            for (int i = 0; i < count; i++)
            {
                float t = count > 1 ? (float)i / (count - 1) : 0f;
                dst[i] = SampleAt(in native, t, in options);
            }
        }

        internal static float4 SampleAt(in NativeGradient native, float t, in GradientLutOptions options)
        {
            float4 c = options.includeModulation ? GradientMath.Evaluate(in native, t) : GradientMath.EvaluateBase(in native, t);
            if (options.toLinear)
                c = new float4(ColorSpaceMath.SrgbToLinear(c.xyz), c.w);
            return c;
        }

        internal static Color32 ToColor32(float4 c) => new Color32(
            (byte)(math.saturate(c.x) * 255f + 0.5f),
            (byte)(math.saturate(c.y) * 255f + 0.5f),
            (byte)(math.saturate(c.z) * 255f + 0.5f),
            (byte)(math.saturate(c.w) * 255f + 0.5f));
    }
}
