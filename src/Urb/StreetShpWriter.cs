using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Buraqueira_Urb
{
    // Legacy companion format. Geometry lives in SHP; relational rows remain in UTF-8 CSV.
    public static class StreetShpWriter
    {
        private static readonly Encoding Utf8 = new UTF8Encoding(false);
        private sealed class Feature
        {
            public byte[] Gpb;
            public string[] Fields;
        }
        public static string Write(StreetGisPackage package,string target,bool overwrite)
        {
            StreetGisWriter.ValidateModel(package);
            if(string.IsNullOrWhiteSpace(target) || !target.EndsWith(".shp",StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("O destino SHP deve terminar em .shp.");
            if(package.Epsg<=0 || string.IsNullOrWhiteSpace(package.CrsDefinition) || string.IsNullOrWhiteSpace(package.WidthUnit))
                throw new ArgumentException("EPSG, WKT e unidade são obrigatórios.");
            string full=Path.GetFullPath(target);
            string folder=Path.Combine(Path.GetDirectoryName(full),Path.GetFileNameWithoutExtension(full)+"_shp");
            if(Directory.Exists(folder) && !overwrite)throw new IOException("Diretório SHP existente; ative Overwrite explicitamente.");
            string temp=folder+".tmp_"+Guid.NewGuid().ToString("N");
            string backup=null;
            try
            {
                Directory.CreateDirectory(temp);
                var centerlines=package.Streets.Select(s=>new Feature{Gpb=s.Centerline,Fields=new[]{s.StreetID,s.FeatureID,s.SourceID,s.RunID,s.StreetName,s.StreetType,s.Scenario,s.Status}}).ToList();
                WriteLayer(temp,"street_centerlines",13,
                    new[]{"STREET_ID","FEATURE_ID","SOURCE_ID","RUN_ID","STREET_NM","STREET_TY","SCENARIO","STATUS"},
                    centerlines,package.CrsDefinition);
                if(package.Surfaces.Count>0)
                {
                    var surfaces=package.Surfaces.Select(s=>new Feature{Gpb=s.Polygon,Fields=new[]{s.StreetID,s.RunID,s.ElementID,
                        s.ElementOrder.ToString(CultureInfo.InvariantCulture),s.ElementType,s.Scenario,s.Status}}).ToList();
                    WriteLayer(temp,"street_element_surfaces",15,
                        new[]{"STREET_ID","RUN_ID","ELEM_ID","ELEM_ORDER","ELEM_TYPE","SCENARIO","STATUS"},surfaces,package.CrsDefinition);
                }
                var semantic=package.Streets.GroupBy(s=>s.StreetID).Select(g=>g.First()).ToList();
                WriteCsv(temp,"streets",new[]{"StreetID","StreetName","StreetType","WidthUnit","TotalMinWidth","TotalMaxWidth","Status","ConflictReason"},
                    semantic.Select(s=>new[]{s.StreetID,s.StreetName,s.StreetType,package.WidthUnit,
                        N(s.Elements.Sum(e=>e.MinWidth)),N(s.Elements.Sum(e=>e.MaxWidth)),s.Status,s.ConflictReason}));
                WriteCsv(temp,"street_profile_elements",new[]{"StreetID","ElementOrder","ElementID","ElementType","MinWidth","MaxWidth","IsFixed","Direction","Required"},
                    semantic.SelectMany(s=>s.Elements.Select(e=>new[]{s.StreetID,e.ElementOrder.ToString(),e.ElementID,e.ElementType,
                        N(e.MinWidth),N(e.MaxWidth),e.IsFixed?"1":"0",e.Direction.ToString(),e.Required?"1":"0"})));
                if(package.Sections.Count>0)
                {
                    WriteCsv(temp,"street_sections",new[]{"StreetID","RunID","SectionID","StationIndex","AvailableWidth","Status","Reason"},
                        package.Sections.Select(s=>new[]{s.StreetID,s.RunID,s.SectionID,s.StationIndex.ToString(),N(s.AvailableWidth),s.Status,s.Reason}));
                    WriteCsv(temp,"street_section_elements",new[]{"StreetID","SectionID","ElementOrder","ElementID","ActualWidth","Status"},
                        package.Sections.SelectMany(s=>s.Elements.Select(e=>new[]{s.StreetID,s.SectionID,e.ElementOrder.ToString(),e.ElementID,N(e.MaxWidth),e.Status})));
                }
                File.WriteAllText(Path.Combine(temp,"README.txt"),
                    "Glaux Urb SHP compatibility export\r\nEPSG:"+package.Epsg+"\r\nWidthUnit: "+package.WidthUnit+
                    "\r\nSHP layers use PolyLineZ/PolygonZ and UTF-8 DBF (.cpg). Semantic 1:N tables are UTF-8 CSV linked by StreetID, ElementID and SectionID. GeoPackage is the complete format.\r\n",Utf8);
                if(Directory.GetFiles(temp,"*.shp").Length==0 || Directory.GetFiles(temp,"*.csv").Length<2)
                    throw new IOException("Validação do conjunto SHP falhou.");
                if(Directory.Exists(folder))
                { backup=folder+".previous_"+Guid.NewGuid().ToString("N"); Directory.Move(folder,backup); }
                try {Directory.Move(temp,folder);} catch {if(backup!=null && !Directory.Exists(folder))Directory.Move(backup,folder);throw;}
                if(backup!=null)Directory.Delete(backup,true);
                return folder;
            }
            finally {if(Directory.Exists(temp))Directory.Delete(temp,true);}
        }
        private static void WriteCsv(string dir,string name,string[] headers,IEnumerable<string[]> rows)
        {
            using(var writer=new StreamWriter(Path.Combine(dir,name+".csv"),false,Utf8))
            {
                writer.WriteLine(string.Join(",",headers.Select(Csv)));
                foreach(var row in rows)writer.WriteLine(string.Join(",",row.Select(Csv)));
            }
        }
        private static string Csv(string s)=>"\""+(s??"").Replace("\"","\"\"")+"\"";
        private static string N(double d)=>d.ToString("R",CultureInfo.InvariantCulture);
        private static void WriteLayer(string dir,string name,int type,string[] fieldNames,List<Feature> features,string wkt)
        {
            string basePath=Path.Combine(dir,name);
            var parsed=features.Select(f=>Decode(f.Gpb,type)).ToList();
            var all=parsed.SelectMany(p=>p).ToList();
            double minX=all.Min(x=>x[0]),maxX=all.Max(x=>x[0]);
            double minY=all.Min(x=>x[1]),maxY=all.Max(x=>x[1]);
            double minZ=all.Min(x=>x[2]),maxZ=all.Max(x=>x[2]);
            var records=parsed.Select(p=>Record(type,p)).ToList();
            int shpWords=50+records.Sum(x=>4+x.Length/2),shxWords=50+records.Count*4;
            using(var shp=new BinaryWriter(File.Create(basePath+".shp")))
            using(var shx=new BinaryWriter(File.Create(basePath+".shx")))
            {
                Header(shp,shpWords,type,minX,minY,maxX,maxY,minZ,maxZ);
                Header(shx,shxWords,type,minX,minY,maxX,maxY,minZ,maxZ);
                int offset=50;
                for(int i=0;i<records.Count;i++)
                {
                    var data=records[i]; Big(shp,i+1);Big(shp,data.Length/2);shp.Write(data);
                    Big(shx,offset);Big(shx,data.Length/2);offset+=4+data.Length/2;
                }
            }
            WriteDbf(basePath+".dbf",fieldNames,features.Select(f=>f.Fields).ToList());
            File.WriteAllText(basePath+".prj",wkt,Utf8);
            File.WriteAllText(basePath+".cpg","UTF-8",Utf8);
        }
        private static List<double[]> Decode(byte[] gpb,int type)
        {
            if(gpb==null || gpb.Length<21 || gpb[0]!='G' || gpb[1]!='P')throw new ArgumentException("Geometria GeoPackage inválida.");
            using(var reader=new BinaryReader(new MemoryStream(gpb)))
            {
                reader.BaseStream.Position=8;
                if(reader.ReadByte()!=1 || reader.ReadUInt32()!=(uint)(type==13?1002:1003))throw new ArgumentException("Tipo geométrico incompatível com SHP.");
                if(type==15 && reader.ReadUInt32()!=1)throw new ArgumentException("Polygon com furos/múltiplos anéis não suportado no SHP de compatibilidade.");
                uint count=reader.ReadUInt32();if(count<2 || count>10000000)throw new ArgumentException("Número de vértices inválido.");
                var points=new List<double[]>((int)count);
                for(int i=0;i<count;i++)points.Add(new[]{reader.ReadDouble(),reader.ReadDouble(),reader.ReadDouble()});
                return points;
            }
        }
        private static byte[] Record(int type,List<double[]> p)
        {
            if(type==15)
            {
                if(p.Count<4 || !p[0].SequenceEqual(p[p.Count-1]))throw new ArgumentException("Anel poligonal aberto.");
                double twiceArea=0;
                for(int i=0;i<p.Count-1;i++)twiceArea+=p[i][0]*p[i+1][1]-p[i+1][0]*p[i][1];
                if(Math.Abs(twiceArea)<1e-12)throw new ArgumentException("Polígono de área zero.");
                if(twiceArea>0)p.Reverse(); // ESRI outer ring: clockwise.
            }
            using(var ms=new MemoryStream())using(var w=new BinaryWriter(ms))
            {
                w.Write(type);w.Write(p.Min(x=>x[0]));w.Write(p.Min(x=>x[1]));w.Write(p.Max(x=>x[0]));w.Write(p.Max(x=>x[1]));
                w.Write(1);w.Write(p.Count);w.Write(0);
                foreach(var point in p){w.Write(point[0]);w.Write(point[1]);}
                w.Write(p.Min(x=>x[2]));w.Write(p.Max(x=>x[2]));foreach(var point in p)w.Write(point[2]);
                w.Write(-1.0e39);w.Write(-1.0e39);foreach(var point in p)w.Write(-1.0e39);
                return ms.ToArray();
            }
        }
        private static void Header(BinaryWriter w,int words,int type,double minX,double minY,double maxX,double maxY,double minZ,double maxZ)
        {
            Big(w,9994);for(int i=0;i<5;i++)Big(w,0);Big(w,words);
            w.Write(1000);w.Write(type);w.Write(minX);w.Write(minY);w.Write(maxX);w.Write(maxY);
            w.Write(minZ);w.Write(maxZ);w.Write(-1.0e39);w.Write(-1.0e39);
        }
        private static void Big(BinaryWriter w,int value)
        {var b=BitConverter.GetBytes(value);Array.Reverse(b);w.Write(b);}
        private static void WriteDbf(string path,string[] names,List<string[]> rows)
        {
            const int width=120;
            using(var w=new BinaryWriter(File.Create(path)))
            {
                var today=DateTime.UtcNow;w.Write((byte)3);w.Write((byte)(today.Year-1900));w.Write((byte)today.Month);w.Write((byte)today.Day);
                w.Write(rows.Count);w.Write((short)(32+names.Length*32+1));w.Write((short)(1+names.Length*width));w.Write(new byte[20]);
                foreach(var name in names)
                {
                    var field=new byte[32];var bytes=Encoding.ASCII.GetBytes(name);Array.Copy(bytes,field,Math.Min(bytes.Length,10));field[11]=(byte)'C';field[16]=width;w.Write(field);
                }
                w.Write((byte)13);
                foreach(var row in rows)
                {
                    w.Write((byte)32);
                    for(int i=0;i<names.Length;i++)
                    {
                        byte[] value=Utf8.GetBytes(row[i]??"");
                        if(value.Length>width)throw new ArgumentException("Atributo excede 120 bytes no DBF; use GeoPackage para preservar o valor: "+names[i]);
                        w.Write(value);for(int k=value.Length;k<width;k++)w.Write((byte)32);
                    }
                }
                w.Write((byte)26);
            }
        }
    }
}
