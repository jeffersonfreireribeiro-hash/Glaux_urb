using System;
using System.Collections.Generic;

namespace Buraqueira_Urb
{
    /// <summary>
    /// Shared one-dimensional minimum-width planner. Call once per continuous
    /// street side; callers split runs at rejected stations and junctions.
    /// </summary>
    internal static class MinimumDimensionPlanner
    {
        internal static double[] Plan(IReadOnlyList<double> existing, double minimum, double tolerance)
        {
            if (existing == null) throw new ArgumentNullException(nameof(existing));
            if (minimum <= 0 || tolerance < 0) throw new ArgumentOutOfRangeException();
            var result = new double[existing.Count];
            double smallest = double.PositiveInfinity, largest = 0;
            int anchor = -1;
            for (int i = 0; i < result.Length; i++)
            {
                if (double.IsNaN(existing[i]) || double.IsInfinity(existing[i]) || existing[i] <= 0)
                    throw new ArgumentException("All dimensions must be finite and positive.", nameof(existing));
                if (existing[i] < smallest) { smallest = existing[i]; anchor = i; }
                largest = Math.Max(largest, existing[i]);
            }

            // When every existing width is below the requested minimum, a plain
            // clamp would flatten the whole street. Retain a bounded fraction of
            // its measured local relief while anchoring the narrowest station.
            bool allBelow = largest < minimum;
            var preferred = new double[existing.Count];
            for (int i = 0; i < result.Length; i++)
            {
                preferred[i] = Math.Max(minimum, existing[i]);
                if (allBelow)
                    preferred[i] += Math.Min(tolerance, 0.25 * (existing[i] - smallest));
                result[i] = preferred[i];
            }

            // Projected local smoothing with a trust band around the preferred
            // geometry. The minimum constraint is restored at every iteration.
            for (int pass = 0; pass < 4; pass++)
            {
                var previous = (double[])result.Clone();
                for (int i = 1; i < result.Length - 1; i++)
                {
                    if (i == anchor && smallest < minimum) { result[i] = minimum; continue; }
                    double local = (previous[i - 1] + 2 * previous[i] + previous[i + 1]) / 4;
                    double lower = Math.Max(minimum, preferred[i] - tolerance);
                    double upper = preferred[i] + tolerance;
                    result[i] = Math.Max(lower, Math.Min(upper, local));
                }
            }
            return result;
        }
    }
}

