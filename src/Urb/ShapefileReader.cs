using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Rhino.Geometry;

namespace Buraqueira_Urb
{
    /// <summary>
    /// Representa uma feição geográfica lida de um arquivo Shapefile (.shp + .dbf).
    /// </summary>
    public class ShpFeature
    {
        public int RecordNumber { get; set; }
        public int ShapeType { get; set; }
        public List<Curve> Curves { get; set; } = new List<Curve>();
        public List<Point3d> Points { get; set; } = new List<Point3d>();
        public Brep Surface { get; set; }
        public Dictionary<string, object> Attributes { get; set; } = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        public object GetAttribute(string fieldName)
        {
            if (Attributes != null && Attributes.TryGetValue(fieldName, out var val))
            {
                return val;
            }
            return null;
        }

        public string GetAttributeString(string fieldName)
        {
            var val = GetAttribute(fieldName);
            return val?.ToString()?.Trim() ?? string.Empty;
        }
    }

    /// <summary>
    /// Leitor puro em C# (.NET 4.8 / .NET Core) para arquivos ESRI Shapefile (.shp) e dBASE (.dbf).
    /// Zero dependências externas não gerenciadas, garantindo compatibilidade total e velocidade no Rhino 8.
    /// </summary>
    public static class ShapefileReader
    {
        public static List<ShpFeature> ReadShapefile(string shpPath, out List<string> fieldNames, string filter = null)
        {
            fieldNames = new List<string>();
            var features = new List<ShpFeature>();

            if (string.IsNullOrWhiteSpace(shpPath) || !File.Exists(shpPath))
            {
                return features;
            }

            // 1. Ler Tabela de Atributos (.dbf) se existir
            string dbfPath = Path.ChangeExtension(shpPath, ".dbf");
            List<Dictionary<string, object>> dbfRows = null;
            if (File.Exists(dbfPath))
            {
                try
                {
                    dbfRows = ReadDbf(dbfPath, out fieldNames);
                }
                catch
                {
                    dbfRows = null;
                }
            }

            // 2. Ler Geometrias (.shp)
            using (var fs = new FileStream(shpPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new BinaryReader(fs))
            {
                if (fs.Length < 100) return features;

                // Cabeçalho de 100 bytes
                int fileCode = ReadBigInt32(reader);
                if (fileCode != 9994)
                {
                    throw new InvalidDataException($"Arquivo não é um Shapefile válido (FileCode = {fileCode}, esperado 9994).");
                }

                // Pular 5 ints não usados (20 bytes)
                reader.BaseStream.Seek(24, SeekOrigin.Begin);
                int fileLengthWords = ReadBigInt32(reader);
                int version = reader.ReadInt32();
                int globalShapeType = reader.ReadInt32();

                // Pular BoundingBox (36-99)
                reader.BaseStream.Seek(100, SeekOrigin.Begin);

                int recordIndex = 0;
                while (reader.BaseStream.Position < reader.BaseStream.Length - 8)
                {
                    int recordNumber = ReadBigInt32(reader);
                    int contentLengthWords = ReadBigInt32(reader);
                    long recordStartPos = reader.BaseStream.Position;
                    long recordEndPos = recordStartPos + (contentLengthWords * 2);

                    if (recordEndPos > reader.BaseStream.Length)
                    {
                        break;
                    }

                    int shapeType = reader.ReadInt32();
                    var feature = new ShpFeature
                    {
                        RecordNumber = recordNumber,
                        ShapeType = shapeType
                    };

                    // Anexar atributos do DBF se disponíveis
                    if (dbfRows != null && recordIndex < dbfRows.Count)
                    {
                        feature.Attributes = dbfRows[recordIndex];
                    }

                    // Processar geometrias conforme o ShapeType
                    switch (shapeType)
                    {
                        case 0: // Null Shape
                            break;

                        case 1: // Point
                        case 11: // PointZ
                        case 21: // PointM
                            ReadPointRecord(reader, shapeType, feature);
                            break;

                        case 3: // PolyLine
                        case 13: // PolyLineZ
                        case 23: // PolyLineM
                            ReadPolyLineRecord(reader, shapeType, feature);
                            break;

                        case 5: // Polygon
                        case 15: // PolygonZ
                        case 25: // PolygonM
                            ReadPolygonRecord(reader, shapeType, feature);
                            break;

                        case 8: // MultiPoint
                        case 18: // MultiPointZ
                            ReadMultiPointRecord(reader, shapeType, feature);
                            break;

                        default:
                            break;
                    }

                    // Pular para o final do registro
                    if (reader.BaseStream.Position < recordEndPos)
                    {
                        reader.BaseStream.Seek(recordEndPos, SeekOrigin.Begin);
                    }

                    // Filtro opcional por texto
                    if (string.IsNullOrWhiteSpace(filter) || MatchesFilter(feature, filter))
                    {
                        features.Add(feature);
                    }

                    recordIndex++;
                }
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
                if (valStr != null && valStr.Contains(fLower))
                {
                    return true;
                }
            }
            return false;
        }

        #region Leitura de Registros Geométricos

        // Alguns SHP PolygonZ usam -Double.MaxValue como sentinela de Z ausente.
        // Mantemos o arquivo de origem intacto e projetamos apenas esse vértice em XY.
        private static double SafeZ(double z)
        {
            return double.IsNaN(z) || double.IsInfinity(z) || Math.Abs(z) > 1e30 ? 0.0 : z;
        }

        private static void ReadPointRecord(BinaryReader reader, int shapeType, ShpFeature feature)
        {
            double x = reader.ReadDouble();
            double y = reader.ReadDouble();
            double z = 0;
            if (shapeType == 11) // PointZ
            {
                z = SafeZ(reader.ReadDouble());
            }
            feature.Points.Add(new Point3d(x, y, z));
        }

        private static void ReadMultiPointRecord(BinaryReader reader, int shapeType, ShpFeature feature)
        {
            // Pular BoundingBox (32 bytes)
            reader.BaseStream.Seek(32, SeekOrigin.Current);
            int numPoints = reader.ReadInt32();
            for (int i = 0; i < numPoints; i++)
            {
                double x = reader.ReadDouble();
                double y = reader.ReadDouble();
                feature.Points.Add(new Point3d(x, y, 0));
            }
        }

        private static void ReadPolyLineRecord(BinaryReader reader, int shapeType, ShpFeature feature)
        {
            // BoundingBox (32 bytes)
            reader.BaseStream.Seek(32, SeekOrigin.Current);
            int numParts = reader.ReadInt32();
            int numPoints = reader.ReadInt32();

            int[] parts = new int[numParts];
            for (int i = 0; i < numParts; i++)
            {
                parts[i] = reader.ReadInt32();
            }

            var points = new Point2d[numPoints];
            for (int i = 0; i < numPoints; i++)
            {
                points[i] = new Point2d(reader.ReadDouble(), reader.ReadDouble());
            }

            // Valores Z se for PolyLineZ
            double[] zVals = null;
            if (shapeType == 13 && numPoints > 0)
            {
                try
                {
                    reader.ReadDouble(); // Zmin
                    reader.ReadDouble(); // Zmax
                    zVals = new double[numPoints];
                    for (int i = 0; i < numPoints; i++)
                    {
                        zVals[i] = reader.ReadDouble();
                    }
                }
                catch { }
            }

            // Construir curvas para cada parte
            for (int p = 0; p < numParts; p++)
            {
                int startIdx = parts[p];
                int endIdx = (p == numParts - 1) ? numPoints : parts[p + 1];
                int count = endIdx - startIdx;
                if (count < 2) continue;

                var poly = new Polyline(count);
                for (int i = startIdx; i < endIdx; i++)
                {
                    double z = (zVals != null && i < zVals.Length) ? SafeZ(zVals[i]) : 0.0;
                    poly.Add(points[i].X, points[i].Y, z);
                }

                if (poly.Count >= 2)
                {
                    feature.Curves.Add(new PolylineCurve(poly));
                }
            }
        }

        private static void ReadPolygonRecord(BinaryReader reader, int shapeType, ShpFeature feature)
        {
            // BoundingBox (32 bytes)
            reader.BaseStream.Seek(32, SeekOrigin.Current);
            int numParts = reader.ReadInt32();
            int numPoints = reader.ReadInt32();

            int[] parts = new int[numParts];
            for (int i = 0; i < numParts; i++)
            {
                parts[i] = reader.ReadInt32();
            }

            var points = new Point2d[numPoints];
            for (int i = 0; i < numPoints; i++)
            {
                points[i] = new Point2d(reader.ReadDouble(), reader.ReadDouble());
            }

            // Valores Z se for PolygonZ
            double[] zVals = null;
            if (shapeType == 15 && numPoints > 0)
            {
                try
                {
                    reader.ReadDouble(); // Zmin
                    reader.ReadDouble(); // Zmax
                    zVals = new double[numPoints];
                    for (int i = 0; i < numPoints; i++)
                    {
                        zVals[i] = reader.ReadDouble();
                    }
                }
                catch { }
            }

            var boundaryCurves = new List<Curve>();

            for (int p = 0; p < numParts; p++)
            {
                int startIdx = parts[p];
                int endIdx = (p == numParts - 1) ? numPoints : parts[p + 1];
                int count = endIdx - startIdx;
                if (count < 3) continue;

                var poly = new Polyline(count + 1);
                for (int i = startIdx; i < endIdx; i++)
                {
                    double z = (zVals != null && i < zVals.Length) ? SafeZ(zVals[i]) : 0.0;
                    poly.Add(points[i].X, points[i].Y, z);
                }

                // Fechar se não estiver fechado
                if (poly.Count > 2 && poly[0].DistanceTo(poly[poly.Count - 1]) > 1e-6)
                {
                    poly.Add(poly[0]);
                }

                if (poly.Count >= 4)
                {
                    var crv = new PolylineCurve(poly);
                    feature.Curves.Add(crv);
                    boundaryCurves.Add(crv);
                }
            }

            // Gerar Brep de Superfície plana para o polígono (Quadra)
            if (boundaryCurves.Count > 0)
            {
                try
                {
                    var breps = Brep.CreatePlanarBreps(boundaryCurves, 0.01);
                    if (breps != null && breps.Length > 0)
                    {
                        feature.Surface = breps[0];
                    }
                }
                catch { }
            }
        }

        #endregion

        #region Leitor de Tabela de Atributos dBASE (.dbf)

        private struct DbfField
        {
            public string Name;
            public char Type;
            public byte Length;
            public byte Decimals;
        }

        private static List<Dictionary<string, object>> ReadDbf(string dbfPath, out List<string> fieldNames)
        {
            fieldNames = new List<string>();
            var rows = new List<Dictionary<string, object>>();

            using (var fs = new FileStream(dbfPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new BinaryReader(fs))
            {
                if (fs.Length < 32) return rows;

                byte version = reader.ReadByte();
                byte year = reader.ReadByte();
                byte month = reader.ReadByte();
                byte day = reader.ReadByte();

                int numRecords = reader.ReadInt32();
                short headerLength = reader.ReadInt16();
                short recordLength = reader.ReadInt16();

                // Pular até a tabela de descritores de campos (posição 32)
                reader.BaseStream.Seek(32, SeekOrigin.Begin);

                var fields = new List<DbfField>();
                while (reader.BaseStream.Position < headerLength - 1)
                {
                    byte firstByte = reader.ReadByte();
                    if (firstByte == 0x0D) break; // Fim do cabeçalho

                    byte[] nameBytes = new byte[11];
                    nameBytes[0] = firstByte;
                    reader.Read(nameBytes, 1, 10);

                    string rawName = Encoding.ASCII.GetString(nameBytes).TrimEnd('\0', ' ');
                    char fType = (char)reader.ReadByte();
                    reader.ReadInt32(); // Reserved / memory address

                    byte fLen = reader.ReadByte();
                    byte fDec = reader.ReadByte();

                    // Pular os 14 bytes restantes do descritor (total 32 bytes)
                    reader.BaseStream.Seek(14, SeekOrigin.Current);

                    fields.Add(new DbfField
                    {
                        Name = rawName,
                        Type = fType,
                        Length = fLen,
                        Decimals = fDec
                    });
                    fieldNames.Add(rawName);
                }

                // Posicionar no início dos registros
                reader.BaseStream.Seek(headerLength, SeekOrigin.Begin);

                // Detectar codificação de caracteres (Latin1 / Windows-1252 para nomes em português)
                Encoding encoding;
                try
                {
                    encoding = Encoding.GetEncoding(1252);
                }
                catch
                {
                    encoding = Encoding.UTF8;
                }

                for (int r = 0; r < numRecords; r++)
                {
                    if (reader.BaseStream.Position >= reader.BaseStream.Length) break;

                    byte deleteFlag = reader.ReadByte(); // ' ' normal, '*' deletado
                    var row = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

                    foreach (var f in fields)
                    {
                        byte[] fieldBytes = reader.ReadBytes(f.Length);
                        string valStr = encoding.GetString(fieldBytes).Trim();

                        object val = valStr;
                        if (f.Type == 'N' || f.Type == 'F')
                        {
                            if (double.TryParse(valStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double dVal))
                            {
                                val = dVal;
                            }
                        }
                        else if (f.Type == 'L') // Logical (Y, y, N, n, T, t, F, f)
                        {
                            val = (valStr.Equals("Y", StringComparison.OrdinalIgnoreCase) || valStr.Equals("T", StringComparison.OrdinalIgnoreCase));
                        }

                        row[f.Name] = val;
                    }

                    if (deleteFlag != '*')
                    {
                        rows.Add(row);
                    }
                }
            }

            return rows;
        }

        #endregion

        #region Helpers de Leitura Binária (Big Endian)

        private static int ReadBigInt32(BinaryReader reader)
        {
            byte[] bytes = reader.ReadBytes(4);
            if (bytes.Length < 4) return 0;
            return (bytes[0] << 24) | (bytes[1] << 16) | (bytes[2] << 8) | bytes[3];
        }

        #endregion
    }
}
