---
name: "Road Transversals from GIS"
nickname: "RoadTransversals"
category: "Glaux Urb"
subcategory: "01 | Infraestrutura Viária"
class: "Buraqueira_Urb.RoadTransversals_Component"
file: "src/Urb/RoadTransversals_Component.cs"
plugin: "Glaux_Urb"
status: "Compilado; validação visual no Grasshopper pendente"
tags: [componente, grasshopper, glaux_urb, gis, transversais, calcadas]
---

# Road Transversals from GIS

Gera seções de quadra a quadra a partir dos eixos de logradouros, das quadras reais e dos limites físicos da rua. A camada de origem chamada `QUADRAS` em Downloads é usada semanticamente como limite viário/meio-fio, sem renomear o SHP. Os eixos mantêm nome, direção e origem da feição; a geometria existente permanece intacta.

## Filtro de cruzamentos

O corte candidato, já limitado pelas duas quadras, é testado contra os outros eixos viários. Cortes que atravessam outro eixo recebem `CROSSES_OTHER_STREET`. Cortes na região de um nó recebem `NEAR_INTERSECTION`, com raio definido por metade da largura medida entre os meios-fios. A identificação de continuação do mesmo logradouro usa nome e alinhamento de extremidades. `AMBIGUOUS` sinaliza interseção que não pode ser classificada com confiança. As árvores Before, Rejected, Reasons e Source mantêm o caminho `{rua;estaca}`. Source inclui nome, índice da curva, ID do registro SHP/GPKG quando disponível e estaca. IDs `GH:n` identificam curvas diretas apenas durante a execução.

## Modos

- `0 Existing`: linhas, pontos, larguras e CSV preservam a geometria medida.
- `1 Planning`: o valor `Minimum Sidewalk Width` é um piso, não uma largura uniforme. Os limites de lote e o eixo ficam fixos; o meio-fio proposto pode avançar para dentro do domínio viário para reservar a calçada mínima sem invadir a quadra. O planejador suaviza apenas trechos contíguos válidos, separados por corte rejeitado, estação sem matching ou mudança brusca da normal. A tolerância local atual é 0,30 m. O CSV legado usa as larguras planejadas neste modo.

Sem os dois meios-fios, a seção permanece em `Lines`, mas não recebe largura inventada; `Planning Conflicts` registra `MISSING_STREET_BOUNDARY`. Uma pista abaixo de `Min Roadway Width` recebe `ROADWAY_BELOW_MINIMUM`, mas a amostra continua disponível para análise. `Intervention Area` é uma prévia de polígonos entre estações, não área cadastral exata. O mínimo é verificado nas estações, não continuamente entre elas. A análise é 2D e não distingue cruzamentos em níveis diferentes.

## Inputs

| Nome | Nick | Tipo | Função |
|---|---|---|---|
| Logradouros (Axis) | Axis | Curve/SHP/GPKG, list | Eixos e identificação da via |
| Quadras (Blocks) | Blocks | Curve/Brep/SHP/GPKG, list | Quadras reais, borda privada existente |
| Meios-fios (Curbs) | Curbs | Curve/SHP/GPKG, list | Limite físico da rua |
| Maximum Section Spacing | Step | Number | Espaçamento máximo de suporte; zero usa 25 m; nick legado mantido |
| Search Radius | Radius | Number | Busca das bordas opostas |
| Sidewalk Mode | Mode | Integer | 0 Existing; 1 Planning |
| Minimum Sidewalk Width | StdSw | Number | Piso das larguras, não valor uniforme; nick legado preservado |
| Min Roadway Width | MinRoad | Number | Referência para aviso de pista estreita |
| Street Name Field | NameField | Text | Campo DBF do nome da rua |
| Run | Run | Boolean | Executa; última entrada |

## Outputs

| Nome | Nick | Tipo | Função |
|---|---|---|---|
| Transversal Lines | Lines | Curve tree | Cortes existentes válidos |
| Section Points | Pts | Point tree | Quadra, meio-fio, centro, meio-fio, quadra |
| CSV Output | CSV | Text | Seções existentes ou planejadas conforme Mode |
| Widths Summary | Widths | Text list | Resumo existente legado |
| Diagnostic Report | Report | Text | Contagens e métricas |
| Candidate Transversals | Before | Curve tree | Candidatos antes do filtro viário |
| Rejected Transversals | Rejected | Curve tree | Cortes descartados |
| Rejection Reasons | Reasons | Text tree | Causa do descarte |
| Source Streets | Source | Text tree | Nome, índice, ID e estaca |
| Planned Transversals | Planned | Curve tree | Cortes da proposta; no modo Existing, cópia geométrica |
| Existing Dimensions | Existing | Text tree | Larguras medidas por lado |
| Planned Dimensions | PlanDim | Text tree | Larguras propostas por lado |
| Planned Boundary | PlanBnd | Curve list | Borda privada fixa no Planning |
| Intervention Area | Area | Brep list | Faixa entre meio-fio existente e proposto |
| Planning Conflicts | Conflicts | Text tree | Matching ausente ou pista estreita |
| Existing Block Boundary | ExistBnd | Curve list | Quadras originais |
| Fixed Street Boundary | Curbs | Curve list | Limites viários originais |
| Planned Section Points | PlanPts | Point tree | Quadra fixa, meio-fio proposto, centro, meio-fio proposto, quadra fixa |
| Planned Curb Boundary | PlanCurbs | Curve list | Meio-fio proposto por trecho viável |
| Section Metadata | SectionMeta | Text tree | Tipo, razão, estaca, lado, fonte, vértice e perfil (`Profile`, `StreetName`, `SourceStreetID`) por candidato |
| Profiled Sections | Sections | ProfiledSection tree | Seções aceitas com cinco pontos, StreetID, StreetName e StreetProfile tipado |

