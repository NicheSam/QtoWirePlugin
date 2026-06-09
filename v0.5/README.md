# QtoWirePlugin v0.5

`v0.5` 是針對 AutoCAD 2023 開發的弱電配線 QTO 外掛版本。

## 目標環境

| 項目 | 需求 |
| --- | --- |
| 作業系統 | Windows x64 |
| CAD | AutoCAD 2023 |
| Runtime | .NET Framework 4.8 |
| 專案類型 | AutoCAD managed .NET plugin |
| 專案檔 | `QtoWirePlugin.csproj` |

## 核心概念

外掛會將 QTO 資訊寫入 AutoCAD 物件的 XData。這樣不只圖面上看得到標註，物件本身也會保存可查詢、可檢查、可匯出的結構化資料。

常用欄位：

| 欄位 | 意義 |
| --- | --- |
| `QTO_TYPE` | 物件角色，例如出線口、配線、結線箱或線槽 |
| `OUTLET_ID` | 出線口編號 |
| `JB_ID` | 結線箱編號 |
| `SYSTEM` | 系統別 |
| `CABLE_TYPE` | 線材類型 |
| `TRAY_ID` | 線槽編號 |
| `QTO_ERROR` | 錯誤標記 |
| `QTO_ERROR_MESSAGE` | 錯誤說明 |

## 建置需求

需要安裝 AutoCAD 2023，並確認以下檔案存在：

```text
C:\Program Files\Autodesk\AutoCAD 2023\AcMgd.dll
C:\Program Files\Autodesk\AutoCAD 2023\AcDbMgd.dll
C:\Program Files\Autodesk\AutoCAD 2023\AcCoreMgd.dll
C:\Program Files\Autodesk\AutoCAD 2023\AdWindows.dll
```

如果 AutoCAD 安裝路徑不同，請修改 `QtoWirePlugin.csproj` 內的 `HintPath`。

## 建置方式

使用 Visual Studio：

1. 開啟 `QtoWirePlugin.csproj`。
2. 選擇 `Release`。
3. 選擇 `x64`。
4. 執行 build。

使用 MSBuild：

```powershell
msbuild QtoWirePlugin.csproj /p:Configuration=Release /p:Platform=x64
```

## 安裝到 AutoCAD

1. 建置 `QtoWirePlugin.dll`。
2. 將 DLL 複製到：

```text
QtoWirePlugin_v0.5.bundle\Contents\Windows\QtoWirePlugin.dll
```

3. 將整個 bundle 資料夾複製到 AutoCAD ApplicationPlugins 資料夾，例如：

```text
%APPDATA%\Autodesk\ApplicationPlugins\
```

4. 重新啟動 AutoCAD。
5. 執行：

```text
QTO_HELLO
```

若外掛有回應，代表已成功載入。

## 命令表

### 啟動與介面

| 命令 | 用途 |
| --- | --- |
| `QTO_HELLO` | 確認外掛是否載入 |
| `QTO_SHOW_UI` | 建立或顯示 Ribbon UI |
| `QTO_PANEL` | 開啟操作面板 |
| `QTO_TEST_XDATA` | 測試 XData 寫入 |

### 出線口與結線箱

| 命令 | 用途 |
| --- | --- |
| `QTO_MARK_OUTLETS` | 將選取物件標記為出線口 |
| `QTO_MARK_JB` | 將選取物件標記為結線箱 |
| `QTO_NUMBER_OUTLETS` | 編號出線口 |
| `QTO_ASSIGN_JB` | 將出線口指派到結線箱 |
| `QTO_EDIT_OUTLET_PROPERTIES` | 編輯出線口資料 |
| `QTO_EDIT_JB_PROPERTIES` | 編輯結線箱資料 |
| `QTO_LABEL_OUTLETS` | 加入出線口標註 |
| `QTO_CALLOUT_TO_JB` | 繪製出線口到結線箱的指向標註 |
| `QTO_DELETE_OUTLET_INFO` | 移除出線口 QTO 資料 |
| `QTO_DELETE_JB_INFO` | 移除結線箱 QTO 資料 |

### 配線

| 命令 | 用途 |
| --- | --- |
| `QTO_BIND_WIRE` | 將 Polyline 綁定為配線 |
| `QTO_BATCH_BIND_WIRE` | 批次綁定配線 Polyline |
| `QTO_RECALC_WIRE` | 重新計算線長 |
| `QTO_CHECK_WIRE` | 檢查出線口、結線箱與配線資料一致性 |
| `QTO_QUERY_WIRE` | 查詢配線資料 |
| `QTO_UNBIND_WIRE` | 移除配線綁定 |
| `QTO_REASSIGN_WIRE` | 重新指派配線 |

### 線槽與路由

| 命令 | 用途 |
| --- | --- |
| `QTO_MARK_TRAY` | 標記線槽 Polyline |
| `QTO_SCAN_TRAY_NETWORK` | 掃描線槽網路連通性 |
| `QTO_ROUTE_BY_TRAY` | 依線槽網路尋找單一路徑 |
| `QTO_BATCH_ROUTE_BY_TRAY` | 批次依線槽網路尋路 |
| `QTO_CONDUIT_TO_TRAY` | 建立管段到線槽資訊 |
| `QTO_CLEAR_TRAY_PROPERTIES` | 清除線槽資料 |

### 匯出與錯誤檢查

| 命令 | 用途 |
| --- | --- |
| `QTO_EXPORT_OUTLETS` | 匯出出線口 CSV |
| `QTO_EXPORT_JB_SUMMARY` | 匯出結線箱摘要 |
| `QTO_EXPORT_CONDUIT_SUMMARY` | 匯出管段摘要 |
| `QTO_HIGHLIGHT_ERRORS` | 高亮顯示檢查錯誤 |
| `QTO_CLEAR_ERROR_HIGHLIGHT` | 清除錯誤高亮 |

## 建議使用流程

1. 開啟 CAD 圖面。
2. 如果 Ribbon 沒出現，執行 `QTO_SHOW_UI`。
3. 使用 `QTO_MARK_OUTLETS` 標記出線口。
4. 使用 `QTO_NUMBER_OUTLETS` 編號出線口。
5. 使用 `QTO_MARK_JB` 標記結線箱。
6. 使用 `QTO_ASSIGN_JB` 指派出線口到結線箱。
7. 使用 `QTO_BIND_WIRE` 或 `QTO_BATCH_BIND_WIRE` 綁定配線。
8. 使用 `QTO_RECALC_WIRE` 重新計算線長。
9. 使用 `QTO_CHECK_WIRE` 檢查資料品質。
10. 使用 `QTO_HIGHLIGHT_ERRORS` 找出錯誤物件。
11. 修正圖面後重新檢查。
12. 匯出 CSV 報告。

## 注意事項與限制

- 對正式 DWG 使用前，請先備份圖面。
- 外掛會修改圖面物件 XData。
- 此版本以 AutoCAD 2023 為目標；其他版本可能需要調整專案引用與 bundle 設定。
- 編譯後 DLL、PDB、`bin/`、`obj/` 與發布包不屬於原始碼版本。

