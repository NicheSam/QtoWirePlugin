# QtoWirePlugin v0.6

QtoWirePlugin v0.6 是 AutoCAD 2023 使用的弱電 QTO 外掛版本。這一版的重點是讓使用者在 CAD 內更直覺地查看、修改、批量整理與匯出 QTO 資訊。

## 使用對象

- 弱電設計人員
- CAD 繪圖人員
- 需要整理弱電數量的估算人員
- 需要把 CAD 產出轉成預算檢查資料的團隊

## 目標環境

- AutoCAD 2023
- Windows x64
- .NET Framework 4.8
- C# Class Library 外掛

## v0.6 主要功能

### QTO 屬性面板

命令：

```text
QTO_PROPERTY_PANEL
```

屬性面板可以停駐在 AutoCAD 內，讓使用者選取圖塊或線段後查看與修改 QTO 資訊。

主要欄位包含：

- 設備類型
- CAD 計量型態
- 系統代碼
- QTO 編號
- 出線口編號
- 箱體編號
- 線材類型
- 樓層
- 區域
- 空間
- 數量依據
- 單位
- 對應狀態
- 待確認原因

### 多選批量修改

多選圖塊或線段後，可批量設定：

- 設備類型
- CAD 計量型態
- 系統代碼
- 線材類型
- 樓層
- 區域
- 空間

按下 `儲存` 時，只會寫入有填值的欄位；空白欄位不會覆蓋原本資料。

### 選取與檢查工具

| 命令 | 說明 |
| --- | --- |
| `QTO_SELECT_UNTAGGED` | 選取模型空間中尚未標註 QTO 主要資訊的圖塊與線段。 |
| `QTO_SELECT_SAME_QTO` | 跳出視窗，讓使用者依設備類型、CAD 計量型態、系統代碼、線材類型選取同類物件。 |

選取結果使用 AutoCAD 原生選取反白，屬於暫時檢視，按 Esc 可解除。

### 預算前置 CSV 匯出

命令：

```text
QTO_EXPORT_BUDGET_INPUT
```

匯出前會先讓使用者勾選要輸出的資訊群組：

- 物件識別
- CAD 計量
- 系統與設備
- 位置與配線
- 對應檢查

匯出的 CSV 是給工作台、估算檢查或後續整理使用，不是正式契約數量表。

## CAD 計量型態

| 顯示名稱 | 代碼 | 用途 |
| --- | --- | --- |
| 出線口 | `OUTLET` | 攝影機、資訊插座、讀卡機、喇叭等點位或端點。 |
| 箱體/接線箱 | `JUNCTION_BOX` | 接線箱、弱電箱、中繼箱、端接箱。 |
| 配線 | `WIRE` | 線材或配線路徑。 |
| 管段 | `CONDUIT_SEGMENT` | 配管或管段。 |
| 線槽 | `TRAY` | 線槽、橋架或幹線路徑。 |
| 設備 | `DEVICE` | 一般設備本體。 |
| 盤箱/設備盤 | `PANEL` | 控制盤、設備盤、主機盤或盤體類設備。 |

## 建置方式

使用 Visual Studio 開啟：

```text
QtoWirePlugin.csproj
```

建議組態：

```text
Release | x64
```

AutoCAD 2023 managed API 預設位於：

```text
C:\Program Files\Autodesk\AutoCAD 2023\
```

需要參考：

- `AcMgd.dll`
- `AcDbMgd.dll`
- `AcCoreMgd.dll`
- `AdWindows.dll`

## 使用者安裝方式

一般使用者請下載 GitHub Release 的安裝包：

```text
QtoWirePlugin_v0.6_installer.zip
```

解壓縮後，先關閉 AutoCAD，再執行：

```text
安裝或更新_QtoWirePlugin.bat
```

## 限制

- 選取工具目前只掃描模型空間。
- 選取工具目前只處理圖塊與線段類物件。
- 同類選取採精確比對，不做模糊比對。
- CSV 匯出是預算檢查前置資料，不是正式報價表。
- CAD 計量型態不等於預算品項；設備類型才是往預算書映射的主要欄位。

## English Summary

QtoWirePlugin v0.6 provides an AutoCAD QTO property palette, batch editing, selection tools, and budget-input CSV export for weak-current CAD quantity takeoff workflows.
