using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Grasshopper.Kernel;

namespace Buraqueira_Urb
{
    /// <summary>
    /// Componente gerador de gabaritos e arquivos CSV para seções viárias típicas.
    /// Gera modelos prontos em colunas que podem ser conectados diretamente ao RoadCrossSection_Component.
    /// </summary>
    public class RoadCsvTemplate_Component : GH_Component
    {
        public RoadCsvTemplate_Component()
            : base(
                "Road CSV Template Generator",
                "RoadTemplate",
                "Gera gabaritos prontos de seções de via em formato CSV (Via Simples 1 Faixa, Seção da Imagem de Referência, Via com Estacionamento, Avenida com Canteiro Central e Ciclovia).",
                "Glaux Urb",
                "01 | Infraestrutura Viária")
        {
        }

        public override Guid ComponentGuid => new Guid("7b2c3d4e-5f6a-7b8c-9d0e-1f2a3b4c5d6e");

        protected override System.Drawing.Bitmap Icon => GlauxUrbIcons.RoadCsvTemplate;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddIntegerParameter("Preset Index", "Preset", 
                "Índice do gabarito pré-configurado:\n0: Imagem de Referência (Via Local 15m: Passeio 3m + Pista 9m + Passeio 3m)\n1: Via Simples 1 Faixa (Mão Única 3.5m)\n2: Via Local Mão Dupla com Estacionamento\n3: Avenida com Canteiro Central (2.5m) e 4 Faixas\n4: Catálogo de Múltiplas Ruas (Loteamento Completo)\n5: Detalhamento por Partes Individuais (Acera + Sarjeta + Pistas)", 
                GH_ParamAccess.item, 0);
            pManager[0].Optional = true;

            pManager.AddTextParameter("File Save Path", "Path", 
                "Caminho de arquivo opcional para salvar o arquivo .csv no disco (ex: 'C:\\Projetos\\minha_via.csv').", 
                GH_ParamAccess.item);
            pManager[1].Optional = true;

