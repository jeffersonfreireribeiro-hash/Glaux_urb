# Glaux Urb 1.1.1

## Correção

`Street Profile Fitting` lê a árvore genérica `Sections` como ramos `IGH_Goo`, eliminando o breakpoint de incompatibilidade de tipo de `GetDataTree<GH_ObjectWrapper>` visto ao abrir Grasshopper com o fluxo conectado.

## Compatibilidade

GUID, entradas, saídas e paths do componente foram preservados. Não há alteração intencional nos resultados do fitting.

## Verificação

Debug/Release e testes isolados passaram. A abertura do documento no Rhino/Grasshopper com o binário 1.1.1 ainda precisa ser revalidada.
