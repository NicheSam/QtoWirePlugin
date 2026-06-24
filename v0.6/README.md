# QtoWirePlugin v0.6

QtoWirePlugin v0.6 is an AutoCAD 2023 .NET plug-in for weak-current CAD quantity takeoff. This version focuses on making QTO information easier to edit, inspect, batch-update, and export for budget preparation.

## Target Users

- Weak-current designers
- CAD drafters preparing quantity takeoff
- Estimators checking whether CAD items are reflected in a budget sheet
- Teams that need a traceable bridge between AutoCAD objects and CSV budget input

## Target Environment

- AutoCAD 2023
- Windows x64
- .NET Framework 4.8
- C# class library plug-in

## Main v0.6 Features

### QTO Property Palette

Command: `QTO_PROPERTY_PANEL`

The dockable property palette lets users inspect and edit QTO data on selected CAD blocks and linework. Labels are Chinese-first, with simple English codes retained where they are useful for stable mapping.

Editable fields include:

- Equipment type
- CAD quantity type
- System code
- QTO ID
- Outlet ID
- Junction box ID
- Cable type
- Floor
- Area
- Space
- Quantity basis
- Unit
- Mapping status
- Review reason

### Batch Editing

When multiple blocks or curves are selected, the property palette can batch-apply these fields:

- Equipment type
- CAD quantity type
- System code
- Cable type
- Floor
- Area
- Space

Only fields with entered values are written. Empty fields do not overwrite existing XData.

### Selection and Review Tools

| Command | Behavior |
| --- | --- |
| `QTO_SELECT_UNTAGGED` | Selects model-space `BlockReference` and `Curve` entities that do not have main QTO information. |
| `QTO_SELECT_SAME_QTO` | Opens a dialog where users choose equipment type, CAD quantity type, system code, and cable type; then selects matching model-space objects. |

Selection uses AutoCAD's normal implied selection highlight. It is temporary and can be cleared with Esc.

### Budget Input CSV Export

Command: `QTO_EXPORT_BUDGET_INPUT`

Before export, users can choose which information groups to include:

- Object identity
- CAD quantity
- System and equipment
- Location and routing
- Mapping review status

The exported CSV is intended for a workbench or estimator review process. It is not a final contract quantity document.

## CAD Quantity Types

| Display | Code | Intended Use |
| --- | --- | --- |
| ??? | `OUTLET` | Device outlet or endpoint, such as camera, data outlet, access reader, speaker. |
| ??/??? | `JUNCTION_BOX` | Junction box, terminal box, or intermediate wiring box. |
| ?? | `WIRE` | Wire or cable path. |
| ?? | `CONDUIT_SEGMENT` | Conduit segment. |
| ?? | `TRAY` | Cable tray or trunking segment. |
| ?? | `DEVICE` | Standalone device. |
| ??/??? | `PANEL` | Equipment panel, control panel, or cabinet-like equipment item. |

## Build

Open `QtoWirePlugin.csproj` in Visual Studio or build with MSBuild using `Release|x64`.

Required AutoCAD references are expected from an AutoCAD 2023 installation:

```text
C:\Program Files\Autodesk\AutoCAD 2023```

The repository intentionally excludes compiled DLLs, PDB files, `bin/`, `obj/`, and packaged release output.

## Installation for Users

Use the GitHub Release installer package rather than the source ZIP:

- `QtoWirePlugin_v0.6_installer.zip`

Unzip it, close AutoCAD, and run `?????_QtoWirePlugin.bat`.

## Limitations

- Selection tools currently scan model space only.
- Selection tools target `BlockReference` and `Curve` entities.
- Matching is exact after trimming and is case-insensitive.
- CSV exports are review inputs, not final formal budget documents.
- CAD quantity type is not the same as budget item. Equipment type is the main bridge toward budget mapping.
