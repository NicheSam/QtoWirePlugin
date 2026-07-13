# QTO 後續功能接口與升級計劃

## 目的

本文件保留 v0.8 之後的功能接口與模組邊界，讓後續 agent 可以接續開發，但不把所有功能一次實作到 v0.8。

v0.8 的唯一主線是：標準圖塊資料庫與插入器 MVP。其他功能只保留接口、欄位與文件，不在 v0.8 展開。

v0.8.1 已補上圖塊庫管理介面、DWG 專案 catalog context、Catalog Review 接線與 release 驗證工具。後續版本必須沿用這些接口，不得重新建立平行 catalog 路徑或 Review snapshot。

## 模組邊界

### 1. 圖塊資料庫模組

目標：
- 管理標準圖塊清單。
- 記錄圖塊名稱、系統代碼、設備類型、CAD 計量型態、單位、預設圖層、預算候選鍵。
- 提供圖塊插入器、Review、圖塊更新器共用的 catalog。

v0.8 做：
- 掃描圖例 DWG 產生或更新 catalog。
- 由 catalog 選擇圖塊並插入模型空間。
- 插入時寫入 QTO XData。

v0.8 不做：
- 自動推論圖塊分類。
- 自動更新既有專案圖塊定義。
- 正式預算 mapping。

### 2. 圖塊插入器模組

目標：
- 使用者從標準圖塊資料庫選擇設備後插入。
- 插入後即帶有 QTO 基本資料，減少事後補標。

v0.8 做：
- 選 catalog。
- 選圖塊。
- 指定插入點。
- 寫入 QTO_TYPE、SYSTEM_CODE、EQUIPMENT_TYPE_CODE、QUANTITY_BASIS、QTO_UNIT、QTO_SYNC_ID、QTO_SOURCE_CATALOG_ID、QTO_CATALOG_VERSION。

v0.8 不做：
- 連續配置工具。
- 路徑自動繪製。
- 依空間自動布點。

### 3. QTO 屬性與同步資料模組

目標：
- 所有圖面 QTO 物件都用同一套 XData 欄位。
- 圖塊、線段、管段、線槽、箱體都能被 Excel 與 Review 讀取。

接口保留：
- `QTO_SOURCE_CATALOG_ID`
- `QTO_CATALOG_VERSION`
- `BUDGET_ITEM_KEY`
- `QTO_SYNC_ID`

v0.8 不做 Excel 回寫 CAD。

### 4. Review 與檢查模組

目標：
- 檢查缺系統、缺設備類型、缺計量型態、缺樓層、缺預算候選。
- 未來可以接 catalog snapshot 判斷圖塊是否過期。

保留接口：
- `QtoCatalogSnapshot BuildReviewSnapshot(QtoBlockCatalog catalog);`
- `QtoBlockUpdatePlan BuildUpdatePlan(Database db, QtoBlockCatalog catalog);`

v0.8 不做正式圖塊更新器，只保留資料來源。

### 5. Excel / 預算模組

目標：
- CAD 是數量來源。
- Excel 是預算整理工作簿。
- 使用者在 Excel 補單價、備註與預算項目，CAD 更新時保留人工欄位。

保留接口：
- `QtoBudgetItem ResolveBudgetItem(QtoSyncRow row);`
- `BUDGET_ITEM_KEY` 先作候選欄位，不當正式報價品項。

v0.8 不做正式預算 mapping 擴充。

### 6. Ribbon 與使用者流程模組

目標：
- 常用功能留在 Ribbon。
- 複雜操作進視窗。
- Ribbon 不堆滿所有後續功能。

v0.8 新增：
- 圖塊庫：重建圖塊庫、插入圖塊。

### 7. 天花標註模組

目標：
- 保持隔離。
- 不讓 MEP 天花標註影響 QTO 圖塊資料庫、同步 Excel 與 Review。

v0.8 不新增天花標註功能。

## 版本路線

### v0.8：標準圖塊資料庫與插入器 MVP

交付：
- 建立 v0.8 獨立資料夾。
- 保留 v0.7 不修改。
- 支援掃描圖例 DWG 產生 catalog。
- 支援從 catalog 插入標準圖塊並寫入 QTO XData。

### v0.9：Catalog 接入 Review

目標：
- Review 可以顯示圖塊來源、catalog 狀態、缺分類、缺預算候選。
- 不自動修正工程判斷項。

### v1.0：繪圖命令 MVP

目標：
- 以設備類型驅動基本出線口、箱體、線段配置。
- 不做完整自動配線。

### v1.1：Excel / 預算草稿升級

目標：
- `BUDGET_ITEM_KEY` 接入預算草稿。
- 待確認列與正式明細列分離。

### v1.2：圖塊更新器

目標：
- 比對專案圖塊與標準圖塊資料庫。
- 提供更新計畫與人工確認。
- 不做無提示大量覆蓋。

### v1.3：專案模板

目標：
- 支援不同案件、業主、公司標準的圖塊資料庫與預算候選。

### v1.4 之後：半自動推論

目標：
- 根據圖層、圖塊名稱、文字標註建立候選分類。
- 所有推論都必須進 Review，不直接寫成 confirmed。

## 禁止事項

- 不在 v0.8 實作完整繪圖系統。
- 不在 v0.8 實作路徑自動尋路。
- 不在 v0.8 實作正式預算 mapping。
- 不在 v0.8 實作圖塊批量更新器。
- 不在 v0.8 實作專案模板。
- 不在 v0.8 實作 AI 或模糊推論。
- 不做無提示破壞性修復。

## 後續 Agent 開發原則

- 先確認目前版本資料夾，不要回到舊版資料夾修改。
- 新功能優先接既有 catalog、XData、Review、Excel 模組，不新增平行欄位。
- 若功能需要工程判斷，輸出候選與 Review，不直接覆蓋成正式結果。
- Ribbon 只放高頻入口，複雜流程用獨立視窗。
