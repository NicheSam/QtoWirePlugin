# QtoWirePlugin v1.0.1

QtoWirePlugin 是 AutoCAD 2023 / Win64 使用的弱電 QTO 外掛。v1.0.1 在既有繪圖、圖塊、Review 與 Excel 同步能力上，補上案件初始化與預算完整性檢查流程。

## v1.0.1 重點

- 新增 `案件設定`：集中查看本 DWG 的圖塊資料庫、預算 Excel、正式預算、mapping、樓層／系統框與自動同步狀態。
- 同步 Excel 路徑改為隨 DWG 保存；切換圖面時不再沿用另一張圖的執行期路徑。
- 開啟 DWG 不會建立或打開 Excel，只有使用者明確選擇、建立或啟用同步時才操作 Excel。
- 新增 `預算完整性`：檢查 CAD 有而預算未對應、預算無 CAD 來源、待確認／阻擋規則、失效目標、單位衝突與重複 mapping。
- 預算無 CAD 來源的品項可多選標記為人工估算、固定數量或不適用，分類保存於 DWG，不修改原始預算工作表。
- 完整性問題整合到既有 Review 與 `QTO_REVIEW`，不建立第二套問題資料庫。
- Ribbon、工作流程面板、同步主控與預算對應視窗使用相同入口。

使用流程與資料邊界請見 [案件設定與預算完整性](docs/QTO_V1_0_1_PROJECT_SETUP_AND_BUDGET_COMPLETENESS.md)。

## v1.0.0 Beta 重點

- 公司預算規則庫以公司標準預算作基準，案件只保存綁定與稽核快照。
- 公司品項採語意穩定 ID，Excel 插入列不會直接破壞既有規則。
- 別名比對只建立待確認候選，不會自動視為正式 mapping。
- 操作面板新增連續放置設備、直接繪製 QTO 線管與既有物件轉換。
- 新增樓層／系統範圍管理器，可檢查範圍、優先序及預計影響數。
- 圖塊更新器並列顯示專案目前圖塊與公司標準圖塊。
- `QTO_BUDGET_BINDINGS` 隱藏工作表保存公司品項與案件品項的對應紀錄。
- Ribbon 新增精簡的「快速繪圖」入口，配線與管段會自動進入 QTO 標準圖層。
- Review 支援中文篩選、多選定位、開啟屬性與只修復選取的同步 ID 問題。
- Excel 新增可見的 `系統摘要`，技術原始層與同步層仍維持隱藏。

v1.0.0 Beta 已通過合成 CAD 與崇德範例預算測試；真實大型案件與第二台電腦仍須持續驗收。

合成效能基線另以 2,000 筆暫時 QTO 配線測試資料列建立與檢查掃描；測試物件不會保留在 DWG，真實大型圖面仍需實機驗收。

## v0.9.0 重點

- 可匯入既有預算 Excel，保留原始工作表並建立穩定的預算階層主檔。
- 一個 CAD 計量群組可對應多個正式預算明細，支援原始數量、長度、固定值、乘數、耗損率、每層與每區規則。
- 案件 mapping 保存於 DWG，只有使用者明確操作才會升級為公司規則。
- `預算草稿` 依正式階層輸出，未確認 mapping 不納入正式數量。
- 圖塊更新器先比較標準庫與專案圖塊，使用者勾選後才更新，並保留實例位置、同名屬性及 QTO XData。

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
- `tools/RunCoreValidation.ps1` 可驗證專案 DLL；加上 `-Installed` 可驗證實際安裝版，並會清理未自行退出的 Core Console 測試程序。
- 新增後續接口文件：`docs/QTO_FUTURE_INTERFACES_AND_UPGRADE_PLAN.md`。

## v0.9.0 不做的事

- 不做完整自動繪圖系統。
- 不做路徑自動尋路。
- 不把 mapping 結果視為正式契約數量或最終報價。
- 不做背景無提示圖塊替換。
- 不做專案模板。
- 不做 AI 或模糊推論。
- 不做無提示破壞性修復。

## Excel 自動同步

外掛啟動時不會自動建立、開啟或連結 Excel。只有使用者明確啟用同步後才開始工作。

1. 開啟 `案件設定`，明確選擇既有 Excel 或建立本案 Excel；也可稍後從 `同步主控` 選擇。
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

v0.9.0 延續既有 `QtoBlockCatalog` 格式：

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

一般使用者請先完整關閉 AutoCAD、解壓縮安裝包，再執行：

```text
install_or_update_QtoWirePlugin.bat
```

安裝完成後可執行 `check_installation.bat`，確認版本與 DLL 都和安裝包一致。第二台電腦的操作驗收請使用 [第二台電腦驗收表](docs/QTO_V1_0_SECOND_PC_CHECKLIST.md)。

手動部署時使用 bundle：

```text
QtoWirePlugin_v1.0.bundle
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

驗證內容包含 Release 建置、manifest 命令一致性、版本、bundle 字典、catalog JSON 讀寫、Excel、公司預算規則、崇德預算解析，以及合成 CAD 範圍與數量驗證。

既有範圍、繪圖員實機檢查項目與驗收邊界，請見 [v1.0 Beta 驗收紀錄](docs/QTO_V1_0_BETA_VALIDATION.md)。

目前各模組的實作、證據與待驗收邊界，請見 [v1.0 Beta 完成矩陣](docs/QTO_V1_0_COMPLETION_MATRIX.md)。繪圖員日常入口、進階管理視窗與背景自動流程，請見 [操作入口與背景流程](docs/QTO_V1_0_UI_FLOW_MAP.md)；新增功能的介面邊界與驗收規則請見 [使用流程與介面驗收](docs/QTO_V1_0_UI_FLOW_AUDIT.md)。

架構決策記錄於 [ADR-001](docs/decisions/ADR-001-project-catalog-and-review.md)。
