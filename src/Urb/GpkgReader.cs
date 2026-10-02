using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Rhino.Geometry;

namespace Buraqueira_Urb
{
    /// <summary>
    /// Metadados de uma camada geoespacial dentro de um GeoPackage (.gpkg).
    /// </summary>
    public class GpkgLayerInfo
    {
        public string TableName { get; set; }
        public string DataType { get; set; }
        public string Identifier { get; set; }
        public string Description { get; set; }
        public string GeometryColumn { get; set; } = "geom";
        public string GeometryType { get; set; } = "GEOMETRY";
        public int SrsId { get; set; }
    }

    /// <summary>
    /// Leitor puro em C# (.NET) para o formato padrão OGC GeoPackage (.gpkg).
    /// Utiliza o motor SQLite nativo embutido no Windows (winsqlite3.dll) via P/Invoke.
    /// Zero dependências externas de NuGet, garantindo estabilidade absoluta no Rhino 8.
    /// </summary>
    public static class GpkgReader
    {
        private const string SQLITE_DLL = "winsqlite3.dll";

        private static string Utf8Column(IntPtr statement, int column)
        {
            IntPtr pointer = sqlite3_column_text(statement, column);
            int length = sqlite3_column_bytes(statement, column);
            if (pointer == IntPtr.Zero || length <= 0) return null;
            var bytes = new byte[length];
            Marshal.Copy(pointer, bytes, 0, length);
            return Encoding.UTF8.GetString(bytes);
        }

        private static string Utf8Name(IntPtr pointer)
        {
            if (pointer == IntPtr.Zero) return null;
            int length = 0;
            while (Marshal.ReadByte(pointer, length) != 0) length++;
            var bytes = new byte[length];
            Marshal.Copy(pointer, bytes, 0, length);
            return Encoding.UTF8.GetString(bytes);
        }

