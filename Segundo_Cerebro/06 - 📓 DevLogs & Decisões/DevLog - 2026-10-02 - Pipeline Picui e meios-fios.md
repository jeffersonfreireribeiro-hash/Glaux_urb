---
date: 2026-10-02
plugin: Glaux_Urb
status: implementado_parcialmente
tags: [urb, pipeline, perfil_viario, meio_fio, auditoria]
---

# Pipeline Picuí e meios-fios

A definição real `picui.gh` ligava `Assignment.Profiled` a `Fitting.Sections`, que exige `ProfiledSection`; `Road Transversals.Axis` recebia o importador diretamente. Gerada cópia com as conexões oficiais em `examples/picui-pipeline-corrigido.gh`; original preservado. O fitting agora mostra tipo/fonte do erro e rejeita associação multivia por índice.

Na reconstrução, removidos segmentos fictícios, fillets de raio arbitrário e offsets fechados de quadras. [[Sidewalk Regularization]] agrega os pontos aceitos em curvas contínuas por `{rua;run;lado}`, separa mudanças de orientação/estação ausente e diagnostica gaps. O limite privado permanece fixo. Identidade de run entre componentes, interseções e validação visual no Rhino seguem pendentes. Ver `docs/GLAUX_URB_PIPELINE_CURB_FIX_2026-10-02.md`.

Reconciliadas alterações externas já presentes em Assignment, GIS, modelo e testes; um teste revelou que perfis independentes com larguras idênticas eram deduplicados indevidamente. A equivalência agora exige `ElementID` igual. Builds Release/Debug e testes GIS, fitting, seções adaptativas e lotes passaram. GH_IO confirmou as três conexões arquivadas; runtime GH não foi executado.
