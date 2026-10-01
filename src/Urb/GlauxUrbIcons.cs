using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;

namespace Buraqueira_Urb
{
    /// <summary>
    /// Gerador e cache centralizado de ícones para o plugin Buraqueira Urb.
    /// Utiliza o mascote oficial (Corujinha Buraqueira com Mapinha Urbano) para a aba e desenha ícones vetoriais em 24x24 px.
    /// </summary>
    public static class GlauxUrbIcons
    {
        // ==========================================
        // MASCOTE OFICIAL BURAQUEIRA URB (48x48 Base64)
        // Corujinha Buraqueira segurando o Mapinha Urbano com rio, ruas, avenida e pino GPS
        // ==========================================
        private const string BURAQUEIRA_URB_ICON_B64 =
            "iVBORw0KGgoAAAANSUhEUgAAADAAAAAwCAYAAABXAvmHAAAEa0lEQVR4nNVYW0hUQRj+dzMi6yFrCUQqfejqW1aQQmBlF3vQl4VaIxV8KbEwEoqCCIqCIomoXgKtzIRA9KG1GwmBClo9GOsliUSRLmhKF0MkjP/szjpnzpw5M7PnVH5wOGf+ncv3/f/MPzMLMMfhc7vD9KXJM6LfB79Oujqmz23SlfkbhHWrwz2uivEl0jh3zfKZD6M/oLunE2pOlMTtTZ1DULhlpaU+a6+OickILIbWd1+0uPgT8TqSR48XbNtu+q355Ys4YRqEPLF393Qab+xHFz5dzyOZ0iu1Wp7n2avDPVpTyq/jeTKorudROGuvzN/gmAB48ClPm7Gf8P1zJO75XTs3m+o8fd4l9DyvfmkskkfvdUHGskWuZyqTABn01h423tcObjIe2uaEdMUoSCvFjkmKpL249/id+HfL1WJYV3xT2E/fnSOWNmz0dNeDowD0JnoSHywbEemF+ENsPG/LtOmtPWyMoRIF7TSK+NA2CbBuxniW5QSiZcrTvG9RGx0oTSESbpwCODASYDHWNgoZOckQqZpvsmdennZs00L1LzuFksBjvOmb9rR/P8xx+GUr0iHFUGPIMfSiqUBDpc2gQgZSigCd/hCEEHmwTDCUGuL2IWrDG8PVNRDzjJHiiMcycvg5nUYgN2qXaTOomP+1NgvM1zy7rPfshK4vuaXMR7kBbjTkG3dkmjSe6+lDHQty+KOPzy1Xi03np2N1r7yLAE2e4P2nCXg79M1C/MzBQjhf12TpA+2kPoJ3zFYR4UuEPH3qtPO6HTAahRzyqiJc2QdYz/PA2lUFexYBcjnRRQ11o/MsAqLpo+p5J7vKmAgplWxnZZkD8e/fpW0wrybHVBaBrTuPKt+OrFaOgmMEnM7muh6WQbrEvSDhRbyxvMZUDlbcMJ7+lDzj7TWSRD/uDkZ33PCU2V4GA7Di8nwYrpo9KpNy8/3rproFRRWmcuPG2bqNHeMQpNqGp6JX1fwFsxubE3x2pBHXqy84dtDeFYH9+7Za7A2POrh2tk725kyLvaLytG2bJw/Nxw1uBGSI60BGlGh8nrA5f6FJctO7uh73TMCh/mS4u1buXwOWKJIvuHEOfsUSUXP52bidV18XfhF5+q0KJI9Y8uyZqYzEWfLtXRHT40oE0POsCDYaaU9XGGnwdx2vhyjJiby8uCU4vsdUNwgAIzBsyUQ8EbxsJRRAE6aFEBuSdwNpVD8ju4aFZLUXMR0NdkC7faA5JboGCBaGH1sWdQO1D2C/rFOIoIQFEBH0ADtWjcFdmBRmIVy4KALJO2GEIcsKshMjJYDbUb9zFkIbZiFanGwWGqEIi6arowA7L8imV/T+fod9oZ1ZtOwaEE0lWwEy4XML2V5kISSNIrwmz4NrWUhEnhysDoRCliMzwYP6esvx2m0k0YVAIPWk7NH2dWvTqdhbOEBBEVxkRREcCIVM/cucgpHj6OjHS1L3AZro30BWbqFJLCsMxbL3AYsAXhT+NbIoYY4CEP+jCAQ9dQj+ABuG6kajwt7/AAAAAElFTkSuQmCC";

