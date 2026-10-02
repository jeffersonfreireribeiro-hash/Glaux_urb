# Changelog — Glaux Urb

## Unreleased

Validação interativa no Rhino e resolução topológica de interseções pendentes.

## 1.2.1 — 2026-10-02

### Fixed

- Cópia da definição Picuí com ligações `Assignment.Profiled → Transversals.Axis` e `Transversals.Sections → Fitting.Sections`; a entrada `Pts` já estava correta.
- `Street Profile Fitting` informa tipo e fonte de `Sections` incompatível e não associa perfis a ruas diferentes pela posição na lista.
- `Road Transversals` não cria segmentos fictícios, fillets com raio arbitrário nem offsets fechados de quadras em `PlanCurbs`.
- `Sidewalk Regularization` emite uma curva por sequência planejada válida e lado, e diagnostica quebras por estação ausente, duplicação ou mudança de orientação.
- Perfis definidos separadamente com faixas de larguras iguais continuam distintos quando seus `ElementID` diferem.

### Verification and limits

- Builds Debug/Release sem erros/avisos; contratos GIS 63/63; perfil 32/32; seções 12/12; lotes 11/11; topologia de runs 6/6; suíte 42/42; escritores GPKG/SHP passaram. GH_IO confirmou as três ligações na cópia.
- Execução no canvas Rhino/Grasshopper e comparação visual de meios-fios **não testadas**. Interseções, `RunID` persistido e fronteiras públicas fechadas seguem pendentes. Ver `docs/GLAUX_URB_PIPELINE_CURB_FIX_2026-10-02.md`.

## 1.2.0 — 2026-10-01

### Added

- `Import Shapefile` e `Import GeoPackage` expõem `Attributes` (`Values`) tipados por `{feição}`, alinhados item a item a `Fields`, além de `Geometry by Feature`, `GIS Features` e `CRS`. As saídas anteriores e `Attrs` textuais continuam nos mesmos índices para compatibilidade.
- `Import Shapefile` aceita `Encoding` manual. Em Auto lê `.cpg`, depois códigos de idioma DBF conhecidos, e declara o fallback Windows-1252 como não verificado em `EncInfo`. DBF preserva inteiros, doubles, booleanos, datas e NULL distinto de texto vazio na saída genérica.
- `Street Profile Assignment` aceita diretamente `Features` dos dois importadores e busca `NameField` no dicionário tipado de atributos.

### Fixed

- Registros DBF marcados como excluídos preservam sua posição relativa ao SHP, evitando atributos associados à geometria errada.
- Leitura de metadados, nomes de campos, caminhos e texto do GeoPackage usa UTF-8 explícito no SQLite.
- `Street Profile Fitting` explica no próprio componente as ligações esperadas, tipos incorretos e conflitos por seção.

### Compatibility and verification

- Inputs/outputs existentes dos importadores são mantidos; no SHP, `Run` continua último input e passa do índice 2 para 3 após a inserção de `Encoding`. Definições `.gh` antigas com ligação explícita em `Run` devem ser conferidas ao reabrir.
- Debug/Release sem warnings; 63 verificações dos leitores SHP/GPKG e árvore comum, 32 do fitting, 11 dos lotes, 12 das seções e testes de escrita GIS passaram. Validação interativa desta versão no Rhino/Grasshopper ainda pendente.

## 1.1.1 — 2026-10-01

### Fixed

- `Street Profile Fitting` agora lê a entrada genérica `Sections` diretamente dos ramos `IGH_Goo`. A chamada anterior de `GetDataTree<GH_ObjectWrapper>` disparava um breakpoint do Grasshopper ao carregar o documento com `ProfiledSection` conectado.

### Verification

- Builds Debug/Release e testes puros de perfil, lotes, seções e escritores GIS. Após reinstalação, o mesmo documento abriu no Rhino/Grasshopper sem o breakpoint relatado (confirmado pelo usuário); outros fluxos interativos permanecem sem validação.

## 1.1.0 — 2026-10-01

### Added

- `Export Streets to GIS`: GeoPackage relacional, camadas SHP/CSV de compatibilidade, identidade semântica e por feição, perfis dinâmicos e larguras reais das seções quando disponíveis.
- Testes isolados de escrita GIS, com leitura de volta SQLite, estrutura SHP, CRS, elementos variáveis e proteção contra sobrescrita.

### Known limitations

- A execução interativa da nova pilha no Rhino/Grasshopper e a inspeção em GIS com CRS real ainda não foram testadas.
- Interseções, calçadas externas e sólidos 3D completos ainda não têm um contrato longitudinal final para exportação.

## 1.0.0.0 — versão exibida antes da adoção deste histórico

O plugin já exibia `1.0.0.0` em `GlauxUrbInfo.cs`. Esse número legado não comprova estabilidade ou validação integral do fluxo. Nenhuma versão anterior foi reconstruída artificialmente neste histórico.
