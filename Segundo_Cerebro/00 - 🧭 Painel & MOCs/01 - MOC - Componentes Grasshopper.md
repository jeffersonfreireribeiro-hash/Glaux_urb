---
aliases: [Catálogo de Componentes, MOC Componentes]
tags: [moc, grasshopper, componentes]
---

# 🎛️ MOC — Catálogo de Componentes Grasshopper

Este catálogo reúne a documentação técnica, entradas, saídas e algoritmos de todos os componentes da suíte **Buraqueira**.

---

## 🔊 Buraqueira Acoustics (v1.01 — Motor Nativo) & Ponte Pachyderm

A suíte acústica agora conta com duas abordagens:
1. **Motor Nativo v1.01 (`Buraqueira_Acoustics_v101.gha`):** Tipos puros próprios (`Buraqueira_Acoustic_Types.dll`) e aceleração DirectCompute via SharpDX.
2. **Ponte Legada (`Buraqueira_Acoustics.gha`):** Para compatibilidade com definições históricas baseadas no Pachyderm.

### 1. Model & Setup Geométrico
* [[Acoustic Scene]] — Consolidador da cena com geometria, receptores e fontes.
* [[Acoustic Surface]] — Atribuição de materiais acústicos a superfícies.
* [[Acoustic Material]] & [[Acoustic Material Builder]] — Definição de coeficientes de absorção e espalhamento.
* [[Adaptive Tri Remesh]] — Remalhamento adaptativo para simulação sonora.
* [[Acoustic Room Volume]] — Cálculo do volume e área de salas para Sabine/Eyring.

### 2. Ray Tracing & Solvers
* [[Gpu Ray Tracer]] — Traçador acelerado em GPU via Compute Shaders (DirectCompute nativo na v1.01).
* [[Gpu Vulkan Ray Tracer]] & [[Dxr Ray Tracer]] — Traçado por hardware RTX (Vulkan / D3D12 DXR).
* [[Gpu Hybrid Ray Tracer]] — Combinação de Image Source determinístico e raios estocásticos.
* [[Image Source (Fixed)]] — Solver determinístico CPU multi-core com resolução geométrica completa, suporte a escalas m/mm e cálculo de 1ª e 2ª ordens sem auto-bloqueio (Glaux Classic).
* [[Specular Reflections (GPU ISM)]] — Solver ISM determinístico acelerado por GPU nativo da v1.01.
* [[Ray Tracing Console]] — Controle e monitoramento da simulação em tempo real.

### 3. Análise & Parâmetros ISO 3382 / Pilhas (Pills)
* [[Pill ETC Calculator]] — Pilha central de cálculo e composição da ETC unindo Ray Tracing, Som Direto, Image Source e Difração com filtragem estrita de bandas.
* [[Pill ETC Band Filter]] — Filtro e recorte cirúrgico de bandas de oitava (125 Hz a 4 kHz, domínios, frequências nominais e presets) para curvas de energia no tempo (AudioSignal / ETC).
* [[Room Acoustic Analyzer]] — Central completa de parâmetros ISO 3382 (EDT, T20, T30, C80, C50, D50, Ts, ITDG) com algoritmo de Lundeby (1995).
* [[Spatial Parameters]] — Cálculo unificado de parâmetros acústicos espaciais.
* [[Clarity]] — $C_{80}$ (música) e $C_{50}$ (fala).
* [[D50]] — Definição / Deutlichkeit para inteligibilidade de fala.
* [[Center Time]] — Tempo de centro ($T_s$).
* [[EDT]] — Early Decay Time.
* [[SPL]] — Nível de Pressão Sonora absoluto e relativo.
* [[ITDG]] — Initial Time Delay Gap.