        private static Bitmap _pluginTabIcon;
        public static Bitmap PluginTabIcon
        {
            get
            {
                if (_pluginTabIcon == null)
                {
                    try
                    {
                        byte[] bytes = Convert.FromBase64String(BURAQUEIRA_URB_ICON_B64);
                        using (var ms = new System.IO.MemoryStream(bytes))
                        {
                            _pluginTabIcon = new Bitmap(ms);
                        }
                    }
                    catch
                    {
                        _pluginTabIcon = DrawRoadCrossSection();
                    }
                }
                return _pluginTabIcon;
            }
        }

        // ==========================================
        // ÍCONES DOS COMPONENTES (24x24 px)
        // ==========================================
        private static Bitmap _roadCrossSection;
        public static Bitmap RoadCrossSection => _roadCrossSection ?? (_roadCrossSection = DrawRoadCrossSection());

        private static Bitmap _roadCsvTemplate;
        public static Bitmap RoadCsvTemplate => _roadCsvTemplate ?? (_roadCsvTemplate = DrawRoadCsvTemplate());

        private static Bitmap _shpImport;
        public static Bitmap ShpImport => _shpImport ?? (_shpImport = DrawShpImport());

        private static Bitmap _gpkgImport;
        public static Bitmap GpkgImport => _gpkgImport ?? (_gpkgImport = DrawGpkgImport());

        private static Bitmap _streetGisExport;
        public static Bitmap StreetGisExport => _streetGisExport ?? (_streetGisExport = DrawStreetGisExport());

        private static Bitmap DrawStreetGisExport()
        {
            var bmp = new Bitmap(24, 24, PixelFormat.Format32bppArgb);
            using (var g = InitGfx(bmp))
            using (var road = new Pen(Color.FromArgb(65, 82, 96), 4f))
            using (var edge = new Pen(Color.FromArgb(128, 146, 158), 1f))
            using (var arrow = new Pen(Color.FromArgb(226, 102, 32), 2f))
            using (var disk = new SolidBrush(Color.FromArgb(67, 160, 71)))
            {
                g.DrawLine(edge, 2, 3, 2, 15); g.DrawLine(edge, 8, 3, 8, 15);
                g.DrawLine(road, 5, 3, 5, 15);
                g.DrawLine(arrow, 10, 10, 15, 10);
                g.DrawLine(arrow, 13, 8, 15, 10); g.DrawLine(arrow, 13, 12, 15, 10);
                g.FillEllipse(disk, 16, 4, 7, 5); g.FillRectangle(disk, 16, 6, 7, 11);
                g.FillEllipse(disk, 16, 14, 7, 5);
            }
            return bmp;
        }

        private static Bitmap _roadTransversals;
        public static Bitmap RoadTransversals => _roadTransversals ?? (_roadTransversals = DrawRoadTransversals());

        private static Bitmap _streetProfileDefinition;
        public static Bitmap StreetProfileDefinition => _streetProfileDefinition ?? (_streetProfileDefinition = DrawStreetProfileDefinition());

        private static Bitmap _streetProfileFitting;
        public static Bitmap StreetProfileFitting => _streetProfileFitting ?? (_streetProfileFitting = DrawStreetProfileFitting());

