using System;
using System.Globalization;
using System.Linq;

namespace Buraqueira_Urb
{
    // Pure dimensional core: no Rhino/Grasshopper dependency, so it can be tested directly.
    public sealed class LotBoundaryFitRequest
    {
        public double ExistingLeft, ExistingRoad, ExistingRight;
        public bool Pedestrian, SidewalkLeft, SidewalkRight, HasMedian, MedianRequired;
        public double MinimumLeft, MinimumRight, PedestrianMinimum;
        public int LaneCount;
        public double LanePreferred, LaneMinimum, MedianPreferred, MedianMinimum;
    }

    public sealed class LotBoundaryFitResult
    {
        public bool Success;
        public string Reason;
        public double AvailableWidth, SidewalkMinimumRequired, RoadMinimumRequired,
            MedianMinimumRequired, TotalMinimumRequired, Deficit;
        public double LeftWidth, RoadWidth, RightWidth, LaneWidth, MedianWidth, RoadwayReserve;
        public bool MedianRemoved;
        public string Diagnostic
        {
            get
            {
                var f = CultureInfo.InvariantCulture;
                return string.Format(f,
                    "Status={0} | AvailableWidth={1:F3} | SidewalkMinimumRequired={2:F3} | RoadMinimumRequired={3:F3} | MedianMinimumRequired={4:F3} | TotalMinimumRequired={5:F3} | Deficit={6:F3} | Reason={7}",
                    Success ? "PASS" : "PROFILE_CONFLICT", AvailableWidth, SidewalkMinimumRequired,
                    RoadMinimumRequired, MedianMinimumRequired, TotalMinimumRequired, Deficit, Reason ?? "NONE");
            }
        }
    }

    public static class LotBoundaryProfileFitter
    {
        public static LotBoundaryFitResult Fit(LotBoundaryFitRequest r)
        {
            if (r == null) throw new ArgumentNullException(nameof(r));
            var o = new LotBoundaryFitResult();
            if (!FiniteNonnegative(r.ExistingLeft) || !FiniteNonnegative(r.ExistingRoad) ||
                !FiniteNonnegative(r.ExistingRight)) return Fail(o, "INVALID_SECTION_GEOMETRY", 0);
            o.AvailableWidth = r.ExistingLeft + r.ExistingRoad + r.ExistingRight;
            if (o.AvailableWidth <= 1e-9) return Fail(o, "INVALID_SECTION_GEOMETRY", 0);
            if (r.Pedestrian)
            {
                if (!FiniteNonnegative(r.PedestrianMinimum) || r.PedestrianMinimum <= 0)
                    return Fail(o, "INVALID_PEDESTRIAN_MINIMUM", 0);
                o.TotalMinimumRequired = r.PedestrianMinimum;
                if (o.AvailableWidth + 1e-9 < r.PedestrianMinimum)
                    return Fail(o, "INSUFFICIENT_PUBLIC_WIDTH", r.PedestrianMinimum - o.AvailableWidth);
                o.Success = true; o.RoadWidth = o.AvailableWidth;
                return o;
            }
            if (r.LaneCount <= 0 || !FiniteNonnegative(r.LaneMinimum) || r.LaneMinimum <= 0 ||
                !FiniteNonnegative(r.LanePreferred) || r.LanePreferred < r.LaneMinimum ||
                !FiniteNonnegative(r.MinimumLeft) || !FiniteNonnegative(r.MinimumRight) ||
                !FiniteNonnegative(r.MedianMinimum) || !FiniteNonnegative(r.MedianPreferred) ||
                (r.HasMedian && r.MedianRequired && r.MedianPreferred < r.MedianMinimum))
                return Fail(o, "INVALID_PROFILE_RULE", 0);
            double floorL = r.SidewalkLeft ? r.MinimumLeft : 0;
            double floorR = r.SidewalkRight ? r.MinimumRight : 0;
            o.SidewalkMinimumRequired = floorL + floorR;
            o.RoadMinimumRequired = r.LaneCount * r.LaneMinimum;
            o.MedianMinimumRequired = r.HasMedian && r.MedianRequired ? r.MedianMinimum : 0;
            o.TotalMinimumRequired = o.SidewalkMinimumRequired + o.RoadMinimumRequired + o.MedianMinimumRequired;
            if (o.AvailableWidth + 1e-9 < o.TotalMinimumRequired)
                return Fail(o, "INSUFFICIENT_PUBLIC_WIDTH", o.TotalMinimumRequired - o.AvailableWidth);

            // Lot boundaries stay fixed. First reserve each enabled sidewalk's minimum.
            double left = Math.Max(r.ExistingLeft, floorL);
            double right = Math.Max(r.ExistingRight, floorR);
            double road = o.AvailableWidth - left - right;
            double roadFloor = o.RoadMinimumRequired + o.MedianMinimumRequired;
            if (road < roadFloor)
            {
                double needed = roadFloor - road;
                double slackL = left - floorL, slackR = right - floorR;
                double slack = slackL + slackR;
                if (slack + 1e-9 < needed)
                    return Fail(o, "INSUFFICIENT_PUBLIC_WIDTH", needed - slack);
                double takeL = slack > 0 ? needed * slackL / slack : 0;
                left -= takeL;
                right -= needed - takeL;
                road = o.AvailableWidth - left - right;
            }
            if (left + 1e-8 < floorL || right + 1e-8 < floorR || road + 1e-8 < roadFloor)
                return Fail(o, "INSUFFICIENT_PUBLIC_WIDTH", Math.Max(0, roadFloor - road));

            double median = r.HasMedian ? r.MedianPreferred : 0;
            double deficit = Math.Max(0, r.LaneCount * r.LanePreferred + median - road);
            if (r.HasMedian)
            {
                double minWhenPresent = r.MedianRequired ? r.MedianMinimum : Math.Min(r.MedianMinimum, median);
                double reduction = Math.Min(deficit, Math.Max(0, median - minWhenPresent));
                median -= reduction; deficit -= reduction;
                if (!r.MedianRequired && deficit > 1e-9 && median > 0)
                {
                    deficit = Math.Max(0, deficit - median);
                    median = 0;
                    o.MedianRemoved = true;
                }
            }
            double lane = r.LanePreferred - deficit / r.LaneCount;
            if (lane + 1e-8 < r.LaneMinimum)
                return Fail(o, "INSUFFICIENT_PUBLIC_WIDTH", r.LaneCount * (r.LaneMinimum - lane));
            o.Success = true;
            o.LeftWidth = left; o.RightWidth = right; o.RoadWidth = road;
            o.LaneWidth = lane; o.MedianWidth = median;
            o.RoadwayReserve = Math.Max(0, road - (r.LaneCount * lane + median));
            o.Reason = o.MedianRemoved ? "OPTIONAL_MEDIAN_REMOVED" : null;
            return o;
        }

        private static LotBoundaryFitResult Fail(LotBoundaryFitResult result, string reason, double deficit)
        {
            result.Reason = reason;
            result.Deficit = Math.Max(0, deficit);
            return result;
        }

        private static bool FiniteNonnegative(double x) => !double.IsNaN(x) && !double.IsInfinity(x) && x >= 0;
    }
}
