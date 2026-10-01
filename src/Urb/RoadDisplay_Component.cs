using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino;
using Rhino.Display;
using Rhino.DocObjects;
using Rhino.Geometry;

namespace Buraqueira_Urb
{
    /// <summary>
    /// Componente de Visualização e Renderização com as Cores Certas:
    /// - Pista / Asfalto: Cinza escuro asfalto (RGB 55, 58, 62)
    /// - Calçada (Piso): Cinza claro concreto (RGB 225, 228, 230)
    /// - Terreno sob Calçada: Tom de terra/bege (RGB 220, 208, 188)
    /// - Meio-Fio: Cinza concreto médio (RGB 195, 200, 205)
    /// - Base Granular: Areia/brita (RGB 210, 195, 160)
    /// - Sub-base: Brita compactada (RGB 190, 175, 140)
    /// - Canteiro Central: Grama verde viva (RGB 110, 180, 90)
    /// - Cotas e Divisas: Vermelho técnico (RGB 210, 30, 30)
    /// - Hachuras: Cinza grafite (RGB 120, 125, 130)
    /// </summary>
    public class RoadDisplay_Component : GH_Component
    {
        public RoadDisplay_Component()
            : base(
                "Road Section Display",
                "RoadDisplay",
                "Exibe e renderiza as seções 2D e vias 3D com as cores e materiais técnicos corretos (asfalto, calçada, terreno, meio-fio, canteiro verde, cotas em vermelho e hachuras). Suporta exportação/bake organizado por camadas.",
                "Glaux Urb",
                "01 | Infraestrutura Viária")
        {
        }

        public override Guid ComponentGuid => new Guid("8c3d4e5f-6a7b-8c9d-0e1f-2a3b4c5d6e7f");

        protected override Bitmap Icon => GlauxUrbIcons.RoadDisplay;

        // Armazenamento para renderização na viewport
        private readonly List<(Brep brep, Color color, DisplayMaterial mat)> _cachedBreps = new List<(Brep, Color, DisplayMaterial)>();
        private readonly List<(Mesh mesh, Color color, DisplayMaterial mat)> _cachedMeshes = new List<(Mesh, Color, DisplayMaterial)>();
        private readonly List<(Curve curve, Color color, int thickness)> _cachedWires = new List<(Curve, Color, int)>();
        private BoundingBox _clippingBox = BoundingBox.Empty;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            // 0: Superfícies 2D ou Sólidos 3D (Árvore de camadas {0} Calçada, {1} Terreno, etc.)
            pManager.AddBrepParameter("Section / 3D Geometry", "Geom", 
                "Superfícies 2D (saída Srf) ou Sólidos 3D (saída 3D) organizados em árvore por camadas pelo componente RoadCrossSection.", 
                GH_ParamAccess.tree);
            pManager[0].Optional = true;

            // 1: Curvas de Hachura
            pManager.AddCurveParameter("Hatch Curves", "Hatch", 
                "Linhas de hachura técnica (saída Hatch do RoadCrossSection).", 
                GH_ParamAccess.list);
            pManager[1].Optional = true;

            // 2: Anotações e Cotas
            pManager.AddGeometryParameter("Annotations", "Anno", 
                "Geometrias das cotas, nomes e divisas em vermelho (saída Anno do RoadCrossSection).", 
                GH_ParamAccess.list);
            pManager[2].Optional = true;

            // 3: Paleta de Cores
            pManager.AddIntegerParameter("Color Palette", "Theme", 
                "Esquema de Cores:\n0: Realista (Asfalto grafite, calçada clara, terreno bege, meio-fio, canteiro verde)\n1: Diagrama Urbanístico (Cores contrastantes para apresentações)\n2: Técnico CAD Preto & Branco", 
                GH_ParamAccess.item, 0);
            pManager[3].Optional = true;

            // 4: Transparência
            pManager.AddNumberParameter("Transparency", "Alpha", 
                "Nível de transparência das superfícies e sólidos de 0.0 (opaco) a 1.0 (transparente). Padrão: 0.0.", 
                GH_ParamAccess.item, 0.0);
            pManager[4].Optional = true;

            // 5: Gravar no Rhino (Bake)
            pManager.AddBooleanParameter("Bake to Layers", "Bake", 
                "Se True, cria automaticamente as camadas com suas cores (BURAQUEIRA_URB::01_Piso, 02_Terreno, etc.) e grava as geometrias no Rhino.", 
                GH_ParamAccess.item, false);
            pManager[5].Optional = true;

