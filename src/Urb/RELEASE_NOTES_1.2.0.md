# Glaux Urb 1.2.0 — Importação GIS e usabilidade do fitting

`Import Shapefile` e `Import GeoPackage` mantêm `Crv`, `Srf`, `Pts`, `Fields` e `Attrs` legados nos mesmos índices. A nova saída `Attributes` (`Values`) é uma árvore genérica de valores tipados por `{feição}`. O item `i` de cada ramo corresponde ao item `i` de `Fields`. NULL aparece como `GisNullValue`, distinto de string vazia. `Geometry by Feature` usa o mesmo caminho e preserva multipartes; `Features` contém o objeto GIS com atributos consultáveis pelo nome do campo.

No SHP, o input `Encoding` aceita `Auto`, UTF-8, Windows-1252, ISO-8859-1 e codificações suportadas pelo .NET. `EncInfo` informa a origem efetiva: `User`, `CPG`, `DBF-LDID` ou `Default (unverified)`. O leitor não converte coordenadas nem estima CRS; `CRS` expõe o WKT `.prj` quando presente. No GPKG, `CRS` expõe o SRS ID da camada e o texto usa UTF-8 do SQLite sem input Encoding.

`Street Profile Assignment.Streets` agora aceita diretamente `Features` dos importadores e lê `NameField` do mapa de atributos. `Street Profile Fitting` mostra instruções de ligação e avisos de conflitos por seção. O algoritmo de distribuição de faixas não foi substituído: respeita mínimos, máximos, IDs e limites dos lotes.

## Compatibilidade

`Attrs` permanece textual como legado. As novas saídas são acrescentadas ao fim. O novo `Encoding` no SHP vem antes de `Run` para manter `Run` como último input; por isso `Run` muda de índice 2 para 3. Ao abrir definições `.gh` existentes, conferir a ligação desse input. Não há migração automática de fios realizada nesta versão.

## Verificação

Builds Debug e Release sem warnings; testes isolados dos leitores e árvore GIS 63/63, fitting 32/32, lotes 11/11, seções 12/12, escrita GeoPackage e SHP aprovados. Os testes de leitores usam fixtures reais SHP/DBF em UTF-8, Windows-1252 e ISO-8859-1, `.cpg`, override e registro DBF excluído; usam GeoPackage real gerado pelo escritor do projeto. A abertura e inspeção desta versão no Rhino/Grasshopper ainda devem ser verificadas.
