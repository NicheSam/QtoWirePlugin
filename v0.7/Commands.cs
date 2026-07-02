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
        private const double OutletSortYTolerance = 0.0001;
        private const double TrayNodeMergeTolerance = 1.0;
        private const double TrayPointTolerance = 0.0001;
        private const double DefaultCalloutArrowLength = 800.0;
        private const double DefaultCalloutTextHeight = 250.0;
        private const double DefaultOutletLabelOffset = 500.0;
        private const double DefaultConduitMlineWidth = 5.0;
        private const string ConduitMlineStyleName = "QTO_CONDUIT_MLINE_W5_BLUE";
        private const string ErrorOutletLayerName = "QTO_ERROR_OUTLET";
        private const string ErrorWireLayerName = "QTO_ERROR_WIRE";
        private const string ErrorJbLayerName = "QTO_ERROR_JB";

        [CommandMethod("QTO_HELLO")]
        public void QtoHello()
        {
            try
            {
                Editor editor = GetEditor();

                if (editor == null)
                {
                    return;
                }

                editor.WriteMessage("\nQtoWirePlugin V0.7 \u5df2\u6210\u529f\u8f09\u5165\u3002");
            }
            catch (System.Exception ex)
            {
                WriteCommandError("QTO_HELLO", ex);
            }
        }

        [CommandMethod("QTO_SHOW_UI")]
        public void QtoShowUi()
        {
            try
            {
                QtoRibbon.CreateRibbon();
            }
            catch (System.Exception ex)
            {
                WriteCommandError("QTO_SHOW_UI", ex);
            }
        }

        [CommandMethod("QTO_PANEL")]
        public void QtoPanel()
        {
            try
            {
                QtoSyncMainPaletteHost.Show();
            }
            catch (System.Exception ex)
            {
                WriteCommandError("QTO_PANEL", ex);
            }
        }

        [CommandMethod("QTO_TEST_XDATA")]
        public void QtoTestXData()
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

                PromptEntityOptions options = new PromptEntityOptions("\n\u8acb\u9078\u53d6\u8981\u5beb\u5165\u6e2c\u8a66 XData \u7684\u7269\u4ef6\uff1a");
                PromptEntityResult result = editor.GetEntity(options);

                if (result.Status != PromptStatus.OK)
                {
                    editor.WriteMessage("\n\u5df2\u53d6\u6d88 QTO_TEST_XDATA\u3002");
                    return;
                }

                using (Transaction transaction = database.TransactionManager.StartTransaction())
                {
                    Entity entity = transaction.GetObject(result.ObjectId, OpenMode.ForWrite) as Entity;

                    if (entity == null)
                    {
                        editor.WriteMessage("\n\u9078\u53d6\u7684\u7269\u4ef6\u4e0d\u662f\u53ef\u5beb\u5165 XData \u7684 Entity\u3002");
                        return;
                    }

                    Dictionary<string, string> testData = new Dictionary<string, string>();
                    testData[QtoXDataHelper.KeyQtoType] = "TEST";
                    testData[QtoXDataHelper.KeySystem] = "DATA";

                    QtoXDataHelper.SetXData(entity, database, transaction, testData);

                    Dictionary<string, string> readData = QtoXDataHelper.GetXData(entity);

                    editor.WriteMessage("\n\u5df2\u5beb\u5165\u4e26\u8b80\u56de QTO_APP XData\uff1a");

                    foreach (KeyValuePair<string, string> item in readData)
                    {
                        editor.WriteMessage("\n" + item.Key + " = " + item.Value);
                    }

                    transaction.Commit();
                }
            }
            catch (System.Exception ex)
            {
                WriteCommandError("QTO_TEST_XDATA", ex);
            }
        }

        [CommandMethod("QTO_CREATE_CONNECTIONS")]
        public void QtoCreateConnections()
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
                selectionOptions.MessageForAdding = "\n\u8acb\u6846\u9078\u8981\u9023\u63a5\u5230\u7d50\u7dda\u7bb1\u7684\u51fa\u7dda\u53e3\u5716\u584a\uff1a";
                PromptSelectionResult selectionResult = editor.GetSelection(selectionOptions);

                if (selectionResult.Status != PromptStatus.OK)
                {
                    editor.WriteMessage("\n\u5df2\u53d6\u6d88 QTO_CREATE_CONNECTIONS\u3002");
                    return;
                }

                PromptEntityOptions junctionBoxOptions = new PromptEntityOptions("\n\u8acb\u9ede\u9078\u8981\u9023\u63a5\u7684\u7d50\u7dda\u7bb1\u5716\u584a\uff1a");
                PromptEntityResult junctionBoxResult = editor.GetEntity(junctionBoxOptions);

                if (junctionBoxResult.Status != PromptStatus.OK)
                {
                    editor.WriteMessage("\n\u5df2\u53d6\u6d88 QTO_CREATE_CONNECTIONS\u3002");
                    return;
                }

                const string defaultSystem = "DATA";
                const string defaultCableType = "Cat6";

                int selectedCount = selectionResult.Value.Count;
                int outletCount = 0;
                int ignoredCount = 0;
                int createdWireCount = 0;
                int skippedExistingWireCount = 0;
                string junctionBoxId = string.Empty;
                List<OutletSelectionItem> outletItems = new List<OutletSelectionItem>();

                using (Transaction transaction = database.TransactionManager.StartTransaction())
                {
                    BlockReference junctionBox = transaction.GetObject(junctionBoxResult.ObjectId, OpenMode.ForWrite, false) as BlockReference;

                    if (junctionBox == null)
                    {
                        editor.WriteMessage("\n\u7d50\u7dda\u7bb1\u5fc5\u9808\u662f BlockReference\u3002");
                        return;
                    }

                    junctionBoxId = QtoXDataHelper.GetXDataValue(junctionBox, QtoXDataHelper.KeyJbId) ?? string.Empty;

                    if (string.IsNullOrWhiteSpace(junctionBoxId))
                    {
                        junctionBoxId = "JB-" + GetNextJunctionBoxNumber(transaction, database).ToString("00");
                    }

                    QtoXDataHelper.UpdateXDataValue(junctionBox, database, transaction, QtoXDataHelper.KeyQtoType, QtoXDataHelper.TypeJunctionBox);
                    QtoXDataHelper.UpdateXDataValue(junctionBox, database, transaction, QtoXDataHelper.KeyJbId, junctionBoxId);
                    QtoXDataHelper.UpdateXDataValue(junctionBox, database, transaction, QtoXDataHelper.KeySystem, defaultSystem);

                    foreach (SelectedObject selectedObject in selectionResult.Value)
                    {
                        if (selectedObject == null || selectedObject.ObjectId == junctionBoxResult.ObjectId)
                        {
                            ignoredCount++;
                            continue;
                        }

                        BlockReference outlet = transaction.GetObject(selectedObject.ObjectId, OpenMode.ForRead, false) as BlockReference;

                        if (outlet == null)
                        {
                            ignoredCount++;
                            continue;
                        }

                        OutletSelectionItem item = new OutletSelectionItem();
                        item.ObjectId = outlet.ObjectId;
                        item.X = outlet.Position.X;
                        item.Y = outlet.Position.Y;
                        item.ExistingValue = QtoXDataHelper.GetXDataValue(outlet, QtoXDataHelper.KeyOutletId) ?? string.Empty;
                        outletItems.Add(item);
                    }

                    outletItems.Sort(CompareOutletSelectionItems);

                    List<WireInfo> existingWires = QtoScanner.GetAllQtoWires(transaction, database);
                    Dictionary<string, bool> existingWireOutletIds = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

                    foreach (WireInfo wire in existingWires)
                    {
                        if (!string.IsNullOrWhiteSpace(wire.OutletId) && !existingWireOutletIds.ContainsKey(wire.OutletId))
                        {
                            existingWireOutletIds.Add(wire.OutletId, true);
                        }
                    }

                    int nextOutletNumber = GetNextOutletNumber(transaction, database);
                    BlockTableRecord modelSpace = GetModelSpace(transaction, database);
                    modelSpace.UpgradeOpen();

                    foreach (OutletSelectionItem item in outletItems)
                    {
                        BlockReference outlet = transaction.GetObject(item.ObjectId, OpenMode.ForWrite, false) as BlockReference;

                        if (outlet == null)
                        {
                            ignoredCount++;
                            continue;
                        }

                        string outletId = QtoXDataHelper.GetXDataValue(outlet, QtoXDataHelper.KeyOutletId) ?? string.Empty;

                        if (string.IsNullOrWhiteSpace(outletId))
                        {
                            outletId = "O-" + nextOutletNumber.ToString("000");
                            nextOutletNumber++;
                        }

                        QtoXDataHelper.UpdateXDataValue(outlet, database, transaction, QtoXDataHelper.KeyQtoType, QtoXDataHelper.TypeOutlet);
                        QtoXDataHelper.UpdateXDataValue(outlet, database, transaction, QtoXDataHelper.KeyOutletId, outletId);
                        QtoXDataHelper.UpdateXDataValue(outlet, database, transaction, QtoXDataHelper.KeyJbId, junctionBoxId);
                        QtoXDataHelper.UpdateXDataValue(outlet, database, transaction, QtoXDataHelper.KeySystem, defaultSystem);
                        QtoXDataHelper.UpdateXDataValue(outlet, database, transaction, QtoXDataHelper.KeyCableType, defaultCableType);
                        outletCount++;

                        if (existingWireOutletIds.ContainsKey(outletId))
                        {
                            skippedExistingWireCount++;
                            continue;
                        }

                        Polyline wire = CreateDoubleLPolyline(outlet.Position, junctionBox.Position);
                        modelSpace.AppendEntity(wire);
                        transaction.AddNewlyCreatedDBObject(wire, true);
                        QtoLayerHelper.MoveEntityToLayer(wire, database, transaction, QtoLayerHelper.WireLayerName);

                        Dictionary<string, string> wireData = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        wireData[QtoXDataHelper.KeyQtoType] = QtoXDataHelper.TypeWire;
                        wireData[QtoXDataHelper.KeyOutletId] = outletId;
                        wireData[QtoXDataHelper.KeyJbId] = junctionBoxId;
                        wireData[QtoXDataHelper.KeySystem] = defaultSystem;
                        wireData[QtoXDataHelper.KeyCableType] = defaultCableType;
                        QtoXDataHelper.SetXData(wire, database, transaction, wireData);

                        existingWireOutletIds[outletId] = true;
                        createdWireCount++;
                    }

                    transaction.Commit();
                }

                editor.WriteMessage("\n========== QTO CREATE CONNECTIONS ==========");
                editor.WriteMessage("\n\u9078\u53d6\u7269\u4ef6\u6578\u91cf\uff1a" + selectedCount);
                editor.WriteMessage("\n\u8655\u7406\u51fa\u7dda\u53e3\u6578\u91cf\uff1a" + outletCount);
                editor.WriteMessage("\n\u4f7f\u7528\u7d50\u7dda\u7bb1\u7de8\u865f\uff1a" + junctionBoxId);
                editor.WriteMessage("\n\u65b0\u589e\u96d9 L \u914d\u7dda\u6578\u91cf\uff1a" + createdWireCount);
                editor.WriteMessage("\n\u5df2\u6709\u914d\u7dda\u800c\u8df3\u904e\uff1a" + skippedExistingWireCount);
                editor.WriteMessage("\n\u5ffd\u7565\u6578\u91cf\uff1a" + ignoredCount);
                editor.WriteMessage("\n===========================================");
                ShowStepMessage(
                    "\u9023\u63a5\u5df2\u5efa\u7acb",
                    "\u5df2\u81ea\u52d5\u6a19\u8a18\u51fa\u7dda\u53e3\u8207\u7d50\u7dda\u7bb1\uff0c\u4e26\u7522\u751f " + createdWireCount + " \u689d\u53ef\u8abf\u6574\u7684\u96d9 L Polyline \u914d\u7dda\u3002\n\u7d50\u7dda\u7bb1\uff1a" + junctionBoxId,
                    "\u4e0b\u4e00\u6b65\u5efa\u8b70\uff1a\u5148\u62d6\u66f3 Polyline \u7bc0\u9ede\u8abf\u6574\u8def\u5f91\uff0c\u518d\u57f7\u884c QTO_RECALC_WIRE \u6216 QTO_EXPORT_JB_SUMMARY\u3002");
            }
            catch (System.Exception ex)
            {
                WriteCommandError("QTO_CREATE_CONNECTIONS", ex);
            }
        }

        [CommandMethod("QTO_CALLOUT_TO_JB")]
        public void QtoCalloutToJb()
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
                selectionOptions.MessageForAdding = "\n請框選要產生指向箱體標註的出線口或圖塊：";
                PromptSelectionResult selectionResult = editor.GetSelection(selectionOptions);

                if (selectionResult.Status != PromptStatus.OK)
                {
                    editor.WriteMessage("\n已取消 QTO_CALLOUT_TO_JB。");
                    return;
                }

                PromptEntityResult junctionBoxResult = PromptJunctionBoxEntity(editor, "\n請點選箭頭要指向的箱體：");

                if (junctionBoxResult.Status != PromptStatus.OK)
                {
                    editor.WriteMessage("\n已取消 QTO_CALLOUT_TO_JB。");
                    return;
                }

                double arrowLength = PromptDoubleWithDefault(editor, "\n短箭頭長度", DefaultCalloutArrowLength);
                double textHeightDefault = database.Textsize > 0.0 ? database.Textsize : DefaultCalloutTextHeight;
                double textHeight = PromptDoubleWithDefault(editor, "\n文字高度", textHeightDefault);
                int createdCount = 0;
                int ignoredCount = 0;
                string junctionBoxId = string.Empty;

                using (Transaction transaction = database.TransactionManager.StartTransaction())
                {
                    BlockReference junctionBox = transaction.GetObject(junctionBoxResult.ObjectId, OpenMode.ForRead, false) as BlockReference;

                    if (junctionBox == null)
                    {
                        editor.WriteMessage("\n箱體必須是 BlockReference。");
                        return;
                    }

                    junctionBoxId = GetJunctionBoxDisplayName(transaction, junctionBox);
                    BlockTableRecord modelSpace = GetModelSpace(transaction, database);
                    modelSpace.UpgradeOpen();

                    foreach (SelectedObject selectedObject in selectionResult.Value)
                    {
                        if (selectedObject == null || selectedObject.ObjectId == junctionBoxResult.ObjectId)
                        {
                            ignoredCount++;
                            continue;
                        }

                        BlockReference sourceBlock = transaction.GetObject(selectedObject.ObjectId, OpenMode.ForWrite, false) as BlockReference;

                        if (sourceBlock == null)
                        {
                            ignoredCount++;
                            continue;
                        }

                        AssignBlockToJunctionBox(sourceBlock, database, transaction, junctionBoxId);
                        CreateCalloutAnnotation(modelSpace, database, transaction, sourceBlock, junctionBox, junctionBoxId, arrowLength, textHeight);
                        createdCount++;
                    }

                    transaction.Commit();
                }

                editor.WriteMessage("\n已建立指向箱體標註：" + createdCount + " 個。");
                editor.WriteMessage("\n忽略：" + ignoredCount + " 個。");
                ShowStepMessage(
                    "箱體指向標註完成",
                    "已建立 " + createdCount + " 個短箭頭與文字標註。\n標註文字：to" + junctionBoxId,
                    "若位置需要微調，可直接移動 QTO_CALLOUT 圖層上的箭頭與文字。");
            }
            catch (System.Exception ex)
            {
                WriteCommandError("QTO_CALLOUT_TO_JB", ex);
            }
        }

        [CommandMethod("QTO_LABEL_OUTLETS")]
        public void QtoLabelOutlets()
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
                selectionOptions.MessageForAdding = "\n請框選要顯示出線口編號的圖塊：";
                PromptSelectionResult selectionResult = editor.GetSelection(selectionOptions);

                if (selectionResult.Status != PromptStatus.OK)
                {
                    editor.WriteMessage("\n已取消 QTO_LABEL_OUTLETS。");
                    return;
                }

                double textHeightDefault = database.Textsize > 0.0 ? database.Textsize : DefaultCalloutTextHeight;
                double textHeight = PromptDoubleWithDefault(editor, "\n文字高度", textHeightDefault);
                double yOffset = PromptDoubleWithDefault(editor, "\n文字向上偏移距離", DefaultOutletLabelOffset);
                int createdCount = 0;
                int skippedCount = 0;

                using (Transaction transaction = database.TransactionManager.StartTransaction())
                {
                    BlockTableRecord modelSpace = GetModelSpace(transaction, database);
                    modelSpace.UpgradeOpen();

                    foreach (SelectedObject selectedObject in selectionResult.Value)
                    {
                        if (selectedObject == null)
                        {
                            skippedCount++;
                            continue;
                        }

                        BlockReference outlet = transaction.GetObject(selectedObject.ObjectId, OpenMode.ForRead, false) as BlockReference;

                        if (outlet == null)
                        {
                            skippedCount++;
                            continue;
                        }

                        string outletId = QtoXDataHelper.GetXDataValue(outlet, QtoXDataHelper.KeyOutletId) ?? string.Empty;

                        if (string.IsNullOrWhiteSpace(outletId))
                        {
                            skippedCount++;
                            continue;
                        }

                        DeleteExistingOutletLabels(modelSpace, transaction, outletId);
                        DBText label = CreateOutletLabelText(outlet.Position, outletId, textHeight, yOffset);
                        modelSpace.AppendEntity(label);
                        transaction.AddNewlyCreatedDBObject(label, true);
                        QtoLayerHelper.MoveEntityToLayer(label, database, transaction, QtoLayerHelper.OutletLabelLayerName);

                        Dictionary<string, string> data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        data[QtoXDataHelper.KeyQtoType] = QtoXDataHelper.TypeOutletLabel;
                        data[QtoXDataHelper.KeyOutletId] = outletId;
                        QtoXDataHelper.SetXData(label, database, transaction, data);
                        createdCount++;
                    }

                    transaction.Commit();
                }

                editor.WriteMessage("\n已建立出線口編號標註：" + createdCount + " 個。");
                editor.WriteMessage("\n略過：" + skippedCount + " 個。");
                ShowStepMessage(
                    "出線口編號標註完成",
                    "已建立 " + createdCount + " 個出線口編號文字，圖層為 QTO_OUTLET_LABEL。",
                    "若出線口編號有異動，重新執行此命令即可更新文字。");
            }
            catch (System.Exception ex)
            {
                WriteCommandError("QTO_LABEL_OUTLETS", ex);
            }
        }

        [CommandMethod("QTO_CONDUIT_TO_TRAY")]
        public void QtoConduitToTray()
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

                PromptSelectionOptions traySelectionOptions = new PromptSelectionOptions();
                traySelectionOptions.MessageForAdding = "\n請選取要接到的線槽 Polyline：";
                PromptSelectionResult traySelectionResult = editor.GetSelection(traySelectionOptions);

                if (traySelectionResult.Status != PromptStatus.OK)
                {
                    editor.WriteMessage("\n已取消 QTO_CONDUIT_TO_TRAY。");
                    return;
                }

                PromptSelectionOptions outletSelectionOptions = new PromptSelectionOptions();
                outletSelectionOptions.MessageForAdding = "\n請框選要生成管段到線槽的出線口或圖塊：";
                PromptSelectionResult outletSelectionResult = editor.GetSelection(outletSelectionOptions);

                if (outletSelectionResult.Status != PromptStatus.OK)
                {
                    editor.WriteMessage("\n已取消 QTO_CONDUIT_TO_TRAY。");
                    return;
                }

                List<ObjectId> trayObjectIds = new List<ObjectId>();

                foreach (SelectedObject selectedTray in traySelectionResult.Value)
                {
                    if (selectedTray != null)
                    {
                        trayObjectIds.Add(selectedTray.ObjectId);
                    }
                }

                int sourceCount = 0;
                int successCount = 0;
                int failureCount = 0;
                List<string> createdDiagnostics = new List<string>();

                using (Transaction transaction = database.TransactionManager.StartTransaction())
                {
                    List<SelectedTraySegment> segments = BuildSelectedTraySegments(transaction, trayObjectIds);

                    if (segments.Count == 0)
                    {
                        editor.WriteMessage("\n沒有可用的線槽 Polyline。");
                        return;
                    }

                    BlockTableRecord modelSpace = GetModelSpace(transaction, database);
                    modelSpace.UpgradeOpen();
                    QtoLayerHelper.EnsureLayerVisible(database, transaction, QtoLayerHelper.ConduitLayerName);

                    foreach (SelectedObject selectedOutlet in outletSelectionResult.Value)
                    {
                        if (selectedOutlet == null)
                        {
                            failureCount++;
                            continue;
                        }

                        sourceCount++;
                        BlockReference sourceBlock = transaction.GetObject(selectedOutlet.ObjectId, OpenMode.ForRead, false) as BlockReference;

                        if (sourceBlock == null)
                        {
                            failureCount++;
                            continue;
                        }

                        string outletId = QtoXDataHelper.GetXDataValue(sourceBlock, QtoXDataHelper.KeyOutletId) ?? string.Empty;
                        string system = QtoXDataHelper.GetXDataValue(sourceBlock, QtoXDataHelper.KeySystem) ?? string.Empty;
                        SelectedTrayProjection projection = FindClosestTrayProjection(sourceBlock.Position, segments);

                        if (projection == null)
                        {
                            failureCount++;
                            continue;
                        }

                        List<Point3d> conduitPoints = BuildOrthogonalRouteToPoint(sourceBlock.Position, projection.Point);

                        if (conduitPoints.Count < 2)
                        {
                            failureCount++;
                            continue;
                        }

                        ConduitPolylinePair conduitPair = CreateConduitPolylinePair(conduitPoints);

                        if (conduitPair == null || conduitPair.Primary == null || conduitPair.Secondary == null)
                        {
                            failureCount++;
                            continue;
                        }

                        modelSpace.AppendEntity(conduitPair.Primary);
                        transaction.AddNewlyCreatedDBObject(conduitPair.Primary, true);
                        QtoLayerHelper.MoveEntityToLayer(conduitPair.Primary, database, transaction, QtoLayerHelper.ConduitLayerName);

                        modelSpace.AppendEntity(conduitPair.Secondary);
                        transaction.AddNewlyCreatedDBObject(conduitPair.Secondary, true);
                        QtoLayerHelper.MoveEntityToLayer(conduitPair.Secondary, database, transaction, QtoLayerHelper.ConduitLayerName);

                        double lengthM = QtoGeometryHelper.ConvertMmToM(GetPointPathLength(conduitPoints));
                        Dictionary<string, string> data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        data[QtoXDataHelper.KeyQtoType] = QtoXDataHelper.TypeConduitSegment;
                        data[QtoXDataHelper.KeyOutletId] = outletId;
                        data[QtoXDataHelper.KeySystem] = system;
                        data[QtoXDataHelper.KeyLengthM] = lengthM.ToString("0.000");
                        data[QtoXDataHelper.KeyLengthSource] = "PARALLEL_POLYLINE_CENTER_LENGTH";
                        QtoXDataHelper.SetXData(conduitPair.Primary, database, transaction, data);

                        Dictionary<string, string> visualData = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        visualData[QtoXDataHelper.KeyQtoType] = QtoXDataHelper.TypeConduitVisual;
                        visualData[QtoXDataHelper.KeyOutletId] = outletId;
                        visualData[QtoXDataHelper.KeySystem] = system;
                        visualData[QtoXDataHelper.KeyLengthM] = lengthM.ToString("0.000");
                        visualData[QtoXDataHelper.KeyLengthSource] = "PARALLEL_POLYLINE_VISUAL";
                        QtoXDataHelper.SetXData(conduitPair.Secondary, database, transaction, visualData);

                        successCount++;

                        if (createdDiagnostics.Count < 5)
                        {
                            createdDiagnostics.Add(conduitPair.Primary.ObjectId.ToString() + " + " + conduitPair.Secondary.ObjectId.ToString() + " layer=" + conduitPair.Primary.Layer + " vertices=" + conduitPair.Primary.NumberOfVertices);
                        }
                    }

                    transaction.Commit();
                }

                editor.Regen();

                editor.WriteMessage("\n管段生成完成。");
                editor.WriteMessage("\n選取圖塊：" + sourceCount);
                editor.WriteMessage("\n成功：" + successCount);
                editor.WriteMessage("\n失敗：" + failureCount);

                foreach (string diagnostic in createdDiagnostics)
                {
                    editor.WriteMessage("\n建立管段：" + diagnostic);
                }

                ShowStepMessage(
                    "管段到線槽完成",
                    "已在模型空間建立 " + successCount + " 條 QTO_CONDUIT 管段。",
                    "如需輸出管段長度，請另外執行「管段明細」。");
            }
            catch (System.Exception ex)
            {
                WriteCommandError("QTO_CONDUIT_TO_TRAY", ex);
            }
        }

        [CommandMethod("QTO_EXPORT_CONDUIT_SUMMARY")]
        public void QtoExportConduitSummary()
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
                ExportScopeSelection exportScope = PromptExportScope(editor, "管段明細");

                if (exportScope == null)
                {
                    editor.WriteMessage("\n已取消管段明細匯出。");
                    return;
                }

                List<string[]> rows = new List<string[]>();
                rows.Add(new string[] { "管段物件ID", "出線口編號", "系統", "圖層", "長度_公尺", "長度來源", "狀態", "錯誤訊息" });
                int conduitCount = 0;

                using (Transaction transaction = database.TransactionManager.StartTransaction())
                {
                    BlockTableRecord modelSpace = GetModelSpace(transaction, database);

                    foreach (ObjectId objectId in modelSpace)
                    {
                        if (!ObjectIdInExportScope(objectId, exportScope))
                        {
                            continue;
                        }

                        Entity conduit = transaction.GetObject(objectId, OpenMode.ForRead, false) as Entity;

                        if (conduit == null || !QtoXDataHelper.HasQtoType(conduit, QtoXDataHelper.TypeConduitSegment))
                        {
                            continue;
                        }

                        double lengthMm = GetConduitLength(conduit);

                        conduitCount++;
                        string outletId = QtoXDataHelper.GetXDataValue(conduit, QtoXDataHelper.KeyOutletId) ?? string.Empty;
                        string system = QtoXDataHelper.GetXDataValue(conduit, QtoXDataHelper.KeySystem) ?? string.Empty;
                        string lengthSource = QtoXDataHelper.GetXDataValue(conduit, QtoXDataHelper.KeyLengthSource) ?? string.Empty;
                        double lengthM = GetConduitLengthM(conduit, lengthMm, lengthSource);
                        string status = "OK";
                        string errorMessage = string.Empty;

                        if (lengthM <= 0.0)
                        {
                            status = "錯誤";
                            errorMessage = "長度為 0";
                        }
                        else
                        {
                            status = "正常";
                        }

                        Dictionary<string, string> data = QtoXDataHelper.GetXData(conduit);
                        data[QtoXDataHelper.KeyQtoType] = QtoXDataHelper.TypeConduitSegment;
                        data[QtoXDataHelper.KeyLengthM] = lengthM.ToString("0.000");
                        data[QtoXDataHelper.KeyLengthSource] = GetConduitLengthSource(conduit, lengthSource);
                        conduit.UpgradeOpen();
                        QtoXDataHelper.SetXData(conduit, database, transaction, data);

                        rows.Add(new string[]
                        {
                            conduit.ObjectId.ToString(),
                            outletId,
                            system,
                            conduit.Layer ?? string.Empty,
                            lengthM.ToString("0.000"),
                            data[QtoXDataHelper.KeyLengthSource],
                            status,
                            errorMessage
                        });
                    }

                    transaction.Commit();
                }

                string outputPath = PromptCsvOutputPath(database, "QTO_CONDUIT_SUMMARY_");

                if (!string.IsNullOrWhiteSpace(outputPath))
                {
                    QtoCsvExporter.WriteCsvFile(outputPath, rows);
                }

                editor.WriteMessage("\n管段明細匯出完成。");
                editor.WriteMessage("\n管段數量：" + conduitCount);
                editor.WriteMessage("\nCSV：" + (outputPath ?? "未輸出"));
                ShowStepMessage(
                    "管段明細完成",
                    "已匯出 " + conduitCount + " 條 QTO_CONDUIT 管段明細。\nCSV：" + (outputPath ?? "未輸出"),
                    "若圖面管段有調整，重新執行本命令即可更新長度。");
            }
            catch (System.Exception ex)
            {
                WriteCommandError("QTO_EXPORT_CONDUIT_SUMMARY", ex);
            }
        }

        [CommandMethod("QTO_MARK_OUTLETS")]
        public void QtoMarkOutlets()
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
                selectionOptions.MessageForAdding = "\n\u8acb\u6846\u9078\u8981\u6a19\u8a18\u70ba\u51fa\u7dda\u53e3\u7684\u7269\u4ef6\uff1a";
                PromptSelectionResult selectionResult = editor.GetSelection(selectionOptions);

                if (selectionResult.Status != PromptStatus.OK)
                {
                    editor.WriteMessage("\n\u5df2\u53d6\u6d88 QTO_MARK_OUTLETS\u3002");
                    return;
                }

                string system = "DATA";
                string cableType = "Cat6";

                int selectedCount = selectionResult.Value.Count;
                int markedCount = 0;
                int ignoredCount = 0;
                int newNumberedCount = 0;
                int preservedNumberCount = 0;
                List<OutletSelectionItem> markedOutlets = new List<OutletSelectionItem>();

                using (Transaction transaction = database.TransactionManager.StartTransaction())
                {
                    foreach (SelectedObject selectedObject in selectionResult.Value)
                    {
                        if (selectedObject == null)
                        {
                            ignoredCount++;
                            continue;
                        }

                        BlockReference blockReference = transaction.GetObject(selectedObject.ObjectId, OpenMode.ForRead, false) as BlockReference;

                        if (blockReference == null)
                        {
                            ignoredCount++;
                            continue;
                        }

                        OutletSelectionItem item = new OutletSelectionItem();
                        item.ObjectId = blockReference.ObjectId;
                        item.X = blockReference.Position.X;
                        item.Y = blockReference.Position.Y;
                        item.ExistingValue = QtoXDataHelper.GetXDataValue(blockReference, QtoXDataHelper.KeyOutletId) ?? string.Empty;
                        markedOutlets.Add(item);
                    }

                    markedOutlets.Sort(CompareOutletSelectionItems);
                    int nextOutletNumber = GetNextOutletNumber(transaction, database);

                    for (int i = 0; i < markedOutlets.Count; i++)
                    {
                        BlockReference blockReference = transaction.GetObject(markedOutlets[i].ObjectId, OpenMode.ForWrite, false) as BlockReference;

                        if (blockReference == null)
                        {
                            continue;
                        }

                        Dictionary<string, string> data = QtoXDataHelper.GetXData(blockReference);
                        string existingOutletId = GetDictionaryValue(data, QtoXDataHelper.KeyOutletId);
                        data[QtoXDataHelper.KeyQtoType] = QtoXDataHelper.TypeOutlet;

                        if (string.IsNullOrWhiteSpace(existingOutletId))
                        {
                            data[QtoXDataHelper.KeyOutletId] = "O-" + nextOutletNumber.ToString("000");
                            nextOutletNumber++;
                            newNumberedCount++;
                        }
                        else
                        {
                            data[QtoXDataHelper.KeyOutletId] = existingOutletId;
                            preservedNumberCount++;
                        }

                        if (!data.ContainsKey(QtoXDataHelper.KeyJbId))
                        {
                            data[QtoXDataHelper.KeyJbId] = string.Empty;
                        }

                        if (string.IsNullOrWhiteSpace(GetDictionaryValue(data, QtoXDataHelper.KeySystem)))
                        {
                            data[QtoXDataHelper.KeySystem] = system;
                        }

                        if (string.IsNullOrWhiteSpace(GetDictionaryValue(data, QtoXDataHelper.KeyCableType)))
                        {
                            data[QtoXDataHelper.KeyCableType] = cableType;
                        }

                        QtoXDataHelper.SetXData(blockReference, database, transaction, data);
                        markedCount++;
                    }

                    transaction.Commit();
                }

                editor.WriteMessage("\n\u9078\u53d6\u7269\u4ef6\u6578\u91cf\uff1a" + selectedCount);
                editor.WriteMessage("\n\u6210\u529f\u6a19\u8a18\u6578\u91cf\uff1a" + markedCount);
                editor.WriteMessage("\n\u5ffd\u7565\u6578\u91cf\uff1a" + ignoredCount);
                ShowStepMessage(
                    "\u51fa\u7dda\u53e3\u6a19\u8a18\u5b8c\u6210",
                    "\u5df2\u6a19\u8a18 " + markedCount + " \u500b\u51fa\u7dda\u53e3\u3002\n\u65b0\u7de8\u865f\uff1a" + newNumberedCount + "\n\u4fdd\u7559\u65e2\u6709\u7de8\u865f\uff1a" + preservedNumberCount + "\nSYSTEM \u7a7a\u767d\u6642\u6703\u81ea\u52d5\u8a2d\u70ba DATA\uff0cCABLE_TYPE \u7a7a\u767d\u6642\u6703\u81ea\u52d5\u8a2d\u70ba Cat6\u3002\n\u5ffd\u7565 " + ignoredCount + " \u500b\u975e\u5716\u584a\u7269\u4ef6\u3002",
                    "\u4e0b\u4e00\u6b65\u5efa\u8b70\uff1a\u57f7\u884c QTO_MARK_JB \u6a19\u8a18\u7d50\u7dda\u7bb1\u3002");
            }
            catch (System.Exception ex)
            {
                WriteCommandError("QTO_MARK_OUTLETS", ex);
            }
        }

        [CommandMethod("QTO_MARK_JB")]
        public void QtoMarkJb()
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
                selectionOptions.MessageForAdding = "\n\u8acb\u9078\u53d6\u8981\u6a19\u8a18\u70ba\u7d50\u7dda\u7bb1\u7684\u5716\u584a\uff1a";
                PromptSelectionResult selectionResult = editor.GetSelection(selectionOptions);

                if (selectionResult.Status != PromptStatus.OK)
                {
                    editor.WriteMessage("\n\u5df2\u53d6\u6d88 QTO_MARK_JB\u3002");
                    return;
                }

                List<ObjectId> blockIds = new List<ObjectId>();

                using (Transaction transaction = database.TransactionManager.StartTransaction())
                {
                    foreach (SelectedObject selectedObject in selectionResult.Value)
                    {
                        if (selectedObject == null)
                        {
                            continue;
                        }

                        BlockReference blockReference = transaction.GetObject(selectedObject.ObjectId, OpenMode.ForRead, false) as BlockReference;

                        if (blockReference != null)
                        {
                            blockIds.Add(selectedObject.ObjectId);
                        }
                    }

                    transaction.Commit();
                }

                if (blockIds.Count == 0)
                {
                    editor.WriteMessage("\n\u6c92\u6709\u9078\u5230 BlockReference\u3002");
                    return;
                }

                string jbInput = PromptStringWithDefault(editor, "\n\u8f38\u5165 JB_ID \u6216\u7de8\u865f\u524d\u7db4", "JB-01");
                string system = "DATA";
                int markedCount = 0;
                List<string> assignedIds = new List<string>();

                using (Transaction transaction = database.TransactionManager.StartTransaction())
                {
                    for (int i = 0; i < blockIds.Count; i++)
                    {
                        BlockReference blockReference = transaction.GetObject(blockIds[i], OpenMode.ForWrite, false) as BlockReference;

                        if (blockReference == null)
                        {
                            continue;
                        }

                        string jbId = jbInput;

                        if (blockIds.Count > 1)
                        {
                            jbId = BuildSequentialJbId(jbInput, i + 1);
                        }

                        Dictionary<string, string> data = new Dictionary<string, string>();
                        data[QtoXDataHelper.KeyQtoType] = QtoXDataHelper.TypeJunctionBox;
                        data[QtoXDataHelper.KeyJbId] = jbId;
                        data[QtoXDataHelper.KeySystem] = system;

                        QtoXDataHelper.SetXData(blockReference, database, transaction, data);
                        assignedIds.Add(jbId);
                        markedCount++;
                    }

                    transaction.Commit();
                }

                editor.WriteMessage("\n\u6210\u529f\u6a19\u8a18\u7d50\u7dda\u7bb1\u6578\u91cf\uff1a" + markedCount);

                foreach (string jbId in assignedIds)
                {
                    editor.WriteMessage("\nJB_ID = " + jbId);
                }

                ShowStepMessage(
                    "\u7d50\u7dda\u7bb1\u6a19\u8a18\u5b8c\u6210",
                    "\u5df2\u6a19\u8a18 " + markedCount + " \u500b\u7d50\u7dda\u7bb1\u3002\n\u7d50\u7dda\u7bb1\u7de8\u865f\u5df2\u5beb\u5165 JB_ID\uff0cSYSTEM \u5df2\u81ea\u52d5\u8a2d\u70ba DATA\u3002",
                    "\u4e0b\u4e00\u6b65\u5efa\u8b70\uff1a\u57f7\u884c QTO_ASSIGN_JB\uff0c\u5c07\u51fa\u7dda\u53e3\u6307\u5b9a\u5230\u7d50\u7dda\u7bb1\u3002");
            }
            catch (System.Exception ex)
            {
                WriteCommandError("QTO_MARK_JB", ex);
            }
        }

        [CommandMethod("QTO_NUMBER_OUTLETS")]
        public void QtoNumberOutlets()
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
                selectionOptions.MessageForAdding = "\n\u8acb\u6846\u9078\u8981\u81ea\u52d5\u7de8\u865f\u7684\u51fa\u7dda\u53e3\uff1a";
                PromptSelectionResult selectionResult = editor.GetSelection(selectionOptions);

                if (selectionResult.Status != PromptStatus.OK)
                {
                    editor.WriteMessage("\n\u5df2\u53d6\u6d88 QTO_NUMBER_OUTLETS\u3002");
                    return;
                }

                List<OutletSelectionItem> outlets = new List<OutletSelectionItem>();
                int ignoredCount = 0;
                bool hasExistingOutletId = false;

                using (Transaction transaction = database.TransactionManager.StartTransaction())
                {
                    foreach (SelectedObject selectedObject in selectionResult.Value)
                    {
                        if (selectedObject == null)
                        {
                            ignoredCount++;
                            continue;
                        }

                        BlockReference blockReference = transaction.GetObject(selectedObject.ObjectId, OpenMode.ForRead, false) as BlockReference;

                        if (blockReference == null || !QtoXDataHelper.HasQtoType(blockReference, QtoXDataHelper.TypeOutlet))
                        {
                            ignoredCount++;
                            continue;
                        }

                        string outletId = QtoXDataHelper.GetXDataValue(blockReference, QtoXDataHelper.KeyOutletId) ?? string.Empty;

                        if (!string.IsNullOrWhiteSpace(outletId))
                        {
                            hasExistingOutletId = true;
                        }

                        OutletSelectionItem item = new OutletSelectionItem();
                        item.ObjectId = selectedObject.ObjectId;
                        item.X = blockReference.Position.X;
                        item.Y = blockReference.Position.Y;
                        item.ExistingValue = outletId;
                        outlets.Add(item);
                    }

                    transaction.Commit();
                }

                if (outlets.Count == 0)
                {
                    editor.WriteMessage("\n\u6c92\u6709\u53ef\u7de8\u865f\u7684 QTO \u51fa\u7dda\u53e3\u3002");
                    editor.WriteMessage("\n\u8df3\u904e\u6578\u91cf\uff1a" + ignoredCount);
                    return;
                }

                bool overwriteExisting = true;

                if (hasExistingOutletId)
                {
                    overwriteExisting = PromptYesNo(editor, "\n\u90e8\u5206\u51fa\u7dda\u53e3\u5df2\u6709 OUTLET_ID\uff0c\u662f\u5426\u8986\u84cb\uff1f", false);
                }

                outlets.Sort(CompareOutletSelectionItems);

                int numberedCount = 0;
                int skippedCount = ignoredCount;
                int overwrittenCount = 0;

                using (Transaction transaction = database.TransactionManager.StartTransaction())
                {
                    for (int i = 0; i < outlets.Count; i++)
                    {
                        OutletSelectionItem item = outlets[i];

                        if (!string.IsNullOrWhiteSpace(item.ExistingValue) && !overwriteExisting)
                        {
                            skippedCount++;
                            continue;
                        }

                        BlockReference blockReference = transaction.GetObject(item.ObjectId, OpenMode.ForWrite, false) as BlockReference;

                        if (blockReference == null || !QtoXDataHelper.HasQtoType(blockReference, QtoXDataHelper.TypeOutlet))
                        {
                            skippedCount++;
                            continue;
                        }

                        if (!string.IsNullOrWhiteSpace(item.ExistingValue))
                        {
                            overwrittenCount++;
                        }

                        string outletId = "O-" + (numberedCount + 1).ToString("000");
                        QtoXDataHelper.UpdateXDataValue(blockReference, database, transaction, QtoXDataHelper.KeyOutletId, outletId);
                        numberedCount++;
                    }

                    transaction.Commit();
                }

                editor.WriteMessage("\n\u6210\u529f\u7de8\u865f\u6578\u91cf\uff1a" + numberedCount);
                editor.WriteMessage("\n\u8df3\u904e\u6578\u91cf\uff1a" + skippedCount);
                editor.WriteMessage("\n\u8986\u84cb\u6578\u91cf\uff1a" + overwrittenCount);
                ShowStepMessage(
                    "\u51fa\u7dda\u53e3\u7de8\u865f\u5b8c\u6210",
                    "\u5df2\u81ea\u52d5\u7de8\u865f " + numberedCount + " \u500b\u51fa\u7dda\u53e3\u3002\n\u8df3\u904e " + skippedCount + " \u500b\uff0c\u8986\u84cb " + overwrittenCount + " \u500b\u65e2\u6709\u7de8\u865f\u3002",
                    "\u4e0b\u4e00\u6b65\u5efa\u8b70\uff1a\u57f7\u884c QTO_ASSIGN_JB\uff0c\u6307\u5b9a\u9019\u4e9b\u51fa\u7dda\u53e3\u5c0d\u61c9\u7684\u7d50\u7dda\u7bb1\u3002");
            }
            catch (System.Exception ex)
            {
                WriteCommandError("QTO_NUMBER_OUTLETS", ex);
            }
        }

        [CommandMethod("QTO_ASSIGN_JB")]
        public void QtoAssignJb()
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
                selectionOptions.MessageForAdding = "\n\u8acb\u6846\u9078\u8981\u6307\u5b9a\u7d50\u7dda\u7bb1\u7684\u51fa\u7dda\u53e3\uff1a";
                PromptSelectionResult selectionResult = editor.GetSelection(selectionOptions);

                if (selectionResult.Status != PromptStatus.OK)
                {
                    editor.WriteMessage("\n\u5df2\u53d6\u6d88 QTO_ASSIGN_JB\u3002");
                    return;
                }

                List<OutletSelectionItem> outlets = new List<OutletSelectionItem>();
                int ignoredCount = 0;
                bool hasExistingJbId = false;

                using (Transaction transaction = database.TransactionManager.StartTransaction())
                {
                    foreach (SelectedObject selectedObject in selectionResult.Value)
                    {
                        if (selectedObject == null)
                        {
                            ignoredCount++;
                            continue;
                        }

                        BlockReference blockReference = transaction.GetObject(selectedObject.ObjectId, OpenMode.ForRead, false) as BlockReference;

                        if (blockReference == null || !QtoXDataHelper.HasQtoType(blockReference, QtoXDataHelper.TypeOutlet))
                        {
                            ignoredCount++;
                            continue;
                        }

                        string jbId = QtoXDataHelper.GetXDataValue(blockReference, QtoXDataHelper.KeyJbId) ?? string.Empty;

                        if (!string.IsNullOrWhiteSpace(jbId))
                        {
                            hasExistingJbId = true;
                        }

                        OutletSelectionItem item = new OutletSelectionItem();
                        item.ObjectId = selectedObject.ObjectId;
                        item.ExistingValue = jbId;
                        outlets.Add(item);
                    }

                    transaction.Commit();
                }

                if (outlets.Count == 0)
                {
                    editor.WriteMessage("\n\u6c92\u6709\u53ef\u6307\u5b9a\u7684 QTO \u51fa\u7dda\u53e3\u3002");
                    editor.WriteMessage("\n\u9078\u53d6\u51fa\u7dda\u53e3\u6578\u91cf\uff1a0");
                    editor.WriteMessage("\n\u6210\u529f\u6307\u5b9a\u6578\u91cf\uff1a0");
                    editor.WriteMessage("\n\u8df3\u904e\u6578\u91cf\uff1a" + ignoredCount);
                    return;
                }

                PromptEntityOptions jbOptions = new PromptEntityOptions("\n\u8acb\u9ede\u9078\u4e00\u500b\u5df2\u6a19\u8a18\u7684\u7d50\u7dda\u7bb1\uff1a");
                PromptEntityResult jbResult = editor.GetEntity(jbOptions);

                if (jbResult.Status != PromptStatus.OK)
                {
                    editor.WriteMessage("\n\u5df2\u53d6\u6d88 QTO_ASSIGN_JB\u3002");
                    return;
                }

                string targetJbId = string.Empty;

                using (Transaction transaction = database.TransactionManager.StartTransaction())
                {
                    BlockReference junctionBox = transaction.GetObject(jbResult.ObjectId, OpenMode.ForRead, false) as BlockReference;

                    if (junctionBox == null || !QtoXDataHelper.HasQtoType(junctionBox, QtoXDataHelper.TypeJunctionBox))
                    {
                        editor.WriteMessage("\n\u9078\u53d6\u7684\u7269\u4ef6\u4e0d\u662f QTO \u7d50\u7dda\u7bb1\u3002");
                        return;
                    }

                    targetJbId = QtoXDataHelper.GetXDataValue(junctionBox, QtoXDataHelper.KeyJbId) ?? string.Empty;

                    transaction.Commit();
                }

                if (string.IsNullOrWhiteSpace(targetJbId))
                {
                    editor.WriteMessage("\n\u7d50\u7dda\u7bb1\u6c92\u6709 JB_ID\uff0c\u8acb\u5148\u57f7\u884c QTO_MARK_JB\u3002");
                    return;
                }

                bool overwriteExisting = true;

                if (hasExistingJbId)
                {
                    overwriteExisting = PromptYesNo(editor, "\n\u90e8\u5206\u51fa\u7dda\u53e3\u5df2\u6709 JB_ID\uff0c\u662f\u5426\u8986\u84cb\uff1f", false);
                }

                int assignedCount = 0;
                int skippedCount = ignoredCount;

                using (Transaction transaction = database.TransactionManager.StartTransaction())
                {
                    foreach (OutletSelectionItem item in outlets)
                    {
                        if (!string.IsNullOrWhiteSpace(item.ExistingValue) && !overwriteExisting)
                        {
                            skippedCount++;
                            continue;
                        }

                        BlockReference blockReference = transaction.GetObject(item.ObjectId, OpenMode.ForWrite, false) as BlockReference;

                        if (blockReference == null || !QtoXDataHelper.HasQtoType(blockReference, QtoXDataHelper.TypeOutlet))
                        {
                            skippedCount++;
                            continue;
                        }

                        QtoXDataHelper.UpdateXDataValue(blockReference, database, transaction, QtoXDataHelper.KeyJbId, targetJbId);
                        assignedCount++;
                    }

                    transaction.Commit();
                }

                editor.WriteMessage("\n\u9078\u53d6\u51fa\u7dda\u53e3\u6578\u91cf\uff1a" + outlets.Count);
                editor.WriteMessage("\n\u6210\u529f\u6307\u5b9a\u6578\u91cf\uff1a" + assignedCount);
                editor.WriteMessage("\n\u8df3\u904e\u6578\u91cf\uff1a" + skippedCount);
                editor.WriteMessage("\n\u4f7f\u7528\u7684 JB_ID\uff1a" + targetJbId);
                ShowStepMessage(
                    "\u51fa\u7dda\u53e3\u6307\u5b9a\u7d50\u7dda\u7bb1\u5b8c\u6210",
                    "\u5df2\u5c07 " + assignedCount + " \u500b\u51fa\u7dda\u53e3\u6307\u5b9a\u5230 JB_ID\uff1a" + targetJbId + "\u3002\n\u8df3\u904e " + skippedCount + " \u500b\u7269\u4ef6\u3002",
                    "\u4e0b\u4e00\u6b65\u5efa\u8b70\uff1a\u624b\u52d5\u756b Polyline\uff0c\u7136\u5f8c\u57f7\u884c QTO_BIND_WIRE \u7d81\u5b9a\u914d\u7dda\u3002");
            }
            catch (System.Exception ex)
            {
                WriteCommandError("QTO_ASSIGN_JB", ex);
            }
        }

        [CommandMethod("QTO_BIND_WIRE")]
        public void QtoBindWire()
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

                PromptEntityOptions outletOptions = new PromptEntityOptions("\n\u8acb\u9ede\u9078\u4e00\u500b\u5df2\u6a19\u8a18\u7684\u51fa\u7dda\u53e3\uff1a");
                PromptEntityResult outletResult = editor.GetEntity(outletOptions);

                if (outletResult.Status != PromptStatus.OK)
                {
                    editor.WriteMessage("\n\u5df2\u53d6\u6d88 QTO_BIND_WIRE\u3002");
                    return;
                }

                string outletId = string.Empty;
                string jbId = string.Empty;
                string system = string.Empty;
                string cableType = string.Empty;

                using (Transaction transaction = database.TransactionManager.StartTransaction())
                {
                    BlockReference outlet = transaction.GetObject(outletResult.ObjectId, OpenMode.ForRead, false) as BlockReference;

                    if (outlet == null || !QtoXDataHelper.HasQtoType(outlet, QtoXDataHelper.TypeOutlet))
                    {
                        editor.WriteMessage("\n\u9078\u53d6\u7684\u7269\u4ef6\u4e0d\u662f QTO \u51fa\u7dda\u53e3\u3002");
                        return;
                    }

                    outletId = QtoXDataHelper.GetXDataValue(outlet, QtoXDataHelper.KeyOutletId) ?? string.Empty;
                    jbId = QtoXDataHelper.GetXDataValue(outlet, QtoXDataHelper.KeyJbId) ?? string.Empty;
                    system = QtoXDataHelper.GetXDataValue(outlet, QtoXDataHelper.KeySystem) ?? string.Empty;
                    cableType = QtoXDataHelper.GetXDataValue(outlet, QtoXDataHelper.KeyCableType) ?? string.Empty;

                    transaction.Commit();
                }

                if (string.IsNullOrWhiteSpace(outletId))
                {
                    editor.WriteMessage("\n\u51fa\u7dda\u53e3\u6c92\u6709 OUTLET_ID\uff0c\u8acb\u5148\u57f7\u884c QTO_NUMBER_OUTLETS\u3002");
                    return;
                }

                if (string.IsNullOrWhiteSpace(jbId))
                {
                    editor.WriteMessage("\n\u51fa\u7dda\u53e3\u6c92\u6709 JB_ID\uff0c\u8acb\u5148\u57f7\u884c QTO_ASSIGN_JB\u3002");
                    return;
                }

                PromptEntityOptions wireOptions = new PromptEntityOptions("\n\u8acb\u9ede\u9078\u4e00\u689d\u65e2\u6709 Polyline \u4f5c\u70ba\u914d\u7dda\uff1a");
                PromptEntityResult wireResult = editor.GetEntity(wireOptions);

                if (wireResult.Status != PromptStatus.OK)
                {
                    editor.WriteMessage("\n\u5df2\u53d6\u6d88 QTO_BIND_WIRE\u3002");
                    return;
                }

                double lengthM = 0.0;

                using (Transaction transaction = database.TransactionManager.StartTransaction())
                {
                    Polyline polyline = transaction.GetObject(wireResult.ObjectId, OpenMode.ForWrite, false) as Polyline;

                    if (polyline == null)
                    {
                        editor.WriteMessage("\n\u9078\u53d6\u7684\u7269\u4ef6\u4e0d\u662f Polyline\u3002");
                        return;
                    }

                    QtoLayerHelper.MoveEntityToLayer(polyline, database, transaction, QtoLayerHelper.WireLayerName);

                    Dictionary<string, string> data = new Dictionary<string, string>();
                    data[QtoXDataHelper.KeyQtoType] = QtoXDataHelper.TypeWire;
                    data[QtoXDataHelper.KeyOutletId] = outletId;
                    data[QtoXDataHelper.KeyJbId] = jbId;
                    data[QtoXDataHelper.KeySystem] = system;
                    data[QtoXDataHelper.KeyCableType] = cableType;

                    QtoXDataHelper.SetXData(polyline, database, transaction, data);

                    double lengthMm = QtoGeometryHelper.GetPolylineLength(polyline);
                    lengthM = QtoGeometryHelper.ConvertMmToM(lengthMm);

                    transaction.Commit();
                }

                editor.WriteMessage("\nOUTLET_ID = " + outletId);
                editor.WriteMessage("\nJB_ID = " + jbId);
                editor.WriteMessage("\nCABLE_TYPE = " + cableType);
                editor.WriteMessage("\nLENGTH_M = " + lengthM.ToString("0.000"));
                ShowStepMessage(
                    "\u914d\u7dda\u7d81\u5b9a\u5b8c\u6210",
                    "\u5df2\u5c07\u9078\u53d6\u7684 Polyline \u79fb\u5230 QTO_WIRE \u5716\u5c64\u4e26\u5beb\u5165 WIRE XData\u3002\nOUTLET_ID\uff1a" + outletId + "\nJB_ID\uff1a" + jbId + "\nLENGTH_M\uff1a" + lengthM.ToString("0.000"),
                    "\u4e0b\u4e00\u6b65\u5efa\u8b70\uff1a\u7e7c\u7e8c\u7d81\u5b9a\u5176\u4ed6\u914d\u7dda\uff0c\u6216\u57f7\u884c QTO_CHECK_WIRE \u6aa2\u67e5\u95dc\u806f\u3002");
            }
            catch (System.Exception ex)
            {
                WriteCommandError("QTO_BIND_WIRE", ex);
            }
        }

        [CommandMethod("QTO_BATCH_BIND_WIRE")]
        public void QtoBatchBindWire()
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

                PromptSelectionOptions outletSelectionOptions = new PromptSelectionOptions();
                outletSelectionOptions.MessageForAdding = "\n\u8acb\u6846\u9078\u8981\u6279\u6b21\u7d81\u5b9a\u914d\u7dda\u7684\u51fa\u7dda\u53e3\uff1a";
                PromptSelectionResult outletSelectionResult = editor.GetSelection(outletSelectionOptions);

                if (outletSelectionResult.Status != PromptStatus.OK)
                {
                    editor.WriteMessage("\n\u5df2\u53d6\u6d88 QTO_BATCH_BIND_WIRE\u3002");
                    return;
                }

                PromptSelectionOptions wireSelectionOptions = new PromptSelectionOptions();
                wireSelectionOptions.MessageForAdding = "\n\u8acb\u6846\u9078\u8981\u6279\u6b21\u7d81\u5b9a\u7684 Polyline \u914d\u7dda\uff1a";
                PromptSelectionResult wireSelectionResult = editor.GetSelection(wireSelectionOptions);

                if (wireSelectionResult.Status != PromptStatus.OK)
                {
                    editor.WriteMessage("\n\u5df2\u53d6\u6d88 QTO_BATCH_BIND_WIRE\u3002");
                    return;
                }

                double tolerance = PromptDoubleWithDefault(editor, "\n\u8f38\u5165\u5bb9\u8a31\u8ddd\u96e2\uff08mm\uff09", 300.0);
                List<BatchOutletItem> outlets = new List<BatchOutletItem>();
                List<BatchWireItem> wires = new List<BatchWireItem>();
                List<BatchBindResult> reportRows = new List<BatchBindResult>();

                using (Transaction transaction = database.TransactionManager.StartTransaction())
                {
                    foreach (SelectedObject selectedObject in outletSelectionResult.Value)
                    {
                        if (selectedObject == null)
                        {
                            continue;
                        }

                        BlockReference outlet = transaction.GetObject(selectedObject.ObjectId, OpenMode.ForRead, false) as BlockReference;

                        if (outlet == null || !QtoXDataHelper.HasQtoType(outlet, QtoXDataHelper.TypeOutlet))
                        {
                            continue;
                        }

                        BatchOutletItem item = new BatchOutletItem();
                        item.ObjectId = outlet.ObjectId;
                        item.Position = outlet.Position;
                        item.OutletId = QtoXDataHelper.GetXDataValue(outlet, QtoXDataHelper.KeyOutletId) ?? string.Empty;
                        item.JbId = QtoXDataHelper.GetXDataValue(outlet, QtoXDataHelper.KeyJbId) ?? string.Empty;
                        item.System = QtoXDataHelper.GetXDataValue(outlet, QtoXDataHelper.KeySystem) ?? string.Empty;
                        item.CableType = QtoXDataHelper.GetXDataValue(outlet, QtoXDataHelper.KeyCableType) ?? string.Empty;
                        outlets.Add(item);
                    }

                    foreach (SelectedObject selectedObject in wireSelectionResult.Value)
                    {
                        if (selectedObject == null)
                        {
                            continue;
                        }

                        Polyline polyline = transaction.GetObject(selectedObject.ObjectId, OpenMode.ForRead, false) as Polyline;

                        if (polyline == null || polyline.NumberOfVertices < 2)
                        {
                            continue;
                        }

                        BatchWireItem item = new BatchWireItem();
                        item.ObjectId = polyline.ObjectId;
                        item.StartPoint = polyline.GetPoint3dAt(0);
                        item.EndPoint = polyline.GetPoint3dAt(polyline.NumberOfVertices - 1);
                        wires.Add(item);
                    }

                    transaction.Commit();
                }

                List<BatchMatchCandidate> candidates = new List<BatchMatchCandidate>();

                for (int outletIndex = 0; outletIndex < outlets.Count; outletIndex++)
                {
                    BatchOutletItem outlet = outlets[outletIndex];

                    if (string.IsNullOrWhiteSpace(outlet.OutletId) || string.IsNullOrWhiteSpace(outlet.JbId))
                    {
                        BatchBindResult row = new BatchBindResult();
                        row.OutletId = outlet.OutletId;
                        row.JbId = outlet.JbId;
                        row.MatchedWireObjectId = string.Empty;
                        row.Distance = 0.0;
                        row.Status = "ERROR";
                        row.ErrorMessage = BuildBatchOutletErrorMessage(outlet);
                        reportRows.Add(row);
                        outlet.Reported = true;
                        continue;
                    }

                    for (int wireIndex = 0; wireIndex < wires.Count; wireIndex++)
                    {
                        BatchWireItem wire = wires[wireIndex];
                        double distance = GetOutletToWireEndpointDistance(outlet.Position, wire);

                        if (distance <= tolerance)
                        {
                            BatchMatchCandidate candidate = new BatchMatchCandidate();
                            candidate.OutletIndex = outletIndex;
                            candidate.WireIndex = wireIndex;
                            candidate.Distance = distance;
                            candidates.Add(candidate);
                        }
                    }
                }

                candidates.Sort(CompareBatchMatchCandidates);

                foreach (BatchMatchCandidate candidate in candidates)
                {
                    BatchOutletItem outlet = outlets[candidate.OutletIndex];
                    BatchWireItem wire = wires[candidate.WireIndex];

                    if (outlet.IsMatched || outlet.Reported || wire.IsMatched)
                    {
                        continue;
                    }

                    outlet.IsMatched = true;
                    outlet.MatchedWireObjectId = wire.ObjectId;
                    outlet.MatchDistance = candidate.Distance;
                    wire.IsMatched = true;
                    wire.MatchedOutletObjectId = outlet.ObjectId;
                }

                int successCount = 0;

                using (Transaction transaction = database.TransactionManager.StartTransaction())
                {
                    foreach (BatchOutletItem outlet in outlets)
                    {
                        if (!outlet.IsMatched)
                        {
                            continue;
                        }

                        Polyline polyline = transaction.GetObject(outlet.MatchedWireObjectId, OpenMode.ForWrite, false) as Polyline;

                        if (polyline == null)
                        {
                            continue;
                        }

                        QtoLayerHelper.MoveEntityToLayer(polyline, database, transaction, QtoLayerHelper.WireLayerName);

                        Dictionary<string, string> data = new Dictionary<string, string>();
                        data[QtoXDataHelper.KeyQtoType] = QtoXDataHelper.TypeWire;
                        data[QtoXDataHelper.KeyOutletId] = outlet.OutletId;
                        data[QtoXDataHelper.KeyJbId] = outlet.JbId;
                        data[QtoXDataHelper.KeySystem] = outlet.System;
                        data[QtoXDataHelper.KeyCableType] = outlet.CableType;

                        QtoXDataHelper.SetXData(polyline, database, transaction, data);
                        successCount++;

                        BatchBindResult row = new BatchBindResult();
                        row.OutletId = outlet.OutletId;
                        row.JbId = outlet.JbId;
                        row.MatchedWireObjectId = outlet.MatchedWireObjectId.ToString();
                        row.Distance = outlet.MatchDistance;
                        row.Status = "OK";
                        row.ErrorMessage = string.Empty;
                        reportRows.Add(row);
                        outlet.Reported = true;
                    }

                    transaction.Commit();
                }

                int unmatchedOutletCount = 0;

                foreach (BatchOutletItem outlet in outlets)
                {
                    if (outlet.Reported)
                    {
                        if (!outlet.IsMatched)
                        {
                            unmatchedOutletCount++;
                        }

                        continue;
                    }

                    unmatchedOutletCount++;

                    BatchBindResult row = new BatchBindResult();
                    row.OutletId = outlet.OutletId;
                    row.JbId = outlet.JbId;
                    row.MatchedWireObjectId = string.Empty;
                    row.Distance = 0.0;
                    row.Status = "ERROR";
                    row.ErrorMessage = "No Polyline endpoint within tolerance";
                    reportRows.Add(row);
                }

                int unusedPolylineCount = 0;

                foreach (BatchWireItem wire in wires)
                {
                    if (!wire.IsMatched)
                    {
                        unusedPolylineCount++;
                    }
                }

                string outputPath = PromptCsvOutputPath(database, "QTO_BATCH_BIND_WIRE_");

                if (!string.IsNullOrWhiteSpace(outputPath))
                {
                    outputPath = QtoCsvExporter.ExportBatchBindReport(reportRows, database, outputPath);
                }

                editor.WriteMessage("\n\u51fa\u7dda\u53e3\u6578\u91cf\uff1a" + outlets.Count);
                editor.WriteMessage("\nPolyline \u6578\u91cf\uff1a" + wires.Count);
                editor.WriteMessage("\n\u6210\u529f\u7d81\u5b9a\u6578\u91cf\uff1a" + successCount);
                editor.WriteMessage("\n\u672a\u914d\u5c0d\u51fa\u7dda\u53e3\u6578\u91cf\uff1a" + unmatchedOutletCount);
                editor.WriteMessage("\n\u672a\u4f7f\u7528 Polyline \u6578\u91cf\uff1a" + unusedPolylineCount);

                if (!string.IsNullOrWhiteSpace(outputPath))
                {
                    editor.WriteMessage("\nCSV \u8f38\u51fa\u8def\u5f91\uff1a" + outputPath);
                }

                ShowStepMessage(
                    "\u6279\u6b21\u7d81\u5b9a\u914d\u7dda\u5b8c\u6210",
                    "\u51fa\u7dda\u53e3\uff1a" + outlets.Count + "\nPolyline\uff1a" + wires.Count + "\n\u6210\u529f\u7d81\u5b9a\uff1a" + successCount + "\n\u672a\u914d\u5c0d\u51fa\u7dda\u53e3\uff1a" + unmatchedOutletCount + "\n\u672a\u4f7f\u7528 Polyline\uff1a" + unusedPolylineCount,
                    "\u4e0b\u4e00\u6b65\u5efa\u8b70\uff1a\u57f7\u884c QTO_CHECK_WIRE \u6aa2\u67e5\u914d\u7dda\u95dc\u806f\u3002");
            }
            catch (System.Exception ex)
            {
                WriteCommandError("QTO_BATCH_BIND_WIRE", ex);
            }
        }

        [CommandMethod("QTO_RECALC_WIRE")]
        public void QtoRecalcWire()
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
                ExportScopeSelection exportScope = PromptExportScope(editor, "配線重新統計");

                if (exportScope == null)
                {
                    editor.WriteMessage("\n已取消 CSV 匯出。");
                    return;
                }

                List<WireInfo> wires = null;

                using (Transaction transaction = database.TransactionManager.StartTransaction())
                {
                    wires = QtoScanner.GetAllQtoWires(transaction, database);
                    wires = FilterWiresByScope(wires, exportScope);
                    transaction.Commit();
                }

                int okCount = 0;
                int errorCount = 0;

                foreach (WireInfo wire in wires)
                {
                    string errorMessage = BuildWireRecalcErrorMessage(wire);

                    if (string.IsNullOrWhiteSpace(errorMessage))
                    {
                        okCount++;
                    }
                    else
                    {
                        errorCount++;
                    }
                }

                string outputPath = PromptCsvOutputPath(database, "QTO_WIRE_RECALC_");

                if (string.IsNullOrWhiteSpace(outputPath))
                {
                    editor.WriteMessage("\n\u5df2\u53d6\u6d88 CSV \u532f\u51fa\u3002");
                    return;
                }

                outputPath = QtoCsvExporter.ExportWireRecalc(wires, database, outputPath);

                editor.WriteMessage("\n\u6383\u63cf\u5230\u7684\u914d\u7dda\u6578\u91cf\uff1a" + wires.Count);
                editor.WriteMessage("\nOK \u6578\u91cf\uff1a" + okCount);
                editor.WriteMessage("\nERROR \u6578\u91cf\uff1a" + errorCount);
                editor.WriteMessage("\nCSV \u8f38\u51fa\u8def\u5f91\uff1a" + outputPath);
                ShowStepMessage(
                    "\u914d\u7dda\u91cd\u65b0\u7d71\u8a08\u5b8c\u6210",
                    "\u5df2\u6383\u63cf " + wires.Count + " \u689d\u914d\u7dda\u3002\nOK\uff1a" + okCount + "\nERROR\uff1a" + errorCount + "\nCSV\uff1a" + outputPath,
                    "\u4e0b\u4e00\u6b65\u5efa\u8b70\uff1a\u958b\u555f CSV \u6aa2\u67e5\u9577\u5ea6\uff0c\u6216\u4fee\u6539 Polyline \u5f8c\u518d\u57f7\u884c QTO_RECALC_WIRE\u3002");
            }
            catch (System.Exception ex)
            {
                WriteCommandError("QTO_RECALC_WIRE", ex);
            }
        }

        [CommandMethod("QTO_CHECK_WIRE")]
        public void QtoCheckWire()
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
                ExportScopeSelection exportScope = PromptExportScope(editor, "配線關係明細");

                if (exportScope == null)
                {
                    editor.WriteMessage("\n已取消 CSV 匯出。");
                    return;
                }

                List<OutletInfo> outlets = null;
                List<JunctionBoxInfo> junctionBoxes = null;
                List<WireInfo> wires = null;

                using (Transaction transaction = database.TransactionManager.StartTransaction())
                {
                    outlets = QtoScanner.GetAllQtoOutlets(transaction, database);
                    junctionBoxes = QtoScanner.GetAllQtoJunctionBoxes(transaction, database);
                    wires = QtoScanner.GetAllQtoWires(transaction, database);
                    outlets = FilterOutletsByScope(outlets, exportScope);
                    junctionBoxes = FilterJunctionBoxesByScope(junctionBoxes, exportScope);
                    wires = FilterWiresByScope(wires, exportScope);
                    transaction.Commit();
                }

                List<CheckResult> results = BuildWireCheckResults(outlets, junctionBoxes, wires);
                int okCount = CountStatus(results, "OK");
                int errorCount = CountStatus(results, "ERROR");
                string outputPath = PromptCsvOutputPath(database, "QTO_WIRE_CHECK_");

                if (string.IsNullOrWhiteSpace(outputPath))
                {
                    editor.WriteMessage("\n\u5df2\u53d6\u6d88 CSV \u532f\u51fa\u3002");
                    return;
                }

                outputPath = QtoCsvExporter.ExportWireCheck(results, database, outputPath);

                editor.WriteMessage("\n\u51fa\u7dda\u53e3\u7e3d\u6578\uff1a" + outlets.Count);
                editor.WriteMessage("\n\u7d50\u7dda\u7bb1\u7e3d\u6578\uff1a" + junctionBoxes.Count);
                editor.WriteMessage("\n\u914d\u7dda\u7e3d\u6578\uff1a" + wires.Count);
                editor.WriteMessage("\nOK \u6578\u91cf\uff1a" + okCount);
                editor.WriteMessage("\nERROR \u6578\u91cf\uff1a" + errorCount);
                editor.WriteMessage("\nCSV \u8f38\u51fa\u8def\u5f91\uff1a" + outputPath);
                ShowStepMessage(
                    "\u914d\u7dda\u6aa2\u67e5\u5b8c\u6210",
                    "\u51fa\u7dda\u53e3\uff1a" + outlets.Count + "\n\u7d50\u7dda\u7bb1\uff1a" + junctionBoxes.Count + "\n\u914d\u7dda\uff1a" + wires.Count + "\nOK\uff1a" + okCount + "\nERROR\uff1a" + errorCount + "\nCSV\uff1a" + outputPath,
                    "\u4e0b\u4e00\u6b65\u5efa\u8b70\uff1a\u82e5\u6709 ERROR\uff0c\u4f9d CSV \u4fee\u6b63\u5f8c\u518d\u57f7\u884c QTO_CHECK_WIRE\u3002");
            }
            catch (System.Exception ex)
            {
                WriteCommandError("QTO_CHECK_WIRE", ex);
            }
        }

        [CommandMethod("QTO_HIGHLIGHT_ERRORS")]
        public void QtoHighlightErrors()
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
                List<OutletInfo> outlets = null;
                List<JunctionBoxInfo> junctionBoxes = null;
                List<WireInfo> wires = null;

                using (Transaction transaction = database.TransactionManager.StartTransaction())
                {
                    outlets = QtoScanner.GetAllQtoOutlets(transaction, database);
                    junctionBoxes = QtoScanner.GetAllQtoJunctionBoxes(transaction, database);
                    wires = QtoScanner.GetAllQtoWires(transaction, database);
                    transaction.Commit();
                }

                List<CheckResult> allResults = BuildWireCheckResults(outlets, junctionBoxes, wires);
                List<CheckResult> errorResults = new List<CheckResult>();

                foreach (CheckResult result in allResults)
                {
                    if (string.Equals(result.Status, "ERROR", StringComparison.OrdinalIgnoreCase))
                    {
                        errorResults.Add(result);
                    }
                }

                string outputPath = PromptCsvOutputPath(database, "QTO_ERROR_HIGHLIGHT_");

                if (string.IsNullOrWhiteSpace(outputPath))
                {
                    editor.WriteMessage("\n\u5df2\u53d6\u6d88 CSV \u532f\u51fa\u3002");
                    return;
                }

                outputPath = QtoCsvExporter.ExportWireCheck(errorResults, database, outputPath);

                int highlightedCount = 0;

                using (Transaction transaction = database.TransactionManager.StartTransaction())
                {
                    QtoLayerHelper.EnsureLayerExists(database, transaction, ErrorOutletLayerName);
                    QtoLayerHelper.EnsureLayerExists(database, transaction, ErrorWireLayerName);
                    QtoLayerHelper.EnsureLayerExists(database, transaction, ErrorJbLayerName);

                    Dictionary<string, CheckResult> errorByObjectId = new Dictionary<string, CheckResult>(StringComparer.OrdinalIgnoreCase);

                    foreach (CheckResult result in errorResults)
                    {
                        if (string.IsNullOrWhiteSpace(result.ObjectId))
                        {
                            continue;
                        }

                        if (!errorByObjectId.ContainsKey(result.ObjectId))
                        {
                            errorByObjectId.Add(result.ObjectId, result);
                        }
                    }

                    BlockTableRecord modelSpace = GetModelSpace(transaction, database);

                    foreach (ObjectId objectId in modelSpace)
                    {
                        Entity entity = transaction.GetObject(objectId, OpenMode.ForWrite, false) as Entity;

                        if (entity == null)
                        {
                            continue;
                        }

                        string key = entity.ObjectId.ToString();

                        if (!errorByObjectId.ContainsKey(key))
                        {
                            continue;
                        }

                        CheckResult result = errorByObjectId[key];
                        string errorLayerName = GetErrorLayerName(result.ItemType);

                        if (string.IsNullOrWhiteSpace(errorLayerName))
                        {
                            continue;
                        }

                        string originalLayer = QtoXDataHelper.GetXDataValue(entity, QtoXDataHelper.KeyQtoOriginalLayer);

                        if (string.IsNullOrWhiteSpace(originalLayer))
                        {
                            QtoXDataHelper.UpdateXDataValue(entity, database, transaction, QtoXDataHelper.KeyQtoOriginalLayer, entity.Layer);
                        }

                        QtoLayerHelper.MoveEntityToLayer(entity, database, transaction, errorLayerName);
                        QtoXDataHelper.UpdateXDataValue(entity, database, transaction, QtoXDataHelper.KeyQtoError, "TRUE");
                        QtoXDataHelper.UpdateXDataValue(entity, database, transaction, QtoXDataHelper.KeyQtoErrorMessage, result.ErrorMessage);
                        highlightedCount++;
                    }

                    transaction.Commit();
                }

                editor.WriteMessage("\n\u932f\u8aa4\u7269\u4ef6\u6578\u91cf\uff1a" + highlightedCount);
                editor.WriteMessage("\nCSV \u8f38\u51fa\u8def\u5f91\uff1a" + outputPath);

                ShowStepMessage(
                    "\u932f\u8aa4\u8996\u89ba\u6a19\u793a\u5b8c\u6210",
                    "\u5df2\u5c07 " + highlightedCount + " \u500b\u932f\u8aa4\u7269\u4ef6\u79fb\u5230 QTO_ERROR \u5716\u5c64\u3002\nCSV\uff1a" + outputPath,
                    "\u4e0b\u4e00\u6b65\u5efa\u8b70\uff1a\u4fee\u6b63\u5716\u9762\u5f8c\u57f7\u884c QTO_CLEAR_ERROR_HIGHLIGHT \u6e05\u9664\u6a19\u793a\uff0c\u518d\u57f7\u884c QTO_CHECK_WIRE\u3002");
            }
            catch (System.Exception ex)
            {
                WriteCommandError("QTO_HIGHLIGHT_ERRORS", ex);
            }
        }

        [CommandMethod("QTO_CLEAR_ERROR_HIGHLIGHT")]
        public void QtoClearErrorHighlight()
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
                int clearedCount = 0;

                using (Transaction transaction = database.TransactionManager.StartTransaction())
                {
                    BlockTableRecord modelSpace = GetModelSpace(transaction, database);

                    foreach (ObjectId objectId in modelSpace)
                    {
                        Entity entity = transaction.GetObject(objectId, OpenMode.ForWrite, false) as Entity;

                        if (entity == null)
                        {
                            continue;
                        }

                        string isError = QtoXDataHelper.GetXDataValue(entity, QtoXDataHelper.KeyQtoError);

                        if (!string.Equals(isError, "TRUE", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        string qtoType = QtoXDataHelper.GetXDataValue(entity, QtoXDataHelper.KeyQtoType);
                        string originalLayer = QtoXDataHelper.GetXDataValue(entity, QtoXDataHelper.KeyQtoOriginalLayer);

                        if (string.Equals(qtoType, QtoXDataHelper.TypeWire, StringComparison.OrdinalIgnoreCase))
                        {
                            QtoLayerHelper.MoveEntityToLayer(entity, database, transaction, QtoLayerHelper.WireLayerName);
                        }
                        else if (!string.IsNullOrWhiteSpace(originalLayer) && LayerExists(database, transaction, originalLayer))
                        {
                            QtoLayerHelper.MoveEntityToLayer(entity, database, transaction, originalLayer);
                        }

                        QtoXDataHelper.RemoveXDataValues(
                            entity,
                            database,
                            transaction,
                            QtoXDataHelper.KeyQtoError,
                            QtoXDataHelper.KeyQtoErrorMessage,
                            QtoXDataHelper.KeyQtoOriginalLayer);

                        clearedCount++;
                    }

                    transaction.Commit();
                }

                editor.WriteMessage("\n\u6e05\u9664\u932f\u8aa4\u6a19\u793a\u6578\u91cf\uff1a" + clearedCount);

                ShowStepMessage(
                    "\u932f\u8aa4\u8996\u89ba\u6a19\u793a\u5df2\u6e05\u9664",
                    "\u5df2\u6e05\u9664 " + clearedCount + " \u500b\u7269\u4ef6\u7684 QTO_ERROR \u6a19\u8a18\u3002\nWIRE \u5df2\u79fb\u56de QTO_WIRE \u5716\u5c64\u3002",
                    "\u4e0b\u4e00\u6b65\u5efa\u8b70\uff1a\u57f7\u884c QTO_CHECK_WIRE \u78ba\u8a8d\u4fee\u6b63\u5f8c\u7684\u7d50\u679c\u3002");
            }
            catch (System.Exception ex)
            {
                WriteCommandError("QTO_CLEAR_ERROR_HIGHLIGHT", ex);
            }
        }

        [CommandMethod("QTO_QUERY_OUTLET")]
        public void QtoQueryOutlet()
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

                PromptEntityOptions options = new PromptEntityOptions("\n\u8acb\u9ede\u9078\u8981\u67e5\u8a62\u7684\u51fa\u7dda\u53e3\uff1a");
                PromptEntityResult result = editor.GetEntity(options);

                if (result.Status != PromptStatus.OK)
                {
                    editor.WriteMessage("\n\u5df2\u53d6\u6d88 QTO_QUERY_OUTLET\u3002");
                    return;
                }

                OutletInfo outletInfo = null;
                List<WireInfo> matchedWires = new List<WireInfo>();

                using (Transaction transaction = database.TransactionManager.StartTransaction())
                {
                    BlockReference outlet = transaction.GetObject(result.ObjectId, OpenMode.ForRead, false) as BlockReference;

                    if (outlet == null || !QtoXDataHelper.HasQtoType(outlet, QtoXDataHelper.TypeOutlet))
                    {
                        editor.WriteMessage("\n\u9078\u53d6\u7684\u7269\u4ef6\u4e0d\u662f QTO \u51fa\u7dda\u53e3\u3002");
                        return;
                    }

                    outletInfo = new OutletInfo();
                    outletInfo.ObjectId = outlet.ObjectId;
                    outletInfo.OutletId = QtoXDataHelper.GetXDataValue(outlet, QtoXDataHelper.KeyOutletId) ?? string.Empty;
                    outletInfo.JbId = QtoXDataHelper.GetXDataValue(outlet, QtoXDataHelper.KeyJbId) ?? string.Empty;
                    outletInfo.System = QtoXDataHelper.GetXDataValue(outlet, QtoXDataHelper.KeySystem) ?? string.Empty;
                    outletInfo.CableType = QtoXDataHelper.GetXDataValue(outlet, QtoXDataHelper.KeyCableType) ?? string.Empty;
                    outletInfo.BlockName = GetBlockName(transaction, outlet);
                    outletInfo.Position = outlet.Position;

                    List<WireInfo> wires = QtoScanner.GetAllQtoWires(transaction, database);

                    foreach (WireInfo wire in wires)
                    {
                        if (string.Equals(wire.OutletId, outletInfo.OutletId, StringComparison.OrdinalIgnoreCase))
                        {
                            matchedWires.Add(wire);
                        }
                    }

                    transaction.Commit();
                }

                editor.WriteMessage("\n========== QTO OUTLET QUERY ==========");
                editor.WriteMessage("\nOUTLET_ID   : " + outletInfo.OutletId);
                editor.WriteMessage("\nJB_ID       : " + outletInfo.JbId);
                editor.WriteMessage("\nSYSTEM      : " + outletInfo.System);
                editor.WriteMessage("\nCABLE_TYPE  : " + outletInfo.CableType);
                editor.WriteMessage("\nBLOCK_NAME  : " + outletInfo.BlockName);
                editor.WriteMessage("\nPOSITION    : X=" + outletInfo.Position.X.ToString("0.###") + ", Y=" + outletInfo.Position.Y.ToString("0.###") + ", Z=" + outletInfo.Position.Z.ToString("0.###"));
                editor.WriteMessage("\n---------- WIRE STATUS ----------");

                if (matchedWires.Count == 0)
                {
                    editor.WriteMessage("\nSTATUS      : Missing Wire");
                }
                else if (matchedWires.Count == 1)
                {
                    WireInfo wire = matchedWires[0];
                    editor.WriteMessage("\nSTATUS      : OK");
                    editor.WriteMessage("\nWIRE_OBJECT_ID : " + wire.ObjectId.ToString());
                    editor.WriteMessage("\nLENGTH_M       : " + wire.LengthM.ToString("0.000"));
                    editor.WriteMessage("\nLAYER          : " + wire.Layer);
                }
                else
                {
                    editor.WriteMessage("\nSTATUS      : Duplicate Wire");

                    foreach (WireInfo wire in matchedWires)
                    {
                        editor.WriteMessage("\nWIRE_OBJECT_ID : " + wire.ObjectId.ToString() + "  LENGTH_M=" + wire.LengthM.ToString("0.000") + "  LAYER=" + wire.Layer);
                    }
                }

                editor.WriteMessage("\n======================================");
            }
            catch (System.Exception ex)
            {
                WriteCommandError("QTO_QUERY_OUTLET", ex);
            }
        }

        [CommandMethod("QTO_QUERY_WIRE")]
        public void QtoQueryWire()
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

                PromptEntityOptions options = new PromptEntityOptions("\n\u8acb\u9ede\u9078\u8981\u67e5\u8a62\u7684\u914d\u7dda Polyline\uff1a");
                PromptEntityResult result = editor.GetEntity(options);

                if (result.Status != PromptStatus.OK)
                {
                    editor.WriteMessage("\n\u5df2\u53d6\u6d88 QTO_QUERY_WIRE\u3002");
                    return;
                }

                WireInfo wireInfo = null;
                bool outletExists = false;
                bool junctionBoxExists = false;

                using (Transaction transaction = database.TransactionManager.StartTransaction())
                {
                    Polyline polyline = transaction.GetObject(result.ObjectId, OpenMode.ForRead, false) as Polyline;

                    if (polyline == null || !QtoXDataHelper.HasQtoType(polyline, QtoXDataHelper.TypeWire))
                    {
                        editor.WriteMessage("\n\u9078\u53d6\u7684\u7269\u4ef6\u4e0d\u662f QTO \u914d\u7dda Polyline\u3002");
                        return;
                    }

                    double lengthMm = QtoGeometryHelper.GetPolylineLength(polyline);

                    wireInfo = new WireInfo();
                    wireInfo.ObjectId = polyline.ObjectId;
                    wireInfo.OutletId = QtoXDataHelper.GetXDataValue(polyline, QtoXDataHelper.KeyOutletId) ?? string.Empty;
                    wireInfo.JbId = QtoXDataHelper.GetXDataValue(polyline, QtoXDataHelper.KeyJbId) ?? string.Empty;
                    wireInfo.System = QtoXDataHelper.GetXDataValue(polyline, QtoXDataHelper.KeySystem) ?? string.Empty;
                    wireInfo.CableType = QtoXDataHelper.GetXDataValue(polyline, QtoXDataHelper.KeyCableType) ?? string.Empty;
                    wireInfo.LengthM = QtoGeometryHelper.ConvertMmToM(lengthMm);
                    wireInfo.Layer = polyline.Layer ?? string.Empty;

                    List<OutletInfo> outlets = QtoScanner.GetAllQtoOutlets(transaction, database);
                    List<JunctionBoxInfo> junctionBoxes = QtoScanner.GetAllQtoJunctionBoxes(transaction, database);

                    foreach (OutletInfo outlet in outlets)
                    {
                        if (string.Equals(outlet.OutletId, wireInfo.OutletId, StringComparison.OrdinalIgnoreCase))
                        {
                            outletExists = true;
                            break;
                        }
                    }

                    foreach (JunctionBoxInfo junctionBox in junctionBoxes)
                    {
                        if (string.Equals(junctionBox.JbId, wireInfo.JbId, StringComparison.OrdinalIgnoreCase))
                        {
                            junctionBoxExists = true;
                            break;
                        }
                    }

                    transaction.Commit();
                }

                editor.WriteMessage("\n========== QTO WIRE QUERY ==========");
                editor.WriteMessage("\nWIRE_OBJECT_ID : " + wireInfo.ObjectId.ToString());
                editor.WriteMessage("\nOUTLET_ID      : " + wireInfo.OutletId);
                editor.WriteMessage("\nJB_ID          : " + wireInfo.JbId);
                editor.WriteMessage("\nSYSTEM         : " + wireInfo.System);
                editor.WriteMessage("\nCABLE_TYPE     : " + wireInfo.CableType);
                editor.WriteMessage("\nLENGTH_M       : " + wireInfo.LengthM.ToString("0.000"));
                editor.WriteMessage("\nLAYER          : " + wireInfo.Layer);
                editor.WriteMessage("\n---------- RELATION STATUS ----------");
                editor.WriteMessage("\nOUTLET_EXISTS  : " + (outletExists ? "YES" : "NO"));
                editor.WriteMessage("\nJB_EXISTS      : " + (junctionBoxExists ? "YES" : "NO"));
                editor.WriteMessage("\n====================================");
            }
            catch (System.Exception ex)
            {
                WriteCommandError("QTO_QUERY_WIRE", ex);
            }
        }

        [CommandMethod("QTO_UNBIND_WIRE")]
        public void QtoUnbindWire()
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

                PromptEntityOptions options = new PromptEntityOptions("\n\u8acb\u9ede\u9078\u8981\u53d6\u6d88\u7d81\u5b9a\u7684\u914d\u7dda Polyline\uff1a");
                PromptEntityResult result = editor.GetEntity(options);

                if (result.Status != PromptStatus.OK)
                {
                    editor.WriteMessage("\n\u5df2\u53d6\u6d88 QTO_UNBIND_WIRE\u3002");
                    return;
                }

                bool moveOutOfWireLayer = PromptYesNo(editor, "\n\u662f\u5426\u5c07\u8a72 Polyline \u79fb\u51fa QTO_WIRE \u5716\u5c64\uff1f [Yes/No]", true);
                string oldOutletId = string.Empty;
                string oldJbId = string.Empty;
                string oldSystem = string.Empty;
                string oldCableType = string.Empty;
                string oldLayer = string.Empty;
                string newLayer = string.Empty;

                using (Transaction transaction = database.TransactionManager.StartTransaction())
                {
                    Polyline polyline = transaction.GetObject(result.ObjectId, OpenMode.ForWrite, false) as Polyline;

                    if (polyline == null || !QtoXDataHelper.HasQtoType(polyline, QtoXDataHelper.TypeWire))
                    {
                        editor.WriteMessage("\n\u9078\u53d6\u7684\u7269\u4ef6\u4e0d\u662f QTO \u914d\u7dda Polyline\u3002");
                        return;
                    }

                    oldOutletId = QtoXDataHelper.GetXDataValue(polyline, QtoXDataHelper.KeyOutletId) ?? string.Empty;
                    oldJbId = QtoXDataHelper.GetXDataValue(polyline, QtoXDataHelper.KeyJbId) ?? string.Empty;
                    oldSystem = QtoXDataHelper.GetXDataValue(polyline, QtoXDataHelper.KeySystem) ?? string.Empty;
                    oldCableType = QtoXDataHelper.GetXDataValue(polyline, QtoXDataHelper.KeyCableType) ?? string.Empty;
                    oldLayer = polyline.Layer ?? string.Empty;

                    QtoXDataHelper.RemoveXDataValues(
                        polyline,
                        database,
                        transaction,
                        QtoXDataHelper.KeyQtoType,
                        QtoXDataHelper.KeyOutletId,
                        QtoXDataHelper.KeyJbId,
                        QtoXDataHelper.KeySystem,
                        QtoXDataHelper.KeyCableType);

                    if (moveOutOfWireLayer && string.Equals(polyline.Layer, QtoLayerHelper.WireLayerName, StringComparison.OrdinalIgnoreCase))
                    {
                        QtoLayerHelper.MoveEntityToLayer(polyline, database, transaction, "0");
                    }

                    newLayer = polyline.Layer ?? string.Empty;
                    transaction.Commit();
                }

                editor.WriteMessage("\n========== QTO UNBIND WIRE ==========");
                editor.WriteMessage("\n\u5df2\u53d6\u6d88\u914d\u7dda\u7d81\u5b9a\u3002");
                editor.WriteMessage("\nOLD_OUTLET_ID  : " + oldOutletId);
                editor.WriteMessage("\nOLD_JB_ID      : " + oldJbId);
                editor.WriteMessage("\nOLD_SYSTEM     : " + oldSystem);
                editor.WriteMessage("\nOLD_CABLE_TYPE : " + oldCableType);
                editor.WriteMessage("\nOLD_LAYER      : " + oldLayer);
                editor.WriteMessage("\nNEW_LAYER      : " + newLayer);
                editor.WriteMessage("\n=====================================");
                ShowStepMessage(
                    "\u914d\u7dda\u5df2\u53d6\u6d88\u7d81\u5b9a",
                    "\u5df2\u6e05\u9664\u8a72 Polyline \u7684 QTO \u914d\u7dda\u8cc7\u6599\u3002",
                    "\u4e0b\u4e00\u6b65\u5efa\u8b70\uff1a\u82e5\u8981\u91cd\u65b0\u7d81\u5b9a\uff0c\u53ef\u57f7\u884c QTO_BIND_WIRE \u6216 QTO_REASSIGN_WIRE\u3002");
            }
            catch (System.Exception ex)
            {
                WriteCommandError("QTO_UNBIND_WIRE", ex);
            }
        }

        [CommandMethod("QTO_DELETE_OUTLET_INFO")]
        public void QtoDeleteOutletInfo()
        {
            try
            {
                if (DateTime.Now.Ticks >= 0)
                {
                    RunBatchDeleteQtoInfo(
                        QtoXDataHelper.TypeOutlet,
                        "刪除出線口資訊",
                        "\n請框選要刪除出線口資訊的圖塊：",
                        "共選到 {0} 個已標記出線口。\n確定要清除這些圖塊的出線口 QTO 資訊嗎？",
                        "已刪除出線口資訊");
                    return;
                }

                Document document = Application.DocumentManager.MdiActiveDocument;

                if (document == null)
                {
                    return;
                }

                Editor editor = document.Editor;
                Database database = document.Database;
                PromptEntityOptions options = new PromptEntityOptions("\n請點選要刪除出線口資訊的圖塊：");
                PromptEntityResult result = editor.GetEntity(options);

                if (result.Status != PromptStatus.OK)
                {
                    editor.WriteMessage("\n已取消刪除出線口資訊。");
                    return;
                }

                using (Transaction transaction = database.TransactionManager.StartTransaction())
                {
                    BlockReference outlet = transaction.GetObject(result.ObjectId, OpenMode.ForWrite, false) as BlockReference;

                    if (outlet == null || !QtoXDataHelper.HasQtoType(outlet, QtoXDataHelper.TypeOutlet))
                    {
                        editor.WriteMessage("\n選取的物件不是 QTO 出線口。");
                        return;
                    }

                    QtoXDataHelper.RemoveXDataValues(
                        outlet,
                        database,
                        transaction,
                        QtoXDataHelper.KeyQtoType,
                        QtoXDataHelper.KeyOutletId,
                        QtoXDataHelper.KeyJbId,
                        QtoXDataHelper.KeySystem,
                        QtoXDataHelper.KeyCableType,
                        QtoXDataHelper.KeyQtoError,
                        QtoXDataHelper.KeyQtoErrorMessage);

                    transaction.Commit();
                }

                editor.WriteMessage("\n已刪除出線口 QTO 資訊。");
                ShowStepMessage("出線口資訊已刪除", "已清除該圖塊上的出線口 QTO 資料，圖塊本身未刪除。", "下一步建議：需要重新使用時，可再執行「標記出線口」。");
            }
            catch (System.Exception ex)
            {
                WriteCommandError("QTO_DELETE_OUTLET_INFO", ex);
            }
        }

        [CommandMethod("QTO_DELETE_JB_INFO")]
        public void QtoDeleteJbInfo()
        {
            try
            {
                if (DateTime.Now.Ticks >= 0)
                {
                    RunBatchDeleteQtoInfo(
                        QtoXDataHelper.TypeJunctionBox,
                        "刪除箱體資訊",
                        "\n請框選要刪除箱體資訊的圖塊：",
                        "共選到 {0} 個已標記箱體。\n確定要清除這些圖塊的箱體 QTO 資訊嗎？",
                        "已刪除箱體資訊");
                    return;
                }

                Document document = Application.DocumentManager.MdiActiveDocument;

                if (document == null)
                {
                    return;
                }

                Editor editor = document.Editor;
                Database database = document.Database;
                PromptEntityOptions options = new PromptEntityOptions("\n請點選要刪除箱體資訊的圖塊：");
                PromptEntityResult result = editor.GetEntity(options);

                if (result.Status != PromptStatus.OK)
                {
                    editor.WriteMessage("\n已取消刪除箱體資訊。");
                    return;
                }

                using (Transaction transaction = database.TransactionManager.StartTransaction())
                {
                    BlockReference junctionBox = transaction.GetObject(result.ObjectId, OpenMode.ForWrite, false) as BlockReference;

                    if (junctionBox == null || !QtoXDataHelper.HasQtoType(junctionBox, QtoXDataHelper.TypeJunctionBox))
                    {
                        editor.WriteMessage("\n選取的物件不是 QTO 箱體。");
                        return;
                    }

                    QtoXDataHelper.RemoveXDataValues(
                        junctionBox,
                        database,
                        transaction,
                        QtoXDataHelper.KeyQtoType,
                        QtoXDataHelper.KeyJbId,
                        QtoXDataHelper.KeySystem,
                        QtoXDataHelper.KeyQtoError,
                        QtoXDataHelper.KeyQtoErrorMessage);

                    transaction.Commit();
                }

                editor.WriteMessage("\n已刪除箱體 QTO 資訊。");
                ShowStepMessage("箱體資訊已刪除", "已清除該圖塊上的箱體 QTO 資料，圖塊本身未刪除。", "下一步建議：需要重新使用時，可再執行「標記箱體」。");
            }
            catch (System.Exception ex)
            {
                WriteCommandError("QTO_DELETE_JB_INFO", ex);
            }
        }

        [CommandMethod("QTO_EDIT_OUTLET_PROPERTIES")]
        public void QtoEditOutletProperties()
        {
            try
            {
                RunBatchEditQtoProperties(
                    QtoXDataHelper.TypeOutlet,
                    "編輯出線口屬性",
                    "\n請框選要編輯屬性的出線口圖塊：",
                    true);
            }
            catch (System.Exception ex)
            {
                WriteCommandError("QTO_EDIT_OUTLET_PROPERTIES", ex);
            }
        }

        [CommandMethod("QTO_EDIT_JB_PROPERTIES")]
        public void QtoEditJbProperties()
        {
            try
            {
                RunBatchEditQtoProperties(
                    QtoXDataHelper.TypeJunctionBox,
                    "編輯箱體屬性",
                    "\n請框選要編輯屬性的箱體圖塊：",
                    false);
            }
            catch (System.Exception ex)
            {
                WriteCommandError("QTO_EDIT_JB_PROPERTIES", ex);
            }
        }

        private static void RunBatchDeleteQtoInfo(string qtoType, string title, string selectionMessage, string confirmMessageFormat, string completedTitle)
        {
            Document document = Application.DocumentManager.MdiActiveDocument;

            if (document == null)
            {
                return;
            }

            Editor editor = document.Editor;
            Database database = document.Database;
            PromptSelectionOptions selectionOptions = new PromptSelectionOptions();
            selectionOptions.MessageForAdding = selectionMessage;
            PromptSelectionResult selectionResult = editor.GetSelection(selectionOptions);

            if (selectionResult.Status != PromptStatus.OK)
            {
                editor.WriteMessage("\n已取消" + title + "。");
                return;
            }

            List<ObjectId> targetIds = new List<ObjectId>();
            int ignoredCount = 0;

            using (Transaction transaction = database.TransactionManager.StartTransaction())
            {
                foreach (SelectedObject selectedObject in selectionResult.Value)
                {
                    if (selectedObject == null)
                    {
                        ignoredCount++;
                        continue;
                    }

                    BlockReference blockReference = transaction.GetObject(selectedObject.ObjectId, OpenMode.ForRead, false) as BlockReference;

                    if (blockReference != null && QtoXDataHelper.HasQtoType(blockReference, qtoType))
                    {
                        targetIds.Add(selectedObject.ObjectId);
                    }
                    else
                    {
                        ignoredCount++;
                    }
                }

                transaction.Commit();
            }

            if (targetIds.Count == 0)
            {
                editor.WriteMessage("\n沒有找到可刪除的 QTO 資訊。");
                ShowStepMessage(title, "沒有找到符合條件的已標記圖塊。", "請確認框選範圍內包含已標記的出線口或箱體。");
                return;
            }

            System.Windows.Forms.DialogResult confirmResult = System.Windows.Forms.MessageBox.Show(
                string.Format(confirmMessageFormat, targetIds.Count) + "\n\n非對應物件會略過：" + ignoredCount + " 個",
                title,
                System.Windows.Forms.MessageBoxButtons.YesNo,
                System.Windows.Forms.MessageBoxIcon.Warning);

            if (confirmResult != System.Windows.Forms.DialogResult.Yes)
            {
                editor.WriteMessage("\n已取消" + title + "。");
                return;
            }

            int deletedCount = 0;

            using (Transaction transaction = database.TransactionManager.StartTransaction())
            {
                foreach (ObjectId targetId in targetIds)
                {
                    BlockReference blockReference = transaction.GetObject(targetId, OpenMode.ForWrite, false) as BlockReference;

                    if (blockReference == null || !QtoXDataHelper.HasQtoType(blockReference, qtoType))
                    {
                        ignoredCount++;
                        continue;
                    }

                    if (qtoType == QtoXDataHelper.TypeOutlet)
                    {
                        QtoXDataHelper.RemoveXDataValues(
                            blockReference,
                            database,
                            transaction,
                            QtoXDataHelper.KeyQtoType,
                            QtoXDataHelper.KeyOutletId,
                            QtoXDataHelper.KeyJbId,
                            QtoXDataHelper.KeySystem,
                            QtoXDataHelper.KeyCableType,
                            QtoXDataHelper.KeyQtoError,
                            QtoXDataHelper.KeyQtoErrorMessage);
                    }
                    else
                    {
                        QtoXDataHelper.RemoveXDataValues(
                            blockReference,
                            database,
                            transaction,
                            QtoXDataHelper.KeyQtoType,
                            QtoXDataHelper.KeyJbId,
                            QtoXDataHelper.KeySystem,
                            QtoXDataHelper.KeyQtoError,
                            QtoXDataHelper.KeyQtoErrorMessage);
                    }

                    deletedCount++;
                }

                transaction.Commit();
            }

            editor.WriteMessage("\n" + completedTitle + "完成。");
            editor.WriteMessage("\n清除數量：" + deletedCount);
            editor.WriteMessage("\n略過數量：" + ignoredCount);
            ShowStepMessage(
                completedTitle,
                "清除數量：" + deletedCount + "\n略過數量：" + ignoredCount,
                "下一步可以重新標記出線口或箱體。");
        }

        private static void RunBatchEditQtoProperties(string qtoType, string title, string selectionMessage, bool editOutlet)
        {
            Document document = Application.DocumentManager.MdiActiveDocument;

            if (document == null)
            {
                return;
            }

            Editor editor = document.Editor;
            Database database = document.Database;
            PromptSelectionOptions selectionOptions = new PromptSelectionOptions();
            selectionOptions.MessageForAdding = selectionMessage;
            PromptSelectionResult selectionResult = editor.GetSelection(selectionOptions);

            if (selectionResult.Status != PromptStatus.OK)
            {
                editor.WriteMessage("\n已取消" + title + "。");
                return;
            }

            List<ObjectId> targetIds = new List<ObjectId>();
            int ignoredCount = 0;
            Dictionary<string, string> defaults;

            using (Transaction transaction = database.TransactionManager.StartTransaction())
            {
                targetIds = CollectQtoBlockReferences(transaction, selectionResult, qtoType, out ignoredCount);
                defaults = GetCommonQtoValues(transaction, targetIds, editOutlet);
                transaction.Commit();
            }

            if (targetIds.Count == 0)
            {
                editor.WriteMessage("\n沒有找到可編輯的 QTO 圖塊。");
                ShowStepMessage(title, "沒有找到符合條件的已標記圖塊。", "請確認框選範圍內包含已標記的出線口或箱體。");
                return;
            }

            QtoEditPropertiesForm form = new QtoEditPropertiesForm(title, targetIds.Count, editOutlet, defaults);
            System.Windows.Forms.DialogResult result = Autodesk.AutoCAD.ApplicationServices.Application.ShowModalDialog(form);

            if (result != System.Windows.Forms.DialogResult.OK)
            {
                editor.WriteMessage("\n已取消" + title + "。");
                return;
            }

            if (!form.UpdateOutletId && !form.UpdateJbId && !form.UpdateSystem && !form.UpdateCableType)
            {
                editor.WriteMessage("\n沒有勾選任何要更新的欄位。");
                return;
            }

            int updatedCount = 0;

            using (Transaction transaction = database.TransactionManager.StartTransaction())
            {
                foreach (ObjectId targetId in targetIds)
                {
                    BlockReference blockReference = transaction.GetObject(targetId, OpenMode.ForWrite, false) as BlockReference;

                    if (blockReference == null || !QtoXDataHelper.HasQtoType(blockReference, qtoType))
                    {
                        ignoredCount++;
                        continue;
                    }

                    Dictionary<string, string> data = QtoXDataHelper.GetXData(blockReference);

                    if (editOutlet && form.UpdateOutletId)
                    {
                        data[QtoXDataHelper.KeyOutletId] = form.OutletId;
                    }

                    if (form.UpdateJbId)
                    {
                        data[QtoXDataHelper.KeyJbId] = form.JbId;
                    }

                    if (form.UpdateSystem)
                    {
                        data[QtoXDataHelper.KeySystem] = form.SystemName;
                    }

                    if (editOutlet && form.UpdateCableType)
                    {
                        data[QtoXDataHelper.KeyCableType] = form.CableType;
                    }

                    QtoXDataHelper.SetXData(blockReference, database, transaction, data);
                    updatedCount++;
                }

                transaction.Commit();
            }

            editor.WriteMessage("\n" + title + "完成。");
            editor.WriteMessage("\n更新數量：" + updatedCount);
            editor.WriteMessage("\n略過數量：" + ignoredCount);
            ShowStepMessage(
                title + "完成",
                "更新數量：" + updatedCount + "\n略過數量：" + ignoredCount,
                editOutlet ? "下一步可以執行「批次尋路」或輸出「出線口清單」確認資料。" : "下一步可以執行「批次尋路」或輸出「線段明細」確認資料。");
        }

        private static List<ObjectId> CollectQtoBlockReferences(Transaction transaction, PromptSelectionResult selectionResult, string qtoType, out int ignoredCount)
        {
            List<ObjectId> targetIds = new List<ObjectId>();
            ignoredCount = 0;

            foreach (SelectedObject selectedObject in selectionResult.Value)
            {
                if (selectedObject == null)
                {
                    ignoredCount++;
                    continue;
                }

                BlockReference blockReference = transaction.GetObject(selectedObject.ObjectId, OpenMode.ForRead, false) as BlockReference;

                if (blockReference != null && QtoXDataHelper.HasQtoType(blockReference, qtoType))
                {
                    targetIds.Add(selectedObject.ObjectId);
                }
                else
                {
                    ignoredCount++;
                }
            }

            return targetIds;
        }

        private static Dictionary<string, string> GetCommonQtoValues(Transaction transaction, List<ObjectId> targetIds, bool includeOutletFields)
        {
            Dictionary<string, string> values = new Dictionary<string, string>();

            SetCommonValue(transaction, targetIds, values, QtoXDataHelper.KeyJbId);
            SetCommonValue(transaction, targetIds, values, QtoXDataHelper.KeySystem);

            if (includeOutletFields)
            {
                SetCommonValue(transaction, targetIds, values, QtoXDataHelper.KeyOutletId);
                SetCommonValue(transaction, targetIds, values, QtoXDataHelper.KeyCableType);
            }

            return values;
        }

        private static void SetCommonValue(Transaction transaction, List<ObjectId> targetIds, Dictionary<string, string> values, string key)
        {
            bool hasValue = false;
            string commonValue = string.Empty;

            foreach (ObjectId targetId in targetIds)
            {
                Entity entity = transaction.GetObject(targetId, OpenMode.ForRead, false) as Entity;

                if (entity == null)
                {
                    continue;
                }

                string value = QtoXDataHelper.GetXDataValue(entity, key) ?? string.Empty;

                if (!hasValue)
                {
                    commonValue = value;
                    hasValue = true;
                }
                else if (!string.Equals(commonValue, value, StringComparison.Ordinal))
                {
                    commonValue = string.Empty;
                    break;
                }
            }

            values[key] = commonValue;
        }

        [CommandMethod("QTO_CLEAR_TRAY_PROPERTIES")]
        public void QtoClearTrayProperties()
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
                PromptEntityOptions options = new PromptEntityOptions("\n請點選要清除線槽屬性的 Polyline：");
                PromptEntityResult result = editor.GetEntity(options);

                if (result.Status != PromptStatus.OK)
                {
                    editor.WriteMessage("\n已取消清除線槽屬性。");
                    return;
                }

                using (Transaction transaction = database.TransactionManager.StartTransaction())
                {
                    Polyline tray = transaction.GetObject(result.ObjectId, OpenMode.ForWrite, false) as Polyline;

                    if (tray == null || !QtoXDataHelper.HasQtoType(tray, QtoXDataHelper.TypeTray))
                    {
                        editor.WriteMessage("\n選取的物件不是 QTO 線槽 Polyline。");
                        return;
                    }

                    QtoXDataHelper.RemoveXDataValues(
                        tray,
                        database,
                        transaction,
                        QtoXDataHelper.KeyQtoType,
                        QtoXDataHelper.KeyTrayId,
                        QtoXDataHelper.KeySystemScope,
                        QtoXDataHelper.KeyQtoError,
                        QtoXDataHelper.KeyQtoErrorMessage);

                    transaction.Commit();
                }

                editor.WriteMessage("\n已清除線槽 QTO 屬性。");
                ShowStepMessage("線槽屬性已清除", "已清除該 Polyline 上的線槽 QTO 屬性，Polyline 本身未刪除。", "下一步建議：需要重新使用時，可再執行「標記線槽」。");
            }
            catch (System.Exception ex)
            {
                WriteCommandError("QTO_CLEAR_TRAY_PROPERTIES", ex);
            }
        }

        [CommandMethod("QTO_RESET_OUTLET")]
        public void QtoResetOutlet()
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

                PromptEntityOptions options = new PromptEntityOptions("\n\u8acb\u9ede\u9078\u8981\u91cd\u7f6e\u7684\u51fa\u7dda\u53e3\uff1a");
                PromptEntityResult result = editor.GetEntity(options);

                if (result.Status != PromptStatus.OK)
                {
                    editor.WriteMessage("\n\u5df2\u53d6\u6d88 QTO_RESET_OUTLET\u3002");
                    return;
                }

                bool clearData = PromptYesNo(editor, "\n\u662f\u5426\u6e05\u9664 OUTLET_ID\u3001JB_ID\u3001SYSTEM\u3001CABLE_TYPE\uff1f [Yes/No]", false);

                if (!clearData)
                {
                    editor.WriteMessage("\n\u672a\u6e05\u9664\u51fa\u7dda\u53e3\u8cc7\u6599\u3002");
                    return;
                }

                string oldOutletId = string.Empty;
                string oldJbId = string.Empty;
                string oldSystem = string.Empty;
                string oldCableType = string.Empty;

                using (Transaction transaction = database.TransactionManager.StartTransaction())
                {
                    BlockReference outlet = transaction.GetObject(result.ObjectId, OpenMode.ForWrite, false) as BlockReference;

                    if (outlet == null || !QtoXDataHelper.HasQtoType(outlet, QtoXDataHelper.TypeOutlet))
                    {
                        editor.WriteMessage("\n\u9078\u53d6\u7684\u7269\u4ef6\u4e0d\u662f QTO \u51fa\u7dda\u53e3\u3002");
                        return;
                    }

                    oldOutletId = QtoXDataHelper.GetXDataValue(outlet, QtoXDataHelper.KeyOutletId) ?? string.Empty;
                    oldJbId = QtoXDataHelper.GetXDataValue(outlet, QtoXDataHelper.KeyJbId) ?? string.Empty;
                    oldSystem = QtoXDataHelper.GetXDataValue(outlet, QtoXDataHelper.KeySystem) ?? string.Empty;
                    oldCableType = QtoXDataHelper.GetXDataValue(outlet, QtoXDataHelper.KeyCableType) ?? string.Empty;

                    QtoXDataHelper.UpdateXDataValue(outlet, database, transaction, QtoXDataHelper.KeyOutletId, string.Empty);
                    QtoXDataHelper.UpdateXDataValue(outlet, database, transaction, QtoXDataHelper.KeyJbId, string.Empty);
                    QtoXDataHelper.UpdateXDataValue(outlet, database, transaction, QtoXDataHelper.KeySystem, string.Empty);
                    QtoXDataHelper.UpdateXDataValue(outlet, database, transaction, QtoXDataHelper.KeyCableType, string.Empty);

                    transaction.Commit();
                }

                editor.WriteMessage("\n========== QTO RESET OUTLET ==========");
                editor.WriteMessage("\n\u5df2\u91cd\u7f6e\u51fa\u7dda\u53e3 QTO \u8cc7\u6599\u3002");
                editor.WriteMessage("\nOLD_OUTLET_ID  : " + oldOutletId);
                editor.WriteMessage("\nOLD_JB_ID      : " + oldJbId);
                editor.WriteMessage("\nOLD_SYSTEM     : " + oldSystem);
                editor.WriteMessage("\nOLD_CABLE_TYPE : " + oldCableType);
                editor.WriteMessage("\n======================================");
                ShowStepMessage(
                    "\u51fa\u7dda\u53e3\u5df2\u91cd\u7f6e",
                    "\u5df2\u6e05\u7a7a OUTLET_ID\u3001JB_ID\u3001SYSTEM\u3001CABLE_TYPE\uff0c\u5716\u584a\u5916\u89c0\u6c92\u6709\u8b8a\u66f4\u3002",
                    "\u4e0b\u4e00\u6b65\u5efa\u8b70\uff1a\u53ef\u57f7\u884c QTO_NUMBER_OUTLETS \u91cd\u65b0\u7de8\u865f\uff0c\u518d\u57f7\u884c QTO_ASSIGN_JB\u3002");
            }
            catch (System.Exception ex)
            {
                WriteCommandError("QTO_RESET_OUTLET", ex);
            }
        }

        [CommandMethod("QTO_REASSIGN_WIRE")]
        public void QtoReassignWire()
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

                PromptEntityOptions wireOptions = new PromptEntityOptions("\n\u8acb\u9ede\u9078\u8981\u6539\u7d81\u7684 QTO \u914d\u7dda Polyline\uff1a");
                PromptEntityResult wireResult = editor.GetEntity(wireOptions);

                if (wireResult.Status != PromptStatus.OK)
                {
                    editor.WriteMessage("\n\u5df2\u53d6\u6d88 QTO_REASSIGN_WIRE\u3002");
                    return;
                }

                PromptEntityOptions outletOptions = new PromptEntityOptions("\n\u8acb\u9ede\u9078\u65b0\u7684\u51fa\u7dda\u53e3\uff1a");
                PromptEntityResult outletResult = editor.GetEntity(outletOptions);

                if (outletResult.Status != PromptStatus.OK)
                {
                    editor.WriteMessage("\n\u5df2\u53d6\u6d88 QTO_REASSIGN_WIRE\u3002");
                    return;
                }

                WireInfo oldWireInfo = new WireInfo();
                WireInfo newWireInfo = new WireInfo();

                using (Transaction transaction = database.TransactionManager.StartTransaction())
                {
                    Polyline wire = transaction.GetObject(wireResult.ObjectId, OpenMode.ForWrite, false) as Polyline;
                    BlockReference outlet = transaction.GetObject(outletResult.ObjectId, OpenMode.ForRead, false) as BlockReference;

                    if (wire == null || !QtoXDataHelper.HasQtoType(wire, QtoXDataHelper.TypeWire))
                    {
                        editor.WriteMessage("\n\u7b2c\u4e00\u500b\u9078\u53d6\u7269\u4ef6\u4e0d\u662f QTO \u914d\u7dda Polyline\u3002");
                        return;
                    }

                    if (outlet == null || !QtoXDataHelper.HasQtoType(outlet, QtoXDataHelper.TypeOutlet))
                    {
                        editor.WriteMessage("\n\u7b2c\u4e8c\u500b\u9078\u53d6\u7269\u4ef6\u4e0d\u662f QTO \u51fa\u7dda\u53e3\u3002");
                        return;
                    }

                    oldWireInfo.ObjectId = wire.ObjectId;
                    oldWireInfo.OutletId = QtoXDataHelper.GetXDataValue(wire, QtoXDataHelper.KeyOutletId) ?? string.Empty;
                    oldWireInfo.JbId = QtoXDataHelper.GetXDataValue(wire, QtoXDataHelper.KeyJbId) ?? string.Empty;
                    oldWireInfo.System = QtoXDataHelper.GetXDataValue(wire, QtoXDataHelper.KeySystem) ?? string.Empty;
                    oldWireInfo.CableType = QtoXDataHelper.GetXDataValue(wire, QtoXDataHelper.KeyCableType) ?? string.Empty;
                    oldWireInfo.Layer = wire.Layer ?? string.Empty;

                    newWireInfo.ObjectId = wire.ObjectId;
                    newWireInfo.OutletId = QtoXDataHelper.GetXDataValue(outlet, QtoXDataHelper.KeyOutletId) ?? string.Empty;
                    newWireInfo.JbId = QtoXDataHelper.GetXDataValue(outlet, QtoXDataHelper.KeyJbId) ?? string.Empty;
                    newWireInfo.System = QtoXDataHelper.GetXDataValue(outlet, QtoXDataHelper.KeySystem) ?? string.Empty;
                    newWireInfo.CableType = QtoXDataHelper.GetXDataValue(outlet, QtoXDataHelper.KeyCableType) ?? string.Empty;
                    newWireInfo.Layer = wire.Layer ?? string.Empty;

                    Dictionary<string, string> data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    data[QtoXDataHelper.KeyQtoType] = QtoXDataHelper.TypeWire;
                    data[QtoXDataHelper.KeyOutletId] = newWireInfo.OutletId;
                    data[QtoXDataHelper.KeyJbId] = newWireInfo.JbId;
                    data[QtoXDataHelper.KeySystem] = newWireInfo.System;
                    data[QtoXDataHelper.KeyCableType] = newWireInfo.CableType;
                    QtoXDataHelper.SetXData(wire, database, transaction, data);

                    QtoLayerHelper.MoveEntityToLayer(wire, database, transaction, QtoLayerHelper.WireLayerName);
                    newWireInfo.Layer = wire.Layer ?? string.Empty;

                    transaction.Commit();
                }

                editor.WriteMessage("\n========== QTO REASSIGN WIRE ==========");
                editor.WriteMessage("\nWIRE_OBJECT_ID : " + oldWireInfo.ObjectId.ToString());
                editor.WriteMessage("\n---------- OLD DATA ----------");
                editor.WriteMessage("\nOUTLET_ID  : " + oldWireInfo.OutletId);
                editor.WriteMessage("\nJB_ID      : " + oldWireInfo.JbId);
                editor.WriteMessage("\nSYSTEM     : " + oldWireInfo.System);
                editor.WriteMessage("\nCABLE_TYPE : " + oldWireInfo.CableType);
                editor.WriteMessage("\nLAYER      : " + oldWireInfo.Layer);
                editor.WriteMessage("\n---------- NEW DATA ----------");
                editor.WriteMessage("\nOUTLET_ID  : " + newWireInfo.OutletId);
                editor.WriteMessage("\nJB_ID      : " + newWireInfo.JbId);
                editor.WriteMessage("\nSYSTEM     : " + newWireInfo.System);
                editor.WriteMessage("\nCABLE_TYPE : " + newWireInfo.CableType);
                editor.WriteMessage("\nLAYER      : " + newWireInfo.Layer);
                editor.WriteMessage("\n=======================================");
                ShowStepMessage(
                    "\u914d\u7dda\u5df2\u6539\u7d81",
                    "\u5df2\u5c07\u9078\u53d6\u7684 Polyline \u6539\u7d81\u5230\u65b0\u7684\u51fa\u7dda\u53e3\u3002",
                    "\u4e0b\u4e00\u6b65\u5efa\u8b70\uff1a\u57f7\u884c QTO_QUERY_WIRE \u6216 QTO_CHECK_WIRE \u78ba\u8a8d\u95dc\u806f\u72c0\u614b\u3002");
            }
            catch (System.Exception ex)
            {
                WriteCommandError("QTO_REASSIGN_WIRE", ex);
            }
        }

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

        private static Editor GetEditor()
        {
            Document document = Application.DocumentManager.MdiActiveDocument;

            if (document == null)
            {
                return null;
            }

            return document.Editor;
        }

        private static Polyline CreateDoubleLPolyline(Point3d outletPosition, Point3d junctionBoxPosition)
        {
            double midY = (outletPosition.Y + junctionBoxPosition.Y) / 2.0;

            Polyline polyline = new Polyline();
            polyline.SetDatabaseDefaults();
            polyline.Elevation = outletPosition.Z;
            polyline.AddVertexAt(0, new Point2d(outletPosition.X, outletPosition.Y), 0.0, 0.0, 0.0);
            polyline.AddVertexAt(1, new Point2d(outletPosition.X, midY), 0.0, 0.0, 0.0);
            polyline.AddVertexAt(2, new Point2d(junctionBoxPosition.X, midY), 0.0, 0.0, 0.0);
            polyline.AddVertexAt(3, new Point2d(junctionBoxPosition.X, junctionBoxPosition.Y), 0.0, 0.0, 0.0);
            return polyline;
        }

        private static Polyline CreatePolylineFromPoints(List<Point3d> points)
        {
            Polyline polyline = new Polyline();
            polyline.SetDatabaseDefaults();

            if (points == null || points.Count == 0)
            {
                return polyline;
            }

            polyline.Elevation = points[0].Z;

            for (int i = 0; i < points.Count; i++)
            {
                polyline.AddVertexAt(i, new Point2d(points[i].X, points[i].Y), 0.0, 0.0, 0.0);
            }

            polyline.ColorIndex = 256;
            polyline.Linetype = "ByLayer";
            polyline.LineWeight = LineWeight.ByLayer;
            return polyline;
        }

        private static Mline CreateMlineFromPoints(List<Point3d> points, Database database, Transaction transaction, Editor editor)
        {
            Mline mline = new Mline();
            mline.SetDatabaseDefaults();
            mline.Justification = MlineJustification.Zero;
            mline.Scale = 1.0;
            mline.Normal = Vector3d.ZAxis;
            mline.ColorIndex = 256;
            mline.Linetype = "ByLayer";
            mline.LineWeight = LineWeight.ByLayer;

            ObjectId styleId = GetMlineStyleId(database, transaction);

            if (!styleId.IsNull && styleId.IsValid)
            {
                mline.Style = styleId;
                mline.SetFromStyle();
            }

            mline.Justification = MlineJustification.Zero;
            mline.Scale = 1.0;
            mline.Normal = Vector3d.ZAxis;
            mline.ColorIndex = 256;
            mline.Linetype = "ByLayer";
            mline.LineWeight = LineWeight.ByLayer;

            if (points == null)
            {
                return mline;
            }

            foreach (Point3d point in points)
            {
                mline.AppendSegment(ToCurrentUcsPoint(point, editor));
            }

            return mline;
        }

        private static ConduitPolylinePair CreateConduitPolylinePair(List<Point3d> points)
        {
            Polyline centerline = CreatePolylineFromPoints(points);

            if (centerline.NumberOfVertices < 2)
            {
                centerline.Dispose();
                return null;
            }

            double halfWidth = DefaultConduitMlineWidth / 2.0;
            Polyline primary = GetSingleOffsetPolyline(centerline, halfWidth);
            Polyline secondary = GetSingleOffsetPolyline(centerline, -halfWidth);
            centerline.Dispose();

            if (primary == null || secondary == null)
            {
                if (primary != null)
                {
                    primary.Dispose();
                }

                if (secondary != null)
                {
                    secondary.Dispose();
                }

                return null;
            }

            ConduitPolylinePair pair = new ConduitPolylinePair();
            pair.Primary = primary;
            pair.Secondary = secondary;
            return pair;
        }

        private static Polyline GetSingleOffsetPolyline(Polyline centerline, double offsetDistance)
        {
            DBObjectCollection offsetObjects;

            try
            {
                offsetObjects = centerline.GetOffsetCurves(offsetDistance);
            }
            catch
            {
                return null;
            }

            Polyline result = null;

            foreach (DBObject offsetObject in offsetObjects)
            {
                Polyline polyline = offsetObject as Polyline;

                if (polyline != null && result == null)
                {
                    result = polyline;
                    result.SetDatabaseDefaults();
                    result.ColorIndex = 256;
                    result.Linetype = "ByLayer";
                    result.LineWeight = LineWeight.ByLayer;
                    continue;
                }

                offsetObject.Dispose();
            }

            return result;
        }

        private static double GetPointPathLength(List<Point3d> points)
        {
            if (points == null || points.Count < 2)
            {
                return 0.0;
            }

            double length = 0.0;

            for (int i = 1; i < points.Count; i++)
            {
                length += points[i - 1].DistanceTo(points[i]);
            }

            return length;
        }

        private static Point3d ToCurrentUcsPoint(Point3d worldPoint, Editor editor)
        {
            if (editor == null)
            {
                return worldPoint;
            }

            return worldPoint.TransformBy(editor.CurrentUserCoordinateSystem.Inverse());
        }

        private static ObjectId GetMlineStyleId(Database database, Transaction transaction)
        {
            if (database == null)
            {
                return ObjectId.Null;
            }

            if (transaction == null || database.MLStyleDictionaryId.IsNull || !database.MLStyleDictionaryId.IsValid)
            {
                return database.CmlstyleID;
            }

            DBDictionary styleDictionary = transaction.GetObject(database.MLStyleDictionaryId, OpenMode.ForRead, false) as DBDictionary;

            if (styleDictionary == null)
            {
                return database.CmlstyleID;
            }

            if (styleDictionary.Contains(ConduitMlineStyleName))
            {
                return styleDictionary.GetAt(ConduitMlineStyleName);
            }

            styleDictionary.UpgradeOpen();

            MlineStyle style = new MlineStyle();
            style.Name = ConduitMlineStyleName;
            style.Description = "QTO conduit centerline style, width 5.";
            ConfigureConduitMlineStyle(style, database);

            ObjectId styleId = styleDictionary.SetAt(ConduitMlineStyleName, style);
            transaction.AddNewlyCreatedDBObject(style, true);

            return styleId;
        }

        private static void ConfigureConduitMlineStyle(MlineStyle style, Database database)
        {
            if (style == null)
            {
                return;
            }

            while (style.Elements.Count > 0)
            {
                style.Elements.RemoveAt(0);
            }

            ObjectId linetypeId = database != null && !database.ContinuousLinetype.IsNull ? database.ContinuousLinetype : ObjectId.Null;
            Autodesk.AutoCAD.Colors.Color color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByAci, 5);
            double halfWidth = DefaultConduitMlineWidth / 2.0;

            style.Elements.Add(new MlineStyleElement(-halfWidth, color, linetypeId), true);
            style.Elements.Add(new MlineStyleElement(halfWidth, color, linetypeId), true);
            style.ShowMiters = true;
            style.StartSquareCap = false;
            style.EndSquareCap = false;
            style.StartRoundCap = false;
            style.EndRoundCap = false;
            style.Filled = false;
        }

        private static double GetConduitLength(Entity conduit)
        {
            Polyline polyline = conduit as Polyline;

            if (polyline != null)
            {
                return QtoGeometryHelper.GetPolylineLength(polyline);
            }

            Mline mline = conduit as Mline;

            if (mline != null)
            {
                return QtoGeometryHelper.GetMlineLength(mline);
            }

            return 0.0;
        }

        private static double GetConduitLengthM(Entity conduit, double measuredLengthMm, string lengthSource)
        {
            if (string.Equals(lengthSource, "PARALLEL_POLYLINE_CENTER_LENGTH", StringComparison.OrdinalIgnoreCase))
            {
                string storedLength = QtoXDataHelper.GetXDataValue(conduit, QtoXDataHelper.KeyLengthM);
                double parsedLength;

                if (double.TryParse(storedLength, out parsedLength) && parsedLength > 0.0)
                {
                    return parsedLength;
                }
            }

            return QtoGeometryHelper.ConvertMmToM(measuredLengthMm);
        }

        private static string GetConduitLengthSource(Entity conduit, string existingLengthSource)
        {
            if (!string.IsNullOrWhiteSpace(existingLengthSource))
            {
                return existingLengthSource;
            }

            if (conduit is Mline)
            {
                return "MLINE_VERTEX_LENGTH";
            }

            if (conduit is Polyline)
            {
                return "POLYLINE_LENGTH";
            }

            return "UNKNOWN";
        }

        private static List<Point3d> BuildOrthogonalRouteToPoint(Point3d startPoint, Point3d endPoint)
        {
            List<Point3d> points = new List<Point3d>();
            AddRoutePoint(points, startPoint);

            double deltaX = Math.Abs(endPoint.X - startPoint.X);
            double deltaY = Math.Abs(endPoint.Y - startPoint.Y);

            if (deltaX > TrayPointTolerance && deltaY > TrayPointTolerance)
            {
                Point3d bendPoint;

                if (deltaX >= deltaY)
                {
                    bendPoint = new Point3d(endPoint.X, startPoint.Y, startPoint.Z);
                }
                else
                {
                    bendPoint = new Point3d(startPoint.X, endPoint.Y, startPoint.Z);
                }

                AddRoutePoint(points, bendPoint);
            }

            AddRoutePoint(points, endPoint);
            return points;
        }

        private static DBText CreateOutletLabelText(Point3d outletPosition, string outletId, double textHeight, double yOffset)
        {
            Point3d labelPosition = new Point3d(outletPosition.X, outletPosition.Y + yOffset, outletPosition.Z);
            DBText label = new DBText();
            label.SetDatabaseDefaults();
            label.TextString = outletId ?? string.Empty;
            label.Height = textHeight;
            label.Position = labelPosition;
            label.HorizontalMode = TextHorizontalMode.TextCenter;
            label.VerticalMode = TextVerticalMode.TextVerticalMid;
            label.AlignmentPoint = labelPosition;
            label.Rotation = 0.0;
            return label;
        }

        private static void DeleteExistingOutletLabels(BlockTableRecord modelSpace, Transaction transaction, string outletId)
        {
            if (modelSpace == null || transaction == null || string.IsNullOrWhiteSpace(outletId))
            {
                return;
            }

            List<ObjectId> labelObjectIds = new List<ObjectId>();

            foreach (ObjectId objectId in modelSpace)
            {
                Entity entity = transaction.GetObject(objectId, OpenMode.ForRead, false) as Entity;

                if (entity == null || !QtoXDataHelper.HasQtoType(entity, QtoXDataHelper.TypeOutletLabel))
                {
                    continue;
                }

                string existingOutletId = QtoXDataHelper.GetXDataValue(entity, QtoXDataHelper.KeyOutletId);

                if (string.Equals(existingOutletId, outletId, StringComparison.OrdinalIgnoreCase))
                {
                    labelObjectIds.Add(objectId);
                }
            }

            foreach (ObjectId labelObjectId in labelObjectIds)
            {
                Entity label = transaction.GetObject(labelObjectId, OpenMode.ForWrite, false) as Entity;

                if (label != null)
                {
                    label.Erase();
                }
            }
        }

        private static void CreateCalloutAnnotation(BlockTableRecord modelSpace, Database database, Transaction transaction, BlockReference sourceBlock, BlockReference junctionBox, string junctionBoxId, double arrowLength, double textHeight)
        {
            Point3d sourcePoint = sourceBlock.Position;
            Point3d targetPoint = junctionBox.Position;
            Vector3d direction = new Vector3d(targetPoint.X - sourcePoint.X, targetPoint.Y - sourcePoint.Y, 0.0);

            if (direction.Length <= 0.0001)
            {
                direction = Vector3d.XAxis;
            }

            direction = direction.GetNormal();
            double sourceToTargetDistance = sourcePoint.DistanceTo(targetPoint);
            double effectiveArrowLength = arrowLength;

            if (sourceToTargetDistance > 0.0001)
            {
                effectiveArrowLength = Math.Min(arrowLength, sourceToTargetDistance * 0.6);
            }

            effectiveArrowLength = Math.Max(effectiveArrowLength, textHeight * 1.5);
            Point3d arrowStart = sourcePoint + direction.MultiplyBy(textHeight * 0.35);
            Point3d arrowEnd = arrowStart + direction.MultiplyBy(effectiveArrowLength);
            Polyline arrowLine = CreateTwoPointPolyline(arrowStart, arrowEnd);
            AppendCalloutEntity(modelSpace, database, transaction, arrowLine, junctionBoxId);

            double arrowHeadLength = Math.Min(effectiveArrowLength * 0.28, textHeight * 1.4);
            arrowHeadLength = Math.Max(arrowHeadLength, textHeight * 0.8);
            Vector3d backDirection = direction.Negate();
            Vector3d leftHeadDirection = RotateVector2d(backDirection, 25.0);
            Vector3d rightHeadDirection = RotateVector2d(backDirection, -25.0);

            Polyline leftHead = CreateTwoPointPolyline(arrowEnd, arrowEnd + leftHeadDirection.MultiplyBy(arrowHeadLength));
            Polyline rightHead = CreateTwoPointPolyline(arrowEnd, arrowEnd + rightHeadDirection.MultiplyBy(arrowHeadLength));
            AppendCalloutEntity(modelSpace, database, transaction, leftHead, junctionBoxId);
            AppendCalloutEntity(modelSpace, database, transaction, rightHead, junctionBoxId);

            Vector3d textOffsetDirection = new Vector3d(-direction.Y, direction.X, 0.0);
            Point3d textPosition = arrowStart + direction.MultiplyBy(effectiveArrowLength * 0.5) + textOffsetDirection.MultiplyBy(textHeight * 0.75);
            DBText label = new DBText();
            label.SetDatabaseDefaults();
            label.Position = textPosition;
            label.Height = textHeight;
            label.TextString = "to" + junctionBoxId;
            label.Rotation = 0.0;
            AppendCalloutEntity(modelSpace, database, transaction, label, junctionBoxId);
        }

        private static Polyline CreateTwoPointPolyline(Point3d startPoint, Point3d endPoint)
        {
            Polyline polyline = new Polyline();
            polyline.SetDatabaseDefaults();
            polyline.Elevation = startPoint.Z;
            polyline.AddVertexAt(0, new Point2d(startPoint.X, startPoint.Y), 0.0, 0.0, 0.0);
            polyline.AddVertexAt(1, new Point2d(endPoint.X, endPoint.Y), 0.0, 0.0, 0.0);
            polyline.ColorIndex = 256;
            polyline.Linetype = "ByLayer";
            polyline.LineWeight = LineWeight.ByLayer;
            return polyline;
        }

        private static Vector3d RotateVector2d(Vector3d vector, double degrees)
        {
            double radians = degrees * Math.PI / 180.0;
            double cos = Math.Cos(radians);
            double sin = Math.Sin(radians);
            return new Vector3d(
                vector.X * cos - vector.Y * sin,
                vector.X * sin + vector.Y * cos,
                0.0).GetNormal();
        }

        private static void AppendCalloutEntity(BlockTableRecord modelSpace, Database database, Transaction transaction, Entity entity, string junctionBoxId)
        {
            modelSpace.AppendEntity(entity);
            transaction.AddNewlyCreatedDBObject(entity, true);
            QtoLayerHelper.MoveEntityToLayer(entity, database, transaction, QtoLayerHelper.CalloutLayerName);

            Dictionary<string, string> data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            data[QtoXDataHelper.KeyQtoType] = QtoXDataHelper.TypeCallout;
            data[QtoXDataHelper.KeyJbId] = junctionBoxId ?? string.Empty;
            QtoXDataHelper.SetXData(entity, database, transaction, data);
        }

        private static void AssignBlockToJunctionBox(BlockReference sourceBlock, Database database, Transaction transaction, string junctionBoxId)
        {
            Dictionary<string, string> data = QtoXDataHelper.GetXData(sourceBlock);

            if (!data.ContainsKey(QtoXDataHelper.KeyQtoType) || string.IsNullOrWhiteSpace(data[QtoXDataHelper.KeyQtoType]))
            {
                data[QtoXDataHelper.KeyQtoType] = QtoXDataHelper.TypeOutlet;
            }

            data[QtoXDataHelper.KeyJbId] = junctionBoxId ?? string.Empty;

            if (!data.ContainsKey(QtoXDataHelper.KeySystem))
            {
                data[QtoXDataHelper.KeySystem] = "DATA";
            }

            QtoXDataHelper.SetXData(sourceBlock, database, transaction, data);
        }

        private static string GetJunctionBoxDisplayName(Transaction transaction, BlockReference junctionBox)
        {
            string junctionBoxId = QtoXDataHelper.GetXDataValue(junctionBox, QtoXDataHelper.KeyJbId);

            if (!string.IsNullOrWhiteSpace(junctionBoxId))
            {
                return junctionBoxId.Trim();
            }

            string blockName = GetBlockName(transaction, junctionBox);

            if (!string.IsNullOrWhiteSpace(blockName))
            {
                return blockName.Trim();
            }

            return "JB";
        }

        private static Dictionary<string, string> BuildWireXData(Polyline polyline, Transaction transaction, Database database, string outletId, string jbId, string system, string cableType, string routeMode, string routeStatus)
        {
            Dictionary<string, string> data = QtoXDataHelper.GetXData(polyline);
            data[QtoXDataHelper.KeyQtoType] = QtoXDataHelper.TypeWire;
            data[QtoXDataHelper.KeyRouteId] = GetOrCreateRouteId(data, transaction, database);
            data[QtoXDataHelper.KeyRouteMode] = string.IsNullOrWhiteSpace(routeMode) ? QtoXDataHelper.RouteModeLegacyManual : routeMode;
            data[QtoXDataHelper.KeyRouteStatus] = string.IsNullOrWhiteSpace(routeStatus) ? QtoXDataHelper.RouteStatusDraft : routeStatus;
            data[QtoXDataHelper.KeyOutletId] = outletId ?? string.Empty;
            data[QtoXDataHelper.KeyJbId] = jbId ?? string.Empty;
            data[QtoXDataHelper.KeySystem] = system ?? string.Empty;
            data[QtoXDataHelper.KeyCableType] = cableType ?? string.Empty;
            data[QtoXDataHelper.KeyLengthM] = QtoGeometryHelper.ConvertMmToM(QtoGeometryHelper.GetPolylineLength(polyline)).ToString("0.000");
            data[QtoXDataHelper.KeyLengthSource] = "POLYLINE_LENGTH";
            return data;
        }

        private static string GetOrCreateRouteId(Dictionary<string, string> data, Transaction transaction, Database database)
        {
            string existing = GetDictionaryValue(data, QtoXDataHelper.KeyRouteId);

            if (!string.IsNullOrWhiteSpace(existing))
            {
                return existing;
            }

            return "ROUTE-" + GetNextRouteNumber(transaction, database).ToString("0000");
        }

        private static string GetDictionaryValue(Dictionary<string, string> data, string key)
        {
            string value;

            if (data != null && data.TryGetValue(key, out value))
            {
                return value;
            }

            return string.Empty;
        }

        private static PromptEntityResult PromptOutletEntity(Editor editor, string message)
        {
            PromptEntityOptions options = new PromptEntityOptions(message);
            options.SetRejectMessage("\n物件必須是 QTO 出線口圖塊。");
            options.AddAllowedClass(typeof(BlockReference), true);
            return editor.GetEntity(options);
        }

        private static PromptEntityResult PromptJunctionBoxEntity(Editor editor, string message)
        {
            PromptEntityOptions options = new PromptEntityOptions(message);
            options.SetRejectMessage("\n物件必須是 QTO 結線箱圖塊。");
            options.AddAllowedClass(typeof(BlockReference), true);
            return editor.GetEntity(options);
        }

        private static void RunSelectedTrayBatchRoutePolylineOnly()
        {
            Document document = Application.DocumentManager.MdiActiveDocument;

            if (document == null)
            {
                return;
            }

            Editor editor = document.Editor;
            Database database = document.Database;

            PromptSelectionOptions traySelectionOptions = new PromptSelectionOptions();
            traySelectionOptions.MessageForAdding = "\n請選取本次要使用的線槽 Polyline：";
            PromptSelectionResult traySelectionResult = editor.GetSelection(traySelectionOptions);

            if (traySelectionResult.Status != PromptStatus.OK)
            {
                editor.WriteMessage("\n已取消批次線槽尋路。");
                return;
            }

            PromptSelectionOptions outletSelectionOptions = new PromptSelectionOptions();
            outletSelectionOptions.MessageForAdding = "\n請框選要沿線槽回到箱體的出線口：";
            PromptSelectionResult outletSelectionResult = editor.GetSelection(outletSelectionOptions);

            if (outletSelectionResult.Status != PromptStatus.OK)
            {
                editor.WriteMessage("\n已取消批次線槽尋路。");
                return;
            }

            PromptEntityResult junctionBoxResult = PromptJunctionBoxEntity(editor, "\n請點選箱體：");

            if (junctionBoxResult.Status != PromptStatus.OK)
            {
                editor.WriteMessage("\n已取消批次線槽尋路。");
                return;
            }

            List<ObjectId> trayObjectIds = new List<ObjectId>();

            foreach (SelectedObject selectedTray in traySelectionResult.Value)
            {
                if (selectedTray != null)
                {
                    trayObjectIds.Add(selectedTray.ObjectId);
                }
            }

            int outletCount = 0;
            int successCount = 0;
            int failureCount = 0;
            List<string> errorMessages = new List<string>();

            foreach (SelectedObject selectedOutlet in outletSelectionResult.Value)
            {
                if (selectedOutlet == null)
                {
                    failureCount++;
                    errorMessages.Add("選取物件無效");
                    continue;
                }

                outletCount++;
                TrayRouteResult routeResult = RouteOutletToJunctionBoxBySelectedTrays(database, selectedOutlet.ObjectId, junctionBoxResult.ObjectId, trayObjectIds);

                if (routeResult.Success)
                {
                    successCount++;
                    editor.WriteMessage("\n已產生配線：" + routeResult.OutletId + " -> " + routeResult.JbId + "，長度 " + routeResult.LengthM.ToString("0.000") + " m");
                }
                else
                {
                    failureCount++;
                    string outletLabel = string.IsNullOrWhiteSpace(routeResult.OutletId) ? selectedOutlet.ObjectId.ToString() : routeResult.OutletId;
                    errorMessages.Add(outletLabel + "：" + routeResult.ErrorMessage);
                }
            }

            editor.WriteMessage("\n批次線槽尋路完成。");
            editor.WriteMessage("\n線槽數量：" + trayObjectIds.Count);
            editor.WriteMessage("\n出線口數量：" + outletCount);
            editor.WriteMessage("\n成功產生聚合線：" + successCount);
            editor.WriteMessage("\n失敗或略過：" + failureCount);

            foreach (string errorMessage in errorMessages)
            {
                editor.WriteMessage("\n" + errorMessage);
            }

            string message = "線槽數量：" + trayObjectIds.Count + "\n出線口數量：" + outletCount + "\n成功產生聚合線：" + successCount + "\n失敗或略過：" + failureCount;

            if (errorMessages.Count > 0)
            {
                message += "\n\n部分出線口未產生，請查看命令列錯誤訊息。";
            }

            ShowStepMessage(
                "批次線槽尋路完成",
                message,
                "下一步可以執行「線段明細」或「配線關係明細」輸出 CSV 報表。");
        }

        private static void RunSelectedTrayBatchRoute()
        {
            if (DateTime.Now.Ticks >= 0)
            {
                RunSelectedTrayBatchRoutePolylineOnly();
                return;
            }

            Document document = Application.DocumentManager.MdiActiveDocument;

            if (document == null)
            {
                return;
            }

            Editor editor = document.Editor;
            Database database = document.Database;

            PromptSelectionOptions traySelectionOptions = new PromptSelectionOptions();
            traySelectionOptions.MessageForAdding = "\n請選取本次要使用的線槽 Polyline：";
            PromptSelectionResult traySelectionResult = editor.GetSelection(traySelectionOptions);

            if (traySelectionResult.Status != PromptStatus.OK)
            {
                editor.WriteMessage("\n已取消批次線槽尋路。");
                return;
            }

            PromptSelectionOptions outletSelectionOptions = new PromptSelectionOptions();
            outletSelectionOptions.MessageForAdding = "\n請框選要沿線槽回到箱體的出線口：";
            PromptSelectionResult outletSelectionResult = editor.GetSelection(outletSelectionOptions);

            if (outletSelectionResult.Status != PromptStatus.OK)
            {
                editor.WriteMessage("\n已取消批次線槽尋路。");
                return;
            }

            PromptEntityResult junctionBoxResult = PromptJunctionBoxEntity(editor, "\n請點選箱體：");

            if (junctionBoxResult.Status != PromptStatus.OK)
            {
                editor.WriteMessage("\n已取消批次線槽尋路。");
                return;
            }

            List<ObjectId> trayObjectIds = new List<ObjectId>();

            foreach (SelectedObject selectedTray in traySelectionResult.Value)
            {
                if (selectedTray != null)
                {
                    trayObjectIds.Add(selectedTray.ObjectId);
                }
            }

            List<string[]> rows = new List<string[]>();
            rows.Add(new string[] { "出線口編號", "箱體編號", "配線物件ID", "長度_公尺", "狀態", "錯誤訊息" });
            int outletCount = 0;
            int successCount = 0;
            int failureCount = 0;

            foreach (SelectedObject selectedOutlet in outletSelectionResult.Value)
            {
                if (selectedOutlet == null)
                {
                    failureCount++;
                    rows.Add(new string[] { string.Empty, string.Empty, string.Empty, "0.000", "錯誤", "選取物件無效" });
                    continue;
                }

                outletCount++;
                TrayRouteResult routeResult = RouteOutletToJunctionBoxBySelectedTrays(database, selectedOutlet.ObjectId, junctionBoxResult.ObjectId, trayObjectIds);

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
            editor.WriteMessage("\n線槽數量：" + trayObjectIds.Count);
            editor.WriteMessage("\n出線口數量：" + outletCount);
            editor.WriteMessage("\n成功產生配線：" + successCount);
            editor.WriteMessage("\n失敗或略過：" + failureCount);
            editor.WriteMessage("\nCSV：" + (outputPath ?? "未輸出"));
            ShowStepMessage(
                "批次線槽尋路完成",
                "線槽數量：" + trayObjectIds.Count + "\n出線口數量：" + outletCount + "\n成功產生配線：" + successCount + "\n失敗或略過：" + failureCount + "\nCSV：" + (outputPath ?? "未輸出"),
                "下一步建議執行「線段明細」或「配線關係明細」，確認每個出線口到箱體的長度與關係。");
        }

        private static TrayRouteResult RouteOutletToJunctionBoxBySelectedTrays(Database database, ObjectId outletObjectId, ObjectId junctionBoxObjectId, List<ObjectId> trayObjectIds)
        {
            TrayRouteResult result = new TrayRouteResult();

            using (Transaction transaction = database.TransactionManager.StartTransaction())
            {
                BlockReference outlet = transaction.GetObject(outletObjectId, OpenMode.ForRead, false) as BlockReference;
                BlockReference junctionBox = transaction.GetObject(junctionBoxObjectId, OpenMode.ForRead, false) as BlockReference;

                if (outlet == null || !QtoXDataHelper.HasQtoType(outlet, QtoXDataHelper.TypeOutlet))
                {
                    result.ErrorMessage = "選取的物件不是已標記的出線口。";
                    return result;
                }

                if (junctionBox == null || !QtoXDataHelper.HasQtoType(junctionBox, QtoXDataHelper.TypeJunctionBox))
                {
                    result.ErrorMessage = "選取的箱體不是已標記的 QTO 箱體。";
                    return result;
                }

                string outletId = QtoXDataHelper.GetXDataValue(outlet, QtoXDataHelper.KeyOutletId) ?? string.Empty;
                string jbId = QtoXDataHelper.GetXDataValue(junctionBox, QtoXDataHelper.KeyJbId) ?? string.Empty;
                string system = QtoXDataHelper.GetXDataValue(outlet, QtoXDataHelper.KeySystem) ?? string.Empty;
                string cableType = QtoXDataHelper.GetXDataValue(outlet, QtoXDataHelper.KeyCableType) ?? string.Empty;
                result.OutletId = outletId;
                result.JbId = jbId;

                if (string.IsNullOrWhiteSpace(outletId))
                {
                    result.ErrorMessage = "缺少 OUTLET_ID，請先標記或重新編號出線口。";
                    return result;
                }

                if (string.IsNullOrWhiteSpace(jbId))
                {
                    result.ErrorMessage = "缺少 JB_ID，請先將出線口指定到箱體。";
                    return result;
                }

                List<SelectedTraySegment> segments = BuildSelectedTraySegments(transaction, trayObjectIds);

                if (segments.Count == 0)
                {
                    result.ErrorMessage = "沒有可用的線槽 Polyline。";
                    return result;
                }

                SelectedTrayProjection outletProjection = FindClosestTrayProjection(outlet.Position, segments);
                SelectedTrayProjection boxProjection = FindClosestTrayProjection(junctionBox.Position, segments);

                if (outletProjection == null || boxProjection == null)
                {
                    result.ErrorMessage = "無法將出線口或箱體投影到線槽。";
                    return result;
                }

                SelectedTrayGraph graph = SelectedTrayGraph.Build(segments, outletProjection, boxProjection);
                List<Point3d> trayPath;

                if (!graph.TryFindPath(outletProjection.NodeId, boxProjection.NodeId, out trayPath))
                {
                    result.ErrorMessage = "選取的線槽之間沒有連通，請確認線槽端點有相接。";
                    return result;
                }

                List<Point3d> routePoints = new List<Point3d>();
                AddRoutePoint(routePoints, outlet.Position);

                foreach (Point3d point in trayPath)
                {
                    AddRoutePoint(routePoints, point);
                }

                AddRoutePoint(routePoints, junctionBox.Position);

                if (routePoints.Count < 2)
                {
                    result.ErrorMessage = "產生的配線路徑點不足。";
                    return result;
                }

                BlockTableRecord modelSpace = GetModelSpace(transaction, database);
                modelSpace.UpgradeOpen();
                Polyline wire = CreatePolylineFromPoints(routePoints);
                modelSpace.AppendEntity(wire);
                transaction.AddNewlyCreatedDBObject(wire, true);
                QtoLayerHelper.MoveEntityToLayer(wire, database, transaction, QtoLayerHelper.WireLayerName);

                Dictionary<string, string> data = BuildWireXData(
                    wire,
                    transaction,
                    database,
                    outletId,
                    jbId,
                    system,
                    cableType,
                    QtoXDataHelper.RouteModeTrayNetwork,
                    QtoXDataHelper.RouteStatusDraft);
                QtoXDataHelper.SetXData(wire, database, transaction, data);

                result.Success = true;
                result.WireObjectId = wire.ObjectId;
                result.LengthM = QtoGeometryHelper.ConvertMmToM(QtoGeometryHelper.GetPolylineLength(wire));
                transaction.Commit();
            }

            return result;
        }

        private static List<SelectedTraySegment> BuildSelectedTraySegments(Transaction transaction, List<ObjectId> trayObjectIds)
        {
            List<SelectedTraySegment> segments = new List<SelectedTraySegment>();
            int segmentId = 0;

            if (trayObjectIds == null)
            {
                return segments;
            }

            foreach (ObjectId trayObjectId in trayObjectIds)
            {
                Polyline tray = transaction.GetObject(trayObjectId, OpenMode.ForRead, false) as Polyline;

                if (tray == null || tray.NumberOfVertices < 2)
                {
                    continue;
                }

                for (int i = 0; i < tray.NumberOfVertices - 1; i++)
                {
                    Point3d start = tray.GetPoint3dAt(i);
                    Point3d end = tray.GetPoint3dAt(i + 1);
                    double length = start.DistanceTo(end);

                    if (length <= TrayPointTolerance)
                    {
                        continue;
                    }

                    segments.Add(new SelectedTraySegment(segmentId, start, end, length));
                    segmentId++;
                }
            }

            return segments;
        }

        private static SelectedTrayProjection FindClosestTrayProjection(Point3d sourcePoint, List<SelectedTraySegment> segments)
        {
            SelectedTrayProjection closest = null;

            foreach (SelectedTraySegment segment in segments)
            {
                double t;
                Point3d point = ProjectPointToSegment(sourcePoint, segment.Start, segment.End, out t);
                double distance = sourcePoint.DistanceTo(point);

                if (closest == null || distance < closest.Distance)
                {
                    closest = new SelectedTrayProjection(segment, point, t, distance);
                }
            }

            return closest;
        }

        private static Point3d ProjectPointToSegment(Point3d point, Point3d start, Point3d end, out double t)
        {
            Vector3d segment = end - start;
            double lengthSquared = segment.DotProduct(segment);

            if (lengthSquared <= TrayPointTolerance)
            {
                t = 0.0;
                return start;
            }

            t = ((point - start).DotProduct(segment)) / lengthSquared;
            t = Math.Max(0.0, Math.Min(1.0, t));
            return start + (segment * t);
        }

        private static void AddRoutePoint(List<Point3d> points, Point3d point)
        {
            if (points.Count == 0 || points[points.Count - 1].DistanceTo(point) > TrayPointTolerance)
            {
                points.Add(point);
            }
        }

        private class SelectedTraySegment
        {
            public SelectedTraySegment(int id, Point3d start, Point3d end, double length)
            {
                Id = id;
                Start = start;
                End = end;
                Length = length;
            }

            public int Id { get; private set; }
            public Point3d Start { get; private set; }
            public Point3d End { get; private set; }
            public double Length { get; private set; }
        }

        private class SelectedTrayProjection
        {
            public SelectedTrayProjection(SelectedTraySegment segment, Point3d point, double t, double distance)
            {
                Segment = segment;
                Point = point;
                T = t;
                Distance = distance;
                NodeId = -1;
            }

            public SelectedTraySegment Segment { get; private set; }
            public Point3d Point { get; private set; }
            public double T { get; private set; }
            public double Distance { get; private set; }
            public int NodeId { get; set; }
        }

        private class SelectedTrayGraphPoint
        {
            public SelectedTrayGraphPoint(Point3d point, double t)
            {
                Point = point;
                T = t;
                NodeId = -1;
            }

            public Point3d Point { get; private set; }
            public double T { get; private set; }
            public int NodeId { get; set; }
        }

        private class SelectedTrayEdge
        {
            public SelectedTrayEdge(int toNodeId, double length)
            {
                ToNodeId = toNodeId;
                Length = length;
            }

            public int ToNodeId { get; private set; }
            public double Length { get; private set; }
        }

        private class SelectedTrayGraph
        {
            private readonly List<Point3d> nodes = new List<Point3d>();
            private readonly Dictionary<int, List<SelectedTrayEdge>> edges = new Dictionary<int, List<SelectedTrayEdge>>();

            public static SelectedTrayGraph Build(List<SelectedTraySegment> segments, SelectedTrayProjection outletProjection, SelectedTrayProjection boxProjection)
            {
                SelectedTrayGraph graph = new SelectedTrayGraph();

                foreach (SelectedTraySegment segment in segments)
                {
                    List<SelectedTrayGraphPoint> points = new List<SelectedTrayGraphPoint>();
                    points.Add(new SelectedTrayGraphPoint(segment.Start, 0.0));
                    points.Add(new SelectedTrayGraphPoint(segment.End, 1.0));

                    if (outletProjection != null && outletProjection.Segment.Id == segment.Id)
                    {
                        points.Add(new SelectedTrayGraphPoint(outletProjection.Point, outletProjection.T));
                    }

                    if (boxProjection != null && boxProjection.Segment.Id == segment.Id)
                    {
                        points.Add(new SelectedTrayGraphPoint(boxProjection.Point, boxProjection.T));
                    }

                    points.Sort(delegate (SelectedTrayGraphPoint left, SelectedTrayGraphPoint right)
                    {
                        return left.T.CompareTo(right.T);
                    });

                    for (int i = 0; i < points.Count; i++)
                    {
                        points[i].NodeId = graph.GetOrAddNode(points[i].Point);
                    }

                    for (int i = 0; i < points.Count - 1; i++)
                    {
                        int fromNodeId = points[i].NodeId;
                        int toNodeId = points[i + 1].NodeId;

                        if (fromNodeId == toNodeId)
                        {
                            continue;
                        }

                        graph.AddEdge(fromNodeId, toNodeId, points[i].Point.DistanceTo(points[i + 1].Point));
                    }
                }

                if (outletProjection != null)
                {
                    outletProjection.NodeId = graph.GetOrAddNode(outletProjection.Point);
                }

                if (boxProjection != null)
                {
                    boxProjection.NodeId = graph.GetOrAddNode(boxProjection.Point);
                }

                return graph;
            }

            public bool TryFindPath(int startNodeId, int endNodeId, out List<Point3d> path)
            {
                path = new List<Point3d>();

                if (startNodeId < 0 || endNodeId < 0 || startNodeId >= nodes.Count || endNodeId >= nodes.Count)
                {
                    return false;
                }

                int count = nodes.Count;
                double[] distances = new double[count];
                int[] previous = new int[count];
                bool[] visited = new bool[count];

                for (int i = 0; i < count; i++)
                {
                    distances[i] = double.MaxValue;
                    previous[i] = -1;
                }

                distances[startNodeId] = 0.0;

                for (int i = 0; i < count; i++)
                {
                    int current = -1;
                    double currentDistance = double.MaxValue;

                    for (int nodeId = 0; nodeId < count; nodeId++)
                    {
                        if (!visited[nodeId] && distances[nodeId] < currentDistance)
                        {
                            current = nodeId;
                            currentDistance = distances[nodeId];
                        }
                    }

                    if (current == -1)
                    {
                        break;
                    }

                    if (current == endNodeId)
                    {
                        break;
                    }

                    visited[current] = true;
                    List<SelectedTrayEdge> nodeEdges;

                    if (!edges.TryGetValue(current, out nodeEdges))
                    {
                        continue;
                    }

                    foreach (SelectedTrayEdge edge in nodeEdges)
                    {
                        if (visited[edge.ToNodeId])
                        {
                            continue;
                        }

                        double nextDistance = distances[current] + edge.Length;

                        if (nextDistance < distances[edge.ToNodeId])
                        {
                            distances[edge.ToNodeId] = nextDistance;
                            previous[edge.ToNodeId] = current;
                        }
                    }
                }

                if (distances[endNodeId] == double.MaxValue)
                {
                    return false;
                }

                List<int> nodePath = new List<int>();
                int pathNodeId = endNodeId;

                while (pathNodeId != -1)
                {
                    nodePath.Add(pathNodeId);

                    if (pathNodeId == startNodeId)
                    {
                        break;
                    }

                    pathNodeId = previous[pathNodeId];
                }

                if (nodePath[nodePath.Count - 1] != startNodeId)
                {
                    return false;
                }

                nodePath.Reverse();

                foreach (int nodeId in nodePath)
                {
                    path.Add(nodes[nodeId]);
                }

                return true;
            }

            private int GetOrAddNode(Point3d point)
            {
                for (int i = 0; i < nodes.Count; i++)
                {
                    if (nodes[i].DistanceTo(point) <= TrayNodeMergeTolerance)
                    {
                        return i;
                    }
                }

                int nodeId = nodes.Count;
                nodes.Add(point);
                edges[nodeId] = new List<SelectedTrayEdge>();
                return nodeId;
            }

            private void AddEdge(int fromNodeId, int toNodeId, double length)
            {
                if (length <= TrayPointTolerance)
                {
                    return;
                }

                edges[fromNodeId].Add(new SelectedTrayEdge(toNodeId, length));
                edges[toNodeId].Add(new SelectedTrayEdge(fromNodeId, length));
            }
        }

        private static TrayRouteResult RouteOutletToJunctionBoxByTray(Database database, ObjectId outletObjectId, ObjectId junctionBoxObjectId, double tolerance)
        {
            TrayRouteResult result = new TrayRouteResult();

            using (Transaction transaction = database.TransactionManager.StartTransaction())
            {
                BlockReference outlet = transaction.GetObject(outletObjectId, OpenMode.ForRead, false) as BlockReference;
                BlockReference junctionBox = transaction.GetObject(junctionBoxObjectId, OpenMode.ForRead, false) as BlockReference;

                if (outlet == null || !QtoXDataHelper.HasQtoType(outlet, QtoXDataHelper.TypeOutlet))
                {
                    result.ErrorMessage = "選取的出線口不是 QTO 出線口。";
                    return result;
                }

                if (junctionBox == null || !QtoXDataHelper.HasQtoType(junctionBox, QtoXDataHelper.TypeJunctionBox))
                {
                    result.ErrorMessage = "選取的結線箱不是 QTO 結線箱。";
                    return result;
                }

                string outletId = QtoXDataHelper.GetXDataValue(outlet, QtoXDataHelper.KeyOutletId) ?? string.Empty;
                string jbId = QtoXDataHelper.GetXDataValue(junctionBox, QtoXDataHelper.KeyJbId) ?? string.Empty;
                string system = QtoXDataHelper.GetXDataValue(outlet, QtoXDataHelper.KeySystem) ?? string.Empty;
                string cableType = QtoXDataHelper.GetXDataValue(outlet, QtoXDataHelper.KeyCableType) ?? string.Empty;
                result.OutletId = outletId;
                result.JbId = jbId;

                if (string.IsNullOrWhiteSpace(outletId))
                {
                    result.ErrorMessage = "缺少 OUTLET_ID。";
                    return result;
                }

                if (string.IsNullOrWhiteSpace(jbId))
                {
                    result.ErrorMessage = "缺少 JB_ID。";
                    return result;
                }

                TrayNetworkScanResult scanResult = new TrayNetworkScanResult();
                QtoTrayNetwork network = QtoTrayNetwork.Build(transaction, database, tolerance, scanResult);
                List<Point3d> routePoints;
                string errorMessage;

                if (!network.TryFindPath(outlet.Position, junctionBox.Position, out routePoints, out errorMessage))
                {
                    result.ErrorMessage = errorMessage;
                    return result;
                }

                BlockTableRecord modelSpace = GetModelSpace(transaction, database);
                modelSpace.UpgradeOpen();
                Polyline wire = CreatePolylineFromPoints(routePoints);
                modelSpace.AppendEntity(wire);
                transaction.AddNewlyCreatedDBObject(wire, true);
                QtoLayerHelper.MoveEntityToLayer(wire, database, transaction, QtoLayerHelper.WireLayerName);

                Dictionary<string, string> data = BuildWireXData(
                    wire,
                    transaction,
                    database,
                    outletId,
                    jbId,
                    system,
                    cableType,
                    QtoXDataHelper.RouteModeTrayNetwork,
                    QtoXDataHelper.RouteStatusDraft);
                QtoXDataHelper.SetXData(wire, database, transaction, data);

                result.Success = true;
                result.WireObjectId = wire.ObjectId;
                result.LengthM = QtoGeometryHelper.ConvertMmToM(QtoGeometryHelper.GetPolylineLength(wire));
                transaction.Commit();
            }

            return result;
        }

        private static int GetNextOutletNumber(Transaction transaction, Database database)
        {
            int maxNumber = 0;
            List<OutletInfo> outlets = QtoScanner.GetAllQtoOutlets(transaction, database);

            foreach (OutletInfo outlet in outlets)
            {
                maxNumber = Math.Max(maxNumber, ParseNumberWithPrefix(outlet.OutletId, "O-"));
            }

            return maxNumber + 1;
        }

        private static int GetNextJunctionBoxNumber(Transaction transaction, Database database)
        {
            int maxNumber = 0;
            List<JunctionBoxInfo> junctionBoxes = QtoScanner.GetAllQtoJunctionBoxes(transaction, database);

            foreach (JunctionBoxInfo junctionBox in junctionBoxes)
            {
                maxNumber = Math.Max(maxNumber, ParseNumberWithPrefix(junctionBox.JbId, "JB-"));
            }

            return maxNumber + 1;
        }

        private static int GetNextRouteNumber(Transaction transaction, Database database)
        {
            int maxNumber = 0;
            List<WireInfo> wires = QtoScanner.GetAllQtoWires(transaction, database);

            foreach (WireInfo wire in wires)
            {
                maxNumber = Math.Max(maxNumber, ParseNumberWithPrefix(wire.RouteId, "ROUTE-"));
            }

            return maxNumber + 1;
        }

        private static int GetNextTrayNumber(Transaction transaction, Database database)
        {
            int maxNumber = 0;
            List<TrayInfo> trays = QtoScanner.GetAllQtoTrays(transaction, database);

            foreach (TrayInfo tray in trays)
            {
                maxNumber = Math.Max(maxNumber, ParseNumberWithPrefix(tray.TrayId, "TRAY-"));
            }

            return maxNumber + 1;
        }

        private static int ParseNumberWithPrefix(string value, string prefix)
        {
            if (string.IsNullOrWhiteSpace(value) || string.IsNullOrWhiteSpace(prefix))
            {
                return 0;
            }

            if (!value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }

            int number;

            if (int.TryParse(value.Substring(prefix.Length), out number))
            {
                return number;
            }

            return 0;
        }

        private static List<JunctionBoxSummaryInfo> BuildJunctionBoxSummaries(List<OutletInfo> outlets, List<WireInfo> wires)
        {
            Dictionary<string, JunctionBoxSummaryInfo> summaries = new Dictionary<string, JunctionBoxSummaryInfo>(StringComparer.OrdinalIgnoreCase);

            if (outlets != null)
            {
                foreach (OutletInfo outlet in outlets)
                {
                    if (string.IsNullOrWhiteSpace(outlet.JbId))
                    {
                        continue;
                    }

                    JunctionBoxSummaryInfo summary = GetOrCreateSummary(summaries, outlet.JbId, outlet.System, outlet.CableType);
                    summary.OutletCount++;
                }
            }

            if (wires != null)
            {
                foreach (WireInfo wire in wires)
                {
                    if (string.IsNullOrWhiteSpace(wire.JbId))
                    {
                        continue;
                    }

                    JunctionBoxSummaryInfo summary = GetOrCreateSummary(summaries, wire.JbId, wire.System, wire.CableType);
                    summary.WireCount++;
                    summary.TotalLengthM += wire.LengthM;
                }
            }

            List<JunctionBoxSummaryInfo> result = new List<JunctionBoxSummaryInfo>(summaries.Values);
            result.Sort(CompareJunctionBoxSummary);
            return result;
        }

        private static JunctionBoxSummaryInfo GetOrCreateSummary(Dictionary<string, JunctionBoxSummaryInfo> summaries, string jbId, string system, string cableType)
        {
            string key = (jbId ?? string.Empty) + "|" + (system ?? string.Empty) + "|" + (cableType ?? string.Empty);
            JunctionBoxSummaryInfo summary;

            if (summaries.TryGetValue(key, out summary))
            {
                return summary;
            }

            summary = new JunctionBoxSummaryInfo();
            summary.JbId = jbId ?? string.Empty;
            summary.System = system ?? string.Empty;
            summary.CableType = cableType ?? string.Empty;
            summaries.Add(key, summary);
            return summary;
        }

        private static int CompareJunctionBoxSummary(JunctionBoxSummaryInfo left, JunctionBoxSummaryInfo right)
        {
            int jbCompare = string.Compare(left.JbId, right.JbId, StringComparison.OrdinalIgnoreCase);

            if (jbCompare != 0)
            {
                return jbCompare;
            }

            int systemCompare = string.Compare(left.System, right.System, StringComparison.OrdinalIgnoreCase);

            if (systemCompare != 0)
            {
                return systemCompare;
            }

            return string.Compare(left.CableType, right.CableType, StringComparison.OrdinalIgnoreCase);
        }

        private static string PromptStringWithDefault(Editor editor, string message, string defaultValue)
        {
            PromptStringOptions options = new PromptStringOptions(message + " <" + defaultValue + ">\uff1a");
            options.AllowSpaces = true;
            options.DefaultValue = defaultValue;
            options.UseDefaultValue = true;

            PromptResult result = editor.GetString(options);

            if (result.Status != PromptStatus.OK)
            {
                return defaultValue;
            }

            if (string.IsNullOrWhiteSpace(result.StringResult))
            {
                return defaultValue;
            }

            return result.StringResult.Trim();
        }

        private static bool PromptYesNo(Editor editor, string message, bool defaultValue)
        {
            System.Windows.Forms.MessageBoxButtons buttons = System.Windows.Forms.MessageBoxButtons.YesNo;
            System.Windows.Forms.MessageBoxIcon icon = System.Windows.Forms.MessageBoxIcon.Question;
            System.Windows.Forms.MessageBoxDefaultButton defaultButton = defaultValue ? System.Windows.Forms.MessageBoxDefaultButton.Button1 : System.Windows.Forms.MessageBoxDefaultButton.Button2;
            System.Windows.Forms.DialogResult result = System.Windows.Forms.MessageBox.Show(
                message.Replace("[Yes/No]", string.Empty).Trim(),
                "\u8acb\u78ba\u8a8d",
                buttons,
                icon,
                defaultButton);

            return result == System.Windows.Forms.DialogResult.Yes;
        }

        private static ExportScopeSelection PromptExportScope(Editor editor, string reportName)
        {
            ExportScopeMode mode = ShowExportScopeDialog(reportName);

            if (mode == ExportScopeMode.Cancel)
            {
                return null;
            }

            ExportScopeSelection selection = new ExportScopeSelection();
            selection.Mode = mode;
            selection.ObjectIds = new HashSet<ObjectId>();

            if (mode == ExportScopeMode.All)
            {
                return selection;
            }

            PromptSelectionOptions options = new PromptSelectionOptions();
            options.MessageForAdding = "\n請框選要匯出的範圍：";
            PromptSelectionResult result = editor.GetSelection(options);

            if (result.Status != PromptStatus.OK)
            {
                return null;
            }

            foreach (SelectedObject selectedObject in result.Value)
            {
                if (selectedObject != null)
                {
                    selection.ObjectIds.Add(selectedObject.ObjectId);
                }
            }

            return selection;
        }

        private static ExportScopeMode ShowExportScopeDialog(string reportName)
        {
            ExportScopeMode selectedMode = ExportScopeMode.Cancel;

            using (System.Windows.Forms.Form form = new System.Windows.Forms.Form())
            using (System.Windows.Forms.Label label = new System.Windows.Forms.Label())
            using (System.Windows.Forms.Button allButton = new System.Windows.Forms.Button())
            using (System.Windows.Forms.Button selectionButton = new System.Windows.Forms.Button())
            using (System.Windows.Forms.Button cancelButton = new System.Windows.Forms.Button())
            {
                form.Text = "選擇匯出範圍";
                form.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
                form.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
                form.ClientSize = new System.Drawing.Size(360, 132);
                form.MaximizeBox = false;
                form.MinimizeBox = false;
                form.ShowInTaskbar = false;

                label.Text = "請選擇「" + (reportName ?? "報表") + "」的匯出範圍：";
                label.AutoSize = false;
                label.Location = new System.Drawing.Point(16, 16);
                label.Size = new System.Drawing.Size(328, 32);

                allButton.Text = "全部匯出";
                allButton.Location = new System.Drawing.Point(16, 70);
                allButton.Size = new System.Drawing.Size(96, 32);
                allButton.Click += delegate
                {
                    selectedMode = ExportScopeMode.All;
                    form.DialogResult = System.Windows.Forms.DialogResult.OK;
                    form.Close();
                };

                selectionButton.Text = "框選範圍";
                selectionButton.Location = new System.Drawing.Point(132, 70);
                selectionButton.Size = new System.Drawing.Size(96, 32);
                selectionButton.Click += delegate
                {
                    selectedMode = ExportScopeMode.Selection;
                    form.DialogResult = System.Windows.Forms.DialogResult.OK;
                    form.Close();
                };

                cancelButton.Text = "取消";
                cancelButton.Location = new System.Drawing.Point(248, 70);
                cancelButton.Size = new System.Drawing.Size(96, 32);
                cancelButton.Click += delegate
                {
                    selectedMode = ExportScopeMode.Cancel;
                    form.DialogResult = System.Windows.Forms.DialogResult.Cancel;
                    form.Close();
                };

                form.Controls.Add(label);
                form.Controls.Add(allButton);
                form.Controls.Add(selectionButton);
                form.Controls.Add(cancelButton);
                form.AcceptButton = allButton;
                form.CancelButton = cancelButton;

                Application.ShowModalDialog(form);
            }

            return selectedMode;
        }

        private static bool ObjectIdInExportScope(ObjectId objectId, ExportScopeSelection exportScope)
        {
            if (exportScope == null || exportScope.Mode == ExportScopeMode.All)
            {
                return true;
            }

            return exportScope.ObjectIds != null && exportScope.ObjectIds.Contains(objectId);
        }

        private static List<OutletInfo> FilterOutletsByScope(List<OutletInfo> outlets, ExportScopeSelection exportScope)
        {
            List<OutletInfo> filtered = new List<OutletInfo>();

            if (outlets == null)
            {
                return filtered;
            }

            foreach (OutletInfo outlet in outlets)
            {
                if (ObjectIdInExportScope(outlet.ObjectId, exportScope))
                {
                    filtered.Add(outlet);
                }
            }

            return filtered;
        }

        private static List<JunctionBoxInfo> FilterJunctionBoxesByScope(List<JunctionBoxInfo> junctionBoxes, ExportScopeSelection exportScope)
        {
            List<JunctionBoxInfo> filtered = new List<JunctionBoxInfo>();

            if (junctionBoxes == null)
            {
                return filtered;
            }

            foreach (JunctionBoxInfo junctionBox in junctionBoxes)
            {
                if (ObjectIdInExportScope(junctionBox.ObjectId, exportScope))
                {
                    filtered.Add(junctionBox);
                }
            }

            return filtered;
        }

        private static List<WireInfo> FilterWiresByScope(List<WireInfo> wires, ExportScopeSelection exportScope)
        {
            List<WireInfo> filtered = new List<WireInfo>();

            if (wires == null)
            {
                return filtered;
            }

            foreach (WireInfo wire in wires)
            {
                if (ObjectIdInExportScope(wire.ObjectId, exportScope))
                {
                    filtered.Add(wire);
                }
            }

            return filtered;
        }

        private static string PromptCsvOutputPath(Database database, string prefix)
        {
            string defaultPath = QtoCsvExporter.BuildDefaultOutputPath(database, prefix);

            using (System.Windows.Forms.SaveFileDialog dialog = new System.Windows.Forms.SaveFileDialog())
            {
                dialog.Title = "\u9078\u64c7 CSV \u8f38\u51fa\u4f4d\u7f6e";
                dialog.Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*";
                dialog.FileName = System.IO.Path.GetFileName(defaultPath);
                dialog.InitialDirectory = System.IO.Path.GetDirectoryName(defaultPath);
                dialog.OverwritePrompt = true;
                dialog.AddExtension = true;
                dialog.DefaultExt = "csv";

                if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                {
                    return null;
                }

                return dialog.FileName;
            }
        }

        private static BlockTableRecord GetModelSpace(Transaction transaction, Database database)
        {
            BlockTable blockTable = (BlockTable)transaction.GetObject(database.BlockTableId, OpenMode.ForRead);
            return (BlockTableRecord)transaction.GetObject(blockTable[BlockTableRecord.ModelSpace], OpenMode.ForRead);
        }

        private static bool LayerExists(Database database, Transaction transaction, string layerName)
        {
            if (string.IsNullOrWhiteSpace(layerName))
            {
                return false;
            }

            LayerTable layerTable = (LayerTable)transaction.GetObject(database.LayerTableId, OpenMode.ForRead);
            return layerTable.Has(layerName);
        }

        private static string GetErrorLayerName(string itemType)
        {
            if (string.Equals(itemType, "OUTLET", StringComparison.OrdinalIgnoreCase))
            {
                return ErrorOutletLayerName;
            }

            if (string.Equals(itemType, "WIRE", StringComparison.OrdinalIgnoreCase))
            {
                return ErrorWireLayerName;
            }

            if (string.Equals(itemType, "JUNCTION_BOX", StringComparison.OrdinalIgnoreCase))
            {
                return ErrorJbLayerName;
            }

            return string.Empty;
        }

        private static string GetBlockName(Transaction transaction, BlockReference blockReference)
        {
            ObjectId blockTableRecordId = blockReference.BlockTableRecord;

            if (blockReference.IsDynamicBlock)
            {
                blockTableRecordId = blockReference.DynamicBlockTableRecord;
            }

            BlockTableRecord blockTableRecord = transaction.GetObject(blockTableRecordId, OpenMode.ForRead, false) as BlockTableRecord;

            if (blockTableRecord == null)
            {
                return string.Empty;
            }

            return blockTableRecord.Name;
        }

        private static double PromptDoubleWithDefault(Editor editor, string message, double defaultValue)
        {
            PromptDoubleOptions options = new PromptDoubleOptions(message + " <" + defaultValue.ToString("0.###") + ">\uff1a");
            options.AllowNegative = false;
            options.AllowZero = false;
            options.AllowNone = true;
            options.DefaultValue = defaultValue;
            options.UseDefaultValue = true;

            PromptDoubleResult result = editor.GetDouble(options);

            if (result.Status != PromptStatus.OK)
            {
                return defaultValue;
            }

            return result.Value;
        }

        private static string BuildSequentialJbId(string input, int index)
        {
            string prefix = string.IsNullOrWhiteSpace(input) ? "JB" : input.Trim();
            int dashIndex = prefix.LastIndexOf('-');

            if (dashIndex > 0 && dashIndex + 1 < prefix.Length)
            {
                string suffix = prefix.Substring(dashIndex + 1);
                int parsedNumber;

                if (int.TryParse(suffix, out parsedNumber))
                {
                    prefix = prefix.Substring(0, dashIndex);
                }
            }

            return prefix + "-" + index.ToString("00");
        }

        private static int CompareOutletSelectionItems(OutletSelectionItem left, OutletSelectionItem right)
        {
            double yDelta = right.Y - left.Y;

            if (Math.Abs(yDelta) > OutletSortYTolerance)
            {
                return yDelta > 0 ? 1 : -1;
            }

            return left.X.CompareTo(right.X);
        }

        private static List<CheckResult> BuildWireCheckResults(List<OutletInfo> outlets, List<JunctionBoxInfo> junctionBoxes, List<WireInfo> wires)
        {
            List<CheckResult> results = new List<CheckResult>();
            Dictionary<string, bool> outletIds = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, bool> junctionBoxIds = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, int> wireCountByOutletId = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (OutletInfo outlet in outlets)
            {
                if (!string.IsNullOrWhiteSpace(outlet.OutletId) && !outletIds.ContainsKey(outlet.OutletId))
                {
                    outletIds.Add(outlet.OutletId, true);
                }
            }

            foreach (JunctionBoxInfo junctionBox in junctionBoxes)
            {
                if (!string.IsNullOrWhiteSpace(junctionBox.JbId) && !junctionBoxIds.ContainsKey(junctionBox.JbId))
                {
                    junctionBoxIds.Add(junctionBox.JbId, true);
                }
            }

            foreach (WireInfo wire in wires)
            {
                if (string.IsNullOrWhiteSpace(wire.OutletId))
                {
                    continue;
                }

                if (!wireCountByOutletId.ContainsKey(wire.OutletId))
                {
                    wireCountByOutletId.Add(wire.OutletId, 0);
                }

                wireCountByOutletId[wire.OutletId]++;
            }

            foreach (OutletInfo outlet in outlets)
            {
                List<string> errors = new List<string>();

                if (string.IsNullOrWhiteSpace(outlet.OutletId))
                {
                    errors.Add("Missing OUTLET_ID");
                }

                if (string.IsNullOrWhiteSpace(outlet.JbId))
                {
                    errors.Add("Missing JB_ID");
                }

                if (!string.IsNullOrWhiteSpace(outlet.OutletId))
                {
                    if (!wireCountByOutletId.ContainsKey(outlet.OutletId))
                    {
                        errors.Add("Missing wire");
                    }
                    else if (wireCountByOutletId[outlet.OutletId] > 1)
                    {
                        errors.Add("Multiple wires for OUTLET_ID");
                    }
                }

                results.Add(BuildCheckResult("OUTLET", outlet.ObjectId.ToString(), outlet.OutletId, outlet.JbId, outlet.System, outlet.CableType, 0.0, errors));
            }

            foreach (JunctionBoxInfo junctionBox in junctionBoxes)
            {
                List<string> errors = new List<string>();

                if (string.IsNullOrWhiteSpace(junctionBox.JbId))
                {
                    errors.Add("Missing JB_ID");
                }

                results.Add(BuildCheckResult("JUNCTION_BOX", junctionBox.ObjectId.ToString(), string.Empty, junctionBox.JbId, junctionBox.System, string.Empty, 0.0, errors));
            }

            foreach (WireInfo wire in wires)
            {
                List<string> errors = new List<string>();

                if (string.IsNullOrWhiteSpace(wire.OutletId))
                {
                    errors.Add("Missing OUTLET_ID");
                }
                else
                {
                    if (!outletIds.ContainsKey(wire.OutletId))
                    {
                        errors.Add("OUTLET_ID does not exist");
                    }

                    if (wireCountByOutletId.ContainsKey(wire.OutletId) && wireCountByOutletId[wire.OutletId] > 1)
                    {
                        errors.Add("Multiple wires for OUTLET_ID");
                    }
                }

                if (string.IsNullOrWhiteSpace(wire.JbId))
                {
                    errors.Add("Missing JB_ID");
                }
                else if (!junctionBoxIds.ContainsKey(wire.JbId))
                {
                    errors.Add("JB_ID does not exist");
                }

                if (wire.LengthM <= 0.0)
                {
                    errors.Add("Length is zero");
                }

                if (!string.Equals(wire.Layer, QtoLayerHelper.WireLayerName, StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add("Wire is not on QTO_WIRE layer");
                }

                results.Add(BuildCheckResult("WIRE", wire.ObjectId.ToString(), wire.OutletId, wire.JbId, wire.System, wire.CableType, wire.LengthM, errors));
            }

            return results;
        }

        private static CheckResult BuildCheckResult(string itemType, string objectId, string outletId, string jbId, string system, string cableType, double lengthM, List<string> errors)
        {
            CheckResult result = new CheckResult();
            result.ItemType = itemType;
            result.ObjectId = objectId;
            result.OutletId = outletId ?? string.Empty;
            result.JbId = jbId ?? string.Empty;
            result.System = system ?? string.Empty;
            result.CableType = cableType ?? string.Empty;
            result.LengthM = lengthM;

            if (errors == null || errors.Count == 0)
            {
                result.Status = "OK";
                result.ErrorMessage = string.Empty;
            }
            else
            {
                result.Status = "ERROR";
                result.ErrorMessage = string.Join("; ", errors.ToArray());
            }

            return result;
        }

        private static int CountStatus(List<CheckResult> results, string status)
        {
            int count = 0;

            foreach (CheckResult result in results)
            {
                if (string.Equals(result.Status, status, StringComparison.OrdinalIgnoreCase))
                {
                    count++;
                }
            }

            return count;
        }

        private static string BuildWireRecalcErrorMessage(WireInfo wire)
        {
            List<string> errors = new List<string>();

            if (string.IsNullOrWhiteSpace(wire.OutletId))
            {
                errors.Add("Missing OUTLET_ID");
            }

            if (string.IsNullOrWhiteSpace(wire.JbId))
            {
                errors.Add("Missing JB_ID");
            }

            if (wire.LengthM <= 0.0)
            {
                errors.Add("Length is zero");
            }

            return string.Join("; ", errors.ToArray());
        }

        private static string BuildBatchOutletErrorMessage(BatchOutletItem outlet)
        {
            List<string> errors = new List<string>();

            if (string.IsNullOrWhiteSpace(outlet.OutletId))
            {
                errors.Add("Missing OUTLET_ID");
            }

            if (string.IsNullOrWhiteSpace(outlet.JbId))
            {
                errors.Add("Missing JB_ID");
            }

            return string.Join("; ", errors.ToArray());
        }

        private static double GetOutletToWireEndpointDistance(Point3d outletPosition, BatchWireItem wire)
        {
            double startDistance = outletPosition.DistanceTo(wire.StartPoint);
            double endDistance = outletPosition.DistanceTo(wire.EndPoint);

            if (startDistance <= endDistance)
            {
                return startDistance;
            }

            return endDistance;
        }

        private static int CompareBatchMatchCandidates(BatchMatchCandidate left, BatchMatchCandidate right)
        {
            int distanceCompare = left.Distance.CompareTo(right.Distance);

            if (distanceCompare != 0)
            {
                return distanceCompare;
            }

            int outletCompare = left.OutletIndex.CompareTo(right.OutletIndex);

            if (outletCompare != 0)
            {
                return outletCompare;
            }

            return left.WireIndex.CompareTo(right.WireIndex);
        }

        private static void ShowStepMessage(string title, string completedMessage, string nextMessage)
        {
            string message = completedMessage;

            if (!string.IsNullOrWhiteSpace(nextMessage))
            {
                message = message + "\n\n" + nextMessage;
            }

            System.Windows.Forms.MessageBox.Show(
                message,
                title,
                System.Windows.Forms.MessageBoxButtons.OK,
                System.Windows.Forms.MessageBoxIcon.Information);
        }

        private static void WriteCommandError(string commandName, System.Exception ex)
        {
            Editor editor = GetEditor();

            if (editor != null)
            {
                editor.WriteMessage("\n" + commandName + " \u57f7\u884c\u5931\u6557\uff1a" + ex.Message);
            }
        }

        private class OutletSelectionItem
        {
            public ObjectId ObjectId { get; set; }
            public double X { get; set; }
            public double Y { get; set; }
            public string ExistingValue { get; set; }
        }

        private class BatchOutletItem
        {
            public ObjectId ObjectId { get; set; }
            public Point3d Position { get; set; }
            public string OutletId { get; set; }
            public string JbId { get; set; }
            public string System { get; set; }
            public string CableType { get; set; }
            public bool IsMatched { get; set; }
            public bool Reported { get; set; }
            public ObjectId MatchedWireObjectId { get; set; }
            public double MatchDistance { get; set; }
        }

        private class BatchWireItem
        {
            public ObjectId ObjectId { get; set; }
            public Point3d StartPoint { get; set; }
            public Point3d EndPoint { get; set; }
            public bool IsMatched { get; set; }
            public ObjectId MatchedOutletObjectId { get; set; }
        }

        private class BatchMatchCandidate
        {
            public int OutletIndex { get; set; }
            public int WireIndex { get; set; }
            public double Distance { get; set; }
        }

        private class ConduitPolylinePair
        {
            public Polyline Primary { get; set; }
            public Polyline Secondary { get; set; }
        }

        private enum ExportScopeMode
        {
            Cancel,
            All,
            Selection
        }

        private class ExportScopeSelection
        {
            public ExportScopeMode Mode { get; set; }
            public HashSet<ObjectId> ObjectIds { get; set; }
        }
    }
}
