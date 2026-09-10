using UnityEngine;

namespace ABCodeworld.Gradients
{
    /// <summary>Choosing a well-spread subset of a set of points.</summary>
    /// <remarks>
    /// Wanted in three places that have nothing else in common: distributing keys onto a lattice, thinning
    /// an over-long key array without deleting a whole region of the cube, and picking sample points off a
    /// mesh or out of a random scatter. Kept non-generic so all three share one copy rather than one per
    /// key type.
    /// </remarks>
    internal static class PointSpread
    {
        /// <summary>
        /// Picks <paramref name="take"/> of <paramref name="points"/> by farthest-point selection: start
        /// at <paramref name="startIndex"/>, then repeatedly take whichever unused point is furthest from
        /// everything taken so far, breaking ties towards the lower index.
        /// </summary>
        /// <remarks>
        /// Runs in O(take * points.Length) by carrying each candidate's distance to the nearest chosen
        /// point forward rather than recomputing it, which at this package's counts is a few thousand
        /// operations at worst. Fully deterministic given the same start, which matters because every
        /// caller either feeds serialized data or is expected to reproduce its result from a seed.
        /// </remarks>
        public static int[] SelectSpreadOut(Vector3[] points, int take, int startIndex = 0)
        {
            // Guards the scan below, which has no unused point to hand back once every one is taken.
            if (take > points.Length)
                take = points.Length;
            if (take < 0)
                take = 0;

            if (startIndex < 0 || startIndex >= points.Length)
                startIndex = 0;

            var nearestSq = new float[points.Length];
            for (int i = 0; i < points.Length; i++)
                nearestSq[i] = float.PositiveInfinity;

            var chosen = new int[take];
            int pick = startIndex;

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
                Vector3 taken = points[pick];
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