## 🔗 Conexões & Compatibilidade de Pilhas (Pills)

```mermaid
flowchart LR
  GIS["[[Shp Import]] / [[Gpkg Import]]"] -->|eixos brutos| ASSIGN["[[Street Profile Assignment]]"]
  DEF["[[Street Profile Definition]]"] -->|Profiles| ASSIGN
  ASSIGN -->|Profiled (eixos + perfis)| RT["[[Road Transversals]]"]
  GIS -->|quadras reais, limites viários| RT
  RT -->|cortes válidos, razões e fontes| QA["[[Buraqueira Urb]]"]
  RT -->|Pts, PlanPts, Sections| FIT["[[Street Profile Fitting]]"]
  RT -->|CSV Existing ou Planning| RCS["[[Road Cross Section (CSV)]]"]
  RT -->|larguras e bordas propostas| SR["[[Sidewalk Regularization]]"]
```

O parâmetro `Axis` aceita diretamente objetos `ProfiledStreet` emitidos por [[Street Profile Assignment]]. Quando conectado, `Road Transversals` herda `StreetID`, `StreetName` e o perfil; cada seção aceita o carrega em `Sections`. `SectionMeta` permanece como diagnóstico textual e ponte de compatibilidade. Parâmetros existentes preservam nomes e índices para compatibilidade com arquivos GH legados. A matriz canônica está em [[05 - Contratos de Conexão e Nomenclatura de Parâmetros]]. `MinimumDimensionPlanner` é compartilhado com [[Sidewalk Regularization]].

## Verificação

Em auditoria geométrica independente com os três SHPs de Picuí: 218 candidatos, 212 aceitos, 6 rejeitados (2 `CROSSES_OTHER_STREET`, 4 `NEAR_INTERSECTION`). Sete cenários sintéticos passaram. Os testes do planejador C# passaram para mínimos de 1,0; 1,5; 2,0; e 2,5 m. Essa auditoria não executa o componente dentro do Rhino/Grasshopper. Debug e Release foram compilados sem erros/avisos em 2026-09-30.

## Lot Boundary Constraint — 2026-09-30

O Planning nunca desloca QL/QR. O mínimo de calçada é buscado primeiro por deslocamento do meio-fio dentro da largura pública. `MinRoad` permanece um diagnóstico transversal; a largura mínima efetivamente obrigatória de pista/faixas vem de [[Street Profile Definition]]. Se nem os mínimos de calçada mais uma largura viária positiva couberem, `Planning Conflicts` informa `INSUFFICIENT_PUBLIC_WIDTH`; o corte existente continua intacto e a seção planejada não é emitida. `PlanPts` transporta a nova posição dos meios-fios; `Planned` sozinho não codifica essa alteração.

## Seções orientadas pela geometria — 2026-09-30

Amostragem: vértices de quadra/lote com mudança relevante são projetados no eixo e aceitos somente quando correspondem à borda pública mais próxima no lado esquerdo ou direito. A tangente do eixo orienta o corte. Mudanças colineares e densificação de curva não geram evento; quinas reais produzem seções obrigatórias. Eventos próximos são unidos, salvo saltos laterais distintos. O Step passa a ser espaçamento **máximo** para preencher lacunas. Extremos do eixo também são obrigatórios, mas a validade topológica em cruzamentos prevalece. SectionMeta classifica GEOMETRY_EVENT, RUN_BOUNDARY e SUPPORT; Block:n é um índice local, e RunID é atribuído a jusante. Os pontos QL/QR de quina não são suavizados. Larguras mínimas/máximas do relatório são amostrais.

Limiar inicial calibrado com o SHP de Picuí: 12 graus, ruído de segmento abaixo de 0,20 m, matching transversal de 0,75 m, união de 0,25 m com exceção para salto lateral acima de 0,30 m. Detalhes e [comparação antes/depois](../../../docs/GLAUX_URB_ADAPTIVE_SECTIONS_2026-09-30.md) no relatório técnico. Testes do núcleo: 12/12; auditoria independente da Francisco de Freitas: 12 seções regulares vs. 34 estações adaptativas (28 obrigatórias, 6 suporte). Validação do GHA no Grasshopper pendente.



