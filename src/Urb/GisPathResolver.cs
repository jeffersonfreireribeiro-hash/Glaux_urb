using System;
using System.IO;
using Grasshopper.Kernel.Types;

namespace Buraqueira_Urb
{
    /// <summary>
    /// Utilitário central para resolução transparente de caminhos de arquivos GIS (.shp e .gpkg),
    /// suportando GH_String, System.String, GH_ObjectWrapper, limpeza de aspas e aliasing de letras de unidade (G: vs H:) no Google Drive.
    /// </summary>
    public static class GisPathResolver
    {
        /// <summary>
        /// Tenta extrair e resolver um caminho de arquivo GIS existente no disco a partir de qualquer objeto do Grasshopper.
        /// </summary>
        /// <param name="obj">Objeto vindo de GH_Structure, GH_Goo, string ou wrapper.</param>
        /// <param name="resolvedPath">Caminho canônico existente no disco (com fallback de unidade se aplicável).</param>
        /// <param name="rawPath">Caminho original fornecido como texto (limpo de aspas e espaços).</param>
        /// <returns>True se for um arquivo existente com extensão GIS válida (.shp ou .gpkg); False caso contrário.</returns>
        public static bool TryResolveGisPath(object obj, out string resolvedPath, out string rawPath)
        {
            resolvedPath = null;
            rawPath = null;
            if (obj == null) return false;

            object unwrapped = obj;
            while (unwrapped is GH_ObjectWrapper wrapper)
            {
                unwrapped = wrapper.Value;
                if (unwrapped == null) return false;
            }

            if (unwrapped is GH_String ghStr) rawPath = ghStr.Value;
            else if (unwrapped is string s) rawPath = s;
            else return false;

            if (string.IsNullOrWhiteSpace(rawPath)) return false;
            rawPath = rawPath.Trim().Trim('"', '\'');

            // Testa primeiro o caminho exato fornecido
            if (File.Exists(rawPath))
            {
                resolvedPath = rawPath;
                return IsGisExtension(resolvedPath);
            }

            // Fallback de alias de unidades de nuvem no Windows (especialmente Google Drive G: vs H:)
            string altPath = TryResolveDriveAlias(rawPath);
            if (!string.IsNullOrEmpty(altPath) && File.Exists(altPath))
            {
                resolvedPath = altPath;
                return IsGisExtension(resolvedPath);
            }

            return false;
        }

        /// <summary>
        /// Verifica se a extensão é uma fonte vetorial GIS suportada (.shp ou .gpkg).
        /// </summary>
        public static bool IsGisExtension(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            return path.EndsWith(".shp", StringComparison.OrdinalIgnoreCase) ||
                   path.EndsWith(".gpkg", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Tenta mapear caminhos absolutos com letras de disco alternativas usuais no Google Drive Desktop (G: <-> H:).
        /// </summary>
        public static string TryResolveDriveAlias(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || path.Length < 3) return null;
            if (path[1] == ':' && (path[2] == '\\' || path[2] == '/'))
            {
                char drive = char.ToUpperInvariant(path[0]);
                if (drive == 'G' || drive == 'H')
                {
                    char altDrive = drive == 'G' ? 'H' : 'G';
                    string alt = altDrive + path.Substring(1);
                    if (File.Exists(alt)) return alt;
                }
            }
            return null;
        }
    }
}
