# Família Glaux

> Este repositório Git publica o módulo **Glaux Urb** deste workspace compartilhado. O código versionado está em [`src/Urb`](src/Urb/README.md); os demais módulos da família, dependências locais, builds e dados GIS completos não fazem parte deste repositório. O Glaux Urb está em desenvolvimento experimental e ainda não possui validação completa no Rhino/Grasshopper.

O Glaux Urb usa Rhino 8, Grasshopper, C#/.NET Framework 4.8 e dados GIS SHP/GeoPackage. O fluxo atual vai da importação de logradouros e perfis dinâmicos até seções adaptativas, fitting e exportação GeoPackage/SHP. O gerador de interseções e parte do 3D longitudinal seguem pendentes. Veja [estado, compilação, testes e instalação](src/Urb/README.md) e o [changelog do módulo](src/Urb/CHANGELOG.md).

Parte do código e da documentação foi desenvolvida com assistência de IA, com revisão e testes registrados por etapa. A presença de código não indica validação de runtime no Rhino.

Base principal de desenvolvimento e manutenção dos plugins da **Família Glaux** para Rhinoceros 8 / Grasshopper.

## 📦 Plugins e Módulos da Família

| Plugin | Projeto | Identidade no Grasshopper | Saída Oficial |
| :--- | :--- | :--- | :--- |
| **Glaux Acoustics** | `src/Acoustics_v101/Glaux_Acoustics_v101.csproj` | `Glaux Acoustics` (Motor Nativo DirectCompute) | `Glaux_Acoustics_v101.gha` |
| **Glaux Acoustics Classic** | `src/Acoustics/Glaux_Acoustics.csproj` | `Glaux Acoustics Classic` (Motor Pachyderm Híbrido) | `Glaux_Acoustics.gha` |
| **Glaux Acoustic Types** | `src/AcousticTypes/Glaux_Acoustic_Types.csproj` | Biblioteca canônica desacoplada | `Glaux_Acoustic_Types.dll` |
| **Glaux Tools** | `../Glaux_Tools/src/Glaux_Tools.csproj` | `Glaux Tools` (Pilhas Funcionais & Dashboard) | `Glaux_Tools.gha` |
| **Glaux Urb** | `src/Urb/Glaux_Urb.csproj` | `Glaux Urb` (Vias, Morfologia Urbana e GIS) | `Glaux_Urb.gha` |
| **Glaux GDL** | `src/GDL/buraqueira_gdl/Glaux_GDL.csproj` | `Glaux GDL` (Exportação e Interoperabilidade BIM) | `Glaux_GDL.gha` |

---

## 🛠️ Regra Fundamental de Instalação no Grasshopper

A instalação de **todos** os plugins e dependências da família Glaux reside **EXCLUSIVAMENTE** na subpasta:
```
%APPDATA%\Grasshopper\Libraries\Glaux\
```

> [!WARNING]
> - **NUNCA** descarregar arquivos soltos na raiz de `Libraries\`.
> - **NUNCA** utilizar pastas legadas como `Libraries\Buraqueira\`.
> - O Rhino deve ser fechado antes de qualquer atualização ou cópia de binários para garantir instalação limpa e sem resíduos (`.old_*`).

---

## 💻 Procedimentos de Compilação

Todos os projetos utilizam .NET SDK (net48):

```powershell
# Compilar Glaux Acoustics (Motor Nativo)
dotnet build -c Release src/Acoustics_v101/Glaux_Acoustics_v101.csproj

# Compilar Tipos Acústicos
dotnet build -c Release src/AcousticTypes/Glaux_Acoustic_Types.csproj

# Compilar Glaux Tools (na pasta Glaux_Tools)
dotnet build -c Release ../Glaux_Tools/src/Glaux_Tools.csproj
```

---

## 🚀 Distribuição Oficial

Os pacotes e arquivos ativos em produção são distribuídos através das pastas de distribuição sincronizadas:
- `dist/Glaux_Acoustics_v101.gha`
- `dist/Glaux_Acoustic_Types.dll`
- `dist/Glaux_Tools.gha`
- `dist/versions/`: Arquivo histórico de versões oficiais (`vX.Y.Z`).

Documentação técnica, MOCs, fichas de componentes e Kanban mantêm-se continuamente atualizados na pasta [`Segundo_Cerebro/`](./Segundo_Cerebro/).
