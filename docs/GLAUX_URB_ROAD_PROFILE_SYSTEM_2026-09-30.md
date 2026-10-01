# GLAUX URB — ROAD PROFILE SYSTEM

Estado em 2026-09-30: primeiro incremento compilado; validação interativa no Rhino/Grasshopper pendente.

## Pilha de seção existente

**Nome:** `Road Cross Section (CSV)` (`RoadSection`, `RoadCrossSection_Component.cs`).

**Função atual:** recebe uma ou várias linhas CSV e desenha a seção 2D por camadas. As saídas `Srf` e `3D` mantêm camadas {0} calçada, {1} terreno, {2} meio-fio, {3} pista, {4} base, {5} sub-base e {6} canteiro. Também entrega curvas, pontos, cotas, hachuras e quantitativos.

**Inputs pré-existentes:** CSV/Table, One Way, Median Width, Alignment Axis, Origin Plane, Default Bombeo, Layer Depths, Curb Profile, Layout Grid, Draw Annotations, Custom Labels, Run. **Outputs pré-existentes:** Crv, Srf, 3D, Anno, Pts, Info, Hatch.

**Extrusão:** `Brep.CreateFromSweep(axis, sectionCurve, true, 0.001)` aplica um perfil constante ao eixo informado. Esse caminho não interpola as larguras das estacas nem respeita quebras em cruzamentos. O eixo aceita Z, mas a seção é calculada nominalmente em seu plano de origem.

**Reutilização:** parcial. O catálogo CSV, camadas, materiais implícitos por camada, espessuras, caimento e pranchas são mantidos. A versão auditada possuía um sinalizador pedonal que alterava sobretudo o rótulo e `Median Width` sem criar `SrfMedian`; ambos receberam implementação geométrica neste incremento. A definição por CSV soma as faixas e não preserva direção/identidade de cada uma; por isso o novo input `Adapted` carrega os elementos separados.

## Integração com Road Transversals

`Road Transversals from GIS` continua responsável por gerar, medir, filtrar e planejar. `Pts` contém cinco pontos por ramo `{rua;estaca}`: quadra esquerda, meio-fio esquerdo, centro, meio-fio direito e quadra direita. `Lines` contém cortes aceitos; `Planned` mantém as extremidades fixas dos lotes e `PlanPts` contém os meios-fios propostos; `Source` informa nome, índice, ID da feição e estaca. Não há ID de run nessa saída; a reconstrução o atribui somente a sequências contíguas válidas.

`Sidewalk Regularization` mantém o nome/GUID porque já está documentado e referenciado. A nomenclatura mais adequada para uma futura migração é **Street Surface Builder**; alternativas analisadas: **Street Geometry Builder** (amplo demais) e **Urban Section Reconstruction** (menos familiar no catálogo). A nova entrada `Pts` aciona o caminho longitudinal e dispensa busca perpendicular, novo matching GIS e nova regularização dimensional. `Lines` valida a identidade do corte; `FitPts` fornece os meios-fios ajustados sem deslocar o lote. Os inputs antigos continuam disponíveis como caminho legado; `Run` passou a ser a última entrada após os novos parâmetros, exigindo conferir arquivos GH salvos no carregamento.

## Nova Profile Definition

`Street Profile Definition` (`StreetProfile`) recebe Street, Street Type 0/1/2, Lane Count A/B, Preferred/Minimum Lane Width, Has Median, Median Width/Minimum, Has Sidewalk Left/Right, Minimum Sidewalk Left/Right, Pedestrian Width, Street Index, Section Points, Planned Section Points, Median Required, Use Planning e Run. Inputs de faixas/canteiro são ignorados para via pedonal. As larguras padrão são exemplos editáveis da interface, não regras normativas.

Outputs: `Elements` em sequência semântica, `CSV` para a pilha de seção existente, `NomW`, `MinW`, `Adapted` por `{rua;estaca}`, `Conflicts` por `{rua;estaca}`, `Report`. A ponte CSV agrega faixas, enquanto `Elements`/`Adapted` preservam direção e tipo. Um `Street Index` explícito evita atribuir o perfil a várias ruas sem escolha do usuário.