            // Regra de ouro: Write/Run é o último
            pManager.AddBooleanParameter("Save File", "Save", 
                "Se True e o caminho de arquivo for fornecido, grava o arquivo CSV no disco.", 
                GH_ParamAccess.item, false);
            pManager[2].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("CSV Text", "CSV", "Conteúdo do CSV pronto para plugar na entrada 'CSV / Table' da Seção de Via.", GH_ParamAccess.item);
            pManager.AddTextParameter("Headers", "H", "Nomes das partes da seção viária.", GH_ParamAccess.list);
            pManager.AddNumberParameter("Widths", "W", "Larguras numéricas de cada parte correspondente.", GH_ParamAccess.list);
            pManager.AddTextParameter("Saved Path", "File", "Caminho do arquivo gravado no disco (se ativado Save).", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            int preset = 0;
            DA.GetData(0, ref preset);

            string savePath = null;
            DA.GetData(1, ref savePath);

            int lastIdx = Params.Input.Count - 1;
            bool save = false;
            DA.GetData(lastIdx, ref save);

            string header;
            string values;
            var headersList = new List<string>();
            var widthsList = new List<double>();

            switch (preset)
            {
                case 1:
                    // 1: Via Simples 1 Faixa (Mão Única)
                    header = "Calcada_Esq, Sarjeta_Esq, Pista_Unica, Sarjeta_Dir, Calcada_Dir";
                    values = "1.50, 0.25, 3.50, 0.25, 1.50";
                    headersList.AddRange(new[] { "Calcada_Esq", "Sarjeta_Esq", "Pista_Unica", "Sarjeta_Dir", "Calcada_Dir" });
                    widthsList.AddRange(new[] { 1.50, 0.25, 3.50, 0.25, 1.50 });
                    break;

                case 2:
                    // 2: Via Local com Estacionamento
                    header = "Calcada_Esq, Sarjeta_Esq, Estacionamento, Pista_Esq, Pista_Dir, Sarjeta_Dir, Calcada_Dir";
                    values = "2.00, 0.25, 2.20, 3.00, 3.00, 0.25, 2.00";
                    headersList.AddRange(new[] { "Calcada_Esq", "Sarjeta_Esq", "Estacionamento", "Pista_Esq", "Pista_Dir", "Sarjeta_Dir", "Calcada_Dir" });
                    widthsList.AddRange(new[] { 2.00, 0.25, 2.20, 3.00, 3.00, 0.25, 2.00 });
                    break;

                case 3:
                    // 3: Avenida com Canteiro Central e 4 Faixas
                    header = "Calcada_Esq, Ciclovia, Sarjeta_Esq, Pista_Esq, Canteiro_Central, Pista_Dir, Sarjeta_Dir, Calcada_Dir";
                    values = "2.50, 2.00, 0.25, 7.00, 2.50, 7.00, 0.25, 2.50";
                    headersList.AddRange(new[] { "Calcada_Esq", "Ciclovia", "Sarjeta_Esq", "Pista_Esq", "Canteiro_Central", "Pista_Dir", "Sarjeta_Dir", "Calcada_Dir" });
                    widthsList.AddRange(new[] { 2.50, 2.00, 0.25, 7.00, 2.50, 7.00, 0.25, 2.50 });
                    break;

                case 4:
                    // 4: Catálogo de Múltiplas Vias do Loteamento (Prancha Completa com Via Pedonal e 1 Calçada)
                    header = "Nome_Via, Quadra_Esq, Passeio_Esq, Faixa_Rolamento, Passeio_Dir, Quadra_Dir, Tipo";
                    values = "Rua Santo Antônio, Quadra 21, 1.80, 6.00, 1.80, Quadra 21, Mão Dupla\r\n" +
                             "Rua Álvaro Bezerra, Quadra 20, 2.00, 7.00, 2.00, Quadra 21, Mão Dupla\r\n" +
                             "Rua Maria Araújo, Quadra 14, 2.50, 7.00, 2.50, Quadra 20, Mão Dupla\r\n" +
                             "Rua Dom Manuel, Quadra 18, 2.00, 6.00, 2.00, Quadra 19, Mão Dupla\r\n" +
                             "Rua Cordeiro de Miranda, Quadra 17, 1.80, 6.00, 1.80, Quadra 18, Mão Dupla\r\n" +
                             "Rua Virgílio Távora, Quadra 16, 2.00, 7.00, 2.00, Quadra 17, Mão Dupla\r\n" +
                             "Rua Pedonal do Comércio, Quadra 10, 0.00, 8.00, 0.00, Quadra 11, Pedonal\r\n" +
                             "Rua do Parque (1 Calçada), Quadra 5, 0.00, 6.00, 2.50, Quadra 6, Mão Única";
                    headersList.AddRange(new[] { "Nome_Via", "Quadra_Esq", "Passeio_Esq", "Faixa_Rolamento", "Passeio_Dir", "Quadra_Dir", "Tipo" });
                    widthsList.AddRange(new[] { 0.0, 0.0, 1.80, 6.00, 1.80, 0.0, 0.0 });
                    break;

                case 5:
                    // 5: Detalhamento por Partes Individuais (Acera + Cuneta + Pistas)
                    header = "Acera_Esq, Cuneta_Esq, Pista_Esq, Pista_Dir, Cuneta_Dir, Acera_Dir";
                    values = "1.80, 0.25, 3.50, 3.50, 0.25, 1.80";
                    headersList.AddRange(new[] { "Acera_Esq", "Cuneta_Esq", "Pista_Esq", "Pista_Dir", "Cuneta_Dir", "Acera_Dir" });
                    widthsList.AddRange(new[] { 1.80, 0.25, 3.50, 3.50, 0.25, 1.80 });
                    break;

                case 0:
                default:
                    // 0: Imagem de Referência Técnica (Via Local 15.00m: Passeio 3.00m, Pista 9.00m, Passeio 3.00m)
                    header = "Nome_Via, Quadra_Esq, Passeio_Esq, Faixa_Rolamento, Passeio_Dir, Quadra_Dir, Tipo";
                    values = "Via Local 15.00m, Quadra 21, 3.00, 9.00, 3.00, Quadra 21, Mão Dupla";
                    headersList.AddRange(new[] { "Nome_Via", "Quadra_Esq", "Passeio_Esq", "Faixa_Rolamento", "Passeio_Dir", "Quadra_Dir", "Tipo" });
                    widthsList.AddRange(new[] { 0.0, 0.0, 3.00, 9.00, 3.00, 0.0, 0.0 });
                    break;
            }

            string csvContent = $"{header}\r\n{values}";

            string savedResult = "Não salvo em disco";
            if (save && !string.IsNullOrWhiteSpace(savePath))
            {
                try
                {
                    string dir = Path.GetDirectoryName(savePath);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }
                    File.WriteAllText(savePath, csvContent, Encoding.UTF8);
                    savedResult = savePath;
                }
                catch (Exception ex)
                {
                    savedResult = $"Erro ao salvar: {ex.Message}";
                }
            }

            DA.SetData(0, csvContent);
            DA.SetDataList(1, headersList);
            DA.SetDataList(2, widthsList);
            DA.SetData(3, savedResult);

            Message = $"Preset {preset}";
        }
    }
}
