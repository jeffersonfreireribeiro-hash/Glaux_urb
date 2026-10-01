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
    public sealed class StreetProfileDefinition_Component : GH_Component
    {
        private sealed class Element
        {
            public string Kind, Name, Rule;
            public int Direction;
            public double Preferred, Minimum;
            public override string ToString() => string.Join(" | ", Kind, Name,
                "Direction=" + Direction,
                "Preferred=" + Preferred.ToString("F2", CultureInfo.InvariantCulture),
                "Minimum=" + Minimum.ToString("F2", CultureInfo.InvariantCulture),
                "Rule=" + Rule);
        }

        public StreetProfileDefinition_Component() : base(
            "Street Profile Definition (Legacy)", "StreetProfileOld",
            "Define elementos semânticos de um perfil viário nominal e fornece CSV compatível com Road Cross Section.",
            "Glaux Urb", "01 | Infraestrutura Viária") { }

        public override Guid ComponentGuid => new Guid("6146fcb8-8670-4f98-a882-5f26d9324b3d");
        public override GH_Exposure Exposure => GH_Exposure.hidden;
        protected override System.Drawing.Bitmap Icon => GlauxUrbIcons.StreetProfileDefinition;

        protected override void RegisterInputParams(GH_InputParamManager p)
        {
            p.AddTextParameter("Street", "Street", "Nome da via para associação ao perfil.", GH_ParamAccess.item, "Rua");
            p.AddIntegerParameter("Street Type", "Type", "0 Pedestrian; 1 One Way; 2 Two Way.", GH_ParamAccess.item, 2);
            p.AddIntegerParameter("Direction A Lanes", "LanesA", "Número de faixas no sentido A.", GH_ParamAccess.item, 1);
            p.AddIntegerParameter("Direction B Lanes", "LanesB", "Número de faixas no sentido B; não se aplica a vias pedonais ou de mão única.", GH_ParamAccess.item, 1);
            p.AddNumberParameter("Preferred Lane Width", "LanePref", "Largura nominal editável da faixa; não é valor normativo.", GH_ParamAccess.item, 3.0);
            p.AddNumberParameter("Minimum Lane Width", "LaneMin", "Mínimo editável para ajuste futuro; não é valor normativo.", GH_ParamAccess.item, 2.5);
            p.AddBooleanParameter("Has Median", "MedianOn", "Gera canteiro central apenas em via de mão dupla.", GH_ParamAccess.item, false);
            p.AddNumberParameter("Median Width", "Median", "Largura nominal do canteiro central.", GH_ParamAccess.item, 1.0);
            p.AddNumberParameter("Minimum Median Width", "MedianMin", "Mínimo editável do canteiro.", GH_ParamAccess.item, 0.6);
            p.AddBooleanParameter("Has Sidewalk Left", "SwLOn", "Ativa calçada esquerda.", GH_ParamAccess.item, true);
            p.AddNumberParameter("Minimum Sidewalk Left", "MinL", "Largura mínima editável da calçada esquerda.", GH_ParamAccess.item, 1.5);
            p.AddBooleanParameter("Has Sidewalk Right", "SwROn", "Ativa calçada direita.", GH_ParamAccess.item, true);
            p.AddNumberParameter("Minimum Sidewalk Right", "MinR", "Largura mínima editável da calçada direita.", GH_ParamAccess.item, 1.5);
            p.AddNumberParameter("Pedestrian Width", "PedW", "Largura total nominal da área pedonal; usada somente no tipo Pedestrian.", GH_ParamAccess.item, 6.0);
            p.AddIntegerParameter("Street Index", "StreetID", "Índice da rua no caminho {rua;estaca} do Road Transversals; -1 aceita somente uma rua presente.", GH_ParamAccess.item, -1);
            p.AddPointParameter("Section Points", "Pts", "Saída Pts do Road Transversals para ajuste do perfil a cada seção.", GH_ParamAccess.tree);
            p[15].Optional = true;
            p.AddCurveParameter("Planned Transversals", "Planned", "Entrada legada opcional; não altera limites de lote. Use PlanPts para o Planning.", GH_ParamAccess.tree);
            p[16].Optional = true;
            p.AddBooleanParameter("Use Planning", "Planning", "Se verdadeiro, usa PlanPts quando conectado; o fitter sempre respeita os limites de lote originais.", GH_ParamAccess.item, false);
            p.AddPointParameter("Planned Section Points", "PlanPts", "Cinco pontos propostos por {rua;estaca}; limites de lote devem coincidir com Pts existentes.", GH_ParamAccess.tree);
            p[18].Optional = true;
            p.AddBooleanParameter("Median Required", "MedReq", "Se False, o canteiro solicitado é opcional e sua remoção será registrada quando não couber.", GH_ParamAccess.item, true);
            p.AddBooleanParameter("Run", "Run", "Executa; última entrada.", GH_ParamAccess.item, true);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager p)
        {
            p.AddTextParameter("Profile Elements", "Elements", "Sequência semântica da esquerda para a direita: tipo, nome, direção e restrições dimensionais.", GH_ParamAccess.list);
            p.AddTextParameter("Road Section CSV", "CSV", "Formato de catálogo aceito pelo Road Cross Section (CSV). Faixas individuais são agregadas nesta ponte legada.", GH_ParamAccess.item);
            p.AddNumberParameter("Nominal Width", "NomW", "Soma das larguras nominais.", GH_ParamAccess.item);
            p.AddNumberParameter("Minimum Width", "MinW", "Soma dos mínimos por elemento.", GH_ParamAccess.item);
            p.AddTextParameter("Profile Report", "Report", "Tipologia, elementos e limites da ponte CSV.", GH_ParamAccess.item);
            p.AddTextParameter("Adapted Profiles", "Adapted", "Elementos adaptados por {rua;estaca}; faixas respeitam mínimos e excedente vira reserva viária.", GH_ParamAccess.tree);
            p.AddTextParameter("Fitting Conflicts", "Conflicts", "Seções em que o perfil não cabe sem violar mínimos, com largura, déficit e motivo.", GH_ParamAccess.tree);
            p.AddPointParameter("Fitted Section Points", "FitPts", "Cinco pontos por {rua;estaca}: limites de lote originais, meios-fios adaptados e centro.", GH_ParamAccess.tree);
            p.AddTextParameter("Profile Adjustments", "Changes", "Deslocamentos dos meios-fios e remoção explícita de canteiro opcional.", GH_ParamAccess.tree);
        }

        private static void EmitAdapted(List<Element> elements, LotBoundaryFitResult fit,
            GH_Structure<GH_String> output, GH_Path path)
        {
            if (elements.Count == 1 && elements[0].Kind == "Pedestrian")
            {
                output.Append(new GH_String($"Pedestrian | Pedestrian Area | Width={fit.RoadWidth:F4}"), path);
                return;
            }
            bool leftSidewalk = elements.Any(x => x.Name == "Left Sidewalk");
            bool rightSidewalk = elements.Any(x => x.Name == "Right Sidewalk");
            if (!leftSidewalk && fit.LeftWidth > 1e-6)
                output.Append(new GH_String($"Verge | Left Verge | Width={fit.LeftWidth:F4} | not sidewalk"), path);
            foreach (var e in elements)
            {
                if (e.Kind == "Curb" && e.Name == "Right Curb Boundary")
                    output.Append(new GH_String($"Roadway Reserve | Remaining Roadway | Width={fit.RoadwayReserve:F4}"), path);
                double width = e.Kind == "Lane" ? fit.LaneWidth
                    : e.Kind == "Median" ? fit.MedianWidth
                    : e.Name == "Left Sidewalk" ? fit.LeftWidth
                    : e.Name == "Right Sidewalk" ? fit.RightWidth : 0;
                output.Append(new GH_String($"{e.Kind} | {e.Name} | Direction={e.Direction} | Width={width:F4} | Minimum={e.Minimum:F4} | Rule={e.Rule}"), path);
            }
            if (!rightSidewalk && fit.RightWidth > 1e-6)
                output.Append(new GH_String($"Verge | Right Verge | Width={fit.RightWidth:F4} | not sidewalk"), path);
        }

        private sealed class FittedSection
        {
            public GH_Path Path;
            public Point3d QL, CL, C, CR, QR;
            public LotBoundaryFitResult Fit;
        }

        private static bool SectionOrder(Point3d ql, Point3d cl, Point3d cr, Point3d qr)
        {
            var span = qr - ql;
            double w2 = span.SquareLength;
            if (w2 <= 1e-9) return false;
            double l = (cl - ql) * span / w2;
            double r = (cr - ql) * span / w2;
            double tol = 0.05;
            return l >= -1e-6 && r <= 1 + 1e-6 && r > l + 1e-6 &&
                (ql + span * l).DistanceTo(cl) <= tol &&
                (ql + span * r).DistanceTo(cr) <= tol;
        }

        private static bool InterpolatedEnvelope(FittedSection a, FittedSection b)
        {
            foreach (double t in new[] { 0.25, 0.5, 0.75 })
            {
                var ql=a.QL+(b.QL-a.QL)*t; var qr=a.QR+(b.QR-a.QR)*t;
                var cl=a.CL+(b.CL-a.CL)*t; var cr=a.CR+(b.CR-a.CR)*t;
                if (!SectionOrder(ql,cl,cr,qr)) return false;
            }
            return true;
        }

        private static double MinimumInterpolatedDistance(Point3d a0, Point3d a1, Point3d b0, Point3d b1)
        {
            var v0 = b0 - a0;
            var dv = (b1 - a1) - v0;
            double d2 = dv.SquareLength;
            double t = d2 > 1e-12 ? Math.Max(0, Math.Min(1, -(v0 * dv) / d2)) : 0;
            return (v0 + dv * t).Length;
        }

        protected override void SolveInstance(IGH_DataAccess da)
        {
            bool run = true; da.GetData(20, ref run); if (!run) { Message = "Pausado"; return; }
            string street = "Rua";
            int type = 2, a = 1, b = 1;
            double lanePref = 3, laneMin = 2.5, median = 1, medianMin = 0.6,
                minL = 1.5, minR = 1.5, pedWidth = 6;
            bool hasMedian = false, left = true, right = true;
            da.GetData(0, ref street); da.GetData(1, ref type); da.GetData(2, ref a); da.GetData(3, ref b);
            da.GetData(4, ref lanePref); da.GetData(5, ref laneMin); da.GetData(6, ref hasMedian);
            da.GetData(7, ref median); da.GetData(8, ref medianMin); da.GetData(9, ref left);
            da.GetData(10, ref minL); da.GetData(11, ref right); da.GetData(12, ref minR);
            da.GetData(13, ref pedWidth);
            bool medianRequired = true; da.GetData(19, ref medianRequired);
            if (type < 0 || type > 2 || pedWidth <= 0 ||
                (type != 0 && (lanePref <= 0 || laneMin <= 0 || laneMin > lanePref ||
                (left && minL <= 0) || (right && minR <= 0) ||
                (type == 1 && a < 1) || (type == 2 && (a < 1 || b < 1)) ||
                (type == 2 && hasMedian && (median <= 0 || medianMin <= 0 || medianMin > median)))))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Verifique tipo 0/1/2, contagens de faixas e larguras positivas; mínimos não podem exceder preferidos.");
                return;
            }
            street = string.IsNullOrWhiteSpace(street) ? "Rua" : street.Trim().Replace(',', ' ');
            var elements = new List<Element>();
            void Add(string kind, string name, int direction, double preferred, double minimum, string rule)
                => elements.Add(new Element { Kind = kind, Name = name, Direction = direction,
                    Preferred = preferred, Minimum = minimum, Rule = rule });
            if (type == 0)
            {
                Add("Pedestrian", "Pedestrian Area", 0, pedWidth, pedWidth, "Minimum");
            }
            else
            {
                if (left) Add("Sidewalk", "Left Sidewalk", 0, minL, minL, "Minimum");
                Add("Curb", "Left Curb Boundary", 0, 0, 0, "Boundary");
                for (int i = 0; i < a; i++) Add("Lane", $"Direction A Lane {i+1}", 1, lanePref, laneMin, "Minimum");
                if (type == 2 && hasMedian) Add("Median", "Central Median", 0, median,
                    medianRequired ? medianMin : 0, medianRequired ? "Required" : "Optional");
                if (type == 2)
                    for (int i = 0; i < b; i++) Add("Lane", $"Direction B Lane {i+1}", -1, lanePref, laneMin, "Minimum");
                Add("Curb", "Right Curb Boundary", 0, 0, 0, "Boundary");
                if (right) Add("Sidewalk", "Right Sidewalk", 0, minR, minR, "Minimum");
            }
            double nominal = elements.Sum(x => x.Preferred), minimumTotal = elements.Sum(x => x.Minimum);
            double roadWidth = type == 0 ? pedWidth : (a + (type == 2 ? b : 0)) * lanePref + (type == 2 && hasMedian ? median : 0);
            string mode = type == 0 ? "Pedonal" : type == 1 ? "Mao Unica" : "Mao Dupla";
            string csv = "Nome_Via,Quadra_Esq,Passeio_Esq,Faixa_Rolamento,Passeio_Dir,Quadra_Dir,Tipo,Canteiro\n" +
                string.Join(",", street, "Quadra", (type != 0 && left ? minL : 0).ToString("F2", CultureInfo.InvariantCulture),
                    roadWidth.ToString("F2", CultureInfo.InvariantCulture),
                    (type != 0 && right ? minR : 0).ToString("F2", CultureInfo.InvariantCulture),
                    "Quadra", mode, (type == 2 && hasMedian ? median : 0).ToString("F2", CultureInfo.InvariantCulture));
            da.SetDataList(0, elements.Select(x => x.ToString()));
            da.SetData(1, csv); da.SetData(2, nominal); da.SetData(3, minimumTotal);
            int selectedStreet = -1; bool usePlanning = false;
            da.GetData(14, ref selectedStreet); da.GetData(17, ref usePlanning);
            GH_Structure<GH_Point> sectionTree, plannedPointTree;
            GH_Structure<GH_Curve> plannedTree;
            da.GetDataTree(15, out sectionTree);
            da.GetDataTree(16, out plannedTree); // ponte antiga mantida; não define o limite do lote
            da.GetDataTree(18, out plannedPointTree);
            var adapted = new GH_Structure<GH_String>();
            var conflicts = new GH_Structure<GH_String>();
            var fittedPoints = new GH_Structure<GH_Point>();
            var adjustments = new GH_Structure<GH_String>();
            var good = new List<FittedSection>();
            int rejected = 0;
            if (sectionTree != null && sectionTree.DataCount > 0)
            {
                var streetIds = sectionTree.Paths.Where(x => x.Indices.Length >= 2)
                    .Select(x => x.Indices[0]).Distinct().ToList();
                if (selectedStreet < 0 && streetIds.Count != 1)
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                        "Escolha Street Index quando Section Points contiver mais de uma rua.");
                else
                {
                    int target = selectedStreet < 0 ? streetIds[0] : selectedStreet;
                    for (int i = 0; i < sectionTree.PathCount; i++)
                    {
                        var path = sectionTree.Paths[i];
                        if (path.Indices.Length < 2 || path.Indices[0] != target) continue;
                        var pts = sectionTree.Branches[i];
                        if (pts.Count != 5 || pts.Any(x => x == null || !x.Value.IsValid))
                        { conflicts.Append(new GH_String("PROFILE_CONFLICT | Reason=INVALID_SECTION_POINTS"), path); rejected++; continue; }
                        var ql = pts[0].Value; var cl = pts[1].Value;
                        var center = pts[2].Value; var cr = pts[3].Value; var qr = pts[4].Value;
                        if (usePlanning && Params.Input[18].SourceCount > 0)
                        {
                            int at = -1;
                            if (plannedPointTree != null)
                                for (int k = 0; k < plannedPointTree.PathCount; k++)
                                    if (plannedPointTree.Paths[k].Equals(path)) { at = k; break; }
                            if (at < 0 || plannedPointTree.Branches[at].Count != 5)
                            { conflicts.Append(new GH_String("PROFILE_CONFLICT | Reason=MISSING_PLANNED_SECTION"), path); rejected++; continue; }
                            var pp = plannedPointTree.Branches[at];
                            if (pp.Any(x => x == null || !x.Value.IsValid) ||
                                pp[0].Value.DistanceTo(ql) > 0.01 || pp[4].Value.DistanceTo(qr) > 0.01)
                            { conflicts.Append(new GH_String("PROFILE_CONFLICT | Reason=LOT_BOUNDARY_MISMATCH"), path); rejected++; continue; }
                            cl = pp[1].Value; cr = pp[3].Value;
                        }
                        if (!SectionOrder(ql, cl, cr, qr))
                        { conflicts.Append(new GH_String("PROFILE_CONFLICT | Reason=INVALID_SECTION_ORDER"), path); rejected++; continue; }
                        var lanes = elements.Where(x => x.Kind == "Lane").ToList();
                        var med = elements.FirstOrDefault(x => x.Kind == "Median");
                        var transverse = qr - ql;
                        double transverseWidth = transverse.Length;
                        double existingLeft = ((cl-ql)*transverse) / transverseWidth;
                        double existingRoad = ((cr-cl)*transverse) / transverseWidth;
                        double existingRight = ((qr-cr)*transverse) / transverseWidth;
                        var request = new LotBoundaryFitRequest
                        {
                            ExistingLeft = existingLeft, ExistingRoad = existingRoad,
                            ExistingRight = existingRight, Pedestrian = type == 0,
                            SidewalkLeft = left, SidewalkRight = right,
                            MinimumLeft = minL, MinimumRight = minR, PedestrianMinimum = pedWidth,
                            LaneCount = lanes.Count, LanePreferred = lanePref, LaneMinimum = laneMin,
                            HasMedian = med != null, MedianRequired = medianRequired,
                            MedianPreferred = med?.Preferred ?? 0, MedianMinimum = medianMin
                        };
                        var fit = LotBoundaryProfileFitter.Fit(request);
                        if (!fit.Success)
                        { conflicts.Append(new GH_String(fit.Diagnostic), path); rejected++; continue; }
                        double w = ql.DistanceTo(qr);
                        var across = qr - ql;
                        var newCl = type == 0 ? cl : ql + across * (fit.LeftWidth / w);
                        var newCr = type == 0 ? cr : qr - across * (fit.RightWidth / w);
                        if (type != 0 && !SectionOrder(ql, newCl, newCr, qr))
                        { conflicts.Append(new GH_String("PROFILE_CONFLICT | Reason=FITTED_SECTION_OUTSIDE_LOT"), path); rejected++; continue; }
                        good.Add(new FittedSection { Path=path, QL=ql, CL=newCl, C=center, CR=newCr, QR=qr, Fit=fit });
                    }
                }
            }
            good.Sort((x,y) => x.Path.Indices[0] != y.Path.Indices[0]
                ? x.Path.Indices[0].CompareTo(y.Path.Indices[0])
                : x.Path.Indices[1].CompareTo(y.Path.Indices[1]));
            var invalidNeighbors = new HashSet<string>();
            if (type != 0)
                for (int i = 1; i < good.Count; i++)
                {
                    var a0 = good[i-1]; var b0 = good[i];
                    if (a0.Path.Indices[0] != b0.Path.Indices[0] ||
                        b0.Path.Indices[1] != a0.Path.Indices[1]+1) continue;
                    double minLeftBetween = MinimumInterpolatedDistance(a0.QL,b0.QL,a0.CL,b0.CL);
                    double minRightBetween = MinimumInterpolatedDistance(a0.CR,b0.CR,a0.QR,b0.QR);
                    if ((left && minLeftBetween + 1e-6 < minL) ||
                        (right && minRightBetween + 1e-6 < minR) ||
                        !InterpolatedEnvelope(a0,b0))
                    {
                        invalidNeighbors.Add(b0.Path.ToString());
                        conflicts.Append(new GH_String($"PROFILE_CONFLICT | Reason=INTERPOLATED_MINIMUM_OR_LOT_ENVELOPE | Left={minLeftBetween:F3} | Right={minRightBetween:F3}"), b0.Path);
                        rejected++;
                    }
                }
            int fitted = 0;
            foreach (var item in good)
            {
                if (invalidNeighbors.Contains(item.Path.ToString())) continue;
                EmitAdapted(elements,item.Fit,adapted,item.Path);
                fittedPoints.Append(new GH_Point(item.QL),item.Path);
                fittedPoints.Append(new GH_Point(item.CL),item.Path);
                fittedPoints.Append(new GH_Point(item.C),item.Path);
                fittedPoints.Append(new GH_Point(item.CR),item.Path);
                fittedPoints.Append(new GH_Point(item.QR),item.Path);
                if (item.Fit.MedianRemoved || item.Fit.Reason != null)
                    adjustments.Append(new GH_String(item.Fit.Diagnostic),item.Path);
                fitted++;
            }
            da.SetDataTree(5, adapted); da.SetDataTree(6, conflicts);
            da.SetDataTree(7, fittedPoints); da.SetDataTree(8, adjustments);
            da.SetData(4, $"Street={street} | Type={mode} | Elements={elements.Count} | Nominal={nominal:F2} | Minimum={minimumTotal:F2} | Adapted={fitted} | Conflicts={rejected}. Lot boundaries fixed; sidewalks reserved first; road minima enforced; no whole-profile scaling.");
            Message = $"{mode} | {fitted} fit";
        }
    }
}
