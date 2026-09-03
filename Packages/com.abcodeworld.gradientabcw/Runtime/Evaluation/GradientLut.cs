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
        public static void Bake(GradientABCW gradient, Span<Color32> dst, GradientLutOptions options)
        {
            var native = NativeGradient.From(gradient);
            int count = dst.Length;
            for (int i = 0; i < count; i++)
            {
                float t = count > 1 ? (float)i / (count - 1) : 0f;
                dst[i] = ToColor32(SampleAt(in native, t, in options));
            }
        }

        public static void Bake(GradientABCW gradient, NativeArray<Color32> dst, GradientLutOptions options)
        {
            var native = NativeGradient.From(gradient);
            Bake(in native, dst, options);
        }

        public static void Bake(in NativeGradient native, NativeArray<Color32> dst, GradientLutOptions options)
        {
            int count = dst.Length;
            for (int i = 0; i < count; i++)
            {
                float t = count > 1 ? (float)i / (count - 1) : 0f;
                dst[i] = ToColor32(SampleAt(in native, t, in options));
            }
        }

        public static void Bake(in NativeGradient native, NativeArray<float4> dst, GradientLutOptions options)
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
                c = new float4(GradientMath.SrgbToLinear(c.xyz), c.w);
            return c;
        }

        public static JobHandle ScheduleBake(in NativeGradient native, NativeArray<float4> dst, GradientLutOptions options, JobHandle deps = default, int batch = 64)
        {
            var job = new BakeLutJob { Gradient = native, Options = options, Count = dst.Length, Result = dst };
            return job.Schedule(dst.Length, batch, deps);
        }

        public static JobHandle ScheduleEvaluate(in NativeGradient native, NativeArray<float> times, NativeArray<float4> results, bool includeModulation, JobHandle deps = default, int batch = 64)
        {
            var job = new EvaluateBatchJob { Gradient = native, Times = times, Results = results, IncludeModulation = includeModulation };
            return job.Schedule(times.Length, batch, deps);
        }

        internal static Color32 ToColor32(float4 c) => new Color32(
            (byte)(math.saturate(c.x) * 255f + 0.5f),
            (byte)(math.saturate(c.y) * 255f + 0.5f),
            (byte)(math.saturate(c.z) * 255f + 0.5f),
            (byte)(math.saturate(c.w) * 255f + 0.5f));
    }
}
