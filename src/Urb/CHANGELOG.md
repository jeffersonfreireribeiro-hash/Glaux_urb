# Changelog — Glaux Urb

## Unreleased

Nenhuma alteração funcional posterior ao marco 1.1.1 registrada.

## 1.1.1 — 2026-10-01

### Fixed

- `Street Profile Fitting` agora lê a entrada genérica `Sections` diretamente dos ramos `IGH_Goo`. A chamada anterior de `GetDataTree<GH_ObjectWrapper>` disparava um breakpoint do Grasshopper ao carregar o documento com `ProfiledSection` conectado.

### Verification

- Builds Debug/Release e testes puros de perfil, lotes, seções e escritores GIS. Validação após reinstalação no Rhino/Grasshopper pendente.

## 1.1.0 — 2026-10-01

### Added

- `Export Streets to GIS`: GeoPackage relacional, camadas SHP/CSV de compatibilidade, identidade semântica e por feição, perfis dinâmicos e larguras reais das seções quando disponíveis.
- Testes isolados de escrita GIS, com leitura de volta SQLite, estrutura SHP, CRS, elementos variáveis e proteção contra sobrescrita.

### Known limitations

- A execução interativa da nova pilha no Rhino/Grasshopper e a inspeção em GIS com CRS real ainda não foram testadas.
- Interseções, calçadas externas e sólidos 3D completos ainda não têm um contrato longitudinal final para exportação.

## 1.0.0.0 — versão exibida antes da adoção deste histórico

O plugin já exibia `1.0.0.0` em `GlauxUrbInfo.cs`. Esse número legado não comprova estabilidade ou validação integral do fluxo. Nenhuma versão anterior foi reconstruída artificialmente neste histórico.
