# Glaux Urb — Width Domain e identidade das vias (2026-10-01)

## Width Domain

Cada `StreetProfileElement` carrega `WidthDomain { Minimum, Maximum }`, além de `ElementID`, `ElementType`, `Direction` e `Required`. O input da faixa aceita um intervalo nativo do Grasshopper, texto `mínimo|máximo` com ponto decimal, ou número `n`, interpretado como `[n,n]`. Não há domínio normativo embutido; sem conexão o domínio fica `[0,0]` e um elemento obrigatório assim configurado causa conflito no fitting. `PreferredWidth` e `Type` permanecem como aliases de compatibilidade no código.

`StreetProfileElementFitter` inicia no máximo de cada elemento. Para caber na largura pública, reduz opcionais até seus mínimos, depois reduz excedentes de elementos obrigatórios (faixas viárias antes de calçadas). Se necessário, suprime opcionais por completo, com `Status=SUPPRESSED`. Uma faixa ativa nunca fica abaixo de seu mínimo. Se a supressão criar um intervalo de larguras impossível de ocupar, retorna `WIDTH_DOMAIN_GAP`; não viola o domínio. Se `AvailableWidth` exceder a soma dos máximos, retorna `EXCESS_WIDTH` e informa `Excess`; não infla `Road` nem ultrapassa o lote. Largura pública menor que a soma dos mínimos obrigatórios retorna `INSUFFICIENT_PUBLIC_WIDTH`.

O output `Adapted` inclui `Width`, `Minimum` e `Maximum`; `Road Cross Section` continua lendo `Width` como antes. O output tipado `Fitted` preserva o domínio original e o valor ajustado por seção. A checagem longitudinal existente protege os mínimos das bandas externas e a ordem dos limites; a distribuição interna das faixas externas em superfícies 3D ainda está pendente.

## Auditoria de Street Index

- `Street Profile Definition` dinâmico: **não possui** `Street Index`; `Street` contém a identidade textual da via.
- `Street Profile Definition (Legacy)`: mantém o input antigo e o GUID original para não quebrar arquivos `.gh` existentes.
- `Road Transversals`: o primeiro índice de cada caminho `{rua;estaca}` é a posição da via na lista de eixos. Serve à estrutura de árvore e pode mudar se a lista for filtrada ou reordenada.
- `Street Profile Fitting`: usava esse índice como seletor público (`StreetID`), confundindo posição e identidade. Agora o input é `Street Path Index` (`PathIdx`), definido explicitamente como fallback manual. O novo input opcional `Section Metadata` (`SectionMeta`) usa `SourceStreetID` para associar automaticamente `Street` do perfil à posição correta; inconsistência ou ambiguidade gera erro. Sem metadados, uma árvore com uma única rua é selecionada automaticamente. Com várias ruas, `PathIdx` continua necessário se não houver `SectionMeta`.

Limite da identidade de origem: o atual `SourceStreetID` de SHP/GPKG usa `arquivo:número do registro`; em curvas diretas usa `GH:posição`. Esses valores distinguem vias na execução atual, mas não são IDs duráveis após regravação do cadastro ou reordenação das curvas. Persistir um identificador GIS de atributo é uma melhoria futura. O `PathIdx` nunca substitui um `SourceStreetID` conectado.

## Verificação

Testes puros de domínio/fitting: 18/18, incluindo mínimo, máximo, largura fixa, excedente e lacuna por supressão. Regressões LotBoundary 11/11 e AdaptiveSection 12/12. Builds Debug e Release: zero erros/avisos. O GHA Release foi sincronizado entre `src/Urb/bin/Release/net48`, `dist` e `%APPDATA%/Grasshopper/Libraries/Glaux`, SHA-256 `A73B4FC9C18F03471EB882CDB0A41420110AF1CE7C010954E0CC3112BFC339D8` nas três cópias; um único `Glaux_Urb.gha` ativo. Serialização da interface, associação real por `SectionMeta` e geometria no Rhino/Grasshopper ainda requerem validação no host.
