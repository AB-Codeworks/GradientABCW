using System;
using UnityEngine;

namespace ABCodeworld.Gradients
{
    /// <summary>A gradient key with a normalized time. Implemented by <see cref="ColorKey"/> and <see cref="AlphaKey"/>.</summary>
    internal interface IGradientKey<TSelf> where TSelf : struct, IGradientKey<TSelf>
    {
        float Time { get; }
        TSelf WithTime(float time);

        /// <summary>
        /// A copy with every field forced back into its valid range, as the key's own constructor does.
        /// Lets validation run generically over both key kinds instead of once per kind.
        /// </summary>
        TSelf Normalized();
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

        public readonly ColorKey Normalized() => new ColorKey(color, time);
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

        public readonly AlphaKey Normalized() => new AlphaKey(alpha, time);
    }

    /// <summary>
    /// Shared, allocation-conscious operations over a sorted array of gradient keys.
    /// Used by <see cref="GradientABCW"/> for both <see cref="ColorKey"/> and <see cref="AlphaKey"/> arrays
    /// so the insert/remove/sort/distribute/flip/clamp logic exists exactly once.
    /// </summary>
    /// <remarks>
    /// Ordering is stable: keys that share a time keep their relative order across every operation here.
    /// Callers depend on that, because an index returned by <see cref="InsertSorted"/> or
    /// <see cref="SetAndResort"/> must identify the key that was actually inserted or moved, which cannot
    /// be guaranteed if equal-time keys are free to swap. Every routine therefore positions elements by
    /// shifting them into place rather than delegating to Array.Sort, which is not a stable sort.
    /// </remarks>
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

        /// <summary>Stable insertion sort, chosen over Array.Sort for its stability as much as its speed at these sizes.</summary>
        public static void SortInPlace(TKey[] keys)
        {
            for (int i = 1; i < keys.Length; i++)
            {
                var current = keys[i];
                float t = current.Time;

                int j = i - 1;
                while (j >= 0 && keys[j].Time > t)
                {
                    keys[j + 1] = keys[j];
                    j--;
                }
                keys[j + 1] = current;
            }
        }

