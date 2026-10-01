# Glaux Urb — exportação GIS, 2026-10-01

## Auditoria e reconciliação

Na pasta oficial H:, antes da edição, não havia escritor GIS nem repositório Git local. `ShapefileReader` e `GpkgReader` são somente leitores; o segundo usa `winsqlite3.dll` do Windows. `ProfiledStreet` contém eixo, perfil, FeatureID de origem e atributos. `ProfiledSection` contém identidade por seção. `Fitted` contém larguras reais. `AdaptSrf` é superfície longitudinal aberta 2.5D; `AdaptLabels` transporta o GUID do elemento. Não existe ainda modelo final fechado de cruzamentos/calçadas/lotes.

## Implementação

`StreetGisWriter` é um escritor puro C# de GeoPackage via `winsqlite3.dll`, separado do componente Grasshopper. O schema usa tabelas `streets`, `street_profile_elements`, `street_centerlines`, e metadados. Tabelas de seções e superfícies só aparecem quando há registros. A camada genérica `street_element_surfaces` usa `ElementType`, sem uma camada por tipo de elemento. `StreetGIS` recebe dados existentes, exige EPSG/WKT/unidade explícitos e só grava na borda positiva de `Export`. O arquivo é construído em temporário e validado antes de substituir o destino.

`StreetShpWriter` gera um conjunto de compatibilidade em pasta `<nome>_shp`: SHP/SHX/DBF de eixos e superfícies (quando houver), PRJ/CPG e CSV UTF-8 para preservar as relações 1:N. A geometria usa PolyLineZ/PolygonZ. O formato `SHP` possui limites DBF e não equivale ao container GeoPackage; atributos acima de 120 bytes no DBF falham explicitamente.

O arquivo conserva `StreetID` semântico e `FeatureID` por segmento. A mesma rua com segmentos múltiplos mantém várias linhas em `street_centerlines` e uma em `streets`. A ordem nominal é independente do índice reduzido de `AdaptSrf` e é recuperada por `AdaptLabels`/`ElementID`.

## Verificação

- `dotnet build` Debug e Release: 0 erros, 0 avisos.
- `validation/urb/test_street_gis_writer.ps1`: leitura SQLite de volta, 1/3/7/11 elementos, dois segmentos por rua, CRS, seções, proteção contra overwrite e substituição explícita.
- `validation/urb/test_street_shp_writer.ps1`: cabeçalhos SHP/SHX, registros Z, DBF, PRJ/CPG, CSV sem perda da relação 1:N, Unicode, overwrite explícito.
- Regressões: perfil 24/24, lotes 11/11, seções adaptativas 12/12.
- Runtime Rhino/Grasshopper: não testado nesta rodada.

## Limites atuais

Superfícies de calçada externa, lotes e interseções não são sintetizadas na exportação porque o modelo longitudinal atual ainda não as emite como feições tipadas. Curvas não polilinha são amostradas para segmentos WKB/SHP; validar tolerância conforme a escala do projeto. EPSG/WKT fornecidos pelo usuário não são reprojetados, então o arquivo deve ser verificado visualmente no GIS antes de uso operacional.
