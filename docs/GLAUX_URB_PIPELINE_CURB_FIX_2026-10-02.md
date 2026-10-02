# Glaux Urb — auditoria da pipeline e meios-fios (2026-10-02)

## Estado e reconciliação

Base Git auditada: `87efd24` em `main`, sincronizada com `origin/main` no início. Havia alterações externas não commitadas em Assignment, Transversals, Fitting, modelo, leitores GIS, testes e notas. Elas foram preservadas. A implementação partiu desses arquivos, sem restaurá-los para a versão do commit. O código externo compilava em Release antes das correções.

## Causa do erro de fitting

Inspeção do arquivo real `picui.gh` fornecido para a auditoria via GH_IO:

| Entrada | Ligação encontrada | Ligação correta |
|---|---|---|
| Road Transversals.Axis | Shp Import.Geometry | Assignment.Profiled |
| Street Profile Fitting.Pts | Road Transversals.Pts | Já correta |
| Street Profile Fitting.Sections | Assignment.Profiled | Road Transversals.Sections |

`Assignment.Profiled` contém `ProfiledStreet`, enquanto `Fitting.Sections` requer `ProfiledSection`. O contrato de código de `Road Transversals.Sections` é `GH_ParamAccess.tree`, ramo `{rua;estaca}`, `GH_ObjectWrapper(ProfiledSection)`; `Pts` é árvore de cinco `GH_Point` no mesmo caminho. O fitting lê a árvore genérica por `VolatileData` e desempacota `GH_ObjectWrapper`. A mensagem anterior indicava o caminho correto, mas não identificava a ligação recebida. Agora informa o tipo e a fonte efetivos. Não há Flatten/Graft no caminho corrigido.

Uma cópia local de `picui.gh` foi gravada em `examples/picui-pipeline-corrigido.gh`, preservando o arquivo original e seus caminhos GIS. O repositório publica somente `validation/urb/fixtures/picui-pipeline-fixture.gh`, com caminhos substituídos por exemplos relativos. As duas ligações foram verificadas após reabrir o arquivo GH_IO; `validation/urb/CheckPicuiPipeline.ps1` passa 3/3 na fixture e na cópia local e falha na definição original. Esse teste valida o arquivo e as conexões, **não** a execução no Rhino.

Foi removido o fallback que associava vários perfis a várias vias só pela ordem/contagem. Um perfil único ainda pode ser aplicado sem metadados quando há uma única rua. Para multivia, usar `ProfiledSection.StreetID`/perfil embutido ou `SectionMeta.SourceStreetID`.

## Causa das descontinuidades

O código anterior emitia linhas fictícias de 2 m para uma estação isolada, arredondava `PlanCurbs` por fillet de raio arbitrário e adicionava offsets de quadras fechados por tolerâncias de até 2 m. Esses elementos não provavam continuidade de `Run`, lado nem ausência de invasão de lote e podiam sobrepor os meios-fios reconstruídos. Foram removidos. `Road Transversals.PlanCurbs` agora contém somente curvas abertas de ao menos duas estacas consecutivas com orientação compatível. A borda privada original continua fixa.

Em `Sidewalk Regularization`, `PlanCurbs` e `PlanEdge` antes continham uma `LineCurve` por par de estações. Agora cada sequência de pares planejados aceitos gera uma `PolylineCurve` no ramo `{rua;run;lado}`. Fitting ausente e invasão de lote quebram a sequência. A regra antiga de distância máxima baseada em largura dividia estações consecutivas de 25 m em ruas estreitas; ela foi removida. Estação ausente, duplicada ou inversão de orientação geram `GAP` em `Conflicts` com rua, run local, seções, distância, classificação e expectativa `OPEN`.

`CurbRunTopology` concentra a regra pura de adjacência de estaca e orientação; proximidade entre pontos não estabelece identidade nem conexão. Os testes cobrem rua reta, curva, sentido invertido, estação ausente (inclusive interrupção por interseção), duplicata e direção inválida.

## Limites atuais

`ProfiledSection` ainda não carrega `RunID` nem estaca física; o `Run` de `Sidewalk Regularization` é derivado localmente e pode não corresponder a outro componente. `Road Transversals.PlanCurbs` ainda é lista plana. Não há construção validada de esquinas, cruzamentos em T/oblíquos ou anéis públicos fechados. Não se deve interpretar curvas de um run como polígono fechado. A validação de invasão de lote planejado no `Sidewalk Regularization` é conservadora e bloqueia quando faltam polígonos fechados ou falha a operação booleana; falta validar visualmente no Rhino.

## Verificações

- GH_IO: conexões da cópia 3/3 PASS; original falha como esperado.
- `StreetProfileElementFitter`: 32/32 PASS após reconciliar duplicatas de perfil por `ElementID`.
- `AdaptiveSectionPlanner`: 12/12 PASS.
- `LotBoundaryProfileFitter`: 11/11 PASS.
- `CurbRunTopology`: 6/6 PASS.
- Contratos GIS: 63/63 PASS com `UseAppHost=false`; teste em Picuí leu 34 eixos e 36 quadras reais. A primeira tentativa do teste falhou em `CreateAppHost` por arquivo mapeado aberto, não por assertiva; a execução sem apphost passou.
- Build Release e Debug: PASS, 0 erros e 0 avisos.
- Grasshopper canvas e inspeção visual antes/depois: **não testados** nesta rodada. Não há contagem confiável de fragmentos, gaps ou fechamentos reais antes/depois. Interseções e casos sintéticos geométricos completos ainda pendentes.
