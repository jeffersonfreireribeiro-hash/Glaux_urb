using System.Collections.Generic;

namespace Rhino.Geometry
{
    public class Curve { public bool IsValid => true; }
    public class Brep
    {
        public bool IsValid => true;
        public static Brep[] CreatePlanarBreps(IEnumerable<Curve> curves, double tolerance) => new Brep[0];
    }
    public struct Point2d { public double X, Y; public Point2d(double x,double y) { X=x; Y=y; } }
    public struct Point3d
    {
        public double X,Y,Z;
        public Point3d(double x,double y,double z) { X=x; Y=y; Z=z; }
        public double DistanceTo(Point3d other) => System.Math.Sqrt((X-other.X)*(X-other.X)+(Y-other.Y)*(Y-other.Y)+(Z-other.Z)*(Z-other.Z));
    }
    public class Polyline : List<Point3d>
    {
        public Polyline(int capacity):base(capacity) { }
        public void Add(double x,double y,double z) => Add(new Point3d(x,y,z));
    }
    public class PolylineCurve : Curve { public PolylineCurve(Polyline points) { } }
}

namespace Grasshopper.Kernel.Types
{
    public class GH_ObjectWrapper
    {
        public object Value { get; }
        public GH_ObjectWrapper(object value) { Value=value; }
    }
}
namespace Grasshopper.Kernel.Data
{
    public class GH_Path
    {
        public int Index { get; }
        public GH_Path(int index) { Index=index; }
    }
    public class GH_Structure<T>
    {
        public readonly Dictionary<int,List<T>> Branches = new Dictionary<int,List<T>>();
        public void EnsurePath(GH_Path path) { if (!Branches.ContainsKey(path.Index)) Branches[path.Index]=new List<T>(); }
        public void Append(T value,GH_Path path) { EnsurePath(path); Branches[path.Index].Add(value); }
    }
}
