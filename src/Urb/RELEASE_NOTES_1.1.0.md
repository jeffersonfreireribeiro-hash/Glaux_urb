# Glaux Urb 1.1.0

## Alterações principais

- `Export Streets to GIS` grava GeoPackage relacional com `streets`, `street_profile_elements`, `street_centerlines` e, quando há dados, seções ajustadas e superfícies semânticas.
- Formato SHP de compatibilidade gera camadas PolyLineZ/PolygonZ, PRJ/CPG e CSVs para os relacionamentos dinâmicos.
- O exportador usa EPSG, WKT e unidade explícitos e escreve somente no pulso `Export` False → True.
- O número exibido pelo Grasshopper agora deriva da propriedade `Version` em `Glaux_Urb.csproj`.

## Compatibilidade

- GUIDs dos componentes existentes foram preservados.
- O componente novo adiciona uma pilha à categoria `00 | GIS & Dados Urbanos`.
- GeoPackage é o formato completo; SHP tem nomes curtos no DBF e limites de valor.

## Verificação

- Build Debug e Release: 0 erros, 0 avisos.
- Testes puros de perfil 24/24, lotes 11/11 e seções adaptativas 12/12.
- Round trip SQLite de GeoPackage e validação estrutural SHP/SHX/DBF/PRJ/CPG passaram.
- Runtime Rhino/Grasshopper: **não testado** para o novo componente. Inspeção visual em QGIS com CRS real: **não testada**.

## Limitações conhecidas

- Calçadas externas, lotes e interseções ainda dependem da conclusão do modelo longitudinal tipado; o exportador não cria geometrias ausentes.
- Curvas NURBS são discretizadas ao converter para GIS; conferir a tolerância na escala do projeto.
- Não há reprojeção automática; as coordenadas atuais precisam corresponder ao EPSG/WKT informados.
