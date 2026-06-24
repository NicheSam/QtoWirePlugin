# QtoWirePlugin

QtoWirePlugin 是一個 AutoCAD 2023 使用的 .NET 外掛，用於弱電設計、估算與繪圖過程中的 CAD 數量整理。

它的核心目的不是取代正式估算，而是把 CAD 圖塊、線段、管段、線槽等物件上的 QTO 資訊保存到 AutoCAD XData，讓後續可以快速檢查、批次修正、匯出 CSV，並銜接預算整理或工作台流程。

## 目前版本

| 版本 | 資料夾 | 目標環境 | 狀態 |
| --- | --- | --- | --- |
| v0.6 | `v0.6/` | AutoCAD 2023、Win64、.NET Framework 4.8 | 目前版本 |
| v0.5 | `v0.5/` | AutoCAD 2023、Win64、.NET Framework 4.8 | 前一版 |

## v0.6 主要新增

- AutoCAD 內的 QTO 屬性面板，操作體驗接近 Revit 性質面板。
- 中文為主的欄位名稱與 CSV 欄位。
- 多選圖塊與線段後批量修改 QTO 資訊。
- 一鍵選取尚未標註 QTO 資訊的物件。
- 依設備類型、CAD 計量型態、系統代碼、線材類型選取同類物件。
- 匯出預算前置 CSV 前，可勾選要輸出的資訊群組。

## 下載與安裝

一般使用者不要使用 GitHub 的 `Code > Download ZIP`，那是原始碼，不是完整安裝包。

請到 GitHub Releases 下載：

- [QtoWirePlugin v0.6 Release](https://github.com/NicheSam/QtoWirePlugin/releases/tag/v0.6)
- [QtoWirePlugin_v0.6_installer.zip](https://github.com/NicheSam/QtoWirePlugin/releases/download/v0.6/QtoWirePlugin_v0.6_installer.zip)

下載後解壓縮，先完全關閉 AutoCAD，再執行：

```text
安裝或更新_QtoWirePlugin.bat
```

## 專案結構

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
    前一版原始碼
```

`bin/`、`obj/`、`release/`、DLL、PDB、installer ZIP 不放入 repo。正式安裝包只放在 GitHub Releases。

## 建置需求

- Windows x64
- AutoCAD 2023
- .NET Framework 4.8 Developer Pack
- Visual Studio 或 MSBuild
- AutoCAD 2023 安裝目錄中的 managed API：
  - `AcMgd.dll`
  - `AcDbMgd.dll`
  - `AcCoreMgd.dll`
  - `AdWindows.dll`

## 主要命令

| 命令 | 用途 |
| --- | --- |
| `QTO_PANEL` | 開啟 QTO 操作面板。 |
| `QTO_PROPERTY_PANEL` | 開啟可停駐的 QTO 屬性面板。 |
| `QTO_EXPORT_BUDGET_INPUT` | 匯出預算前置 CSV，可選擇輸出欄位群組。 |
| `QTO_SELECT_UNTAGGED` | 選取模型空間中尚未標註 QTO 主要資訊的圖塊與線段。 |
| `QTO_SELECT_SAME_QTO` | 依設備類型、CAD 計量型態、系統代碼、線材類型選取同類物件。 |

## 注意事項

這個工具的定位是 CAD 到預算整理的前置輔助。匯出的 CSV 是檢查與整理用資料，不是正式契約數量或正式報價表，仍需要人工確認。

## English Summary

QtoWirePlugin is an AutoCAD 2023 .NET plug-in for weak-current CAD quantity takeoff. It stores QTO information in AutoCAD XData, provides a property palette, supports batch editing and selection tools, and exports review CSV files for budget preparation.
