using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;

namespace Buraqueira_Urb
{
    /// <summary>
    /// Componente principal da Pilha de Seção Transversal de Via Paramétrica a partir de CSV.
    /// Suporta:
    /// - Divisão em Árvore (DataTree) das camadas 3D e 2D:
    ///     {0} Calçada (Piso)
    ///     {1} Terreno sob Calçada
    ///     {2} Meio-fio (Curb)
    ///     {3} Pista / Asfalto (Pavimento)
    ///     {4} Base Granular
    ///     {5} Sub-base Granular
    ///     {6} Canteiro Central
    /// - Rótulos customizáveis (trocando 'Leito Carroçável' por 'Pista' ou nomes manuais).
    /// - Conexão perfeitamente estanque e retangular de meio-fio (sem vãos triangulares).
    /// - Nome da rua elevado e alinhado à esquerda com o nome da quadra.
    /// - Prancha de catálogo multi-vias em colunas.
    /// - Hachuras técnicas vetoriais (45° e cruzada de terra).
    /// - Varredura 3D sólida ao longo do alinhamento.
    /// </summary>
    public class RoadCrossSection_Component : GH_Component
    {
        public RoadCrossSection_Component()
            : base(
                "Road Cross Section (CSV)",
                "RoadSection",
                "Gera catálogo/prancha de seções transversais urbanas a partir de CSV com divisão de camadas em árvore ({0} Calçada, {1} Terreno, {2} Meio-fio, {3} Pista...), rótulos customizáveis, cotas, hachuras e sólidos 3D.",
                "Glaux Urb",
                "01 | Infraestrutura Viária")
        {
        }

        public override Guid ComponentGuid => new Guid("9a1b2c3d-4e5f-6a7b-8c9d-0e1f2a3b4c5d");

        protected override System.Drawing.Bitmap Icon => GlauxUrbIcons.RoadCrossSection;

