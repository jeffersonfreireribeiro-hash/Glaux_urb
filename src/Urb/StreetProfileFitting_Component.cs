using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;

namespace Buraqueira_Urb
{
    public sealed class StreetProfileFitting_Component : GH_Component
    {
        public StreetProfileFitting_Component() : base(
            "Street Profile Fitting", "ProfileFit",
            "Ligue Pts e Sections de Road Transversals (Axis vindo de ProfileAssign.Profiled). Alternativa: Profile + Pts de uma via; para várias vias use SectionMeta. Ajusta larguras sem mover lotes.",
            "Glaux Urb", "01 | Infraestrutura Viária") { }
        public override Guid ComponentGuid => new Guid("a02b5df9-c46e-4842-9be6-d63c12861642");
        protected override System.Drawing.Bitmap Icon => GlauxUrbIcons.StreetProfileFitting;
        protected override void RegisterInputParams(GH_InputParamManager p)
        {
            p.AddGenericParameter("Street Profiles", "Profile", "Opcional com Sections tipadas. Alternativa: saída Profile de Street Profile Definition ou lista Profiled de Assignment. Para várias vias ligue SectionMeta; para uma, Pts basta.", GH_ParamAccess.list);
            p[0].Optional = true;
            p.AddPointParameter("Section Points", "Pts", "Obrigatório: saída Pts de Road Transversals, árvore {rua;estaca} com exatamente [limite lote E, meio-fio E, centro, meio-fio D, limite lote D].", GH_ParamAccess.tree);
            p.AddPointParameter("Planned Section Points", "PlanPts", "Opcional: saída PlanPts de Road Transversals; cada ramo deve manter os limites de lote de Pts.", GH_ParamAccess.tree);
            p[2].Optional = true;
            p.AddIntegerParameter("Street Path Index", "PathIdx", "Seleção manual da posição {rua;estaca} em Pts quando não há SectionMeta; não é identidade da via. -1 usa Street/SourceStreetID ou a única rua.", GH_ParamAccess.item, -1);
            p.AddBooleanParameter("Run", "Run", "Executa o ajuste.", GH_ParamAccess.item, true);
            p.AddTextParameter("Section Metadata", "SectionMeta", "Ponte legada: associa via/perfil pelo SourceStreetID ou nome; prefira Sections tipadas. ID de arquivo bruto pode ser posicional.", GH_ParamAccess.tree);
            p[5].Optional = true;
            p.AddGenericParameter("Profiled Sections", "Sections", "Preferido: saída Sections de Road Transversals após ligar ProfileAssign.Profiled em Axis. Mesmo caminho {rua;estaca} de Pts, com StreetProfile embutido.", GH_ParamAccess.tree);
            p[6].Optional = true;
        }
        protected override void RegisterOutputParams(GH_OutputParamManager p)
        {
            p.AddTextParameter("Adapted Profiles", "Adapted", "Faixas ajustadas por {rua;estaca}, compatíveis com Road Cross Section.", GH_ParamAccess.tree);
            p.AddPointParameter("Fitted Section Points", "FitPts", "QL, meio-fio esquerdo, centro, meio-fio direito e QR por seção.", GH_ParamAccess.tree);
            p.AddTextParameter("Fitting Conflicts", "Conflicts", "Largura insuficiente, limites inválidos ou mínimas entre seções.", GH_ParamAccess.tree);
            p.AddGenericParameter("Fitted Street Profiles", "Fitted", "StreetProfile tipado por seção, com IDs, ordem, largura e status de cada faixa.", GH_ParamAccess.tree);
            p.AddTextParameter("Fitting Report", "Report", "Quantidade de seções ajustadas, conflitos e supressões.", GH_ParamAccess.item);
        }
        protected override void SolveInstance(IGH_DataAccess da)
        {
            bool run = true; da.GetData(4, ref run); if (!run) { Message = "Pausado"; return; }
            var values=Params.Input[0].VolatileData.AllData(true).Select(Unwrap).ToList();
            var profiledStreets=values.OfType<ProfiledStreet>().Where(x=>x.StreetProfile!=null).ToList();
            var profiles=values.OfType<StreetProfile>()
                .Concat(profiledStreets.Select(x=>x.StreetProfile)).Distinct().ToList();
            GH_Structure<GH_Point> sections, planned;
            da.GetDataTree(1, out sections); da.GetDataTree(2, out planned);
            if (sections == null || sections.DataCount == 0)
            { AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Street Profile Fitting: ligue Road Transversals.Pts em Pts; esperado ramo {rua;estaca} com cinco pontos."); return; }
            // Generic GH parameters expose IGH_Goo branches. GetDataTree<T> only
            // accepts an exact T and breaks when GH_ObjectWrapper is requested here.
            var typedSections=Params.Input[6].VolatileData;
            var typedByPath=new Dictionary<string,ProfiledSection>();
            if(typedSections!=null)
                for(int i=0;i<typedSections.PathCount;i++)
                {
                    var section=typedSections.get_Branch(i).Cast<IGH_Goo>()
                        .Select(Unwrap).OfType<ProfiledSection>().FirstOrDefault();
                    if(section!=null) typedByPath[typedSections.Paths[i].ToString()]=section;
                }
            bool hasTyped=typedByPath.Count>0;
            if (Params.Input[6].SourceCount > 0 && !hasTyped)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                    "Street Profile Fitting: Sections está conectado, mas não contém ProfiledSection. Ligue Road Transversals.Sections e alimente Axis com ProfileAssign.Profiled.");
                return;
            }
            profiles.AddRange(typedByPath.Values.Where(x=>x.StreetProfile!=null)
                .Select(x=>x.StreetProfile).Where(x=>!profiles.Contains(x)));
            if (profiles.Count == 0)
            {
                string received = values.Count == 0 ? "nada" : string.Join(", ", values.Where(x => x != null)
                    .Select(x => x.GetType().Name).Distinct().Take(3));
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                    $"Street Profile Fitting: esperado StreetProfile em Profile ou ProfiledSection em Sections; recebido {received}. Ligue Definition.Profile ou Road Transversals.Sections.");
                return;
            }
            int streetIndex = -1; da.GetData(3, ref streetIndex);
            var ids = sections.Paths.Where(x => x.Indices.Length >= 2).Select(x => x.Indices[0]).Distinct().ToList();
            GH_Structure<GH_String> metadata; da.GetDataTree(5, out metadata);
            bool hasMetadata = metadata != null && metadata.DataCount > 0;
            var profilesByPath = new Dictionary<int,StreetProfile>();
            var ambiguousPaths = new HashSet<int>();
            if (hasMetadata && !hasTyped)
            {
                for (int i = 0; i < metadata.PathCount; i++)
                {
                    var path = metadata.Paths[i];
                    if (path.Indices.Length < 2 || !ids.Contains(path.Indices[0])) continue;
                    foreach (var metaItem in metadata.Branches[i])
                    {
                        string text = metaItem?.Value;
                        if (string.IsNullOrEmpty(text)) continue;
                        string sName = Field(text, "StreetName");
                        string pName = Field(text, "Profile");
                        string srcId = Field(text, "SourceStreetID");
                        var candidates=new HashSet<StreetProfile>();
                        if(!string.IsNullOrEmpty(srcId))
                            foreach(var matched in profiledStreets.Where(x=>
                                string.Equals(x.StreetID,srcId,StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(x.SourceId,srcId,StringComparison.OrdinalIgnoreCase)))
                                candidates.Add(matched.StreetProfile);
                        if(candidates.Count==0)
                        {
                            foreach(var candidate in profiles.Where(x=>
                                (!string.IsNullOrEmpty(sName) && StreetNameNormalizer.Matches(sName,x.StreetName,true)) ||
                                (!string.IsNullOrEmpty(pName) && StreetNameNormalizer.Matches(pName,x.StreetName,true)) ||
                                (!string.IsNullOrEmpty(srcId) && string.Equals(srcId,x.Street,StringComparison.OrdinalIgnoreCase))))
                                candidates.Add(candidate);
                        }
                        int pathIndex=path.Indices[0];
                        if(candidates.Count>1 || (candidates.Count==1 && profilesByPath.TryGetValue(pathIndex,out var existing) &&
                            !ReferenceEquals(existing,candidates.First()))) ambiguousPaths.Add(pathIndex);
                        else if(candidates.Count==1) profilesByPath[pathIndex]=candidates.First();
                    }
                }
            }
            if (streetIndex >= 0 && !hasMetadata && !hasTyped && profiles.Count==1 && ids.Contains(streetIndex))
            {
                profilesByPath[streetIndex]=profiles[0];
            }
            else if (!hasMetadata && !hasTyped && streetIndex < 0 && ids.Count==1 && profiles.Count==1)
            {
                profilesByPath[ids[0]]=profiles[0];
            }
            if (streetIndex>=0 && hasMetadata && !hasTyped &&
                (!profilesByPath.ContainsKey(streetIndex) || ambiguousPaths.Contains(streetIndex)))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                    "PathIdx não pode substituir a identidade de SectionMeta; verifique os perfis e SourceStreetID."); return;
            }
            if(!hasTyped && profilesByPath.Count==0 && ambiguousPaths.Count==0)
            {AddRuntimeMessage(GH_RuntimeMessageLevel.Error,"Street Profile Fitting: nenhum perfil corresponde aos caminhos de Pts. Para uma via, conecte Definition.Profile; para várias, conecte Road Transversals.Sections ou SectionMeta com SourceStreetID. PathIdx seleciona posição, não identidade.");return;}
            bool usePlanned = Params.Input[2].SourceCount > 0;
            var adapted = new GH_Structure<GH_String>();
            var fittedPoints = new GH_Structure<GH_Point>();
            var conflicts = new GH_Structure<GH_String>();
            var fittedProfiles = new GH_Structure<GH_ObjectWrapper>();
            var good = new List<Section>();
            int rejected = 0, suppressed = 0;
            for (int i = 0; i < sections.PathCount; i++)
            {
                var path = sections.Paths[i];
                if (path.Indices.Length < 2) continue;
                if (streetIndex>=0 && path.Indices[0]!=streetIndex) continue;
                StreetProfile profile;
                ProfiledSection typedSection=null;
                if(hasTyped)
                {
                    if(!typedByPath.TryGetValue(path.ToString(),out typedSection) || typedSection.StreetProfile==null)
                    {Conflict("UNMATCHED_SECTION_PROFILE");continue;}
                    profile=typedSection.StreetProfile;
                }
                else
                {
                    if (ambiguousPaths.Contains(path.Indices[0])) { Conflict("AMBIGUOUS_PROFILE_MATCH"); continue; }
                    if (!profilesByPath.TryGetValue(path.Indices[0],out profile))
                    { Conflict("UNMATCHED_STREET"); continue; }
                }
                var pts = sections.Branches[i];
                if (!ValidFive(pts)) { Conflict("INVALID_SECTION_POINTS: Pts deve conter cinco pontos válidos [lote E, meio-fio E, centro, meio-fio D, lote D]"); continue; }
                var ql = pts[0].Value; var qr = pts[4].Value;
                if(typedSection!=null && (typedSection.Points==null || typedSection.Points.Length!=5 ||
                    typedSection.Points[0].DistanceTo(ql)>.01 || typedSection.Points[4].DistanceTo(qr)>.01))
                {Conflict("SECTION_LOT_BOUNDARY_MISMATCH");continue;}
                if (usePlanned)
                {
                    int pi = planned == null ? -1 : planned.Paths.ToList().FindIndex(x => x.Equals(path));
                    if (pi < 0 || !ValidFive(planned.Branches[pi]) ||
                        planned.Branches[pi][0].Value.DistanceTo(ql) > .01 ||
                        planned.Branches[pi][4].Value.DistanceTo(qr) > .01)
                    { Conflict("MISSING_OR_CHANGED_LOT_BOUNDARY"); continue; }
                }
                double width = ql.DistanceTo(qr);
                var fit = StreetProfileElementFitter.Fit(profile, width);
                if (!fit.Success)
                { conflicts.Append(new GH_String(fit.Diagnostic), path); rejected++; continue; }
                var across = qr - ql;
                var cl = ql + across * (fit.LeftWidth / width);
                var cr = qr - across * (fit.RightWidth / width);
                if (fit.RoadWidth <= 1e-8 || cl.DistanceTo(cr) <= 1e-8)
                { Conflict("ROAD_DOMAIN_COLLAPSED"); continue; }
                good.Add(new Section { Path = path, QL = ql, CL = cl, C = pts[2].Value,
                    CR = cr, QR = qr, Fit = fit });
                void Conflict(string reason)
                { conflicts.Append(new GH_String("PROFILE_CONFLICT | Reason=" + reason), path); rejected++; }
            }
            good.Sort((a,b) => a.Path.Indices[0] != b.Path.Indices[0]
                ? a.Path.Indices[0].CompareTo(b.Path.Indices[0])
                : a.Path.Indices[1].CompareTo(b.Path.Indices[1]));
            var invalid = new HashSet<string>();
            for (int i = 1; i < good.Count; i++)
            {
                var a = good[i-1]; var b = good[i];
                if (a.Path.Indices[0] != b.Path.Indices[0] ||
                    b.Path.Indices[1] != a.Path.Indices[1]+1) continue;
                double l = MinimumDistance(a.QL,b.QL,a.CL,b.CL);
                double r = MinimumDistance(a.CR,b.CR,a.QR,b.QR);
                double minL = ExteriorMinimum(a.Fit.Profile.Elements, true);
                double minR = ExteriorMinimum(a.Fit.Profile.Elements, false);
                if (l + 1e-6 < minL || r + 1e-6 < minR || !OrderedMid(a,b))
                {
                    invalid.Add(b.Path.ToString()); rejected++;
                    conflicts.Append(new GH_String(string.Format(CultureInfo.InvariantCulture,
                        "PROFILE_CONFLICT | Reason=INTERPOLATED_MINIMUM_OR_LOT_ENVELOPE | Left={0:F3} | Right={1:F3}",l,r)),b.Path);
                }
            }
            foreach (var section in good)
            {
                if (invalid.Contains(section.Path.ToString())) continue;
                var path = section.Path;
                int firstRoad = section.Fit.Profile.Elements.FindIndex(x => StreetProfileElementFitter.IsRoadDomain(x.Type));
                int lastRoad = section.Fit.Profile.Elements.FindLastIndex(x => StreetProfileElementFitter.IsRoadDomain(x.Type));
                for (int elementIndex = 0; elementIndex < section.Fit.Profile.Elements.Count; elementIndex++)
                {
                    var e = section.Fit.Profile.Elements[elementIndex];
                    if (e.Status == "SUPPRESSED") suppressed++;
                    if (elementIndex < firstRoad || elementIndex > lastRoad) continue;
                    adapted.Append(new GH_String(string.Format(CultureInfo.InvariantCulture,
                        "{0} | {1} | ElementID={2} | Direction={3} | Width={4:F4} | Minimum={5:F4} | Maximum={6:F4} | Status={7} | Reason={8}",
                        e.Type,e.Id,e.Id,e.Direction,e.FittedWidth,e.MinimumWidth,e.MaximumWidth,e.Status,e.Reason ?? "NONE")),path);
                }
                fittedPoints.Append(new GH_Point(section.QL),path);
                fittedPoints.Append(new GH_Point(section.CL),path);
                fittedPoints.Append(new GH_Point(section.C),path);
                fittedPoints.Append(new GH_Point(section.CR),path);
                fittedPoints.Append(new GH_Point(section.QR),path);
                fittedProfiles.Append(new GH_ObjectWrapper(section.Fit.Profile),path);
            }
            da.SetDataTree(0,adapted); da.SetDataTree(1,fittedPoints);
            da.SetDataTree(2,conflicts); da.SetDataTree(3,fittedProfiles);
            da.SetData(4,$"Profiles={profiles.Count} | StreetPaths={(hasTyped?typedByPath.Values.Select(x=>x.StreetPathIndex).Distinct().Count():profilesByPath.Count)} | Fitted={good.Count-invalid.Count} | Conflicts={rejected} | Suppressed={suppressed}. Order and ElementIDs preserved; lot boundaries fixed.");
            if (rejected > 0)
            {
                string examples = string.Join("; ", conflicts.AllData(true).Take(3).Select(x => x.ToString()));
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                    $"Street Profile Fitting: {rejected} seção(ões) rejeitada(s). Consulte Conflicts por caminho {{rua;estaca}}. {examples}");
            }
            Message = $"{good.Count-invalid.Count} fit";
        }

        private sealed class Section
        { public GH_Path Path; public Point3d QL,CL,C,CR,QR; public StreetProfileFitResult Fit; }
        private static object Unwrap(IGH_Goo goo)
        {object value=goo;while(value is GH_ObjectWrapper wrapper)value=wrapper.Value;return value;}
        private static string Field(string text, string key)
        {
            if (string.IsNullOrEmpty(text)) return null;
            foreach (string token in text.Split('|'))
            {
                string part=token.Trim();
                if (part.StartsWith(key+"=",StringComparison.OrdinalIgnoreCase))
                    return part.Substring(key.Length+1).Trim();
            }
            return null;
        }
        private static bool ValidFive(IList<GH_Point> p) => p != null && p.Count == 5 &&
            p.All(x => x != null && x.Value.IsValid);
        private static double ExteriorMinimum(IList<StreetProfileElement> e, bool left)
        {
            int first=e.ToList().FindIndex(x=>StreetProfileElementFitter.IsRoadDomain(x.Type));
            int last=e.ToList().FindLastIndex(x=>StreetProfileElementFitter.IsRoadDomain(x.Type));
            return left ? e.Take(first).Where(x=>x.Required).Sum(x=>x.MinimumWidth)
                : e.Skip(last+1).Where(x=>x.Required).Sum(x=>x.MinimumWidth);
        }
        private static double MinimumDistance(Point3d a0,Point3d a1,Point3d b0,Point3d b1)
        {
            var v=b0-a0; var dv=(b1-a1)-v; double d2=dv.SquareLength;
            double t=d2>1e-12 ? Math.Max(0,Math.Min(1,-(v*dv)/d2)) : 0;
            return (v+dv*t).Length;
        }
        private static bool OrderedMid(Section a,Section b)
        {
            foreach(double t in new[]{.25,.5,.75})
            {
                var ql=a.QL+(b.QL-a.QL)*t;var qr=a.QR+(b.QR-a.QR)*t;
                var cl=a.CL+(b.CL-a.CL)*t;var cr=a.CR+(b.CR-a.CR)*t;
                var span=qr-ql;double w2=span.SquareLength;
                if(w2<=1e-8)return false;
                double l=(cl-ql)*span/w2,r=(cr-ql)*span/w2;
                if(l < -1e-6 || r > 1+1e-6 || r <= l+1e-6 ||
                   (ql+span*l).DistanceTo(cl)>.05 || (ql+span*r).DistanceTo(cr)>.05)return false;
            }
            return true;
        }
    }
}
