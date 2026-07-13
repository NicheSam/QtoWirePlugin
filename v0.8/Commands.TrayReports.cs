using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;

namespace QtoWirePlugin
{
    public partial class Commands
    {
        [CommandMethod("QTO_EXPORT_OUTLETS")]
        public void QtoExportOutlets()
        {
            try
            {
                Document document = Application.DocumentManager.MdiActiveDocument;

                if (document == null)
                {
                    return;
                }

                Editor editor = document.Editor;
                Database database = document.Database;
                ExportScopeSelection exportScope = PromptExportScope(editor, "出線口清單");

                if (exportScope == null)
                {
                    editor.WriteMessage("\n已取消 CSV 匯出。");
                    return;
                }

                List<OutletInfo> outlets = null;

                using (Transaction transaction = database.TransactionManager.StartTransaction())
                {
                    outlets = QtoScanner.GetAllQtoOutlets(transaction, database);
                    outlets = FilterOutletsByScope(outlets, exportScope);
                    transaction.Commit();
                }

                string outputPath = PromptCsvOutputPath(database, "QTO_OUTLETS_");

                if (string.IsNullOrWhiteSpace(outputPath))
                {
                    editor.WriteMessage("\n\u5df2\u53d6\u6d88 CSV \u532f\u51fa\u3002");
                    return;
                }

                outputPath = QtoCsvExporter.ExportOutlets(outlets, database, outputPath);

                editor.WriteMessage("\n\u51fa\u7dda\u53e3\u6e05\u55ae\u5df2\u532f\u51fa\uff1a" + outputPath);
                editor.WriteMessage("\n\u532f\u51fa\u7b46\u6578\uff1a" + outlets.Count);
                ShowStepMessage(
                    "\u51fa\u7dda\u53e3\u6e05\u55ae\u532f\u51fa\u5b8c\u6210",
                    "\u5df2\u532f\u51fa " + outlets.Count + " \u7b46\u51fa\u7dda\u53e3\u8cc7\u6599\u3002\nCSV\uff1a" + outputPath,
                    "\u4e0b\u4e00\u6b65\u5efa\u8b70\uff1a\u958b\u555f CSV \u6aa2\u67e5 OUTLET_ID \u8207 JB_ID \u662f\u5426\u5b8c\u6574\u3002");
            }
            catch (System.Exception ex)
            {
                WriteCommandError("QTO_EXPORT_OUTLETS", ex);
            }
        }

        [CommandMethod("QTO_MARK_TRAY")]
        public void QtoMarkTray()
        {
            try
            {
                Document document = Application.DocumentManager.MdiActiveDocument;

                if (document == null)
                {
                    return;
                }

                Editor editor = document.Editor;
                Database database = document.Database;
                PromptSelectionOptions selectionOptions = new PromptSelectionOptions();
                selectionOptions.MessageForAdding = "\n請框選要指定為線槽的 Polyline：";
                PromptSelectionResult selectionResult = editor.GetSelection(selectionOptions);

                if (selectionResult.Status != PromptStatus.OK)
                {
                    editor.WriteMessage("\n已取消指定線槽。");
                    return;
                }

                string systemScope = PromptStringWithDefault(editor, "\n線槽適用系統", "ALL");
                int markedCount = 0;
                int skippedCount = 0;

                using (Transaction transaction = database.TransactionManager.StartTransaction())
                {
                    int nextTrayNumber = GetNextTrayNumber(transaction, database);

                    foreach (SelectedObject selectedObject in selectionResult.Value)
                    {
                        if (selectedObject == null)
                        {
                            skippedCount++;
                            continue;
                        }

                        Polyline polyline = transaction.GetObject(selectedObject.ObjectId, OpenMode.ForWrite, false) as Polyline;

                        if (polyline == null)
                        {
                            skippedCount++;
                            continue;
                        }

                        Dictionary<string, string> data = QtoXDataHelper.GetXData(polyline);
                        data[QtoXDataHelper.KeyQtoType] = QtoXDataHelper.TypeTray;

                        if (string.IsNullOrWhiteSpace(GetDictionaryValue(data, QtoXDataHelper.KeyTrayId)))
                        {
                            data[QtoXDataHelper.KeyTrayId] = "TRAY-" + nextTrayNumber.ToString("000");
                            nextTrayNumber++;
                        }

                        data[QtoXDataHelper.KeySystemScope] = systemScope;
                        QtoXDataHelper.SetXData(polyline, database, transaction, data);
                        markedCount++;
                    }

                    transaction.Commit();
                }

                editor.WriteMessage("\n成功指定線槽數量：" + markedCount);
                editor.WriteMessage("\n忽略非 Polyline 數量：" + skippedCount);
                ShowStepMessage(
                    "線槽指定完成",
                    "已將 " + markedCount + " 條 Polyline 指定為線槽。\n忽略 " + skippedCount + " 個非 Polyline 物件。",
                    "下一步建議：執行「掃描線槽」檢查線槽端點是否有連接。");
            }
            catch (System.Exception ex)
            {
                WriteCommandError("QTO_MARK_TRAY", ex);
            }
        }

