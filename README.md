# QtoWirePlugin

QtoWirePlugin 是 AutoCAD 2023 使用的弱電 QTO 外掛，用於弱電設計、估算與繪圖過程中的 CAD 數量整理。

它的核心目的不是取代正式估算，而是把 CAD 圖塊、線段、管段、線槽等物件上的 QTO 資訊保存到 AutoCAD XData，讓後續可以快速檢查、批次修正、輸出 Excel / CSV，並銜接預算整理或工作台流程。

## 目前版本

| 版本 | 資料夾 | 目標環境 | 狀態 |
| --- | --- | --- | --- |
| v1.0.0-beta | `v1.0/` | AutoCAD 2023、Win64、.NET Framework 4.8 | 目前預發行版 |
| v0.8.2 | `v0.8/` | AutoCAD 2023、Win64、.NET Framework 4.8 | 前一版 |
| v0.7 | `v0.7/` | AutoCAD 2023、Win64、.NET Framework 4.8 | 舊版 |
| v0.6 | `v0.6/` | AutoCAD 2023、Win64、.NET Framework 4.8 | 舊版 |
| v0.5 | `v0.5/` | AutoCAD 2023、Win64、.NET Framework 4.8 | 舊版 |

## 給同事下載安裝

一般使用者不要使用 GitHub 的 `Code > Download ZIP`，那是原始碼，不是完整安裝包。

請到 GitHub Releases 下載：

- [QtoWirePlugin v1.0.0-beta Release](https://github.com/NicheSam/QtoWirePlugin/releases/tag/v1.0.0-beta)
- [QtoWirePlugin_v1.0.0-beta_installer.zip](https://github.com/NicheSam/QtoWirePlugin/releases/download/v1.0.0-beta/QtoWirePlugin_v1.0.0-beta_installer.zip)

下載後解壓縮，先完全關閉 AutoCAD，再執行：

```text
install_or_update_QtoWirePlugin.bat
```

安裝後可執行 `run_plugin_self_test.bat`，確認 AutoCAD Core、外掛 DLL 與主要 QTO 規則是否正常。

## v1.0.0 Beta 主要新增

- 匯入既有預算 Excel，保留原始工作表並建立穩定預算階層主檔。
- 支援一個 CAD 計量群組對應多個預算明細，未確認 mapping 不納入正式合計。
- 新增公司預算規則庫與案件預算綁定；公司規則不會因案件確認而自動污染。
- `預算草稿` 由 CAD 更新數量，同時保留 Excel 人工單價、成本、廠牌、備註與確認狀態。
- 新增標準圖塊差異分析與批次更新，保留實例位置、屬性與 QTO XData。
- 新增連續放置設備、QTO 線管繪製、舊物件轉換與樓層／系統範圍管理。
- 檢查清單改為中文篩選、多選定位、屬性檢視與選取項目修復。
- Excel 新增 `系統摘要`；技術原始資料與同步資料維持隱藏。
- 內附自我測試，可檢查主要規則、關聯資料與 2,000 筆合成資料效能。

本版為預發行版。合成測試與範例預算驗證已通過，但正式 `v1.0.0` 前仍需要真實案件與第二台電腦完成使用驗收。

## 專案結構

```text
QtoWirePlugin/
  README.md
  v1.0/
    QtoWirePlugin.csproj
    QtoBudgetMappingForm.cs
    QtoBlockUpdateForm.cs
    QtoReviewForm.cs
    docs/
    tools/
    QtoWirePlugin_v1.0.bundle/
  v0.8/
    前一版原始碼
  v0.7/
    前一版原始碼
  v0.6/
    舊版原始碼
  v0.5/
    舊版原始碼
```

`bin/`、`obj/`、`release/`、DLL、PDB、installer ZIP 不放入 repo。可安裝 ZIP 只放在 GitHub Releases。

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

QtoWirePlugin is an AutoCAD 2023 .NET plug-in for weak-current CAD quantity takeoff. The v1.0.0 beta adds budget hierarchy import, controlled one-to-many budget mapping, company mapping profiles, standard block updates, drawing tools, review workflows, and event-driven CAD-to-Excel budget draft synchronization.
