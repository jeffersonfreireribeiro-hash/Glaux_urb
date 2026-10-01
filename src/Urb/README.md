# Glaux Urb

Plugin experimental para Rhino 8 e Grasshopper, desenvolvido em C# / .NET Framework 4.8. Combina importação GIS, definição dinâmica de perfis viários, associação de perfis a logradouros, seções adaptativas, fitting com limites de lote, geração longitudinal parcial e exportação GIS.

## Estado atual

- `Street Profile Definition`: elementos variáveis em ordem esquerda→direita, com IDs e domínios de largura.
- `Street Profile Assignment`: associa perfis a feições de logradouros e preserva identificadores dos segmentos.
- `Road Transversals`, `Street Profile Fitting`, `Sidewalk Regularization`: seções, ajuste e controle de limites de lote.
- `Road Cross Section`: seções técnicas e superfícies longitudinais abertas 2.5D.
- `Export Streets to GIS`: GeoPackage relacional e conjunto SHP/CSV de compatibilidade, com CRS e unidade explícitos.

O modelo 3D completo de calçadas externas, esquinas e interseções ainda está em desenvolvimento. Testes isolados e builds Debug/Release passaram em 1º de outubro de 2026; o novo exportador ainda precisa de validação interativa no Rhino/Grasshopper e inspeção em QGIS com dados georreferenciados reais.

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
```

Os testes GIS usam o SQLite do Windows e decodificação estrutural dos arquivos SHP; não carregam RhinoCommon fora do Rhino.

## Exportação GIS

Conecte `Profiled Streets` de `Street Profile Assignment` ao exportador. `Sections`, `Fitted`, `AdaptSrf` e `AdaptLabels` acrescentam dados reais quando disponíveis. Informe EPSG, WKT (`.prj` aceito) e unidade que correspondam às coordenadas atuais; o plugin não reprojeta. Use `Export=True` como pulso, com `Overwrite=True` somente quando desejar substituir o arquivo. O GeoPackage é o formato principal. O formato SHP cria uma pasta `<nome>_shp` com camadas espaciais e CSVs relacionais.

Detalhes e limites: [relatório GIS](../../docs/GLAUX_URB_GIS_EXPORT_2026-10-01.md).
