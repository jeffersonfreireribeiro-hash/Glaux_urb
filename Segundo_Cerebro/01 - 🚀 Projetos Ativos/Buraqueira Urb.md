---
projeto: Buraqueira Urb
status: Ativo / Release
linguagem: C# (.NET Framework 4.8 / net48)
saida: Buraqueira_Urb.gha
tags: [projeto, urbanismo, gis, grasshopper]
---

# 🏙️ Projeto: Buraqueira Urb

Módulo paramétrico para projeto urbano, seções viárias e integração GIS nativa no Grasshopper.

## 🎯 Escopo & Destaques
[[Export Streets to GIS]] acrescenta saída GeoPackage relacional de vias, perfis, seções ajustadas e superfícies geradas quando disponíveis. SHP de compatibilidade produz camadas geométricas e CSVs relacionais. O runtime Rhino/Grasshopper ainda requer validação interativa; geometrias finais de interseções não estão implementadas.
1. **Seções Viárias Paramétricas:** Montagem de calçadas, faixas de rolamento, ciclovias e canteiros via CSV e sliders.
2. **Leitores GIS Nativos:** Leitura de Shapefiles (.shp) e GeoPackage (.gpkg) em memória sem depender de softwares GIS externos.

## Sidewalk Regularization (MVP, 30/09/2026)
[[Sidewalk Regularization]] analisa um eixo por execução, limites viários e quadras reais. O modo Existing emite curvas originais; o modo de proposta gera trechos amostrados, larguras por lado, conflitos e área estimada de intervenção. Compila em Debug e Release (0 erros, 0 avisos). Validação visual e de largura contínua no Grasshopper ainda pendentes.


## Road Transversals Existing/Planning (2026-09-30)

[[Road Transversals]] agora registra candidatos e cortes rejeitados em cruzamentos, mede a situação existente e propõe largura mínima local sem deslocar eixo ou limite viário. Auditoria 2D independente: 218 candidatos, 212 aceitos e 6 rejeitados. Validação visual no Grasshopper pendente.

## Road Profile System (2026-09-30)

[[GLAUX URB — ROAD PROFILE SYSTEM]] documenta a divisão transversal/longitudinal. [[Sidewalk Regularization]] usa `Pts`/`Lines`/`Planned` para formar runs sem reamostrar GIS; [[Street Profile Definition]] define e ajusta os elementos; [[Road Cross Section]] emite superfícies adaptativas. Interseções T/+ e sólidos volumétricos continuam pendentes.

## Lot Boundary Constraint (2026-09-30)

[[Lot Boundary Constraint — Glaux Urb]] substitui o deslocamento da borda privada por ajuste do meio-fio dentro da faixa pública, com calçadas mínimas prioritárias e conflitos dimensionais detalhados. A checagem booleana dos lotes em Grasshopper ainda precisa de validação interativa antes da fase 3D.

## Seções adaptativas e ícones (2026-09-30)
[[Road Transversals]] agora usa mudanças relevantes das quadras reais nos dois lados para posicionar seções obrigatórias, completa lacunas por espaçamento máximo, preserva o filtro de interseções e emite `SectionMeta`. Auditoria independente da rua Francisco de Freitas: 12 seções no esquema regular anterior e 34 estações adaptativas (28 obrigatórias, 6 suporte); compilação/execução visual no Grasshopper ainda precisam ser distinguidas. [[Street Profile Definition]] e [[Sidewalk Regularization]] têm ícones próprios no padrão Glaux Urb. Evidências em `docs/GLAUX_URB_ADAPTIVE_SECTIONS_2026-09-30.md`.

## Street Profile Definition dinâmico (2026-09-30)
A definição atual possui `Street`, `Type` e inputs variáveis de faixas em ordem esquerda→direita, com um `Road` inicial; o menu nativo permite tipo, direção, obrigatoriedade, presets e reordenação. O componente fixo anterior permanece oculto com GUID para arquivos GH antigos. [[Street Profile Fitting]] ajusta a lista semântica às seções, preserva IDs e supressões explícitas e emite `FitPts` para [[Sidewalk Regularization]]. Testes C# 10/10; Debug/Release e instalação oficial concluídos; interface Grasshopper e arquivos `.gh` aguardam validação no Rhino.
