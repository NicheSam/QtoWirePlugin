# QtoWirePlugin

QtoWirePlugin 是一個 AutoCAD .NET 外掛，用於弱電與機電配線的 CAD 數量整理流程。

它可以讓使用者在 AutoCAD 圖面中標記出線口、結線箱、配線、線槽與管段相關物件，將數量資訊寫入圖面物件的 XData，並輸出 CSV 報告供後續估算、檢核或點交流程使用。

## 解決的問題

傳統 CAD 配線數量整理常依賴人工標註與試算表，容易出現以下問題：

- 出線口與結線箱關係不清楚。
- 配線與端點沒有結構化資料。
- 系統別與線材類型分散在文字標註中。
- 線槽路徑與配線長度難以追蹤。
- 圖面物件與匯出數量列缺乏穩定連結。

QtoWirePlugin 透過 AutoCAD XData 保存這些資訊，並提供標記、檢查、路由與匯出命令。

## 目前版本

| 版本 | 資料夾 | 目標環境 |
| --- | --- | --- |
| v0.5 | `v0.5/` | AutoCAD 2023、Win64、.NET Framework 4.8 |

## 主要功能

- 標記出線口與結線箱。
- 將出線口指派到結線箱。
- 綁定配線 Polyline 與 QTO 資料。
- 重新計算配線長度。
- 標記線槽並掃描線槽網路。
- 依線槽網路估算路徑。
- 匯出出線口、結線箱、管段與配線檢查 CSV。
- 高亮顯示錯誤物件並清除錯誤標示。

## 專案結構

```text
QtoWirePlugin/
  v0.5/
    QtoWirePlugin.csproj
    Commands.cs
    QtoRibbon.cs
    QtoScanner.cs
    QtoCsvExporter.cs
    QtoWirePlugin_v0.5.bundle/
```

## 開始使用

請閱讀 [v0.5/README.md](v0.5/README.md)，裡面包含建置需求、AutoCAD 安裝方式、命令表與使用流程。

## 狀態

這是外掛原始碼專案，不包含已編譯 DLL 或正式發布包。使用前需在本機建置並安裝到 AutoCAD。

