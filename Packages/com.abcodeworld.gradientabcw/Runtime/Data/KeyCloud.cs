using System;
using UnityEngine;

namespace ABCodeworld.Gradients
{
    /// <summary>
    /// Shared operations over an <em>unordered</em> array of 3D gradient keys. Used by
    /// <see cref="GradientABCW3D"/> for both <see cref="ColorKey3D"/> and <see cref="AlphaKey3D"/> arrays
    /// so the add/remove/flip/distribute/validate logic exists exactly once.
    /// </summary>
    /// <remarks>
    /// The deliberate counterpart to <c>KeyArray&lt;TKey&gt;</c>, not a reuse of it. Every routine there
    /// maintains a sort by time — insert in sorted position, slide a replaced key back into place, clamp
    /// into the gap between neighbours, reverse the array after flipping — and none of that has a meaning
    /// for points scattered through a cube. What this loses in shared code it buys back in simplicity: a
    /// key's index never changes while it exists, so nothing here has to track a key's identity across a
    /// mutation the way <c>SetAndResort</c> does.
    /// <para>
    /// Two keys are allowed to share a position, exactly as two 1D keys are allowed to share a time.
    /// <see cref="GradientMath3D"/> defines what that evaluates to, so nothing here needs to prevent it.
    /// </para>
    /// </remarks>
    internal static class KeyCloud<TKey> where TKey : struct, IGradientKey3D<TKey>
    {
        /// <summary>
        /// Appends <paramref name="key"/> and returns the index it landed at, which is always the last.
        /// Returns -1 without mutating when already at <paramref name="maxCount"/>.
        /// </summary>
        public static int Append(ref TKey[] keys, TKey key, int maxCount)
        {
            if (keys.Length >= maxCount)
                return -1;

            var next = new TKey[keys.Length + 1];
            Array.Copy(keys, next, keys.Length);
            next[keys.Length] = key;

            keys = next;
            return keys.Length - 1;
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
        /// Mirrors every key through the centre of the cube (p becomes 1 - p on all three axes).
        /// </summary>
        /// <remarks>
        /// Unlike the 1D flip this does not reverse the array afterwards. That reversal exists to restore
        /// the sort order a flip inverts; with no order to restore, reversing here would only shuffle
        /// indices for nothing.
        /// </remarks>
        public static void FlipPositions(TKey[] keys)
        {
            for (int i = 0; i < keys.Length; i++)
                keys[i] = keys[i].WithPosition(Vector3.one - keys[i].Position);
        }

        /// <summary>
        /// Spreads the keys as evenly as the count allows, over the smallest cubic lattice that can hold
        /// them: 8 keys land on the cube's corners, 27 on a 3x3x3 grid, and so on.
        /// </summary>
        /// <remarks>
        /// A corner lattice (positions i/(m-1)) rather than cell centres, because inverse-distance
        /// weighting has nothing to extrapolate from outside the convex hull of its keys — spanning the
        /// corners is what makes the cube's own corners well defined.
        /// <para>
        /// When the count is not a perfect cube the lattice has spare sites, and which ones go unused
        /// matters: taking them in raster order piles every leftover onto one face. Farthest-point
        /// selection instead keeps the chosen sites spread through the whole volume at any count.
        /// </para>
        /// </remarks>
        public static void Distribute(TKey[] keys)
        {
            int count = keys.Length;
            if (count == 0)
                return;

            int m = LatticeSize(count);
            if (m == 1)
            {
                keys[0] = keys[0].WithPosition(new Vector3(0.5f, 0.5f, 0.5f));
                return;
            }

            var sites = BuildLattice(m);
            var chosen = SelectSpreadOut(sites, count);
            for (int i = 0; i < count; i++)
                keys[i] = keys[i].WithPosition(sites[chosen[i]]);
        }

        /// <summary>
        /// Brings an externally supplied key array into a usable state: normalizes every key, and
        /// decimates when over-long.
        /// </summary>
        /// <remarks>
        /// There is no minimum-count padding here, unlike the 1D version. That padding spans a lone key
        /// across the domain so a gradient always has two ends to interpolate between; a 3D gradient with
        /// one key is a perfectly meaningful constant field, and padding it would only manufacture keys
        /// stacked on top of each other.
        /// </remarks>
        /// <returns>The validated array, or null when there was nothing to validate.</returns>
        public static TKey[] Validate(TKey[] keys, int maxCount)
        {
            // Null rather than a fallback array, so the caller's default is only built when it is needed.
            if (keys == null || keys.Length == 0)
                return null;

            for (int i = 0; i < keys.Length; i++)
                keys[i] = keys[i].Normalized();

            return keys.Length > maxCount ? Decimate(keys, maxCount) : keys;
        }

        /// <summary>Smallest m whose cubic lattice can hold <paramref name="count"/> keys.</summary>
        /// <remarks>Counted up rather than derived from <c>Mathf.Pow(count, 1f/3f)</c>, which lands on the
        /// wrong side of an exact cube often enough to matter (27 is the classic case).</remarks>
        internal static int LatticeSize(int count)
        {
            int m = 1;
            while (m * m * m < count)
                m++;
            return m;
        }

        private static Vector3[] BuildLattice(int m)
        {
            var sites = new Vector3[m * m * m];
            float step = 1f / (m - 1);

            int i = 0;
            for (int z = 0; z < m; z++)
            {
                for (int y = 0; y < m; y++)
                {
                    for (int x = 0; x < m; x++)
                        sites[i++] = new Vector3(x * step, y * step, z * step);
                }
            }
            return sites;
        }

        /// <summary>
        /// Reduces an over-long array to <paramref name="maxCount"/> by farthest-point selection over the
        /// key positions, so the keys that survive still span the volume the original covered.
        /// </summary>
        /// <remarks>
        /// The 1D version samples the sorted array evenly, which works because position and index agree
        /// there. Here they do not, so sampling by index could delete an entire region of the cube and
        /// leave a dense clump behind.
        /// </remarks>
        private static TKey[] Decimate(TKey[] keys, int maxCount)
        {
            var positions = new Vector3[keys.Length];
            for (int i = 0; i < keys.Length; i++)
                positions[i] = keys[i].Position;

            var chosen = SelectSpreadOut(positions, maxCount);

            var reduced = new TKey[maxCount];
            for (int i = 0; i < maxCount; i++)
                reduced[i] = keys[chosen[i]];
            return reduced;
        }

        /// <summary>
        /// Picks <paramref name="take"/> of <paramref name="points"/> by farthest-point selection: start
        /// at index 0, then repeatedly take whichever unused point is furthest from everything taken so
        /// far, breaking ties towards the lower index.
        /// </summary>
        /// <remarks>
        /// Runs in O(take * points.Length) by carrying each candidate's distance to the nearest chosen
        /// point forward rather than recomputing it, which at this package's key counts is a few thousand
        /// operations at worst. Fully deterministic, which matters because both callers feed serialized
        /// data.
        /// </remarks>
        private static int[] SelectSpreadOut(Vector3[] points, int take)
        {
            // Guards the scan below, which has no unused point to hand back once every one is taken.
            // Neither caller can reach this, but the failure would be a silently duplicated index.
            if (take > points.Length)
                take = points.Length;

            var nearestSq = new float[points.Length];
            for (int i = 0; i < points.Length; i++)
                nearestSq[i] = float.PositiveInfinity;

            var chosen = new int[take];
            int pick = 0;

            for (int k = 0; k < take; k++)
            {
                if (k > 0)
                {
                    // Used points carry -1, and the running best starts there, so they can never win.
                    float best = -1f;
                    pick = 0;
                    for (int i = 0; i < points.Length; i++)
                    {
                        if (nearestSq[i] > best)
                        {
                            best = nearestSq[i];
                            pick = i;
                        }
                    }
                }

                chosen[k] = pick;
                var taken = points[pick];
                for (int i = 0; i < points.Length; i++)
                {
                    if (nearestSq[i] < 0f)
                        continue;
                    float d = (points[i] - taken).sqrMagnitude;
                    if (d < nearestSq[i])
                        nearestSq[i] = d;
                }
                nearestSq[pick] = -1f;
            }

            return chosen;
        }
    }
}
