using System.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using Rhino.Geometry.Intersect;

namespace Buraqueira_Urb
{
    /// <summary>
    /// Analisador de Seções Transversais Urbanas a partir de dados GIS (Shapefile ou Curvas).
    /// Gera linhas de corte perpendiculares entre Quadras, Meios-fios e Logradouros,
    /// calcula larguras reais (Calçada Existente) ou projeta Calçada Padrão, e gera CSV para o Road Cross Section.
    /// </summary>
    public class RoadTransversals_Component : GH_Component
    {
        public RoadTransversals_Component()
            : base(
                "Road Transversals from GIS",
                "RoadTransversals",
                "Gera cortes nos eventos geométricos das quadras e por espaçamento máximo, rejeita cruzamentos e mede/planeja calçadas.",
                "Glaux Urb",
                "01 | Infraestrutura Viária")
        {
        }

        public override Guid ComponentGuid => new Guid("b2c3d4e5-f6a7-8b9c-0d1e-2f3a4b5c6d7e");

        protected override System.Drawing.Bitmap Icon => GlauxUrbIcons.RoadTransversals;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            // 0: Logradouros (Eixos viários)
            pManager.AddGenericParameter("Logradouros (Axis)", "Axis", 
                "Eixos dos logradouros (Curvas no Rhino ou caminho string para .shp). Usado para identificar a via e direção de tráfego.", 
                GH_ParamAccess.list);

            // 1: Quadras (Limites prediais / testadas)
            pManager.AddGenericParameter("Quadras (Blocks)", "Blocks", 
                "Polígonos ou limites de quadras mescladas (Curvas, Breps ou caminho string para .shp). Define a caixa total da via.", 
                GH_ParamAccess.list);

            // 2: Meios-fios (Transição calçada / pista)
            pManager.AddGenericParameter("Meios-fios (Curbs)", "Curbs", 
                "Linhas de meio-fio (Curvas ou caminho string para .shp). Evidencia o limite real da calçada existente.", 
                GH_ParamAccess.list);

            // 3: Espaçamento máximo de suporte; eventos geométricos têm prioridade.
            pManager.AddNumberParameter("Maximum Section Spacing", "Step", 
                "Espaçamento máximo entre seções (m). Eventos das bordas reais geram seções obrigatórias; 0 usa o padrão de 25 m.", 
                GH_ParamAccess.item, 25.0);
            pManager[3].Optional = true;

            // 4: Raio Máximo de Busca (Metros)
            pManager.AddNumberParameter("Search Radius", "Radius", 
                "Distância máxima de busca transversal para encontrar as testadas das quadras opostas (Padrão: 40.0m).", 
                GH_ParamAccess.item, 40.0);
            pManager[4].Optional = true;

            // 5: Modo de Calçada
            pManager.AddIntegerParameter("Sidewalk Mode", "Mode", 
                "0 = Existing: mede o shape; 1 = Planning: largura mínima e ajuste local dos meios-fios dentro da faixa pública, com eixo e quadras fixos.", 
                GH_ParamAccess.item, 0);
            pManager[5].Optional = true;

            // 6: Largura Proposta da Calçada Padrão (para Modo 1)
            pManager.AddNumberParameter("Minimum Sidewalk Width", "StdSw", 
                "Largura mínima das calçadas no modo Planning; não é largura uniforme (Padrão: 2.50m).", 
                GH_ParamAccess.item, 2.50);
            pManager[6].Optional = true;

            // 7: Largura Mínima da Pista
            pManager.AddNumberParameter("Min Roadway Width", "MinRoad", 
                "Largura mínima aceitável da pista veicular em metros (Padrão: 6.00m).", 
                GH_ParamAccess.item, 6.00);
            pManager[7].Optional = true;

            // 8: Nome do Campo de Logradouro (DBF)
            pManager.AddTextParameter("Street Name Field", "NameField", 
                "Nome da coluna do DBF contendo o nome da rua (opcional, busca automática por 'NOME', 'LOGRADOURO', 'RUA').", 
                GH_ParamAccess.item, "");
            pManager[8].Optional = true;

            // 9: REGRA DE OURO: 'Run' deve ser SEMPRE o último parâmetro de entrada!
            pManager.AddBooleanParameter("Run", "Run", 
                "Ativa o cálculo das seções transversais (Padrão: True).", 
                GH_ParamAccess.item, true);
            pManager[9].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddCurveParameter("Transversal Lines", "Lines", "Linhas transversais cortando de quadra a quadra no mapa.", GH_ParamAccess.tree);
            pManager.AddPointParameter("Section Points", "Pts", "Árvore de pontos {Rua; Estaca} com [0] QuadraEsq, [1] MeioFioEsq, [2] CentroCorredor, [3] MeioFioDir, [4] QuadraDir.", GH_ParamAccess.tree);
            pManager.AddTextParameter("CSV Output", "CSV", "Tabela CSV completa pronta para conectar diretamente no 'Road Cross Section (CSV)'.", GH_ParamAccess.item);
            pManager.AddTextParameter("Widths Summary", "Widths", "Resumo das larguras das seções [Nome, Estaca, CaixaTotal, CalçadaEsq, Pista, CalçadaDir].", GH_ParamAccess.list);
            pManager.AddTextParameter("Diagnostic Report", "Report", "Relatório técnico com diagnóstico de acessibilidade urbana e pontos críticos.", GH_ParamAccess.item);
            pManager.AddCurveParameter("Candidate Transversals", "Before", "Todos os cortes ajustados entre quadras antes do filtro de cruzamentos.", GH_ParamAccess.tree);
            pManager.AddCurveParameter("Rejected Transversals", "Rejected", "Cortes descartados por cruzamento ou proximidade de nó viário.", GH_ParamAccess.tree);
            pManager.AddTextParameter("Rejection Reasons", "Reasons", "Motivo por {rua;estaca}: CROSSES_OTHER_STREET, NEAR_INTERSECTION ou AMBIGUOUS.", GH_ParamAccess.tree);
            pManager.AddTextParameter("Source Streets", "Source", "Nome, índice do eixo e estaca de cada transversal candidata.", GH_ParamAccess.tree);
            pManager.AddCurveParameter("Planned Transversals", "Planned", "Extensão entre os mesmos limites de lote; PlanPts informa os novos meios-fios.", GH_ParamAccess.tree);
            pManager.AddTextParameter("Existing Dimensions", "Existing", "Larguras medidas à esquerda e direita para cada corte válido.", GH_ParamAccess.tree);
            pManager.AddTextParameter("Planned Dimensions", "PlanDim", "Larguras propostas após mínimo e regularização local.", GH_ParamAccess.tree);
            pManager.AddCurveParameter("Planned Boundary", "PlanBnd", "Bordas privadas originais por lado; LotBoundary permanece fixo no Planning.", GH_ParamAccess.list);
            pManager.AddBrepParameter("Intervention Area", "Area", "Prévia amostrada da faixa entre meio-fio existente e proposto.", GH_ParamAccess.list);
            pManager.AddTextParameter("Planning Conflicts", "Conflicts", "Cortes válidos sem limites confiáveis ou com restrição geométrica.", GH_ParamAccess.tree);
            pManager.AddCurveParameter("Existing Block Boundary", "ExistBnd", "Limites reais de quadra/lote, sem alteração.", GH_ParamAccess.list);
            pManager.AddCurveParameter("Fixed Street Boundary", "Curbs", "Limites viários originais, sem alteração.", GH_ParamAccess.list);
            pManager.AddPointParameter("Planned Section Points", "PlanPts", "Cinco pontos por {rua;estaca}; quadras fixas e meios-fios propostos dentro da faixa pública.", GH_ParamAccess.tree);
            pManager.AddCurveParameter("Planned Curb Boundary", "PlanCurbs", "Prévia aberta por sequências válidas; use Sidewalk Regularization com Blocks e FitPts para validar lotes. Não fecha esquinas.", GH_ParamAccess.list);
            pManager.AddTextParameter("Section Metadata", "SectionMeta", "Tipo, motivo, obrigatoriedade, lado, fonte e vértice de cada seção candidata; mesmo caminho {rua;estaca}.", GH_ParamAccess.tree);
            pManager.AddGenericParameter("Profiled Sections", "Sections", "Seções aceitas com StreetID, StreetName, perfil associado e cinco pontos; mesmo caminho de Pts.", GH_ParamAccess.tree);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // O parâmetro 'Run' é o último (índice 9)
            bool run = true;
            DA.GetData(9, ref run);

