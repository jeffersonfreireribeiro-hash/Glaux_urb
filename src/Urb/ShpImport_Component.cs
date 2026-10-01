using System;
using System.Collections.Generic;
using System.IO;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;

namespace Buraqueira_Urb
{
    /// <summary>
    /// Componente do Grasshopper para importar arquivos vetoriais ESRI Shapefile (.shp + .dbf).
    /// Suporta Quadras (Polígonos), Meios-fios e Logradouros (Polilinhas), Lotes e Pontos com extração de atributos.
    /// </summary>
    public class ShpImport_Component : GH_Component
    {
        public ShpImport_Component()
            : base(
                "Import Shapefile",
                "ShpImport",
                "Importa SHP/DBF com Fields separado de Attributes tipados por feição; preserva Attrs legados e permite escolher Encoding.",
                "Glaux Urb",
                "00 | GIS & Dados Urbanos")
        {
        }

        public override Guid ComponentGuid => new Guid("a1b2c3d4-e5f6-7a8b-9c0d-1e2f3a4b5c6d");

        protected override System.Drawing.Bitmap Icon => GlauxUrbIcons.ShpImport;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("File Path", "Path", "Caminho absoluto do arquivo Shapefile (.shp).", GH_ParamAccess.item);
            
            pManager.AddTextParameter("Filter Query", "Filter", "Filtro de texto opcional para selecionar feições por atributo (ex: nome de bairro, tipo).", GH_ParamAccess.item, "");
            pManager[1].Optional = true;

            pManager.AddTextParameter("Encoding", "Encoding", "Auto usa .cpg, depois o código de idioma DBF reconhecido; sem metadados usa fallback Windows-1252 identificado como Default. Informe UTF-8, Windows-1252, ISO-8859-1 ou outra codificação .NET para forçar a leitura.", GH_ParamAccess.item, "Auto");
            pManager[2].Optional = true;

            // Run permanece o último input; o parâmetro existente mantém seu GUID.
            pManager.AddBooleanParameter("Run", "Run", "Ativa o processamento do componente (Padrão: True).", GH_ParamAccess.item, true);
            pManager[3].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddCurveParameter("Curves", "Crv", "Curvas e polilinhas das feições importadas (limites de quadras, eixos de vias, meios-fios).", GH_ParamAccess.list);
            pManager.AddBrepParameter("Surfaces", "Srf", "Superfícies planas geradas para polígonos fechados (ex: polígonos de quadra).", GH_ParamAccess.list);
            pManager.AddPointParameter("Points", "Pts", "Pontos ou vértices das feições.", GH_ParamAccess.list);
            pManager.AddTextParameter("Field Names", "Fields", "Lista de nomes das colunas da tabela de atributos (.dbf).", GH_ParamAccess.list);
            pManager.AddTextParameter("Attributes Tree", "Attrs", "Legado: {feição} -> [Campo: Valor]. Use Attributes para valores limpos.", GH_ParamAccess.tree);
            pManager.AddGenericParameter("Attributes", "Values", "Valores tipados por {feição}, na mesma ordem de Fields; NULL aparece como GisNullValue. Preserve a estrutura da árvore.", GH_ParamAccess.tree);
            pManager.AddGenericParameter("Geometry by Feature", "Geometry", "Geometrias agrupadas por {feição}, no mesmo índice da árvore Attributes; inclui todas as partes.", GH_ParamAccess.tree);
            pManager.AddGenericParameter("GIS Features", "Features", "Feições tipadas com geometria, RecordNumber e atributos consultáveis por nome; conecte ao Street Profile Assignment.", GH_ParamAccess.list);
            pManager.AddTextParameter("CRS", "CRS", "Conteúdo WKT do arquivo .prj, quando presente; sem reprojeção automática.", GH_ParamAccess.item);
            pManager.AddTextParameter("Encoding Info", "EncInfo", "Codificação efetiva e origem: User, CPG, DBF-LDID ou Default (unverified).", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // O parâmetro 'Run' é o último (índice 3)
            bool run = true;
            DA.GetData(3, ref run);

            if (!run)
            {
                Message = "Pausado (Run=False)";
                return;
            }

            string shpPath = null;
            if (!DA.GetData(0, ref shpPath) || string.IsNullOrWhiteSpace(shpPath))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Caminho do arquivo .shp não fornecido.");
                Message = "Sem Caminho";
                return;
            }

            if (!File.Exists(shpPath))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Arquivo .shp não encontrado: {shpPath}");
                Message = "Arquivo Não Encontrado";
                return;
            }

            string filter = null;
            DA.GetData(1, ref filter);
            string encodingName = "Auto";
            DA.GetData(2, ref encodingName);

            try
            {
                var features = ShapefileReader.ReadShapefile(shpPath, out var fieldNames,
                    out var encodingInfo, filter, encodingName);

                var outCurves = new List<GH_Curve>();
                var outSurfaces = new List<GH_Brep>();
                var outPoints = new List<GH_Point>();
                var attrTree = new GH_Structure<GH_String>();

                for (int i = 0; i < features.Count; i++)
                {
                    var feat = features[i];
                    var path = new GH_Path(i);

                    // Curvas
                    if (feat.Curves != null)
                    {
                        foreach (var crv in feat.Curves)
                        {
                            if (crv != null && crv.IsValid)
                            {
                                outCurves.Add(new GH_Curve(crv));
                            }
                        }
                    }

                    // Superfícies (Quadras)
                    if (feat.Surface != null && feat.Surface.IsValid)
                    {
                        outSurfaces.Add(new GH_Brep(feat.Surface));
                    }

                    // Pontos
                    if (feat.Points != null)
                    {
                        foreach (var pt in feat.Points)
                        {
                            outPoints.Add(new GH_Point(pt));
                        }
                    }

                    // Atributos
                    if (feat.Attributes != null)
                    {
                        foreach (var kvp in feat.Attributes)
                        {
                            attrTree.Append(new GH_String($"{kvp.Key}: {kvp.Value}"), path);
                        }
                    }
                }

                DA.SetDataList(0, outCurves);
                DA.SetDataList(1, outSurfaces);
                DA.SetDataList(2, outPoints);
                DA.SetDataList(3, fieldNames);
                DA.SetDataTree(4, attrTree);
                GisImportOutputs.Build(features, fieldNames, out var values, out var geometry);
                DA.SetDataTree(5, values);
                DA.SetDataTree(6, geometry);
                DA.SetDataList(7, features);
                string prjPath = Path.ChangeExtension(shpPath, ".prj");
                DA.SetData(8, File.Exists(prjPath) ? File.ReadAllText(prjPath) : "Unknown (no .prj)");
                DA.SetData(9, encodingInfo);

                Message = $"{features.Count} Feições";
            }
            catch (Exception ex)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Erro ao processar Shapefile: {ex.Message}");
                Message = "Erro SHP";
            }
        }
    }
}
