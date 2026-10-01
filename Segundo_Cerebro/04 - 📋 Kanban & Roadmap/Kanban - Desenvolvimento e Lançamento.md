---
kanban-plugin: basic
tags: [kanban, roadmap, buraqueira]
---

# 📋 Kanban — Desenvolvimento e Lançamento Buraqueira

## 💡 Backlog & Novas Ideias
- [ ] Criar visualizador 3D de frentes de onda esféricas no Rhino
- [ ] Exportação automática de relatórios em PDF com gráficos de conformidade
- [ ] Suporte a materiais anisotrópicos e metamateriais acústicos
- [ ] Integração de IA para estimativa de coeficientes de absorção via foto

## 📐 Especificação & Design
- [ ] Novo componente: Calculador de STI (Speech Transmission Index) via MTF
- [ ] Otimização de malha para ElmerFEM automática via OpenVDB

## 💻 Em Codificação C#
- [ ] Aperfeiçoamento do solver Vulkan RTX para GPUs AMD/Intel Arc
- [ ] Interface de agrupamento no Pill Hub

## 🙋 Aguardando o usuário (Glaux Acoustics — `DEVELOPMENT_STATUS` §0 e §5)
- [ ] #9 AGUARDANDO TESTE MANUAL DO USUÁRIO (opcional; o resto foi validado com interface em 30/09): (1) abrir as abas Glaux Acoustics, Glaux Acoustics Classic e Glaux Tools e arrastar um componente de cada uma; (2) num GPU Ray Tracer (aba Glaux Acoustics) com Run = True e 5 milhões de raios, clicar "Cancelar Simulação (ESC)" no console e ver o Grasshopper voltar a responder — [[DevLog - 2026-09-30 - Instalacao, teste com interface e teatro]]
- [ ] #13b SEM AMBIENTE: não há GPU AMD/Intel Arc disponível (resposta do usuário, 29/09); o Vulkan pode ser feito na NVIDIA, a validação AMD/Intel fica NÃO VALIDADA

## 🧪 Em Teste no Grasshopper
- [ ] Glaux Acoustics (ALTA) #36: validação com RIRs medidas — Organ Room e Refeitório dos Jerónimos feitos (A/B/C); falta decidir se entra um 3º espaço — `validation/real_world/`
- [ ] Glaux Acoustics #39: Code Signing — ADIADA (SignPath exige repositório público; nada comprado nem contratado) — `docs/CODE_SIGNING.md`
- [ ] Glaux Acoustics (MÉDIA) #27: revalidar o Auditório de Referência na composição híbrida v1.21 (ISM ordem 2 + traçado com `ismOrder` = 2)
- [ ] Glaux Tools (MÉDIA): `dotnet test tests/Glaux_Tools.Tests` não compila com o Rhino 8.35 instalado (CS1705: plugin contra o Grasshopper da instalação, testes contra o pacote 8.0); com `-p:GlauxUseRhinoInstall=false` passa 219/219 (v1.2.1; documentado em `tests/README.md`)
- [ ] Glaux Acoustics: otimizações restantes do DXR (lote/ocupação, cache do shader, métricas do console); depois Vulkan e equivalência CPU × DXR × Vulkan (fila no `DEVELOPMENT_STATUS`)

## 🚀 Concluído & Lançado (Release)
- [x] Glaux Acoustics #35 & #43: Auditoria Geral do Modelo, Higienização e Publicação no GitHub (Caminho 1) (30/09/2026):
  - **Auditoria Geral Concluída:** Relatório técnico de 25 seções (`AUDITORIA_GERAL_GLAUX_ACOUSTICS.md`), matriz de confiabilidade e respostas às 11 questões fundamentais. Comprovada ausência de "fudge factors".
  - **Volume & Acoplamento:** Esclarecido que $V = 19.931\text{ m}^3$ é artefato de malha não-manifold/aberta. Volume acústico real da plateia ($\approx 7.000\text{ m}^3$) confere $T_{\text{Sabine}} = 0,864\text{ s}$ compatível com a mediana dos 465 receptores no traçado ($0,800\text{ s}$, $-7,4\% \approx 1,5\text{ JND}$).
  - **Higienização (Caminho 1):** Todas as menções nominais a "Teatro Escola" removidas e anonimizadas para "Auditório de Referência / Estudo de Caso".
  - **Sincronização & Push:** Árvore limpa commitada (`chore: sanitizacao de referencias e atualizacao de tipologias de auditorio`) e enviada com sucesso para `origin/main` no GitHub. Histórico local preservado intacto.
  - **Suíte:** 33/33 testes automatizados PASS.