        private static Bitmap _streetProfileAssignment;
        public static Bitmap StreetProfileAssignment => _streetProfileAssignment ?? (_streetProfileAssignment = DrawStreetProfileAssignment());

        private static Bitmap _sidewalkRegularization;
        public static Bitmap SidewalkRegularization => _sidewalkRegularization ?? (_sidewalkRegularization = DrawSidewalkRegularization());

        private static Graphics InitGfx(Bitmap bmp)
        {
            var g = Graphics.FromImage(bmp);
            g.Clear(Color.Transparent);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            return g;
        }

        /// <summary>
        /// Ícone da Seção de Via: Perfil transversal com pista abaulada (bombeo), sarjeta, meio-fio, calçada e camadas estruturais.
        /// Fundo 100% transparente.
        /// </summary>
        private static Bitmap DrawRoadCrossSection()
        {
            var bmp = new Bitmap(24, 24, PixelFormat.Format32bppArgb);
            using (var g = InitGfx(bmp))
            {
                // 1. Camada de Subleito / Terreno (Marrom terra)
                using (var bMej = new SolidBrush(Color.FromArgb(195, 140, 110)))
                using (var pMej = new Pen(Color.FromArgb(145, 95, 75), 1f))
                {
                    g.FillRectangle(bMej, 1, 18, 22, 4);
                    g.DrawRectangle(pMej, 1, 18, 21, 4);
                }

                // 2. Camada de Sub-base e Base (Amarelo ocre / brita)
                using (var bBase = new SolidBrush(Color.FromArgb(235, 195, 130)))
                using (var pBase = new Pen(Color.FromArgb(185, 150, 95), 1f))
                {
                    g.FillRectangle(bBase, 2, 14, 20, 4);
                    g.DrawRectangle(pBase, 2, 14, 19, 4);
                }

                // 3. Calçada Esquerda e Direita (Cinza concreto)
                using (var bConc = new SolidBrush(Color.FromArgb(215, 220, 225)))
                using (var pConc = new Pen(Color.FromArgb(100, 110, 120), 1f))
                {
                    // Calçada Esq
                    g.FillRectangle(bConc, 1, 9, 4, 5);
                    g.DrawRectangle(pConc, 1, 9, 4, 5);

                    // Calçada Dir
                    g.FillRectangle(bConc, 19, 9, 4, 5);
                    g.DrawRectangle(pConc, 19, 9, 4, 5);
                }

                // 4. Meio-fio e Sarjeta (Desnível em degrau)
                using (var pCurb = new Pen(Color.FromArgb(70, 80, 90), 1f))
                {
                    g.DrawLine(pCurb, 5, 9, 5, 12);
                    g.DrawLine(pCurb, 5, 12, 7, 12); // Sarjeta Esq
                    
                    g.DrawLine(pCurb, 19, 9, 19, 12);
                    g.DrawLine(pCurb, 17, 12, 19, 12); // Sarjeta Dir
                }

                // 5. Pista de Asfalto com Abaulamento (Bombeo no centro)
                PointF[] asphaltPoly = {
                    new PointF(7, 12),
                    new PointF(12, 10), // Greide central mais alto
                    new PointF(17, 12),
                    new PointF(17, 14),
                    new PointF(7, 14)
                };

                using (var bAsf = new SolidBrush(Color.FromArgb(45, 50, 55)))
                using (var pAsf = new Pen(Color.FromArgb(25, 30, 35), 1f))
                {
                    g.FillPolygon(bAsf, asphaltPoly);
                    g.DrawPolygon(pAsf, asphaltPoly);
                }

                // Linha pontilhada do eixo central / coroa
                using (var pCl = new Pen(Color.FromArgb(245, 158, 11), 1f) { DashStyle = DashStyle.Dash })
                {
                    g.DrawLine(pCl, 12, 2, 12, 22);
                }

                // Setas indicativas de caimento (bombeo)
                using (var pSlope = new Pen(Color.FromArgb(41, 128, 185), 1.1f))
                {
                    // Seta esq <-
                    g.DrawLine(pSlope, 9, 7, 11, 7);
                    g.DrawLine(pSlope, 9, 7, 10, 6);
                    // Seta dir ->
                    g.DrawLine(pSlope, 13, 7, 15, 7);
                    g.DrawLine(pSlope, 15, 7, 14, 6);
                }
            }
            return bmp;
        }

