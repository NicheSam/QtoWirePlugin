# QtoWirePlugin v0.8.2

QtoWirePlugin 是 AutoCAD 2023 / Win64 使用的弱電 QTO 外掛。v0.8.2 延續標準圖塊資料庫與插入器 MVP，補齊 CAD 到 Excel 的事件式自動同步、預算草稿排列設定與人員閱讀層，讓 Excel 保持開啟時也能依 CAD 變更更新數量。

## v0.8.2 重點

- 保留 v0.7 的屬性面板、Review、Excel 同步、樓層框 / 系統框、報表功能。
- `啟用自動同步` 會先建立或連結預算 Excel；相關 QTO 物件在 CAD 指令完成後，會合併成一次更新，不使用固定秒數輪詢。
- `立即重整 Excel` 保留為初次校正、Excel 忙碌或版面規則強制重套時的備援操作，不是日常必要步驟。
- Excel 分成 `預算草稿`、`數量統整`、`檢查清單`、隱藏的 `CAD原始資料` 與 `QTO_SYNC_DATA`；人工欄位仍依穩定預算 Key 保留。
- 新增 `預算表設定`：可調整八個系統與工程分類的上下順序、顯示狀態，以及預算草稿的人員欄位。
- 自動更新開啟中的 Excel 時會保留作用中工作表與選取位置；Excel 正在編輯儲存格時會短暫重試，失敗後由同步主控提示。
- 新增 `圖塊庫` Ribbon 面板。
- 新增 `QTO_BLOCK_LIBRARY_MANAGER`：直接選擇圖例 DWG，搜尋、預覽、篩選、單筆或批次整理圖塊分類。
- 新增 `QTO_INSERT_CATALOG_BLOCK`：從圖例 DWG 的圖塊清單選擇標準圖塊，右側顯示圖案預覽，可選依圖塊原點或依圖形中心插入，插入後自動寫入 QTO XData。
- 使用者以 DWG 為圖塊來源；外掛會在專案索引位置自動維護 `.qto_catalog.json`，不需要手動產生或編輯 JSON，也不要求共用圖例資料夾具備寫入權限。
- DWG 會保存目前 catalog 路徑，Review、插入器與更新 Excel 使用同一份圖塊資料庫。
- 插入器預設顯示使用中與待設定圖塊；待設定圖塊需確認，缺失、衝突、更新中或停用圖塊不得直接插入。
- 圖塊庫管理與插入器都提供圖塊預覽，方便依圖形辨識與分類。
- 變更監看只在新增／複製 QTO 物件時掃描同步 ID，樓層／系統框採範圍快取，降低移動、編輯與命令結束時的卡頓。
- 系統代號預設分類固定為：弱電、停管、資訊、TV、CCTV、BA、視聽音響、緊急廣播。
- Review 在未設定 catalog 時只顯示一筆全域提醒，不再逐圖塊重複列出。
- 新增 `tools/ValidateRelease.ps1`，檢查建置、版本、命令、字典、catalog round-trip 與視窗啟動。
- 新增後續接口文件：`docs/QTO_FUTURE_INTERFACES_AND_UPGRADE_PLAN.md`。

## v0.8.2 不做的事

- 不做完整自動繪圖系統。
- 不做路徑自動尋路。
- 不做正式預算 mapping。
- 不做圖塊批量更新器。
- 不做專案模板。
- 不做 AI 或模糊推論。
- 不做無提示破壞性修復。

## Excel 自動同步

1. 開啟 `同步主控`；需要使用既有預算書時先選擇 Excel。
2. 按 `啟用自動同步`，外掛會先更新並開啟 Excel。
3. 後續相關 QTO 物件變更會在 CAD 指令結束後自動更新，不需要每次按重整。
4. 使用 `預算表設定`調整系統、工程分類與欄位的排列和顯示。
5. `立即重整 Excel`只在自動同步未完成或需要強制重套版面時使用。

詳細規則請見 [QTO Excel 同步與預算草稿規則](docs/QTO_EXCEL_SYNC_AND_BUDGET_LAYOUT.md)。

## 圖塊資料庫流程

1. 在 AutoCAD 開啟專案 DWG。
2. 點選 Ribbon `圖塊庫 / 管理圖塊庫`。
3. 在管理視窗按「選擇圖例 DWG」，選取公司或專案使用的標準圖例圖檔。
4. 外掛會自動讀取 DWG 圖塊，並在專案索引位置建立或更新背景索引。
5. 以中文欄位整理系統代碼、設備類型、CAD 計量型態、數量依據、單位與預設圖層。
6. 必要時多選圖塊後使用「批次設定」。
7. 完整圖塊設為「使用中」並儲存；本圖面會記住背景索引位置。
8. 點選 Ribbon `圖塊庫 / 插入圖塊`。
9. 搜尋或篩選圖塊，確認預覽與插入基準後指定插入點。

插入後會寫入下列主要 XData：

- `QTO_TYPE`
- `SYSTEM_CODE`
- `SYSTEM`
- `EQUIPMENT_TYPE_CODE`
- `QUANTITY_BASIS`
- `QTO_UNIT`
- `CABLE_TYPE`
- `CONDUIT_TYPE`
- `CONDUIT_SIZE`
- `BUDGET_ITEM_KEY`
- `QTO_SOURCE_CATALOG_ID`
- `QTO_CATALOG_VERSION`
- `QTO_SYNC_ID`
- `QTO_SYNC_STATUS`
- `QTO_LAST_MODIFIED_AT`
- `SOURCE_RULE`

## Catalog 欄位

v0.8.2 延續既有 `QtoBlockCatalog` 格式：

- `DefaultLayer`：插入時優先使用的圖層。
- `BudgetItemKey`：預算候選鍵，只作候選，不代表正式報價品項。

若 `DefaultLayer` 空白，插入器會依序使用圖例參考圖層、`QTO_系統代碼`、`QTO_BLOCK`。

## 建置環境

- AutoCAD 2023
- .NET Framework 4.8
- Visual Studio / MSBuild
- Platform target：x64

AutoCAD DLL 參考：

```text
C:\Program Files\Autodesk\AutoCAD 2023\
```

必要參考：

- `AcMgd.dll`
- `AcDbMgd.dll`
- `AcCoreMgd.dll`
- `AdWindows.dll`

## 載入方式

建議使用 bundle：

```text
QtoWirePlugin_v0.8.bundle
```

將整個 bundle 複製到：

```text
%AppData%\Autodesk\ApplicationPlugins\
```

或：

```text
%ProgramData%\Autodesk\ApplicationPlugins\
```

AutoCAD 啟動後執行：

```text
QTO_HELLO
QTO_SHOW_UI
```

Ribbon 頁籤名稱：

```text
QTO 弱電計算
```

## 開發驗證

在 PowerShell 執行：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\ValidateRelease.ps1
```

驗證內容包含 Release 建置、manifest 命令一致性、版本、bundle 字典、catalog JSON 讀寫、Review 規則與圖塊庫視窗啟動。

架構決策記錄於 [ADR-001](docs/decisions/ADR-001-project-catalog-and-review.md)。
