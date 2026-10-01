# Glaux Urb — reauditoria e continuação de 2026-10-01, 02h50

## Método e última etapa real

Foram lidos código C#, projeto, fichas do Segundo Cérebro, relatórios, testes e a estrutura da pasta Glaux. Na data original da auditoria, esta pasta não continha `.git`; `git status`, `git log` e `git diff` não se aplicavam. A última etapa encontrada antes daquela retomada foi a criação de `Street Profile Assignment` e sua ligação textual a `Road Transversals`/`Street Profile Fitting`, modificada por volta de 01h03. O passo seguinte era verificar o fluxo multivia de ponta a ponta e transportar o perfil em cada seção sem novo casamento. Imediatamente antes das edições, os arquivos centrais foram relidos e seus timestamps/hashes conferidos; não houve mudança adicional durante a auditoria inicial e a rechecagem.

## Estado confirmado no código

| Parte | Estado | Evidência e limite |
|---|---|---|
| Road Transversals from GIS | IMPLEMENTED / PARTIAL | Amostragem por eventos geométricos + suporte por espaçamento; filtro de cruzamentos; `Pts`, `PlanPts`, `SectionMeta` e agora `Sections` tipadas. Runtime Rhino não testado nesta retomada. |
| Street Profile Definition | IMPLEMENTED / PARTIAL | Inputs variáveis nativos, tipo/direção por parâmetro, Width Domain por elemento. Salvar/colar/desfazer no Grasshopper ainda não testados. |
| Street/Profile Elements | IMPLEMENTED | `StreetProfileElement` com ID, tipo, domínio, direção e obrigatoriedade; `ProfiledStreet` e `ProfiledSection` tipados. |
| Street Sections | PARTIALLY IMPLEMENTED | `ProfiledSection` por seção aceita carrega cinco pontos, StreetID, nome e perfil. Ainda não é um grafo topológico nem modelo longitudinal completo. |
| Profile Fitting | IMPLEMENTED / PARTIAL | Respeita mínimo/máximo, supressão explícita e QL/QR; agora aceita várias vias e usa `Sections` sem rematch. Verificação geométrica no host pendente. |
| Sidewalk / Street Regularization | PARTIALLY IMPLEMENTED | Regularização por trechos no código; transições e bandas semânticas externas ainda exigem validação geométrica. |
| Longitudinal Reconstruction | PARTIALLY IMPLEMENTED | `Road Cross Section` produz superfícies 2.5D abertas por runs; infill de nós e sólidos não implementados. |
| Intersection handling | PARTIALLY IMPLEMENTED | Filtro topológico rejeita cortes perto/através de outras vias; Junction Builder e interseções completas não implementados. |
| GIS attribute handling | PARTIALLY IMPLEMENTED | Assignment lê SHP/GPKG e NameField configurável; campo explícito ausente agora gera erro. A validação runtime dos dados no Grasshopper permanece pendente. |
| Street identification | PARTIALLY IMPLEMENTED | Atributo cadastral válido tem prioridade; fallback usa hash de nome/geometria, estável sob reordenação mas muda com alteração geométrica. `PathIdx` é posição temporária. |
| Icons | IMPLEMENTED / NEEDS REVIEW | Ícone 24×24 em `GlauxUrbIcons.StreetProfileAssignment`; inspeção visual dentro do Grasshopper pendente. Nenhuma pilha nova foi criada nesta retomada. |
| Tests | TESTED PARTIALLY | Teste puro próprio 24/24; LotBoundary 11/11; AdaptiveSection 12/12. Suíte externa de 42/42 consta em relatório anterior, mas não foi repetida por carregar RhinoCommon/GHA fora do Rhino. |

## GIS real

Auditoria independente somente do cabeçalho/registros DBF do dataset local de logradouros usado no teste: 34 feições, campos `id`, `NOME`, `TIPO`, `CEP`; todos os 34 valores de `id` aparecem como `**********`; 34 nomes preenchidos, 26 nomes distintos, cinco nomes presentes em mais de um segmento. Portanto, usar `id` sem validar geraria colisão para todas as feições. O código agora ignora atributos compostos apenas por asteriscos e usa fingerprint de nome e geometria quando não encontra ID cadastral válido. Não foi feita leitura geométrica RhinoCommon desse SHP fora do host.

## Alterações desta retomada

1. `Street Profile Assignment`: removeu nomes `Rua_N` inventados para feições sem nome; um `NameField` explícito inexistente gera erro em vez de cair na primeira coluna. IDs GIS válidos são preservados; fallback geométrico usa SHA-256 de nome e 17 amostras da curva, com orientação canônica para suportar inversão da direção da linha.
2. `Street Profile Fitting`: o input `Profile` passou a receber lista e não ignora os segmentos após o primeiro. Ambiguidade e falta de perfil geram conflitos por seção. O `PathIdx` não substitui o ID quando há metadados.
3. `Road Transversals`: acrescentou output `Sections` (`ProfiledSection`) somente para seções aceitas, contendo perfil, identidade, evento e cinco pontos. O fitting aceita esse output e verifica os extremos QL/QR contra `Pts`; nesse caminho não repete matching por nome. `SectionMeta` permanece disponível para definições existentes.

## EXTERNAL CHANGE RECONCILIATION

- **Changes detected after initial audit:** NONE. As alterações externas foram detectadas ao comparar a retomada com o estado deixado antes da pausa, não durante a auditoria e rechecagem de 02h50.
- **Files affected before this run:** `StreetProfileModel.cs`, `StreetProfileAssignment_Component.cs`, `RoadTransversals_Component.cs`, `StreetProfileFitting_Component.cs`, ícones, testes e documentação de Assignment.
- **Features completed externally:** componente Assignment, normalizador de nomes, serviço 1:N, saída `ProfiledStreet` e ícone; integração textual básica.
- **Features partially completed externally:** seleção multivia no fitting e identidade GIS; o fitting lia só o primeiro objeto e o `id` real inválido passaria como ID.
- **Planned work skipped because already done:** criação de outro componente Assignment, outro normalizador, outro tipo `ProfiledStreet` e outro ícone.
- **Existing work extended:** Assignment, Road Transversals, Fitting, modelo, testes e fichas técnicas.
- **Conflicts encountered/resolved:** campo de nome ausente deixava fallback arbitrário; corrigido. Identidade `id=**********` causaria colisão; corrigida por validação e fingerprint. Matching por texto repetido no fitting foi substituído no fluxo novo por `ProfiledSection`.
- **New components created due to architectural need:** NONE. `ProfiledSection` é classe de contrato e novo output, não uma pilha independente.

## Próximas lacunas

Validar no Rhino o fluxo `Profiled → Axis → Sections → ProfileFit`, a serialização GH e o ícone. Para 3D, subdividir bandas externas por elemento, conferir invasão de lotes entre estações e implementar um Junction Builder por topologia sem ligar runs através do nó. Esses itens permanecem **PLANNED / NOT IMPLEMENTED** onde indicado acima; documentação não substitui prova no código ou no host.

## Build e distribuição

`dotnet build` Debug e Release concluídos com zero erros e zero avisos. Testes puros: 24/24, 11/11 e 12/12. O GHA Release está em `src/Urb/bin/Release/net48`, `dist` e `%APPDATA%/Grasshopper/Libraries/Glaux`, todos com SHA-256 `DD93C8555DB0ACC05D9E13849E6FC4A2C737AB272FA6926296146C8D8D16342D`; há somente um `Glaux_Urb.gha` ativo na pasta oficial. Nenhum teste que carrega Grasshopper/RhinoCommon fora do Rhino foi repetido.
