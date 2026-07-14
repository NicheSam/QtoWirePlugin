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

                editor.WriteMessage("\nQtoWirePlugin V1.0.1 \u5df2\u6210\u529f\u8f09\u5165\u3002");
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

        [CommandMethod("QTO_WORKFLOW_PANEL")]
        public void QtoWorkflowPanel()
        {
            try
            {
                QtoWorkflowFormHost.Show();
            }
            catch (System.Exception ex)
            {
                WriteCommandError("QTO_WORKFLOW_PANEL", ex);
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

    }
}
