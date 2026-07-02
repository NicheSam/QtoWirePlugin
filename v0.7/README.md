# QtoWirePlugin v0.7

QtoWirePlugin v0.7 是 AutoCAD 2023 使用的弱電 QTO 外掛，重點是讓 CAD 圖面中的圖塊、線段、管線、線槽可以帶有 QTO 資訊，並把圖面數量整理成可檢查、可延伸成預算草稿的 Excel。

這版的目標不是取代正式估算，而是降低弱電設計、繪圖、估算之間重複查數量、重開項、人工比對圖面與預算表的成本。

## 一般使用者安裝

一般使用者請不要下載 GitHub 的 `Source code` 或 `Code > Download ZIP`，那些是原始碼，不能直接安裝。

請從 GitHub Release 下載安裝包：

- `QtoWirePlugin_v0.7_installer.zip`

安裝方式：

1. 完全關閉 AutoCAD。
2. 解壓縮 `QtoWirePlugin_v0.7_installer.zip`。
3. 執行 `安裝或更新_QtoWirePlugin.bat`。
4. 開啟 AutoCAD 2023。
5. 上方 Ribbon 應出現 `QTO 弱電計算`。

若沒有看到 Ribbon，可在命令列輸入：

```text
QTO_HELLO
QTO_PANEL
```

## v0.7 重點

- 新增 `更新預算 Excel` 流程：依 CAD 現況更新 Excel 數量，保留 Excel 內人工填寫的單價、備註、確認狀態等欄位。
- 新增同步主控視窗：集中處理 Excel 路徑、更新、檢查、修復與同步紀錄。
- 新增 `預算草稿` 與 `數量統整` 工作表，讓使用者先看到可整理預算的表，而不是只看到物件級資料。
- 保留 `QTO_LIVE`、`QTO_REVIEW`、`QTO_SYNC_DATA` 作為底層檢查與同步資料。
- 新增樓層框與系統框：用聚合線範圍批次覆蓋有 QTO 資訊物件的樓層或系統代碼。
- 樓層框與系統框分開管理，兩者重疊處的物件才會同時取得樓層與系統。
- 新增 Review / 修復流程，用於檢查缺系統、缺設備類型、缺樓層、缺同步 ID 等問題。
- 整理 Ribbon 面板，保留常用的標記、編輯、刪除、指向箱體、編號標註、報表與同步功能。

## 建議使用流程

1. 在 AutoCAD 中完成弱電圖塊、線段、管線、線槽繪製。
2. 使用屬性面板或批量修改，補上 QTO 主要資訊：
   - CAD 計量型態
   - 系統代碼
   - 設備類型
   - 線材類型
   - 樓層
   - 區域
   - 空間
3. 必要時使用樓層框與系統框，快速覆蓋範圍內已具備 QTO 資訊的物件。
4. 開啟同步主控。
5. 按 `更新預算 Excel` 產生或更新 Excel。
6. 在 Excel 的 `預算草稿` 填寫單價、備註、確認狀態或預算對應。
7. 回到 CAD 修改圖面後，再按 `更新預算 Excel`，讓數量更新但保留人工欄位。
8. 使用 `檢查問題 / 修復` 找出缺資料與待確認項目。

## Excel 工作表

| 工作表 | 用途 |
| --- | --- |
| `預算草稿` | 給使用者整理預算，保留單價、備註、確認狀態等人工欄位。 |
| `數量統整` | 依系統、設備、線材、樓層等方式彙總數量。 |
| `QTO_LIVE` | 物件級資料，主要供檢查與除錯。 |
| `QTO_REVIEW` | 缺資料、缺對應、待人工確認的項目。 |
| `QTO_SYNC_DATA` | 同步用資料，通常不需要人工編輯。 |

## 重要限制

- 第一版以 CAD 作為數量來源，Excel 作為整理與人工編修工作簿。
- Excel 可以保留人工欄位，但不作為第一版回寫 CAD 的來源。
- 若要讓開啟中的 Excel 被更新，建議由外掛的同步主控開啟或建立 Excel，避免手動開啟造成檔案鎖定或 COM 連線失敗。
- 匯出的 Excel 與 CSV 是預算檢查與整理資料，不是正式契約數量或正式報價表。
- 樓層框與系統框只會處理已經有 QTO 資訊的物件；沒有 QTO 資訊的普通 CAD 物件會跳過。

## 開發者建置需求

- Windows x64
- AutoCAD 2023
- .NET Framework 4.8 Developer Pack
- Visual Studio 或 MSBuild
- AutoCAD 2023 managed API：
  - `AcMgd.dll`
  - `AcDbMgd.dll`
  - `AcCoreMgd.dll`
  - `AdWindows.dll`

建置輸出 DLL 不放入 GitHub source folder。正式給同事安裝的 DLL 只放在 GitHub Release 的 installer zip。

## 常用命令

| 命令 | 用途 |
| --- | --- |
| `QTO_PANEL` | 開啟 QTO 操作面板。 |
| `QTO_PROPERTY_PANEL` | 開啟 QTO 屬性面板。 |
| `QTO_SYNC_PANEL` | 開啟同步主控。 |
| `QTO_SYNC_FULL_REBUILD` | 依 CAD 現況更新預算 Excel。 |
| `QTO_REVIEW` | 開啟 Review 清單。 |
| `QTO_SCOPE_DEFINE_FLOOR` | 建立樓層框。 |
| `QTO_SCOPE_DEFINE_SYSTEM` | 建立系統框。 |
| `QTO_SCOPE_APPLY_ALL` | 套用全圖樓層 / 系統框規則。 |
| `QTO_SELECT_UNTAGGED` | 選取尚未標註 QTO 主要資訊的物件。 |
| `QTO_SELECT_SAME_QTO` | 依 QTO 條件選取相同資訊物件。 |

## 版本定位

v0.7 是工作流程整合版，重點放在 CAD 到 Excel 預算整理的低摩擦流程。後續若要做 Excel 回寫 CAD、正式預算 ItemMaster、多人協作或資料庫同步，應作為獨立階段開發。
