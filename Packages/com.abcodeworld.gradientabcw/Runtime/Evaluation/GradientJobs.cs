using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

// ScheduleBake / ScheduleEvaluate live on GradientLut (see GradientLut.cs); this file holds
// only the job struct implementations that back them.

namespace ABCodeworld.Gradients
{
    [BurstCompile]
    internal struct BakeLutJob : IJobParallelFor
    {
        public NativeGradient Gradient;
        public GradientLutOptions Options;
        public int Count;
        [WriteOnly] public NativeArray<float4> Result;

        public void Execute(int index)
        {
            float t = Count > 1 ? (float)index / (Count - 1) : 0f;
            Result[index] = GradientLut.SampleAt(in Gradient, t, in Options);
        }
    }

    [BurstCompile]
    internal struct EvaluateBatchJob : IJobParallelFor
    {
        public NativeGradient Gradient;
        [ReadOnly] public NativeArray<float> Times;
        public bool IncludeModulation;
        [WriteOnly] public NativeArray<float4> Results;

        public void Execute(int index)
        {
            float t = Times[index];
            Results[index] = IncludeModulation ? GradientMath.Evaluate(in Gradient, t) : GradientMath.EvaluateBase(in Gradient, t);
        }
    }
}