        /// <summary>
        /// Ícone do Gerador de Gabarito CSV: Planilha/tabela com seção de rua e ícone CSV.
        /// Fundo 100% transparente.
        /// </summary>
        private static Bitmap DrawRoadCsvTemplate()
        {
            var bmp = new Bitmap(24, 24, PixelFormat.Format32bppArgb);
            using (var g = InitGfx(bmp))
            {
                // Folha de planilha com fundo branco próprio
                using (var sheetBrush = new SolidBrush(Color.White))
                using (var sheetPen = new Pen(Color.FromArgb(46, 125, 50), 1.2f))
                {
                    g.FillRectangle(sheetBrush, 2, 1, 19, 21);
                    g.DrawRectangle(sheetPen, 2, 1, 19, 21);

                    // Cabeçalho verde de planilha
                    using (var hBrush = new SolidBrush(Color.FromArgb(46, 125, 50)))
                    {
                        g.FillRectangle(hBrush, 2, 1, 19, 6);
                    }
                }

                // Linhas e colunas de grade da tabela
                using (var gridPen = new Pen(Color.FromArgb(200, 220, 205), 1f))
                {
                    g.DrawLine(gridPen, 8, 7, 8, 22);
                    g.DrawLine(gridPen, 15, 7, 15, 22);
                    g.DrawLine(gridPen, 2, 12, 21, 12);
                    g.DrawLine(gridPen, 2, 16, 21, 16);
                }

                // Mini perfil de rua na base
                using (var pRoad = new Pen(Color.FromArgb(245, 158, 11), 1.5f))
                {
                    g.DrawLine(pRoad, 4, 19, 11, 16);
                    g.DrawLine(pRoad, 11, 16, 18, 19);
                }

                // Badge "CSV"
                using (var font = new Font(FontFamily.GenericSansSerif, 5.5f, FontStyle.Bold))
                using (var textBrush = new SolidBrush(Color.White))
                {
                    g.DrawString("CSV", font, textBrush, 3, 1);
                }
            }
            return bmp;
        }

        /// <summary>
        /// Ícone do Visualizador de Seção Viária com Cores Reais.
        /// Fundo 100% transparente.
        /// </summary>
        public static Bitmap RoadDisplay => _roadDisplay ?? (_roadDisplay = DrawRoadDisplay());
        private static Bitmap _roadDisplay;

        private static Bitmap DrawRoadDisplay()
        {
            var bmp = new Bitmap(24, 24, PixelFormat.Format32bppArgb);
            using (var g = InitGfx(bmp))
            {
                // Camadas de cores (paleta viária)
                using (var bAsf = new SolidBrush(Color.FromArgb(55, 58, 62)))
                    g.FillRectangle(bAsf, 2, 2, 20, 4);

                using (var bSw = new SolidBrush(Color.FromArgb(220, 225, 230)))
                    g.FillRectangle(bSw, 2, 6, 20, 4);

                using (var bTerr = new SolidBrush(Color.FromArgb(220, 205, 180)))
                    g.FillRectangle(bTerr, 2, 10, 20, 4);

                using (var bMed = new SolidBrush(Color.FromArgb(110, 180, 90)))
                    g.FillRectangle(bMed, 2, 14, 20, 4);

                using (var pBorder = new Pen(Color.FromArgb(70, 80, 95), 1.2f))
                {
                    g.DrawRectangle(pBorder, 2, 2, 20, 16);
                }

                // Ícone de Olho / Visualização estilizado sobre a paleta
                using (var scleraBrush = new SolidBrush(Color.White))
                {
                    g.FillEllipse(scleraBrush, 10, 9, 12, 10);
                }
                using (var eyePen = new Pen(Color.FromArgb(40, 50, 60), 1.3f))
                {
                    g.DrawEllipse(eyePen, 10, 9, 12, 10);
                }
                using (var irisBrush = new SolidBrush(Color.FromArgb(41, 128, 185)))
                {
                    g.FillEllipse(irisBrush, 13, 11, 6, 6);
                }
                using (var pupilBrush = new SolidBrush(Color.FromArgb(20, 25, 30)))
                {
                    g.FillEllipse(pupilBrush, 15, 13, 2, 2);
                }
            }
            return bmp;
        }