            if (!run)
            {
                Message = "Pausado (Run=False)";
                return;
            }

            var axisInput = new List<object>();
            DA.GetDataList(0, axisInput);

            var blocksInput = new List<object>();
            DA.GetDataList(1, blocksInput);

            var curbsInput = new List<object>();
            DA.GetDataList(2, curbsInput);

            double step = 25.0;
            DA.GetData(3, ref step);
            if (double.IsNaN(step) || double.IsInfinity(step) || step <= 0) step = 25.0;

            double radius = 40.0;
            DA.GetData(4, ref radius);
            if (radius < 5.0) radius = 5.0;

            int mode = 0;
            DA.GetData(5, ref mode);

            double stdSw = 2.50;
            DA.GetData(6, ref stdSw);

            double minRoad = 6.00;
            DA.GetData(7, ref minRoad);

            string nameField = "";
            DA.GetData(8, ref nameField);

            // 1. Extração de Geometrias e Nomes
            var streetItems = ExtractStreetCurves(axisInput, nameField);
            var blockCurves = ExtractCurves(blocksInput);
            var curbCurves = ExtractCurves(curbsInput);

            if (streetItems.Count == 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Nenhum eixo de logradouro válido foi fornecido.");
                Message = "Sem Logradouros";
                return;
            }

            if (blockCurves.Count == 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Nenhum limite de quadra foi fornecido.");
                Message = "Sem Quadras";
                return;
            }

            // 2. Processamento das Transversais por Logradouro
            var outLinesTree = new GH_Structure<GH_Curve>();
            var outPtsTree = new GH_Structure<GH_Point>();
            var candidatesTree = new GH_Structure<GH_Curve>();
            var rejectedTree = new GH_Structure<GH_Curve>();
            var reasonsTree = new GH_Structure<GH_String>();
            var sourcesTree = new GH_Structure<GH_String>();
            var sectionMetadataTree = new GH_Structure<GH_String>();
            var profiledSectionsTree = new GH_Structure<GH_ObjectWrapper>();
            var plannedLinesTree = new GH_Structure<GH_Curve>();
            var plannedPointsTree = new GH_Structure<GH_Point>();
            var existingDimensionsTree = new GH_Structure<GH_String>();
            var plannedDimensionsTree = new GH_Structure<GH_String>();
            var planningConflictsTree = new GH_Structure<GH_String>();
            var plannedBoundary = new List<Curve>();
            var plannedCurbBoundary = new List<Curve>();
            var interventionPatches = new List<Brep>();
            var samplesForPlanning = new List<TransversalSample>();
            var widthsList = new List<string>();
            var sbCsv = new StringBuilder();
            var sbReport = new StringBuilder();

            // Cabeçalho CSV compatível com RoadCrossSection_Component
            sbCsv.AppendLine("Nome_Rua,Calcada_Esq,Sarjeta_Esq,Pista_Esq,Pista_Dir,Sarjeta_Dir,Calcada_Dir,Canteiro_Central,Bombeo_Pct,Mao_Unica,Quadra_Esq,Quadra_Dir");

            sbReport.AppendLine("================================================================================");
            sbReport.AppendLine("         BURAQUEIRA URB: DIAGNÓSTICO E SEÇÕES TRANSVERSAIS VIA SHP             ");
            sbReport.AppendLine("================================================================================");
            sbReport.AppendLine($"Modo de Calçada: {(mode == 0 ? "0: Calçada Existente (As-Built)" : $"1: Propor Calçada Padrão ({stdSw:F2}m)")}");
            sbReport.AppendLine($"Logradouros Analisados: {streetItems.Count} | Quadras: {blockCurves.Count} | Meios-fios: {curbCurves.Count}");
            sbReport.AppendLine($"Espaçamento máximo de suporte: {step:F1} metros; eventos geométricos prevalecem.\n");

            int totalSectionsGenerated = 0;
            int criticalSectionsCount = 0;
            int candidateCount = 0, validTransversalCount = 0, rejectedCrossing = 0, rejectedNear = 0, rejectedAmbiguous = 0;
            int missingGeometryCount = 0;
            int beforeSectionCount = 0, geometryRequiredCount = 0, supportCount = 0,
                mergedEventCount = 0, rejectedGeometryCount = 0, detectedLotCorners = 0;
            double minWidthMeasured = double.PositiveInfinity, maxWidthMeasured = double.NegativeInfinity;
            double interventionArea = 0, maxDisplacement = 0;

