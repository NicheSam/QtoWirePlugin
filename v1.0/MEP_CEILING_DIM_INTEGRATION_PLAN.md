# MEP 天花自動標註整合開發規劃

版本：v0.1  
目標外掛：QtoWirePlugin v0.5  
規劃方向：同一個 AutoCAD 外掛內新增一套 MEP 天花標註功能，與既有 QTO 弱電計算功能共用安裝與 Ribbon 頁籤，但程式邏輯、命令、圖層、XData 與規則資料分開管理。

---

## 1. 開發定位

MEP 天花自動標註不是取代目前 QTO 功能，而是在同一個外掛中新增另一套工作流程。

既有 QTO 功能負責：

- 出線口與箱體標記
- 配線、管段與線槽相關處理
- 長度統計與報表輸出

新增 MEP 天花標註功能負責：

- 依使用者指定基準線產生設備定位尺寸
- 依設備圖塊規則產生標籤
- 產生開孔、維修孔、待確認註記
- 標示未知圖塊供人工覆核

核心原則：

- 同一個 DLL
- 同一個 BAT installer
- 同一個 `QTO 弱電計算` Ribbon 頁籤
- Ribbon 頁籤內新增一個大分類：`天花標註`
- MEP 功能與 QTO 功能保持資料隔離

---

## 2. UI 配置規劃

目前 Ribbon 頁籤維持：

```text
QTO 弱電計算
```

頁籤內面板規劃調整為：

```text
流程
出線口
箱體
線槽
報表
天花標註
```

新增 `天花標註` 面板按鈕：

| 按鈕名稱 | 命令 | 用途 |
|---|---|---|
| 初始化 | `MEPDIMINIT` | 建立 MEP 天花標註需要的圖層、文字樣式與尺寸樣式 |
| 自動標註 | `MEPCEILDIM` | 選取 X/Y 基準線與設備圖塊後，自動產生定位尺寸與標籤 |
| 標註規則 | `MEPDIMRULE` | 載入、檢查或重載標註規則 |

第一版可以先只放 `初始化` 與 `自動標註`。`標註規則` 若仍使用內建規則表，可先保留命令但不放主要按鈕，等外部 JSON / CSV 規則穩定後再放上 Ribbon。

---

## 3. 命令命名

QTO 既有命令維持 `QTO_` 前綴。

MEP 天花標註新增命令使用 `MEP` 前綴：

| 命令 | 階段 | 說明 |
|---|---|---|
| `MEPDIMINIT` | Phase 1 | 初始化圖層、文字樣式、尺寸樣式 |
| `MEPCEILDIM` | Phase 1 | 主標註命令 |
| `MEPDIMRULE` | Phase 2 | 規則檢查、重載、管理入口 |

避免使用 `QTO_` 前綴，讓使用者與程式碼都能明確區分兩套邏輯。

---

## 4. 資料隔離規劃

### 4.1 XData

既有 QTO 使用：

```text
QTO_APP
QTO_TYPE
OUTLET_ID
JB_ID
LENGTH_M
```

MEP 天花標註新增：

```text
MEP_DIM_APP
MEP_TYPE
MEP_BLOCK_NAME
MEP_RULE_ID
MEP_CATEGORY
MEP_REVIEW_REQUIRED
MEP_SOURCE_OBJECT_ID
```

第一版可只在新增的尺寸、文字、註記上寫入 XData，不修改原始設備圖塊。這樣比較安全，也符合「不更動原始圖面物件」原則。

### 4.2 圖層

新增 MEP 專用圖層：

| 圖層名稱 | 用途 |
|---|---|
| `MEP-DIM-X` | X 向定位尺寸 |
| `MEP-DIM-Y` | Y 向定位尺寸 |
| `MEP-TAG` | 設備標籤 |
| `MEP-OPENING-NOTE` | 開孔尺寸與安裝註記 |
| `MEP-UNKNOWN` | 未分類圖塊標記 |
| `MEP-HOLD-AREA` | 暫緩封板區 |
| `MEP-ACCESS-PANEL` | 維修孔標註 |
| `MEP-REFERENCE` | 使用者指定基準輔助線 |

MEP 功能不得使用 `QTO_WIRE`、`QTO_CONDUIT`、`QTO_OUTLET_LABEL` 等既有 QTO 圖層。

---

## 5. 程式結構規劃

同一個 `QtoWirePlugin.csproj` 內新增 MEP 相關檔案，但不把邏輯寫進既有 QTO command 大檔中。

建議新增：

```text
MepCeilingDimCommands.cs
MepCeilingDimModels.cs
MepDimensionEngine.cs
MepAnnotationEngine.cs
MepGeometryService.cs
MepLayerHelper.cs
MepRuleLoader.cs
MepXDataHelper.cs
Rules\default_mep_dim_rules.json
```

責任分工：

| 檔案 / 類別 | 責任 |
|---|---|
| `MepCeilingDimCommands` | AutoCAD 命令入口，處理使用者選取流程 |
| `MepGeometryService` | 基準線解析、垂足、距離、定位點計算 |
| `MepDimensionEngine` | 建立 AutoCAD dimension entity |
| `MepAnnotationEngine` | 建立標籤、開孔註記、未知圖塊標示 |
| `MepLayerHelper` | 建立 MEP 圖層與樣式 |
| `MepRuleLoader` | 讀取內建或外部規則 |
| `MepXDataHelper` | 寫入 MEP 專用 XData |

---

## 6. Phase 1 MVP

目標：先做出可測試的半自動標註流程。

### 6.1 功能範圍

必做：