        /// <summary>
        /// Ícone do Importador Shapefile (SHP): Polígono vetorial urbano com nós de topografia e eixo viário em curva.
        /// Fundo 100% transparente (sem tarja preta).
        /// </summary>
        private static Bitmap DrawShpImport()
        {
            var bmp = new Bitmap(24, 24, PixelFormat.Format32bppArgb);
            using (var g = InitGfx(bmp))
            {
                // Polígono de quadra em perspectiva (azul técnico urbano)
                PointF[] poly = {
                    new PointF(4, 7),
                    new PointF(15, 3),
                    new PointF(21, 10),
                    new PointF(10, 16)
                };

                using (var bPoly = new SolidBrush(Color.FromArgb(185, 220, 245)))
                using (var pPoly = new Pen(Color.FromArgb(30, 100, 175), 1.3f))
                {
                    g.FillPolygon(bPoly, poly);
                    g.DrawPolygon(pPoly, poly);
                }

                // Vértices do polígono (Laranja agrimensura/topografia)
                using (var bVert = new SolidBrush(Color.FromArgb(235, 95, 20)))
                using (var pVert = new Pen(Color.FromArgb(180, 50, 0), 1f))
                {
                    foreach (var pt in poly)
                    {
                        g.FillEllipse(bVert, pt.X - 1.8f, pt.Y - 1.8f, 3.6f, 3.6f);
                        g.DrawEllipse(pVert, pt.X - 1.8f, pt.Y - 1.8f, 3.6f, 3.6f);
                    }
                }

                // Linha de rua / eixo viário passando em curva na base
                using (var pRoad = new Pen(Color.FromArgb(50, 55, 65), 2.2f))
                {
                    g.DrawCurve(pRoad, new PointF[] {
                        new PointF(2, 20),
                        new PointF(9, 18),
                        new PointF(16, 20),
                        new PointF(22, 22)
                    });
                }

                // Linha central pontilhada amarela da via
                using (var pCl = new Pen(Color.FromArgb(245, 165, 30), 1f) { DashStyle = DashStyle.Dash })
                {
                    g.DrawCurve(pCl, new PointF[] {
                        new PointF(2, 20),
                        new PointF(9, 18),
                        new PointF(16, 20),
                        new PointF(22, 22)
                    });
                }
            }
            return bmp;
        }