            for (int sIdx = 0; sIdx < streetItems.Count; sIdx++)
            {
                var street = streetItems[sIdx];
                var crv = street.Curve;
                if (crv == null || !crv.IsValid) continue;

                double len = crv.GetLength();
                if (len < 1.0) continue;

                // Eventos dos dois lados primeiro; lacunas longas são preenchidas depois.
                var events = DetectBoundaryEvents(crv, blockCurves, radius, out int corners);
                var distribution = AdaptiveSectionPlanner.Build(len, step, Math.Min(0.25, step * 0.1), events);
                var stations = distribution.Stations;
                beforeSectionCount += LegacyRegularCount(len, step);
                geometryRequiredCount += distribution.GeometryRequired;
                supportCount += distribution.Support;
                mergedEventCount += distribution.Merged;
                detectedLotCorners += corners;

                sbReport.AppendLine($"📍 Logradouro: {street.Name} (Extensão: {len:F1}m | {stations.Count} estacas)");

                for (int stIdx = 0; stIdx < stations.Count; stIdx++)
                {
                    var section = stations[stIdx];
                    double distOnCrv = section.Station;
                    if (!crv.LengthParameter(distOnCrv, out double t)) continue;

                    var ptOnAxis = crv.PointAt(t);
                    var tan = crv.TangentAt(t);
                    tan.Z = 0;
                    if (!tan.Unitize()) continue;

                    // Vetor Normal perpendicular em XY: (-Ty, Tx, 0)
                    var norm = new Vector3d(-tan.Y, tan.X, 0);

                    // Criar raio de corte transversal de -Radius a +Radius
                    var pLeftRay = ptOnAxis + (norm * radius);
                    var pRightRay = ptOnAxis - (norm * radius);
                    var transLine = new Line(pLeftRay, pRightRay);
                    var transCrv = new LineCurve(transLine);

                    // 1. Interseção com Quadras (encontrar testadas da quadra esquerda e direita)
                    Point3d? ptQuadraLeft = null;
                    Point3d? ptQuadraRight = null;
                    double bestDistLeft = double.MaxValue;
                    double bestDistRight = double.MaxValue;

                    foreach (var blk in blockCurves)
                    {
                        var ccx = Intersection.CurveCurve(transCrv, blk, 0.01, 0.01);
                        if (ccx == null || ccx.Count == 0) continue;

                        for (int k = 0; k < ccx.Count; k++)
                        {
                            var intPt = ccx[k].PointA;
                            var v = intPt - ptOnAxis;
                            double dot = v * norm; // > 0 = Lado Esquerdo, < 0 = Lado Direito
                            double d = ptOnAxis.DistanceTo(intPt);

                            if (dot > 0.05 && d < bestDistLeft)
                            {
                                bestDistLeft = d;
                                ptQuadraLeft = intPt;
                            }
                            else if (dot < -0.05 && d < bestDistRight)
                            {
                                bestDistRight = d;
                                ptQuadraRight = intPt;
                            }
                        }
                    }

                    // Se não encontrou as duas quadras, não é possível fechar o corredor com precisão
                    if (!ptQuadraLeft.HasValue || !ptQuadraRight.HasValue)
                    {
                        continue;
                    }

                    var qL = ptQuadraLeft.Value;
                    var qR = ptQuadraRight.Value;
                    double wTotal = qL.DistanceTo(qR);

                    // Centro real do corredor entre as quadras (independente de onde o logradouro foi digitalizado!)
                    var corridorMid = (qL + qR) * 0.5;

                    // 2. Interseção com Meios-fios dentro do corredor da via
                    var corridorLine = new Line(qL, qR);
                    var corridorCrv = new LineCurve(corridorLine);
                    var candidatePath = new GH_Path(sIdx, stIdx);
                    candidatesTree.Append(new GH_Curve(corridorCrv), candidatePath);
                    sourcesTree.Append(new GH_String($"{street.Name} | SourceIndex={sIdx} | SourceId={street.SourceId} | Station={distOnCrv:F2}"), candidatePath);
                    string sectionType = !section.Required ? "SUPPORT" :
                        section.SourceId == "AXIS" ? "RUN_BOUNDARY" : "GEOMETRY_EVENT";
                    string profInfo = street.Profile != null ? $" | Profile={street.Profile.StreetName}" : "";
                    sectionMetadataTree.Append(new GH_String($"Station={distOnCrv:F3} | Type={sectionType} | Reason={section.Reason} | IsRequired={section.Required} | IsSupport={!section.Required} | Hard={section.Hard} | Side={section.Side} | SourceStreetID={street.SourceId} | StreetName={street.Name}{profInfo} | RunID=UNASSIGNED | SourceGeometryID={section.SourceId} | SourceVertexID={section.VertexId} | Status=CANDIDATE"), candidatePath);
                    candidateCount++;

                    Point3d? ptCurbLeft = null;
                    Point3d? ptCurbRight = null;
                    double bestCurbDistL = double.MaxValue;
                    double bestCurbDistR = double.MaxValue;

                    foreach (var curb in curbCurves)
                    {
                        var ccx = Intersection.CurveCurve(corridorCrv, curb, 0.01, 0.01);
                        if (ccx == null || ccx.Count == 0) continue;

                        for (int k = 0; k < ccx.Count; k++)
                        {
                            var intPt = ccx[k].PointA;
                            double dL = qL.DistanceTo(intPt);
                            double dR = qR.DistanceTo(intPt);

                            // O meio-fio esquerdo deve estar a pelo menos 0.3m da quadra e do centro
                            if (dL > 0.30 && dL < (wTotal * 0.5) && dL < bestCurbDistL)
                            {
                                bestCurbDistL = dL;
                                ptCurbLeft = intPt;
                            }

                            if (dR > 0.30 && dR < (wTotal * 0.5) && dR < bestCurbDistR)
                            {
                                bestCurbDistR = dR;
                                ptCurbRight = intPt;
                            }
                        }
                    }

                    // Filtro localizado: somente o corte final ajustado entre quadras é avaliado.
                    // A zona de exclusão usa metade da largura real entre meios-fios; sem ambos,
                    // não se presume uma distância arbitrária.
                    double nodeRadius = ptCurbLeft.HasValue && ptCurbRight.HasValue
                        ? 0.5 * ptCurbLeft.Value.DistanceTo(ptCurbRight.Value) : 0.0;
                    string rejection = ValidateAgainstStreetNetwork(corridorCrv, streetItems, sIdx, crv, t, nodeRadius);
                    if (rejection != null)
                    {
                        rejectedTree.Append(new GH_Curve(corridorCrv), candidatePath);
                        reasonsTree.Append(new GH_String(rejection), candidatePath);
                        if (rejection == "CROSSES_OTHER_STREET") rejectedCrossing++;
                        else if (rejection == "NEAR_INTERSECTION") rejectedNear++;
                        else rejectedAmbiguous++;
                        if (section.Required && section.SourceId != "AXIS") rejectedGeometryCount++;
                        continue;
                    }

                    // O corte válido continua sendo a geometria dentro da faixa pública.
                    var path = new GH_Path(sIdx, stIdx);
                    outLinesTree.Append(new GH_Curve(corridorCrv), path);
                    validTransversalCount++;
                    if (!ptCurbLeft.HasValue || !ptCurbRight.HasValue)
                    {
                        if (mode == 0) // Existing Mode: requer meio-fio real medido
                        {
                            planningConflictsTree.Append(new GH_String("MISSING_STREET_BOUNDARY"), path);
                            missingGeometryCount++;
                            continue;
                        }
                        else // Planning Mode: projeta calçada padrão a partir do domínio público entre quadras
                        {
                            double wPublic = qL.DistanceTo(qR);
                            double maxSw = Math.Max(0.5, (wPublic - Math.Max(minRoad, 2.0)) * 0.5);
                            double defSwL = Math.Min(stdSw, maxSw);
                            double defSwR = defSwL;

                            var acrossCorridor = qR - qL;
                            ptCurbLeft = qL + acrossCorridor * (defSwL / Math.Max(1e-6, wPublic));
                            ptCurbRight = qR - acrossCorridor * (defSwR / Math.Max(1e-6, wPublic));
                        }
                    }

                    var cL = ptCurbLeft.Value;
                    var cR = ptCurbRight.Value;
                    double wSwL_meas = qL.DistanceTo(cL);
                    double wSwR_meas = qR.DistanceTo(cR);
                    double wRoad_meas = cL.DistanceTo(cR);
                    minWidthMeasured = Math.Min(minWidthMeasured, wTotal);
                    maxWidthMeasured = Math.Max(maxWidthMeasured, wTotal);
                    if (wSwL_meas <= 0 || wSwR_meas <= 0 || wRoad_meas <= 0)
                    {
                        planningConflictsTree.Append(new GH_String("INVALID_GEOMETRY"), path);
                        missingGeometryCount++;
                        continue;
                    }

                    if (wRoad_meas < minRoad)
                    {
                        criticalSectionsCount++;
                        planningConflictsTree.Append(new GH_String("ROADWAY_BELOW_MINIMUM"), path);
                    }

                    outPtsTree.Append(new GH_Point(qL), path);
                    outPtsTree.Append(new GH_Point(cL), path);
                    outPtsTree.Append(new GH_Point(corridorMid), path);
                    outPtsTree.Append(new GH_Point(cR), path);
                    outPtsTree.Append(new GH_Point(qR), path);
                    profiledSectionsTree.Append(new GH_ObjectWrapper(new ProfiledSection
                    {
                        StreetPathIndex=sIdx,StationIndex=stIdx,StreetID=street.SourceId,
                        StreetName=street.Name,StreetProfile=street.Profile,
                        IsRequired=section.Required,Reason=section.Reason,
                        Points=new[]{qL,cL,corridorMid,cR,qR}
                    }),path);

                    existingDimensionsTree.Append(new GH_String($"L={wSwL_meas:F2} | R={wSwR_meas:F2} | Road={wRoad_meas:F2}"), path);
                    Vector3d tanOnAxis = crv.TangentAt(t);
                    tanOnAxis.Unitize();
                    samplesForPlanning.Add(new TransversalSample
                    {
                        StreetIndex = sIdx, StationIndex = stIdx, Station = distOnCrv, Name = street.Name,
                        QL = qL, QR = qR, CL = cL, CR = cR, Axis = ptOnAxis, Normal = norm, Tangent = tanOnAxis,
                        ExistingLeft = wSwL_meas, ExistingRight = wSwR_meas, Road = wRoad_meas
                    });

                    // CSV legado continua em modo Existing. No Planning ele será
                    // preenchido depois da regularização da sequência.
                    double halfRoad = wRoad_meas / 2.0;
                    string secLabel = stations.Count == 1 ? street.Name : $"{street.Name} Est.{distOnCrv:F0}";
                    if (mode == 0)
                        sbCsv.AppendLine($"{secLabel},{wSwL_meas:F2},0.25,{halfRoad:F2},{halfRoad:F2},0.25,{wSwR_meas:F2},0.0,2.0,Falso,Quadra,Quadra");
                    widthsList.Add($"{secLabel} | Caixa: {wTotal:F2}m | Calç.Esq: {wSwL_meas:F2}m | Pista: {wRoad_meas:F2}m | Calç.Dir: {wSwR_meas:F2}m");
                    sbReport.AppendLine($"   - Estaca {distOnCrv:F1}m: Calçada existente L={wSwL_meas:F2}m | R={wSwR_meas:F2}m | Pista={wRoad_meas:F2}m");
                    totalSectionsGenerated++;
                }
                sbReport.AppendLine("");
            }

