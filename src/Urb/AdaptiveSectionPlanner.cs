using System;
using System.Collections.Generic;
using System.Linq;

namespace Buraqueira_Urb
{
    // Geometry classification and station spacing are independent of RhinoCommon.
    public sealed class SectionEvent
    {
        public double Station;
        public double BoundaryDistance;
        public string Reason, SourceId;
        public int VertexId, Side;
        public bool Hard;
    }

    public sealed class SectionStation
    {
        public double Station;
        public double BoundaryDistance;
        public bool Required, Hard;
        public string Reason, SourceId;
        public int VertexId, Side;
    }

    public sealed class SectionDistribution
    {
        public List<SectionStation> Stations = new List<SectionStation>();
        public int GeometryRequired, Support, Merged;
    }

    public static class AdaptiveSectionPlanner
    {
        public static string ClassifyVertex(double ax, double ay, double bx, double by,
            double cx, double cy, double minimumTurnDegrees, double noiseLength)
        {
            double ux = bx - ax, uy = by - ay, vx = cx - bx, vy = cy - by;
            double la = Math.Sqrt(ux * ux + uy * uy), lb = Math.Sqrt(vx * vx + vy * vy);
            if (la < 1e-8 || lb < 1e-8) return "INVALID_NOISE";
            double cross = ux * vy - uy * vx;
            double dot = ux * vx + uy * vy;
            double angle = Math.Atan2(Math.Abs(cross), dot) * 180.0 / Math.PI;
            if (angle < minimumTurnDegrees) return "COLLINEAR_OR_CURVE_SAMPLE";
            // A sub-noise segment is meaningful only if it creates a large corner.
            if (Math.Min(la, lb) < noiseLength && angle < 45) return "MINOR_CHANGE";
            return angle >= 30 ? "LOT_CORNER" : "DIRECTION_CHANGE";
        }

        public static SectionDistribution Build(double length, double maximumSpacing,
            double mergeTolerance, IEnumerable<SectionEvent> events)
        {
            if (!Finite(length) || length <= 0) throw new ArgumentOutOfRangeException(nameof(length));
            if (!Finite(maximumSpacing) || maximumSpacing <= 0) throw new ArgumentOutOfRangeException(nameof(maximumSpacing));
            if (!Finite(mergeTolerance) || mergeTolerance < 0) throw new ArgumentOutOfRangeException(nameof(mergeTolerance));
            var result = new SectionDistribution();
            var required = new List<SectionEvent> {
                new SectionEvent { Station = 0, Reason = "RUN_START", SourceId = "AXIS", Hard = true },
                new SectionEvent { Station = length, Reason = "RUN_END", SourceId = "AXIS", Hard = true }
            };
            if (events != null) required.AddRange(events.Where(e => e != null && Finite(e.Station) && e.Station >= 0 && e.Station <= length));
            required.Sort((a, b) => a.Station.CompareTo(b.Station));
            foreach (var e in required)
            {
                var last = result.Stations.Count == 0 ? null : result.Stations[result.Stations.Count - 1];
                // Keep two close corners on the same side when they belong to different vertices.
                bool distinctHard = last != null && e.Hard && last.Hard &&
                    e.Side == last.Side && e.Side != 0 &&
                    Math.Abs(e.BoundaryDistance - last.BoundaryDistance) > 0.30 &&
                    (e.SourceId != last.SourceId || e.VertexId != last.VertexId);
                if (last != null && e.Station - last.Station <= mergeTolerance && !distinctHard)
                {
                    result.Merged++;
                    if (e.Hard && !last.Hard) { last.Station = e.Station; last.Hard = true; }
                    if (e.Reason == "LOT_CORNER" && last.Reason != "LOT_CORNER")
                        last.BoundaryDistance = e.BoundaryDistance;
                    if (e.Reason == "LOT_CORNER") last.Reason = e.Reason;
                    else if (last.Reason != e.Reason && last.Reason != "LOT_CORNER") last.Reason += "+" + e.Reason;
                    if (last.SourceId != e.SourceId) last.SourceId += "+" + e.SourceId;
                    last.Side = last.Side == e.Side ? last.Side : 0;
                    continue;
                }
                result.Stations.Add(new SectionStation { Station = e.Station, Required = true,
                    Hard = e.Hard, Reason = e.Reason, SourceId = e.SourceId,
                    VertexId = e.VertexId, Side = e.Side });
                result.Stations[result.Stations.Count - 1].BoundaryDistance = e.BoundaryDistance;
            }
            result.GeometryRequired = result.Stations.Count;
            var filled = new List<SectionStation>();
            for (int i = 0; i < result.Stations.Count - 1; i++)
            {
                var a = result.Stations[i]; var b = result.Stations[i + 1];
                filled.Add(a);
                int gaps = (int)Math.Ceiling((b.Station - a.Station) / maximumSpacing);
                for (int j = 1; j < gaps; j++)
                {
                    filled.Add(new SectionStation { Station = a.Station + (b.Station - a.Station) * j / gaps,
                        Required = false, Hard = false, Reason = "MAX_SPACING", SourceId = "AXIS" });
                    result.Support++;
                }
            }
            filled.Add(result.Stations[result.Stations.Count - 1]);
            result.Stations = filled;
            return result;
        }

        private static bool Finite(double x) => !double.IsNaN(x) && !double.IsInfinity(x);
    }
}