### 4. Gráficos no Canvas (UI Customizada)
* [[Acoustic Compliance Bar Graph]] — Barras empilhadas de conformidade com metas de projeto.
* [[Acoustic Frequency Graph]] — Curvas de resposta em frequência em oitavas.
* [[ETC Canvas Graph]] — Gráfico de Curva de Energia-Tempo (ETC) interativo.
* [[Acoustic Material Heatmap]] — Heatmap visual de distribuição de absorção.

### 5. Áudio & Auralização
* [[Steam Audio]] — Renderizador de áudio espacial binaural baseado em física.
* [[Gpu Auralize]] — Convolução de resposta ao impulso acelerada por GPU.

### 6. Validação & Autoteste (v1.01 & Legado)
* [[Acoustic Propagation Self-Test]] — Validação analítica integrada (campo livre, oclusão, som direto $1/r^2$, reflexão ISM de 1ª ordem e concordância DirectCompute vs CPU).
* [[Mesh Ray Sample Validation]] — Validação analítica (360°/eixo, 0ms) e empírica real com suporte nativo a dados de GPU Ray Tracing (`Tr` / `Receiver_Bank`), `ETC`, contagem de penetrações e heatmap de densidade.

---

## 🛠️ Glaux Tools / Buraqueira Tools (85 Componentes)

### 1. Subsistema Pill (Gerenciamento de Fluxo & Transformação)
* [[Pill Hub]] — Centralizador de dados e sinais do canvas.
* [[Pill ETC Band Filter]] — Filtro e recorte cirúrgico de bandas de oitava (125 Hz a 4 kHz, domínios, frequências nominais e presets) para AudioSignals (ETC).
* [[Data Interpolator (Gap Filler & Resampler)]] — Interpolação contínua (Linear, Cosseno, Cúbico Spline, Nearest), preenchimento de lacunas (null/NaN), reamostragem de listas e particionamento por contagem de indivíduos.
* [[Pill Domain Filter]] — Filtro e recorte cirúrgico de valores por domínio [Min..Max] com modos Compactar, Alinhar com Null e Clamp.
* [[Pill Number Rounder]] — Arredondamento flexível (Nearest, Floor, Ceil, Trunc) e formatação de texto (ponto/vírgula).
* [[Pill Time Impulse Clock Pulse]] — Gerador de impulsos temporizados estilo metrônomo / clock pulse com botões interativos no canvas.
* [[Pill Slider Pool]] — Controle agrupado de sliders com interface interativa.
* [[Pill Preset Vault]] — Banco de salvamento e recuperação de presets em JSON.
* [[Pill Layer Pipeline]] — Leitura e vinculação automatizada de camadas do Rhino.
* [[Pill Bundle Pack]] & [[Pill Bundle Unpack]] — Empacotamento de dados complexos em cabo único.
* [[Pill Viewport 3D Capture]] — Captura do Viewport 3D em alta resolução com preview no canvas, modos do Rhino e fundo transparente.

### 2. Estatística, Inferência & Distribuições
* [[Central Tendency]], [[Standard Deviation]], [[Variance]], [[Skewness Kurtosis]]
* [[Beta Distribution]], [[Binomial Distribution]], [[Chi Square Distribution]], [[Normal Distribution (NORM.DIST  NORM.INV)]], [[Poisson Distribution (POISSON.DIST)]]
* [[Probability & Bayes Theorem]], [[Confidence Interval & t-Score]], [[ANOVA & F-Test (Variance & Means)]]
* [[Outlier Detection]], [[Fast Pareto]], [[Shannon Entropy]]

### 3. Matrizes, Álgebra Linear & Machine Learning
* [[Matrix Construct]], [[Matrix Multiply]], [[Matrix Invert Det]], [[Matrix Eigen]], [[Matrix Solver]]
* [[Linear Regression (OLS)]], [[Model Evaluation (RMSE, MAE, R²)]], [[Loss Functions]]
* [[Classification & Tree Metrics (Gini, InfoGain, Logit)]], [[Cluster Validation (Mahalanobis & Silhouette)]]
* [[KL Divergence (Relative Entropy)]], [[Covariance & Correlation]]