            // 6: A REGRA DE OURO: RUN É O ÚLTIMO PARÂMETRO
            pManager.AddBooleanParameter("Run", "Run", 
                "Gatilho booleano para ativar o visualizador com as cores corretas.", 
                GH_ParamAccess.item, true);
            pManager[6].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddMeshParameter("Colored Meshes", "Mesh", "Malhas 2D/3D com cores por vértice e materiais aplicados prontas para renderização.", GH_ParamAccess.list);
            pManager.AddTextParameter("Display Report", "Info", "Relatório descritivo das camadas, cores aplicadas e status do Bake.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            int runIdx = Params.Input.Count - 1;
            bool run = true;
            DA.GetData(runIdx, ref run);

            _cachedBreps.Clear();
            _cachedMeshes.Clear();
            _cachedWires.Clear();
            _clippingBox = BoundingBox.Empty;

            if (!run)
            {
                Message = "Pausado";
                return;
            }

            GH_Structure<GH_Brep> geomTree;
            DA.GetDataTree(0, out geomTree);

            var hatchList = new List<Curve>();
            DA.GetDataList(1, hatchList);

            var annoList = new List<IGH_GeometricGoo>();
            DA.GetDataList(2, annoList);

            int theme = 0;
            DA.GetData(3, ref theme);

            double alpha = 0.0;
            DA.GetData(4, ref alpha);
            alpha = Math.Max(0.0, Math.Min(1.0, alpha));

            bool bake = false;
            DA.GetData(5, ref bake);

            var palette = GetPalette(theme);

            var outMeshes = new List<Mesh>();
            var sbInfo = new StringBuilder();
            sbInfo.AppendLine("=== BURAQUEIRA URB: VISUALIZADOR DE SEÇÃO VIÁRIA ===");
            sbInfo.AppendLine($"Paleta Ativa: {(theme == 0 ? "Realista" : theme == 1 ? "Diagrama Urbanístico" : "Técnico CAD PB")}");
            sbInfo.AppendLine($"Transparência: {alpha * 100:F0}%\n");

            var bakeItems = new List<(GeometryBase geom, string layerName, Color layerColor)>();

            // 1. Processar Geometrias (Superfícies ou Sólidos 3D organizados em árvore)
            if (geomTree != null && geomTree.DataCount > 0)
            {
                foreach (var path in geomTree.Paths)
                {
                    int layerIndex = path.Indices.Length > 0 ? path.Indices.Last() : 0;
                    var branch = geomTree[path];
                    if (branch == null || branch.Count == 0) continue;

                    var style = GetLayerStyle(layerIndex, palette);
                    string layerName = style.Name;
                    Color baseColor = style.Color;

                    Color renderColor = ApplyAlpha(baseColor, alpha);
                    var mat = new DisplayMaterial(renderColor, 0.2) { Transparency = alpha };

                    sbInfo.AppendLine($"Camada {{{layerIndex}}} {layerName}: {branch.Count} elemento(s) - RGB({baseColor.R}, {baseColor.G}, {baseColor.B})");

                    for (int bIdx = 0; bIdx < branch.Count; bIdx++)
                    {
                        var ghBrep = branch[bIdx];
                        if (ghBrep == null || !ghBrep.IsValid) continue;
                        Brep brep = ghBrep.Value;
                        if (brep == null || !brep.IsValid) continue;

                        _cachedBreps.Add((brep, renderColor, mat));
                        _clippingBox.Union(brep.GetBoundingBox(true));

                        var meshes = Mesh.CreateFromBrep(brep, MeshingParameters.FastRenderMesh);
                        if (meshes != null)
                        {
                            foreach (var m in meshes)
                            {
                                if (m == null || !m.IsValid) continue;
                                m.VertexColors.Clear();
                                for (int vi = 0; vi < m.Vertices.Count; vi++)
                                {
                                    m.VertexColors.Add(renderColor);
                                }
                                outMeshes.Add(m);
                                _cachedMeshes.Add((m, renderColor, mat));
                            }
                        }

                        if (bake)
                        {
                            bakeItems.Add((brep, $"BURAQUEIRA_URB::{layerName}", baseColor));
                        }
                    }
                }
            }

            // 2. Processar Hachuras Técnicas
            if (hatchList != null && hatchList.Count > 0)
            {
                Color hatchColor = palette.HatchColor;
                foreach (var crv in hatchList)
                {
                    if (crv == null || !crv.IsValid) continue;
                    _cachedWires.Add((crv, hatchColor, 1));
                    _clippingBox.Union(crv.GetBoundingBox(true));

                    if (bake)
                    {
                        bakeItems.Add((crv, "BURAQUEIRA_URB::08_Hachuras_Tecnicas", hatchColor));
                    }
                }
                sbInfo.AppendLine($"\nHachuras Técnicas: {hatchList.Count} linhas vetoriais");
            }

            // 3. Processar Anotações e Cotas em Vermelho
            if (annoList != null && annoList.Count > 0)
            {
                Color annoColor = palette.AnnoColor;
                int crvCount = 0;
                foreach (var item in annoList)
                {
                    if (item == null) continue;
                    if (item is GH_Curve ghCrv && ghCrv.Value != null)
                    {
                        _cachedWires.Add((ghCrv.Value, annoColor, 1));
                        _clippingBox.Union(ghCrv.Value.GetBoundingBox(true));
                        crvCount++;

                        if (bake)
                        {
                            bakeItems.Add((ghCrv.Value, "BURAQUEIRA_URB::07_Cotas_Anotacoes", annoColor));
                        }
                    }
                    else if (item.ScriptVariable() is GeometryBase geom)
                    {
                        _clippingBox.Union(geom.GetBoundingBox(true));
                        if (bake)
                        {
                            bakeItems.Add((geom, "BURAQUEIRA_URB::07_Cotas_Anotacoes", annoColor));
                        }
                    }
                }
                sbInfo.AppendLine($"Anotações & Cotas: {crvCount} curvas de cota em vermelho");
            }

            // 4. Executar Bake se solicitado
            if (bake && bakeItems.Count > 0)
            {
                string bakeReport = ExecuteBake(bakeItems);
                sbInfo.AppendLine($"\nSTATUS BAKE:\n{bakeReport}");
            }

            DA.SetDataList(0, outMeshes);
            DA.SetData(1, sbInfo.ToString());

            Message = theme == 0 ? "Cores Reais" : theme == 1 ? "Diagrama" : "CAD PB";
        }

