using System.Globalization;
using System.Text;
using System.Runtime.InteropServices;
using Buraqueira_Urb;

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
int checks = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new Exception("FAIL: " + name);
    checks++;
}
string directory = Path.Combine(Path.GetTempPath(), "glaux_gis_import_" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(directory);
try
{
    string utf8 = MakeShapefile(directory, "utf8", Encoding.UTF8, "UTF-8", 0);
    CheckShapefile(utf8, "CPG", "UTF-8");
    string cp1252 = MakeShapefile(directory, "cp1252", Encoding.GetEncoding(1252), "1252", 0);
    CheckShapefile(cp1252, "CPG", "1252");
    string latin1 = MakeShapefile(directory, "latin1", Encoding.GetEncoding("ISO-8859-1"), "ISO-8859-1", 0);
    CheckShapefile(latin1, "CPG", "ISO-8859-1");
    File.Delete(Path.ChangeExtension(cp1252, ".cpg"));
    var manual = ShapefileReader.ReadShapefile(cp1252, out _, out string manualInfo, encodingName:"Windows-1252");
    Check(manual[0].GetAttributeString("NOME") == "Rua São José" && manualInfo.Contains("EncodingSource=User"), "manual encoding override");
    string ldid = MakeShapefile(directory, "ldid", Encoding.GetEncoding(1252), null, 0x03);
    var ldidFeatures = ShapefileReader.ReadShapefile(ldid, out _, out string ldidInfo);
    Check(ldidFeatures[0].GetAttributeString("NOME") == "Rua São José" && ldidInfo.Contains("DBF-LDID"), "DBF metadata encoding");
    string fallback = MakeShapefile(directory, "fallback", Encoding.GetEncoding(1252), null, 0);
    ShapefileReader.ReadShapefile(fallback, out _, out string defaultInfo);
    Check(defaultInfo.Contains("Default (unverified)"), "unverified fallback reported honestly");
    CheckThrows(() => ShapefileReader.ReadShapefile(utf8, out _, encodingName:"not-an-encoding"), "invalid override rejected");

    var package = new StreetGisPackage { Epsg=31985, CrsDefinition="PROJCS[\"Fixture\"]", WidthUnit="m" };
    var street = new StreetGisRecord { StreetID="street1", StreetName="Rua São José", StreetType="Local",
        FeatureID="feature1", SourceID=null, Scenario="EXISTING", Status="MATCHED" };
    var coordinates = new List<double[]> { new[]{100.0,200.0,0.0}, new[]{110.0,200.0,0.0} };
    street.Centerline=StreetGisWriter.Geometry(coordinates,31985,false);
    package.Streets.Add(street);
    string gpkg = Path.Combine(directory, "Ação São José.gpkg");
    StreetGisWriter.Write(package,gpkg,false);
    NativeSqlite.Exec(gpkg, "ALTER TABLE street_centerlines ADD COLUMN \"NÓME\" TEXT; " +
        "UPDATE street_centerlines SET \"NÓME\"='Avenida João Pessoa'; " +
        "UPDATE gpkg_contents SET identifier='Vias São José' WHERE table_name='street_centerlines';");
    var layers = GpkgReader.GetLayers(gpkg);
    Check(layers.Any(x => x.TableName == "street_centerlines" && x.SrsId == 31985), "GPKG layer and CRS");
    Check(layers.Any(x => x.TableName == "street_centerlines" && x.Identifier == "Vias São José"), "GPKG UTF-8 layer metadata");
    var gpkgFeatures = GpkgReader.ReadGeoPackage(gpkg,"street_centerlines",out var gpkgFields);
    Check(gpkgFeatures.Count == 1 && gpkgFeatures[0].GetAttributeString("StreetName") == "Rua São José", "GPKG UTF-8 text and accented path");
    Check(gpkgFields.Contains("StreetName") && gpkgFeatures[0].GetAttribute("fid") is long, "GPKG field names and integer type");
    Check(gpkgFields.Contains("NÓME") && gpkgFeatures[0].GetAttributeString("NÓME") == "Avenida João Pessoa", "GPKG UTF-8 field name and value");
    GisImportOutputs.Build(gpkgFeatures,gpkgFields,out var gpkgValues,out var gpkgGeometry);
    Check(gpkgValues.Branches[0].Count == gpkgFields.Count && gpkgGeometry.Branches[0].Count > 0, "GPKG field/value/geometry feature path");
    Check(gpkgValues.Branches[0][gpkgFields.IndexOf("StreetName")].Value.Equals("Rua São José"), "GPKG same ordered output model");
    try { GpkgReader.ReadGeoPackage(gpkg,"nao_existe",out _); throw new Exception("FAIL: missing GPKG layer"); }
    catch (InvalidDataException) { checks++; }
    Console.WriteLine($"GIS import reader contracts: {checks}/{checks} PASS");

    if (args.Length > 0 && args[0] == "--picui")
    {
        RunPicuiDiagnostic();
    }
}
finally { Directory.Delete(directory, true); }

void RunPicuiDiagnostic()
{
    string logPath = Environment.GetEnvironmentVariable("GLAUX_PICUI_LOGRADOUROS");
    string quadPath = Environment.GetEnvironmentVariable("GLAUX_PICUI_QUADRAS");

    Console.WriteLine("\n=== PICUI DIAGNOSTIC ===");
    Console.WriteLine("Logradouros Path: " + logPath + " (Exists=" + File.Exists(logPath) + ")");
    Console.WriteLine("Quadras Path: " + quadPath + " (Exists=" + File.Exists(quadPath) + ")");

    if (File.Exists(logPath))
    {
        var feats = ShapefileReader.ReadShapefile(logPath, out var fields, out var info);
        Console.WriteLine($"Logradouros: {feats.Count} features. Encoding info: {info}");
        Console.WriteLine("Fields: " + string.Join(", ", fields));
        for (int i = 0; i < feats.Count; i++)
        {
            var f = feats[i];
            string nome = f.GetAttributeString("NOME") ?? "";
            string tipo = f.GetAttributeString("TIPO") ?? "";
            int crvCount = f.Curves?.Count ?? 0;
            Console.WriteLine($"  [{i,2}] id={f.GetAttribute("id")} | {tipo,-10} | {nome,-30} | Crvs={crvCount}");
        }
    }

    if (File.Exists(quadPath))
    {
        var qFeats = ShapefileReader.ReadShapefile(quadPath, out var qFields, out var qInfo);
        Console.WriteLine($"\nQuadras: {qFeats.Count} features. Encoding info: {qInfo}");
        Console.WriteLine("Fields: " + string.Join(", ", qFields));
        for (int i = 0; i < Math.Min(10, qFeats.Count); i++)
        {
            var f = qFeats[i];
            int crvCount = f.Curves?.Count ?? 0;
            Console.WriteLine($"  [{i,2}] Quadra crvs={crvCount}");
        }
    }
}

void CheckShapefile(string shp, string source, string encoding)
{
    var features=ShapefileReader.ReadShapefile(shp,out var fields,out var info);
    string[] names={"Rua São José","Avenida João Pessoa",null,"Açude Novo","José de Alencar","Coração de Jesus"};
    Check(features.Count==names.Length && info.Contains("EncodingSource="+source), $"{encoding} feature count and encoding source");
    Check(fields.SequenceEqual(new[]{"NOME","TIPO","FAIXAS","LARGURA","LOGICO","DATA","VAZIO"}), $"{encoding} ordered schema");
    for(int i=0;i<names.Length;i++)
        Check(features[i].RecordNumber==i+1 && features[i].Points.Count==1 &&
            (names[i]==null ? features[i].Attributes==null : features[i].GetAttributeString("NOME")==names[i]),
            $"{encoding} accented record/geometric alignment {i}");
    Check(features[0].GetAttribute("FAIXAS") is long n && n==2, $"{encoding} integer");
    Check(features[0].GetAttribute("LARGURA") is double d && Math.Abs(d-7.5)<1e-8, $"{encoding} double");
    Check(features[0].GetAttribute("LOGICO") is bool b && b, $"{encoding} boolean");
    Check(features[0].GetAttribute("DATA") is DateTime date && date.Year==2026, $"{encoding} date");
    Check(features[0].GetAttribute("VAZIO")==null, $"{encoding} null retained");
    GisImportOutputs.Build(features,fields,out var values,out var geometry);
    Check(values.Branches.Count==features.Count && geometry.Branches.Count==features.Count, $"{encoding} path count");
    Check(values.Branches[0].Count==fields.Count && values.Branches[0][0].Value.Equals(names[0]), $"{encoding} clean values ordered by Fields");
    Check(values.Branches[0][2].Value is long && values.Branches[0][3].Value is double &&
        values.Branches[0][6].Value is GisNullValue, $"{encoding} typed GH values and explicit null");
    Check(values.Branches[2].All(x=>x.Value is GisNullValue) && geometry.Branches[2].Count==1,
        $"{encoding} deleted DBF slot keeps SHP feature alignment");
}

void CheckThrows(Action action,string name)
{
    try { action(); throw new Exception("FAIL: "+name); }
    catch (ArgumentException) { checks++; }
}

string MakeShapefile(string folder,string stem,Encoding encoding,string cpg,byte ldid)
{
    string path=Path.Combine(folder,stem+".shp");
    string[] names={"Rua São José","Avenida João Pessoa","EXCLUDED","Açude Novo","José de Alencar","Coração de Jesus"};
    using(var fs=File.Create(path)) using(var w=new BinaryWriter(fs))
    {
        WriteBig(w,9994); for(int i=0;i<5;i++) WriteBig(w,0);
        WriteBig(w,(100+names.Length*28)/2); w.Write(1000); w.Write(1);
        for(int i=0;i<8;i++) w.Write(0.0);
        for(int i=0;i<names.Length;i++) { WriteBig(w,i+1); WriteBig(w,10); w.Write(1); w.Write((double)i); w.Write(0.0); }
    }
    var fieldSpecs=new (string Name,char Type,int Length,int Decimals)[]
    { ("NOME",'C',32,0),("TIPO",'C',12,0),("FAIXAS",'N',4,0),("LARGURA",'N',8,2),
      ("LOGICO",'L',1,0),("DATA",'D',8,0),("VAZIO",'C',4,0) };
    int recordLength=1+fieldSpecs.Sum(x=>x.Length);
    using(var fs=File.Create(Path.ChangeExtension(path,".dbf"))) using(var w=new BinaryWriter(fs))
    {
        byte[] header=new byte[32]; header[0]=3; header[1]=126; header[2]=10; header[3]=1;
        BitConverter.GetBytes(names.Length).CopyTo(header,4);
        BitConverter.GetBytes((short)(32+fieldSpecs.Length*32+1)).CopyTo(header,8);
        BitConverter.GetBytes((short)recordLength).CopyTo(header,10);
        header[29]=ldid; w.Write(header);
        foreach(var f in fieldSpecs)
        {
            var desc=new byte[32]; Encoding.ASCII.GetBytes(f.Name).CopyTo(desc,0);
            desc[11]=(byte)f.Type; desc[16]=(byte)f.Length; desc[17]=(byte)f.Decimals; w.Write(desc);
        }
        w.Write((byte)0x0d);
        for(int i=0;i<names.Length;i++)
        {
            w.Write((byte)(i==2?'*':' '));
            string[] cells={names[i],"Local","2","7.50","T","20261001",""};
            for(int j=0;j<fieldSpecs.Length;j++)
            {
                var bytes=encoding.GetBytes(cells[j]);
                if(bytes.Length>fieldSpecs[j].Length) throw new Exception("Fixture DBF field too short");
                w.Write(bytes); for(int k=bytes.Length;k<fieldSpecs[j].Length;k++) w.Write((byte)' ');
            }
        }
        w.Write((byte)0x1a);
    }
    if(cpg!=null) File.WriteAllText(Path.ChangeExtension(path,".cpg"),cpg,Encoding.ASCII);
    return path;
}

void WriteBig(BinaryWriter writer,int value)
{
    var bytes=BitConverter.GetBytes(value); Array.Reverse(bytes); writer.Write(bytes);
}

static class NativeSqlite
{
    [DllImport("winsqlite3.dll", CallingConvention=CallingConvention.Cdecl)]
    private static extern int sqlite3_open_v2(byte[] path,out IntPtr db,int flags,IntPtr vfs);
    [DllImport("winsqlite3.dll", CallingConvention=CallingConvention.Cdecl)]
    private static extern int sqlite3_exec(IntPtr db,byte[] sql,IntPtr callback,IntPtr context,out IntPtr error);
    [DllImport("winsqlite3.dll", CallingConvention=CallingConvention.Cdecl)]
    private static extern int sqlite3_close(IntPtr db);
    private static byte[] Utf8(string text) => Encoding.UTF8.GetBytes(text+"\0");
    public static void Exec(string path,string sql)
    {
        if(sqlite3_open_v2(Utf8(path),out var db,2,IntPtr.Zero)!=0) throw new Exception("SQLite fixture open failed");
        try { if(sqlite3_exec(db,Utf8(sql),IntPtr.Zero,IntPtr.Zero,out _)!=0) throw new Exception("SQLite fixture update failed"); }
        finally { sqlite3_close(db); }
    }
}
