# Glaux Urb — Street Profile Assignment e Casamento com Vias GIS (2026-10-01)

> Correção após reauditoria de 02h50: a integração inicial de `Street Profile Fitting` lia apenas o primeiro `ProfiledStreet`; isso foi corrigido para consumir listas e, preferencialmente, a nova saída tipada `Sections` de `Road Transversals`. O campo `id` do SHP real de Picuí contém `**********` em todos os 34 registros; o componente agora o rejeita e usa fingerprint de nome e geometria como fallback. Campos de nome explicitamente solicitados e ausentes geram erro. Veja [auditoria de retomada](GLAUX_URB_REAUDIT_2026-10-01.md).

## 1. Contexto e Motivação

Nas versões anteriores do Glaux Urb, a conexão entre os perfis viários conceituais criados em `Street Profile Definition` e as seções transversais geradas em `Road Transversals` dependia de índices posicionais de caminhos de árvore de dados (`{rua;estaca}`) ou da tentativa de casamento individual via metadados de baixo nível (`SourceStreetID` baseados em número de registro do arquivo SHP/GPKG). 

Essa abordagem apresentava duas fragilidades críticas:
1. **Relação 1-para-1 rígida:** Um mesmo logradouro cadastral frequentemente possui dezenas de segmentos e trechos contíguos no arquivo GIS. Forçar o usuário a criar dezenas de perfis duplicados apenas para alimentar árvores de caminhos distintos tornava o fluxo inviável em escala urbana.
2. **Índice ≠ Identidade:** A posição ordinal de um ramo na árvore Grasshopper é volátil e muda caso a lista de feições seja filtrada, reordenada ou particionada.

O novo componente **Street Profile Assignment** resolve essa lacuna estabelecendo uma camada semântica de atribuição declarativa 1-para-N baseada no atributo cadastral do logradouro.

---

## 2. Nova Arquitetura de Atribuição

### 2.1 Componente `Street Profile Assignment` (`ProfileAssign`)
- **GUID:** `d3e4f5a6-b7c8-4d9e-0f1a-2b3c4d5e6f7a`
- **Categoria:** `Glaux Urb` | **Subcategoria:** `01 | Infraestrutura Viária`
- **Inputs:**
  1. `Streets` (`Str`): Feições GIS / eixos de logradouros (Curvas, Feições de SHP/GPKG, ou dicionários de feições).
  2. `Street Profiles` (`Profiles`): Coleção de perfis conceituais (`StreetProfile`).
  3. `Street Name Field` (`NameField`): Nome da coluna de atributos cadastrais contendo o nome do logradouro (padrão: `"NOME_LOG"`).
  4. `Match Mode` (`Mode`): `0` = Normalized (insensível a maiúsculas/minúsculas, trim e espaços múltiplos colapsados); `1` = Exact.
  5. `Run` (`Run`): Booleano de controle da execução (estritamente o último parâmetro de entrada).
- **Outputs:**
  1. `Profiled Streets` (`Profiled`): Lista de objetos tipados `ProfiledStreet`.
  2. `Matched Streets` (`Matched`): Lista de feições com perfil atribuído.
  3. `Unmatched Streets` (`Unmatched`): Feições GIS sem perfil atribuído (`UNMATCHED_STREET`).
  4. `Ambiguous Streets` (`Ambiguous`): Feições com perfis duplicados conflitantes (`AMBIGUOUS_PROFILE_MATCH`).
  5. `Assignment Report` (`Report`): Relatório diagnóstico detalhado com contagens e divergências.

### 2.2 Estrutura de Dados `ProfiledStreet`
Agrega de forma íntegra a geometria do segmento viário, o perfil atribuído e as identidades de rastreamento:
```csharp
public sealed class ProfiledStreet
{
    public object Geometry;
    public StreetProfile StreetProfile;
    public string StreetName;
    public string StreetID;
    public string SourceStreetID;
    public Dictionary<string, object> Attributes;
}
```

### 2.3 Normalização Robusta de Nomes (`StreetNameNormalizer`)
- `Normalize(string name)`: Realiza `Trim()`, converte para caixa alta invariante (`ToUpperInvariant()`) e colapsa múltiplos espaços internos consecutivos em um único espaço.
- `Matches(string nameA, string nameB, bool normalize)`: Garante comparação precisa e determinística.

### 2.4 Resolução de Conflitos e Invariantes
- **Se não cabe / não casa:** Vias não casadas são emitidas em `Unmatched` com aviso explícito no relatório; nunca recebem perfil padrão arbitrário silenciosamente.
- **Ambiguidade:** Se o usuário definir dois perfis com o mesmo nome (ex.: dois perfis chamados `"Rua Amazonas"` com seções diferentes), o sistema bloqueia o casamento automático dessas feições e as emite em `Ambiguous` com o código `AMBIGUOUS_PROFILE_MATCH`.