            // Planeja apenas sequências de estacas válidas e medidas. Saltos,
            // rejeições e mudanças bruscas de direção quebram a suavização.
            double existingMin = double.PositiveInfinity, plannedMin = double.PositiveInfinity;
            double existingSum = 0, plannedSum = 0, maxNeighborExisting = 0, maxNeighborPlanned = 0;
            int measuredCount = 0;
            var byStreet = new Dictionary<int, List<TransversalSample>>();
            foreach (var sample in samplesForPlanning)
            {
                if (!byStreet.TryGetValue(sample.StreetIndex, out var list))
                    byStreet[sample.StreetIndex] = list = new List<TransversalSample>();
                list.Add(sample);
            }
            foreach (var streetPair in byStreet)
            {
                var ordered = streetPair.Value;
                ordered.Sort((a, b) => a.StationIndex.CompareTo(b.StationIndex));
                int start = 0;
                while (start < ordered.Count)
                {
                    int end = start + 1;
                    while (end < ordered.Count &&
                           CurbRunTopology.BreakReason(ordered[end - 1].StationIndex,
                               ordered[end].StationIndex, ordered[end - 1].Normal.X,
                               ordered[end - 1].Normal.Y, ordered[end].Normal.X,
                               ordered[end].Normal.Y, 0.9) == null) end++;
                    int n = end - start;
                    var leftExisting = new double[n];
                    var rightExisting = new double[n];
                    for (int j = 0; j < n; j++)
                    {
                        leftExisting[j] = ordered[start + j].ExistingLeft;
                        rightExisting[j] = ordered[start + j].ExistingRight;
                    }
                    var leftPlanned = mode == 1
                        ? MinimumDimensionPlanner.Plan(leftExisting, stdSw, 0.30) : leftExisting;
                    var rightPlanned = mode == 1
                        ? MinimumDimensionPlanner.Plan(rightExisting, stdSw, 0.30) : rightExisting;
                    var leftBoundary = new List<Point3d>();
                    var rightBoundary = new List<Point3d>();
                    var leftCurbs = new List<Point3d>();
                    var rightCurbs = new List<Point3d>();
                    TransversalSample previous = null;
                    Point3d previousPlannedCL = Point3d.Unset, previousPlannedCR = Point3d.Unset;
                    void FlushPlanningRun()
                    {
                        if (leftBoundary.Count >= 2) plannedBoundary.Add(new PolylineCurve(leftBoundary));
                        if (rightBoundary.Count >= 2) plannedBoundary.Add(new PolylineCurve(rightBoundary));

                        // A single station is a point, not a measured longitudinal curb.
                        // Keep hard corners until their geometry can be inferred from GIS.
                        if (leftCurbs.Count >= 2) plannedCurbBoundary.Add(new PolylineCurve(leftCurbs));
                        if (rightCurbs.Count >= 2) plannedCurbBoundary.Add(new PolylineCurve(rightCurbs));

                        leftBoundary.Clear(); rightBoundary.Clear(); leftCurbs.Clear(); rightCurbs.Clear();
                        previous = null;
                    }
                    for (int j = 0; j < n; j++)
                    {
                        var sample = ordered[start + j];
                        var path = new GH_Path(sample.StreetIndex, sample.StationIndex);
                        double pl = leftPlanned[j], pr = rightPlanned[j];
                        double publicWidth = sample.QL.DistanceTo(sample.QR);
                        const double positiveRoad = 0.01;
                        if (mode == 1)
                        {
                            if (publicWidth + 1e-8 < 2 * stdSw + positiveRoad)
                            {
                                planningConflictsTree.Append(new GH_String(
                                    $"PROFILE_CONFLICT | AvailableWidth={publicWidth:F3} | SidewalkMinimumRequired={2 * stdSw:F3} | RoadMinimumRequired=0.010 | TotalMinimumRequired={2 * stdSw + positiveRoad:F3} | Deficit={Math.Max(0, 2 * stdSw + positiveRoad - publicWidth):F3} | Reason=INSUFFICIENT_PUBLIC_WIDTH"), path);
                                FlushPlanningRun();
                                continue;
                            }
                            double remainingForSidewalk = publicWidth - positiveRoad;
                            if (pl + pr > remainingForSidewalk)
                            {
                                double extraL = Math.Max(0, pl - stdSw), extraR = Math.Max(0, pr - stdSw);
                                double factor = (remainingForSidewalk - 2 * stdSw) / Math.Max(1e-9, extraL + extraR);
                                pl = stdSw + extraL * factor;
                                pr = stdSw + extraR * factor;
                            }
                        }
                        // A borda privada é um limite duro. Qualquer ganho de calçada desloca
                        // o meio-fio proposto para dentro da faixa pública, nunca para o lote.
                        var across = sample.QR - sample.QL;
                        var plannedCL = mode == 1 ? sample.QL + across * (pl / publicWidth) : sample.CL;
                        var plannedCR = mode == 1 ? sample.QR - across * (pr / publicWidth) : sample.CR;
                        double plannedRoad = plannedCL.DistanceTo(plannedCR);
                        if (plannedRoad < positiveRoad - 1e-6)
                        {
                            planningConflictsTree.Append(new GH_String("PROFILE_CONFLICT | Reason=ROAD_DOMAIN_COLLAPSED"), path);
                            FlushPlanningRun();
                            continue;
                        }
                        plannedLinesTree.Append(new GH_Curve(new LineCurve(sample.QL, sample.QR)), path);
                        plannedPointsTree.Append(new GH_Point(sample.QL), path);
                        plannedPointsTree.Append(new GH_Point(plannedCL), path);
                        plannedPointsTree.Append(new GH_Point(sample.Axis), path);
                        plannedPointsTree.Append(new GH_Point(plannedCR), path);
                        plannedPointsTree.Append(new GH_Point(sample.QR), path);
                        plannedDimensionsTree.Append(new GH_String($"L={pl:F2} | R={pr:F2} | Road={plannedRoad:F2}"), path);
                        bool adjacentForMetrics = j > 0 && (mode == 0 || previous != null);

                        leftBoundary.Add(sample.QL); rightBoundary.Add(sample.QR);
                        leftCurbs.Add(plannedCL); rightCurbs.Add(plannedCR);

                        if (mode == 1)
                        {
                            double halfRoad = plannedRoad / 2.0;
                            sbCsv.AppendLine($"{sample.Name} Est.{sample.Station:F0},{pl:F2},0.25,{halfRoad:F2},{halfRoad:F2},0.25,{pr:F2},0.0,2.0,Falso,Quadra,Quadra");
                            maxDisplacement = Math.Max(maxDisplacement,
                                Math.Max(sample.CL.DistanceTo(plannedCL), sample.CR.DistanceTo(plannedCR)));
                            if (previous != null)
                            {
                                interventionArea += AddInterventionPatch(interventionPatches,
                                    previous.CL, sample.CL, plannedCL, previousPlannedCL);
                                interventionArea += AddInterventionPatch(interventionPatches,
                                    previous.CR, sample.CR, plannedCR, previousPlannedCR);
                            }
                            previous = sample;
                            previousPlannedCL = plannedCL; previousPlannedCR = plannedCR;
                        }
                        existingMin = Math.Min(existingMin, Math.Min(sample.ExistingLeft, sample.ExistingRight));
                        plannedMin = Math.Min(plannedMin, Math.Min(pl, pr));
                        existingSum += sample.ExistingLeft + sample.ExistingRight;
                        plannedSum += pl + pr; measuredCount += 2;
                        if (adjacentForMetrics)
                        {
                            maxNeighborExisting = Math.Max(maxNeighborExisting,
                                Math.Max(Math.Abs(leftExisting[j] - leftExisting[j - 1]),
                                         Math.Abs(rightExisting[j] - rightExisting[j - 1])));
                            maxNeighborPlanned = Math.Max(maxNeighborPlanned,
                                Math.Max(Math.Abs(pl - leftPlanned[j - 1]), Math.Abs(pr - rightPlanned[j - 1])));
                        }
                    }
                    FlushPlanningRun();
                    start = end;
                }
            }
            // Preserve the actual lot boundary; curb closure belongs to a validated
            // intersection/topology stage, never to a block offset.
            if (blockCurves != null && blockCurves.Count > 0)
            {
                // Planned Boundary (PlanBnd) preserva sempre os limites de quadra fixos (Hard Constraint)
                foreach (var b in blockCurves)
                {
                    if (b != null && b.IsValid && !plannedBoundary.Contains(b))
                    {
                        plannedBoundary.Add(b);
                    }
                }
            }
            if (measuredCount > 0)
                sbReport.AppendLine($"Widths existing min/mean={existingMin:F2}/{existingSum / measuredCount:F2}; planned min/mean={plannedMin:F2}/{plannedSum / measuredCount:F2}; max neighbor jump {maxNeighborExisting:F2} -> {maxNeighborPlanned:F2}");
            sbReport.AppendLine($"Intervention area (sampled)={interventionArea:F2} m2 | Max displacement={maxDisplacement:F2} m | Missing geometry={missingGeometryCount}");
            sbReport.AppendLine("PlanCurbs is an open section-sampled preview. Validate lot intrusion and intersections downstream; no curb rings are inferred from block offsets.");
            sbReport.AppendLine("================================================================================");
            sbReport.AppendLine($"TRANSVERSAIS CANDIDATAS: {candidateCount}");
            sbReport.AppendLine($"VÁLIDAS: {validTransversalCount} | REJEITADAS: {rejectedCrossing + rejectedNear + rejectedAmbiguous}");
            sbReport.AppendLine($"CROSSES_OTHER_STREET: {rejectedCrossing} | NEAR_INTERSECTION: {rejectedNear} | AMBIGUOUS: {rejectedAmbiguous}");
            sbReport.AppendLine($"TOTAL DE SEÇÕES GERADAS: {totalSectionsGenerated}");
            sbReport.AppendLine($"Distribution: Before={beforeSectionCount} | After={candidateCount} | GeometryRequired={geometryRequiredCount} | Support={supportCount} | Merged={mergedEventCount} | RejectedAtIntersections={rejectedGeometryCount} | LotCornersDetected={detectedLotCorners}");
            sbReport.AppendLine($"Measured corridor width min/max: {(double.IsPositiveInfinity(minWidthMeasured) ? "unknown" : minWidthMeasured.ToString("F2"))} / {(double.IsNegativeInfinity(maxWidthMeasured) ? "unknown" : maxWidthMeasured.ToString("F2"))} m; station samples do not prove global extrema between stations.");
            if (criticalSectionsCount > 0)
            {
                sbReport.AppendLine($"⚠️ SEÇÕES CRÍTICAS DETECTADAS: {criticalSectionsCount} (largura de pista insuficiente para o padrão)");
            }
            sbReport.AppendLine("Tabela CSV gerada pronta para alimentar o componente 'Road Cross Section (CSV)'!");
            sbReport.AppendLine("================================================================================");