        #region Renderização na Viewport do Rhino

        public override bool IsPreviewCapable => true;

        public override BoundingBox ClippingBox => _clippingBox;

        public override void DrawViewportMeshes(IGH_PreviewArgs args)
        {
            if (_cachedBreps.Count == 0 && _cachedMeshes.Count == 0) return;

            foreach (var item in _cachedBreps)
            {
                if (item.brep == null || !item.brep.IsValid) continue;
                args.Display.DrawBrepShaded(item.brep, item.mat);
            }
        }

        public override void DrawViewportWires(IGH_PreviewArgs args)
        {
            if (_cachedWires.Count == 0 && _cachedBreps.Count == 0) return;

            foreach (var item in _cachedBreps)
            {
                if (item.brep == null || !item.brep.IsValid) continue;
                args.Display.DrawBrepWires(item.brep, Color.FromArgb(40, 40, 40), 1);
            }

            foreach (var item in _cachedWires)
            {
                if (item.curve == null || !item.curve.IsValid) continue;
                args.Display.DrawCurve(item.curve, item.color, item.thickness);
            }
        }

        #endregion

        #region Paletas e Estilos de Cores

        public class ColorPalette
        {
            public Color Sidewalk { get; set; }
            public Color Terrain { get; set; }
            public Color Curb { get; set; }
            public Color Roadway { get; set; }
            public Color Base { get; set; }
            public Color Subbase { get; set; }
            public Color Median { get; set; }
            public Color AnnoColor { get; set; }
            public Color HatchColor { get; set; }
        }