        [CommandMethod("QTO_SCAN_TRAY_NETWORK")]
        public void QtoScanTrayNetwork()
        {
            try
            {
                Document document = Application.DocumentManager.MdiActiveDocument;

                if (document == null)
                {
                    return;
                }

                Editor editor = document.Editor;
                Database database = document.Database;
                double tolerance = PromptDoubleWithDefault(editor, "\n線槽端點連接容許距離 mm", 100.0);
                TrayNetworkScanResult scanResult = new TrayNetworkScanResult();

                using (Transaction transaction = database.TransactionManager.StartTransaction())
                {
                    QtoTrayNetwork.Build(transaction, database, tolerance, scanResult);
                    transaction.Commit();
                }

                string outputPath = PromptCsvOutputPath(database, "QTO_TRAY_NETWORK_");

                if (!string.IsNullOrWhiteSpace(outputPath))
                {
                    QtoCsvExporter.WriteCsvFile(outputPath, scanResult.Rows ?? new List<string[]>());
                }

                editor.WriteMessage("\n========== QTO 線槽網路 ==========");
                editor.WriteMessage("\n線槽數量：" + scanResult.TrayCount);
                editor.WriteMessage("\n節點數量：" + scanResult.NodeCount);
                editor.WriteMessage("\n線段數量：" + scanResult.EdgeCount);
                editor.WriteMessage("\n孤立線槽數量：" + scanResult.IsolatedTrayCount);
                editor.WriteMessage("\n未連接端點數量：" + scanResult.UnconnectedEndpointCount);
                editor.WriteMessage("\n短線段數量：" + scanResult.ShortSegmentCount);
                editor.WriteMessage("\nCSV：" + (outputPath ?? "未輸出"));
                editor.WriteMessage("\n=================================");
                ShowStepMessage(
                    "線槽掃描完成",
                    "已掃描 " + scanResult.TrayCount + " 條線槽。\n未連接端點：" + scanResult.UnconnectedEndpointCount + "\n孤立線槽：" + scanResult.IsolatedTrayCount,
                    "下一步建議：若端點連接正常，可執行「依線槽尋路」或「批次線槽尋路」。");
            }
            catch (System.Exception ex)
            {
                WriteCommandError("QTO_SCAN_TRAY_NETWORK", ex);
            }
        }

