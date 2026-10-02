using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using Rhino.Geometry.Intersect;

namespace Buraqueira_Urb
{
    public sealed class SidewalkRegularization_Component : GH_Component
    {
        private sealed class Section
        {
            public double Station;
            public Point3d Curb, Block;
            public Vector3d Normal;
            public double Width, Shift;
            public bool Conflict;
        }

        private struct Hit
        {
            public Point3d Point;
            public double Distance;
        }

        public SidewalkRegularization_Component()
            : base("Sidewalk Regularization", "SidewalkReg",
                "Diagnostica e propõe calçadas por lado, preservando eixo, meio-fio e limites originais.",
                "Glaux Urb", "01 | Infraestrutura Viária") { }

        public override Guid ComponentGuid => new Guid("77b1651b-2dcc-4b06-aea3-79e6b17ef3a0");
        protected override System.Drawing.Bitmap Icon => GlauxUrbIcons.SidewalkRegularization;

        protected override void RegisterInputParams(GH_InputParamManager p)
        {
            p.AddCurveParameter("Street Axis", "Axis", "Eixo de um logradouro; não será modificado.", GH_ParamAccess.item);
            p.AddCurveParameter("Block Boundary", "Blocks", "Limites reais de quadra ou lote.", GH_ParamAccess.list);
            p.AddCurveParameter("Street Boundary", "Curbs", "Limites viários existentes; aceita anéis fechados do SHP.", GH_ParamAccess.list);
            p[0].Optional = true; p[1].Optional = true; p[2].Optional = true;
            p.AddIntegerParameter("Mode", "Mode", "0 = Existing; 1 = proposta de regularização.", GH_ParamAccess.item, 0);
            p.AddNumberParameter("Minimum Width Left", "MinL", "Largura mínima no lado esquerdo do eixo.", GH_ParamAccess.item, 1.5);
            p.AddNumberParameter("Minimum Width Right", "MinR", "Largura mínima no lado direito do eixo.", GH_ParamAccess.item, 1.5);
            p.AddNumberParameter("Sampling Step", "Step", "Passo máximo da análise em unidades do modelo.", GH_ParamAccess.item, 2.0);
            p.AddNumberParameter("Alignment Tolerance", "Tol", "Limiar para suavizar deslocamentos locais.", GH_ParamAccess.item, 0.3);
            p.AddNumberParameter("Maximum Displacement", "MaxD", "Deslocamento máximo permitido para o limite privado.", GH_ParamAccess.item, 3.0);
            p.AddTextParameter("Street Name", "Name", "Nome do logradouro no relatório.", GH_ParamAccess.item, "Rua");
            p.AddPointParameter("Section Points", "Pts", "Saída Pts do Road Transversals: {rua;estaca}, cinco pontos [quadra esquerda, meio-fio esquerdo, centro, meio-fio direito, quadra direita]. Quando conectada, reconstrói sem amostrar SHPs.", GH_ParamAccess.tree);
            p[10].Optional = true;
            p.AddCurveParameter("Valid Transversals", "Lines", "Saída Lines do Road Transversals; valida a identidade dos cortes.", GH_ParamAccess.tree);
            p[11].Optional = true;
            p.AddCurveParameter("Planned Transversals", "Planned", "Entrada legada preservada; não altera a borda privada. Use FitPts para a proposta.", GH_ParamAccess.tree);
            p[12].Optional = true;
            p.AddPointParameter("Fitted Section Points", "FitPts", "Cinco pontos ajustados por Street Profile Definition; quadras devem coincidir com Pts originais.", GH_ParamAccess.tree);
            p[13].Optional = true;
            p.AddBooleanParameter("Run", "Run", "Executa a análise; última entrada.", GH_ParamAccess.item, true);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager p)
        {
            p.AddCurveParameter("Original Axis", "Axis", "Eixo original.", GH_ParamAccess.item);
            p.AddCurveParameter("Original Street Boundary", "Curbs", "Limites viários originais.", GH_ParamAccess.list);
            p.AddCurveParameter("Existing Block Boundary", "Blocks", "Limites privados originais.", GH_ParamAccess.list);
            p.AddCurveParameter("Regularized Boundary", "Reg", "Bordas privadas originais; no caminho de seções os limites dos lotes são fixos.", GH_ParamAccess.list);
            p.AddBrepParameter("Existing Sidewalk", "Exist", "Prévia amostrada da calçada existente.", GH_ParamAccess.list);
            p.AddBrepParameter("Proposed Sidewalk", "Prop", "Prévia amostrada da calçada proposta.", GH_ParamAccess.list);
            p.AddBrepParameter("Intervention Area", "Area", "Faixa estimada de intervenção.", GH_ParamAccess.list);
            p.AddTextParameter("Width Analysis", "Widths", "Estaca | lado | existente | proposto | estado.", GH_ParamAccess.list);
            p.AddPointParameter("Conflict Points", "Conf", "Estações sem correspondência ou acima do deslocamento permitido.", GH_ParamAccess.list);
            p.AddTextParameter("Report", "Report", "Métricas e limitações do protótipo.", GH_ParamAccess.item);
            p.AddCurveParameter("Existing Block Edges", "ExistEdge", "Bordas privadas existentes por {rua;run;lado}.", GH_ParamAccess.tree);
            p.AddCurveParameter("Planned Block Edges", "PlanEdge", "Bordas privadas propostas por {rua;run;lado}.", GH_ParamAccess.tree);
            p.AddCurveParameter("Curb Edges", "CurbEdge", "Meios-fios reconstruídos por {rua;run;lado}.", GH_ParamAccess.tree);
            p.AddBrepParameter("Existing Sidewalk Surfaces", "ExistSw", "Faixas de calçada existentes por {rua;run;lado}.", GH_ParamAccess.tree);
            p.AddBrepParameter("Planned Sidewalk Surfaces", "PlanSw", "Faixas de calçada propostas por {rua;run;lado}.", GH_ParamAccess.tree);
            p.AddBrepParameter("Road Surfaces", "RoadSrf", "Superfície viária entre meios-fios por {rua;run}.", GH_ParamAccess.tree);
            p.AddTextParameter("Run Status", "Runs", "Contagens e lacunas por rua e run.", GH_ParamAccess.tree);
            p.AddPointParameter("Run Section Points", "RunPts", "Pontos propostos viáveis por {rua;run;estaca}; alimentam geração adaptativa.", GH_ParamAccess.tree);
            p.AddCurveParameter("Planned Curb Edges", "PlanCurbs", "Meios-fios ajustados, sem mover a borda dos lotes.", GH_ParamAccess.tree);
            p.AddBrepParameter("Planned Road Surfaces", "PlanRoad", "Via entre meios-fios ajustados por {rua;run}.", GH_ParamAccess.tree);
            p.AddPointParameter("Existing Run Section Points", "ExistRunPts", "Cinco pontos existentes por {rua;run;estaca}.", GH_ParamAccess.tree);
            p.AddTextParameter("Planning Conflicts", "Conflicts", "Motivos de ausência ou invalidade das seções ajustadas.", GH_ParamAccess.tree);
        }