        private static ColorPalette GetPalette(int theme)
        {
            switch (theme)
            {
                case 1:
                    return new ColorPalette
                    {
                        Sidewalk = Color.FromArgb(245, 230, 200),
                        Terrain = Color.FromArgb(220, 195, 150),
                        Curb = Color.FromArgb(170, 180, 190),
                        Roadway = Color.FromArgb(70, 80, 95),
                        Base = Color.FromArgb(210, 200, 175),
                        Subbase = Color.FromArgb(195, 185, 160),
                        Median = Color.FromArgb(100, 190, 90),
                        AnnoColor = Color.FromArgb(220, 30, 30),
                        HatchColor = Color.FromArgb(100, 105, 115)
                    };

                case 2:
                    return new ColorPalette
                    {
                        Sidewalk = Color.FromArgb(250, 250, 250),
                        Terrain = Color.FromArgb(235, 235, 235),
                        Curb = Color.FromArgb(210, 210, 210),
                        Roadway = Color.FromArgb(100, 100, 100),
                        Base = Color.FromArgb(225, 225, 225),
                        Subbase = Color.FromArgb(215, 215, 215),
                        Median = Color.FromArgb(240, 240, 240),
                        AnnoColor = Color.FromArgb(20, 20, 20),
                        HatchColor = Color.FromArgb(60, 60, 60)
                    };

                case 0:
                default:
                    return new ColorPalette
                    {
                        Sidewalk = Color.FromArgb(225, 228, 230),  // Concreto piso calçada
                        Terrain = Color.FromArgb(220, 208, 188),   // Subleito / Terra sob passeio
                        Curb = Color.FromArgb(195, 200, 205),      // Meio-fio de concreto
                        Roadway = Color.FromArgb(55, 58, 62),      // Asfalto CBUQ grafite
                        Base = Color.FromArgb(210, 195, 160),      // Base de brita graduada
                        Subbase = Color.FromArgb(190, 175, 140),   // Sub-base
                        Median = Color.FromArgb(110, 180, 90),     // Grama viva do canteiro
                        AnnoColor = Color.FromArgb(210, 30, 30),   // Cotas em vermelho
                        HatchColor = Color.FromArgb(120, 125, 130)  // Hachuras grafite
                    };
            }
        }

        private static (string Name, Color Color) GetLayerStyle(int layerIndex, ColorPalette pal)
        {
            switch (layerIndex)
            {
                case 0: return ("01_Piso_Calcada", pal.Sidewalk);
                case 1: return ("02_Terreno_Passeio", pal.Terrain);
                case 2: return ("03_Meios_Fios", pal.Curb);
                case 3: return ("04_Pista_Asfalto", pal.Roadway);
                case 4: return ("05_Base_Granular", pal.Base);
                case 5: return ("06_Subbase_Granular", pal.Subbase);
                case 6: return ("07_Canteiro_Verde", pal.Median);
                default: return ($"Camada_{layerIndex}", pal.Roadway);
            }
        }

        private static Color ApplyAlpha(Color c, double alpha)
        {
            int a = (int)Math.Max(10, Math.Min(255, (1.0 - alpha) * 255.0));
            return Color.FromArgb(a, c.R, c.G, c.B);
        }

        #endregion

        #region Gravação em Camadas do Rhino (Bake)

        private static string ExecuteBake(List<(GeometryBase geom, string layerName, Color layerColor)> items)
        {
            var doc = RhinoDoc.ActiveDoc;
            if (doc == null) return "Erro: Nenhum documento do Rhino ativo encontrado.";

            int bakedCount = 0;
            var layerTable = doc.Layers;

            foreach (var group in items.GroupBy(x => x.layerName))
            {
                string layerFullName = group.Key;
                Color layerColor = group.First().layerColor;

                int layerIndex = layerTable.FindByFullPath(layerFullName, -1);
                if (layerIndex < 0)
                {
                    var parts = layerFullName.Split(new[] { "::" }, StringSplitOptions.RemoveEmptyEntries);
                    int parentIdx = -1;

                    string curPath = "";
                    foreach (var part in parts)
                    {
                        curPath = string.IsNullOrEmpty(curPath) ? part : curPath + "::" + part;
                        int idx = layerTable.FindByFullPath(curPath, -1);
                        if (idx < 0)
                        {
                            var newLayer = new Layer { Name = part, Color = layerColor };
                            if (parentIdx >= 0) newLayer.ParentLayerId = layerTable[parentIdx].Id;
                            idx = layerTable.Add(newLayer);
                        }
                        parentIdx = idx;
                    }
                    layerIndex = parentIdx;
                }

                var attrs = new ObjectAttributes { LayerIndex = layerIndex };

                foreach (var item in group)
                {
                    if (item.geom is Brep b)
                    {
                        doc.Objects.AddBrep(b, attrs);
                        bakedCount++;
                    }
                    else if (item.geom is Curve c)
                    {
                        doc.Objects.AddCurve(c, attrs);
                        bakedCount++;
                    }
                    else if (item.geom is Mesh m)
                    {
                        doc.Objects.AddMesh(m, attrs);
                        bakedCount++;
                    }
                    else if (item.geom is TextEntity t)
                    {
                        doc.Objects.AddText(t, attrs);
                        bakedCount++;
                    }
                }
            }

            doc.Views.Redraw();
            return $"Gravados {bakedCount} objetos em {items.Select(x => x.layerName).Distinct().Count()} camadas no Rhino.";
        }

        #endregion
    }
}
