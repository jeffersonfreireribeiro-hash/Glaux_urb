# Glaux Urb — seções adaptativas e ícones

## Distribuição das transversais

`Road Transversals from GIS` conserva os dez inputs existentes e os índices dos outputs. O input, exibido como `Maximum Section Spacing` (`Step`, nickname legado), agora significa **espaçamento máximo de suporte**; zero ou valor inválido usa 25 m. A distribuição é: extremos do eixo como limites do run, eventos geométricos detectados nas quadras reais dos dois lados, união de eventos equivalentes e preenchimento das lacunas com seções de suporte. A orientação de cada corte vem da tangente local do eixo. O filtro de cruzamentos roda após gerar o candidato e continua tendo prioridade.

Somente curvas poligonais com vértices efetivos podem gerar eventos. A classificação usa deflexão em planta e suprime colinearidade, amostras de curva abaixo de 12° e pequenos segmentos de menos de 0,20 m quando a mudança é inferior a 45°. O vértice ainda precisa projetar-se até 40 m do eixo e corresponder à borda mais próxima naquele lado (desvio máximo de 0,75 m); isso evita capturar bordas de fundos de lote. No SHP de quadras reais de Picuí, há 1532 vértices úteis, mediana do menor segmento adjacente de 4,07 m e distribuição de deflexões com mediana de 7,87° e quartil superior de 89,30°. Esses limiares foram escolhidos para separar a densificação de curvas das quinas desse conjunto; sua calibração para outros cadastros continua necessária.

Eventos a até 0,25 m são unificados, exceto quinas do mesmo lado com mudança lateral superior a 0,30 m. O output novo `Section Metadata` (`SectionMeta`, índice 19) registra estaca, tipo, razão, Required/Support, Hard, lado, ID da rua, índice da geometria de quadra e vértice. `RunID` permanece não atribuído até o componente longitudinal; o status candidato é decidido pelos outputs `Rejected` e `Reasons`. O índice de quadra (`Block:n`) é local à entrada desta execução, não um ID cadastral permanente.

As quadras QL/QR continuam fixas no Planning. Uma quina real permanece representada por sua seção obrigatória e não é deslocada pela regularização dimensional. As seções de suporte reduzem lacunas, mas as larguras mínimas/máximas relatadas são **extremos amostrados**, não prova de extremos globais entre estações. Em cruzamentos, falta de bordas ou ambiguidades, um candidato pode ser rejeitado e a distância entre cortes válidos exceder o máximo solicitado.

## Verificação

- Núcleo C# `AdaptiveSectionPlanner`: 12/12 verificações sintéticas, incluindo reta, quinas em ambos os lados, vértices colineares, curva densificada, união de duplicatas, preservação de salto lateral e suporte intermediário.
- Auditoria 2D independente com os três SHPs da rua Francisco de Freitas: 12 seções no passo antigo de 25 m; 34 estações adaptativas (28 obrigatórias, 6 de suporte), 1 união de eventos, 27 quinas detectadas (21 no lado esquerdo e 6 no direito); 31 estações válidas e 3 sem bordas completas. O menor corredor amostrado passou de 8,203 m para 7,991 m; ambos são mínimos da amostra, não mínimo global comprovado. A auditoria não executa o componente no Rhino.
- [Antes × depois](../validation/urb/audit_adaptive_sections.svg) e [dados da auditoria](../validation/urb/audit_adaptive_sections.json).

## Auditoria dos ícones Glaux

| Item | Resultado |
|---|---|
| Padrão visual | Sim: `GlauxUrbIcons.cs`, bitmap ARGB transparente 24×24, desenho vetorial GDI+, blocos cinza, azul técnico, eixo âmbar e acentos verdes/vermelhos. |
| Referências | `RoadTransversals`, `RoadCrossSection`, `ShpImport` e `GpkgImport` existentes. |
| Componente alterado | `RoadTransversals` preserva o ícone próprio, pois a função central continua gerar cortes. |
| Componentes novos da pilha viária | `StreetProfileDefinition` e `SidewalkRegularization` tinham ícones emprestados; agora usam `GlauxUrbIcons.StreetProfileDefinition` e `.SidewalkRegularization`. |
| Arquitetura | Ícones criados, referenciados e compilados no gerador central existente; não foi criado outro sistema de assets. PNGs em `validation/urb/` são apenas renderizações de QA. |
| Ícones ausentes | Nenhum dos três componentes da pilha atual. |
| Inconsistências visuais observadas | Nenhuma na prévia 24×24 ampliada; verificação no canvas do Grasshopper pendente. |
| Grasshopper visual validation | **NOT TESTED**. |

[Prévia dos três ícones](../validation/urb/glaux_icon_audit.png). O render foi produzido chamando as propriedades de ícone do GHA compilado, via `System.Drawing` no .NET Framework.

## Estado

A compilação Debug/Release e a instalação do GHA são registradas no DevLog diário. A próxima etapa do 3D deve consumir as seções e os metadados de quinas, evitando interpolar através de uma quina que falhe na validação topológica ou invada um lote. É necessária execução interativa em Rhino/Grasshopper para confirmar caminhos das árvores, ícones, orientação dos cortes e comportamento nas curvas reais.



## Build e instalação final

Em 2026-09-30, `dotnet build` Debug e Release passaram com zero erros e zero avisos. `Glaux_Urb.gha` Release foi sincronizado entre `src/Urb/bin/Release/net48`, `dist` e `%APPDATA%/Grasshopper/Libraries/Glaux`, todos com SHA-256 `279C656C57F4BCA981A27EB3D85D2B952E4A0795E56332ADD7AF1230F3599484`. Há um único `Glaux_Urb.gha` na pasta oficial. **ICON RUNTIME VISUAL VALIDATION = NOT TESTED** porque o Grasshopper não foi aberto.