- [x] Glaux (BAIXA) #45: `Glaux\README.md` reescrito para a Família Glaux, documentando a instalação exclusiva em `Libraries\Glaux\` (30/09/2026)
- [x] Glaux Acoustics (BAIXA) #44: `install.ps1` recusa execução com o Rhino aberto, garantindo instalação 100% limpa sem resíduos `*.old_*` (30/09/2026)
- [x] Glaux Acoustics v1.21.0 (#46): gerador pseudoaleatório PCG32 com fluxo independente por raio (`g_pcgInc`) e controle de fluxo explícito `[branch]` no compilador FXC; eliminação do viés estrutural de −0,5 % nas roletas; bateria estatística 8/8 (`Test-v101-RandomGenerator`) com todos os índices |z| < 4; suíte 32/32 PASS (30/09/2026)
- [x] #40: o instalador do pacote `Glaux_Acoustics_Pachyderm_Classic` não rebaixa mais arquivos (antes trocava o Types novo pelo antigo e quebrava 31 métodos do principal); `Buraqueira_Acoustic_Types.dll` removido; `Test-ClassicPackageInstaller` 8/8 com controle negativo (30/09/2026)
- [x] Glaux Acoustics v1.20.1 (#35, auditoria pré-publicação):
  - **Auditoria:** 0 segredos no histórico (com controle positivo).
  - **Corrigidos:** o caminho fixo no [[GPU Auralization]], o `PathMap`, temporários, PDFs desatualizados, avisos de licença (SharpDX, REVERBDATA CC BY-NC-SA) e aviso de IA.
  - **Nova guarda:** `NoLocalPaths`; suíte 31/31.
  - **Publicação BLOQUEADA** até a decisão do usuário sobre o histórico.
  - [[DevLog - 2026-09-30 - Auditoria pre-publicacao v1.20.1]] (30/09/2026)
- [x] Glaux Acoustics v1.20.0 (#38): roleta russa sem viés abaixo de −60 dB, com fluxo aleatório próprio. Teatro 1M: 25,0 → 20,2 s (0,81×; CONTROLADA), |ΔT30|/T30 ≤ 2·10⁻⁶, viés dentro do erro-padrão; teste novo `Test-v101-RussianRoulette` (blocos independentes). Achado #46 (gerador) — [[DevLog - 2026-09-30 - Roleta russa sem vies v1.20.0]] (30/09/2026)
- [x] Glaux Acoustics v1.19.0 (#31): nome **Glaux Acoustics** (sem "(v1.01)"; versão à parte) e **Glaux Acoustics Classic** 2.0.1 com aba e versão próprias (#41). GUIDs, classes, namespaces e arquivos iguais. Suíte 29/29; JIT Classic × Types 0 falhas; Rhino com interface A/B PASS (o `Glaux_v1.gh` abre, 157 superfícies). #42: o usuário mantém o SafeOpen corrigido — [[DevLog - 2026-09-30 - Nome Glaux Acoustics v1.19.0]] (30/09/2026)
- [x] #33: família Glaux instalada e auditada no computador novo (v1.01 1.18.0, Classic 2.0.0 do build atual, Tools 1.2.1; 0 GUIDs/assemblies duplicados; Types × Classic compatíveis no JIT); #9 automatizado no Rhino **com interface** (246 componentes, GPU 1M, recálculo, Mesh ETC + BTM); #21: `Glaux_v1.gh` com 157 superfícies e nenhuma coincidente; #26: tabela de TR verificada contra Sabine (referência analítica, não medição) — [[DevLog - 2026-09-30 - Instalacao, teste com interface e teatro]] (30/09/2026)
- [x] Glaux Tools v1.2.1 (#32): Pill Bundle Pack sem duplicação com Keys + Wires (deduplicação pela instância do transmissor; mesmo nome nos dois caminhos; Keys opcional, também em arquivos antigos); Teatro Escola 34 → 17 entradas, 314 → 157 superfícies; Rhino 24/24 (v1.2.0: 18 falhas), xUnit 219/219 — [[DevLog - 2026-09-30 - Pill Bundle Pack sem duplicacao v1.2.1]] (30/09/2026)
- [x] Glaux Acoustics v1.18.0: limite automático de reflexões cobre o tempo de corte (antes ≤ 300 truncava salas grandes: T30 −26 % em 125 Hz no Refeitório dos Jerónimos, sem aviso); contador de truncamento e aviso; suíte 29/29; +24 % de tempo em 1M no teatro (CONTROLADA) — [[DevLog - 2026-09-30 - RIRs reais e limite de reflexoes v1.18.0]] (30/09/2026)
- [x] Validação com RIRs medidas (REVERBDATA): analisador do Glaux = publicado ≤ 1 JND; refeitório: Glaux = Sabine, 1 kHz ≤ 0,4 JND da medição; sem calibração (30/09/2026)
- [x] Glaux Acoustics v1.17.0: oclusão do som direto na referência CPU (margem de 20 m em modelos em metros) com o critério da GPU; autoteste de propagação aprovado; teatro sem mudança; suíte 28/28 — [[DevLog - 2026-09-29 - Oclusao do som direto v1.17.0 e SAC]] (29/09/2026)
- [x] Glaux Acoustics v1.16.1: custo fixo do som direto 3,1 → 0,5 s por execução (a malha da sala era remontada por par; dispositivo e shader agora reutilizados); física idêntica; CONTROLADA — MESMO HARDWARE (RTX 5070); baseline da máquina nova; suíte 27/27 — [[DevLog - 2026-09-29 - Custo fixo do som direto v1.16.1]] (29/09/2026)
- [x] #20: tag `v1.15.0` conferida no GitHub, já aponta para `f21620b` (29/09/2026)
- [x] Glaux Acoustics v1.16.0: falhas silenciosas que escondiam resultado errado — difração BTM no mapeamento (4× mais tarde / ignorada), superfícies sem malha fora da simulação, energia 0 no ISM do Pachyderm Classic; suíte 26/26 — [[DevLog - 2026-09-29 - Falhas silenciosas e handoff v1.16.0]] (29/09/2026)
- [x] Teste de performance de 1.000.000 de raios (motor DXR/DirectCompute): 29 s no Teatro Escola — [[DevLog - 2026-09-29 - BVH e benchmark de 1M v1.15.0]]; a parte com interface ficou no #9 (29/09/2026)
- [x] Glaux Acoustics v1.15.2: verificador de conclusão da etapa de VRAM — lotes de receptores e integração no Grasshopper testados (`GpuMemoryRelease` 11/11); suíte 24/24 (29/09/2026)
- [x] Glaux Acoustics v1.15.1: VRAM liberada entre execuções do traçador (destruição adiada do D3D11 retinha ~115 MiB por solução); física igual; suíte 24/24 — [[DevLog - 2026-09-29 - VRAM liberada entre execucoes v1.15.1]] (29/09/2026)
- [x] Glaux Acoustics v1.15.0: BVH de triângulos (mesma física da força bruta; 16–37× no Teatro); benchmark progressivo 1k → 1M (1M em 29,3 s, T(N) linear); convergência com referência 1M (médias da sala < 0,5 JND com 250k); suíte 23/23 — [[DevLog - 2026-09-29 - BVH e benchmark de 1M v1.15.0]] (29/09/2026)
- [x] Glaux Acoustics v1.12.1–v1.13.0: versão visível no GH; volume robusto + aviso de superfícies coincidentes + m³; verificação do Teatro Escola com traçador CPU independente (T30 dentro de 1–7 %) — [[DevLog - 2026-09-29 - Verificacao do modelo Teatro Escola]] (29/09/2026)
- [x] Glaux Acoustics v1.14.1: reversão da v1.14.0 (divisão de transmissão travava o traçador com vidros internos); diagnóstico "4.000 → 300 raios/s": modelo duplicado, sem BVH, GPU subutilizada — [[DevLog - 2026-09-29 - Desempenho e reversao da v1.14.0]] (29/09/2026)
- [x] Glaux Acoustics v1.12.0: velocidade do som c(T) em todos os motores; T30 ∝ 1/c confirmado (1,05348); suíte 21/21; instalada em `Libraries\Glaux` — [[DevLog - 2026-09-29 - Velocidade do som c(T) v1.12.0]] (29/09/2026)
- [x] Glaux Acoustics v1.11.1–v1.11.2: auditoria de fundamentos (`eletroacustica1`), benchmark pyroomacoustics, itens BAIXA — [[DevLog - 2026-09-29 - Fundamentos, pyroomacoustics e v1.11.1-1.11.2]] (29/09/2026)
- [x] Glaux Acoustics: validação integrada `Test-v101-RoomValidation` (conservação α = 0, T30/EDT contra referência CPU, convergência) 13/13 — [[DevLog - 2026-09-29 - Conciliacao e Versoes 1.5-1.11]] (29/09/2026)
- [x] Glaux Acoustics v1.1.0–v1.11.0: correções físicas Q1–Q10 da auditoria (transmissão, CAS, Lundeby, espalhamento, EDT, SPL, ETC, RIR, materiais, Split-Ray, ar ISO 9613-1), cada uma com teste antes/depois, publicadas no GitHub (29/09/2026)
- [x] Comparação lado a lado com o Pachyderm 2.6: decaimento concorda ≤ 1,6 %; nível do Pachyderm tem receptor 2/3 e difuso não Lambertiano (29/09/2026)
- [x] Matriz Canônica de Parâmetros e Esquema de Conexões de Pilhas ([[05 - Contratos de Conexão e Nomenclatura de Parâmetros]]) (22/09/2026)
- [x] Adição do componente `Hierarchical Cluster Graph` (dendrograma HRP + heatmap reordenado)
- [x] Adição do componente `Pill Domain Filter` (recorte cirúrgico com modos Compactar, Preservar Ramos, Null e Clamp)
- [x] Adição do componente `Pill Number Rounder` (arredondamento multiprecisão e formatação)
- [x] Adição do componente `Box Plot Distribution` (dispersão populacional, quartis e outliers)
- [x] Adição do componente `Data Table Visualizer` (grid de dados interativo com paginação e busca no canvas)
- [x] Lançamento do pacote limpo de distribuição `dist/Buraqueiras-v1.01` (21/09/2026)
- [x] Criação do motor acústico nativo `Buraqueira_Acoustics_v101` com DirectCompute (SharpDX)
- [x] Criação da biblioteca desacoplada `Buraqueira_Acoustic_Types`
- [x] Adição do componente `Pill Time Impulse / Clock Pulse` em Tools
- [x] Adição do componente `Acoustic Propagation Self-Test` para validação física
- [x] Script de implantação automatizada `scripts/deploy.ps1`
- [x] Centralização da solução em `Buraqueiras.slnx` com 6 projetos
- [x] Compilação Release com 0 erros e 0 warnings em todos os projetos
- [x] Criação e atualização contínua do Segundo Cérebro no Obsidian

- [x] Estabilizar continuidade espacial do TR60 no grid do Glaux Classic (R², truncamento, mediana/MAD e escala robusta) — 2026-09-23

- [x] Correção de zeros artificiais no Frequency Graph e contraste automático do Heatmap — [[DevLog - 2026-09-24 - ETC e zeros artificiais nos graficos]].


- 24/09/2026: [[DevLog - 2026-09-24 - Validacao de janelas fixas e lacunas ETC]] — janelas fixas, qualidade explícita e lacunas preservadas. Convergência do solver ainda pendente.


- 24/09/2026: [[DevLog - 2026-09-24 - Degraus como ressalva Classic e v101]] — removido veto de degrau, cálculo alinhado no RT/Graph/Map v1.01.


- [x] 24/09/2026 — Estabilidade numérica GPU Classic/v1.01: lotes, histograma float e normalização independente de N. [[DevLog - 2026-09-24 - Estabilidade GPU ETC e Contagem de Raios]].
- [x] Validar convergência no teatro com ETCs novos, mantendo os demais parâmetros fixos. *(feito na v1.01: convergência com referência de 1M, `DEVELOPMENT_STATUS` §20)*


- [x] ISM: memória, poda conservadora, índices de material; EDT inicial com qualidade separada. [[DevLog - 2026-09-24 - Image Source Ordem 2 e EDT Impulsivo]].
- [ ] Benchmark e revalidação da cena do teatro em ordem 2 com novos ETCs. *(reaberto como #27 na v1.01, em "Em Teste no Grasshopper")*


## Auditoria S3 — 25/09/2026

Ver [[DevLog - 2026-09-25 - Auditoria S3 e Acoplamento ISM GPU]]. O motor Classic passa a preservar caminhos difusos/transmitidos de baixa ordem ao excluir caminhos exclusivamente especulares cobertos pelo ISM. A revisão reproduziu os 521 T30 indisponíveis de 500 Hz; os critérios não foram relaxados. O contrato de potência/unidades da soma híbrida ainda precisa de calibração física, portanto não há aval definitivo do TR do teatro. Testes GPU e de ISM ordem 2 aprovados.


## Calibração GPU/BDPT — 26/09/2026

Ver [[DevLog - 2026-09-26 - Calibracao GPU e BDPT Preservado]]. BDPT/NEE mantido com partição por evento final, sem dupla contagem difusa; potência direcional por raio e normalização volumétrica do receptor. Fallback do direto preserva bandas zero e usa potência correta. Cache ISM invalida mudanças físicas. Testes de energia, T30, fontes e cena aberta aprovados. Os 492 pares reais dependem de novos ETCs; não houve relaxamento dos critérios de TR.

## Glaux Urb — validação de calçadas
- [x] Confirmar geometria das três camadas reais de Picuí 5 e implementar o MVP [[Sidewalk Regularization]].
- [ ] Validar no Grasshopper os cenários 1,20 / 1,50 / 2,00 / 2,50 m na Rua Francisco de Freitas, ambos os lados, incluindo esquina.
- [ ] Validar largura contínua entre estacas, topologia dos anéis e área exata de intervenção antes de uso de projeto.


- [x] [[Road Transversals]]: filtro de cruzamentos, saídas Before/Rejected/Reasons/Source e modos Existing/Planning; compilação Debug/Release e auditoria geométrica.
- [ ] [[Road Transversals]]: validar visualmente no Rhino/Grasshopper, inclusive curvas, cruzamentos em nível diferente e continuidade entre estações.

- [x] [[GLAUX URB — ROAD PROFILE SYSTEM]]: auditar Road Cross Section, conectar seções válidas a runs, criar definição/fitting semântico, pedonal/canteiro e superfícies adaptativas abertas.
- [ ] Validar os novos componentes e a migração de parâmetros em definições reais do Rhino/Grasshopper.
- [ ] Construir fechamento cadastral das quadras e Intersection Builder para nós T/+; gerar sólidos adaptativos com espessuras e materiais.

- [x] [[Lot Boundary Constraint — Glaux Urb]]: limites privados fixos, calçadas mínimas prioritárias, fitting C# 11/11, conflitos por seção e filtro booleano dos lotes no código.
- [ ] Validar `Lot Boundary Constraint` no Grasshopper com as quadras reais de Picuí, especialmente os oito módulos candidatos a invasão.
- [ ] Depois da validação, completar via 3D adaptativa e interseções T/+.

## Glaux Urb — distribuição adaptativa (2026-09-30)
- [x] Eventos geométricos nas duas bordas de quadra, união por estaca/posição lateral e suporte por espaçamento máximo em [[Road Transversals]].
- [x] Metadados de seção, testes C# 12/12 e comparação GIS independente antes/depois.
- [x] Ícones distintos para [[Street Profile Definition]] e [[Sidewalk Regularization]], mantendo o ícone existente de [[Road Transversals]].
- [ ] Validar a distribuição e os ícones visualmente no Rhino/Grasshopper; ampliar a calibração para outros cadastros e curvas NURBS.

## Glaux Urb — perfil semântico dinâmico (2026-09-30)
- [x] [[Export Streets to GIS]]: GeoPackage relacional, identidade semântica/feição, largura nominal e real, ícone; testes SQLite e builds Debug/Release.
- [ ] Validar [[Export Streets to GIS]] no Rhino/Grasshopper e em QGIS com EPSG/WKT reais; completar superfícies externas quando o modelo longitudinal tipado estiver disponível.
- [x] [[Street Profile Definition]] com `IGH_VariableParameterComponent`, Street/Street Type fixos, um Road inicial, presets, menu por faixa e objeto semântico ordenado.
- [x] Metadados de cada faixa persistidos no próprio parâmetro variável, migração dos chunks posicionais anteriores e warning `PROFILE_TYPE_MISMATCH` sem alteração silenciosa.
- [x] [[Street Profile Fitting]] com fitting de mínimos, supressão explícita, IDs preservados, FitPts e integração textual com Road Cross Section.
- [x] Ícone específico de fitting e auditoria visual junto à família Glaux; builds Debug/Release e distribuição sincronizada.
- [ ] No Rhino: testar `+`/`−`, menus, presets, reordenação, Undo/Redo, Save/Open `.gh`, Copy/Paste, fios e ícones.
- [x] Width Domain `[mínimo,máximo]` por faixa, largura fixa, conflito de excedente e `PathIdx` separado de `SourceStreetID`.
- [ ] Validar no Rhino o input Interval, associação `SectionMeta` e regularização geométrica com domínios variados; evoluir `SourceStreetID` para atributo GIS persistente.
- [ ] Subdividir faixas externas Tree/Furniture/Green Strip em superfícies 3D próprias e validar transições de perfis.
- [x] Reauditoria 2026-10-01: conservar Street Profile Assignment existente, corrigir campo GIS ausente sem fallback arbitrário, IDs inválidos e fitting que lia só o primeiro segmento.
- [x] Emitir `ProfiledSection` por seção aceita e consumi-la no fitting sem repetir o matching por nome.
- [ ] Validar no Rhino o fluxo Profiled → Axis → Sections → ProfileFit e os IDs geométricos em SHP/GPKG reais.
