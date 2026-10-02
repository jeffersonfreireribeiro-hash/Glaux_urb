using System;

namespace Buraqueira_Urb
{
    // Pure topology gate shared by section samplers and longitudinal reconstruction.
    // Coordinates are directions of the same semantic side, not nearest-point links.
    public static class CurbRunTopology
    {
        public static string BreakReason(int previousStation, int nextStation,
            double ax, double ay, double bx, double by, double minimumDot = 0.85)
        {
            if (nextStation <= previousStation) return "DUPLICATE_OR_ORDER_ERROR";
            if (nextStation != previousStation + 1) return "MISSING_SECTION";
            double al = Math.Sqrt(ax * ax + ay * ay);
            double bl = Math.Sqrt(bx * bx + by * by);
            if (al < 1e-9 || bl < 1e-9) return "INVALID_DIRECTION";
            return (ax * bx + ay * by) / (al * bl) < minimumDot
                ? "SIDE_FLIP_OR_DIRECTION_BREAK" : null;
        }
    }
}
