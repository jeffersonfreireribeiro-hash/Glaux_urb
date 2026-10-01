# Glaux Urb 1.1.1

## Correção

`Street Profile Fitting` lê a árvore genérica `Sections` como ramos `IGH_Goo`, eliminando o breakpoint de incompatibilidade de tipo de `GetDataTree<GH_ObjectWrapper>` visto ao abrir Grasshopper com o fluxo conectado.

## Compatibilidade

GUID, entradas, saídas e paths do componente foram preservados. Não há alteração intencional nos resultados do fitting.

## Verificação

Debug/Release e testes isolados passaram. Após reinstalação do binário 1.1.1, o usuário confirmou que o mesmo documento abriu no Rhino/Grasshopper sem o breakpoint `GetDataTree()` relatado. Os demais fluxos interativos não foram revalidados nesta rodada.