        [CommandMethod("QTO_ROUTE_BY_TRAY")]
        public void QtoRouteByTray()
        {
            try
            {
                Document document = Application.DocumentManager.MdiActiveDocument;

                if (document == null)
                {
                    return;
                }

                Editor editor = document.Editor;
                Database database = document.Database;
                PromptEntityResult outletResult = PromptOutletEntity(editor, "\n請點選一個出線口：");

                if (outletResult.Status != PromptStatus.OK)
                {
                    editor.WriteMessage("\n已取消線槽尋路。");
                    return;
                }

                PromptEntityResult junctionBoxResult = PromptJunctionBoxEntity(editor, "\n請點選一個結線箱：");

                if (junctionBoxResult.Status != PromptStatus.OK)
                {
                    editor.WriteMessage("\n已取消線槽尋路。");
                    return;
                }

                double tolerance = PromptDoubleWithDefault(editor, "\n線槽端點連接容許距離 mm", 100.0);
                TrayRouteResult routeResult = RouteOutletToJunctionBoxByTray(database, outletResult.ObjectId, junctionBoxResult.ObjectId, tolerance);

                if (!routeResult.Success)
                {
                    editor.WriteMessage("\n線槽尋路失敗：" + routeResult.ErrorMessage);
                    editor.WriteMessage("\n建議：檢查線槽端點是否連接，或改用一鍵連接產生雙 L 配線。");
                    return;
                }

                editor.WriteMessage("\n已建立線槽路徑配線：" + routeResult.WireObjectId.ToString());
                editor.WriteMessage("\nOUTLET_ID：" + routeResult.OutletId);
                editor.WriteMessage("\nJB_ID：" + routeResult.JbId);
                editor.WriteMessage("\nLENGTH_M：" + routeResult.LengthM.ToString("0.000"));
                ShowStepMessage(
                    "線槽尋路完成",
                    "已依線槽網路建立配線。\nOUTLET_ID：" + routeResult.OutletId + "\nJB_ID：" + routeResult.JbId + "\n長度：" + routeResult.LengthM.ToString("0.000") + " m",
                    "下一步建議：執行「重算長度」或「用線明細」核對報表。");
            }
            catch (System.Exception ex)
            {
                WriteCommandError("QTO_ROUTE_BY_TRAY", ex);
            }
        }

        [CommandMethod("QTO_BATCH_ROUTE_BY_TRAY")]
        public void QtoBatchRouteByTray()
        {
            try
            {
                if (DateTime.Now.Ticks >= 0)
                {
                    RunSelectedTrayBatchRoute();
                    return;
                }

                Document document = Application.DocumentManager.MdiActiveDocument;

                if (document == null)
                {
                    return;
                }

                Editor editor = document.Editor;
                Database database = document.Database;
                PromptSelectionOptions outletSelectionOptions = new PromptSelectionOptions();
                outletSelectionOptions.MessageForAdding = "\n請框選要依線槽尋路的出線口：";
                PromptSelectionResult outletSelectionResult = editor.GetSelection(outletSelectionOptions);

                if (outletSelectionResult.Status != PromptStatus.OK)
                {
                    editor.WriteMessage("\n已取消批次線槽尋路。");
                    return;
                }

                PromptEntityResult junctionBoxResult = PromptJunctionBoxEntity(editor, "\n請點選一個結線箱：");

                if (junctionBoxResult.Status != PromptStatus.OK)
                {
                    editor.WriteMessage("\n已取消批次線槽尋路。");
                    return;
                }

                double tolerance = PromptDoubleWithDefault(editor, "\n線槽端點連接容許距離 mm", 100.0);
                List<string[]> rows = new List<string[]>();
                rows.Add(new string[] { "出線口編號", "結線箱編號", "配線物件ID", "長度_公尺", "狀態", "錯誤訊息" });
                int successCount = 0;
                int failureCount = 0;

                foreach (SelectedObject selectedObject in outletSelectionResult.Value)
                {
                    if (selectedObject == null)
                    {
                        failureCount++;
                        rows.Add(new string[] { string.Empty, string.Empty, string.Empty, "0.000", "錯誤", "空白選取" });
                        continue;
                    }

                    TrayRouteResult routeResult = RouteOutletToJunctionBoxByTray(database, selectedObject.ObjectId, junctionBoxResult.ObjectId, tolerance);

                    if (routeResult.Success)
                    {
                        successCount++;
                        rows.Add(new string[] { routeResult.OutletId, routeResult.JbId, routeResult.WireObjectId.ToString(), routeResult.LengthM.ToString("0.000"), "正常", string.Empty });
                    }
                    else
                    {
                        failureCount++;
                        rows.Add(new string[] { routeResult.OutletId, routeResult.JbId, string.Empty, "0.000", "錯誤", routeResult.ErrorMessage });
                    }
                }

                string outputPath = PromptCsvOutputPath(database, "QTO_BATCH_ROUTE_BY_TRAY_");

                if (!string.IsNullOrWhiteSpace(outputPath))
                {
                    QtoCsvExporter.WriteCsvFile(outputPath, rows);
                }

                editor.WriteMessage("\n批次線槽尋路完成。");
                editor.WriteMessage("\n成功：" + successCount);
                editor.WriteMessage("\n失敗：" + failureCount);
                editor.WriteMessage("\nCSV：" + (outputPath ?? "未輸出"));
                ShowStepMessage(
                    "批次線槽尋路完成",
                    "成功：" + successCount + "\n失敗：" + failureCount + "\nCSV：" + (outputPath ?? "未輸出"),
                    "下一步建議：執行「資料檢查」確認配線關係。");
            }
            catch (System.Exception ex)
            {
                WriteCommandError("QTO_BATCH_ROUTE_BY_TRAY", ex);
            }
        }