### 4. Visualização, Gráficos & Canvas
* [[Hierarchical Cluster Graph]] — Agrupamento hierárquico (dendrograma + heatmap reordenado + barras de peso HRP).
* [[Box Plot Distribution]] — Dispersão populacional com quartis, média, mediana, bigodes e detecção de outliers.
* [[Pill Isometric Surface Graph]] — Malha 3D e superfície com gradiente em projeção isométrica com 4 quadrantes cardeais, eixos e colorbar.
* [[Data Table Visualizer]] — Grid interativo tipo planilha no canvas com paginação, fatiamento e busca textual com destaque.
* [[Spatial Grid & Viewport Heatmap]] — Mapa de calor espacial com gradientes configuráveis e preview 3D.
* [[Line Chart & Statistics]] & [[Scatter & Bubble Plot]] — Gráficos renderizados diretamente no GH.
* [[Pill Viewport 3D Capture]] — Captura e miniatura visual interativa da viewport no canvas.
* [[Pill View Generator]] — Orientação de câmeras, geração de vistas isométricas/ortogonais e automação multi-view.
* [[Pill Vector Sheet Layout]] — Diagramação paramétrica de pranchas técnicas vetoriais (SVG e PDF), carimbos customizados, escalas gráficas e pré-visualização instantânea no navegador.
* [[Pill Pen Style]] — Estilização vetorial e simbologia gráfica inspirada no QGIS e ABNT (espessuras em mm, traçados, cores, preenchimento e marcadores).

### 5. Árvores de Dados & Tabelas (Data Trees)
* [[Duplicate Data Inspector]] — Inspeciona, detecta e extrai dados/linhas repetidas (Modo Tabela: Ramos=Colunas, Linhas=Linhas; Por Ramo; Global; Ramo vs Ramo).
* [[Data Table Visualizer]] — Visualização e seleção interativa de linhas/colunas em tabelas de árvore de dados.
* [[Batch Distinct (Keep Order & Map)]], [[Data Group Finder (Cluster & Runs)]], [[Data Stack (VSTACK  HSTACK)]]
* [[Tree Filter (Preserve Paths)]], [[Tree Align (Match Topology)]], [[Tree Structural Diff]]

---

## 🏙️ Buraqueira Urb & 📦 GDL
* **Urb:** [[Street Profile Definition]], [[Street Profile Assignment]], [[Street Profile Fitting]], [[Road Transversals]], [[Sidewalk Regularization]], [[Road Cross Section]], [[Shp Import]], [[Gpkg Import]], [[Export Streets to GIS]]
* **GDL:** [[GDL Toolkit Core]], [[Mesa GDL Template]]

* [[Acoustic Mesh Mapping]] + [[Reverberation Time (ISO 3382  TR60)]] + [[Spatial Grid & Viewport Heatmap]] — cadeia estabilizada de TR por receptor até o mapa espacial.

- [x] Correção de zeros artificiais no Frequency Graph e contraste automático do Heatmap — [[DevLog - 2026-09-24 - ETC e zeros artificiais nos graficos]].


- 24/09/2026: [[DevLog - 2026-09-24 - Validacao de janelas fixas e lacunas ETC]] — janelas fixas, qualidade explícita e lacunas preservadas. Convergência do solver ainda pendente.


- 24/09/2026: [[DevLog - 2026-09-24 - Degraus como ressalva Classic e v101]] — removido veto de degrau, cálculo alinhado no RT/Graph/Map v1.01.
- 24/09/2026: [[DevLog - 2026-09-24 - Otimizacao Pill Slider Pool e Regra Pasta Unica Glaux]] — otimização contra recálculo em cascata da DAG, throttling de 50ms e regra de pasta única Glaux.


