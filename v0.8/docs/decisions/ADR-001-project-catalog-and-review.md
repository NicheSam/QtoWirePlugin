# ADR-001：由 DWG 保存專案圖塊資料庫並供 Review 共用

## 狀態

Accepted

## 日期

2026-07-10

## 背景

v0.8 已能掃描圖例 DWG、建立 catalog 並插入帶 QTO XData 的圖塊，但插入器、Review 與使用者選擇的 catalog 沒有共用專案狀態。Review 因此無法判斷目前應使用哪一份資料庫，並會對每個圖塊重複顯示「尚未載入圖塊資料庫」。

Catalog 也只能靠文字編輯器修改，不適合一般設計與估算人員使用。

## 決策

- 每個 DWG 在 Named Object Dictionary 的 `QTO_PROJECT_SETTINGS` 保存 `CATALOG_PATH`。
- Catalog 位於 DWG 目錄內時保存相對路徑，位於外部時保存絕對路徑。
- 插入器、圖塊庫管理、Review 與 Excel 更新都透過 `QtoProjectCatalogContext` 解析同一份 catalog。
- `QtoCatalogSnapshotBuilder` 是 catalog 模型進入 Review 的唯一轉換接口。
- Catalog 未載入時只產生一筆全域 Review；載入後才逐物件檢查缺失、停用、更新與衝突。
- 圖塊狀態控制插入：`active` 可用、`unconfigured` 需確認，其餘狀態禁止直接插入。

## 替代方案

### 每次命令要求使用者重新選 catalog

操作重複且容易讓插入器、Review 與 Excel 使用不同檔案，因此不採用。

### 將 catalog 路徑存成電腦全域設定

不同案件可能使用不同公司或業主標準，全域設定無法代表專案，因此不採用。

### 將完整 catalog 嵌入 DWG

可攜性較高，但會形成多份難以同步的主資料，且不利多人共用標準圖塊庫，因此不採用。

## 影響

- 使用者第一次選擇或建立 catalog 後，後續流程不需重複指定。
- DWG 移動時，同目錄內的相對 catalog 可一起移動。
- 外部絕對路徑失效時，Review 會顯示一筆重新設定提醒，不自動改用其他資料庫。
- 後續圖塊更新器與預算候選 mapping 必須沿用此 context 與 snapshot，不建立平行設定欄位。
