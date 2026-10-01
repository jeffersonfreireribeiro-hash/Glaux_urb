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
    /// Componente do Grasshopper para importar arquivos geoespaciais OGC GeoPackage (.gpkg).
    /// Suporta Quadras (Polígonos), Meios-fios e Logradouros (Linhas), Lotes e Pontos com extração de atributos e listagem de camadas.
    /// </summary>
    public class GpkgImport_Component : GH_Component
    {
        public GpkgImport_Component()
            : base(
                "Import GeoPackage",
                "GpkgImport",
                "Importa GeoPackage com Fields separado de Attributes tipados por feição; mantém Attrs legados e permite selecionar a camada.",
                "Glaux Urb",
                "00 | GIS & Dados Urbanos")
        {
        }

        public override Guid ComponentGuid => new Guid("c3d4e5f6-a7b8-9c0d-1e2f-3a4b5c6d7e8f");

        protected override System.Drawing.Bitmap Icon => GlauxUrbIcons.GpkgImport;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("File Path", "Path", "Caminho absoluto do arquivo GeoPackage (.gpkg).", GH_ParamAccess.item);

            pManager.AddTextParameter("Layer Name", "Layer", "Nome da camada a ser importada (opcional; se vazio, importa a primeira camada de feições).", GH_ParamAccess.item, "");
            pManager[1].Optional = true;

            pManager.AddTextParameter("Filter Query", "Filter", "Filtro de texto opcional para selecionar feições por atributo (ex: nome, tipo).", GH_ParamAccess.item, "");
            pManager[2].Optional = true;

            // REGRA DE OURO: O parâmetro 'Run' deve ser SEMPRE o último parâmetro de entrada!
            pManager.AddBooleanParameter("Run", "Run", "Ativa o processamento do componente (Padrão: True).", GH_ParamAccess.item, true);
            pManager[3].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddCurveParameter("Curves", "Crv", "Curvas e polilinhas da camada importada (limites de quadras, eixos, meios-fios).", GH_ParamAccess.list);
            pManager.AddBrepParameter("Surfaces", "Srf", "Superfícies planas geradas para polígonos fechados (ex: quadras).", GH_ParamAccess.list);
            pManager.AddPointParameter("Points", "Pts", "Pontos ou vértices das feições.", GH_ParamAccess.list);
            pManager.AddTextParameter("Field Names", "Fields", "Lista de nomes das colunas da tabela de atributos.", GH_ParamAccess.list);
            pManager.AddTextParameter("Attributes Tree", "Attrs", "Legado: {feição} -> [Campo: Valor]. Use Attributes para valores limpos.", GH_ParamAccess.tree);
            pManager.AddTextParameter("Available Layers", "Layers", "Lista de todas as camadas de feições disponíveis dentro do GeoPackage.", GH_ParamAccess.list);
            pManager.AddGenericParameter("Attributes", "Values", "Valores tipados por {feição}, na mesma ordem de Fields; NULL aparece como GisNullValue.", GH_ParamAccess.tree);
            pManager.AddGenericParameter("Geometry by Feature", "Geometry", "Geometrias agrupadas por {feição}, no mesmo índice da árvore Attributes.", GH_ParamAccess.tree);
            pManager.AddGenericParameter("GIS Features", "Features", "Feições tipadas com geometria, RecordNumber e atributos consultáveis por nome; conecte ao Street Profile Assignment.", GH_ParamAccess.list);
            pManager.AddTextParameter("CRS", "CRS", "SRS ID declarado pela camada GeoPackage; sem reprojeção automática.", GH_ParamAccess.item);
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

            string gpkgPath = null;
            if (!DA.GetData(0, ref gpkgPath) || string.IsNullOrWhiteSpace(gpkgPath))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Caminho do arquivo .gpkg não fornecido.");
                Message = "Sem Caminho";
                return;
            }

            if (!File.Exists(gpkgPath))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Arquivo .gpkg não encontrado: {gpkgPath}");
                Message = "Arquivo Não Encontrado";
                return;
            }

            string layerName = null;
            DA.GetData(1, ref layerName);

            string filter = null;
            DA.GetData(2, ref filter);

            try
            {
                // Obter todas as camadas disponíveis
                var allLayers = GpkgReader.GetLayers(gpkgPath);
                var layerNamesList = new List<string>();
                foreach (var lay in allLayers)
                {
                    layerNamesList.Add($"{lay.TableName} ({lay.GeometryType})");
                }
                DA.SetDataList(5, layerNamesList);

                // Ler a camada solicitada (ou primeira)
                var features = GpkgReader.ReadGeoPackage(gpkgPath, layerName, out var fieldNames, filter);

                var outCurves = new List<GH_Curve>();
                var outSurfaces = new List<GH_Brep>();
                var outPoints = new List<GH_Point>();
                var attrTree = new GH_Structure<GH_String>();

                for (int i = 0; i < features.Count; i++)
                {
                    var feat = features[i];
                    var path = new GH_Path(i);

                    if (feat.Curves != null)
                    {
                        foreach (var crv in feat.Curves)
                        {
                            if (crv != null && crv.IsValid) outCurves.Add(new GH_Curve(crv));
                        }
                    }

                    if (feat.Surface != null && feat.Surface.IsValid)
                    {
                        outSurfaces.Add(new GH_Brep(feat.Surface));
                    }

                    if (feat.Points != null)
                    {
                        foreach (var pt in feat.Points)
                        {
                            outPoints.Add(new GH_Point(pt));
                        }
                    }

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
                DA.SetDataTree(6, values);
                DA.SetDataTree(7, geometry);
                DA.SetDataList(8, features);
                var selected = allLayers.Find(l => l.TableName.Equals(layerName, StringComparison.OrdinalIgnoreCase) ||
                    l.Identifier.Equals(layerName, StringComparison.OrdinalIgnoreCase)) ?? (allLayers.Count > 0 ? allLayers[0] : null);
                DA.SetData(9, selected == null ? "Unknown" : $"SRS ID={selected.SrsId}");

                Message = $"{features.Count} Feições";
            }
            catch (Exception ex)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Erro ao ler GeoPackage: {ex.Message}");
                Message = "Erro GPKG";
            }
        }
    }
}
