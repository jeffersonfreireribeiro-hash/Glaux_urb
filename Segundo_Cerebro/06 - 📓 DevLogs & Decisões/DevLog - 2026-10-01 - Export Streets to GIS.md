# Export Streets to GIS — 2026-10-01

- Reauditados leitores SHP/GPKG, perfis, IDs, seções, superfícies, ícones e Git local. Nenhum exportador existente.
- Criada [[Export Streets to GIS]] e escritor GeoPackage relacional, com ícone próprio.
- Decisão: `StreetID` semântico separado de `FeatureID` do segmento; `ElementID` preserva vínculo de superfícies; schema não muda com `+`.
- Debug/Release e testes puros concluídos. Rhino/Grasshopper ainda pendente.
- SHP de compatibilidade implementado como camadas SHP + CSV relacional; geometria completa de lote/calçada/interseção segue pendente por falta de contratos tipados/final 3D no pipeline atual.
