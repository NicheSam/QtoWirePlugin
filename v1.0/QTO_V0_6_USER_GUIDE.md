# QtoWirePlugin v0.6 使用說明

## 這版新增什麼

v0.6 新增兩個核心能力：

1. `QTO_PROPERTY_PANEL`
   - 開啟 QTO 屬性面板。
   - 可挑選 CAD 圖塊或線條。
   - 可查看與編輯 QTO 屬性。
   - 可看到該物件是否缺必要欄位。

2. `QTO_EXPORT_BUDGET_INPUT`
   - 匯出 `QTO_BUDGET_INPUT_*.csv`。
   - 供工作台讀取，用來產生預算草稿與缺項清單。

## 建議操作流程

1. 在 AutoCAD 載入新版外掛。
2. 執行 `QTO_PANEL`。
3. 點 `v0.6 Panel`，或直接輸入 `QTO_PROPERTY_PANEL`。
4. 在面板按 `Pick object`。
5. 選取一個出線口、箱體、線條、管段或線槽。
6. 檢查並補齊：
   - `QTO_TYPE`
   - `SYSTEM_CODE`
   - `EQUIPMENT_TYPE_CODE`
   - `CABLE_TYPE`
   - `JB_ID`
   - `QUANTITY_BASIS`
   - `QTO_UNIT`
7. 按 `Save XData`。
8. 對其他物件重複檢查。
9. 按 `Budget CSV`，或執行 `QTO_EXPORT_BUDGET_INPUT`。
10. 把匯出的 CSV 匯入工作台。

## 欄位怎麼填

### 出線口

例如攝影機、資訊插座、門禁讀卡機、廣播喇叭：

| 欄位 | 建議 |
|---|---|
| `QTO_TYPE` | `OUTLET` |
| `SYSTEM_CODE` | `CCTV`、`LAN`、`ACS`、`PA` |
| `EQUIPMENT_TYPE_CODE` | `CCTV_CAMERA`、`ACS_CARD_READER`、`PA_SPEAKER` |
| `OUTLET_ID` | 例如 `O-001` |
| `JB_ID` | 例如 `JB-01` |
| `CABLE_TYPE` | 例如 `CAT6` |
| `QUANTITY_BASIS` | `point_count` |
| `QTO_UNIT` | `point` |

### 配線

| 欄位 | 建議 |
|---|---|
| `QTO_TYPE` | `WIRE` |
| `SYSTEM_CODE` | `LAN`、`CCTV`、`ACS` |
| `EQUIPMENT_TYPE_CODE` | `ELV_CABLE` |
| `CABLE_TYPE` | `CAT6`、`Fiber`、`Control` |
| `QUANTITY_BASIS` | `wire_length_m` |
| `QTO_UNIT` | `m` |

### 管段

| 欄位 | 建議 |
|---|---|
| `QTO_TYPE` | `CONDUIT_SEGMENT` |
| `SYSTEM_CODE` | `ELV_WIRE` |
| `EQUIPMENT_TYPE_CODE` | `ELV_CONDUIT` |
| `QUANTITY_BASIS` | `conduit_length_m` |
| `QTO_UNIT` | `m` |

### 箱體或設備

| 欄位 | 建議 |
|---|---|
| `QTO_TYPE` | `JUNCTION_BOX`、`DEVICE`、`PANEL` |
| `SYSTEM_CODE` | 依系統填 |
| `EQUIPMENT_TYPE_CODE` | 依設備填 |
| `QUANTITY_BASIS` | `device_count` |
| `QTO_UNIT` | `set` |

## 面板上的 Diagnostics 怎麼看

如果顯示：

```text
review: OK
```

代表目前屬性大致完整。

如果顯示：

```text
missing_equipment_type_code
missing_jb_id
missing_cable_type
```

代表該物件仍需補資料，工作台會把它列入待確認或缺項。

## 字典來源

外掛會依序讀取：

- 環境變數 `QTO_DICTIONARY_DIR` 指定的資料夾。
- DWG 同資料夾下的 `m2_m4_shared_dictionary`。
- 安裝 bundle 內附的 `m2_m4_shared_dictionary`。

需要覆蓋預設字典時，可設定環境變數：

```text
QTO_DICTIONARY_DIR
```

若字典讀不到，面板仍可使用，但下拉選單會不完整。

## 匯出給工作台

執行：

```text
QTO_EXPORT_BUDGET_INPUT
```

會產出：

```text
QTO_BUDGET_INPUT_yyyyMMdd_HHmmss.csv
```

這份 CSV 的用途是讓工作台判斷：

- 哪些 CAD 物件可以進預算草稿
- 哪些圖面有但預算未列
- 哪些物件缺屬性，需要人工確認

## 目前限制

- 這版不是正式報價工具。
- 這版不會修改預算書 Excel。
- 這版不會自動判斷責任歸屬。
- 狀態標籤與上色檢查尚未做成完整 CAD 視覺模式。
- `QTO_PROPERTY_PANEL` 是可用面板，但還不是 AutoCAD 停駐 Palette。
