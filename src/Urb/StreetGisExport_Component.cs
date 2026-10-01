using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;

namespace Buraqueira_Urb
{
    public sealed class StreetGisExport_Component : GH_Component
    {
        private bool wasExport;
        public StreetGisExport_Component() : base("Export Streets to GIS", "StreetGIS",
            "Exporta vias perfiladas, seções ajustadas e superfícies semânticas para GeoPackage com identidade e CRS explícitos.",
            "Glaux Urb", "00 | GIS & Dados Urbanos") { }
        public override Guid ComponentGuid => new Guid("b72287cb-0de3-4c16-8ec8-96d6739b305a");
        protected override System.Drawing.Bitmap Icon => GlauxUrbIcons.StreetGisExport;
        protected override void RegisterInputParams(GH_InputParamManager p)
        {
            p.AddGenericParameter("Profiled Streets", "Streets", "Saída Profiled de Street Profile Assignment: eixos, atributos e perfis nominais.", GH_ParamAccess.list);
            p.AddGenericParameter("Profiled Sections", "Sections", "Opcional: saída Sections de Road Transversals, com identidade da via.", GH_ParamAccess.tree); p[1].Optional=true;
            p.AddGenericParameter("Fitted Street Profiles", "Fitted", "Opcional: saída Fitted de Street Profile Fitting, no mesmo caminho das Sections.", GH_ParamAccess.tree); p[2].Optional=true;
            p.AddBrepParameter("Adaptive Road Elements", "AdaptSrf", "Opcional: saída AdaptSrf de Road Cross Section em {rua;run;elemento}.", GH_ParamAccess.tree); p[3].Optional=true;
            p.AddTextParameter("Adaptive Element Labels", "AdaptLabels", "Obrigatório para exportar AdaptSrf: saída AdaptLabels do mesmo Road Cross Section, com ElementID.", GH_ParamAccess.tree); p[4].Optional=true;
            p.AddTextParameter("File Path", "Path", "Arquivo GeoPackage .gpkg de destino.", GH_ParamAccess.item);
            p.AddIntegerParameter("EPSG", "EPSG", "Código EPSG comprovado do CRS das coordenadas atuais; nenhuma reprojeção é feita.", GH_ParamAccess.item);
            p.AddTextParameter("CRS Definition", "WKT", "WKT completo do CRS atual ou caminho para arquivo .prj correspondente.", GH_ParamAccess.item);
            p.AddTextParameter("Width Unit", "Unit", "Unidade real de coordenadas e larguras, por exemplo m; deve corresponder ao documento Rhino e GIS.", GH_ParamAccess.item);
            p.AddTextParameter("Format", "Format", "GPKG (padrão) ou SHP (conjunto de camadas e CSV relacionais).", GH_ParamAccess.item,"GPKG"); p[9].Optional=true;
            p.AddBooleanParameter("Overwrite", "Overwrite", "Autoriza substituir o arquivo existente.", GH_ParamAccess.item,false); p[10].Optional=true;
            p.AddBooleanParameter("Export", "Export", "Grava somente na transição False → True. Retorne a False antes de uma nova exportação.", GH_ParamAccess.item,false); p[11].Optional=true;
        }
        protected override void RegisterOutputParams(GH_OutputParamManager p)
        {
            p.AddBooleanParameter("Success", "Success", "True quando o GeoPackage foi escrito e validado nesta ativação.", GH_ParamAccess.item);
            p.AddTextParameter("File", "File", "Caminho do GeoPackage exportado.", GH_ParamAccess.item);
            p.AddTextParameter("Report", "Report", "Contagens, CRS, avisos e conflitos da exportação.", GH_ParamAccess.item);
        }
        protected override void SolveInstance(IGH_DataAccess da)
        {
            bool export=false,overwrite=false; da.GetData(11,ref export); da.GetData(10,ref overwrite);
            if(!export) {wasExport=false; da.SetData(0,false); da.SetData(2,"Aguardando Export=True."); Message="Pronto";return;}
            if(wasExport) {da.SetData(0,false);da.SetData(2,"Export já executado; altere Export para False e depois True.");Message="Aguardando";return;}
            wasExport=true;
            string path=null,wkt=null,unit=null,format="GPKG"; int epsg=0;
            da.GetData(5,ref path);da.GetData(6,ref epsg);da.GetData(7,ref wkt);da.GetData(8,ref unit);
            da.GetData(9,ref format);format=(format??"").Trim().ToUpperInvariant();
            try
            {
                if(!string.IsNullOrWhiteSpace(wkt) && wkt.EndsWith(".prj",StringComparison.OrdinalIgnoreCase))wkt=File.ReadAllText(wkt);
                if(string.IsNullOrWhiteSpace(unit))throw new ArgumentException("Informe Width Unit conforme o GIS e o documento Rhino.");
                var package=new StreetGisPackage{Epsg=epsg,CrsDefinition=wkt,WidthUnit=unit};
                var raw=new List<object>();da.GetDataList(0,raw);
                var input=raw.Select(Unwrap).OfType<ProfiledStreet>().ToList();
                if(input.Count==0)throw new ArgumentException("Conecte Profiled Streets.");
                var warnings=new List<string>();
                var byFeature=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
                foreach(var s in input)
                {
                    if(s.StreetProfile==null || s.SourceGeometry==null || !s.SourceGeometry.IsValid)
                    {warnings.Add("Feição sem perfil/eixo válido: "+s.StreetName);continue;}
                    string id=SemanticID(s);
                    var record=new StreetGisRecord{
                        StreetID=id,StreetName=s.StreetName,StreetType=s.StreetProfile.StreetType,
                        FeatureID=s.StreetID,SourceID=s.SourceId,RunID=null,
                        Scenario="EXISTING",Status=string.IsNullOrEmpty(s.MatchInfo)?"MATCHED":s.MatchInfo,
                        Centerline=StreetGisWriter.Geometry(Points(s.SourceGeometry).Select(Coordinates),epsg,false)};
                    for(int i=0;i<s.StreetProfile.Elements.Count;i++)
                    {
                        var e=s.StreetProfile.Elements[i];
                        record.Elements.Add(new StreetGisElementRecord{ElementOrder=i,ElementID=e.Id==Guid.Empty?null:e.Id.ToString("D"),
                            ElementType=e.Type,MinWidth=e.MinimumWidth,MaxWidth=e.MaximumWidth,IsFixed=e.IsFixed,
                            Direction=e.Direction,Required=e.Required});
                    }
                    package.Streets.Add(record);
                    if(!string.IsNullOrWhiteSpace(s.StreetID))byFeature[s.StreetID]=id;
                }
                if(package.Streets.Count==0)throw new ArgumentException("Nenhuma via perfilada válida.");
                var sectionByPath=new Dictionary<string,ProfiledSection>();
                var streetByPathIndex=new Dictionary<int,string>();
                var sections=Params.Input[1].VolatileData;
                for(int b=0;b<sections.PathCount;b++)
                {
                    var typed=sections.get_Branch(b).Cast<object>().Select(Unwrap).OfType<ProfiledSection>().FirstOrDefault();
                    if(typed==null)continue;
                    string id;
                    if(!byFeature.TryGetValue(typed.StreetID??"",out id))
                    {warnings.Add("Section sem identidade exportável: "+sections.Paths[b]);continue;}
                    sectionByPath[sections.Paths[b].ToString()]=typed;
                    int index=sections.Paths[b].Indices[0];
                    if(streetByPathIndex.TryGetValue(index,out var previous) && previous!=id)
                        throw new ArgumentException("Street Path Index ambíguo nas Sections.");
                    streetByPathIndex[index]=id;
                }
                var fitted=Params.Input[2].VolatileData;
                for(int b=0;b<fitted.PathCount;b++)
                {
                    var key=fitted.Paths[b].ToString();
                    if(!sectionByPath.TryGetValue(key,out var typed))continue;
                    var profile=fitted.get_Branch(b).Cast<object>().Select(Unwrap).OfType<StreetProfile>().FirstOrDefault();
                    if(profile==null)continue;
                    var id=byFeature[typed.StreetID];
                    var item=new StreetGisSectionRecord{StreetID=id,RunID=null,SectionID="T"+typed.StreetPathIndex+"_"+typed.StationIndex,
                        StationIndex=typed.StationIndex,AvailableWidth=profile.Elements.Sum(e=>e.FittedWidth),Status="FITTED",Reason=typed.Reason};
                    for(int i=0;i<profile.Elements.Count;i++)
                    {
                        var e=profile.Elements[i];
                        item.Elements.Add(new StreetGisElementRecord{ElementOrder=i,ElementID=e.Id==Guid.Empty?null:e.Id.ToString("D"),
                            ElementType=e.Type,MaxWidth=e.FittedWidth,Status=e.Status});
                    }
                    package.Sections.Add(item);
                }
                var surfaces=Params.Input[3].VolatileData;
                var labels=Params.Input[4].VolatileData;
                for(int b=0;b<surfaces.PathCount;b++)
                {
                    var indices=surfaces.Paths[b].Indices;
                    if(indices.Length<3 || !streetByPathIndex.TryGetValue(indices[0],out var id))
                    {warnings.Add("Superfície sem identidade de Sections: "+surfaces.Paths[b]);continue;}
                    var nominal=package.Streets.First(x=>x.StreetID==id).Elements;
                    int labelBranch=-1;
                    for(int k=0;k<labels.PathCount;k++)if(labels.Paths[k].Equals(surfaces.Paths[b])){labelBranch=k;break;}
                    string label=labelBranch>=0?labels.get_Branch(labelBranch).Cast<object>().Select(x=>x as GH_String).Where(x=>x!=null).Select(x=>x.Value).FirstOrDefault():null;
                    string elementId=label?.Split(':').LastOrDefault()?.Trim();
                    int order=nominal.FindIndex(x=>string.Equals(x.ElementID,elementId,StringComparison.OrdinalIgnoreCase));
                    if(order<0){warnings.Add("AdaptLabels ausente ou ElementID incompatível: "+surfaces.Paths[b]);continue;}
                    foreach(var rawSurface in surfaces.get_Branch(b))
                    {
                        var brep=Unwrap(rawSurface) as Brep;
                        if(brep==null || !brep.IsValid || brep.Loops.Count!=1)
                        {warnings.Add("Superfície não exportada (inválida ou com furos): "+surfaces.Paths[b]);continue;}
                        var boundary=brep.Loops[0].To3dCurve();
                        try
                        {
                            var geometry=StreetGisWriter.Geometry(Points(boundary).Select(Coordinates),epsg,true);
                            package.Surfaces.Add(new StreetGisSurfaceRecord{StreetID=id,RunID=indices[1].ToString(CultureInfo.InvariantCulture),
                                ElementOrder=order,ElementID=nominal[order].ElementID,ElementType=nominal[order].ElementType,
                                Status="GENERATED",Scenario="PLANNING",Polygon=geometry});
                        }
                        catch(ArgumentException){warnings.Add("Geometria inválida: "+surfaces.Paths[b]);}
                    }
                }
                if(format!="GPKG" && format!="SHP")throw new ArgumentException("Format deve ser GPKG ou SHP.");
                string output=format=="GPKG"?StreetGisWriter.Write(package,path,overwrite):StreetShpWriter.Write(package,path,overwrite);
                string report="GLAUX URB GIS EXPORT\nFormat: "+format+"\nFile: "+output+
                    "\nStreets: "+package.Streets.Select(x=>x.StreetID).Distinct().Count()+
                    "\nStreet Features: "+package.Streets.Count+
                    "\nProfile Elements: "+package.Streets.GroupBy(x=>x.StreetID).Sum(g=>g.First().Elements.Count)+
                    "\nSections: "+package.Sections.Count+"\nStreet Element Geometries: "+package.Surfaces.Count+
                    "\nCRS: EPSG:"+epsg+"\nWidth Unit: "+unit+"\nWarnings: "+warnings.Count+
                    (warnings.Count>0?"\n"+string.Join("\n",warnings):"")+"\nStatus: SUCCESS";
                da.SetData(0,true);da.SetData(1,output);da.SetData(2,report);Message="Exportado";
            }
            catch(Exception ex)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error,ex.Message);
                da.SetData(0,false);da.SetData(2,"GIS EXPORT FAILED: "+ex.Message);Message="Erro";
            }
        }
        private static object Unwrap(object o)
        { while(o is GH_ObjectWrapper wrapper)o=wrapper.Value; if(o is GH_Brep b)return b.Value; return o; }
        private static string SemanticID(ProfiledStreet s)
        {
            string profile=string.Join("|",s.StreetProfile.Elements.Select(e=>e.Id+":"+e.Type+":"+
                e.MinimumWidth.ToString("R",CultureInfo.InvariantCulture)+":"+e.MaximumWidth.ToString("R",CultureInfo.InvariantCulture)+":"+e.Direction));
            string name=StreetNameNormalizer.Normalize(s.StreetName??s.StreetProfile.StreetName??"");
            using(var sha=SHA256.Create())return "S"+BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(name+"|"+s.StreetProfile.StreetType+"|"+profile))).Replace("-","").Substring(0,24);
        }
        private static IEnumerable<Point3d> Points(Curve curve)
        {
            if(curve==null || !curve.IsValid)throw new ArgumentException("Curva GIS inválida.");
            if(curve.TryGetPolyline(out Polyline poly))return poly;
            // WKB has straight segments; record any NURBS as a sampled approximation.
            int count=Math.Max(2,Math.Min(4096,(int)Math.Ceiling(curve.GetLength())));
            var parameters=curve.DivideByCount(count,true);
            return parameters.Select(curve.PointAt);
        }
        private static double[] Coordinates(Point3d p)=>new[]{p.X,p.Y,p.Z};
    }
}
