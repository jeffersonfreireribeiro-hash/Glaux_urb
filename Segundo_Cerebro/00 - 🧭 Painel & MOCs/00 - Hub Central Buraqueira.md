---
aliases: [Hub Central, Painel Principal, Dashboard]
tags: [dashboard, moc, buraqueira]
---

# 🐘 Segundo Cérebro — Ecossistema Buraqueira & Grasshopper

Bem-vindo ao centro de inteligência e desenvolvimento dos plugins **Buraqueira** para Rhinoceros e Grasshopper. Este cofre conecta código C#, arquitetura de software, teoria física/acústica, normas técnicas e gestão de lançamentos em um único fluxo ágil.

---

## 🧭 Mapas de Conteúdo (MOCs)
Navegue pelas dimensões do projeto:

* 🎛️ **[[01 - MOC - Componentes Grasshopper]]**: Catálogo vivo com fichas completas dos **135+ componentes**.
* 📐 **[[02 - MOC - Acústica & Simulação]]**: Fórmulas, traçado de raios (GPU DXR/Vulkan), transição modal (ElmerFEM) e auralização.
* 💻 **[[03 - MOC - Arquitetura C# & SDKs]]**: Guias de RhinoCommon, Grasshopper SDK, manipulação de DataTrees e ciclo de compilação.
* 📑 **[[04 - MOC - Normas Técnicas]]**: Resumo operacional da ISO 3382, NBR 12179, NBR 10152 e DIN 18041.
* 🔗 **[[05 - Contratos de Conexão e Nomenclatura de Parâmetros]]**: Matriz canônica de inputs/outputs e regras para criar novas pilhas (Pills) compatíveis.

---

## 🚀 Projetos Ativos (Solução Glaux.slnx)

| Projeto | Descrição | Status Compilação | Ficha do Projeto |
| :--- | :--- | :---: | :---: |
| **Glaux Acoustics v1.21.0** | Motor acústico autônomo e nativo Clean-Room (DirectCompute / SharpDX / PCG32) | ✅ 0 Erros / 0 Warnings (33/33 PASS) | [[Glaux Acoustics v1.21]] |
| **Glaux Acoustic Types** | DLL independente de tipos acústicos puros e contrato IVersionedAcousticStack | ✅ 0 Erros / 0 Warnings | [[Glaux Acoustic Types]] |
| **Glaux Acoustics Classic** | Motor legado mantido e segregado em aba própria (Glaux_Acoustics_Classic.gha v2.0.1) | ✅ 0 Erros / 0 Warnings | [[Glaux Acoustics Classic]] |
| **Glaux Tools v1.2.1** | Hub de dados, estatística, UI Pills, deduplicação de instâncias e visualização | ✅ 0 Erros / 0 Warnings (219/219 PASS) | [[Glaux Tools]] |
| **Glaux Urb** | Seções viárias paramétricas, importação GIS (SHP, GPKG) | ✅ Operacional | [[Glaux Urb]] |
| **Glaux GDL** | Automação paramétrica e integração BIM ArchiCAD | ✅ 14 Testes Unitários | [[Glaux GDL]] |

---

## 📦 Pacote Oficial de Distribuição (Família Glaux)
A instalação limpa e ativa no Grasshopper reside exclusivamente em:
%APPDATA%\Grasshopper\Libraries\Glaux\ (sincronizada com dist/).
Todas as bibliotecas dependentes (SharpDX, Silk.NET, etc.) permanecem íntegras para execução imediata.

---

## ⚡ Ações Rápidas de Desenvolvimento

* **Compilar Tudo (Release):**
  Execute no terminal PowerShell:
  ```powershell
  & "scripts/build.ps1" -Plugin All
  ```
* **Compilar Apenas Acoustics v1.01:**
  ```powershell
  & "scripts/build.ps1" -Plugin Acoustics_v101
  ```
* **Implantar no Grasshopper (Deploy Automatizado):**
  ```powershell
  & "scripts/deploy.ps1" -Plugin All
  ```
* **Quadro de Tarefas & Ideias:** [[Kanban - Desenvolvimento e Lançamento]]
* **Registros de Decisão de Arquitetura:**
  * [[ADR-001 - Centralização dos Plugins e Compilação Unificada]]
  * [[ADR-002 - Desacoplamento do Pachyderm e Motor Acústico Nativo v1.01]]

---

## 💡 Como aproveitar o Segundo Cérebro no Obsidian

1. **Ative o plugin Dataview (Opcional, Altamente Recomendado):**
   * Vá em *Settings* ➔ *Community Plugins* ➔ Instale o **Dataview**.
   * Ele transformará os MOCs e tabelas em visualizações automáticas e dinâmicas dos seus componentes.
2. **Ative o plugin Kanban:**
   * Permite visualizar [[Kanban - Desenvolvimento e Lançamento]] como um painel visual arrastável de tarefas (Trello-like).
3. **Crie novos componentes rapidamente:**
   * Use o modelo em [[Template - Novo Componente Grasshopper]] para desenhar a interface antes de codificar em C#.

- [x] Correção de zeros artificiais no Frequency Graph e contraste automático do Heatmap — [[DevLog - 2026-09-24 - ETC e zeros artificiais nos graficos]].


- 24/09/2026: [[DevLog - 2026-09-24 - Validacao de janelas fixas e lacunas ETC]] — janelas fixas, qualidade explícita e lacunas preservadas. Convergência do solver ainda pendente.


- 24/09/2026: [[DevLog - 2026-09-24 - Degraus como ressalva Classic e v101]] — removido veto de degrau, cálculo alinhado no RT/Graph/Map v1.01.


- [x] 24/09/2026 — Estabilidade numérica GPU Classic/v1.01: lotes, histograma float e normalização independente de N. [[DevLog - 2026-09-24 - Estabilidade GPU ETC e Contagem de Raios]].
- [ ] Validar convergência no teatro com ETCs novos, mantendo os demais parâmetros fixos.


- [x] ISM: memória, poda conservadora, índices de material; EDT inicial com qualidade separada. [[DevLog - 2026-09-24 - Image Source Ordem 2 e EDT Impulsivo]].
- [ ] Benchmark e revalidação da cena do teatro em ordem 2 com novos ETCs.


## Auditoria S3 — 25/09/2026

Ver [[DevLog - 2026-09-25 - Auditoria S3 e Acoplamento ISM GPU]]. O motor Classic passa a preservar caminhos difusos/transmitidos de baixa ordem ao excluir caminhos exclusivamente especulares cobertos pelo ISM. A revisão reproduziu os 521 T30 indisponíveis de 500 Hz; os critérios não foram relaxados. O contrato de potência/unidades da soma híbrida ainda precisa de calibração física, portanto não há aval definitivo do TR do teatro. Testes GPU e de ISM ordem 2 aprovados.


## Calibração GPU/BDPT — 26/09/2026

Ver [[DevLog - 2026-09-26 - Calibracao GPU e BDPT Preservado]]. BDPT/NEE mantido com partição por evento final, sem dupla contagem difusa; potência direcional por raio e normalização volumétrica do receptor. Fallback do direto preserva bandas zero e usa potência correta. Cache ISM invalida mudanças físicas. Testes de energia, T30, fontes e cena aberta aprovados. Os 492 pares reais dependem de novos ETCs; não houve relaxamento dos critérios de TR.

## Ray Tracing em Grids Densos (0.5m) — 28/09/2026

Ver [[DevLog - 2026-09-28 - Resolucao Travamento Ray Tracing Grids Densos 0.5m]]. Eliminação do travamento/congelamento (TDR do Windows e Gen 2 GC pauses) em malhas de plateia densas (1.600+ receptores). Acúmulo atômico direto em VRAM (zero cópias intermediárias de 100 MB via PCIe), dimensionamento adaptativo do lote de raios calibrado por clusters e saltos, proteção de spin loop CAS bounded a 64 tentativas e descompactação multithread paralela via `Parallel.For`.

## Glaux Urb — calçadas (30/09/2026)
* [[Sidewalk Regularization]] — modo existente, proposta por largura mínima e diagnóstico de conflitos; protótipo compilado, validação visual pendente.


- [[Road Transversals]] — cortes viários Existing/Planning, filtro de cruzamentos e rastreabilidade GIS.

- [[GLAUX URB — ROAD PROFILE SYSTEM]] — auditoria de seção, perfis semânticos e reconstrução por runs.
- [[Street Profile Definition]] — tipologias e fitting por seção.

- [[Lot Boundary Constraint — Glaux Urb]] — limite de lote rígido e prioridade das calçadas mínimas no planejamento.

## Glaux Urb — amostragem adaptativa e ícones (2026-09-30)
- [[Road Transversals]]: quinas dos lotes/quadras geram seções obrigatórias; `Step` limita o espaçamento dos suportes. Metadados em `SectionMeta`.
- [[Street Profile Definition]] e [[Sidewalk Regularization]]: ícones próprios 24×24 px na família `GlauxUrbIcons`.
- Relatório: [[GLAUX URB — ROAD PROFILE SYSTEM]]. Teste visual no Grasshopper pendente.

## Glaux Urb — perfil dinâmico (2026-09-30)

[[Export Streets to GIS]] escreve GeoPackage relacional com vias semânticas, eixos segmentados, elementos de perfil variáveis, seções ajustadas e superfícies adaptativas quando disponíveis. Ver `docs/GLAUX_URB_GIS_EXPORT_2026-10-01.md`; validação no Rhino pendente.
- [[Street Profile Definition]] usa `+`/`−` nativos para faixas ordenadas e emite `StreetProfile` semântico.
- [[Street Profile Fitting]] ajusta cada seção sem deslocar lotes e preserva IDs/status dos elementos.
- A versão fixa anterior fica oculta com GUID preservado; teste interativo de serialização/UI no Rhino pendente.
- Em 2026-10-01, [[Street Profile Definition]] passou a persistir tipo, sentido e obrigatoriedade em cada input variável; `Street Type` global e `ElementType` individual ficaram explícitos. Composições contraditórias geram `PROFILE_TYPE_MISMATCH`.
- [[Street Profile Definition]] agora emite `WidthDomain [mínimo,máximo]` por faixa; [[Street Profile Fitting]] limita o ajuste ao domínio e associa ruas por `SectionMeta.SourceStreetID` quando disponível. Ver `docs/GLAUX_URB_WIDTH_DOMAIN_STREET_IDENTITY_2026-10-01.md`.
- A retomada de 02h50 reutilizou [[Street Profile Assignment]] existente e criou o contrato tipado `ProfiledSection` entre [[Road Transversals]] e [[Street Profile Fitting]]. Auditoria: `docs/GLAUX_URB_REAUDIT_2026-10-01.md`.