        [DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_open_v2(byte[] filename, out IntPtr ppDb, int flags, IntPtr zVfs);

        [DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_close(IntPtr db);

        [DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_prepare_v2(IntPtr db, byte[] zSql, int nByte, out IntPtr ppStmt, IntPtr pzTail);

        private static byte[] SqliteUtf8(string value) => Encoding.UTF8.GetBytes(value + "\0");

        [DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_step(IntPtr stmt);

        [DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_column_count(IntPtr stmt);

        [DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr sqlite3_column_name(IntPtr stmt, int N);

        [DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_column_type(IntPtr stmt, int iCol);

        [DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
        private static extern long sqlite3_column_int64(IntPtr stmt, int iCol);

        [DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
        private static extern double sqlite3_column_double(IntPtr stmt, int iCol);

        [DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr sqlite3_column_text(IntPtr stmt, int iCol);

        [DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr sqlite3_column_blob(IntPtr stmt, int iCol);

        [DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_column_bytes(IntPtr stmt, int iCol);

        [DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_finalize(IntPtr stmt);

        private const int SQLITE_OK = 0;
        private const int SQLITE_ROW = 100;
        private const int SQLITE_DONE = 101;
        private const int SQLITE_OPEN_READONLY = 0x00000001;

        private const int SQLITE_INTEGER = 1;
        private const int SQLITE_FLOAT = 2;
        private const int SQLITE_TEXT = 3;
        private const int SQLITE_BLOB = 4;
        private const int SQLITE_NULL = 5;

        /// <summary>
        /// Lista as camadas de feições presentes no arquivo GeoPackage (.gpkg).
        /// </summary>
        public static List<GpkgLayerInfo> GetLayers(string gpkgPath)
        {
            var layers = new List<GpkgLayerInfo>();
            if (string.IsNullOrWhiteSpace(gpkgPath) || !File.Exists(gpkgPath)) return layers;

            if (sqlite3_open_v2(SqliteUtf8(gpkgPath), out IntPtr db, SQLITE_OPEN_READONLY, IntPtr.Zero) != SQLITE_OK)
            {
                return layers;
            }

            try
            {
                string sql = "SELECT c.table_name, c.data_type, c.identifier, c.description, c.srs_id, " +
                             "g.column_name, g.geometry_type_name " +
                             "FROM gpkg_contents c " +
                             "LEFT JOIN gpkg_geometry_columns g ON c.table_name = g.table_name " +
                             "WHERE c.data_type = 'features';";

                if (sqlite3_prepare_v2(db, SqliteUtf8(sql), -1, out IntPtr stmt, IntPtr.Zero) == SQLITE_OK)
                {
                    while (sqlite3_step(stmt) == SQLITE_ROW)
                    {
                        string tableName = Utf8Column(stmt, 0) ?? "";
                        string dataType = Utf8Column(stmt, 1) ?? "";
                        string identifier = Utf8Column(stmt, 2) ?? "";
                        string desc = Utf8Column(stmt, 3) ?? "";
                        int srs = (int)sqlite3_column_int64(stmt, 4);
                        string geomCol = Utf8Column(stmt, 5) ?? "geom";
                        string geomType = Utf8Column(stmt, 6) ?? "GEOMETRY";

                        layers.Add(new GpkgLayerInfo
                        {
                            TableName = tableName,
                            DataType = dataType,
                            Identifier = string.IsNullOrWhiteSpace(identifier) ? tableName : identifier,
                            Description = desc,
                            SrsId = srs,
                            GeometryColumn = geomCol,
                            GeometryType = geomType
                        });
                    }
                    sqlite3_finalize(stmt);
                }
            }
            finally
            {
                sqlite3_close(db);
            }

            return layers;
        }

        /// <summary>
        /// Lê as feições e atributos de uma camada específica (ou da primeira camada) do GeoPackage.
        /// </summary>
        public static List<ShpFeature> ReadGeoPackage(string gpkgPath, string layerName, out List<string> fieldNames, string filter = null)
        {
            fieldNames = new List<string>();
            var features = new List<ShpFeature>();

            if (string.IsNullOrWhiteSpace(gpkgPath) || !File.Exists(gpkgPath)) return features;

            var layers = GetLayers(gpkgPath);
            if (layers.Count == 0) return features;

            // Selecionar camada desejada
            GpkgLayerInfo targetLayer = null;
            if (!string.IsNullOrWhiteSpace(layerName))
            {
                targetLayer = layers.Find(l => l.TableName.Equals(layerName, StringComparison.OrdinalIgnoreCase) ||
                                               l.Identifier.Equals(layerName, StringComparison.OrdinalIgnoreCase));
                if (targetLayer == null)
                    throw new InvalidDataException($"Camada GeoPackage não encontrada: {layerName}");
            }
            if (targetLayer == null)
            {
                targetLayer = layers[0];
            }

            if (sqlite3_open_v2(SqliteUtf8(gpkgPath), out IntPtr db, SQLITE_OPEN_READONLY, IntPtr.Zero) != SQLITE_OK)
            {
                return features;
            }

            try
            {
                string sql = $"SELECT * FROM \"{targetLayer.TableName.Replace("\"", "\"\"")}\";";

                if (sqlite3_prepare_v2(db, SqliteUtf8(sql), -1, out IntPtr stmt, IntPtr.Zero) == SQLITE_OK)
                {
                    int colCount = sqlite3_column_count(stmt);
                    int geomColIdx = -1;

                    for (int c = 0; c < colCount; c++)
                    {
                        string cName = Utf8Name(sqlite3_column_name(stmt, c)) ?? $"col_{c}";
                        if (cName.Equals(targetLayer.GeometryColumn, StringComparison.OrdinalIgnoreCase))
                        {
                            geomColIdx = c;
                        }
                        else
                        {
                            fieldNames.Add(cName);
                        }
                    }

                    int recNum = 1;
                    while (sqlite3_step(stmt) == SQLITE_ROW)
                    {
                        var feat = new ShpFeature { RecordNumber = recNum++, SourcePath = gpkgPath };

                        // Ler atributos
                        for (int c = 0; c < colCount; c++)
                        {
                            if (c == geomColIdx) continue;

                            string cName = Utf8Name(sqlite3_column_name(stmt, c)) ?? $"col_{c}";
                            int type = sqlite3_column_type(stmt, c);

                            object val = null;
                            if (type == SQLITE_INTEGER) val = sqlite3_column_int64(stmt, c);
                            else if (type == SQLITE_FLOAT) val = sqlite3_column_double(stmt, c);
                            else if (type == SQLITE_TEXT)
                            {
                                IntPtr ptr = sqlite3_column_text(stmt, c);
                                if (ptr != IntPtr.Zero)
                                {
                                    int bytes = sqlite3_column_bytes(stmt, c);
                                    byte[] buf = new byte[bytes];
                                    Marshal.Copy(ptr, buf, 0, bytes);
                                    val = Encoding.UTF8.GetString(buf);
                                }
                            }
                            else if (type == SQLITE_BLOB)
                            {
                                int bytes = sqlite3_column_bytes(stmt, c);
                                var buf = new byte[bytes];
                                if (bytes > 0) Marshal.Copy(sqlite3_column_blob(stmt, c), buf, 0, bytes);
                                val = buf;
                            }
                            feat.Attributes[cName] = val;
                        }

                        // Ler Geometria (GPB / WKB)
                        if (geomColIdx >= 0 && sqlite3_column_type(stmt, geomColIdx) == SQLITE_BLOB)
                        {
                            IntPtr blobPtr = sqlite3_column_blob(stmt, geomColIdx);
                            int blobLen = sqlite3_column_bytes(stmt, geomColIdx);
                            if (blobPtr != IntPtr.Zero && blobLen > 8)
                            {
                                byte[] blob = new byte[blobLen];
                                Marshal.Copy(blobPtr, blob, 0, blobLen);
                                ParseGpbGeometry(blob, feat);
                            }
                        }

                        // Anexar atributos às curvas para permitir consulta direta por nome/metadados
                        if (feat.Attributes != null && feat.Curves != null)
                        {
                            foreach (var crv in feat.Curves)
                            {
                                if (crv == null) continue;
                                foreach (var kvp in feat.Attributes)
                                {
                                    if (kvp.Value != null)
                                    {
                                        crv.SetUserString(kvp.Key, kvp.Value.ToString());
                                    }
                                }
                            }
                        }

                        // Filtro opcional
                        if (string.IsNullOrWhiteSpace(filter) || MatchesFilter(feat, filter))
                        {
                            features.Add(feat);
                        }
                    }
                    sqlite3_finalize(stmt);
                }
            }
            finally
            {
                sqlite3_close(db);
            }

            return features;
        }

        private static bool MatchesFilter(ShpFeature feature, string filter)
        {
            if (string.IsNullOrWhiteSpace(filter)) return true;
            if (feature.Attributes == null) return false;

            string fLower = filter.Trim().ToLowerInvariant();
            foreach (var kvp in feature.Attributes)
            {
                string valStr = kvp.Value?.ToString()?.ToLowerInvariant();
                if (valStr != null && valStr.Contains(fLower)) return true;
            }
            return false;
        }

        #region Parser GeoPackage Standard Binary (GPB) & WKB

        private static void ParseGpbGeometry(byte[] blob, ShpFeature feat)
        {
            if (blob == null || blob.Length < 8) return;

            // Verificar Magic Number GPB: 0x47 0x50 ('G', 'P')
            if (blob[0] != 0x47 || blob[1] != 0x50) return;

            byte flags = blob[3];
            int envelopeType = (flags >> 1) & 0x07;

            int envLength = 0;
            if (envelopeType == 1) envLength = 32;       // minX, maxX, minY, maxY
            else if (envelopeType == 2 || envelopeType == 3) envLength = 48; // com Z ou M
            else if (envelopeType == 4) envLength = 64;  // com Z e M

            int wkbOffset = 8 + envLength;
            if (wkbOffset >= blob.Length) return;

            using (var ms = new MemoryStream(blob, wkbOffset, blob.Length - wkbOffset))
            using (var reader = new BinaryReader(ms))
            {
                ParseWkbGeometry(reader, feat);
            }
        }

        private static void ParseWkbGeometry(BinaryReader reader, ShpFeature feat)
        {
            if (reader.BaseStream.Position >= reader.BaseStream.Length) return;

            byte byteOrder = reader.ReadByte(); // 1 = Little Endian, 0 = Big Endian
            uint rawType = ReadUInt32(reader, byteOrder);

            // Determinar tipo base (1..6) e flags de dimensão Z
            uint baseType = rawType % 1000;
            bool hasZ = (rawType >= 1000 && rawType < 2000) || (rawType >= 3000 && rawType < 4000) || ((rawType & 0x80000000) != 0);

            switch (baseType)
            {
                case 1: // Point
                    var pt = ReadPoint(reader, byteOrder, hasZ);
                    feat.Points.Add(pt);
                    break;

                case 2: // LineString
                    var crv = ReadLineString(reader, byteOrder, hasZ);
                    if (crv != null) feat.Curves.Add(crv);
                    break;

                case 3: // Polygon
                    ReadPolygon(reader, byteOrder, hasZ, feat);
                    break;

                case 4: // MultiPoint
                    uint numPts = ReadUInt32(reader, byteOrder);
                    for (int i = 0; i < numPts; i++)
                    {
                        reader.ReadByte(); // sub-byteOrder
                        ReadUInt32(reader, byteOrder); // sub-type
                        feat.Points.Add(ReadPoint(reader, byteOrder, hasZ));
                    }
                    break;

                case 5: // MultiLineString
                    uint numLines = ReadUInt32(reader, byteOrder);
                    for (int i = 0; i < numLines; i++)
                    {
                        reader.ReadByte();
                        ReadUInt32(reader, byteOrder);
                        var c = ReadLineString(reader, byteOrder, hasZ);
                        if (c != null) feat.Curves.Add(c);
                    }
                    break;

                case 6: // MultiPolygon
                    uint numPolys = ReadUInt32(reader, byteOrder);
                    for (int i = 0; i < numPolys; i++)
                    {
                        reader.ReadByte();
                        ReadUInt32(reader, byteOrder);
                        ReadPolygon(reader, byteOrder, hasZ, feat);
                    }
                    break;
            }
        }

        private static Point3d ReadPoint(BinaryReader reader, byte byteOrder, bool hasZ)
        {
            double x = ReadDouble(reader, byteOrder);
            double y = ReadDouble(reader, byteOrder);
            double z = hasZ ? ReadDouble(reader, byteOrder) : 0.0;
            return new Point3d(x, y, z);
        }

        private static PolylineCurve ReadLineString(BinaryReader reader, byte byteOrder, bool hasZ)
        {
            uint numPoints = ReadUInt32(reader, byteOrder);
            if (numPoints < 2) return null;

            var poly = new Polyline((int)numPoints);
            for (int i = 0; i < numPoints; i++)
            {
                poly.Add(ReadPoint(reader, byteOrder, hasZ));
            }

            return new PolylineCurve(poly);
        }

        private static void ReadPolygon(BinaryReader reader, byte byteOrder, bool hasZ, ShpFeature feat)
        {
            uint numRings = ReadUInt32(reader, byteOrder);
            if (numRings == 0) return;

            var rings = new List<Curve>();

            for (int r = 0; r < numRings; r++)
            {
                uint numPoints = ReadUInt32(reader, byteOrder);
                if (numPoints < 3) continue;

                var poly = new Polyline((int)numPoints + 1);
                for (int i = 0; i < numPoints; i++)
                {
                    poly.Add(ReadPoint(reader, byteOrder, hasZ));
                }

                if (poly.Count > 2 && poly[0].DistanceTo(poly[poly.Count - 1]) > 1e-6)
                {
                    poly.Add(poly[0]);
                }

                if (poly.Count >= 4)
                {
                    var c = new PolylineCurve(poly);
                    feat.Curves.Add(c);
                    rings.Add(c);
                }
            }

            if (rings.Count > 0 && feat.Surface == null)
            {
                try
                {
                    var breps = Brep.CreatePlanarBreps(rings, 0.01);
                    if (breps != null && breps.Length > 0)
                    {
                        feat.Surface = breps[0];
                    }
                }
                catch { }
            }
        }

        private static uint ReadUInt32(BinaryReader reader, byte byteOrder)
        {
            byte[] bytes = reader.ReadBytes(4);
            if (bytes.Length < 4) return 0;
            if (byteOrder == 1) // Little Endian
            {
                return (uint)(bytes[0] | (bytes[1] << 8) | (bytes[2] << 16) | (bytes[3] << 24));
            }
            else // Big Endian
            {
                return (uint)((bytes[0] << 24) | (bytes[1] << 16) | (bytes[2] << 8) | bytes[3]);
            }
        }

        private static double ReadDouble(BinaryReader reader, byte byteOrder)
        {
            byte[] bytes = reader.ReadBytes(8);
            if (bytes.Length < 8) return 0.0;
            if (byteOrder != (BitConverter.IsLittleEndian ? 1 : 0))
            {
                Array.Reverse(bytes);
            }
            return BitConverter.ToDouble(bytes, 0);
        }

        #endregion
    }
}
