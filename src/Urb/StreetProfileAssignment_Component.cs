using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;

namespace Buraqueira_Urb
{
    /// <summary>
    /// Componente central para associar StreetProfile a feições reais de logradouros do GIS.
    /// Faz o match por nome (exato ou normalizado), suporta múltiplos segmentos por rua,
    /// identifica ruas sem perfil (UNMATCHED_STREET) e perfis concorrentes (AMBIGUOUS_PROFILE_MATCH).
    /// Emite ProfiledStreet com StreetID estável, geometria de origem e o perfil acoplado.
    /// </summary>
    public sealed class StreetProfileAssignment_Component : GH_Component
    {
        public StreetProfileAssignment_Component() : base(
            "Street Profile Assignment", "ProfileAssign",
            "Associa perfis viários (StreetProfile) aos eixos de logradouros do GIS por nome de atributo, preservando geometria, segmentos e identificadores.",
            "Glaux Urb", "01 | Infraestrutura Viária") { }

        public override Guid ComponentGuid => new Guid("d3e4f5a6-b7c8-4d9e-0f1a-2b3c4d5e6f7a");

        protected override System.Drawing.Bitmap Icon => GlauxUrbIcons.StreetProfileAssignment;

        protected override void RegisterInputParams(GH_InputParamManager p)
        {
            // 0: Logradouros (Eixos viários)
            p.AddGenericParameter("Logradouros (GIS)", "Streets",
                "Eixos dos logradouros: caminho .shp/.gpkg, curvas do Rhino ou saída Features de Import Shapefile/GeoPackage. O nome é lido do atributo NameField, sem parsing de strings de painel.",
                GH_ParamAccess.list);

            // 1: Street Profiles (Definições de perfil)
            p.AddGenericParameter("Street Profiles", "Profiles",
                "Um ou vários objetos StreetProfile configurados no Street Profile Definition.",
                GH_ParamAccess.list);

            // 2: Street Name Field (Coluna do DBF/GPKG)
            p.AddTextParameter("Street Name Field", "NameField",
                "Nome da coluna na tabela de atributos com o nome do logradouro (opcional; se vazio busca 'NOME_LOG', 'NOME', 'LOGRADOURO', 'RUA', etc.).",
                GH_ParamAccess.item, "");
            p[2].Optional = true;

            // 3: Matching Mode (Normalizado vs Exato)
            p.AddIntegerParameter("Matching Mode", "Mode",
                "Modo de comparação de nomes: 0 = Normalizado (ignora maiúsculas/minúsculas e múltiplos espaços); 1 = Exato.",
                GH_ParamAccess.item, 0);
            p[3].Optional = true;

            // 4: REGRA DE OURO: 'Run' deve ser SEMPRE o último parâmetro de entrada!
            p.AddBooleanParameter("Run", "Run",
                "Ativa o casamento dos perfis com as vias (Padrão: True); última entrada.",
                GH_ParamAccess.item, true);
            p[4].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager p)
        {
            p.AddGenericParameter("Profiled Streets", "Profiled",
                "Lista de objetos ProfiledStreet contendo a via, geometria, identidade estável (StreetID) e o StreetProfile associado.",
                GH_ParamAccess.list);

            p.AddCurveParameter("Matched Curves", "Matched",
                "Curvas dos eixos que receberam perfil viário com sucesso.",
                GH_ParamAccess.list);

            p.AddCurveParameter("Unmatched Curves", "Unmatched",
                "Curvas dos eixos que não encontraram nenhum perfil correspondente (UNMATCHED_STREET).",
                GH_ParamAccess.list);

            p.AddCurveParameter("Ambiguous Curves", "Ambiguous",
                "Curvas dos eixos onde múltiplos perfis competem pelo mesmo nome (AMBIGUOUS_PROFILE_MATCH).",
                GH_ParamAccess.list);

            p.AddTextParameter("Assignment Report", "Report",
                "Relatório técnico detalhado com diagnósticos de casamento, segmentos por via e conflitos.",
                GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess da)
        {
            // Regra de Ouro: o gatilho Run é sempre o último parâmetro
            bool run = true;
            da.GetData(4, ref run);
            if (!run)
            {
                Message = "Pausado (Run=False)";
                return;
            }

            var streetsInput = new List<object>();
            da.GetDataList(0, streetsInput);

            var profilesInput = new List<object>();
            da.GetDataList(1, profilesInput);

            string nameField = "";
            da.GetData(2, ref nameField);

            int mode = 0;
            da.GetData(3, ref mode);
            bool normalized = mode == 0;

            // 1. Extrair os perfis
            var profiles = new List<StreetProfile>();
            foreach (var item in profilesInput)
            {
                if (item == null) continue;
                object unwrapped = item;
                while (unwrapped is GH_ObjectWrapper wrapper) unwrapped = wrapper.Value;

                if (unwrapped is StreetProfile sp)
                {
                    profiles.Add(sp);
                }
            }

            if (profiles.Count == 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Nenhum StreetProfile válido foi conectado em Profiles.");
                Message = "Sem Perfis";
                return;
            }

            // 2. Extrair itens GIS de logradouros
            List<RawGisStreetItem> gisItems;
            try { gisItems = ExtractRawGisItems(streetsInput, nameField); }
            catch (InvalidOperationException ex)
            { AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message); return; }
            if (gisItems.Count == 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Nenhum eixo de logradouro válido foi fornecido em Streets.");
                Message = "Sem Logradouros";
                return;
            }

            // 3. Executar o casamento no serviço puro
            var result = StreetProfileAssignmentService.Assign(gisItems, profiles, normalized);

            // Avisos informativos
            if (result.AmbiguousCount > 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                    $"{result.AmbiguousCount} vias encontraram perfis ambíguos concorrentes (consulte Ambiguous e Report).");
            }
            if (result.UnmatchedCount > 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Remark,
                    $"{result.UnmatchedCount} vias do GIS não possuem perfil associado (consulte Unmatched).");
            }