        protected override void SolveInstance(IGH_DataAccess da)
        {
            bool run = true;
            da.GetData(14, ref run);
            if (!run) { Message = "Pausado"; return; }

            GH_Structure<GH_Point> sectionTree;
            da.GetDataTree(10, out sectionTree);
            if (sectionTree != null && sectionTree.DataCount > 0)
            {
                GH_Structure<GH_Curve> validTree, plannedTree;
                GH_Structure<GH_Point> fittedTree;
                da.GetDataTree(11, out validTree);
                da.GetDataTree(12, out plannedTree);
                da.GetDataTree(13, out fittedTree);
                if (plannedTree != null && plannedTree.DataCount > 0 &&
                    (fittedTree == null || fittedTree.DataCount == 0))
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                        "Planned Transversals não define novos limites de lote. Conecte FitPts para geometria proposta.");
                var lotBoundaries = new List<Curve>();
                da.GetDataList(1, lotBoundaries);
                ReconstructFromSections(da, sectionTree, validTree, fittedTree,
                    Params.Input[13].SourceCount > 0, lotBoundaries);
                return;
            }

            Curve axis = null;
            var blocks = new List<Curve>();
            var curbs = new List<Curve>();
            if (!da.GetData(0, ref axis) || axis == null || !axis.IsValid ||
                !da.GetDataList(1, blocks) || !da.GetDataList(2, curbs))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Informe eixo, quadras e limites viários.");
                return;
            }
            blocks = blocks.Where(c => c != null && c.IsValid).ToList();
            curbs = curbs.Where(c => c != null && c.IsValid).ToList();
            if (blocks.Count == 0 || curbs.Count == 0) return;