            DA.SetDataTree(0, outLinesTree);
            DA.SetDataTree(1, outPtsTree);
            DA.SetData(2, sbCsv.ToString());
            DA.SetDataList(3, widthsList);
            DA.SetData(4, sbReport.ToString());
            DA.SetDataTree(5, candidatesTree);
            DA.SetDataTree(6, rejectedTree);
            DA.SetDataTree(7, reasonsTree);
            DA.SetDataTree(8, sourcesTree);
            DA.SetDataTree(9, plannedLinesTree);
            DA.SetDataTree(10, existingDimensionsTree);
            DA.SetDataTree(11, plannedDimensionsTree);
            DA.SetDataList(12, plannedBoundary);
            DA.SetDataList(13, interventionPatches);
            DA.SetDataTree(14, planningConflictsTree);
            DA.SetDataList(15, blockCurves);
            DA.SetDataList(16, curbCurves);
            DA.SetDataTree(17, plannedPointsTree);
            DA.SetDataList(18, plannedCurbBoundary);
            DA.SetDataTree(19, sectionMetadataTree);
            DA.SetDataTree(20, profiledSectionsTree);

            Message = $"{totalSectionsGenerated} Seções";
        }

        private static int LegacyRegularCount(double length, double spacing)
        {
            if (length <= spacing * 1.2) return 1;
            double margin = Math.Min(5.0, length * 0.15);
            return Math.Max(1, (int)Math.Floor((length - 2 * margin) / spacing) + 1);
        }

