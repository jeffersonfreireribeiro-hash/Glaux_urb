# Auditoria — importação GIS e Street Profile Fitting (2026-10-01)

## Estado anterior e consumidores

`ShpImport_Component` usa `ShapefileReader`, um parser C# interno de SHP/DBF, sem biblioteca GIS externa. `GpkgImport_Component` usa `GpkgReader`, com SQLite nativo `winsqlite3.dll`. Ambos já emitiam `Crv`, `Srf` e `Pts` achatados, `Fields` ordenados e `Attrs` por `{índice}` com textos `Campo: Valor`. Essa mistura dificultava o acesso direto aos valores e apagava os tipos. `ShpFeature` já era o modelo comum de feição (`RecordNumber`, geometria, dicionário de atributos e `GetAttribute`), reutilizado pelos dois leitores. `Street Profile Assignment` e `Road Transversals` liam os mapas de atributos dos leitores diretamente ao receber caminhos `.shp`/`.gpkg`; não dependiam do texto `Attrs`. Os outputs antigos permanecem nos mesmos índices.

## Contrato 1.2.0

| Saída | SHP / GPKG | Contrato |
|---|---|---|
| `Fields` | índices originais | Lista ordenada de nomes de campos. |
| `Attrs` | índice 4, ambos | Texto legado `Campo: Valor`, preservado para definições antigas. |
| `Attributes` (`Values`) | nova saída | Árvore genérica `{feição}`; `Values[{feição}][i]` corresponde a `Fields[i]`. Apenas valores. |
| `Geometry by Feature` | nova saída | Árvore `{feição}` com curvas, superfície e pontos da mesma feição; multipartes permanecem no ramo. |
| `Features` | nova saída | Objetos `ShpFeature` com `GetAttribute(nome)` para uso interno e `Street Profile Assignment.Streets`. |
| `CRS` | nova saída | WKT do `.prj` no SHP, ou SRS ID da camada no GPKG; sem reprojeção. |
| `EncInfo` | só SHP | Codificação e origem declarada. |

SHP mantém geometria e DBF alinhados pelo índice físico do registro. Um DBF marcado como excluído ocupa um slot nulo, sem deslocar atributos das feições seguintes. Filtros preservam a ordem relativa das feições aceitas. `Fields` não depende da ordem de enumeração do dicionário; a árvore é construída iterando o schema explicitamente. Inteiro DBF sem decimais é `long`, número decimal é `double`, lógico é `bool`, data `yyyyMMdd` é `DateTime`, célula DBF vazia vira NULL. No GPKG, SQLite fornece `long`, `double`, texto UTF-8, `byte[]` para BLOB e NULL; SQLite não fornece tipo Date distinto nesse leitor. Na árvore Grasshopper, NULL é o objeto sentinela `GisNullValue`, exibido como `NULL`, não uma string vazia.

## Encoding

`Encoding` do SHP é um override explícito. Em `Auto`, a ordem é `.cpg`, códigos DBF LDID reconhecidos (`0x01`, `0x02`, `0x03`, `0x57`) e fallback Windows-1252 rotulado `Default (unverified)`. Nome inválido, `.cpg` inválido e bytes inválidos para o decoder geram erro; o leitor não afirma detecção confiável quando ela não ocorreu. O texto e os nomes de campos do GPKG usam UTF-8 explícito na API SQLite; não há input Encoding. Os nomes dos campos DBF seguem o cabeçalho ASCII do formato legado.

## Fitting: diagnóstico e uso

O núcleo `StreetProfileElementFitter` já aceita domínio fixo (`Number` convertido em `[n,n]` pelo Definition) e variável, preserva a ordem e os IDs, protege os mínimos obrigatórios e devolve `INSUFFICIENT_PUBLIC_WIDTH`, `EXCESS_WIDTH` e `WIDTH_DOMAIN_GAP`. A dificuldade observada nesta rodada é principalmente de contrato/UX: é necessário `Pts` com cinco pontos por `{rua;estaca}` e um perfil associado; `Sections` só contém perfil se `Road Transversals.Axis` recebeu `ProfileAssign.Profiled`. `Profile` isolado funciona para uma rua; múltiplas ruas exigem `SectionMeta` ou `Sections` tipadas. O componente agora explica essas ligações, rejeita uma árvore conectada a `Sections` sem objetos `ProfiledSection` e agrega os primeiros conflitos no runtime. O breakpoint anterior de `GetDataTree<GH_ObjectWrapper>` foi corrigido em 1.1.1.

| Input | Tipo/estrutura | Obrigatoriedade e origem |
|---|---|---|
| `Profile` | lista genérica `StreetProfile` ou `ProfiledStreet` | Opcional se `Sections` contém perfis; `Definition.Profile` ou `Assignment.Profiled`. |
| `Pts` | árvore de pontos `{rua;estaca}`, cinco itens `[lote E, meio-fio E, centro, meio-fio D, lote D]` | Obrigatório; `Road Transversals.Pts`. |
| `PlanPts` | árvore com os mesmos caminhos/cinco pontos e limites de lote idênticos | Opcional; `Road Transversals.PlanPts`. |
| `PathIdx` | inteiro | `-1` padrão. Seleção posicional em caso legado, não identidade da via. |
| `Run` | booleano | Executa o cálculo. |
| `SectionMeta` | árvore de texto `{rua;estaca}` | Ponte legada para várias vias sem `Sections`. |
| `Sections` | árvore genérica `ProfiledSection` `{rua;estaca}` | Preferida; `Road Transversals.Sections` após associação de perfis. |

Fluxo preferido: `Definition.Profile → Assignment.Profiles`, importação `Features → Assignment.Streets`, `Assignment.Profiled → Road Transversals.Axis`, depois `Road Transversals.Pts/Sections → Fitting.Pts/Sections`. `Fitting.FitPts → Sidewalk Regularization.FitPts` e `Fitting.Adapted → Road Cross Section.Adapted` quando esses consumidores estiverem configurados. `FitPts` preserva os limites QL/QR recebidos. O fitting de uma única faixa `Pedestrian` é aceito; perfil sem domínio viário é rejeitado por `ROAD_DOMAIN_REQUIRED` conforme contrato atual.

## Verificação e limites

Builds Debug/Release sem warnings. Fixture SHP/DBF real com acentos, UTF-8, Windows-1252, ISO-8859-1, `.cpg`, override, LDID, NULL, números, lógico, data e registro excluído: 63/63 verificações incluindo GPKG e árvores. Fitting puro 32/32, lotes 11/11, seções 12/12, escritores GIS aprovados. Esses testes não carregam o componente Grasshopper em um canvas Rhino; inspeção visual dos Panels, persistência de fios antigos em `Run` e fluxo geométrico interativo permanecem sem validação nesta rodada.