- `MEPDIMINIT`
- `MEPCEILDIM`
- 支援選取一條 X 向基準線
- 支援選取一條 Y 向基準線
- 支援框選 `BlockReference`
- 讀取有效圖塊名稱
- 取得定位點，先使用 insertion point
- 依內建規則產生設備標籤
- 未知圖塊產生 `UNKNOWN` 標記
- 建立 X/Y 方向定位尺寸
- 所有新增物件放在 MEP 專用圖層
- 不修改原始圖塊、不炸開圖塊、不修改 XREF

暫不做：

- 自動找最近牆
- 自動辨識房間邊界
- 完整避讓與排版
- 自動連續尺寸
- 消防或空調法規判斷
- 外部規則編輯 UI

### 6.2 操作流程

```text
Command: MEPCEILDIM
選取 X 向定位基準線：
選取 Y 向定位基準線：
框選要標註的設備圖塊：
指定尺寸線偏移點或使用預設：
產生標註？[Yes/No]
```

### 6.3 第一版規則表

先以 C# 內建資料表開始，避免第一階段被 JSON 檔路徑、部署、編碼問題拖慢。

| 圖塊名稱 | 類型 | 標籤 | 定位點 | 開孔註記 |
|---|---|---|---|---|
| `PANEL_LIGHT_600x600` | 平板燈 | `L1 方形平板燈` | bounding box center | `開孔依型錄確認` |
| `SMOKE_DETECTOR` | 偵煙 | `SD 偵煙` | insertion point | 無 |
| `SPRINKLER` | 灑水頭 | `SP 灑水頭` | insertion point | 無 |
| `ACCESS_PANEL_600x600` | 維修孔 | `AP 600x600` | bounding box center | `開孔 600x600` |
| `ELV_OUTLET` | 弱電出線口 | `弱電出線口` | insertion point | 無 |
| `EXHAUST_GRILLE` | 排煙口 | `排煙口` | bounding box center | `開孔依空調確認` |
| `AIR_DIFFUSER` | 空調風口 | `空調風口` | bounding box center | `開孔依空調確認` |

若圖塊名稱不在表內：

- 建立 `UNKNOWN` 文字
- 放在 `MEP-UNKNOWN`
- 命令結束時列出未知圖塊名稱與數量

---

## 7. Phase 2 增強

目標：讓功能開始符合實際專案使用。

新增：

- 支援 bounding box center 定位
- 支援外部 JSON 或 CSV 規則表
- `MEPDIMRULE` 可檢查規則檔是否有效
- 支援開孔註記
- 支援人工覆核標記
- 支援 Undo group
- 命令結束輸出錯誤與未知圖塊摘要
- 尺寸偏移距離可設定
- 支援依比例調整文字高度與尺寸偏移

---

## 8. Phase 3 正式化

目標：從可用原型整理成穩定功能。

新增：

- Ribbon 按鈕完整化
- 規則管理視窗
- 標註前預覽或摘要確認
- 自動連續尺寸
- 尺寸線分層錯開
- 未知圖塊報表匯出
- 設備標註清單匯出
- 與公司圖層、文字樣式、尺寸樣式整合
- 支援區域批次處理

---

## 9. 幾何策略

第一版只支援可靠情境。

基準線支援：

- `Line`
- `Polyline` 的單一線段或可取最近線段

第一版限制：

- 基準線需接近水平或垂直
- 若基準線角度不合理，提示使用者重新選取
- 不支援曲線、圓弧、不規則牆線自動解析

定位點來源：

- 第一優先：規則指定
- 若規則指定 bounding box center 但取得失敗，改用 insertion point 並列入覆核
- 若圖塊沒有有效定位點，跳過該圖塊並列入錯誤摘要

---

## 10. 驗收條件

Phase 1 驗收：

- 開啟 AutoCAD 後，同一個 `QTO 弱電計算` 頁籤可看到 `天花標註` 面板
- `MEPDIMINIT` 可建立所有 MEP 圖層
- `MEPCEILDIM` 可完成一次完整流程
- 框選多個設備圖塊時，不因單一未知圖塊中止
- 已知圖塊產生 X/Y 定位尺寸與標籤
- 未知圖塊產生 `UNKNOWN` 標記
- 所有新增物件在 MEP 專用圖層
- 原始設備圖塊與 XREF 不被修改
- AutoCAD 不因錯誤選取或未知圖塊崩潰

---

## 11. 需要補充的資料

正式開發前或 Phase 1 測試後，需要整理：

- 實際天花整合 DWG 測試檔
- 常用設備圖塊名稱
- 圖塊插入點是否在中心
- 公司尺寸樣式名稱
- 公司文字樣式名稱
- 出圖比例與常用文字高度
- 平板燈、風口、維修孔常用尺寸
- 哪些設備需要開孔註記
- 哪些設備只需中心定位
- 哪些設備必須標示待人工確認

---

## 12. 開發順序建議

建議依下列順序實作：

1. 新增 MEP 專用 helper 與 model 檔案
2. 實作 `MEPDIMINIT`
3. 在 Ribbon 內新增 `天花標註` 面板
4. 實作基準線選取與驗證
5. 實作圖塊框選與分類
6. 實作定位點計算
7. 實作 X/Y 尺寸生成
8. 實作設備標籤與 UNKNOWN 標記
9. 加入錯誤摘要
10. 打包 installer 測試

---

## 13. 風險與注意事項

主要風險：

- 圖塊命名不一致會導致大量 UNKNOWN
- 圖塊插入點不在設備中心會導致尺寸錯誤
- 基準線選錯方向會產生錯誤尺寸
- 尺寸線重疊會需要人工調整
- 不同專案比例不同，文字高度與偏移距離需要可調

處理策略：

- 第一版保留人工確認流程
- 不做全自動牆線判斷
- 只處理使用者明確選取的範圍
- 對未知與不可靠結果明確標示
- 不修改原始圖塊與 XREF

