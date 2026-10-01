---
name: "Street Profile Fitting"
nickname: "ProfileFit"
category: "Glaux Urb"
subcategory: "01 | Infraestrutura Viária"
class: "Buraqueira_Urb.StreetProfileFitting_Component"
file: "src/Urb/StreetProfileFitting_Component.cs"
plugin: "Glaux_Urb"
status: "Correção de leitura de Sections compilada; revalidação no Grasshopper pendente"
tags: [componente, grasshopper, glaux_urb, fitting, perfil_viario]
---

# Street Profile Fitting

Ajusta a sequência semântica às seções reais/adaptativas, preserva QL/QR e cada `ElementID`. Cada faixa ativa permanece dentro de seu `WidthDomain [Minimum,Maximum]`; uma faixa opcional só pode ficar abaixo do mínimo quando explicitamente `SUPPRESSED` (largura zero). Largura insuficiente gera `PROFILE_CONFLICT`; largura acima da soma dos máximos gera `EXCESS_WIDTH`, sem expandir faixas silenciosamente. O input de `PlanPts` conectado deve manter os limites dos lotes originais.

## Inputs

| Nome | Nick | Tipo | Função |
|---|---|---|---|
| Street Profiles | Profile | StreetProfile / ProfiledStreet list, optional | Ponte de compatibilidade; aceita vários perfis quando `Sections` não estiver conectado |
| Section Points | Pts | Point tree | Seções existentes de [[Road Transversals]] |
| Planned Section Points | PlanPts | Point tree, optional | Proposta com QL/QR fixos |
| Street Path Index | PathIdx | Integer | Posição temporária em `{rua;estaca}`; fallback manual para árvore multivia sem SectionMeta; não é identidade |
| Run | Run | Boolean | Executa |
| Section Metadata | SectionMeta | Text tree, optional | `SourceStreetID` / `StreetName` / `Profile` de [[Road Transversals]] para seleção automática por identidade |
| Profiled Sections | Sections | ProfiledSection tree, optional | Seções tipadas com perfil já associado; evita novo matching |

## Outputs

| Nome | Nick | Tipo | Função |
|---|---|---|---|
| Adapted Profiles | Adapted | Text tree | Faixas internas do domínio viário por seção, com ID/status |
| Fitted Section Points | FitPts | Point tree | QL, meio-fio L, centro, meio-fio R, QR |
| Fitting Conflicts | Conflicts | Text tree | Déficit, `EXCESS_WIDTH`, lacuna de domínios, geometria ou mínimo interpolado |
| Fitted Street Profiles | Fitted | StreetProfile tree | Todos os elementos com ordem, largura e status |
| Fitting Report | Report | Text | Contagens e supressões |

## 🔗 Conexões & Compatibilidade de Pilhas (Pills)

```mermaid
flowchart LR
  ASSIGN["[[Street Profile Assignment]]"] -->|Profiled / Profiles| FIT["[[Street Profile Fitting]]"]
  DEF["[[Street Profile Definition]]"] -->|Profile| FIT
  RT["[[Road Transversals]]"] -->|Pts, PlanPts e Sections| FIT
  FIT -->|FitPts| SR["[[Sidewalk Regularization]]"]
  FIT -->|Adapted| RCS["[[Road Cross Section]]"]
```

Quando `Sections` está conectado, o fitting usa o `StreetProfile` embutido em cada seção e confere QL/QR contra `Pts`; seção sem perfil recebe `UNMATCHED_SECTION_PROFILE`. Sem `Sections`, aceita lista de `ProfiledStreet`/`StreetProfile` e usa `SectionMeta` como ponte textual de compatibilidade. O índice do caminho (`PathIdx`) é seleção temporária, não identidade. Ícone 24×24 px exclusivo em `GlauxUrbIcons.StreetProfileFitting`. O 3D atual reconhece faixas viárias internas; subdivisão de Tree Strip e Furniture Strip externas nas superfícies longitudinais segue pendente. Testes puros 24/24; runtime Rhino/Grasshopper pendente.

Em 1º de outubro de 2026, a entrada genérica `Sections` passou a ser lida por `VolatileData` e ramos `IGH_Goo`. A conversão exigida por `GetDataTree<GH_ObjectWrapper>` causava breakpoint na abertura do Grasshopper quando a árvore conectada tinha o tipo genérico real. A correção preserva os caminhos `{rua;estaca}` e o perfil embutido.