            int mode = 0;
            double minL = 1.5, minR = 1.5, step = 2.0, tol = 0.3, maxD = 3.0;
            string name = "Rua";
            da.GetData(3, ref mode); da.GetData(4, ref minL); da.GetData(5, ref minR);
            da.GetData(6, ref step); da.GetData(7, ref tol); da.GetData(8, ref maxD);
            da.GetData(9, ref name);
            if ((mode != 0 && mode != 1) || minL <= 0 || minR <= 0 ||
                step <= 0 || step > 10 || tol < 0 || maxD < 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Mode 0/1; larguras e Step positivos; Step até 10; Tol e MaxD não negativos.");
                return;
            }

            if (mode == 1)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                    "Planning por shapes brutos foi desativado para proteger LotBoundary. Conecte Pts, FitPts e Blocks ao caminho longitudinal.");
                da.SetData(0,axis); da.SetDataList(1,curbs); da.SetDataList(2,blocks);
                da.SetDataList(3,blocks);
                da.SetData(9,"LOT_BOUNDARY_CONSTRAINT: planejamento legado por deslocamento da borda privada desativado. Use seções filtradas, fitter e quadras reais.");
                Message="Conecte Pts + FitPts";
                return;
            }

            var watch = Stopwatch.StartNew();
            double length = axis.GetLength();
            if (length < step * 2) return;
            int count = Math.Max(2, (int)Math.Ceiling(length / step));
            double radius = Math.Max(20, Math.Max(minL, minR) + maxD + 10);
            var sides = new[] { new List<Section>(), new List<Section>() };
            for (int i = 0; i <= count; i++)
            {
                double s = length * i / count;
                if (!axis.LengthParameter(s, out double t)) continue;
                var origin = axis.PointAt(t);
                var tangent = axis.TangentAt(t);
                tangent.Z = 0;
                if (!tangent.Unitize()) continue;
                var left = new Vector3d(-tangent.Y, tangent.X, 0);
                for (int side = 0; side < 2; side++)
                {
                    var normal = side == 0 ? left : -left;
                    var curb = FirstHit(origin, normal, radius, curbs, 0);
                    var block = curb.HasValue ? FirstHit(origin, normal, radius, blocks, curb.Value.Distance + 0.05) : null;
                    var sec = new Section { Station = s, Normal = normal, Conflict = !curb.HasValue || !block.HasValue };
                    if (!sec.Conflict)
                    {
                        sec.Curb = curb.Value.Point;
                        sec.Block = block.Value.Point;
                        sec.Width = block.Value.Distance - curb.Value.Distance;
                        if (sec.Width <= 0 || sec.Width > 15) sec.Conflict = true;
                    }
                    sides[side].Add(sec);
                }
            }

            var proposed = new List<Curve>();
            var existingSurfaces = new List<Brep>();
            var proposedSurfaces = new List<Brep>();
            var areas = new List<Brep>();
            var widths = new List<string>();
            var conflicts = new List<Point3d>();
            int ok = 0, changed = 0, critical = 0, conflict = 0, valid = 0;
            double minExisting = double.PositiveInfinity, minProposed = double.PositiveInfinity;
            double sumExisting = 0, sumShift = 0, maxShift = 0, area = 0;
            var inv = CultureInfo.InvariantCulture;

            for (int side = 0; side < 2; side++)
            {
                var row = sides[side];
                double target = side == 0 ? minL : minR;
                // O caminho legado é apenas Existing. Planning é resolvido por seções
                // no componente transversal e por LotBoundaryProfileFitter.
                foreach (var sec in row) sec.Shift = 0;

                var chain = new List<Point3d>();
                for (int i = 0; i < row.Count; i++)
                {
                    var sec = row[i];
                    if (sec.Conflict)
                    {
                        Flush(chain, proposed);
                        if (axis.LengthParameter(sec.Station, out double t)) conflicts.Add(axis.PointAt(t));
                        widths.Add($"{sec.Station.ToString("F2", inv)} | {(side == 0 ? "L" : "R")} | ? | ? | CONFLICT");
                        conflict++;
                        continue;
                    }
                    double newWidth = sec.Width + sec.Shift;
                    minExisting = Math.Min(minExisting, sec.Width);
                    minProposed = Math.Min(minProposed, newWidth);
                    sumExisting += sec.Width; sumShift += Math.Abs(sec.Shift); maxShift = Math.Max(maxShift, Math.Abs(sec.Shift)); valid++;
                    string status = sec.Width + 1e-6 >= target
                        ? (Math.Abs(sec.Shift) > 1e-6 ? "REGULARIZED" : "OK")
                        : (mode == 0 ? "CRITICAL" : "REGULARIZED");
                    if (status == "OK") ok++; else if (status == "CRITICAL") critical++; else changed++;
                    widths.Add($"{sec.Station.ToString("F2", inv)} | {(side == 0 ? "L" : "R")} | {sec.Width.ToString("F2", inv)} | {newWidth.ToString("F2", inv)} | {status}");
                    var newBlock = sec.Block + sec.Normal * sec.Shift;
                    if (i > 0 && !row[i - 1].Conflict && row[i - 1].Normal * sec.Normal <= 0.9)
                        Flush(chain, proposed);
                    if (i > 0 && !row[i - 1].Conflict && row[i - 1].Normal * sec.Normal > 0.9)
                    {
                        var prev = row[i - 1];
                        AddPatch(existingSurfaces, prev.Curb, sec.Curb, sec.Block, prev.Block);
                        AddPatch(proposedSurfaces, prev.Curb, sec.Curb, newBlock, prev.Block + prev.Normal * prev.Shift);
                        if (mode == 1 && (Math.Abs(prev.Shift) > 1e-6 || Math.Abs(sec.Shift) > 1e-6))
                        {
                            var patch = AddPatch(areas, prev.Block, sec.Block, newBlock, prev.Block + prev.Normal * prev.Shift);
                            if (patch != null)
                            {
                                var mass = AreaMassProperties.Compute(patch);
                                if (mass != null) area += mass.Area;
                            }
                        }
                    }
                    chain.Add(newBlock);
                }
                Flush(chain, proposed);
            }

            if (mode == 0) proposed = new List<Curve>(blocks);
            watch.Stop();
            var report = new StringBuilder();
            report.AppendLine($"Street: {name} | Mode: {(mode == 0 ? "EXISTING" : "REGULARIZED")}");
            report.AppendLine($"Length: {length:F2} | Samples/side: {count + 1} | Valid: {valid} | Conflicts: {conflict}");
            report.AppendLine($"Existing min: {(valid > 0 ? minExisting.ToString("F2", inv) : "?")} | Existing mean: {(valid > 0 ? (sumExisting / valid).ToString("F2", inv) : "?")} | Proposed min: {(valid > 0 ? minProposed.ToString("F2", inv) : "?")}");
            report.AppendLine($"OK: {ok} | REGULARIZED: {changed} | CRITICAL: {critical} | CONFLICT: {conflict}");
            report.AppendLine($"Max shift: {maxShift:F2} | Mean shift: {(valid > 0 ? sumShift / valid : 0):F2} | Sampled intervention area: {area:F2} | Time: {watch.ElapsedMilliseconds} ms");
            report.AppendLine("Original curves are unchanged. Surfaces and clearance are sampled previews; inspect corners and continuous clearance before design use.");
            da.SetData(0, axis); da.SetDataList(1, curbs); da.SetDataList(2, blocks);
            da.SetDataList(3, proposed); da.SetDataList(4, existingSurfaces);
            da.SetDataList(5, proposedSurfaces); da.SetDataList(6, areas);
            da.SetDataList(7, widths); da.SetDataList(8, conflicts); da.SetData(9, report.ToString());
            Message = $"{valid} seções | {conflict} conflitos";
        }

        private sealed class InputSection
        {
            public int Street, Station;
            public Point3d QL, CL, C, CR, QR, PCL, PCR;
            public bool PlannedValid;
        }

        private static void ReconstructFromSections(IGH_DataAccess da, GH_Structure<GH_Point> points,
            GH_Structure<GH_Curve> lines, GH_Structure<GH_Point> fitted,
            bool fittingConnected, List<Curve> lotBoundaries)
        {
            var watch = Stopwatch.StartNew();
            var rows = new List<InputSection>();
            var conflictReasons = new GH_Structure<GH_String>();
            int invalid = 0;
            bool hasFitted = fittingConnected;
            var closedLots = lotBoundaries?.Where(x => x != null && x.IsValid && x.IsClosed).ToList()
                ?? new List<Curve>();
            for (int i = 0; i < points.PathCount; i++)
            {
                var path = points.Paths[i]; var branch = points.Branches[i];
                if (path.Indices.Length < 2 || branch.Count != 5 || branch.Any(x => x == null || !x.Value.IsValid))
                { invalid++; continue; }
                if (lines != null && lines.DataCount > 0 && !lines.Paths.Any(x => x.Equals(path)))
                { invalid++; continue; }
                var r = new InputSection
                {
                    Street=path.Indices[0], Station=path.Indices[1],
                    QL=branch[0].Value, CL=branch[1].Value, C=branch[2].Value,
                    CR=branch[3].Value, QR=branch[4].Value,
                    PCL=branch[1].Value, PCR=branch[3].Value,
                    PlannedValid=!hasFitted
                };
                if (!OrderedSection(r.QL,r.CL,r.CR,r.QR)) { invalid++; continue; }
                if (hasFitted)
                {
                    int at=-1;
                    if (fitted != null)
                        for (int k=0;k<fitted.PathCount;k++) if (fitted.Paths[k].Equals(path)) {at=k;break;}
                    if (at<0 || fitted.Branches[at].Count!=5 ||
                        fitted.Branches[at].Any(x=>x==null || !x.Value.IsValid))
                        conflictReasons.Append(new GH_String("MISSING_FITTED_SECTION"),path);
                    else
                    {
                        var fb=fitted.Branches[at];
                        if (fb[0].Value.DistanceTo(r.QL)>0.01 || fb[4].Value.DistanceTo(r.QR)>0.01)
                            conflictReasons.Append(new GH_String("LOT_BOUNDARY_MISMATCH"),path);
                        else if (!OrderedSection(r.QL,fb[1].Value,fb[3].Value,r.QR))
                            conflictReasons.Append(new GH_String("INVALID_FITTED_SECTION"),path);
                        else
                        {
                            r.PCL=fb[1].Value; r.PCR=fb[3].Value; r.PlannedValid=true;
                        }
                    }
                }
                rows.Add(r);
            }
            rows.Sort((a,b)=>a.Street!=b.Street?a.Street.CompareTo(b.Street):a.Station.CompareTo(b.Station));
            var existEdge=new GH_Structure<GH_Curve>(); var planEdge=new GH_Structure<GH_Curve>();
            var curbEdge=new GH_Structure<GH_Curve>(); var planCurbs=new GH_Structure<GH_Curve>();
            var existSw=new GH_Structure<GH_Brep>(); var planSw=new GH_Structure<GH_Brep>();
            var road=new GH_Structure<GH_Brep>(); var planRoad=new GH_Structure<GH_Brep>();
            var runStatus=new GH_Structure<GH_String>();
            var runSections=new GH_Structure<GH_Point>(); var existRunSections=new GH_Structure<GH_Point>();
            var legacyExisting=new List<Brep>(); var legacyPlanned=new List<Brep>();
            var legacyBoundary=new List<Curve>(); var intervention=new List<Brep>();
            var legacyConflicts=new List<Point3d>(); var widths=new List<string>();
            int runs=0, roadCount=0, sidewalkCount=0, plannedCount=0;
            var runByStreet=new Dictionary<int,int>();
            int start=0;
            while(start<rows.Count)
            {
                int end=start+1;
                while(end<rows.Count && rows[end].Street==rows[end-1].Street &&
                    IsContinuous(rows[end-1],rows[end])) end++;
                int street=rows[start].Street;
                int run=runByStreet.TryGetValue(street,out int previousRun)?previousRun:0;
                runByStreet[street]=run+1; runs++;
                var runPath=new GH_Path(street,run);
                var allowed=new bool[end-start];
                for(int j=start;j<end;j++) allowed[j-start]=rows[j].PlannedValid;
                if(hasFitted)
                {
                    for(int j=start;j<end;j++)
                        if(allowed[j-start] && closedLots.Count==0)
                        {
                            allowed[j-start]=false;
                            conflictReasons.Append(new GH_String("LOT_BOUNDARY_REQUIRED_FOR_PLANNING"),
                                new GH_Path(street,rows[j].Station));
                        }
                    for(int j=start+1;j<end && closedLots.Count>0;j++)
                    {
                        var a=rows[j-1]; var b=rows[j];
                        if(!allowed[j-start-1] || !allowed[j-start])continue;
                        if(IntrudesLot(a.PCL,b.PCL,b.QL,a.QL,closedLots) ||
                           IntrudesLot(a.PCR,b.PCR,b.QR,a.QR,closedLots) ||
                           IntrudesLot(a.PCL,b.PCL,b.PCR,a.PCR,closedLots))
                        {
                            allowed[j-start]=false;
                            conflictReasons.Append(new GH_String("PLANNED_SURFACE_INTRUDES_LOT"),
                                new GH_Path(street,b.Station));
                        }
                    }
                }
                for(int j=start;j<end;j++)
                {
                    var r=rows[j]; var path=new GH_Path(street,run,r.Station);
                    foreach(var pt in new[]{r.QL,r.CL,r.C,r.CR,r.QR}) existRunSections.Append(new GH_Point(pt),path);
                    if(allowed[j-start])
                        foreach(var pt in new[]{r.QL,r.PCL,r.C,r.PCR,r.QR}) runSections.Append(new GH_Point(pt),path);
                    else legacyConflicts.Add(r.C);
                    widths.Add($"Street={street} | Station={r.Station} | ExistingL={r.QL.DistanceTo(r.CL):F3} | ExistingR={r.QR.DistanceTo(r.CR):F3} | PlannedL={(r.PlannedValid?r.QL.DistanceTo(r.PCL).ToString("F3"):"?")} | PlannedR={(r.PlannedValid?r.QR.DistanceTo(r.PCR).ToString("F3"):"?")}");
                }
                if(end-start>=2)
                {
                    for(int side=0;side<2;side++)
                    {
                        var edgePath=new GH_Path(street,run,side);
                        var q=new List<Point3d>(); var c=new List<Point3d>();
                        for(int j=start;j<end;j++)
                        {
                            var r=rows[j]; q.Add(side==0?r.QL:r.QR); c.Add(side==0?r.CL:r.CR);
                        }
                        existEdge.Append(new GH_Curve(new PolylineCurve(q)),edgePath);
                        curbEdge.Append(new GH_Curve(new PolylineCurve(c)),edgePath);
                        var plannedQ = new List<Point3d>();
                        var plannedCurb = new List<Point3d>();
                        void FlushPlannedChain()
                        {
                            if (plannedCurb.Count >= 2)
                            {
                                planEdge.Append(new GH_Curve(new PolylineCurve(plannedQ)),edgePath);
                                planCurbs.Append(new GH_Curve(new PolylineCurve(plannedCurb)),edgePath);
                            }
                            plannedQ.Clear();
                            plannedCurb.Clear();
                        }
                        for(int j=start+1;j<end;j++)
                        {
                            var a=rows[j-1]; var b=rows[j];
                            Point3d qa=side==0?a.QL:a.QR, qb=side==0?b.QL:b.QR;
                            Point3d ca=side==0?a.CL:a.CR, cb=side==0?b.CL:b.CR;
                            var ex=CreateStrip(ca,cb,qb,qa);
                            if(ex!=null){existSw.Append(new GH_Brep(ex),edgePath);legacyExisting.Add(ex);sidewalkCount++;}
                            else invalid++;
                            if(!allowed[j-start-1] || !allowed[j-start])
                            {
                                FlushPlannedChain();
                                continue;
                            }
                            Point3d pa=side==0?a.PCL:a.PCR, pb=side==0?b.PCL:b.PCR;
                            if (plannedCurb.Count == 0)
                            {
                                plannedQ.Add(qa);
                                plannedCurb.Add(pa);
                            }
                            plannedQ.Add(qb);
                            plannedCurb.Add(pb);
                            legacyBoundary.Add(new LineCurve(qa,qb));
                            var pl=CreateStrip(pa,pb,qb,qa);
                            if(pl!=null){planSw.Append(new GH_Brep(pl),edgePath);legacyPlanned.Add(pl);plannedCount++;}
                            else invalid++;
                            var area=CreateStrip(ca,cb,pb,pa);
                            if(area!=null)intervention.Add(area);
                        }
                        FlushPlannedChain();
                    }
                    for(int j=start+1;j<end;j++)
                    {
                        var a=rows[j-1]; var b=rows[j];
                        var ex=CreateStrip(a.CL,b.CL,b.CR,a.CR);
                        if(ex!=null){road.Append(new GH_Brep(ex),runPath);roadCount++;}else invalid++;
                        if(!allowed[j-start-1] || !allowed[j-start])continue;
                        var pl=CreateStrip(a.PCL,b.PCL,b.PCR,a.PCR);
                        if(pl!=null)planRoad.Append(new GH_Brep(pl),runPath);else invalid++;
                    }
                }
                else legacyConflicts.Add(rows[start].C);
                int fitCount=allowed.Count(x=>x);
                runStatus.Append(new GH_String($"Sections={end-start} | Fitted={fitCount} | RoadPatches={Math.Max(0,end-start-1)}"),runPath);
                if (end < rows.Count && rows[end].Street == street)
                {
                    var a = rows[end-1]; var b = rows[end];
                    string cause = CurbRunTopology.BreakReason(a.Station,b.Station,
                        a.CR.X-a.CL.X,a.CR.Y-a.CL.Y,b.CR.X-b.CL.X,b.CR.Y-b.CL.Y);
                    conflictReasons.Append(new GH_String(
                        $"GAP | StreetPath={street} | Run={run} | PreviousSection={a.Station} | NextSection={b.Station} | Distance={a.C.DistanceTo(b.C):F3} | Classification={cause} | Expected=OPEN"),runPath);
                }
                start=end;
            }
            watch.Stop();
            da.SetDataList(3,legacyBoundary); da.SetDataList(4,legacyExisting);
            da.SetDataList(5,legacyPlanned); da.SetDataList(6,intervention);
            da.SetDataList(7,widths); da.SetDataList(8,legacyConflicts);
            da.SetData(9,$"Street runs={runs} | Existing sections={rows.Count} | Fitted sections={runSections.PathCount} | Existing sidewalk patches={sidewalkCount} | Planned sidewalk patches={plannedCount} | Existing road patches={roadCount} | Invalid geometries={invalid} | Time={watch.ElapsedMilliseconds} ms. Lot endpoints are fixed; gaps in fitted sections are not connected.");
            da.SetDataTree(10,existEdge); da.SetDataTree(11,planEdge); da.SetDataTree(12,curbEdge);
            da.SetDataTree(13,existSw); da.SetDataTree(14,planSw); da.SetDataTree(15,road);
            da.SetDataTree(16,runStatus); da.SetDataTree(17,runSections);
            da.SetDataTree(18,planCurbs); da.SetDataTree(19,planRoad);
            da.SetDataTree(20,existRunSections); da.SetDataTree(21,conflictReasons);
        }

        private static bool IntrudesLot(Point3d a, Point3d b, Point3d c, Point3d d,
            List<Curve> closedLots)
        {
            try
            {
                var patch=new PolylineCurve(new[]{a,b,c,d,a});
                var flat=patch.DuplicateCurve();
                if(flat==null || !flat.Transform(Transform.PlanarProjection(Plane.WorldXY)))return true;
                var box=flat.GetBoundingBox(true);
                foreach(var lot in closedLots)
                {
                    var ring=lot.DuplicateCurve();
                    if(ring==null || !ring.Transform(Transform.PlanarProjection(Plane.WorldXY)))return true;
                    var lb=ring.GetBoundingBox(true);
                    if(box.Max.X<lb.Min.X || lb.Max.X<box.Min.X ||
                       box.Max.Y<lb.Min.Y || lb.Max.Y<box.Min.Y)continue;
                    var overlap=Curve.CreateBooleanIntersection(flat,ring,0.01);
                    if(overlap==null)return true; // falha booleana não libera geometria insegura
                    foreach(var part in overlap)
                    {
                        var mass=AreaMassProperties.Compute(part);
                        if(mass!=null && mass.Area>1e-4)return true;
                    }
                }
                return false;
            }
            catch
            {
                return true; // falha de geometria também bloqueia a proposta
            }
        }

        private static bool OrderedSection(Point3d ql, Point3d cl, Point3d cr, Point3d qr)
        {
            var span=qr-ql; double w2=span.SquareLength;
            if(w2<=1e-9)return false;
            double l=(cl-ql)*span/w2, r=(cr-ql)*span/w2;
            return l>=0 && r<=1 && r>l+1e-6 &&
                (ql+span*l).DistanceTo(cl)<0.05 &&
                (ql+span*r).DistanceTo(cr)<0.05;
        }

        private static bool IsContinuous(InputSection a, InputSection b)
        {
            var va = a.CR-a.CL; var vb = b.CR-b.CL;
            // Station indices come from the same ordered source street. A fixed
            // distance threshold fragmented legitimate 25 m sampling on narrow roads.
            // Direction changes are still a topological break, never smoothed over.
            return CurbRunTopology.BreakReason(a.Station,b.Station,va.X,va.Y,vb.X,vb.Y)==null;
        }

        private static Brep CreateStrip(Point3d a, Point3d b, Point3d c, Point3d d)
        {
            var brep = Brep.CreateFromCornerPoints(a, b, c, d, 0.01);
            return brep != null && brep.IsValid ? brep : null;
        }

        private static Hit? FirstHit(Point3d origin, Vector3d normal, double radius, List<Curve> curves, double after)
        {
            var ray = new LineCurve(origin, origin + normal * radius);
            Hit? best = null;
            foreach (var curve in curves)
            {
                var events = Intersection.CurveCurve(ray, curve, 0.01, 0.01);
                if (events == null) continue;
                foreach (var ev in events)
                {
                    double distance = origin.DistanceTo(ev.PointA);
                    if (distance <= after || distance >= radius ||
                        (best.HasValue && distance >= best.Value.Distance)) continue;
                    var tangent = curve.TangentAt(ev.ParameterB);
                    tangent.Z = 0;
                    if (!tangent.Unitize() || Math.Abs(tangent * normal) > 0.9) continue;
                    best = new Hit { Point = ev.PointA, Distance = distance };
                }
            }
            return best;
        }

        private static Brep AddPatch(List<Brep> output, Point3d a, Point3d b, Point3d c, Point3d d)
        {
            var ring = new PolylineCurve(new[] { a, b, c, d, a });
            var breps = Brep.CreatePlanarBreps(ring, 0.01);
            if (breps == null || breps.Length == 0 || breps[0] == null || !breps[0].IsValid) return null;
            output.Add(breps[0]);
            return breps[0];
        }

        private static void Flush(List<Point3d> chain, List<Curve> output)
        {
            if (chain.Count >= 2) output.Add(new PolylineCurve(chain));
            chain.Clear();
        }
    }
}