        /// <summary>
        /// Ícone do Importador GeoPackage (GPKG): Cilindro de banco de dados espacial (verde OGC) com mapa vetorial e ponto.
        /// Fundo 100% transparente.
        /// </summary>
        private static Bitmap DrawGpkgImport()
        {
            var bmp = new Bitmap(24, 24, PixelFormat.Format32bppArgb);
            using (var g = InitGfx(bmp))
            {
                // Disco inferior do banco de dados (SQLite / GPKG)
                using (var bBot = new SolidBrush(Color.FromArgb(46, 125, 50)))
                using (var pEdge = new Pen(Color.FromArgb(27, 94, 32), 1f))
                {
                    g.FillEllipse(bBot, 3, 14, 18, 7);
                    g.DrawEllipse(pEdge, 3, 14, 18, 7);

                    g.FillRectangle(bBot, 3, 11, 18, 6);
                    g.DrawLine(pEdge, 3, 11, 3, 17);
                    g.DrawLine(pEdge, 21, 11, 21, 17);
                }

                // Disco do meio
                using (var bMid = new SolidBrush(Color.FromArgb(67, 160, 71)))
                using (var pEdge = new Pen(Color.FromArgb(27, 94, 32), 1f))
                {
                    g.FillEllipse(bMid, 3, 8, 18, 7);
                    g.DrawEllipse(pEdge, 3, 8, 18, 7);

                    g.FillRectangle(bMid, 3, 6, 18, 5);
                    g.DrawLine(pEdge, 3, 6, 3, 11);
                    g.DrawLine(pEdge, 21, 6, 21, 11);
                }

                // Topo do cilindro (verde claro com mapa vetorial)
                using (var bTop = new SolidBrush(Color.FromArgb(200, 230, 205)))
                using (var pEdge = new Pen(Color.FromArgb(27, 94, 32), 1.2f))
                {
                    g.FillEllipse(bTop, 3, 2, 18, 7);
                    g.DrawEllipse(pEdge, 3, 2, 18, 7);
                }

                // Curva de rio / linha vetorial no topo do banco
                using (var pVector = new Pen(Color.FromArgb(30, 110, 180), 1.2f))
                {
                    g.DrawLine(pVector, 6, 6, 10, 5);
                    g.DrawLine(pVector, 10, 5, 14, 6);
                    g.DrawLine(pVector, 14, 6, 18, 4);
                }

                // Pino / Ponto geográfico no mapa
                using (var bPin = new SolidBrush(Color.FromArgb(230, 80, 20)))
                {
                    g.FillEllipse(bPin, 12, 4, 3, 3);
                }
            }
            return bmp;
        }

        /// <summary>
        /// Ícone do Analisador de Seções Transversais (SHP -> Seções):
        /// Duas quadras opostas, eixo viário, meios-fios e linhas de corte transversais.
        /// Fundo 100% transparente.
        /// </summary>
        private static Bitmap DrawRoadTransversals()
        {
            var bmp = new Bitmap(24, 24, PixelFormat.Format32bppArgb);
            using (var g = InitGfx(bmp))
            {
                // Quadra Esquerda (Bloco)
                using (var bQuadra = new SolidBrush(Color.FromArgb(210, 215, 220)))
                using (var pQuadra = new Pen(Color.FromArgb(130, 140, 150), 1.2f))
                {
                    g.FillRectangle(bQuadra, 1, 2, 5, 20);
                    g.DrawRectangle(pQuadra, 1, 2, 5, 20);

                    // Quadra Direita (Bloco)
                    g.FillRectangle(bQuadra, 18, 2, 5, 20);
                    g.DrawRectangle(pQuadra, 18, 2, 5, 20);
                }

                // Linhas de Meio-fio (Guias)
                using (var pCurb = new Pen(Color.FromArgb(80, 90, 100), 1f))
                {
                    g.DrawLine(pCurb, 8, 2, 8, 22);
                    g.DrawLine(pCurb, 16, 2, 16, 22);
                }

                // Eixo central do Logradouro (Tracejado âmbar)
                using (var pAxis = new Pen(Color.FromArgb(245, 158, 11), 1f) { DashStyle = DashStyle.Dash })
                {
                    g.DrawLine(pAxis, 12, 1, 12, 23);
                }

                // Linhas de corte transversais (Vermelho técnico / medição)
                using (var pTrans = new Pen(Color.FromArgb(220, 30, 30), 1.4f))
                {
                    g.DrawLine(pTrans, 1, 7, 23, 7);
                    g.DrawLine(pTrans, 1, 16, 23, 16);
                }

                // Pontos de interseção / corte nas quadras
                using (var bDot = new SolidBrush(Color.FromArgb(220, 30, 30)))
                {
                    g.FillEllipse(bDot, 0f, 6f, 3f, 3f);
                    g.FillEllipse(bDot, 21f, 6f, 3f, 3f);
                    g.FillEllipse(bDot, 0f, 15f, 3f, 3f);
                    g.FillEllipse(bDot, 21f, 15f, 3f, 3f);
                }
            }
            return bmp;
        }

