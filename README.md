# QtoWirePlugin

QtoWirePlugin is an AutoCAD .NET plug-in for weak-current / low-voltage CAD quantity takeoff workflows. It stores QTO information on CAD entities with AutoCAD XData, supports property editing inside AutoCAD, and exports CSV files for budget checking and downstream workbench workflows.

QtoWirePlugin ??? AutoCAD .NET ??????????????????? CAD ???????? QTO ????????? XData????????????????????????????

## Current Version

| Version | Folder | Target Environment | Status |
| --- | --- | --- | --- |
| v0.6 | `v0.6/` | AutoCAD 2023, Win64, .NET Framework 4.8 | Current development release |
| v0.5 | `v0.5/` | AutoCAD 2023, Win64, .NET Framework 4.8 | Previous release |

## What v0.6 Adds

- Revit-like QTO property palette inside AutoCAD.
- Chinese-first property labels and CSV headers.
- Batch editing for selected blocks and linework.
- Selection tools for untagged QTO objects and objects with matching QTO information.
- Budget-input CSV export with selectable information groups.
- Shared dictionary-driven equipment type, system code, CAD quantity type, and budget mapping fields.

## Download and Installation

For normal use, do not install from GitHub source ZIP. Download the installer package from GitHub Releases:

- [QtoWirePlugin v0.6 Release](https://github.com/NicheSam/QtoWirePlugin/releases/tag/v0.6)
- `QtoWirePlugin_v0.6_installer.zip`

Unzip the package and run `?????_QtoWirePlugin.bat`. Close AutoCAD before installing or updating.

## Repository Structure

```text
QtoWirePlugin/
  README.md
  v0.6/
    QtoWirePlugin.csproj
    Commands.cs
    QtoPropertyPanelForm.cs
    QtoSelectionCommands.cs
    QtoSelectSameOptionsForm.cs
    QtoWirePlugin_v0.6.bundle/
      PackageContents.xml
  v0.5/
    ... previous version source ...
```

Compiled DLLs, PDB files, build folders, and packaged installer ZIP files are intentionally excluded from the repository. User-facing installer packages are distributed through GitHub Releases.

## Build Requirements

- Windows x64
- AutoCAD 2023
- .NET Framework 4.8 Developer Pack
- Visual Studio or MSBuild with C# support
- AutoCAD managed API references from the AutoCAD 2023 install folder:
  - `AcMgd.dll`
  - `AcDbMgd.dll`
  - `AcCoreMgd.dll`
  - `AdWindows.dll`

## Main Commands

| Command | Purpose |
| --- | --- |
| `QTO_PANEL` | Open the main QTO operation panel. |
| `QTO_PROPERTY_PANEL` | Open the dockable QTO property palette. |
| `QTO_EXPORT_BUDGET_INPUT` | Export budget-input CSV with selectable fields. |
| `QTO_SELECT_UNTAGGED` | Select model-space blocks/curves missing QTO information. |
| `QTO_SELECT_SAME_QTO` | Select model-space blocks/curves matching selected QTO conditions. |

## Notes

This project is focused on practical CAD-to-budget preparation. It does not generate final contract quantities by itself, and exported CSV files should still be reviewed before use in formal budget documents.
