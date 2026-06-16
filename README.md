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

## 下載與安裝

一般使用者請不要使用 GitHub 的 `Code > Download ZIP`，那是原始碼 ZIP，不包含完整安裝檔。

請從 GitHub Releases 下載完整安裝包：

- [QtoWirePlugin v0.5 Release](https://github.com/NicheSam/QtoWirePlugin/releases/tag/v0.5)
- [QtoWirePlugin_v0.5_installer.zip](https://github.com/NicheSam/QtoWirePlugin/releases/download/v0.5/QtoWirePlugin_v0.5_installer.zip)

下載後解壓縮，執行資料夾內的 `安裝或更新_QtoWirePlugin.bat`。安裝前請先完全關閉 AutoCAD。

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

這個 repo 保留外掛原始碼。正式給使用者安裝的 ZIP 會放在 GitHub Releases，不建議從原始碼 ZIP 安裝。
