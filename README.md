# QtoWirePlugin

QtoWirePlugin 是 AutoCAD 2023 使用的弱電 QTO 外掛，用於弱電設計、估算與繪圖過程中的 CAD 數量整理。

它的核心目的不是取代正式估算，而是把 CAD 圖塊、線段、管段、線槽等物件上的 QTO 資訊保存到 AutoCAD XData，讓後續可以快速檢查、批次修正、輸出 Excel / CSV，並銜接預算整理或工作台流程。

## 目前版本

| 版本 | 資料夾 | 目標環境 | 狀態 |
| --- | --- | --- | --- |
| v0.7 | `v0.7/` | AutoCAD 2023、Win64、.NET Framework 4.8 | 目前版本 |
| v0.6 | `v0.6/` | AutoCAD 2023、Win64、.NET Framework 4.8 | 前一版 |
| v0.5 | `v0.5/` | AutoCAD 2023、Win64、.NET Framework 4.8 | 舊版 |

## 給同事下載安裝

一般使用者不要使用 GitHub 的 `Code > Download ZIP`，那是原始碼，不是完整安裝包。

請到 GitHub Releases 下載：

- [QtoWirePlugin v0.7 Release](https://github.com/NicheSam/QtoWirePlugin/releases/tag/v0.7)
- [QtoWirePlugin_v0.7_installer.zip](https://github.com/NicheSam/QtoWirePlugin/releases/download/v0.7/QtoWirePlugin_v0.7_installer.zip)

下載後解壓縮，先完全關閉 AutoCAD，再執行：

```text
安裝或更新_QtoWirePlugin.bat
```

## v0.7 主要新增

- `更新預算 Excel`：依 CAD 現況更新 Excel 數量，保留 Excel 內人工填寫的單價、備註、確認狀態。
- `同步主控`：集中處理 Excel 路徑、更新、檢查、修復與同步紀錄。
- `預算草稿`：接近預算整理的工作表，不再只輸出物件級資料。
- `數量統整`：彙整樓層、系統、設備、線材等數量。
- `樓層框 / 系統框`：用聚合線範圍快速覆蓋有 QTO 資訊物件的樓層或系統代碼。
- `Review / 修復`：檢查缺系統、缺設備類型、缺樓層、缺同步 ID 等問題。
- Ribbon 面板整理，保留常用的標記、編輯、刪除、指向箱體、編號標註、報表與同步功能。

## 專案結構

```text
QtoWirePlugin/
  README.md
  v0.7/
    QtoWirePlugin.csproj
    Commands.cs
    QtoSyncMainPalette.cs
    QtoExcelWorkbookBuilder.cs
    QtoExcelComWorkbookBridge.cs
    QtoScopeService.cs
    QtoReviewForm.cs
    QtoWirePlugin_v0.7.bundle/
      PackageContents.xml
      m2_m4_shared_dictionary/
  v0.6/
    前一版原始碼
  v0.5/
    舊版原始碼
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

## 使用定位

這個工具的定位是 CAD 到預算整理的前置輔助。匯出的 Excel 與 CSV 是檢查與整理用資料，不是正式契約數量或正式報價表，仍需要人工確認。

## English Summary

QtoWirePlugin is an AutoCAD 2023 .NET plug-in for weak-current CAD quantity takeoff. It stores QTO information in AutoCAD XData, provides property editing, review tools, scope-based floor/system assignment, and Excel/CSV outputs for budget preparation.
