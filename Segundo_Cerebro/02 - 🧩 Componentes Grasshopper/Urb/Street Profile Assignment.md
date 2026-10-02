---
name: "Street Profile Assignment"
nickname: "ProfileAssign"
category: "Glaux Urb"
subcategory: "01 | Infraestrutura Viária"
class: "Buraqueira_Urb.StreetProfileAssignment_Component"
file: "src/Urb/StreetProfileAssignment_Component.cs"
plugin: "Glaux_Urb"
status: "Compilado; testes puros 24/24 compartilhados; validação no Rhino/Grasshopper pendente"
tags: [componente, grasshopper, glaux_urb, perfil_viario, gis, assignment, logradouros]
---

# Street Profile Assignment

Conecta perfis viários conceituais ([[Street Profile Definition]]) às feições reais de logradouros vindas de fontes GIS (SHP, GPKG ou curvas diretas). A atribuição é realizada por correspondência semântica de nome de rua (`StreetName` ↔ coluna configurável de atributos GIS), suportando múltiplos trechos por logradouro sem exigir que o usuário crie cópias manuais de perfis para cada segmento. Vias sem perfil compatível são sinalizadas como `UNMATCHED_STREET` e perfis conflitantes geram `AMBIGUOUS_PROFILE_MATCH` sem casamento silencioso.

## Inputs

| Nome | Nick | Tipo | Padrão | Função |
|---|---|---|---|---|
| Streets | Streets | Generic tree | — | Eixos dos logradouros: árvore/lista de curvas, feições ShpFeature/GpkgFeature, caminhos .shp/.gpkg ou ProfiledStreet. Preserva a estrutura {feição} |
| Street Profiles | Profiles | StreetProfile list | — | Coleção de perfis viários conceituais gerados por [[Street Profile Definition]] |
| Street Name Field | NameField | Text | vazio | Campo explícito do nome; vazio procura nomes conhecidos. Campo solicitado e ausente gera erro |
| Match Mode | Mode | Integer | `0` | Modo de casamento: `0` = Normalized (insensível a maiúsculas/minúsculas, trim e espaços múltiplos colapsados); `1` = Exact |
| Run | Run | Boolean | `true` | Executa o cálculo da pilha (último input estrito) |

## Outputs

| Nome | Nick | Tipo | Função |
|---|---|---|---|
| Profiled Streets | Profiled | Generic tree | Árvore de objetos ProfiledStreet contendo a via, geometria, identidade estável (`StreetID`) e o `StreetProfile` associado |
| Matched Curves | Matched | Curve tree | Eixos que receberam perfil viário com sucesso, preservando os ramos de origem |
| Unmatched Curves | Unmatched | Curve tree | Eixos sem perfil (`UNMATCHED_STREET`), preservados no mesmo ramo para análise |
| Ambiguous Curves | Ambiguous | Curve tree | Eixos com perfis concorrentes (`AMBIGUOUS_PROFILE_MATCH`) |
| Assignment Report | Report | Text | Resumo diagnóstico com contagens, status de casamento e divergências |

## 🔗 Conexões & Compatibilidade de Pilhas (Pills)

```mermaid
flowchart LR
  GIS["[[Shp Import]] / [[Gpkg Import]]"] -->|Streets| ASSIGN["[[Street Profile Assignment]]"]
  DEF["[[Street Profile Definition]]"] -->|Profiles| ASSIGN
  ASSIGN -->|Profiled| RT["[[Road Transversals]]"]
  RT -->|Pts / PlanPts / Sections| FIT["[[Street Profile Fitting]]"]
  FIT -->|FitPts| SR["[[Sidewalk Regularization]]"]
  FIT -->|Adapted| RCS["[[Road Cross Section]]"]
```

## Regras de Associação e Normalização

1. **Relação 1-para-N (Um perfil para múltiplos segmentos):** Um único `StreetProfile` com `StreetName = "Rua Amazonas"` associa-se automaticamente a todos os segmentos cadastrais cujo campo GIS corresponda a esse nome.
2. **Normalização Robusta (`Mode = 0`):** Remove espaços residuais nas extremidades, colapsa sequências de múltiplos espaços internos (`"Rua   Amazonas"` $\to$ `"Rua Amazonas"`) e compara em caixa alta insensível.
3. **Detecção de Ambiguidade e Deduplicação Inteligente:** Se dois ou mais perfis com o mesmo `StreetName` possuírem definições distintas ou conflitantes (número de faixas, direções, larguras), as vias afetadas são emitidas em `Ambiguous` com status `AMBIGUOUS_PROFILE_MATCH`. Contudo, se perfis duplicados forem estruturalmente equivalentes (por exemplo, quando o usuário passa a lista de trechos do GIS para o `Street Profile Definition` e segmentos repetidos geram cópias idênticas), o sistema deduplica automaticamente os perfis (`AreProfilesEquivalent`), casando 100% dos trechos sem falsos alarmes de ambiguidade.
4. **Acoplamento a Jusante:** `ProfiledStreet` alimenta `Axis` de [[Road Transversals]]. A saída tipada `Sections` transporta o perfil diretamente ao [[Street Profile Fitting]], sem novo matching por nome. `SectionMeta` e `Profile` continuam como ponte de compatibilidade. Desde 1.2.0, `Streets` também aceita `Features` de [[Shp Import]]/[[Gpkg Import]] e consulta `NameField` no mapa tipado de atributos da feição.
5. **Identidade e Caminhos GIS:** O input `Streets` aceita diretamente strings, `GH_String` (vindos de Panels do Grasshopper ou File Path), objetos de arquivo e feições. O `GisPathResolver` desempacota as strings, limpa aspas/espaços e resolve mapeamentos de letras de unidade (ex.: `G:\` $\leftrightarrow$ `H:\`). Atributos GIS válidos (`STREET_ID`, `ID_LOGRADOURO`, `ID_LOG`, `COD_LOG`, `ID`, `FID`) geram `StreetID` de origem. Sem eles, um fingerprint de nome e geometria substitui o número de registro.
6. **Perfis Coringa / Padrão (Wildcard) e Match por Tipo:** Quando um perfil possui `StreetName` genérico (`Rua`, `Vias`, `Logradouro`, `*`, `DEFAULT`, `PADRAO`) ou vazio, ele atua como perfil coringa (fallback). Além disso, vias que possuam o atributo GIS `TIPO` (ex.: "Rua", "Travessa", "Avenida") casam automaticamente com perfis definidos para essa tipologia. Caso um trecho viário não encontre um perfil específico pelo seu nome de logradouro, o perfil padrão é automaticamente atribuído a ele, viabilizando análises de bairros inteiros com um único perfil padrão.

## Ícone Vetorial Nativo
Possui ícone exclusivo 24×24 px desenhado via GDI+ vetorial em `GlauxUrbIcons.StreetProfileAssignment`, apresentando insígnia de perfil viário à esquerda, vetor de conexão central e eixo viário com limites públicos à direita, obedecendo à paleta canônica Glaux.