        /// <summary>
        /// Inserts <paramref name="key"/> in sorted position and returns the index it landed at.
        /// Returns -1 without mutating when already at <paramref name="maxCount"/>.
        /// </summary>
        /// <remarks>
        /// The index comes from the insertion position itself, never from searching the array for a
        /// matching time afterwards. Two keys are allowed to share a time, so a value search cannot tell
        /// them apart and would hand back the wrong one.
        /// </remarks>
        public static int InsertSorted(ref TKey[] keys, TKey key, int maxCount)
        {
            if (keys.Length >= maxCount)
                return -1;

            int index = UpperBound(keys, key.Time);

            var next = new TKey[keys.Length + 1];
            Array.Copy(keys, 0, next, 0, index);
            next[index] = key;
            Array.Copy(keys, index, next, index + 1, keys.Length - index);

            keys = next;
            return index;
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

        /// <summary>
        /// Replaces the key at <paramref name="index"/> and slides it back into sorted position.
        /// Returns the index the replaced key now occupies.
        /// </summary>
        /// <remarks>
        /// Only the replaced element moves, so its identity is tracked exactly: the returned index refers
        /// to <paramref name="key"/> itself even when other keys share its time. Dragging a key in the
        /// editor depends on this to keep hold of the key under the pointer.
        /// </remarks>
        public static int SetAndResort(TKey[] keys, int index, TKey key)
        {
            keys[index] = key;
            float t = key.Time;

            int i = index;
            while (i > 0 && keys[i - 1].Time > t)
            {
                keys[i] = keys[i - 1];
                i--;
            }
            while (i < keys.Length - 1 && keys[i + 1].Time < t)
            {
                keys[i] = keys[i + 1];
                i++;
            }

            keys[i] = key;
            return i;
        }

        public static void Distribute(TKey[] keys)
        {
            if (keys.Length <= 1)
                return;

            for (int i = 0; i < keys.Length; i++)
                keys[i] = keys[i].WithTime((float)i / (keys.Length - 1));
        }

        /// <summary>Flips every key's time (1 - t), which reverses their order.</summary>
        public static void FlipTimes(TKey[] keys)
        {
            for (int i = 0; i < keys.Length; i++)
                keys[i] = keys[i].WithTime(1f - keys[i].Time);

            // Flipping a sorted array yields a reversed sorted array, so reversing restores order exactly
            // and stably. Re-sorting here would be both slower and, before this change, order-scrambling.
            for (int i = 0, j = keys.Length - 1; i < j; i++, j--)
                (keys[i], keys[j]) = (keys[j], keys[i]);
        }

        /// <summary>
        /// Clamps a candidate time for the key at <paramref name="index"/> into the gap between the two
        /// keys that bracket <paramref name="candidateTime"/>, keeping it <paramref name="epsilon"/> clear
        /// of both.
        /// </summary>
        /// <remarks>
        /// This deliberately permits reordering: a key dragged past its neighbours settles into whichever
        /// gap the pointer is over, it simply may never land exactly on another key's time. That is what
        /// makes the index returned by <see cref="SetAndResort"/> meaningful during a drag. (The previous
        /// name and summary of this method claimed it stopped a key crossing its neighbours, which it has
        /// never done.)
        /// </remarks>
        public static float ClampTimeIntoGap(TKey[] keys, int index, float candidateTime, float epsilon)
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

        /// <summary>
        /// Brings an externally supplied key array into a usable state: normalizes every key, sorts,
        /// spans a lone key across the whole domain, and decimates evenly when over-long.
        /// </summary>
        /// <remarks>
        /// Written once here rather than once per key kind. The colour and alpha versions of this were
        /// byte-identical apart from the key type and the fallback array.
        /// </remarks>
        /// <returns>The validated array, or null when there was nothing to validate.</returns>
        public static TKey[] Validate(TKey[] keys, int minCount, int maxCount)
        {
            // Null rather than a fallback array, so the caller's default is only built when it is needed.
            if (keys == null || keys.Length == 0)
                return null;

            for (int i = 0; i < keys.Length; i++)
                keys[i] = keys[i].Normalized();

            SortInPlace(keys);

            if (keys.Length < minCount)
            {
                // One key describes a flat value: span it across the domain rather than discarding the
                // caller's data and reverting to the fallback.
                var only = keys[0];
                var padded = new TKey[minCount];
                for (int i = 0; i < minCount; i++)
                    padded[i] = only.WithTime((float)i / (minCount - 1));
                return padded;
            }

            return keys.Length > maxCount ? Decimate(keys, maxCount) : keys;
        }

        /// <summary>
        /// Reduces a sorted, over-long array to <paramref name="maxCount"/> by sampling it evenly, keeping
        /// the first and last key. Truncating the tail instead silently deleted the high-time end.
        /// </summary>
        private static TKey[] Decimate(TKey[] keys, int maxCount)
        {
            var reduced = new TKey[maxCount];
            int last = keys.Length - 1;
            for (int i = 0; i < maxCount; i++)
            {
                int source = Mathf.RoundToInt((float)i * last / (maxCount - 1));
                reduced[i] = keys[Mathf.Clamp(source, 0, last)];
            }
            return reduced;
        }

        /// <summary>First index whose time is strictly greater than <paramref name="time"/>, so equal times insert after.</summary>
        private static int UpperBound(TKey[] keys, float time)
        {
            int lo = 0, hi = keys.Length;
            while (lo < hi)
            {
                int mid = (lo + hi) >> 1;
                if (keys[mid].Time <= time) lo = mid + 1;
                else hi = mid;
            }
            return lo;
        }
    }
}