            // 4. Alimentar as saídas
            var wrappedProfiled = result.Profiled.Select(x => new GH_ObjectWrapper(x)).ToList();
            da.SetDataList(0, wrappedProfiled);
            da.SetDataList(1, result.MatchedCurves);
            da.SetDataList(2, result.UnmatchedCurves);
            da.SetDataList(3, result.AmbiguousCurves);
            da.SetData(4, result.Report);

            Message = $"{result.MatchedCount}/{result.TotalFeatures} casadas";
        }

        private static List<RawGisStreetItem> ExtractRawGisItems(List<object> inputList, string nameField)
        {
            var result = new List<RawGisStreetItem>();
            if (inputList == null) return result;

            for (int itemIdx = 0; itemIdx < inputList.Count; itemIdx++)
            {
                var item = inputList[itemIdx];
                if (item == null) continue;

                object unwrapped = item;
                while (unwrapped is GH_ObjectWrapper wrapper) unwrapped = wrapper.Value;

                // Import SHP/GPKG exposes a common feature object. Resolve names
                // from its typed attribute map, never by parsing panel strings.
                if (unwrapped is ShpFeature imported)
                {
                    string chosenField = FindStreetNameField(imported.Attributes?.Keys.ToList() ?? new List<string>(), nameField);
                    string streetName = chosenField == null ? "" : imported.GetAttributeString(chosenField);
                    if (imported.Curves != null)
                        foreach (var curve in imported.Curves)
                            if (curve != null && curve.IsValid)
                                result.Add(new RawGisStreetItem
                                {
                                    Geometry = curve,
                                    Name = streetName,
                                    SourceId = FeatureId(Path.GetFileName(imported.SourcePath ?? "import"), imported.Attributes, curve, streetName),
                                    FeatureIndex = imported.RecordNumber,
                                    Attributes = imported.Attributes
                                });
                    continue;
                }

                // Caso A: Se já é ProfiledStreet
                if (unwrapped is ProfiledStreet ps)
                {
                    result.Add(new RawGisStreetItem
                    {
                        Geometry = ps.SourceGeometry,
                        Name = ps.StreetName,
                        SourceId = ps.StreetID,
                        FeatureIndex = ps.FeatureIndex,
                        Attributes = ps.Attributes
                    });
                    continue;
                }

                // Caso B: Se é caminho para arquivo Shapefile (.shp)
                if (unwrapped is string pathStr && File.Exists(pathStr) && pathStr.EndsWith(".shp", StringComparison.OrdinalIgnoreCase))
                {
                    var feats = ShapefileReader.ReadShapefile(pathStr, out var fieldNames);
                    string chosenField = FindStreetNameField(fieldNames, nameField);

                    foreach (var f in feats)
                    {
                        string sName = "";
                        if (!string.IsNullOrEmpty(chosenField) && f.Attributes != null && f.Attributes.TryGetValue(chosenField, out var val))
                        {
                            sName = val?.ToString()?.Trim();
                        }
                        // A missing cadastral name must remain unmatched, not acquire
                        // an invented Rua_N value that could match a real profile.

                        if (f.Curves != null)
                        {
                            for (int cIdx = 0; cIdx < f.Curves.Count; cIdx++)
                            {
                                var c = f.Curves[cIdx];
                                if (c != null && c.IsValid)
                                {
                                    string segmentId = FeatureId(Path.GetFileName(pathStr),f.Attributes,c,sName);
                                    result.Add(new RawGisStreetItem
                                    {
                                        Geometry = c,
                                        Name = sName,
                                        SourceId = segmentId,
                                        FeatureIndex = f.RecordNumber,
                                        Attributes = f.Attributes
                                    });
                                }
                            }
                        }
                    }
                    continue;
                }

                // Caso C: Se é caminho para arquivo GeoPackage (.gpkg)
                if (unwrapped is string gpkgPath && File.Exists(gpkgPath) && gpkgPath.EndsWith(".gpkg", StringComparison.OrdinalIgnoreCase))
                {
                    var feats = GpkgReader.ReadGeoPackage(gpkgPath, null, out var fieldNames);
                    string chosenField = FindStreetNameField(fieldNames, nameField);

                    foreach (var f in feats)
                    {
                        string sName = "";
                        if (!string.IsNullOrEmpty(chosenField) && f.Attributes != null && f.Attributes.TryGetValue(chosenField, out var val))
                        {
                            sName = val?.ToString()?.Trim();
                        }
                        // Preserve unnamed features for UNMATCHED_STREET diagnostics.

                        if (f.Curves != null)
                        {
                            for (int cIdx = 0; cIdx < f.Curves.Count; cIdx++)
                            {
                                var c = f.Curves[cIdx];
                                if (c != null && c.IsValid)
                                {
                                    string segmentId = FeatureId(Path.GetFileName(gpkgPath),f.Attributes,c,sName);
                                    result.Add(new RawGisStreetItem
                                    {
                                        Geometry = c,
                                        Name = sName,
                                        SourceId = segmentId,
                                        FeatureIndex = f.RecordNumber,
                                        Attributes = f.Attributes
                                    });
                                }
                            }
                        }
                    }
                    continue;
                }

                // Caso D: Curva direta do Rhino/Grasshopper
                Curve directCrv = null;
                if (unwrapped is GH_Curve ghCrv) directCrv = ghCrv.Value;
                else if (unwrapped is Curve crv) directCrv = crv;

                if (directCrv != null && directCrv.IsValid)
                {
                    string name = directCrv.GetUserString("Name") ??
                                  directCrv.GetUserString("NOME") ??
                                  directCrv.GetUserString("RUA") ??
                                  directCrv.GetUserString("LOGRADOURO");
                    // A direct curve without a name remains unmatched.

                    result.Add(new RawGisStreetItem
                    {
                        Geometry = directCrv,
                        Name = name,
                        SourceId = FeatureId("GH",null,directCrv,name),
                        FeatureIndex = result.Count + 1
                    });
                }
            }

            return result;
        }

        private static string FindStreetNameField(List<string> fieldNames, string userPreference)
        {
            if (fieldNames == null || fieldNames.Count == 0)
                throw new InvalidOperationException("O GIS não contém colunas de atributos para localizar o nome do logradouro.");

            if (!string.IsNullOrWhiteSpace(userPreference))
            {
                foreach (var f in fieldNames)
                {
                    if (f.Equals(userPreference, StringComparison.OrdinalIgnoreCase)) return f;
                }
                throw new InvalidOperationException("Street Name Field '"+userPreference+"' não existe no GIS. Colunas: "+string.Join(", ",fieldNames));
            }

            string[] commonNames = { "NOME_LOG", "NOME", "LOGRADOURO", "RUA", "NM_LOG", "DS_NOME", "NM_TITULO", "STREET", "NAME" };
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

            throw new InvalidOperationException("Nenhuma coluna de nome de logradouro reconhecida. Informe Street Name Field explicitamente.");
        }

        private static string FeatureId(string fileName,Dictionary<string,object> attributes,
            Curve curve,string streetName)
        {
            string id=null;
            if(attributes!=null)
                foreach(string key in new[]{"STREET_ID","ID_LOGRADOURO","ID_LOG","COD_LOG","ID","FID"})
                {
                    var field=attributes.Keys.FirstOrDefault(x=>string.Equals(x,key,StringComparison.OrdinalIgnoreCase));
                    if(field==null)continue;
                    id=Convert.ToString(attributes[field],System.Globalization.CultureInfo.InvariantCulture)?.Trim();
                    if(!string.IsNullOrWhiteSpace(id) && id.Any(ch=>ch!='*'))break;
                    id=null;
                }
            if(!string.IsNullOrWhiteSpace(id))return fileName+":id:"+id;
            // Stable under feature-list reorder. Geometry edits intentionally produce
            // a new identity when no durable cadastral attribute exists.
            string Forward(bool reverse)
            {
                var sb=new StringBuilder(512);
                sb.Append(StreetNameNormalizer.Normalize(streetName)).Append('|');
                for(int i=0;i<=16;i++)
                {
                    double t=(reverse?16-i:i)/16.0;
                    var pt=curve.PointAtNormalizedLength(t);
                    sb.Append(Math.Round(pt.X,4).ToString("F4",CultureInfo.InvariantCulture)).Append(',')
                      .Append(Math.Round(pt.Y,4).ToString("F4",CultureInfo.InvariantCulture)).Append(',')
                      .Append(Math.Round(pt.Z,4).ToString("F4",CultureInfo.InvariantCulture)).Append(';');
                }
                sb.Append(curve.GetLength().ToString("F4",CultureInfo.InvariantCulture));
                return sb.ToString();
            }
            string a=Forward(false),b=Forward(true);
            byte[] hash;
            using(var sha=SHA256.Create())hash=sha.ComputeHash(Encoding.UTF8.GetBytes(
                string.CompareOrdinal(a,b)<=0?a:b));
            return fileName+":geom:"+BitConverter.ToString(hash).Replace("-","").Substring(0,24);
        }
    }
}