        // Plano Padrão XZ (Front View: X horizontal transversal, Z vertical elevação)
        public static Plane DefaultPlaneXZ
        {
            get
            {
                var pln = Plane.WorldXY;
                pln.XAxis = new Vector3d(1, 0, 0);
                pln.YAxis = new Vector3d(0, 0, 1);
                pln.ZAxis = new Vector3d(0, -1, 0);
                return pln;
            }
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            // 0: CSV de Entrada
            pManager.AddGenericParameter("CSV / Table", "CSV", 
                "Tabela CSV contendo uma ou múltiplas vias urbanas. Pode ser caminho (.csv), texto multilinha do Panel ou lista. Colunas suportadas: Nome_Via, Quadra_Esq, Passeio_Esq, Faixa_Rolamento, Passeio_Dir, Quadra_Dir, Tipo.", 
                GH_ParamAccess.item);
            pManager[0].Optional = true;

            // 1: Mão Única vs Mão Dupla
            pManager.AddBooleanParameter("One Way", "1Way", 
                "Se True, configura caimento contínuo ou sentido único. Se False (padrão), mão dupla com coroamento no centro.", 
                GH_ParamAccess.item, false);
            pManager[1].Optional = true;

            // 2: Canteiro Central (Largura)
            pManager.AddNumberParameter("Median Width", "Median", 
                "Largura padrão do canteiro central entre pistas em metros (ex: 2.0m, 3.0m). Se 0, via contígua sem canteiro.", 
                GH_ParamAccess.item, 0.0);
            pManager[2].Optional = true;

            // 3: Eixo 3D da Via (Alinhamento Horizontal/Vertical)
            pManager.AddCurveParameter("Alignment Axis", "Axis", 
                "Curva 3D opcional (ou lista de curvas) representando o eixo da via. Gera modelo 3D sólido por varredura.", 
                GH_ParamAccess.item);
            pManager[3].Optional = true;

            // 4: Plano Base da Seção 2D
            pManager.AddPlaneParameter("Origin Plane", "Pln", 
                "Plano de inserção da prancha 2D. Padrão: Plano XZ (X = largura transversal horizontal, Z = elevação/altura vertical na vista Frontal).", 
                GH_ParamAccess.item, DefaultPlaneXZ);
            pManager[4].Optional = true;

            // 5: Bombeamento Transversal (Caimento da pista)
            pManager.AddNumberParameter("Default Bombeo", "Slope", 
                "Declividade transversal padrão da pista em porcentagem (Padrão: 2.0%).", 
                GH_ParamAccess.item, 2.0);
            pManager[5].Optional = true;

            // 6: Espessuras das Camadas Estruturais do Pavimento
            pManager.AddTextParameter("Layer Depths", "Layers", 
                "Espessuras [Asfalto, Base Granular, Sub-base Granular, Melhoramento] em metros (ex: '0.05, 0.15, 0.20, 0.30').", 
                GH_ParamAccess.item, "0.05, 0.15, 0.20, 0.30");
            pManager[6].Optional = true;

            // 7: Perfil de Meio-Fio e Sarjeta
            pManager.AddTextParameter("Curb Profile", "Curb", 
                "Dimensões [Largura Meio-Fio, Altura Espelho, Largura Sarjeta/Cuneta] (Padrão: '0.15, 0.15, 0.25').", 
                GH_ParamAccess.item, "0.15, 0.15, 0.25");
            pManager[7].Optional = true;

            // 8: Layout da Prancha (Para Múltiplas Vias)
            pManager.AddTextParameter("Layout Grid", "Grid", 
                "Configuração da prancha para múltiplas ruas [Colunas, EspaçamentoY, EspaçamentoX] (Padrão: '2, 6.0, 25.0').", 
                GH_ParamAccess.item, "2, 6.0, 25.0");
            pManager[8].Optional = true;

            // 9: Gerar Anotações Técnicas e Hachuras
            pManager.AddBooleanParameter("Draw Annotations", "Anno", 
                "Se True, gera os nomes das ruas, quadras adjacentes, linhas de divisa, cotas parciais/totais em vermelho e hachuras.", 
                GH_ParamAccess.item, true);
            pManager[9].Optional = true;

            // 10: Rótulos Customizados dos Elementos
            pManager.AddTextParameter("Custom Labels", "Labels", 
                "Rótulos customizados para os elementos da seção [Passeio, Pista, Canteiro] separados por vírgula (ex: 'Calçada, Pista, Canteiro' ou 'Passeio, Asfalto'). Padrão: 'Passeio, Pista, Canteiro'.", 
                GH_ParamAccess.item, "Passeio, Pista, Canteiro");
            pManager[10].Optional = true;

            // 11: A REGRA DE OURO: O GATILHO RUN É SEMPRE O ÚLTIMO PARÂMETRO
            pManager.AddPointParameter("Run Section Points", "RunPts",
                "Saída RunPts de Sidewalk Regularization; seções {rua;run;estaca} para geometria longitudinal adaptativa.", GH_ParamAccess.tree);
            pManager[11].Optional = true;
            pManager.AddTextParameter("Adapted Profiles", "Adapted",
                "Saída Adapted de Street Profile Definition, por {rua;estaca}.", GH_ParamAccess.tree);
            pManager[12].Optional = true;
            pManager.AddBooleanParameter("Run", "Run", 
                "Gatilho booleano para executar o cálculo e modelagem da seção viária; última entrada.", 
                GH_ParamAccess.item, true);
            pManager[11].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddCurveParameter("Section Curves", "Crv", "Curvas 2D de contorno de todas as partes, calçadas, terreno sob passeio, meio-fio e camadas viárias.", GH_ParamAccess.list);
            
            pManager.AddBrepParameter("Section Surfaces", "Srf", 
                "Superfícies 2D fechadas organizadas em árvore por camada:\n{0} Calçada (Piso)\n{1} Terreno sob Calçada\n{2} Meio-fio (Curb)\n{3} Pista / Asfalto\n{4} Base Granular\n{5} Sub-base Granular\n{6} Canteiro Central", 
                GH_ParamAccess.tree);

            pManager.AddBrepParameter("Road 3D", "3D", 
                "Sólidos 3D por varredura organizados em árvore por camada:\n{0} Calçada (Piso)\n{1} Terreno sob Calçada\n{2} Meio-fio (Curb)\n{3} Pista / Asfalto\n{4} Base Granular\n{5} Sub-base Granular\n{6} Canteiro Central", 
                GH_ParamAccess.tree);

            pManager.AddGeometryParameter("Annotations", "Anno", "Geometrias das cotas em vermelho, nomes das ruas, quadras, divisas tracejadas e hachuras.", GH_ParamAccess.list);
            pManager.AddPointParameter("Key Points", "Pts", "Pontos geométricos notáveis de cada via (Eixo, Sarjetas, Meios-fios, Calçadas e Quadras).", GH_ParamAccess.list);
            pManager.AddTextParameter("Quantities Report", "Info", "Relatório quantitativo detalhado com larguras totais, áreas de piso, terreno e volumes por metro linear.", GH_ParamAccess.item);
            pManager.AddCurveParameter("Hatch Curves", "Hatch", "Linhas de hachura vetorial técnica para calçadas (45°), terreno sob calçadas (cruzado), base granular (-45°) e pavimento.", GH_ParamAccess.list);
            pManager.AddBrepParameter("Adaptive Road Elements", "AdaptSrf", "Superfícies semânticas de pista, faixas, canteiro ou área pedonal por {rua;run;elemento}.", GH_ParamAccess.tree);
            pManager.AddTextParameter("Adaptive Element Labels", "AdaptLabels", "Nome e tipo de cada superfície por {rua;run;elemento}.", GH_ParamAccess.tree);
            pManager.AddTextParameter("Adaptive Report", "AdaptInfo", "Contagens e conflitos da geração longitudinal adaptativa.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // Verificação da Regra de Ouro: o parâmetro 'Run' é o último da lista
            int runIndex = Params.Input.Count - 1;
            bool run = true;
            DA.GetData(runIndex, ref run);

            if (!run)
            {
                Message = "Pausado (Run=False)";
                return;
            }

            // 1. Obter Parâmetros
            object csvObj = null;
            DA.GetData(0, ref csvObj);

            bool globalOneWay = false;
            DA.GetData(1, ref globalOneWay);

            double globalMedian = 0.0;
            DA.GetData(2, ref globalMedian);
            if (globalMedian < 0) globalMedian = 0;

            Curve axisCurve = null;
            DA.GetData(3, ref axisCurve);
            GH_Structure<GH_Point> runSections;
            GH_Structure<GH_String> adaptedProfiles;
            DA.GetDataTree(11, out runSections);
            DA.GetDataTree(12, out adaptedProfiles);
            bool adaptiveMode = Params.Input[11].SourceCount > 0 || Params.Input[12].SourceCount > 0;
            bool adaptiveReady = runSections != null && runSections.DataCount > 0 &&
                adaptedProfiles != null && adaptedProfiles.DataCount > 0;

            Plane basePlane = DefaultPlaneXZ;
            DA.GetData(4, ref basePlane);

            double defaultBombeo = 2.0;
            DA.GetData(5, ref defaultBombeo);

            string layersStr = "0.05, 0.15, 0.20, 0.30";
            DA.GetData(6, ref layersStr);

            string curbStr = "0.15, 0.15, 0.25";
            DA.GetData(7, ref curbStr);

            string gridStr = "2, 6.0, 25.0";
            DA.GetData(8, ref gridStr);

            bool drawAnno = true;
            DA.GetData(9, ref drawAnno);

            string labelsStr = "Passeio, Pista, Canteiro";
            DA.GetData(10, ref labelsStr);

            // 2. Parse de configurações
            double tAsphalt = 0.05, tBase = 0.15, tSubbase = 0.20, tMej = 0.30;
            ParseLayerDepths(layersStr, ref tAsphalt, ref tBase, ref tSubbase, ref tMej);

            double curbWidth = 0.15, curbHeight = 0.15, gutterWidth = 0.25;
            ParseCurbProfile(curbStr, ref curbWidth, ref curbHeight, ref gutterWidth);

            int numColumns = 2;
            double spacingY = 6.0;
            double spacingX = 25.0;
            ParseLayoutGrid(gridStr, ref numColumns, ref spacingY, ref spacingX);

            string labelSw = "PASSEIO", labelRoad = "PISTA", labelMedian = "CANTEIRO";
            ParseCustomLabels(labelsStr, ref labelSw, ref labelRoad, ref labelMedian);

            // 3. Parse das Ruas da Tabela CSV
            var roadDefs = ParseRoadCatalogue(csvObj, globalMedian, gutterWidth, defaultBombeo, globalOneWay, curbWidth);

            if (roadDefs == null || roadDefs.Count == 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Nenhuma via válida identificada no CSV.");
                Message = "Erro CSV";
                return;
            }

            // 4. Construção da Prancha de Vias e Árvores de Camadas
            var allCurves = new List<Curve>();
            var allAnnotations = new List<IGH_GeometricGoo>();
            var allKeyPoints = new List<Point3d>();
            var allHatchCurves = new List<Curve>();
            var srfTree = new GH_Structure<GH_Brep>();
            var sweepTree = new GH_Structure<GH_Brep>();
            var sbReport = new StringBuilder();

            sbReport.AppendLine("=== BURAQUEIRA URB: CATÁLOGO DE SEÇÕES TRANSVERSAIS ===");
            sbReport.AppendLine($"Total de Vias Processadas: {roadDefs.Count}");
            sbReport.AppendLine($"Divisão em Árvore: {{0}} Calçada | {{1}} Terreno | {{2}} Meio-fio | {{3}} Pista | {{4}} Base | {{5}} Sub-base | {{6}} Canteiro");
            sbReport.AppendLine($"Disposição em Prancha: {numColumns} Colunas | Espaçamento Y = {spacingY:F1}m | Espaçamento X = {spacingX:F1}m\n");

            for (int k = 0; k < roadDefs.Count; k++)
            {
                var road = roadDefs[k];
                if (!road.IsPedestrian && road.MedianWidth > 0 &&
                    (road.IsOneWay || road.MedianWidth >= road.RoadwayWidth))
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                        $"PROFILE_CONFLICT em {road.StreetName}: canteiro exige mão dupla e largura menor que a caixa viária.");
                    sbReport.AppendLine($"[{k + 1}] {road.StreetName}: PROFILE_CONFLICT (canteiro incompatível)");
                    continue;
                }

                int col = k % numColumns;
                int row = k / numColumns;
                double shiftX = col * spacingX;
                double shiftY = -row * spacingY;

                Plane streetPlane = new Plane(
                    basePlane.Origin + (basePlane.XAxis * shiftX) + (basePlane.YAxis * shiftY),
                    basePlane.XAxis,
                    basePlane.YAxis
                );

                var sec = BuildSingleRoadSection(
                    road,
                    streetPlane,
                    road.Bombeo,
                    road.IsOneWay,
                    road.MedianWidth,
                    tAsphalt,
                    tBase,
                    tSubbase,
                    tMej,
                    curbWidth,
                    curbHeight,
                    gutterWidth,
                    drawAnno,
                    labelSw,
                    labelRoad,
                    labelMedian
                );

                allCurves.AddRange(sec.AllCurves);
                allAnnotations.AddRange(sec.AnnotationGeometry);
                allKeyPoints.AddRange(sec.KeyPoints);
                allHatchCurves.AddRange(sec.HatchCurves);

                // Popular a Árvore de Superfícies 2D por Camadas
                GH_Path PathFor(int layerIdx) => roadDefs.Count == 1 ? new GH_Path(layerIdx) : new GH_Path(k, layerIdx);

                AppendBrepsToTree(srfTree, PathFor(0), sec.SrfSidewalk);
                AppendBrepsToTree(srfTree, PathFor(1), sec.SrfTerrain);
                AppendBrepsToTree(srfTree, PathFor(2), sec.SrfCurb);
                AppendBrepsToTree(srfTree, PathFor(3), sec.SrfRoadway);
                AppendBrepsToTree(srfTree, PathFor(4), sec.SrfBase);
                AppendBrepsToTree(srfTree, PathFor(5), sec.SrfSubbase);
                AppendBrepsToTree(srfTree, PathFor(6), sec.SrfMedian);

                // Varredura 3D sólida ao longo do Axis (se conectado) também em árvore por camada!
                if (axisCurve != null && axisCurve.IsValid && !adaptiveMode)
                {
                    AppendBrepsToTree(sweepTree, PathFor(0), BuildRoad3DSweep(sec.CrvSidewalk, axisCurve));
                    AppendBrepsToTree(sweepTree, PathFor(1), BuildRoad3DSweep(sec.CrvTerrain, axisCurve));
                    AppendBrepsToTree(sweepTree, PathFor(2), BuildRoad3DSweep(sec.CrvCurb, axisCurve));
                    AppendBrepsToTree(sweepTree, PathFor(3), BuildRoad3DSweep(sec.CrvRoadway, axisCurve));
                    AppendBrepsToTree(sweepTree, PathFor(4), BuildRoad3DSweep(sec.CrvBase, axisCurve));
                    AppendBrepsToTree(sweepTree, PathFor(5), BuildRoad3DSweep(sec.CrvSubbase, axisCurve));
                    AppendBrepsToTree(sweepTree, PathFor(6), BuildRoad3DSweep(sec.CrvMedian, axisCurve));
                }

                sbReport.AppendLine($"[{k + 1}] {road.StreetName}: Largura Total = {sec.TotalWidth:F2} m");
                if (!string.IsNullOrEmpty(road.QuadraLeft) || !string.IsNullOrEmpty(road.QuadraRight))
                {
                    sbReport.AppendLine($"    Divisas de Quadra: {road.QuadraLeft} <---> {road.QuadraRight}");
                }
                sbReport.AppendLine($"    Tipo: {(road.IsPedestrian ? "Via Pedonal (Calçadão)" : road.IsOneWay ? "Mão Única" : "Mão Dupla")}");
                sbReport.AppendLine($"    Piso Calçada (0.10m): {sec.AreaSidewalkSlab:F2} m²/m");
                sbReport.AppendLine($"    Terreno sob Calçada: {sec.VolumeSidewalkGround:F2} m³/m");
                sbReport.AppendLine($"    Extensão de Meio-Fio: {sec.LengthCurbs:F1} m/m");
                sbReport.AppendLine($"    Capa Asfáltica: {sec.AreaAsphalt:F2} m²/m");
                sbReport.AppendLine($"    Base Granular: {sec.VolumeBase:F2} m³/m\n");
            }