        private static List<SectionEvent> DetectBoundaryEvents(Curve axis, List<Curve> blocks,
            double radius, out int corners)
        {
            var result = new List<SectionEvent>();
            corners = 0;
            var axisBox = axis.GetBoundingBox(true);
            axisBox.Inflate(radius);
            for (int source = 0; source < blocks.Count; source++)
            {
                var boundary = blocks[source];
                if (boundary == null || !boundary.IsValid) continue;
                var blockBox = boundary.GetBoundingBox(true);
                if (axisBox.Max.X < blockBox.Min.X || blockBox.Max.X < axisBox.Min.X ||
                    axisBox.Max.Y < blockBox.Min.Y || blockBox.Max.Y < axisBox.Min.Y) continue;
                if (!boundary.TryGetPolyline(out Polyline poly) || poly.Count < 3) continue;
                bool closed = poly.IsClosed;
                int unique = closed ? poly.Count - 1 : poly.Count;
                if (unique < 3) continue;
                for (int vertex = 0; vertex < unique; vertex++)
                {
                    if (!closed && (vertex == 0 || vertex == unique - 1)) continue;
                    var prev = poly[(vertex - 1 + unique) % unique];
                    var point = poly[vertex];
                    var next = poly[(vertex + 1) % unique];
                    string kind = AdaptiveSectionPlanner.ClassifyVertex(prev.X, prev.Y,
                        point.X, point.Y, next.X, next.Y, 12.0, 0.20);
                    if (kind != "LOT_CORNER" && kind != "DIRECTION_CHANGE") continue;
                    if (!axis.ClosestPoint(point, out double t)) continue;
                    var onAxis = axis.PointAt(t);
                    var transverse = point - onAxis;
                    transverse.Z = 0;
                    if (transverse.Length > radius || transverse.Length < 0.5) continue;
                    var tangent = axis.TangentAt(t); tangent.Z = 0;
                    if (!tangent.Unitize()) continue;
                    var normal = new Vector3d(-tangent.Y, tangent.X, 0);
                    int side = transverse * normal > 0 ? 1 : -1;
                    if (!MatchesNearestBoundary(point, onAxis, normal, side, radius, blocks)) continue;
                    double station;
                    try { station = axis.GetLength(new Interval(axis.Domain.T0, t)); }
                    catch { continue; }
                    if (double.IsNaN(station) || station < 0 || station > axis.GetLength()) continue;
                    result.Add(new SectionEvent { Station = station, Reason = kind,
                        SourceId = "Block:" + source, VertexId = vertex, Side = side, Hard = true,
                        BoundaryDistance = transverse.Length });
                    if (kind == "LOT_CORNER") corners++;
                }
            }
            return result;
        }

        private static bool MatchesNearestBoundary(Point3d vertex, Point3d axisPoint,
            Vector3d normal, int side, double radius, List<Curve> blocks)
        {
            var ray = new LineCurve(axisPoint, axisPoint + normal * (side * radius));
            double nearest = double.PositiveInfinity;
            Point3d nearestPoint = Point3d.Unset;
            foreach (var boundary in blocks)
            {
                var hits = Intersection.CurveCurve(ray, boundary, 0.01, 0.01);
                if (hits == null) continue;
                foreach (var hit in hits)
                {
                    double distance = axisPoint.DistanceTo(hit.PointA);
                    if (distance > 0.05 && distance < nearest)
                    {
                        nearest = distance;
                        nearestPoint = hit.PointA;
                    }
                }
            }
            return nearestPoint.IsValid && nearestPoint.DistanceTo(vertex) <= 0.75;
        }

