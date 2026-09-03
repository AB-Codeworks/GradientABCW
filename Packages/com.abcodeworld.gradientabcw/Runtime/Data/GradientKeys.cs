using System;
using UnityEngine;

namespace ABCodeworld.Gradients
{
    /// <summary>A gradient key with a normalized time. Implemented by <see cref="ColorKey"/> and <see cref="AlphaKey"/>.</summary>
    internal interface IGradientKey<TSelf> where TSelf : struct, IGradientKey<TSelf>
    {
        float Time { get; }
        TSelf WithTime(float time);
    }

    /// <summary>A colour at a normalized time. The colour's alpha channel is ignored; alpha comes from <see cref="AlphaKey"/>.</summary>
    [Serializable]
    public struct ColorKey : IGradientKey<ColorKey>
    {
        public Color color;
        [Range(0f, 1f)] public float time;

        public ColorKey(Color color, float time)
        {
            color.a = 1f;
            this.color = color;
            this.time = Mathf.Clamp01(time);
        }

        public readonly float Time => time;

        public readonly ColorKey WithTime(float newTime)
        {
            var copy = this;
            copy.time = Mathf.Clamp01(newTime);
            return copy;
        }
    }

    /// <summary>An alpha value at a normalized time.</summary>
    [Serializable]
    public struct AlphaKey : IGradientKey<AlphaKey>
    {
        [Range(0f, 1f)] public float alpha;
        [Range(0f, 1f)] public float time;

        public AlphaKey(float alpha, float time)
        {
            this.alpha = Mathf.Clamp01(alpha);
            this.time = Mathf.Clamp01(time);
        }

        public readonly float Time => time;

        public readonly AlphaKey WithTime(float newTime)
        {
            var copy = this;
            copy.time = Mathf.Clamp01(newTime);
            return copy;
        }
    }

    /// <summary>
    /// Shared, allocation-conscious operations over a sorted array of gradient keys.
    /// Used by <see cref="GradientABCW"/> for both <see cref="ColorKey"/> and <see cref="AlphaKey"/> arrays
    /// so the insert/remove/sort/distribute/flip/clamp logic exists exactly once.
    /// </summary>
    internal static class KeyArray<TKey> where TKey : struct, IGradientKey<TKey>
    {
        public static bool IsSorted(TKey[] keys)
        {
            for (int i = 1; i < keys.Length; i++)
            {
                if (keys[i].Time < keys[i - 1].Time)
                    return false;
            }
            return true;
        }

        public static void SortInPlace(TKey[] keys)
        {
            if (!IsSorted(keys))
                Array.Sort(keys, (a, b) => a.Time.CompareTo(b.Time));
        }

        /// <summary>Inserts <paramref name="key"/> in sorted position. Returns -1 without mutating when already at <paramref name="maxCount"/>.</summary>
        public static int InsertSorted(ref TKey[] keys, TKey key, int maxCount)
        {
            if (keys.Length >= maxCount)
                return -1;

            var next = new TKey[keys.Length + 1];
            Array.Copy(keys, next, keys.Length);
            next[keys.Length] = key;
            SortInPlace(next);
            keys = next;
            return IndexOfTime(keys, key.Time);
        }

        /// <summary>Removes the key at <paramref name="index"/>. Returns false without mutating when at or below <paramref name="minCount"/>.</summary>
        public static bool RemoveAt(ref TKey[] keys, int index, int minCount)
        {
            if (keys.Length <= minCount || index < 0 || index >= keys.Length)
                return false;

            var next = new TKey[keys.Length - 1];
            Array.Copy(keys, 0, next, 0, index);
            Array.Copy(keys, index + 1, next, index, keys.Length - index - 1);
            keys = next;
            return true;
        }

        /// <summary>Replaces the key at <paramref name="index"/> and re-sorts. Returns the key's new index.</summary>
        public static int SetAndResort(TKey[] keys, int index, TKey key)
        {
            keys[index] = key;
            SortInPlace(keys);
            return IndexOfTime(keys, key.Time);
        }

        public static void Distribute(TKey[] keys)
        {
            if (keys.Length <= 1)
                return;

            for (int i = 0; i < keys.Length; i++)
                keys[i] = keys[i].WithTime((float)i / (keys.Length - 1));
        }

        /// <summary>Flips every key's time (1 - t) and re-sorts (which reverses key order).</summary>
        public static void FlipTimes(TKey[] keys)
        {
            for (int i = 0; i < keys.Length; i++)
                keys[i] = keys[i].WithTime(1f - keys[i].Time);
            SortInPlace(keys);
        }

        /// <summary>Clamps a candidate time for the key at <paramref name="index"/> so it cannot cross either neighbour.</summary>
        public static float ClampTimeBetweenNeighbours(TKey[] keys, int index, float candidateTime, float epsilon)
        {
            float min = epsilon, max = 1f - epsilon;
            for (int i = 0; i < keys.Length; i++)
            {
                if (i == index)
                    continue;

                float t = keys[i].Time;
                if (t <= candidateTime)
                    min = Mathf.Max(min, t + epsilon);
                else
                    max = Mathf.Min(max, t - epsilon);
            }
            return Mathf.Clamp(candidateTime, min, max);
        }

        private static int IndexOfTime(TKey[] keys, float time)
        {
            for (int i = 0; i < keys.Length; i++)
            {
                if (Mathf.Approximately(keys[i].Time, time))
                    return i;
            }
            return -1;
        }
    }
}
