# QtoWirePlugin

QtoWirePlugin 是 AutoCAD 2023 使用的弱電 QTO 外掛，用於弱電設計、估算與繪圖過程中的 CAD 數量整理。

它的核心目的不是取代正式估算，而是把 CAD 圖塊、線段、管段、線槽等物件上的 QTO 資訊保存到 AutoCAD XData，讓後續可以快速檢查、批次修正、輸出 Excel / CSV，並銜接預算整理或工作台流程。

## 目前版本

| 版本 | 資料夾 | 目標環境 | 狀態 |
| --- | --- | --- | --- |
| v0.8.2 | `v0.8/` | AutoCAD 2023、Win64、.NET Framework 4.8 | 目前版本 |
| v0.7 | `v0.7/` | AutoCAD 2023、Win64、.NET Framework 4.8 | 前一版 |
| v0.6 | `v0.6/` | AutoCAD 2023、Win64、.NET Framework 4.8 | 舊版 |
| v0.5 | `v0.5/` | AutoCAD 2023、Win64、.NET Framework 4.8 | 舊版 |

## 給同事下載安裝

一般使用者不要使用 GitHub 的 `Code > Download ZIP`，那是原始碼，不是完整安裝包。

請到 GitHub Releases 下載：

- [QtoWirePlugin v0.8.2 Release](https://github.com/NicheSam/QtoWirePlugin/releases/tag/v0.8.2)
- [QtoWirePlugin_v0.8.2_installer.zip](https://github.com/NicheSam/QtoWirePlugin/releases/download/v0.8.2/QtoWirePlugin_v0.8.2_installer.zip)

下載後解壓縮，先完全關閉 AutoCAD，再執行：

```text
安裝或更新_QtoWirePlugin.bat
```

## v0.8.2 主要新增

- `啟用自動同步`：先建立並開啟 Excel；後續相關 QTO 物件變更會在 CAD 指令結束後自動更新，不需每次手動重整。
- `預算表設定`：可調整弱電、停管、資訊、TV、CCTV、BA、視聽音響、緊急廣播的上下順序與顯示狀態，也可控制工程分類與人員欄位。
- Excel 分成 `預算草稿`、`數量統整`、`檢查清單`、隱藏的 `CAD原始資料` 與 `QTO_SYNC_DATA`。
- 隱藏系統、零數量項目或欄位只會隱藏 Excel 列／欄，不會刪除原始資料或人工單價、廠牌、備註。
- `立即重整 Excel` 降為故障復原與強制重套版面的備援操作。
- 更新既有 Excel 時保留其他人工工作表；若無法安全保留，外掛會停止操作，不整本覆寫。
- 新增標準圖塊資料庫管理、圖案預覽、依原點／圖形中心插入與插入後自動寫入 QTO XData。
- 保留 v0.7 的樓層框、系統框、Review、修復、屬性面板、報表與 CSV 功能。

## 專案結構

```text
QtoWirePlugin/
  README.md
  v0.8/
    QtoWirePlugin.csproj
    Commands.cs
    QtoSyncMainPalette.cs
    QtoExcelWorkbookBuilder.cs
    QtoExcelComWorkbookBridge.cs
    QtoBudgetLayoutSettings.cs
    QtoBlockCatalogManagerForm.cs
    QtoWirePlugin_v0.8.bundle/
      PackageContents.xml
      m2_m4_shared_dictionary/
  v0.7/
    前一版原始碼
  v0.6/
    舊版原始碼
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

QtoWirePlugin is an AutoCAD 2023 .NET plug-in for weak-current CAD quantity takeoff. It stores QTO information in AutoCAD XData and provides property editing, review tools, scope-based floor/system assignment, a standard block catalog, and event-driven CAD-to-Excel budget draft synchronization.
