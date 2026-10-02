# Glaux Urb

Plugin experimental para Rhino 8 e Grasshopper, desenvolvido em C# / .NET Framework 4.8. Combina importação GIS, definição dinâmica de perfis viários, associação de perfis a logradouros, seções adaptativas, fitting com limites de lote, geração longitudinal parcial e exportação GIS.

Versão de desenvolvimento atual: **1.2.1**. O número evolui da versão legada `1.0.0.0` exibida antes do início do histórico Git e não indica estabilidade completa.

## Estado atual

- `Street Profile Definition`: elementos variáveis em ordem esquerda→direita, com IDs e domínios de largura.
- `Street Profile Assignment`: associa perfis a feições de logradouros e preserva identificadores dos segmentos.
- `Road Transversals`, `Street Profile Fitting`, `Sidewalk Regularization`: seções, ajuste e controle de limites de lote.
- `Road Cross Section`: seções técnicas e superfícies longitudinais abertas 2.5D.
- `Export Streets to GIS`: GeoPackage relacional e conjunto SHP/CSV de compatibilidade, com CRS e unidade explícitos.
- `Import Shapefile` / `Import GeoPackage`: `Fields` ordenados, `Attributes` tipados e limpos por feição, `Geometry by Feature` e `Features` consultáveis por nome; `Attrs` mantém a saída textual anterior. SHP permite override de encoding e informa a origem usada.

O modelo 3D completo de calçadas externas, esquinas e interseções ainda está em desenvolvimento. Testes isolados e builds Debug/Release passaram em 2 de outubro de 2026; o novo exportador e a continuidade dos meios-fios ainda precisam de validação interativa no Rhino/Grasshopper e inspeção em QGIS com dados georreferenciados reais.

## Compilar

Na raiz do workspace:

```powershell
dotnet build src/Urb/Glaux_Urb.csproj -c Debug
dotnet build src/Urb/Glaux_Urb.csproj -c Release
```

Rhino 8 e Grasshopper devem estar instalados. O arquivo gerado é `src/Urb/bin/Release/net48/Glaux_Urb.gha`. A instalação ativa é `%APPDATA%\Grasshopper\Libraries\Glaux\Glaux_Urb.gha`.

## Testar

```powershell
pwsh -NoProfile -File validation/urb/test_street_profile_elements.ps1
pwsh -NoProfile -File validation/urb/test_lot_boundary_fitter.ps1
pwsh -NoProfile -File validation/urb/test_adaptive_sections.ps1
pwsh -NoProfile -File validation/urb/test_street_gis_writer.ps1
pwsh -NoProfile -File validation/urb/test_street_shp_writer.ps1
pwsh -NoProfile -File validation/urb/test_curb_run_topology.ps1
pwsh -NoProfile -File validation/urb/CheckPicuiPipeline.ps1
dotnet build validation/urb/GisImportContractTests/GisImportContractTests.csproj -c Release -p:UseAppHost=false
dotnet validation/urb/GisImportContractTests/bin/Release/net10.0/GisImportContractTests.dll
```

Os testes GIS usam o SQLite do Windows e decodificação estrutural dos arquivos SHP; não carregam RhinoCommon fora do Rhino.

## Exportação GIS

Conecte `Profiled Streets` de `Street Profile Assignment` ao exportador. `Sections`, `Fitted`, `AdaptSrf` e `AdaptLabels` acrescentam dados reais quando disponíveis. Informe EPSG, WKT (`.prj` aceito) e unidade que correspondam às coordenadas atuais; o plugin não reprojeta. Use `Export=True` como pulso, com `Overwrite=True` somente quando desejar substituir o arquivo. O GeoPackage é o formato principal. O formato SHP cria uma pasta `<nome>_shp` com camadas espaciais e CSVs relacionais.

Detalhes e limites: [relatório GIS](../../docs/GLAUX_URB_GIS_EXPORT_2026-10-01.md).

## Importação GIS e fitting

Conecte `Fields` a um painel e `Attributes` (`Values`) a outro: cada ramo `{feição}` contém valores na ordem exata de `Fields`; `NULL` aparece como `GisNullValue`. `Geometry by Feature` usa o mesmo caminho, inclusive quando uma feição contém várias partes. `Features` mantém a geometria e o mapa de atributos e pode alimentar `Street Profile Assignment.Streets`; escolha `NameField` pelo nome de coluna. Para SHP, `Encoding=Auto` usa `.cpg` ou metadados DBF conhecidos, senão informa fallback não verificado; `Encoding` manual prevalece.

`Street Profile Fitting` precisa de `Road Transversals.Pts` e preferencialmente `Road Transversals.Sections`, produzidas após ligar `Street Profile Assignment.Profiled` em `Road Transversals.Axis`. Alternativamente, use `Street Profile Definition.Profile` + `Pts` de uma via; para várias vias, ligue `SectionMeta`. Consulte `Conflicts` quando houver rejeições. [Auditoria e contrato](../../docs/GLAUX_URB_GIS_IMPORT_PROFILE_FITTING_2026-10-01.md).

A definição corrigida de Picuí está na cópia **local** `examples/picui-pipeline-corrigido.gh`; o arquivo original permanece intacto. O repositório publica apenas uma [fixture sem caminhos privados](../../validation/urb/fixtures/picui-pipeline-fixture.gh) para testar as ligações. A verificação foi feita no arquivo GH; o fluxo ainda precisa ser executado no canvas. [Diagnóstico de meios-fios](../../docs/GLAUX_URB_PIPELINE_CURB_FIX_2026-10-02.md).