        // Simplified cross section: two sidewalks, roadway and optional median.
        private static Bitmap DrawStreetProfileDefinition()
        {
            var bmp = new Bitmap(24, 24, PixelFormat.Format32bppArgb);
            using (var g = InitGfx(bmp))
            using (var earth = new SolidBrush(Color.FromArgb(195, 140, 110)))
            using (var sidewalk = new SolidBrush(Color.FromArgb(210, 215, 220)))
            using (var asphalt = new SolidBrush(Color.FromArgb(70, 80, 95)))
            using (var median = new SolidBrush(Color.FromArgb(110, 180, 90)))
            using (var curb = new Pen(Color.FromArgb(80, 90, 100), 1.2f))
            using (var axis = new Pen(Color.FromArgb(245, 158, 11), 1f))
            {
                g.FillRectangle(earth, 1, 19, 22, 3);
                g.FillRectangle(sidewalk, 1, 9, 5, 10);
                g.FillRectangle(sidewalk, 18, 9, 5, 10);
                g.FillRectangle(asphalt, 6, 13, 12, 6);
                g.FillRectangle(median, 11, 11, 2, 8);
                g.DrawLine(curb, 6, 9, 6, 19);
                g.DrawLine(curb, 18, 9, 18, 19);
                g.DrawLine(axis, 12, 5, 12, 10);
            }
            return bmp;
        }

        // Two fixed lot boundaries and a continuous sidewalk band following them.
        private static Bitmap DrawSidewalkRegularization()
        {
            var bmp = new Bitmap(24, 24, PixelFormat.Format32bppArgb);
            using (var g = InitGfx(bmp))
            using (var block = new SolidBrush(Color.FromArgb(210, 215, 220)))
            using (var sidewalk = new SolidBrush(Color.FromArgb(185, 220, 245)))
            using (var boundary = new Pen(Color.FromArgb(130, 140, 150), 1.1f))
            using (var curb = new Pen(Color.FromArgb(30, 100, 175), 1.4f))
            using (var axis = new Pen(Color.FromArgb(245, 158, 11), 1f) { DashStyle = DashStyle.Dash })
            {
                var left = new[] { new PointF(1, 1), new PointF(6, 1), new PointF(6, 10),
                    new PointF(8, 10), new PointF(8, 23), new PointF(1, 23) };
                g.FillPolygon(block, left);
                g.DrawLines(boundary, new[] { new PointF(6, 1), new PointF(6, 10),
                    new PointF(8, 10), new PointF(8, 23) });
                var band = new[] { new PointF(6, 1), new PointF(9, 1), new PointF(9, 9),
                    new PointF(11, 9), new PointF(11, 23), new PointF(8, 23),
                    new PointF(8, 10), new PointF(6, 10) };
                g.FillPolygon(sidewalk, band);
                g.DrawLines(curb, new[] { new PointF(9, 1), new PointF(9, 9),
                    new PointF(11, 9), new PointF(11, 23) });
                g.DrawLine(axis, 17, 1, 17, 23);
            }
            return bmp;
        }

