using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace Buraqueira_Urb
{
    // Deliberately independent of Rhino: the database and schema can be tested outside Grasshopper.
    public sealed class StreetGisRecord
    {
        public string StreetID, StreetName, StreetType, FeatureID, SourceID, RunID, Status, ConflictReason, Scenario;
        public byte[] Centerline;
        public readonly List<StreetGisElementRecord> Elements = new List<StreetGisElementRecord>();
    }
    public sealed class StreetGisElementRecord
    {
        public string ElementID, ElementType, Status;
        public int ElementOrder, Direction;
        public double MinWidth, MaxWidth;
        public bool IsFixed, Required;
    }
    public sealed class StreetGisSectionRecord
    {
        public string StreetID, RunID, SectionID, Status, Reason;
        public int StationIndex;
        public double AvailableWidth;
        public readonly List<StreetGisElementRecord> Elements = new List<StreetGisElementRecord>();
    }
    public sealed class StreetGisSurfaceRecord
    {
        public string StreetID, RunID, ElementID, ElementType, Status, Scenario;
        public int ElementOrder;
        public byte[] Polygon;
    }
    public sealed class StreetGisPackage
    {
        public int Epsg;
        public string CrsDefinition, WidthUnit;
        public readonly List<StreetGisRecord> Streets = new List<StreetGisRecord>();
        public readonly List<StreetGisSectionRecord> Sections = new List<StreetGisSectionRecord>();
        public readonly List<StreetGisSurfaceRecord> Surfaces = new List<StreetGisSurfaceRecord>();
    }

    public static class StreetGisWriter
    {
        private const string Sqlite = "winsqlite3.dll";
        [DllImport(Sqlite, CallingConvention=CallingConvention.Cdecl, CharSet=CharSet.Ansi)]
        private static extern int sqlite3_open_v2(byte[] filename, out IntPtr db, int flags, IntPtr vfs);
        [DllImport(Sqlite, CallingConvention=CallingConvention.Cdecl)]
        private static extern int sqlite3_close(IntPtr db);
        [DllImport(Sqlite, CallingConvention=CallingConvention.Cdecl, CharSet=CharSet.Ansi)]
        private static extern int sqlite3_exec(IntPtr db, byte[] sql, IntPtr callback, IntPtr context, out IntPtr error);
        [DllImport(Sqlite, CallingConvention=CallingConvention.Cdecl)]
        private static extern void sqlite3_free(IntPtr pointer);
        [DllImport(Sqlite, CallingConvention=CallingConvention.Cdecl, CharSet=CharSet.Ansi)]
        private static extern int sqlite3_prepare_v2(IntPtr db, byte[] sql, int bytes, out IntPtr statement, IntPtr tail);
        [DllImport(Sqlite, CallingConvention=CallingConvention.Cdecl)]
        private static extern int sqlite3_step(IntPtr statement);
        [DllImport(Sqlite, CallingConvention=CallingConvention.Cdecl)]
        private static extern int sqlite3_finalize(IntPtr statement);
        [DllImport(Sqlite, CallingConvention=CallingConvention.Cdecl)]
        private static extern long sqlite3_column_int64(IntPtr statement, int column);

        public static string Write(StreetGisPackage package, string target, bool overwrite)
        {
            Validate(package, target);
            string full = Path.GetFullPath(target);
            if (File.Exists(full) && !overwrite) throw new IOException("Arquivo existente; ative Overwrite explicitamente.");
            string folder = Path.GetDirectoryName(full);
            if (!Directory.Exists(folder)) throw new DirectoryNotFoundException(folder);
            string temp = Path.Combine(folder, "." + Path.GetFileName(full) + "." + Guid.NewGuid().ToString("N") + ".tmp");
            IntPtr db = IntPtr.Zero;
            try
            {
                if (sqlite3_open_v2(Utf8(temp), out db, 0x00000002 | 0x00000004, IntPtr.Zero) != 0)
                    throw new IOException("Não foi possível abrir o GeoPackage temporário.");
                Exec(db, "PRAGMA application_id=1196444487; PRAGMA user_version=10400; PRAGMA foreign_keys=ON; BEGIN IMMEDIATE;");
                CreateCore(db, package);
                InsertData(db, package);
                Exec(db, "COMMIT;");
                Check(db, package);
                sqlite3_close(db); db = IntPtr.Zero;
                if (File.Exists(full))
                {
                    // File.Replace is atomic on the same volume and keeps the former file if it fails.
                    File.Replace(temp, full, null);
                }
                else File.Move(temp, full);
                return full;
            }
            catch
            {
                if (db != IntPtr.Zero) { try { Exec(db, "ROLLBACK;"); } catch { } }
                throw;
            }
            finally
            {
                if (db != IntPtr.Zero) sqlite3_close(db);
                if (File.Exists(temp)) File.Delete(temp);
            }
        }

        private static void Validate(StreetGisPackage p, string target)
        {
            ValidateModel(p);
            if (string.IsNullOrWhiteSpace(target) || !target.EndsWith(".gpkg", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("O destino deve ser um arquivo .gpkg.");
        }
        internal static void ValidateModel(StreetGisPackage p)
        {
            if (p == null || p.Streets.Count == 0) throw new ArgumentException("Nenhuma via para exportar.");
            if (p.Epsg <= 0 || string.IsNullOrWhiteSpace(p.CrsDefinition))
                throw new ArgumentException("Informe EPSG e definição WKT do CRS original; não é seguro presumir um CRS.");
            if (string.IsNullOrWhiteSpace(p.WidthUnit)) throw new ArgumentException("Informe a unidade das larguras.");
            foreach (var street in p.Streets)
            {
                if (string.IsNullOrWhiteSpace(street.StreetID) || string.IsNullOrWhiteSpace(street.FeatureID) ||
                    street.Centerline == null || street.Centerline.Length == 0)
                    throw new ArgumentException("Toda feição precisa de StreetID, FeatureID e eixo válido.");
                foreach (var e in street.Elements)
                    if (e.MinWidth < 0 || e.MaxWidth < e.MinWidth || double.IsNaN(e.MaxWidth) || double.IsInfinity(e.MaxWidth))
                        throw new ArgumentException("Domínio de largura inválido.");
            }
            var ids = new HashSet<string>(p.Streets.Select(x => x.StreetID), StringComparer.Ordinal);
            if (p.Sections.Any(x => !ids.Contains(x.StreetID)) || p.Surfaces.Any(x => !ids.Contains(x.StreetID)))
                throw new ArgumentException("Seção ou superfície sem StreetID correspondente.");
            foreach(var group in p.Streets.GroupBy(x=>x.StreetID))
            {
                var first=group.First();
                if(group.Any(x=>!SameElements(first.Elements,x.Elements) || x.StreetName!=first.StreetName || x.StreetType!=first.StreetType))
                    throw new ArgumentException("StreetID compartilhado por perfis semânticos diferentes: "+group.Key);
            }
        }

        private static void CreateCore(IntPtr db, StreetGisPackage p)
        {
            Exec(db, "CREATE TABLE gpkg_spatial_ref_sys (srs_name TEXT NOT NULL, srs_id INTEGER NOT NULL PRIMARY KEY, organization TEXT NOT NULL, organization_coordsys_id INTEGER NOT NULL, definition TEXT NOT NULL, description TEXT);"+
                "CREATE TABLE gpkg_contents (table_name TEXT NOT NULL PRIMARY KEY,data_type TEXT NOT NULL,identifier TEXT UNIQUE,description TEXT DEFAULT '',last_change DATETIME NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),min_x DOUBLE,min_y DOUBLE,max_x DOUBLE,max_y DOUBLE,srs_id INTEGER,FOREIGN KEY(srs_id) REFERENCES gpkg_spatial_ref_sys(srs_id));"+
                "CREATE TABLE gpkg_geometry_columns (table_name TEXT NOT NULL,column_name TEXT NOT NULL,geometry_type_name TEXT NOT NULL,srs_id INTEGER NOT NULL,z TINYINT NOT NULL,m TINYINT NOT NULL,PRIMARY KEY(table_name,column_name),FOREIGN KEY(srs_id) REFERENCES gpkg_spatial_ref_sys(srs_id));"+
                "INSERT INTO gpkg_spatial_ref_sys VALUES('Undefined Cartesian',-1,'NONE',-1,'undefined','');"+
                "INSERT INTO gpkg_spatial_ref_sys VALUES('Undefined Geographic',0,'NONE',0,'undefined','');"+
                "INSERT INTO gpkg_spatial_ref_sys VALUES('WGS 84',4326,'EPSG',4326,'GEOGCS[\"WGS 84\",DATUM[\"WGS_1984\",SPHEROID[\"WGS 84\",6378137,298.257223563]],PRIMEM[\"Greenwich\",0],UNIT[\"degree\",0.0174532925199433]]','');");
            if (p.Epsg != 4326) Exec(db, "INSERT INTO gpkg_spatial_ref_sys VALUES(" + Q("EPSG:" + p.Epsg) + "," + p.Epsg + ",'EPSG'," + p.Epsg + "," + Q(p.CrsDefinition) + ",'');");
            else Exec(db, "UPDATE gpkg_spatial_ref_sys SET definition=" + Q(p.CrsDefinition) + " WHERE srs_id=4326;");
            Exec(db, "CREATE TABLE streets (StreetID TEXT PRIMARY KEY,StreetName TEXT,StreetType TEXT,ProfileSignature TEXT,TotalMinWidth REAL,TotalMaxWidth REAL,WidthUnit TEXT,Status TEXT,ConflictReason TEXT);"+
                "CREATE TABLE street_profile_elements (StreetID TEXT NOT NULL,ElementOrder INTEGER NOT NULL,ElementID TEXT,ElementType TEXT NOT NULL,MinWidth REAL NOT NULL,MaxWidth REAL NOT NULL,IsFixed INTEGER NOT NULL,Direction INTEGER NOT NULL,Required INTEGER NOT NULL,PRIMARY KEY(StreetID,ElementOrder),FOREIGN KEY(StreetID) REFERENCES streets(StreetID));"+
                "CREATE TABLE street_centerlines (fid INTEGER PRIMARY KEY AUTOINCREMENT,StreetID TEXT NOT NULL,FeatureID TEXT NOT NULL,RunID TEXT,SourceID TEXT,StreetName TEXT,StreetType TEXT,Scenario TEXT,Status TEXT,geom BLOB NOT NULL,FOREIGN KEY(StreetID) REFERENCES streets(StreetID));"+
                "CREATE TABLE glaux_export_metadata (key TEXT PRIMARY KEY,value TEXT NOT NULL);");
            AddContent(db,"streets","attributes",p.Epsg);
            AddContent(db,"street_profile_elements","attributes",p.Epsg);
            AddGeometry(db,"street_centerlines","LINESTRING",p.Epsg,1);
            AddContent(db,"glaux_export_metadata","attributes",p.Epsg);
            if (p.Sections.Count > 0)
            {
                Exec(db,"CREATE TABLE street_sections (StreetID TEXT NOT NULL,RunID TEXT,SectionID TEXT NOT NULL,StationIndex INTEGER NOT NULL,AvailableWidth REAL,Status TEXT,Reason TEXT,PRIMARY KEY(StreetID,SectionID),FOREIGN KEY(StreetID) REFERENCES streets(StreetID));"+
                    "CREATE TABLE street_section_elements (StreetID TEXT NOT NULL,SectionID TEXT NOT NULL,ElementOrder INTEGER NOT NULL,ElementID TEXT,ActualWidth REAL NOT NULL,Status TEXT,PRIMARY KEY(StreetID,SectionID,ElementOrder),FOREIGN KEY(StreetID,SectionID) REFERENCES street_sections(StreetID,SectionID));");
                AddContent(db,"street_sections","attributes",p.Epsg); AddContent(db,"street_section_elements","attributes",p.Epsg);
            }
            if (p.Surfaces.Count > 0)
            {
                Exec(db,"CREATE TABLE street_element_surfaces (fid INTEGER PRIMARY KEY AUTOINCREMENT,StreetID TEXT NOT NULL,RunID TEXT,ElementOrder INTEGER NOT NULL,ElementID TEXT,ElementType TEXT,Scenario TEXT,Status TEXT,geom BLOB NOT NULL,FOREIGN KEY(StreetID) REFERENCES streets(StreetID));");
                AddGeometry(db,"street_element_surfaces","POLYGON",p.Epsg,1);
            }
        }
        private static void InsertData(IntPtr db, StreetGisPackage p)
        {
            Exec(db,"INSERT INTO glaux_export_metadata VALUES('WidthUnit',"+Q(p.WidthUnit)+");");
            Exec(db,"INSERT INTO glaux_export_metadata VALUES('CRS',"+Q("EPSG:"+p.Epsg)+");");
            foreach (var group in p.Streets.GroupBy(x=>x.StreetID))
            {
                var first=group.First(); var elements=first.Elements;
                string signature=string.Join("|",elements.Select(e=>e.ElementID+":"+e.ElementType+":"+e.MinWidth.ToString("R",CultureInfo.InvariantCulture)+":"+e.MaxWidth.ToString("R",CultureInfo.InvariantCulture)+":"+e.Direction));
                if(group.Any(x=>!SameElements(elements,x.Elements) || x.StreetName!=first.StreetName || x.StreetType!=first.StreetType))
                    throw new ArgumentException("StreetID compartilhado por perfis semânticos diferentes: "+group.Key);
                Exec(db,"INSERT INTO streets VALUES("+Q(first.StreetID)+","+Q(first.StreetName)+","+Q(first.StreetType)+","+Q(signature)+","+N(elements.Sum(x=>x.MinWidth))+","+N(elements.Sum(x=>x.MaxWidth))+","+Q(p.WidthUnit)+","+Q(first.Status)+","+Q(first.ConflictReason)+");");
                foreach(var e in elements)
                    Exec(db,"INSERT INTO street_profile_elements VALUES("+Q(first.StreetID)+","+e.ElementOrder+","+Q(e.ElementID)+","+Q(e.ElementType)+","+N(e.MinWidth)+","+N(e.MaxWidth)+","+(e.IsFixed?1:0)+","+e.Direction+","+(e.Required?1:0)+");");
            }
            foreach(var street in p.Streets)
                Exec(db,"INSERT INTO street_centerlines(StreetID,FeatureID,RunID,SourceID,StreetName,StreetType,Scenario,Status,geom) VALUES("+
                    Q(street.StreetID)+","+Q(street.FeatureID)+","+Q(street.RunID)+","+Q(street.SourceID)+","+Q(street.StreetName)+","+Q(street.StreetType)+","+Q(street.Scenario)+","+Q(street.Status)+","+Hex(street.Centerline)+");");
            foreach(var section in p.Sections)
            {
                Exec(db,"INSERT INTO street_sections VALUES("+Q(section.StreetID)+","+Q(section.RunID)+","+Q(section.SectionID)+","+section.StationIndex+","+N(section.AvailableWidth)+","+Q(section.Status)+","+Q(section.Reason)+");");
                foreach(var e in section.Elements)
                    Exec(db,"INSERT INTO street_section_elements VALUES("+Q(section.StreetID)+","+Q(section.SectionID)+","+e.ElementOrder+","+Q(e.ElementID)+","+N(e.MaxWidth)+","+Q(e.Status)+");");
            }
            foreach(var surface in p.Surfaces)
                Exec(db,"INSERT INTO street_element_surfaces(StreetID,RunID,ElementOrder,ElementID,ElementType,Scenario,Status,geom) VALUES("+
                    Q(surface.StreetID)+","+Q(surface.RunID)+","+surface.ElementOrder+","+Q(surface.ElementID)+","+Q(surface.ElementType)+","+Q(surface.Scenario)+","+Q(surface.Status)+","+Hex(surface.Polygon)+");");
        }
        private static bool SameElements(List<StreetGisElementRecord> a,List<StreetGisElementRecord> b) =>
            a.Count==b.Count && !a.Where((e,i)=>e.ElementOrder!=b[i].ElementOrder || e.ElementID!=b[i].ElementID || e.ElementType!=b[i].ElementType || e.MinWidth!=b[i].MinWidth || e.MaxWidth!=b[i].MaxWidth || e.Direction!=b[i].Direction).Any();
        private static void AddContent(IntPtr db,string table,string type,int epsg) => Exec(db,"INSERT INTO gpkg_contents(table_name,data_type,identifier,srs_id) VALUES("+Q(table)+","+Q(type)+","+Q(table)+","+(type=="features"?epsg.ToString(CultureInfo.InvariantCulture):"NULL")+");");
        private static void AddGeometry(IntPtr db,string table,string type,int epsg,int z)
        { AddContent(db,table,"features",epsg); Exec(db,"INSERT INTO gpkg_geometry_columns VALUES("+Q(table)+",'geom',"+Q(type)+","+epsg+","+z+",0);"); }
        private static void Check(IntPtr db,StreetGisPackage p)
        {
            if(Scalar(db,"SELECT count(*) FROM street_centerlines;")!=p.Streets.Count ||
               Scalar(db,"SELECT count(*) FROM street_profile_elements;")!=p.Streets.GroupBy(x=>x.StreetID).Sum(g=>g.First().Elements.Count) ||
               (p.Sections.Count>0 && Scalar(db,"SELECT count(*) FROM street_sections;")!=p.Sections.Count) ||
               (p.Surfaces.Count>0 && Scalar(db,"SELECT count(*) FROM street_element_surfaces;")!=p.Surfaces.Count))
                throw new IOException("Falha na validação das contagens do GeoPackage.");
            if(Scalar(db,"SELECT count(*) FROM pragma_foreign_key_check;")!=0) throw new IOException("Relações inválidas no GeoPackage.");
            if(Scalar(db,"SELECT count(*) FROM pragma_integrity_check WHERE integrity_check <> 'ok';")!=0)
                throw new IOException("Integridade SQLite inválida.");
        }
        private static long Scalar(IntPtr db,string sql)
        { IntPtr s; if(sqlite3_prepare_v2(db,Utf8(sql),-1,out s,IntPtr.Zero)!=0) throw new IOException("Falha na validação SQLite."); try { return sqlite3_step(s)==100?sqlite3_column_int64(s,0):-1; } finally { sqlite3_finalize(s); } }
        private static void Exec(IntPtr db,string sql)
        { IntPtr err; int rc=sqlite3_exec(db,Utf8(sql),IntPtr.Zero,IntPtr.Zero,out err); if(rc==0)return; string message=err==IntPtr.Zero?"SQLite error "+rc:Marshal.PtrToStringAnsi(err); if(err!=IntPtr.Zero)sqlite3_free(err); throw new IOException(message); }
        private static byte[] Utf8(string s)=>Encoding.UTF8.GetBytes(s+"\0");
        private static string Q(string s)=>s==null?"NULL":"'"+s.Replace("'","''")+"'";
        private static string N(double n)=>double.IsNaN(n)||double.IsInfinity(n)?"NULL":n.ToString("R",CultureInfo.InvariantCulture);
        private static string Hex(byte[] bytes)=>"X'"+BitConverter.ToString(bytes).Replace("-","")+"'";

        // Standard GeoPackageBinary with a 3D WKB geometry and no envelope.
        public static byte[] Geometry(IEnumerable<double[]> coordinates,int epsg,bool polygon)
        {
            var points=coordinates.ToList();
            if(polygon && points.Count>0 && !points[0].SequenceEqual(points[points.Count-1])) points.Add(points[0]);
            if(points.Count<(polygon?4:2) || points.Any(p=>p.Length<3 || p.Take(3).Any(x=>double.IsNaN(x)||double.IsInfinity(x))))
                throw new ArgumentException("Geometria GIS inválida.");
            using(var ms=new MemoryStream()) using(var w=new BinaryWriter(ms))
            {
                w.Write((byte)'G'); w.Write((byte)'P'); w.Write((byte)0); w.Write((byte)1); w.Write(epsg);
                w.Write((byte)1); w.Write((uint)(polygon?1003:1002));
                if(polygon)w.Write((uint)1);
                w.Write((uint)points.Count);
                foreach(var p in points){w.Write(p[0]);w.Write(p[1]);w.Write(p[2]);}
                return ms.ToArray();
            }
        }
    }
}