            // 5. Atribuir Saídas
            DA.SetDataList(0, allCurves);
            DA.SetDataTree(1, srfTree);
            DA.SetDataTree(2, sweepTree);
            DA.SetDataList(3, allAnnotations);
            DA.SetDataList(4, allKeyPoints);
            DA.SetData(5, sbReport.ToString());
            DA.SetDataList(6, allHatchCurves);
            if (adaptiveMode)
            {
                if (adaptiveReady)
                {
                    BuildAdaptiveRoad(runSections, adaptedProfiles, out var adaptiveSurfaces,
                        out var adaptiveLabels, out var adaptiveReport);
                    DA.SetDataTree(7, adaptiveSurfaces);
                    DA.SetDataTree(8, adaptiveLabels);
                    DA.SetData(9, adaptiveReport);
                }
                else
                {
                    DA.SetDataTree(7, new GH_Structure<GH_Brep>());
                    DA.SetDataTree(8, new GH_Structure<GH_String>());
                    DA.SetData(9, "PROFILE_CONFLICT: adaptive inputs connected but no fitted sections; no road geometry generated.");
                }
            }

            Message = adaptiveMode ? "Perfil adaptativo" : $"{roadDefs.Count} Vias | Árvore de Camadas";
        }

        private static void AppendBrepsToTree(GH_Structure<GH_Brep> tree, GH_Path path, List<Brep> breps)
        {
            if (breps == null || breps.Count == 0) return;
            foreach (var b in breps)
            {
                if (b != null && b.IsValid)
                {
                    tree.Append(new GH_Brep(b), path);
                }
            }
        }

        #region Classes de Domínio Urbano

        public enum RoadElementType
        {
            Sidewalk,
            Gutter,
            Curb,
            Roadway,
            Median,
            Pedestrian
        }

        public class RoadPart
        {
            public string Name { get; set; }
            public RoadElementType Type { get; set; }
            public double Width { get; set; }
            public bool IsLeftSide { get; set; }
        }

        public class RoadDefinition
        {
            public string StreetName { get; set; } = "Rua";
            public string QuadraLeft { get; set; } = "";
            public string QuadraRight { get; set; } = "";
            public bool IsPedestrian { get; set; } = false;
            public bool IsOneWay { get; set; } = false;
            public double Bombeo { get; set; } = 2.0;
            public double MedianWidth { get; set; } = 0.0;
            public double SidewalkLeftWidth { get; set; } = 0.0;
            public double RoadwayWidth { get; set; } = 7.0;
            public double SidewalkRightWidth { get; set; } = 0.0;
            public List<RoadPart> Parts { get; set; } = new List<RoadPart>();
        }

        public class SectionBuildResult
        {
            public List<Curve> AllCurves { get; set; } = new List<Curve>();
            public List<IGH_GeometricGoo> AnnotationGeometry { get; set; } = new List<IGH_GeometricGoo>();
            public List<Curve> HatchCurves { get; set; } = new List<Curve>();
            public List<Point3d> KeyPoints { get; set; } = new List<Point3d>();
            public double TotalWidth { get; set; }
            public double AreaSidewalkSlab { get; set; }
            public double VolumeSidewalkGround { get; set; }
            public double LengthCurbs { get; set; }
            public double AreaAsphalt { get; set; }
            public double VolumeBase { get; set; }

            // Breps 2D separados por camada:
            public List<Brep> SrfSidewalk { get; set; } = new List<Brep>();
            public List<Brep> SrfTerrain { get; set; } = new List<Brep>();
            public List<Brep> SrfCurb { get; set; } = new List<Brep>();
            public List<Brep> SrfRoadway { get; set; } = new List<Brep>();
            public List<Brep> SrfBase { get; set; } = new List<Brep>();
            public List<Brep> SrfSubbase { get; set; } = new List<Brep>();
            public List<Brep> SrfMedian { get; set; } = new List<Brep>();

            // Curvas de contorno fechadas para varredura 3D:
            public List<Curve> CrvSidewalk { get; set; } = new List<Curve>();
            public List<Curve> CrvTerrain { get; set; } = new List<Curve>();
            public List<Curve> CrvCurb { get; set; } = new List<Curve>();
            public List<Curve> CrvRoadway { get; set; } = new List<Curve>();
            public List<Curve> CrvBase { get; set; } = new List<Curve>();
            public List<Curve> CrvSubbase { get; set; } = new List<Curve>();
            public List<Curve> CrvMedian { get; set; } = new List<Curve>();
        }

        #endregion

        #region Parsing e Tratamento de Dados