        private static Bitmap DrawStreetProfileFitting()
        {
            var bmp = new Bitmap(24, 24, PixelFormat.Format32bppArgb);
            using (var g = InitGfx(bmp))
            using (var block = new SolidBrush(Color.FromArgb(210, 215, 220)))
            using (var road = new SolidBrush(Color.FromArgb(70, 80, 95)))
            using (var sidewalk = new SolidBrush(Color.FromArgb(185, 220, 245)))
            using (var limit = new Pen(Color.FromArgb(130, 140, 150), 1.1f))
            using (var dimension = new Pen(Color.FromArgb(220, 30, 30), 1.2f))
            using (var center = new Pen(Color.FromArgb(245, 158, 11), 1f))
            {
                g.FillRectangle(block, 1, 6, 3, 13); g.FillRectangle(block, 20, 6, 3, 13);
                g.FillRectangle(sidewalk, 4, 11, 4, 8); g.FillRectangle(sidewalk, 16, 11, 4, 8);
                g.FillRectangle(road, 8, 13, 8, 6);
                g.DrawLine(limit, 4, 5, 4, 20); g.DrawLine(limit, 20, 5, 20, 20);
                g.DrawLine(dimension, 4, 4, 20, 4);
                g.DrawLine(dimension, 4, 2, 4, 6); g.DrawLine(dimension, 20, 2, 20, 6);
                g.DrawLine(center, 12, 10, 12, 19);
            }
            return bmp;
        }

        /// <summary>
        /// Ícone do Street Profile Assignment:
        /// Topo: representação horizontal do perfil viário paramétrico (calçadas e pista).
        /// Meio: seta/elo de associação apontando para baixo.
        /// Base: eixo viário com limites públicos e testadas de quadra.
        /// Fundo 100% transparente, paleta Glaux 24x24 px.
        /// </summary>
        private static Bitmap DrawStreetProfileAssignment()
        {
            var bmp = new Bitmap(24, 24, PixelFormat.Format32bppArgb);
            using (var g = InitGfx(bmp))
            using (var block = new SolidBrush(Color.FromArgb(210, 215, 220)))
            using (var road = new SolidBrush(Color.FromArgb(70, 80, 95)))
            using (var sidewalk = new SolidBrush(Color.FromArgb(185, 220, 245)))
            using (var linkPen = new Pen(Color.FromArgb(245, 158, 11), 1.5f))
            using (var linkBrush = new SolidBrush(Color.FromArgb(245, 158, 11)))
            using (var framePen = new Pen(Color.FromArgb(130, 140, 150), 1f))
            using (var axisPen = new Pen(Color.FromArgb(245, 158, 11), 1f) { DashStyle = DashStyle.Dash })
            using (var curbPen = new Pen(Color.FromArgb(80, 90, 100), 1f))
            {
                // 1. Topo: Cartão / Banner do StreetProfile (faixas semânticas)
                g.FillRectangle(sidewalk, 2, 2, 4, 6);
                g.FillRectangle(road, 6, 2, 12, 6);
                g.FillRectangle(sidewalk, 18, 2, 4, 6);
                g.DrawRectangle(framePen, 2, 2, 20, 6);

                // 2. Meio: Seta técnica de atribuição (Profile -> GIS Street)
                g.DrawLine(linkPen, 12, 8, 12, 13);
                var arrowPoints = new[] {
                    new PointF(9.5f, 11.5f),
                    new PointF(14.5f, 11.5f),
                    new PointF(12f, 14.5f)
                };
                g.FillPolygon(linkBrush, arrowPoints);

                // 3. Base: Eixo e corredor viário do GIS
                // Quadras nos lados
                g.FillRectangle(block, 1, 15, 3, 8);
                g.FillRectangle(block, 20, 15, 3, 8);
                g.DrawRectangle(framePen, 1, 15, 3, 8);
                g.DrawRectangle(framePen, 20, 15, 3, 8);

                // Pavimento da rua
                g.FillRectangle(road, 6, 16, 12, 7);

                // Guias de meio-fio
                g.DrawLine(curbPen, 6, 15, 6, 23);
                g.DrawLine(curbPen, 18, 15, 18, 23);

                // Eixo central
                g.DrawLine(axisPen, 12, 15, 12, 23);
            }
            return bmp;
        }
    }
}