---

## 3. Integração com a Pilha Viária

### 3.1 Acoplamento com `Road Transversals from GIS`
O input `Logradouros (Axis)` do componente `Road Transversals` foi atualizado para desembalar nativamente objetos `ProfiledStreet`:
- Extrai a curva ou feição subjacente para a geração das seções e transversais.
- Preserva o `StreetID`, `StreetName` e o nome do perfil.
- Injeta automaticamente `Profile`, `StreetName` e `SourceStreetID` no output `SectionMeta` (`{rua;estaca}`).

### 3.2 Acoplamento com `Street Profile Fitting`
O input `Street Profile (Profile)` do componente `Street Profile Fitting` aceita diretamente `ProfiledStreet` (ou coleções de perfis):
- Indexa os perfis por `StreetName` e por `Profile`.
- Ao processar uma árvore de pontos de seções multivia (`Pts`), o componente examina o `SectionMeta` e localiza automaticamente o perfil correto para cada ramo da árvore, aplicando o mesmo perfil a todos os segmentos pertencentes à mesma via.
- O input `Street Path Index` (`PathIdx`) é mantido apenas como fallback numérico temporário quando não há metadados disponíveis na árvore.

---

## 4. Ícone Vetorial Nativo

O componente foi dotado de ícone vetorial exclusivo de 24×24 px gerado em código puro via `System.Drawing` (GDI+) em `GlauxUrbIcons.StreetProfileAssignment`:
- Insígnia de perfil viário esquemático à esquerda (duas calçadas laterais e pista central).
- Vetor direcional dinâmico com ponta de seta ao centro.
- Eixo viário com demarcação de alinhamentos prediais à direita.
- Paleta canônica Glaux com fundo verde escuro translúcido, contorno em verde sálvia e traçados nítidos com antialiasing ativo.

---

## 5. Validação e Testes Automatizados

Foi desenvolvido e executado o script de validação `validation/urb/test_glaux_urb_suite.ps1` carregando a DLL compilada em Release:

```text
==================================================
SUITE DE TESTES GLAUX URB — NOVA ARQUITETURA
==================================================
1. Testes de WidthConstraint e WidthDomain: 7/7 APROVADOS
2. Testes de Normalização de Nomes: 5/5 APROVADOS
3. Testes de Street Profile Assignment Service: 11/11 APROVADOS
4. Testes de Profile Fitting e Hard Constraints: 11/11 APROVADOS
5. Testes de Adaptive Section Planner: 8/8 APROVADOS
==================================================
RESULTADO DOS TESTES
==================================================
Passaram: 42
Falharam: 0
```

### Casos de Teste Chave Validados:
1. **Casamento 1-para-N:** 3 segmentos de "Rua Amazonas" e 1 "Rua Pará" alimentados com 1 perfil de "Rua Amazonas". Resultado: 3 matched, 1 unmatched, 0 ambíguos.
2. **Normalização:** Feição com `"  RUA   AMAZONAS  "` casa com perfil `"Rua Amazonas"`.
3. **Detecção de Ambiguidade:** Dois perfis com mesmo nome geram `AMBIGUOUS_PROFILE_MATCH` e impedem casamento incorreto.
4. **Hard Constraints de Lote:** Largura de 10.5 m cabe perfeitamente; largura de 7.0 m (menor que o mínimo de 8.6 m) falha com `INSUFFICIENT_PUBLIC_WIDTH` e relatório de déficit; largura de 15.0 m (maior que o máximo de 12.0 m) falha com `EXCESS_WIDTH` e excesso de 3.0 m sem expandir faixas silenciosamente.
5. **Seções Orientadas pela Geometria:** Vértices colineares filtrados como `COLLINEAR_OR_CURVE_SAMPLE`; quinas reais de 90° classificadas como `LOT_CORNER` e retidas como seções obrigatórias.

---

## 6. Estado de Compilação e Distribuição

- `dotnet build -c Release src/Urb/Glaux_Urb.csproj`: **0 erros, 0 avisos**.
- `dotnet build -c Debug src/Urb/Glaux_Urb.csproj`: **0 erros, 0 avisos**.
- Binários sincronizados de forma limpa em:
  - `dist/Glaux_Urb.gha` (223.744 bytes)
  - `%APPDATA%\Grasshopper\Libraries\Glaux\Glaux_Urb.gha` (223.744 bytes)
- Não existem arquivos duplicados ou resíduos (`.old`, `.previous`) nas pastas de distribuição.
- **Ressalva de Runtime:** Testes automatizados executados fora do Rhino; a inicialização de nós geométricos interativos e visualização no canvas do Grasshopper permanece pendente de validação em sessão ativa do Rhino.