        private static void ParseCustomLabels(string s, ref string sw, ref string road, ref string median)
        {
            if (string.IsNullOrWhiteSpace(s)) return;
            var tokens = s.Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries).Select(t => t.Trim()).ToList();
            if (tokens.Count > 0 && !string.IsNullOrWhiteSpace(tokens[0])) sw = tokens[0].ToUpperInvariant();
            if (tokens.Count > 1 && !string.IsNullOrWhiteSpace(tokens[1])) road = tokens[1].ToUpperInvariant();
            if (tokens.Count > 2 && !string.IsNullOrWhiteSpace(tokens[2])) median = tokens[2].ToUpperInvariant();
        }

        private static void ParseLayerDepths(string s, ref double tAsf, ref double tBase, ref double tSub, ref double tMej)
        {
            if (string.IsNullOrWhiteSpace(s)) return;
            var tokens = s.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length > 0 && double.TryParse(tokens[0], NumberStyles.Any, CultureInfo.InvariantCulture, out double v0)) tAsf = Math.Max(0.01, v0);
            if (tokens.Length > 1 && double.TryParse(tokens[1], NumberStyles.Any, CultureInfo.InvariantCulture, out double v1)) tBase = Math.Max(0.01, v1);
            if (tokens.Length > 2 && double.TryParse(tokens[2], NumberStyles.Any, CultureInfo.InvariantCulture, out double v2)) tSub = Math.Max(0.01, v2);
            if (tokens.Length > 3 && double.TryParse(tokens[3], NumberStyles.Any, CultureInfo.InvariantCulture, out double v3)) tMej = Math.Max(0.01, v3);
        }

        private static void ParseCurbProfile(string s, ref double wCurb, ref double hCurb, ref double wGutter)
        {
            if (string.IsNullOrWhiteSpace(s)) return;
            var tokens = s.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length > 0 && double.TryParse(tokens[0], NumberStyles.Any, CultureInfo.InvariantCulture, out double v0)) wCurb = Math.Max(0.05, v0);
            if (tokens.Length > 1 && double.TryParse(tokens[1], NumberStyles.Any, CultureInfo.InvariantCulture, out double v1)) hCurb = Math.Max(0.05, v1);
            if (tokens.Length > 2 && double.TryParse(tokens[2], NumberStyles.Any, CultureInfo.InvariantCulture, out double v2)) wGutter = Math.Max(0.10, v2);
        }

        private static void ParseLayoutGrid(string s, ref int cols, ref double spY, ref double spX)
        {
            if (string.IsNullOrWhiteSpace(s)) return;
            var tokens = s.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length > 0 && int.TryParse(tokens[0], out int c)) cols = Math.Max(1, c);
            if (tokens.Length > 1 && double.TryParse(tokens[1], NumberStyles.Any, CultureInfo.InvariantCulture, out double sy)) spY = Math.Max(2.0, sy);
            if (tokens.Length > 2 && double.TryParse(tokens[2], NumberStyles.Any, CultureInfo.InvariantCulture, out double sx)) spX = Math.Max(10.0, sx);
        }

        private List<RoadDefinition> ParseRoadCatalogue(object csvObj, double globalMedian, double gutterWidth, double defaultBombeo, bool globalOneWay, double curbWidth)
        {
            var rawText = ExtractTextFromInput(csvObj);

            if (string.IsNullOrWhiteSpace(rawText))
            {
                return new List<RoadDefinition> { BuildDefaultReferenceRoad(gutterWidth, defaultBombeo, globalOneWay, globalMedian) };
            }

            var lines = rawText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                               .Select(l => l.Trim())
                               .Where(l => !string.IsNullOrWhiteSpace(l) && !l.StartsWith("#") && !l.StartsWith("//"))
                               .ToList();

            if (lines.Count == 0)
                return new List<RoadDefinition> { BuildDefaultReferenceRoad(gutterWidth, defaultBombeo, globalOneWay, globalMedian) };

            char delim = DetectDelimiter(lines[0]);
            var headerCols = lines[0].Split(delim).Select(c => c.Trim()).ToList();

            int streetNameCol = headerCols.FindIndex(h => h.Equals("Nome_Via", StringComparison.OrdinalIgnoreCase) ||
                                                          h.Equals("Rua", StringComparison.OrdinalIgnoreCase) ||
                                                          h.Equals("Via", StringComparison.OrdinalIgnoreCase) ||
                                                          h.Equals("Street", StringComparison.OrdinalIgnoreCase) ||
                                                          h.Equals("Nome", StringComparison.OrdinalIgnoreCase));

            if (streetNameCol >= 0 && lines.Count > 1)
            {
                int quadraEsqCol = headerCols.FindIndex(h => h.StartsWith("Quadra_Esq", StringComparison.OrdinalIgnoreCase) || h.Equals("Quadra1", StringComparison.OrdinalIgnoreCase));
                int quadraDirCol = headerCols.FindIndex(h => h.StartsWith("Quadra_Dir", StringComparison.OrdinalIgnoreCase) || h.Equals("Quadra2", StringComparison.OrdinalIgnoreCase));
                int passeioEsqCol = headerCols.FindIndex(h => h.Contains("Passeio_Esq") || h.Contains("Calcada_Esq") || h.Contains("Acera_Esq"));
                int faixaCol = headerCols.FindIndex(h => h.Contains("Faixa") || h.Contains("Pista") || h.Contains("Roadway") || h.Contains("Carretera"));
                int passeioDirCol = headerCols.FindIndex(h => h.Contains("Passeio_Dir") || h.Contains("Calcada_Dir") || h.Contains("Acera_Dir"));
                int tipoCol = headerCols.FindIndex(h => h.Equals("Tipo", StringComparison.OrdinalIgnoreCase) || h.Equals("Type", StringComparison.OrdinalIgnoreCase));
                int canteiroCol = headerCols.FindIndex(h => h.Contains("Canteiro") || h.Contains("Median"));

                var catalogue = new List<RoadDefinition>();

                for (int i = 1; i < lines.Count; i++)
                {
                    var cols = lines[i].Split(delim).Select(c => c.Trim()).ToList();
                    if (cols.Count == 0 || string.IsNullOrWhiteSpace(cols[0])) continue;

                    string streetName = cols[streetNameCol];
                    string qLeft = quadraEsqCol >= 0 && quadraEsqCol < cols.Count ? cols[quadraEsqCol] : "";
                    string qRight = quadraDirCol >= 0 && quadraDirCol < cols.Count ? cols[quadraDirCol] : "";
                    string tipoStr = tipoCol >= 0 && tipoCol < cols.Count ? cols[tipoCol] : "";

                    bool isPed = tipoStr.IndexOf("pedonal", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                 tipoStr.IndexOf("pedestre", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                 streetName.IndexOf("pedonal", StringComparison.OrdinalIgnoreCase) >= 0;

                    bool is1Way = globalOneWay || tipoStr.IndexOf("unica", StringComparison.OrdinalIgnoreCase) >= 0 || tipoStr.IndexOf("única", StringComparison.OrdinalIgnoreCase) >= 0;

                    double wPasseioEsq = 3.00;
                    if (passeioEsqCol >= 0 && passeioEsqCol < cols.Count)
                    {
                        double.TryParse(cols[passeioEsqCol], NumberStyles.Any, CultureInfo.InvariantCulture, out wPasseioEsq);
                    }

                    double wFaixa = 9.00;
                    if (faixaCol >= 0 && faixaCol < cols.Count)
                    {
                        double.TryParse(cols[faixaCol], NumberStyles.Any, CultureInfo.InvariantCulture, out wFaixa);
                    }

                    double wPasseioDir = 3.00;
                    if (passeioDirCol >= 0 && passeioDirCol < cols.Count)
                    {
                        double.TryParse(cols[passeioDirCol], NumberStyles.Any, CultureInfo.InvariantCulture, out wPasseioDir);
                    }

                    double wMedian = globalMedian;
                    if (canteiroCol >= 0 && canteiroCol < cols.Count)
                    {
                        double.TryParse(cols[canteiroCol], NumberStyles.Any, CultureInfo.InvariantCulture, out wMedian);
                    }

                    var roadDef = new RoadDefinition
                    {
                        StreetName = streetName,
                        QuadraLeft = qLeft,
                        QuadraRight = qRight,
                        IsPedestrian = isPed,
                        IsOneWay = is1Way,
                        Bombeo = defaultBombeo,
                        MedianWidth = wMedian,
                        SidewalkLeftWidth = wPasseioEsq,
                        RoadwayWidth = wFaixa,
                        SidewalkRightWidth = wPasseioDir
                    };

                    catalogue.Add(roadDef);
                }

                return catalogue;
            }
            else
            {
                var singleRoad = new RoadDefinition
                {
                    StreetName = "Seção Transversal",
                    IsOneWay = globalOneWay,
                    MedianWidth = globalMedian,
                    Bombeo = defaultBombeo
                };

                List<string> values = lines.Count > 1 ? lines[1].Split(delim).Select(c => c.Trim()).ToList()
                                                      : lines[0].Split(delim).Select(c => c.Trim()).ToList();

                double sumSwL = 0, sumRoad = 0, sumSwR = 0;
                bool passedCenter = false;

                for (int i = 0; i < headerCols.Count; i++)
                {
                    string colName = headerCols[i];
                    double w = 2.0;
                    if (values != null && i < values.Count)
                    {
                        if (!double.TryParse(values[i], NumberStyles.Any, CultureInfo.InvariantCulture, out w))
                        {
                            double.TryParse(colName, NumberStyles.Any, CultureInfo.InvariantCulture, out w);
                        }
                    }
                    else
                    {
                        double.TryParse(colName, NumberStyles.Any, CultureInfo.InvariantCulture, out w);
                    }

                    if (w <= 0.001) continue;
                    var type = DetectElementType(colName, colName);

                    if (type == RoadElementType.Sidewalk)
                    {
                        if (!passedCenter) sumSwL += w;
                        else sumSwR += w;
                    }
                    else if (type == RoadElementType.Roadway || type == RoadElementType.Gutter || type == RoadElementType.Median)
                    {
                        sumRoad += w;
                        passedCenter = true;
                    }

                    singleRoad.Parts.Add(new RoadPart { Name = colName, Type = type, Width = w });
                }

                singleRoad.SidewalkLeftWidth = sumSwL > 0 ? sumSwL : 3.00;
                singleRoad.RoadwayWidth = sumRoad > 0 ? sumRoad : 9.00;
                singleRoad.SidewalkRightWidth = sumSwR > 0 ? sumSwR : 3.00;

                return new List<RoadDefinition> { singleRoad };
            }
        }

        private static RoadDefinition BuildDefaultReferenceRoad(double gutterWidth, double defaultBombeo, bool oneWay, double medianWidth)
        {
            return new RoadDefinition
            {
                StreetName = "Via Local 15.00m",
                QuadraLeft = "Quadra 21",
                QuadraRight = "Quadra 21",
                Bombeo = defaultBombeo,
                IsOneWay = oneWay,
                MedianWidth = medianWidth,
                SidewalkLeftWidth = 3.00,
                RoadwayWidth = 9.00,
                SidewalkRightWidth = 3.00
            };
        }

        private static RoadElementType DetectElementType(string typeStr, string name)
        {
            string s = (typeStr + " " + name).ToLowerInvariant();
            if (s.Contains("pedonal") || s.Contains("pedestre") || s.Contains("calcadao") || s.Contains("calçadão")) return RoadElementType.Pedestrian;
            if (s.Contains("acera") || s.Contains("calcada") || s.Contains("calçada") || s.Contains("sidewalk") || s.Contains("passeio")) return RoadElementType.Sidewalk;
            if (s.Contains("cuneta") || s.Contains("sarjeta") || s.Contains("gutter") || s.Contains("valeta")) return RoadElementType.Gutter;
            if (s.Contains("canteiro") || s.Contains("median") || s.Contains("separador")) return RoadElementType.Median;
            return RoadElementType.Roadway;
        }

        private static string ExtractTextFromInput(object obj)
        {
            if (obj == null) return null;
            if (obj is GH_String ghStr) return ghStr.Value;
            if (obj is string s)
            {
                if (File.Exists(s))
                {
                    try { return File.ReadAllText(s, Encoding.UTF8); }
                    catch { }
                }
                return s;
            }
            return obj.ToString();
        }

        private static char DetectDelimiter(string headerLine)
        {
            int commas = headerLine.Count(c => c == ',');
            int semicolons = headerLine.Count(c => c == ';');
            int tabs = headerLine.Count(c => c == '\t');
            if (semicolons > commas && semicolons >= tabs) return ';';
            if (tabs > commas && tabs >= semicolons) return '\t';
            return ',';
        }

        #endregion

        #region Construção Geométrica Avançada da Seção Transversal

        private SectionBuildResult BuildSingleRoadSection(
            RoadDefinition road,
            Plane pln,
            double bombeo,
            bool oneWay,
            double medianW,
            double tAsf,
            double tBase,
            double tSub,
            double tMej,
            double curbW,
            double curbH,
            double gutterW,
            bool drawAnno,
            string labelSw,
            string labelRoad,
            string labelMedian)
        {
            var res = new SectionBuildResult();

            if (road.IsPedestrian)
            {
                double width = road.RoadwayWidth;
                if (width <= 0) return res;
                const double pedestrianSlab = 0.10;
                var deck = PolyToCurve(pln, new List<Point2d>
                {
                    new Point2d(-width / 2, 0), new Point2d(width / 2, 0),
                    new Point2d(width / 2, -pedestrianSlab),
                    new Point2d(-width / 2, -pedestrianSlab), new Point2d(-width / 2, 0)
                });
                res.AllCurves.Add(deck);
                res.CrvSidewalk.Add(deck);
                AddBreps(res.SrfSidewalk, deck);
                res.TotalWidth = width;
                res.AreaSidewalkSlab = width * pedestrianSlab;
                res.KeyPoints.Add(LocalToWorld(pln, -width / 2, 0));
                res.KeyPoints.Add(LocalToWorld(pln, width / 2, 0));
                return res;
            }

            double wSwL = Math.Max(0.0, road.SidewalkLeftWidth);
            double wRoad = Math.Max(0.0, road.RoadwayWidth);
            if (wRoad <= 0) return res;
            double wSwR = Math.Max(0.0, road.SidewalkRightWidth);
            bool hasSwL = wSwL > 0.05;
            bool hasSwR = wSwR > 0.05;

            double totalWidth = (hasSwL ? wSwL : 0.0) + wRoad + (hasSwR ? wSwR : 0.0);
            res.TotalWidth = totalWidth;

            double centerOffset = (hasSwL ? wSwL : 0.0) + (wRoad / 2.0);
            double bombeoRatio = bombeo / 100.0;

            // Coordenadas U no plano da seção
            double uQuadraL = -centerOffset;
            double uRoadL = -wRoad / 2.0;
            double uRoadR = wRoad / 2.0;
            double uQuadraR = totalWidth - centerOffset;

            // Níveis verticais fundamentais
            double crownV = 0.0;
            double gutterL_V = -Math.Abs(uRoadL) * bombeoRatio - 0.02;
            double gutterR_V = -Math.Abs(uRoadR) * bombeoRatio - 0.02;

            if (oneWay)
            {
                gutterL_V = 0.0;
                crownV = -(wRoad / 2.0) * bombeoRatio;
                gutterR_V = -wRoad * bombeoRatio - 0.02;
            }

            double curbTopL_V = gutterL_V + curbH;
            double curbTopR_V = gutterR_V + curbH;

            // Calçada sobe 1.5% em direção ao alinhamento predial
            double swSlabThickness = 0.10;
            double swSlope = 0.015;

            double uCurbL_Back = hasSwL ? (uRoadL - curbW) : uRoadL;
            double uCurbR_Back = hasSwR ? (uRoadR + curbW) : uRoadR;

            double swOuterL_V = curbTopL_V + Math.Max(0, (uCurbL_Back - uQuadraL)) * swSlope;
            double swOuterR_V = curbTopR_V + Math.Max(0, (uQuadraR - uCurbR_Back)) * swSlope;

            // Nível inferior contínuo da base da via (Subleito)
            double roadTotalDepth = tAsf + tBase + tSub;
            double roadBaseBottomLevel = Math.Min(gutterL_V, gutterR_V) - roadTotalDepth;

            // =========================================================================
            // 1. PISTA DE ROLAMENTO E CAMADAS ESTRUTURAIS
            // =========================================================================
            var roadTopPtsLocal = new List<Point2d>();
            int nRoadSegs = 16;
            for (int s = 0; s <= nRoadSegs; s++)
            {
                double frac = (double)s / nRoadSegs;
                double u = uRoadL + frac * (uRoadR - uRoadL);
                double v = oneWay ? -(u - uRoadL) * bombeoRatio : -Math.Abs(u) * bombeoRatio;
                roadTopPtsLocal.Add(new Point2d(u, v));
            }

            // Polígono 1.1: Capa Asfáltica (CBUQ) -> Camada 3
            var polyAsfLocal = new List<Point2d>();
            polyAsfLocal.AddRange(roadTopPtsLocal);
            for (int s = roadTopPtsLocal.Count - 1; s >= 0; s--)
            {
                polyAsfLocal.Add(new Point2d(roadTopPtsLocal[s].X, roadTopPtsLocal[s].Y - tAsf));
            }
            var crvAsf = PolyToCurve(pln, polyAsfLocal);
            res.AllCurves.Add(crvAsf);
            res.CrvRoadway.Add(crvAsf);
            AddBreps(res.SrfRoadway, crvAsf);
            res.AreaAsphalt = wRoad * tAsf;

            // Canteiro físico separado do meio-fio externo, sobre a base da pista.
            if (!oneWay && medianW > 0 && medianW < wRoad)
            {
                double halfMedian = medianW / 2.0;
                double medianTop = curbH;
                var medianCurve = PolyToCurve(pln, new List<Point2d>
                {
                    new Point2d(-halfMedian, 0), new Point2d(-halfMedian, medianTop),
                    new Point2d(halfMedian, medianTop), new Point2d(halfMedian, 0),
                    new Point2d(-halfMedian, 0)
                });
                res.AllCurves.Add(medianCurve);
                res.CrvMedian.Add(medianCurve);
                AddBreps(res.SrfMedian, medianCurve);
            }

            // Polígono 1.2: Base Granular -> Camada 4
            var polyBaseLocal = new List<Point2d>();
            for (int s = 0; s < roadTopPtsLocal.Count; s++)
            {
                polyBaseLocal.Add(new Point2d(roadTopPtsLocal[s].X, roadTopPtsLocal[s].Y - tAsf));
            }
            for (int s = roadTopPtsLocal.Count - 1; s >= 0; s--)
            {
                polyBaseLocal.Add(new Point2d(roadTopPtsLocal[s].X, roadTopPtsLocal[s].Y - tAsf - tBase));
            }
            var crvBase = PolyToCurve(pln, polyBaseLocal);
            res.AllCurves.Add(crvBase);
            res.CrvBase.Add(crvBase);
            AddBreps(res.SrfBase, crvBase);
            res.VolumeBase = wRoad * tBase;

            // Polígono 1.3: Sub-base Granular -> Camada 5
            if (tSub > 0.01)
            {
                var polySubLocal = new List<Point2d>();
                for (int s = 0; s < roadTopPtsLocal.Count; s++)
                {
                    polySubLocal.Add(new Point2d(roadTopPtsLocal[s].X, roadTopPtsLocal[s].Y - tAsf - tBase));
                }
                for (int s = roadTopPtsLocal.Count - 1; s >= 0; s--)
                {
                    polySubLocal.Add(new Point2d(roadTopPtsLocal[s].X, roadBaseBottomLevel));
                }
                var crvSub = PolyToCurve(pln, polySubLocal);
                res.AllCurves.Add(crvSub);
                res.CrvSubbase.Add(crvSub);
                AddBreps(res.SrfSubbase, crvSub);
            }

            // =========================================================================
            // 2. MEIOS-FIOS (CURBS) -> Camada 2 (RETANGULAR, SEM VÃO TRIANGULAR!)
            // =========================================================================
            double curbCount = 0;

            // Meio-fio Esquerdo
            List<Point2d> polyCurbLLocal = null;
            if (hasSwL)
            {
                curbCount += 1.0;
                polyCurbLLocal = new List<Point2d>
                {
                    new Point2d(uCurbL_Back, curbTopL_V),
                    new Point2d(uRoadL, curbTopL_V),
                    new Point2d(uRoadL, gutterL_V),
                    new Point2d(uRoadL, roadBaseBottomLevel),        // Veda verticalmente contra a base!
                    new Point2d(uCurbL_Back, roadBaseBottomLevel),    // Base perfeitamente horizontal!
                    new Point2d(uCurbL_Back, curbTopL_V)
                };
                var crvCurbL = PolyToCurve(pln, polyCurbLLocal);
                res.AllCurves.Add(crvCurbL);
                res.CrvCurb.Add(crvCurbL);
                AddBreps(res.SrfCurb, crvCurbL);
            }

            // Meio-fio Direito
            List<Point2d> polyCurbRLocal = null;
            if (hasSwR)
            {
                curbCount += 1.0;
                polyCurbRLocal = new List<Point2d>
                {
                    new Point2d(uRoadR, curbTopR_V),
                    new Point2d(uCurbR_Back, curbTopR_V),
                    new Point2d(uCurbR_Back, roadBaseBottomLevel),   // Face traseira vertical!
                    new Point2d(uRoadR, roadBaseBottomLevel),        // Base horizontal!
                    new Point2d(uRoadR, gutterR_V),                  // Veda verticalmente contra a via!
                    new Point2d(uRoadR, curbTopR_V)
                };
                var crvCurbR = PolyToCurve(pln, polyCurbRLocal);
                res.AllCurves.Add(crvCurbR);
                res.CrvCurb.Add(crvCurbR);
                AddBreps(res.SrfCurb, crvCurbR);
            }

            res.LengthCurbs = curbCount;

            // =========================================================================
            // 3. CALÇADA ESQUERDA (PISO: Camada 0 | TERRENO: Camada 1)
            // =========================================================================
            List<Point2d> polySwSlabLLocal = null;
            List<Point2d> polyTerrLLocal = null;

            if (hasSwL)
            {
                // 3.1: Piso da Calçada (Placa de 10cm) -> Camada 0
                polySwSlabLLocal = new List<Point2d>
                {
                    new Point2d(uQuadraL, swOuterL_V),
                    new Point2d(uCurbL_Back, curbTopL_V),
                    new Point2d(uCurbL_Back, curbTopL_V - swSlabThickness),
                    new Point2d(uQuadraL, swOuterL_V - swSlabThickness),
                    new Point2d(uQuadraL, swOuterL_V)
                };
                var crvSwL = PolyToCurve(pln, polySwSlabLLocal);
                res.AllCurves.Add(crvSwL);
                res.CrvSidewalk.Add(crvSwL);
                AddBreps(res.SrfSidewalk, crvSwL);
                res.AreaSidewalkSlab += (uCurbL_Back - uQuadraL) * swSlabThickness;

                // 3.2: Terreno sob Calçada -> Camada 1 (Vai até a base da via sem vão!)
                polyTerrLLocal = new List<Point2d>
                {
                    new Point2d(uQuadraL, swOuterL_V - swSlabThickness),
                    new Point2d(uCurbL_Back, curbTopL_V - swSlabThickness),
                    new Point2d(uCurbL_Back, roadBaseBottomLevel),
                    new Point2d(uQuadraL, roadBaseBottomLevel),
                    new Point2d(uQuadraL, swOuterL_V - swSlabThickness)
                };
                var crvTerrL = PolyToCurve(pln, polyTerrLLocal);
                res.AllCurves.Add(crvTerrL);
                res.CrvTerrain.Add(crvTerrL);
                AddBreps(res.SrfTerrain, crvTerrL);

                double avgH_L = ((swOuterL_V - swSlabThickness) + (curbTopL_V - swSlabThickness)) / 2.0 - roadBaseBottomLevel;
                res.VolumeSidewalkGround += (uCurbL_Back - uQuadraL) * avgH_L;
            }

            // =========================================================================
            // 4. CALÇADA DIREITA (PISO: Camada 0 | TERRENO: Camada 1)
            // =========================================================================
            List<Point2d> polySwSlabRLocal = null;
            List<Point2d> polyTerrRLocal = null;

            if (hasSwR)
            {
                // 4.1: Piso da Calçada Direita -> Camada 0
                polySwSlabRLocal = new List<Point2d>
                {
                    new Point2d(uCurbR_Back, curbTopR_V),
                    new Point2d(uQuadraR, swOuterR_V),
                    new Point2d(uQuadraR, swOuterR_V - swSlabThickness),
                    new Point2d(uCurbR_Back, curbTopR_V - swSlabThickness),
                    new Point2d(uCurbR_Back, curbTopR_V)
                };
                var crvSwR = PolyToCurve(pln, polySwSlabRLocal);
                res.AllCurves.Add(crvSwR);
                res.CrvSidewalk.Add(crvSwR);
                AddBreps(res.SrfSidewalk, crvSwR);
                res.AreaSidewalkSlab += (uQuadraR - uCurbR_Back) * swSlabThickness;

                // 4.2: Terreno sob Calçada Direita -> Camada 1
                polyTerrRLocal = new List<Point2d>
                {
                    new Point2d(uCurbR_Back, curbTopR_V - swSlabThickness),
                    new Point2d(uQuadraR, swOuterR_V - swSlabThickness),
                    new Point2d(uQuadraR, roadBaseBottomLevel),
                    new Point2d(uCurbR_Back, roadBaseBottomLevel),
                    new Point2d(uCurbR_Back, curbTopR_V - swSlabThickness)
                };
                var crvTerrR = PolyToCurve(pln, polyTerrRLocal);
                res.AllCurves.Add(crvTerrR);
                res.CrvTerrain.Add(crvTerrR);
                AddBreps(res.SrfTerrain, crvTerrR);

                double avgH_R = ((swOuterR_V - swSlabThickness) + (curbTopR_V - swSlabThickness)) / 2.0 - roadBaseBottomLevel;
                res.VolumeSidewalkGround += (uQuadraR - uCurbR_Back) * avgH_R;
            }

            // =========================================================================
            // 5. LINHA CONTÍNUA DO TERRENO INFERIOR
            // =========================================================================
            var groundLinePt1 = LocalToWorld(pln, uQuadraL - 0.5, roadBaseBottomLevel);
            var groundLinePt2 = LocalToWorld(pln, uQuadraR + 0.5, roadBaseBottomLevel);
            res.AllCurves.Add(new LineCurve(groundLinePt1, groundLinePt2));

            // =========================================================================
            // 6. GERAÇÃO DE HACHURAS TÉCNICAS VETORIAIS REAIS
            // =========================================================================
            if (polySwSlabLLocal != null) res.HatchCurves.AddRange(GeneratePolygonHatch(pln, polySwSlabLLocal, 0.15, 45.0));
            if (polySwSlabRLocal != null) res.HatchCurves.AddRange(GeneratePolygonHatch(pln, polySwSlabRLocal, 0.15, 45.0));

            if (polyTerrLLocal != null)
            {
                res.HatchCurves.AddRange(GeneratePolygonHatch(pln, polyTerrLLocal, 0.25, 45.0));
                res.HatchCurves.AddRange(GeneratePolygonHatch(pln, polyTerrLLocal, 0.25, -45.0));
            }
            if (polyTerrRLocal != null)
            {
                res.HatchCurves.AddRange(GeneratePolygonHatch(pln, polyTerrRLocal, 0.25, 45.0));
                res.HatchCurves.AddRange(GeneratePolygonHatch(pln, polyTerrRLocal, 0.25, -45.0));
            }

            res.HatchCurves.AddRange(GeneratePolygonHatch(pln, polyBaseLocal, 0.30, -45.0));
            res.HatchCurves.AddRange(GeneratePolygonHatch(pln, polyAsfLocal, 0.10, 45.0));

            if (drawAnno)
            {
                foreach (var hc in res.HatchCurves)
                {
                    res.AnnotationGeometry.Add(new GH_Curve(hc));
                }
            }

            // =========================================================================
            // 7. PONTOS NOTÁVEIS (KEY POINTS)
            // =========================================================================
            res.KeyPoints.Add(LocalToWorld(pln, 0, crownV));
            res.KeyPoints.Add(LocalToWorld(pln, uRoadL, gutterL_V));
            res.KeyPoints.Add(LocalToWorld(pln, uRoadR, gutterR_V));
            if (hasSwL)
            {
                res.KeyPoints.Add(LocalToWorld(pln, uCurbL_Back, curbTopL_V));
                res.KeyPoints.Add(LocalToWorld(pln, uQuadraL, swOuterL_V));
            }
            if (hasSwR)
            {
                res.KeyPoints.Add(LocalToWorld(pln, uCurbR_Back, curbTopR_V));
                res.KeyPoints.Add(LocalToWorld(pln, uQuadraR, swOuterR_V));
            }

            // =========================================================================
            // 8. ANOTAÇÕES TÉCNICAS E COTAS EM DOIS NÍVEIS
            // =========================================================================
            if (drawAnno)
            {
                BuildTechnicalAnnotations(
                    pln,
                    road,
                    uQuadraL,
                    uRoadL,
                    uRoadR,
                    uQuadraR,
                    wSwL,
                    wRoad,
                    wSwR,
                    totalWidth,
                    hasSwL,
                    hasSwR,
                    roadBaseBottomLevel,
                    labelSw,
                    labelRoad,
                    res
                );
            }

            return res;
        }

        private static Curve PolyToCurve(Plane pln, List<Point2d> localPts)
        {
            var pts3D = localPts.Select(p => LocalToWorld(pln, p.X, p.Y)).ToList();
            if (pts3D.Count > 1 && pts3D[0].DistanceTo(pts3D.Last()) > 1e-6)
            {
                pts3D.Add(pts3D[0]);
            }
            return new Polyline(pts3D).ToNurbsCurve();
        }

        private static void AddBreps(List<Brep> target, Curve crv)
        {
            if (crv == null || !crv.IsClosed) return;
            var breps = Brep.CreatePlanarBreps(crv, 0.001);
            if (breps != null) target.AddRange(breps);
        }

        private static void BuildTechnicalAnnotations(
            Plane pln,
            RoadDefinition road,
            double uQuadraL,
            double uRoadL,
            double uRoadR,
            double uQuadraR,
            double wSwL,
            double wRoad,
            double wSwR,
            double totalWidth,
            bool hasSwL,
            bool hasSwR,
            double roadBaseBottomLevel,
            string labelSw,
            string labelRoad,
            SectionBuildResult res)
        {
            // 1. DIVISA DE QUADRA E NOME DA RUA ELEVADO E ALINHADO À ESQUERDA
            double quadraTopY = 1.40;

            // Divisa Esquerda (Alinhamento Predial)
            var qL_bottom = LocalToWorld(pln, uQuadraL, roadBaseBottomLevel);
            var qL_top = LocalToWorld(pln, uQuadraL, quadraTopY);
            AddDashedLine(res, qL_bottom, qL_top, 0.15, 0.08);

            // Nome da Quadra Esquerda (logo acima do tracejado, alinhado à esquerda)
            if (!string.IsNullOrWhiteSpace(road.QuadraLeft))
            {
                var qPt = LocalToWorld(pln, uQuadraL, quadraTopY + 0.12);
                AddTextAnnotation(res, road.QuadraLeft, qPt, pln, 0.16, TextJustification.BottomLeft);
            }

            // Nome da Rua: DESLOCADO MAIS PARA O ALTO (2.00m) E ALINHADO À ESQUERDA COM A QUADRA!
            double titleY = quadraTopY + 0.60;
            var titlePt = LocalToWorld(pln, uQuadraL, titleY);
            AddTextAnnotation(res, road.StreetName, titlePt, pln, 0.28, TextJustification.BottomLeft);

            // Divisa Direita
            var qR_bottom = LocalToWorld(pln, uQuadraR, roadBaseBottomLevel);
            var qR_top = LocalToWorld(pln, uQuadraR, quadraTopY);
            AddDashedLine(res, qR_bottom, qR_top, 0.15, 0.08);

            if (!string.IsNullOrWhiteSpace(road.QuadraRight))
            {
                var qPt = LocalToWorld(pln, uQuadraR, quadraTopY + 0.12);
                AddTextAnnotation(res, road.QuadraRight, qPt, pln, 0.16, TextJustification.BottomRight);
            }

            // 2. RÓTULOS DOS ELEMENTOS (Substituindo 'Leito Carroçável' por rótulo usual / customizável)
            double labelElev = 0.50;
            if (hasSwL)
            {
                var pt = LocalToWorld(pln, (uQuadraL + uRoadL) / 2.0, labelElev);
                AddTextAnnotation(res, labelSw, pt, pln, 0.14, TextJustification.BottomCenter);
            }

            var ptRoad = LocalToWorld(pln, (uRoadL + uRoadR) / 2.0, labelElev);
            string finalRoadLabel = road.IsPedestrian ? "VIA PEDONAL" : labelRoad;
            AddTextAnnotation(res, finalRoadLabel, ptRoad, pln, 0.14, TextJustification.BottomCenter);

            if (hasSwR)
            {
                var pt = LocalToWorld(pln, (uRoadR + uQuadraR) / 2.0, labelElev);
                AddTextAnnotation(res, labelSw, pt, pln, 0.14, TextJustification.BottomCenter);
            }

            // 3. COTAS EM VERMELHO NA BASE (DOIS NÍVEIS)
            double tickH = 0.08;
            double dim1Y = roadBaseBottomLevel - 0.40;

            if (hasSwL)
            {
                AddDimensionSegment(res, pln, uQuadraL, uRoadL, dim1Y, tickH, $"{wSwL:F2}");
            }

            AddDimensionSegment(res, pln, uRoadL, uRoadR, dim1Y, tickH, $"{wRoad:F2}");

            if (hasSwR)
            {
                AddDimensionSegment(res, pln, uRoadR, uQuadraR, dim1Y, tickH, $"{wSwR:F2}");
            }

            // Nível 2: Cota Total da Via (espaçamento amplo de 50cm para que o texto '15.00' fique acima da linha 2 com folga perfeita)
            double dim2Y = dim1Y - 0.50;
            AddDimensionSegment(res, pln, uQuadraL, uQuadraR, dim2Y, tickH, $"{totalWidth:F2}");
        }

        private static void AddDimensionSegment(SectionBuildResult res, Plane pln, double u0, double u1, double dimY, double tickH, string label)
        {
            // Linha contínua da cota
            var p1 = LocalToWorld(pln, u0, dimY);
            var p2 = LocalToWorld(pln, u1, dimY);
            res.AnnotationGeometry.Add(new GH_Curve(new LineCurve(p1, p2)));

            // Ticks / traços delimitadores verticais
            var t1_bot = LocalToWorld(pln, u0, dimY - tickH);
            var t1_top = LocalToWorld(pln, u0, dimY + tickH);
            res.AnnotationGeometry.Add(new GH_Curve(new LineCurve(t1_bot, t1_top)));

            var t2_bot = LocalToWorld(pln, u1, dimY - tickH);
            var t2_top = LocalToWorld(pln, u1, dimY + tickH);
            res.AnnotationGeometry.Add(new GH_Curve(new LineCurve(t2_bot, t2_top)));

            // Cota posicionada estritamente ACIMA da linha de cota
            double uMid = (u0 + u1) / 2.0;
            double textOffsetAboveLine = 0.06;
            AddDimensionTextAboveLine(res, label, pln, uMid, dimY + textOffsetAboveLine, 0.14);
        }

        private static void AddDimensionTextAboveLine(SectionBuildResult res, string text, Plane pln, double uMid, double targetBottomY, double height)
        {
            var pt = LocalToWorld(pln, uMid, targetBottomY);
            var textEnt = new TextEntity
            {
                PlainText = text,
                Plane = new Plane(pt, pln.XAxis, pln.YAxis),
                TextHeight = height,
                Justification = TextJustification.BottomCenter
            };

            var curves = textEnt.Explode();
            if (curves != null && curves.Length > 0)
            {
                // Obter a caixa delimitadora real de todas as curvas no plano da seção
                var totalBb = BoundingBox.Empty;
                foreach (var c in curves)
                {
                    totalBb.Union(c.GetBoundingBox(pln));
                }

                // Garantir milimetricamente que o fundo do texto fique exatamente em targetBottomY e centralizado em uMid
                double currentUMid = (totalBb.Min.X + totalBb.Max.X) / 2.0;
                double uShift = uMid - currentUMid;
                double vShift = targetBottomY - totalBb.Min.Y;

                var xform = Transform.Translation((pln.XAxis * uShift) + (pln.YAxis * vShift));

                foreach (var c in curves)
                {
                    c.Transform(xform);
                    res.AnnotationGeometry.Add(new GH_Curve(c));
                }
            }
            else
            {
                res.AnnotationGeometry.Add(new GH_GeometricGooWrapper(textEnt));
            }
        }

        private static void AddDashedLine(SectionBuildResult res, Point3d p1, Point3d p2, double dashLen, double gapLen)
        {
            double dist = p1.DistanceTo(p2);
            if (dist < 0.001) return;

            var dir = (p2 - p1);
            dir.Unitize();

            double t = 0;
            while (t < dist)
            {
                double segEnd = Math.Min(t + dashLen, dist);
                var segP1 = p1 + dir * t;
                var segP2 = p1 + dir * segEnd;
                res.AnnotationGeometry.Add(new GH_Curve(new LineCurve(segP1, segP2)));
                t += dashLen + gapLen;
            }
        }

        private static List<Curve> GeneratePolygonHatch(Plane pln, List<Point2d> localPoly, double spacing, double angleDeg)
        {
            var curves = new List<Curve>();
            if (localPoly == null || localPoly.Count < 3) return curves;
            if (spacing <= 0.01) spacing = 0.20;

            var poly = new List<Point2d>(localPoly);
            if (poly.Count > 1 && poly[0].DistanceTo(poly[poly.Count - 1]) < 1e-6)
            {
                poly.RemoveAt(poly.Count - 1);
            }
            if (poly.Count < 3) return curves;

            double rad = angleDeg * Math.PI / 180.0;
            double cosA = Math.Cos(rad);
            double sinA = Math.Sin(rad);

            double minD = double.MaxValue;
            double maxD = double.MinValue;

            for (int i = 0; i < poly.Count; i++)
            {
                double u = poly[i].X;
                double v = poly[i].Y;
                double d = -u * sinA + v * cosA;
                if (d < minD) minD = d;
                if (d > maxD) maxD = d;
            }

            if (maxD <= minD) return curves;

            double c = minD + spacing * 0.5;
            int n = poly.Count;

            while (c <= maxD)
            {
                var intersections = new List<(double u, double v, double s)>();

                for (int i = 0; i < n; i++)
                {
                    var pA = poly[i];
                    var pB = poly[(i + 1) % n];

                    double da = -pA.X * sinA + pA.Y * cosA;
                    double db = -pB.X * sinA + pB.Y * cosA;

                    if ((da - c) * (db - c) <= 0.0 && Math.Abs(db - da) > 1e-9)
                    {
                        double t = (c - da) / (db - da);
                        if (t >= 0.0 && t <= 1.0)
                        {
                            double ui = pA.X + t * (pB.X - pA.X);
                            double vi = pA.Y + t * (pB.Y - pA.Y);
                            double si = ui * cosA + vi * sinA;
                            intersections.Add((ui, vi, si));
                        }
                    }
                }

                intersections.Sort((a, b) => a.s.CompareTo(b.s));

                for (int k = 0; k < intersections.Count - 1; k += 2)
                {
                    var pt1 = LocalToWorld(pln, intersections[k].u, intersections[k].v);
                    var pt2 = LocalToWorld(pln, intersections[k + 1].u, intersections[k + 1].v);
                    if (pt1.DistanceTo(pt2) > 0.005)
                    {
                        curves.Add(new LineCurve(pt1, pt2));
                    }
                }

                c += spacing;
            }

            return curves;
        }

        private static void AddTextAnnotation(SectionBuildResult res, string text, Point3d pt, Plane pln, double height, TextJustification justification)
        {
            var textEnt = new TextEntity
            {
                PlainText = text,
                Plane = new Plane(pt, pln.XAxis, pln.YAxis),
                TextHeight = height,
                Justification = justification
            };

            var curves = textEnt.Explode();
            if (curves != null && curves.Length > 0)
            {
                foreach (var c in curves)
                {
                    res.AnnotationGeometry.Add(new GH_Curve(c));
                }
            }
            else
            {
                res.AnnotationGeometry.Add(new GH_GeometricGooWrapper(textEnt));
            }
        }

        private sealed class AdaptiveSection
        {
            public int Street, Run, Station;
            public List<string> Labels = new List<string>();
            public List<Point3d> Boundaries = new List<Point3d>();
        }

        private static void BuildAdaptiveRoad(GH_Structure<GH_Point> points,
            GH_Structure<GH_String> profiles, out GH_Structure<GH_Brep> surfaces,
            out GH_Structure<GH_String> labels, out string report)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            surfaces = new GH_Structure<GH_Brep>(); labels = new GH_Structure<GH_String>();
            var groups = new Dictionary<string, List<AdaptiveSection>>();
            int rejected = 0, count = 0, runCount = 0;
            for (int i = 0; i < points.PathCount; i++)
            {
                var path = points.Paths[i]; var branch = points.Branches[i];
                if (path.Indices.Length < 3 || branch.Count != 5 || branch.Any(x => x == null || !x.Value.IsValid))
                { rejected++; continue; }
                var profilePath = new GH_Path(path.Indices[0], path.Indices[2]);
                int pi = -1;
                for (int k = 0; k < profiles.PathCount; k++)
                    if (profiles.Paths[k].Equals(profilePath)) { pi = k; break; }
                if (pi < 0) { rejected++; continue; }
                var names = new List<string>(); var widths = new List<double>();
                foreach (var item in profiles.Branches[pi])
                {
                    if (item == null || string.IsNullOrWhiteSpace(item.Value)) continue;
                    var fields = item.Value.Split('|').Select(x => x.Trim()).ToArray();
                    if (fields.Length < 2) continue;
                    string kind = fields[0];
                    if (kind != "Lane" && kind != "Median" && kind != "Roadway Reserve" &&
                        kind != "Pedestrian" && kind != "Road" && kind != "Cycle Track" &&
                        kind != "Parking" && kind != "Tree Strip" && kind != "Green Strip" &&
                        kind != "Furniture Strip" && kind != "Transit" && kind != "Shoulder" &&
                        kind != "Other") continue;
                    var widthField = fields.FirstOrDefault(x => x.StartsWith("Width=", StringComparison.OrdinalIgnoreCase));
                    if (widthField == null || !double.TryParse(widthField.Substring(6), NumberStyles.Float,
                        CultureInfo.InvariantCulture, out double w) || w < 0 ||
                        (w == 0 && kind != "Roadway Reserve" && kind != "Median" &&
                         !fields.Any(x => x.Equals("Status=SUPPRESSED", StringComparison.OrdinalIgnoreCase)))) continue;
                    names.Add(kind + ": " + fields[1]); widths.Add(w);
                }
                if (widths.Count == 0) { rejected++; continue; }
                bool pedestrian = names.Count == 1 && names[0].StartsWith("Pedestrian:");
                Point3d left = pedestrian ? branch[0].Value : branch[1].Value;
                Point3d right = pedestrian ? branch[4].Value : branch[3].Value;
                double available = left.DistanceTo(right), assigned = widths.Sum();
                if (available <= 0 || Math.Abs(available - assigned) > 0.02)
                { rejected++; continue; }
                widths[widths.Count - 1] += available - assigned; // somente erro de arredondamento
                var sec = new AdaptiveSection { Street=path.Indices[0], Run=path.Indices[1],
                    Station=path.Indices[2], Labels=names };
                sec.Boundaries.Add(left);
                double accumulated = 0;
                foreach (double w in widths)
                {
                    accumulated += w;
                    sec.Boundaries.Add(left + (right-left)*(accumulated/available));
                }
                string key = sec.Street + ":" + sec.Run;
                if (!groups.TryGetValue(key, out var list)) groups[key] = list = new List<AdaptiveSection>();
                list.Add(sec);
            }
            foreach (var group in groups.Values)
            {
                group.Sort((a,b) => a.Station.CompareTo(b.Station));
                if (group.Count < 2) continue;
                runCount++;
                for (int i = 1; i < group.Count; i++)
                {
                    var a = group[i-1]; var b = group[i];
                    if (b.Station != a.Station+1 || a.Labels.Count != b.Labels.Count ||
                        !a.Labels.SequenceEqual(b.Labels)) { rejected++; continue; }
                    for (int e = 0; e < a.Labels.Count; e++)
                    {
                        var a0 = a.Boundaries[e]; var a1 = a.Boundaries[e+1];
                        var b0 = b.Boundaries[e]; var b1 = b.Boundaries[e+1];
                        bool aZero = a0.DistanceTo(a1) < 1e-5;
                        bool bZero = b0.DistanceTo(b1) < 1e-5;
                        if (aZero && bZero) continue;
                        var brep = aZero ? Brep.CreateFromCornerPoints(a0, b0, b1, 0.01)
                            : bZero ? Brep.CreateFromCornerPoints(a0, b0, a1, 0.01)
                            : Brep.CreateFromCornerPoints(a0, b0, b1, a1, 0.01);
                        if (brep == null || !brep.IsValid) { rejected++; continue; }
                        var path = new GH_Path(a.Street,a.Run,e);
                        surfaces.Append(new GH_Brep(brep),path);
                        if (!labels.Paths.Any(x => x.Equals(path)))
                            labels.Append(new GH_String(a.Labels[e]),path);
                        count++;
                    }
                }
            }
            watch.Stop();
            report = $"Street runs={runCount} | Sections={groups.Values.Sum(x => x.Count)} | Profiles={groups.Values.Sum(x => x.Count)} | Road element surfaces={count} | Intersections=0 | Invalid/Conflicts={rejected} | Generation time={watch.ElapsedMilliseconds} ms. Adaptive output is an open 2.5D surface model; junction infill and volumetric solids are pending.";
        }

        private static List<Brep> BuildRoad3DSweep(List<Curve> sectionCurves, Curve axis)
        {
            var list = new List<Brep>();
            if (axis == null || !axis.IsValid || sectionCurves == null) return list;

            foreach (var crv in sectionCurves)
            {
                if (crv == null || !crv.IsValid) continue;
                var sweeps = Brep.CreateFromSweep(axis, crv, true, 0.001);
                if (sweeps != null) list.AddRange(sweeps);
            }
            return list;
        }

        private static Point3d LocalToWorld(Plane pln, double u, double v)
        {
            return pln.Origin + (pln.XAxis * u) + (pln.YAxis * v);
        }

        #endregion
    }

    /// <summary>
    /// Wrapper para empacotar TextEntity e outras geometrias no Grasshopper.
    /// </summary>
    public class GH_GeometricGooWrapper : GH_GeometricGoo<GeometryBase>
    {
        private GeometryBase _geom;

        public GH_GeometricGooWrapper(GeometryBase geom)
        {
            _geom = geom;
        }

        public override bool IsValid => _geom != null && _geom.IsValid;
        public override string TypeName => _geom?.GetType().Name ?? "Geometry";
        public override string TypeDescription => "Geometria de anotação e texto técnico";
        public override BoundingBox Boundingbox => _geom?.GetBoundingBox(true) ?? BoundingBox.Empty;
        public override IGH_GeometricGoo DuplicateGeometry() => new GH_GeometricGooWrapper(_geom?.Duplicate());
        public override BoundingBox GetBoundingBox(Transform xform) => _geom?.GetBoundingBox(xform) ?? BoundingBox.Empty;
        public override IGH_GeometricGoo Transform(Transform xform)
        {
            var dup = _geom?.Duplicate();
            dup?.Transform(xform);
            return new GH_GeometricGooWrapper(dup);
        }
        public override IGH_GeometricGoo Morph(SpaceMorph morph) => this;
        public override string ToString() => _geom?.ToString() ?? "Geometry";
        public override object ScriptVariable() => _geom;
        public override bool CastTo<Q>(ref Q target)
        {
            if (typeof(Q).IsAssignableFrom(typeof(GeometryBase)))
            {
                target = (Q)(object)_geom;
                return true;
            }
            return false;
        }
    }
}