- [x] 24/09/2026 — Estabilidade numérica GPU Classic/v1.01: lotes, histograma float e normalização independente de N. [[DevLog - 2026-09-24 - Estabilidade GPU ETC e Contagem de Raios]].
- [ ] Validar convergência no teatro com ETCs novos, mantendo os demais parâmetros fixos.


- [x] ISM: memória, poda conservadora, índices de material; EDT inicial com qualidade separada. [[DevLog - 2026-09-24 - Image Source Ordem 2 e EDT Impulsivo]].
- [ ] Benchmark e revalidação da cena do teatro em ordem 2 com novos ETCs.


## Auditoria S3 — 25/09/2026

Ver [[DevLog - 2026-09-25 - Auditoria S3 e Acoplamento ISM GPU]]. O motor Classic passa a preservar caminhos difusos/transmitidos de baixa ordem ao excluir caminhos exclusivamente especulares cobertos pelo ISM. A revisão reproduziu os 521 T30 indisponíveis de 500 Hz; os critérios não foram relaxados. O contrato de potência/unidades da soma híbrida ainda precisa de calibração física, portanto não há aval definitivo do TR do teatro. Testes GPU e de ISM ordem 2 aprovados.


## Calibração GPU/BDPT — 26/09/2026

Ver [[DevLog - 2026-09-26 - Calibracao GPU e BDPT Preservado]]. BDPT/NEE mantido com partição por evento final, sem dupla contagem difusa; potência direcional por raio e normalização volumétrica do receptor. Fallback do direto preserva bandas zero e usa potência correta. Cache ISM invalida mudanças físicas. Testes de energia, T30, fontes e cena aberta aprovados. Os 492 pares reais dependem de novos ETCs; não houve relaxamento dos critérios de TR.

## Glaux Urb — calçadas
* [[Sidewalk Regularization]] — proposta local por lado com meio-fio fixo e limites privados originais preservados.


- [[Road Transversals]] — transversais GIS com rejeições e proposta de calçadas por largura mínima.

- [[Street Profile Definition]] — via pedonal, mão única/dupla, faixas, canteiro e calçadas independentes.
- [[Road Cross Section]] — seção nominal CSV e superfícies adaptativas por run.

- [[Lot Boundary Constraint — Glaux Urb]] — contrato `Pts` → `PlanPts` → `FitPts` → `RunPts` com quadras reais.

## Glaux Urb — seções orientadas pela geometria
- [[Road Transversals]]: eventos de borda esquerda/direita, união de estacas próximas, filtro de cruzamentos e suporte por espaçamento máximo.
- [[Street Profile Definition]] e [[Sidewalk Regularization]]: ícones específicos integrados ao gerador visual Urb; validação no Grasshopper pendente.

## Glaux Urb — perfil composto dinâmico & atribuição GIS
- [[Street Profile Definition]] — Street, Street Type e faixas variáveis com tipo/sentido/obrigatoriedade persistidos em cada parâmetro; Tree Strip e Furniture Strip incluídos.
- [[Street Profile Assignment]] — Associação 1-para-N entre perfis conceituais e vias reais de atributos GIS (`NOME_LOG`), com modos exato/normalizado, detecção de conflitos (`UNMATCHED_STREET`, `AMBIGUOUS_PROFILE_MATCH`) e emissão de `ProfiledStreet`.
- [[Street Profile Fitting]] — `Sections` tipadas + `Pts`/`PlanPts` → `Adapted`, `FitPts`, conflitos e objeto ajustado; leitura genérica de `Sections` corrigida em 1.1.1; listas de perfis com `SectionMeta` seguem como ponte de compatibilidade.
- Width Domain por faixa: `MinimumWidth|MaximumWidth`, largura fixa `[n,n]`, limites rígidos no fitting e `EXCESS_WIDTH` quando a caixa pública excede a soma dos máximos. `SectionMeta.SourceStreetID` / `StreetName` associam a via; `PathIdx` é posição temporária de fallback.