## Profile Fitting

Para cada seção, a largura disponível é medida entre QL–CL, CL–CR e CR–QR; no Planning, QL/QR vêm das extremidades de `Planned`. Em via pedonal, toda a caixa QL–QR vira área pedonal, sem faixa veicular ou calçada fictícia. Em via veicular, calçadas ativadas exigem seu mínimo. A pista exige a soma dos mínimos das faixas e do canteiro. Se a pista for menor que a soma nominal, o canteiro é reduzido até seu mínimo, depois cada faixa apenas até o mínimo configurado. Se ainda não couber, retorna `PROFILE_CONFLICT`; nunca escala o perfil inteiro. Espaço adicional vira `Roadway Reserve`; a ausência de calçada preserva o espaço externo como `Verge`, não como calçada.

## Geração longitudinal

`Sidewalk Regularization` agrupa estações em runs, quebra em índice ausente, mudança brusca de orientação ou salto geométrico e gera bordas privadas, meios-fios, faixas de calçada e via existentes/planejadas. `RunPts` mantém os cinco pontos em `{rua;run;estaca}`. `Road Cross Section` recebe `RunPts` e `Adapted`, interpola as divisões semânticas ao longo do mesmo run e gera `AdaptSrf` por `{rua;run;elemento}`, `AdaptLabels` e `AdaptInfo`. Faixas, reserva, canteiro ou área pedonal continuam identificáveis. O `3D` legado de perfil constante é desativado nesse modo para evitar uma segunda geometria contraditória.

As superfícies adaptativas são Breps abertas entre estacas que herdam o Z dos pontos quando disponível: um modelo 2,5D de representação, ainda sem espessuras/volumes adaptativos. O `Srf` nominal continua servindo de prancha. Quadras poligonais planejadas exigem correspondência topológica do trecho com o anel original e não são produzidas por conectar pontos indiscriminadamente.

## Estratégia de interseções

Os cortes rejeitados impedem ligação longitudinal automática sobre o nó. O preenchimento de T e + requer identificar braços, ordenar limites ao redor do nó, formar a área de pista, virar calçadas nas esquinas e terminar/costurar canteiros. Esse `Intersection Builder` continua pendente; `AdaptInfo` informa `Intersections=0`. Não se deve fechar a lacuna por uma varredura única.

## Tipologias iniciais

Pedestrian (área única), One Way, Two Way, Two Way + Median, calçada só à esquerda, só à direita e sem calçadas. Os sete arranjos passaram em auditoria dimensional independente; todos usam a mesma sequência de elementos, sem classes específicas para cada preset.

## MVP e validação

Incremento entregue: auditoria da pilha, ligação de `Pts`/`Lines`/`Planned`, runs, bordas e superfícies longitudinalmente, definição semântica, fitting por seção, via pedonal/canteiro na pilha existente e superfícies de elementos adaptativos. Compilação Debug/Release é a verificação de API disponível sem Rhino aberto. Auditorias Python são modelos 2D independentes, não execuções do GHA.

Em Picuí, a auditoria independente de uma rua real (`Francisco de Freitas`, passo 10 m) encontrou 27 seções contíguas, 2 bordas de quadra, 2 meios-fios, 52 módulos de calçada, 26 módulos de pista e zero polígonos degenerados 2D. Um exemplo de perfil bidirecional com duas faixas, mínimo de 2,5 m por faixa e mínimo de 1,5 m por calçada coube em 10/27 seções; 17 receberam conflitos de largura (9 pista, 5 calçada esquerda, 3 direita). Esses mínimos são hipóteses de teste, não prescrição urbanística.

## Limitações e etapas futuras

