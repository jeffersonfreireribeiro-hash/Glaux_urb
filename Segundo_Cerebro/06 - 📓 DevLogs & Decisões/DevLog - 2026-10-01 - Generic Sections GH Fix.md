# Correção da árvore genérica em Street Profile Fitting — 2026-10-01

Ao abrir Grasshopper, o breakpoint apontou para `GH_StructureIterator.GetDataTree`: o parâmetro em índice 6 possuía `GH_Goo`, mas o componente pediu `GH_ObjectWrapper`. A entrada `Sections` é genérica e pode transportar wrappers sem ser uma árvore tipada exatamente como `GH_ObjectWrapper`.

Foi substituída a chamada por leitura de `Params.Input[6].VolatileData`, iteração de ramos `IGH_Goo` e desembrulho de `ProfiledSection`. O contrato de paths, identidade e fitting não mudou. Versão patch `1.1.1`. Revalidação interativa no Rhino/Grasshopper ainda necessária.