        [CommandMethod("QTO_EXPORT_JB_SUMMARY")]
        public void QtoExportJbSummary()
        {
            try
            {
                Document document = Application.DocumentManager.MdiActiveDocument;

                if (document == null)
                {
                    return;
                }

                Editor editor = document.Editor;
                Database database = document.Database;
                ExportScopeSelection exportScope = PromptExportScope(editor, "線段明細");

                if (exportScope == null)
                {
                    editor.WriteMessage("\n已取消 CSV 匯出。");
                    return;
                }

                List<JunctionBoxSummaryInfo> summaries = null;
                List<WireInfo> wires = null;

                using (Transaction transaction = database.TransactionManager.StartTransaction())
                {
                    List<OutletInfo> outlets = QtoScanner.GetAllQtoOutlets(transaction, database);
                    wires = QtoScanner.GetAllQtoWires(transaction, database);
                    outlets = FilterOutletsByScope(outlets, exportScope);
                    wires = FilterWiresByScope(wires, exportScope);
                    summaries = BuildJunctionBoxSummaries(outlets, wires);
                    transaction.Commit();
                }

                string outputPath = PromptCsvOutputPath(database, "QTO_JB_SUMMARY_");

                if (string.IsNullOrWhiteSpace(outputPath))
                {
                    editor.WriteMessage("\n\u5df2\u53d6\u6d88 CSV \u532f\u51fa\u3002");
                    return;
                }

                outputPath = QtoCsvExporter.ExportJunctionBoxDetailSummary(summaries, wires, database, outputPath);

                editor.WriteMessage("\n\u7d50\u7dda\u7bb1\u7528\u7dda\u660e\u7d30\u8207\u7e3d\u8868\u5df2\u532f\u51fa\uff1a" + outputPath);
                editor.WriteMessage("\n\u914d\u7dda\u660e\u7d30\u6578\u91cf\uff1a" + wires.Count);
                editor.WriteMessage("\n\u7d50\u7dda\u7bb1\u5c0f\u8a08\u6578\u91cf\uff1a" + summaries.Count);
                ShowStepMessage(
                    "\u7d50\u7dda\u7bb1\u7528\u7dda\u5831\u8868\u532f\u51fa\u5b8c\u6210",
                    "\u5831\u8868\u5df2\u5305\u542b\u6bcf\u500b\u51fa\u7dda\u53e3\u5230\u7d50\u7dda\u7bb1\u7684\u9577\u5ea6\u660e\u7d30\uff0c\u4ee5\u53ca\u6bcf\u500b\u7d50\u7dda\u7bb1\u7684\u5c0f\u8a08\u3002\nCSV\uff1a" + outputPath,
                    "\u4e0b\u4e00\u6b65\u5efa\u8b70\uff1a\u5148\u770b\u300c\u660e\u7d30\u300d\u5217\u6838\u5c0d\u55ae\u689d\u9577\u5ea6\uff0c\u518d\u770b\u300c\u5c0f\u8a08\u300d\u5217\u6838\u5c0d\u7d50\u7dda\u7bb1\u7e3d\u9577\u5ea6\u3002");
            }
            catch (System.Exception ex)
            {
                WriteCommandError("QTO_EXPORT_JB_SUMMARY", ex);
            }
        }
    }
}
