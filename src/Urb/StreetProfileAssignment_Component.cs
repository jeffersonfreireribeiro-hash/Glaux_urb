using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;

namespace Buraqueira_Urb
{
    /// <summary>
    /// Componente central para associar StreetProfile a feições reais de logradouros do GIS.
    /// Faz o match por nome (exato ou normalizado), suporta múltiplos segmentos por rua,
    /// identifica ruas sem perfil (UNMATCHED_STREET) e perfis concorrentes (AMBIGUOUS_PROFILE_MATCH).
    /// Emite ProfiledStreet preservando a estrutura em DataTree, geometria de origem, atributos e StreetID.
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
            // 0: Logradouros (Eixos viários) - Suporte integral a DataTree
            p.AddGenericParameter("Logradouros (GIS)", "Streets",
                "Eixos dos logradouros: árvore/lista de curvas, feições ShpFeature/GpkgFeature, caminhos .shp/.gpkg ou ProfiledStreet. Preserva a estrutura de árvore {feição}.",
                GH_ParamAccess.tree);

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
                "Árvore de objetos ProfiledStreet contendo a via, geometria, identidade estável (StreetID) e o StreetProfile associado.",
                GH_ParamAccess.tree);

            p.AddCurveParameter("Matched Curves", "Matched",
                "Curvas dos eixos que receberam perfil viário com sucesso.",
                GH_ParamAccess.tree);

            p.AddCurveParameter("Unmatched Curves", "Unmatched",
                "Curvas dos eixos que não encontraram nenhum perfil correspondente (UNMATCHED_STREET).",
                GH_ParamAccess.tree);

            p.AddCurveParameter("Ambiguous Curves", "Ambiguous",
                "Curvas dos eixos onde múltiplos perfis competem pelo mesmo nome (AMBIGUOUS_PROFILE_MATCH).",
                GH_ParamAccess.tree);

            p.AddTextParameter("Assignment Report", "Report",
                "Relatório técnico detalhado com diagnósticos de casamento, segmentos por via e conflitos.",
                GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess da)
        {
            // Regra de Ouro: o gatilho Run é sempre o último parâmetro (índice 4)
            bool run = true;
            da.GetData(4, ref run);
            if (!run)
            {
                Message = "Pausado (Run=False)";
                return;
            }

            GH_Structure<IGH_Goo> streetsTree;
            da.GetDataTree(0, out streetsTree);

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

            // 2. Extrair itens GIS de logradouros inspecionando a árvore
            int totalItems, branchCount, nullCount;
            HashSet<string> receivedTypes;
            List<RawGisStreetItem> gisItems;
            try
            {
                gisItems = ExtractRawGisItemsFromTree(streetsTree, nameField, out totalItems, out branchCount, out nullCount, out receivedTypes);
            }
            catch (InvalidOperationException ex)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
                return;
            }

            if (gisItems.Count == 0)
            {
                if (totalItems == 0)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Street Profile Assignment: Streets está vazio (0 itens recebidos).");
                    Message = "Sem Logradouros";
                }
                else
                {
                    string typesStr = receivedTypes.Count > 0 ? string.Join(", ", receivedTypes) : "Nenhum";
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                        $"Street Profile Assignment:\nStreets recebeu {totalItems} item(ns) em {branchCount} branch(es) ({nullCount} nulo(s)), mas nenhum pôde ser convertido para um eixo viário válido.\n\n" +
                        $"Tipos recebidos:\n{typesStr}\n\n" +
                        "Esperado:\nCurve / feição viária GIS válida (Curve, PolylineCurve, Line, ShpFeature, caminhos .shp/.gpkg).");
                    Message = "Eixos Inválidos";
                }
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

            // 4. Alimentar as saídas preservando os caminhos da DataTree
            var profiledTree = new GH_Structure<GH_ObjectWrapper>();
            var matchedTree = new GH_Structure<GH_Curve>();
            var unmatchedTree = new GH_Structure<GH_Curve>();
            var ambiguousTree = new GH_Structure<GH_Curve>();

            GH_Path ParsePath(string pathStr, int fallbackIndex = 0)
            {
                if (!string.IsNullOrEmpty(pathStr))
                {
                    try
                    {
                        string clean = pathStr.Trim('{', '}').Trim();
                        if (!string.IsNullOrEmpty(clean))
                        {
                            var parts = clean.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                            var indices = parts.Select(int.Parse).ToArray();
                            if (indices.Length > 0) return new GH_Path(indices);
                        }
                    }
                    catch { }
                }
                return new GH_Path(fallbackIndex);
            }

            foreach (var profiled in result.Profiled)
            {
                var p = ParsePath(profiled.TreePath);
                profiledTree.Append(new GH_ObjectWrapper(profiled), p);
                if (profiled.SourceGeometry != null && profiled.SourceGeometry.IsValid)
                    matchedTree.Append(new GH_Curve(profiled.SourceGeometry), p);
            }

            foreach (var unmatched in result.UnmatchedItems)
            {
                var p = ParsePath(unmatched.TreePath);
                if (unmatched.Geometry != null && unmatched.Geometry.IsValid)
                    unmatchedTree.Append(new GH_Curve(unmatched.Geometry), p);
            }

            foreach (var ambiguous in result.AmbiguousItems)
            {
                var p = ParsePath(ambiguous.TreePath);
                if (ambiguous.Geometry != null && ambiguous.Geometry.IsValid)
                    ambiguousTree.Append(new GH_Curve(ambiguous.Geometry), p);
            }

            da.SetDataTree(0, profiledTree);
            da.SetDataTree(1, matchedTree);
            da.SetDataTree(2, unmatchedTree);
            da.SetDataTree(3, ambiguousTree);
            da.SetData(4, result.Report);

            Message = $"{result.MatchedCount}/{result.TotalFeatures} casadas";
        }

        public static bool TryExtractCurve(object obj, out Curve curve)
        {
            curve = null;
            if (obj == null) return false;

            while (obj is GH_ObjectWrapper wrapper)
            {
                obj = wrapper.Value;
                if (obj == null) return false;
            }

            if (obj is GH_Curve ghCrv)
            {
                curve = ghCrv.Value;
                return curve != null && curve.IsValid;
            }
            if (obj is Curve crv)
            {
                curve = crv;
                return curve.IsValid;
            }
            if (obj is GH_Line ghLine)
            {
                curve = new LineCurve(ghLine.Value);
                return curve.IsValid;
            }
            if (obj is Line line)
            {
                curve = new LineCurve(line);
                return curve.IsValid;
            }
            if (obj is Polyline pl)
            {
                curve = new PolylineCurve(pl);
                return curve.IsValid;
            }
            if (obj is PolyCurve polyCurve)
            {
                curve = polyCurve;
                return curve.IsValid;
            }
            if (obj is IGH_GeometricGoo geoGoo)
            {
                var geom = geoGoo.ScriptVariable();
                if (geom is Curve c) { curve = c; return curve.IsValid; }
                if (geom is Line l) { curve = new LineCurve(l); return curve.IsValid; }
                if (geom is Polyline p) { curve = new PolylineCurve(p); return curve.IsValid; }
            }
            return false;
        }

        private static Dictionary<string, object> ExtractAttributesFromCurve(Curve curve)
        {
            var attrs = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            if (curve == null) return attrs;

            try
            {
                if (curve.UserDictionary != null && curve.UserDictionary.Count > 0)
                {
                    foreach (var key in curve.UserDictionary.Keys)
                    {
                        attrs[key] = curve.UserDictionary[key];
                    }
                }
            }
            catch { }

            try
            {
                var userStrings = curve.GetUserStrings();
                if (userStrings != null && userStrings.Count > 0)
                {
                    for (int i = 0; i < userStrings.Count; i++)
                    {
                        string key = userStrings.GetKey(i);
                        string val = userStrings.Get(i);
                        if (!string.IsNullOrEmpty(key) && !attrs.ContainsKey(key))
                        {
                            attrs[key] = val;
                        }
                    }
                }
            }
            catch { }

            return attrs;
        }

        private static string ResolveStreetName(Dictionary<string, object> attrs, string userPreference, Curve curve)
        {
            if (attrs != null && attrs.Count > 0)
            {
                string chosenField = TryFindStreetNameField(attrs.Keys.ToList(), userPreference);
                if (!string.IsNullOrEmpty(chosenField) && attrs.TryGetValue(chosenField, out var val))
                {
                    string sVal = val?.ToString()?.Trim();
                    if (!string.IsNullOrWhiteSpace(sVal)) return sVal;
                }
            }

            if (curve != null)
            {
                string name = curve.GetUserString("Name") ??
                              curve.GetUserString("NOME") ??
                              curve.GetUserString("RUA") ??
                              curve.GetUserString("LOGRADOURO");
                if (!string.IsNullOrWhiteSpace(name)) return name.Trim();
            }

            return "";
        }

        private static List<RawGisStreetItem> ExtractRawGisItemsFromTree(
            GH_Structure<IGH_Goo> tree,
            string nameField,
            out int totalItems,
            out int branchCount,
            out int nullCount,
            out HashSet<string> receivedTypes)
        {
            var result = new List<RawGisStreetItem>();
            receivedTypes = new HashSet<string>();
            totalItems = 0;
            branchCount = 0;
            nullCount = 0;

            if (tree == null) return result;

            branchCount = tree.PathCount;
            totalItems = tree.DataCount;

            foreach (var path in tree.Paths)
            {
                var branch = tree[path];
                if (branch == null) continue;

                string pathStr = path.ToString();

                for (int i = 0; i < branch.Count; i++)
                {
                    var goo = branch[i];
                    if (goo == null)
                    {
                        nullCount++;
                        continue;
                    }

                    receivedTypes.Add(goo.GetType().Name);

                    object unwrapped = goo;
                    while (unwrapped is GH_ObjectWrapper wrapper)
                    {
                        unwrapped = wrapper.Value;
                        if (unwrapped != null) receivedTypes.Add(unwrapped.GetType().Name);
                    }

                    if (unwrapped == null)
                    {
                        nullCount++;
                        continue;
                    }

                    // Caso A: ShpFeature (vindo de Features de ShpImport / GpkgImport)
                    if (unwrapped is ShpFeature imported)
                    {
                        var attrs = imported.Attributes ?? new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                        string chosenField = TryFindStreetNameField(attrs.Keys.ToList(), nameField);
                        string streetName = (!string.IsNullOrEmpty(chosenField) && attrs.TryGetValue(chosenField, out var sv))
                            ? sv?.ToString()?.Trim() ?? "" : "";

                        if (imported.Curves != null)
                        {
                            foreach (var c in imported.Curves)
                            {
                                if (c != null && c.IsValid)
                                {
                                    result.Add(new RawGisStreetItem
                                    {
                                        Geometry = c,
                                        Name = streetName,
                                        SourceId = FeatureId(Path.GetFileName(imported.SourcePath ?? "import"), attrs, c, streetName),
                                        FeatureIndex = imported.RecordNumber,
                                        Attributes = attrs,
                                        TreePath = pathStr
                                    });
                                }
                            }
                        }
                        continue;
                    }

                    // Caso B: ProfiledStreet
                    if (unwrapped is ProfiledStreet ps)
                    {
                        result.Add(new RawGisStreetItem
                        {
                            Geometry = ps.SourceGeometry,
                            Name = ps.StreetName,
                            SourceId = ps.StreetID,
                            FeatureIndex = ps.FeatureIndex,
                            Attributes = ps.Attributes,
                            TreePath = !string.IsNullOrEmpty(ps.TreePath) ? ps.TreePath : pathStr
                        });
                        continue;
                    }

                    // Caso C: Caminho de arquivo GIS (.shp ou .gpkg) via GisPathResolver
                    if (GisPathResolver.TryResolveGisPath(unwrapped, out string resolvedPath, out string rawPath))
                    {
                        if (resolvedPath.EndsWith(".shp", StringComparison.OrdinalIgnoreCase))
                        {
                            var feats = ShapefileReader.ReadShapefile(resolvedPath, out var fieldNames);
                            string chosenField = TryFindStreetNameField(fieldNames, nameField);

                            foreach (var f in feats)
                            {
                                string sName = "";
                                if (!string.IsNullOrEmpty(chosenField) && f.Attributes != null && f.Attributes.TryGetValue(chosenField, out var val))
                                {
                                    sName = val?.ToString()?.Trim() ?? "";
                                }

                                if (f.Curves != null)
                                {
                                    foreach (var c in f.Curves)
                                    {
                                        if (c != null && c.IsValid)
                                        {
                                            result.Add(new RawGisStreetItem
                                            {
                                                Geometry = c,
                                                Name = sName,
                                                SourceId = FeatureId(Path.GetFileName(resolvedPath), f.Attributes, c, sName),
                                                FeatureIndex = f.RecordNumber,
                                                Attributes = f.Attributes,
                                                TreePath = pathStr
                                            });
                                        }
                                    }
                                }
                            }
                            continue;
                        }
                        else if (resolvedPath.EndsWith(".gpkg", StringComparison.OrdinalIgnoreCase))
                        {
                            var feats = GpkgReader.ReadGeoPackage(resolvedPath, null, out var fieldNames);
                            string chosenField = TryFindStreetNameField(fieldNames, nameField);

                            foreach (var f in feats)
                            {
                                string sName = "";
                                if (!string.IsNullOrEmpty(chosenField) && f.Attributes != null && f.Attributes.TryGetValue(chosenField, out var val))
                                {
                                    sName = val?.ToString()?.Trim() ?? "";
                                }

                                if (f.Curves != null)
                                {
                                    foreach (var c in f.Curves)
                                    {
                                        if (c != null && c.IsValid)
                                        {
                                            result.Add(new RawGisStreetItem
                                            {
                                                Geometry = c,
                                                Name = sName,
                                                SourceId = FeatureId(Path.GetFileName(resolvedPath), f.Attributes, c, sName),
                                                FeatureIndex = f.RecordNumber,
                                                Attributes = f.Attributes,
                                                TreePath = pathStr
                                            });
                                        }
                                    }
                                }
                            }
                            continue;
                        }
                    }
                    else if (!string.IsNullOrEmpty(rawPath) && GisPathResolver.IsGisExtension(rawPath))
                    {
                        throw new InvalidOperationException($"O arquivo GIS especificado em Streets não foi encontrado no disco: '{rawPath}'. Verifique se o caminho existe e se o drive está montado.");
                    }

                    // Caso E: Curva direta com ou sem UserStrings / atributos
                    if (TryExtractCurve(unwrapped, out Curve directCrv))
                    {
                        var attrs = ExtractAttributesFromCurve(directCrv);
                        string sName = ResolveStreetName(attrs, nameField, directCrv);

                        result.Add(new RawGisStreetItem
                        {
                            Geometry = directCrv,
                            Name = sName,
                            SourceId = FeatureId("GH", attrs, directCrv, sName),
                            FeatureIndex = result.Count + 1,
                            Attributes = attrs,
                            TreePath = pathStr
                        });
                        continue;
                    }
                }
            }

            return result;
        }

        private static string TryFindStreetNameField(List<string> fieldNames, string userPreference)
        {
            if (fieldNames == null || fieldNames.Count == 0) return null;

            if (!string.IsNullOrWhiteSpace(userPreference))
            {
                foreach (var f in fieldNames)
                {
                    if (f.Equals(userPreference, StringComparison.OrdinalIgnoreCase)) return f;
                }
                throw new InvalidOperationException("Street Name Field '" + userPreference + "' não existe no GIS. Colunas disponíveis: " + string.Join(", ", fieldNames));
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

            return null;
        }

        private static string FeatureId(string fileName, Dictionary<string, object> attributes,
            Curve curve, string streetName)
        {
            string id = null;
            if (attributes != null)
            {
                foreach (string key in new[] { "STREET_ID", "ID_LOGRADOURO", "ID_LOG", "COD_LOG", "ID", "FID" })
                {
                    var field = attributes.Keys.FirstOrDefault(x => string.Equals(x, key, StringComparison.OrdinalIgnoreCase));
                    if (field == null) continue;
                    id = Convert.ToString(attributes[field], CultureInfo.InvariantCulture)?.Trim();
                    if (!string.IsNullOrWhiteSpace(id) && id.Any(ch => ch != '*')) break;
                    id = null;
                }
            }
            if (!string.IsNullOrWhiteSpace(id)) return fileName + ":id:" + id;

            // Identificador estável via hash de geometria e nome
            string Forward(bool reverse)
            {
                var sb = new StringBuilder(512);
                sb.Append(StreetNameNormalizer.Normalize(streetName)).Append('|');
                for (int i = 0; i <= 16; i++)
                {
                    double t = (reverse ? 16 - i : i) / 16.0;
                    var pt = curve.PointAtNormalizedLength(t);
                    sb.Append(Math.Round(pt.X, 4).ToString("F4", CultureInfo.InvariantCulture)).Append(',')
                      .Append(Math.Round(pt.Y, 4).ToString("F4", CultureInfo.InvariantCulture)).Append(',')
                      .Append(Math.Round(pt.Z, 4).ToString("F4", CultureInfo.InvariantCulture)).Append(';');
                }
                sb.Append(curve.GetLength().ToString("F4", CultureInfo.InvariantCulture));
                return sb.ToString();
            }

            string a = Forward(false), b = Forward(true);
            byte[] hash;
            using (var sha = SHA256.Create())
            {
                hash = sha.ComputeHash(Encoding.UTF8.GetBytes(string.CompareOrdinal(a, b) <= 0 ? a : b));
            }
            return fileName + ":geom:" + BitConverter.ToString(hash).Replace("-", "").Substring(0, 24);
        }
    }
}
