# QtoWirePlugin

QtoWirePlugin 是 AutoCAD 2023 使用的弱電 QTO 外掛，用於弱電設計、估算與繪圖過程中的 CAD 數量整理。

它的核心目的不是取代正式估算，而是把 CAD 圖塊、線段、管段、線槽等物件上的 QTO 資訊保存到 AutoCAD XData，讓後續可以快速檢查、批次修正、輸出 Excel / CSV，並銜接預算整理或工作台流程。

## 目前版本

| 版本 | 資料夾 | 目標環境 | 狀態 |
| --- | --- | --- | --- |
| v1.0.1 | `v1.0/` | AutoCAD 2023、Win64、.NET Framework 4.8 | 目前版本 |
| v0.8.2 | `v0.8/` | AutoCAD 2023、Win64、.NET Framework 4.8 | 前一版 |
| v0.7 | `v0.7/` | AutoCAD 2023、Win64、.NET Framework 4.8 | 舊版 |
| v0.6 | `v0.6/` | AutoCAD 2023、Win64、.NET Framework 4.8 | 舊版 |
| v0.5 | `v0.5/` | AutoCAD 2023、Win64、.NET Framework 4.8 | 舊版 |

## 給同事下載安裝

一般使用者不要使用 GitHub 的 `Code > Download ZIP`，那是原始碼，不是完整安裝包。

請到 GitHub Releases 下載：

- [QtoWirePlugin v1.0.1 Release](https://github.com/NicheSam/QtoWirePlugin/releases/tag/v1.0.1)
- [QtoWirePlugin_v1.0.1_installer.zip](https://github.com/NicheSam/QtoWirePlugin/releases/download/v1.0.1/QtoWirePlugin_v1.0.1_installer.zip)

下載後解壓縮，先完全關閉 AutoCAD，再執行：

```text
install_or_update_QtoWirePlugin.bat
```

安裝後可執行 `run_plugin_self_test.bat`，確認 AutoCAD Core、外掛 DLL 與主要 QTO 規則是否正常。

## 設計人員操作手冊

一般弱電設計人員請先閱讀 [QTO v1.0.1 弱電設計人員操作手冊](v1.0/docs/QTO_v1.0.1_designer_user_guide.html)。內容包含日常工作流程、Ribbon 圖示、34 個按鈕的用途與連動、QTO 屬性、圖塊庫、樓層／系統框、Excel 預算整理、Review 與維護方式。

- [HTML 互動閱讀版](v1.0/docs/QTO_v1.0.1_designer_user_guide.html)
- [Word 可編輯版](v1.0/docs/QTO_v1.0.1_designer_user_guide.docx)
- [PDF 列印分享版](v1.0/docs/QTO_v1.0.1_designer_user_guide.pdf)

## v1.0.1 主要能力

- `案件設定`集中確認本 DWG 的圖塊資料庫、預算 Excel、正式預算、mapping、樓層／系統框與同步狀態。
- 每張 DWG 保存自己的 Excel 連結；開啟圖面不會自動建立或打開 Excel。
- `預算完整性`檢查 CAD 有而預算未對應、預算無 CAD 來源、待確認規則、失效目標、單位衝突與重複 mapping。
- 預算無 CAD 來源的品項可多選標記為人工估算、固定數量或不適用，結果保存於 DWG。

- 匯入既有預算 Excel，保留原始工作表並建立穩定預算階層主檔。
- 支援一個 CAD 計量群組對應多個預算明細，未確認 mapping 不納入正式合計。
- 新增公司預算規則庫與案件預算綁定；公司規則不會因案件確認而自動污染。
- `預算草稿` 由 CAD 更新數量，同時保留 Excel 人工單價、成本、廠牌、備註與確認狀態。
- 新增標準圖塊差異分析與批次更新，保留實例位置、屬性與 QTO XData。
- 新增連續放置設備、QTO 線管繪製、舊物件轉換與樓層／系統範圍管理。
- 檢查清單改為中文篩選、多選定位、屬性檢視與選取項目修復。
- Excel 新增 `系統摘要`；技術原始資料與同步資料維持隱藏。
- 內附自我測試，可檢查主要規則、關聯資料與 2,000 筆合成資料效能。

v1.0.1 已通過 Release 建置、catalog、Excel、預算完整性與崇德範例預算 smoke test；真實大型案件與第二台電腦仍需持續驗收。

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

QtoWirePlugin is an AutoCAD 2023 .NET plug-in for weak-current CAD quantity takeoff. Version 1.0.1 adds per-DWG project setup, explicit Excel linking, budget coverage checks, controlled one-to-many budget mapping, standard block updates, review workflows, and event-driven CAD-to-Excel budget draft synchronization.