1. Abrir no Rhino/Grasshopper e verificar carregamento de definições GH antigas, árvore de caminhos, `Brep.CreateFromCornerPoints`, orientação e preview; nenhum teste de execução do GHA foi feito nesta sessão.
2. Implementar polígonos de quadra planejada por associação topológica ao anel de origem e validação de auto-interseção.
3. Validar fitting e superfícies para curvas, larguras variáveis, Z variável, calçadas assimétricas e mudanças entre perfil pedonal e veicular.
4. Criar Junction Builder inicial para T e +, com pistas, canto das calçadas e término de canteiro; depois oblíquos, diferenças de largura, muitos braços e rotatórias.
5. Converter os perfis adaptativos em sólidos com materiais/camadas, cotas de meio-fio/mediana e espessuras por elemento. O sweep legado não resolve isso.
6. Para escala urbana, substituir buscas lineares por índice espacial e perfis em texto por contrato tipado/serializado versionado, preservando um adaptador CSV.

## Resultado da compilação e distribuição

Em 2026-09-30, `dotnet build -c Debug` e `-c Release` encerraram com zero erros e zero avisos. O GHA Release foi copiado para `dist/Glaux_Urb.gha` e `%APPDATA%/Grasshopper/Libraries/Glaux/Glaux_Urb.gha`, ambos com SHA-256 `279C656C57F4BCA981A27EB3D85D2B952E4A0795E56332ADD7AF1230F3599484`. Havia somente um `Glaux_Urb.gha` ativo na pasta Glaux.

Comparação visual independente: [GIS original × seções × superfícies](../validation/urb/urb_reconstruction_compare.svg). Evidências tabulares: [reconstrução](../validation/urb/urb_reconstruction_audit.json) e [tipologias/fitting](../validation/urb/urb_profile_audit.json). Esses arquivos não são saída executada pelo GHA.

## Correção: Lot Boundary Constraint (2026-09-30)

A regra anterior de mover a borda privada foi substituída. O espaço disponível é medido entre os limites reais dos lotes. `Road Transversals` mantém QL/QR, propõe meio-fios internos em `PlanPts` e não emite seção planejada quando nem os pisos de calçada e um domínio viário positivo cabem. `Street Profile Definition` aplica os mínimos por lado, tipologia e elementos obrigatórios no núcleo puro `LotBoundaryProfileFitter`; `FitPts` mantém os lotes fixos e posiciona os meio-fios finais. Os conflitos incluem largura disponível, mínimos exigidos e déficit. O canteiro pode ser explicitamente obrigatório ou opcional. Não há compressão proporcional do perfil inteiro.

`Sidewalk Regularization` usa `FitPts` e **as quadras reais fechadas** no input `Blocks` para avaliar as superfícies planejadas em planta contra o interior dos lotes. Sem quadras reais, sem `FitPts`, ou com interseção booleana inconclusiva, a proposta longitudinal não é liberada. `RunPts` carrega apenas as seções planejadas aceitas para a etapa 3D. O antigo caminho Planning por deslocamento de limite privado foi desativado. A geometria Existing e os arquivos SHP permanecem intactos.

Testes: `test_lot_boundary_fitter.ps1` executa 11 cenários do núcleo C# (todos passaram). `test_lot_geometry.py` cobre os casos K (invasão de lote apesar de soma caber) e L (mínimo violado entre estacas) em modelo geométrico independente. Na rua Francisco de Freitas, 26/27 seções passam o fitting C# com duas calçadas mínimas de 1,5 m e duas faixas mínimas de 2,5 m; a seção restante tem déficit de 0,0035 m. Uma auditoria 2D independente detectou oito módulos candidatos que invadiriam quadras reais e devem ser barrados pelo teste booleano no Grasshopper. A execução do GHA no Rhino ainda precisa confirmar o comportamento de `Curve.CreateBooleanIntersection`, árvores e preview; não se deve interpretar as contagens Python como saída executada do componente.

A próxima etapa, conforme solicitado, é completar a via 3D após validar esta correção no Grasshopper: volumes adaptativos, cotas de meio-fio/canteiro e interseções T/+.