        private static string ValidateAgainstStreetNetwork(
            Curve candidate, List<StreetItem> streets, int sourceIndex,
            Curve source, double sourceParameter, double nodeRadius)
        {
            const double tolerance = 0.01;
            var sourceName = streets[sourceIndex].Name;
            bool nearIntersection = false;
            for (int i = 0; i < streets.Count; i++)
            {
                if (i == sourceIndex || streets[i].Curve == null || !streets[i].Curve.IsValid) continue;
                var other = streets[i].Curve;
                bool sameNamedStreet = !string.IsNullOrWhiteSpace(sourceName) &&
                    !sourceName.StartsWith("Rua_", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(sourceName, streets[i].Name, StringComparison.OrdinalIgnoreCase);
                bool continuation = AreConnectedContinuation(source, other);
                if (continuation) continue;

                var hits = Intersection.CurveCurve(candidate, other, tolerance, tolerance);
                if (hits != null && hits.Count > 0)
                {
                    // Nome igual pode indicar outro segmento da mesma rua; se o ângulo
                    // contradiz essa hipótese, não se usa a seção sem revisão.
                    if (sameNamedStreet)
                    {
                        foreach (var hit in hits)
                        {
                            var a = source.TangentAt(sourceParameter);
                            var b = other.TangentAt(hit.ParameterB);
                            a.Z = 0; b.Z = 0;
                            if (!a.Unitize() || !b.Unitize() || Math.Abs(a * b) < 0.8)
                                return "AMBIGUOUS";
                        }
                        continue;
                    }
                    return "CROSSES_OTHER_STREET";
                }

                if (sameNamedStreet || nodeRadius <= 0) continue;
                var nodes = Intersection.CurveCurve(source, other, tolerance, tolerance);
                if (nodes == null) continue;
                foreach (var node in nodes)
                {
                    var domain = new Interval(
                        Math.Min(sourceParameter, node.ParameterA),
                        Math.Max(sourceParameter, node.ParameterA));
                    if (source.GetLength(domain) <= nodeRadius)
                        nearIntersection = true;
                }
            }
            return nearIntersection ? "NEAR_INTERSECTION" : null;
        }

        private static bool AreConnectedContinuation(Curve a, Curve b)
        {
            const double joinTolerance = 0.25;
            var endsA = new[] { a.PointAtStart, a.PointAtEnd };
            var endsB = new[] { b.PointAtStart, b.PointAtEnd };
            var tangentsA = new[] { a.TangentAtStart, a.TangentAtEnd };
            var tangentsB = new[] { b.TangentAtStart, b.TangentAtEnd };
            for (int i = 0; i < 2; i++)
            for (int j = 0; j < 2; j++)
            {
                if (endsA[i].DistanceTo(endsB[j]) > joinTolerance) continue;
                var ta = tangentsA[i]; var tb = tangentsB[j];
                ta.Z = 0; tb.Z = 0;
                if (ta.Unitize() && tb.Unitize() && Math.Abs(ta * tb) >= 0.985)
                    return true;
            }
            return false;
        }

        #region Helpers de Extração de Geometrias e Nomes

        private class StreetItem
        {
            public Curve Curve;
            public string Name;
            public string SourceId;
            public StreetProfile Profile;
        }

        private sealed class TransversalSample
        {
            public int StreetIndex, StationIndex;
            public double Station, ExistingLeft, ExistingRight, Road;
            public string Name;
            public Point3d QL, QR, CL, CR, Axis;
            public Vector3d Normal;
            public Vector3d Tangent;
        }

        private static double AddInterventionPatch(List<Brep> output, Point3d a, Point3d b,
            Point3d c, Point3d d)
        {
            if (a.DistanceTo(d) < 1e-6 && b.DistanceTo(c) < 1e-6) return 0;
            var ring = new PolylineCurve(new[] { a, b, c, d, a });
            var patches = Brep.CreatePlanarBreps(ring, 0.01);
            if (patches == null || patches.Length == 0 || patches[0] == null || !patches[0].IsValid)
                return 0;
            var mass = AreaMassProperties.Compute(patches[0]);
            if (mass == null) return 0;
            output.Add(patches[0]);
            return mass.Area;
        }

        private static List<StreetItem> ExtractStreetCurves(List<object> inputList, string nameField)
        {
            var result = new List<StreetItem>();
            if (inputList == null) return result;

            int count = 1;

            foreach (var item in inputList)
            {
                if (item == null) continue;

                object unwrapped = item;
                while (unwrapped is GH_ObjectWrapper wrapper) unwrapped = wrapper.Value;
                if (unwrapped == null) continue;

                // Caso 0: ProfiledStreet vindo do Street Profile Assignment
                if (unwrapped is ProfiledStreet ps)
                {
                    if (ps.SourceGeometry != null && ps.SourceGeometry.IsValid)
                    {
                        result.Add(new StreetItem
                        {
                            Curve = ps.SourceGeometry,
                            Name = ps.StreetName,
                            SourceId = ps.StreetID,
                            Profile = ps.StreetProfile
                        });
                    }
                    continue;
                }

                // Caso 0b: ShpFeature vindo de Features de ShpImport / GpkgImport
                if (unwrapped is ShpFeature sf)
                {
                    string sName = "";
                    string chosenField = FindStreetNameField(sf.Attributes?.Keys.ToList() ?? new List<string>(), nameField);
                    if (!string.IsNullOrEmpty(chosenField) && sf.Attributes != null && sf.Attributes.TryGetValue(chosenField, out var sv))
                    {
                        sName = sv?.ToString()?.Trim() ?? "";
                    }
                    if (string.IsNullOrWhiteSpace(sName)) sName = $"Rua_{count++}";

                    if (sf.Curves != null)
                    {
                        foreach (var c in sf.Curves)
                        {
                            if (c != null && c.IsValid)
                            {
                                result.Add(new StreetItem { Curve = c, Name = sName, SourceId = $"{Path.GetFileName(sf.SourcePath ?? "import")}:{sf.RecordNumber}" });
                            }
                        }
                    }
                    continue;
                }

                // Caso 1: Caminho de arquivo GIS (.shp ou .gpkg) via GisPathResolver
                if (GisPathResolver.TryResolveGisPath(unwrapped, out string resolvedPath, out _))
                {
                    if (resolvedPath.EndsWith(".shp", StringComparison.OrdinalIgnoreCase))
                    {
                        var feats = ShapefileReader.ReadShapefile(resolvedPath, out var fieldNames);
                        string chosenField = FindStreetNameField(fieldNames, nameField);

                        foreach (var f in feats)
                        {
                            string sName = "";
                            if (!string.IsNullOrEmpty(chosenField) && f.Attributes != null && f.Attributes.TryGetValue(chosenField, out var val))
                            {
                                sName = val?.ToString()?.Trim();
                            }
                            if (string.IsNullOrWhiteSpace(sName))
                            {
                                sName = $"Rua_{count++}";
                            }

                            if (f.Curves != null)
                            {
                                foreach (var c in f.Curves)
                                {
                                    if (c != null && c.IsValid)
                                    {
                                        result.Add(new StreetItem { Curve = c, Name = sName, SourceId = $"{Path.GetFileName(resolvedPath)}:{f.RecordNumber}" });
                                    }
                                }
                            }
                        }
                        continue;
                    }
                    else if (resolvedPath.EndsWith(".gpkg", StringComparison.OrdinalIgnoreCase))
                    {
                        var feats = GpkgReader.ReadGeoPackage(resolvedPath, null, out var fieldNames);
                        string chosenField = FindStreetNameField(fieldNames, nameField);

                        foreach (var f in feats)
                        {
                            string sName = "";
                            if (!string.IsNullOrEmpty(chosenField) && f.Attributes != null && f.Attributes.TryGetValue(chosenField, out var val))
                            {
                                sName = val?.ToString()?.Trim();
                            }
                            if (string.IsNullOrWhiteSpace(sName))
                            {
                                sName = $"Rua_{count++}";
                            }

                            if (f.Curves != null)
                            {
                                foreach (var c in f.Curves)
                                {
                                    if (c != null && c.IsValid)
                                    {
                                        result.Add(new StreetItem { Curve = c, Name = sName, SourceId = $"{Path.GetFileName(resolvedPath)}:{f.RecordNumber}" });
                                    }
                                }
                            }
                        }
                        continue;
                    }
                }

                // Caso 2: Curva direta do Grasshopper (GH_Curve, Curve, Line, Polyline)
                Curve directCrv = null;
                if (unwrapped is GH_Curve ghCrv) directCrv = ghCrv.Value;
                else if (unwrapped is Curve crv) directCrv = crv;
                else if (unwrapped is GH_Line ghL) directCrv = new LineCurve(ghL.Value);
                else if (unwrapped is Line l) directCrv = new LineCurve(l);
                else if (unwrapped is Polyline pl) directCrv = new PolylineCurve(pl);
                else if (unwrapped is IGH_GeometricGoo geoGoo)
                {
                    var geom = geoGoo.ScriptVariable();
                    if (geom is Curve gc) directCrv = gc;
                    else if (geom is Line gl) directCrv = new LineCurve(gl);
                    else if (geom is Polyline gp) directCrv = new PolylineCurve(gp);
                }

                if (directCrv != null && directCrv.IsValid)
                {
                    string curveName = directCrv.GetUserString("Name") ??
                                       directCrv.GetUserString("NOME") ??
                                       directCrv.GetUserString("RUA") ??
                                       directCrv.GetUserString("LOGRADOURO");
                    string sName = !string.IsNullOrWhiteSpace(curveName) ? curveName.Trim() : $"Rua_{count++}";
                    result.Add(new StreetItem { Curve = directCrv, Name = sName, SourceId = $"GH:{result.Count}" });
                }
            }

            return result;
        }

        private static string FindStreetNameField(List<string> fieldNames, string userPreference)
        {
            if (fieldNames == null || fieldNames.Count == 0) return null;

            if (!string.IsNullOrWhiteSpace(userPreference))
            {
                foreach (var f in fieldNames)
                {
                    if (f.Equals(userPreference, StringComparison.OrdinalIgnoreCase)) return f;
                }
            }

            // Busca automática por campos usuais em GIS no Brasil
            string[] commonNames = { "NOME", "NOME_LOG", "LOGRADOURO", "RUA", "NM_LOG", "DS_NOME", "NM_TITULO", "STREET", "NAME" };
            foreach (var cand in commonNames)
            {
                foreach (var f in fieldNames)
                {
                    if (f.IndexOf(cand, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return f;
                    }
                }
            }

            return fieldNames[0];
        }

        private static List<Curve> ExtractCurves(List<object> inputList)
        {
            var curves = new List<Curve>();
            if (inputList == null) return curves;

            foreach (var item in inputList)
            {
                if (item == null) continue;

                object unwrapped = item;
                while (unwrapped is GH_ObjectWrapper wrapper) unwrapped = wrapper.Value;
                if (unwrapped == null) continue;

                // Caminho GIS (.shp ou .gpkg) via GisPathResolver
                if (GisPathResolver.TryResolveGisPath(unwrapped, out string resolvedPath, out _))
                {
                    if (resolvedPath.EndsWith(".shp", StringComparison.OrdinalIgnoreCase))
                    {
                        var feats = ShapefileReader.ReadShapefile(resolvedPath, out _);
                        foreach (var f in feats)
                        {
                            if (f.Curves != null)
                            {
                                foreach (var c in f.Curves)
                                {
                                    if (c != null && c.IsValid) curves.Add(c);
                                }
                            }
                            if (f.Surface != null && f.Surface.IsValid)
                            {
                                curves.AddRange(f.Surface.GetWireframe(1));
                            }
                        }
                        continue;
                    }
                    else if (resolvedPath.EndsWith(".gpkg", StringComparison.OrdinalIgnoreCase))
                    {
                        var feats = GpkgReader.ReadGeoPackage(resolvedPath, null, out _);
                        foreach (var f in feats)
                        {
                            if (f.Curves != null)
                            {
                                foreach (var c in f.Curves)
                                {
                                    if (c != null && c.IsValid) curves.Add(c);
                                }
                            }
                            if (f.Surface != null && f.Surface.IsValid)
                            {
                                curves.AddRange(f.Surface.GetWireframe(1));
                            }
                        }
                        continue;
                    }
                }

                // ShpFeature (vindo de Features de ShpImport / GpkgImport)
                if (unwrapped is ShpFeature sf)
                {
                    if (sf.Curves != null)
                    {
                        foreach (var c in sf.Curves)
                        {
                            if (c != null && c.IsValid) curves.Add(c);
                        }
                    }
                    if (sf.Surface != null && sf.Surface.IsValid)
                    {
                        curves.AddRange(sf.Surface.GetWireframe(1));
                    }
                    continue;
                }

                // GH_Curve / Curve
                if (unwrapped is GH_Curve ghC)
                {
                    if (ghC.Value != null && ghC.Value.IsValid) curves.Add(ghC.Value);
                }
                else if (unwrapped is Curve c)
                {
                    if (c.IsValid) curves.Add(c);
                }
                else if (unwrapped is GH_Line ghL)
                {
                    curves.Add(new LineCurve(ghL.Value));
                }
                else if (unwrapped is Line l)
                {
                    curves.Add(new LineCurve(l));
                }
                else if (unwrapped is Polyline pl)
                {
                    curves.Add(new PolylineCurve(pl));
                }
                // Brep (Quadra como superfície)
                else if (unwrapped is GH_Brep ghB)
                {
                    if (ghB.Value != null && ghB.Value.IsValid)
                    {
                        curves.AddRange(ghB.Value.GetWireframe(1));
                    }
                }
                else if (unwrapped is Brep b)
                {
                    if (b.IsValid) curves.AddRange(b.GetWireframe(1));
                }
                else if (unwrapped is IGH_GeometricGoo geoGoo)
                {
                    var geom = geoGoo.ScriptVariable();
                    if (geom is Curve gc && gc.IsValid) curves.Add(gc);
                    else if (geom is Brep gb && gb.IsValid) curves.AddRange(gb.GetWireframe(1));
                    else if (geom is Line gl) curves.Add(new LineCurve(gl));
                    else if (geom is Polyline gp) curves.Add(new PolylineCurve(gp));
                }
            }

            return curves;
        }

        #endregion
    }
}
