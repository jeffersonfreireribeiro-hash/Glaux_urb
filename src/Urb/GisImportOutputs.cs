using System;
using System.Collections.Generic;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

namespace Buraqueira_Urb
{
    // A DBF/SQLite NULL must remain distinct from an empty string in generic GH data.
    public sealed class GisNullValue
    {
        public static readonly GisNullValue Instance = new GisNullValue();
        private GisNullValue() { }
        public override string ToString() => "NULL";
    }

    internal static class GisImportOutputs
    {
        public static void Build(IList<ShpFeature> features, IList<string> fields,
            out GH_Structure<GH_ObjectWrapper> values,
            out GH_Structure<GH_ObjectWrapper> geometry)
        {
            values = new GH_Structure<GH_ObjectWrapper>();
            geometry = new GH_Structure<GH_ObjectWrapper>();
            for (int i = 0; i < features.Count; i++)
            {
                var feature = features[i];
                var path = new GH_Path(i);
                // Create the path even when the source feature has no geometry.
                values.EnsurePath(path);
                geometry.EnsurePath(path);
                foreach (string field in fields)
                {
                    object value = feature.Attributes != null && feature.Attributes.TryGetValue(field, out var found)
                        ? found : null;
                    values.Append(new GH_ObjectWrapper(value ?? GisNullValue.Instance), path);
                }
                if (feature.Curves != null)
                    foreach (var curve in feature.Curves)
                        if (curve != null && curve.IsValid) geometry.Append(new GH_ObjectWrapper(curve), path);
                if (feature.Surface != null && feature.Surface.IsValid)
                    geometry.Append(new GH_ObjectWrapper(feature.Surface), path);
                if (feature.Points != null)
                    foreach (var point in feature.Points)
                        geometry.Append(new GH_ObjectWrapper(point), path);
            }
        }
    }
}
