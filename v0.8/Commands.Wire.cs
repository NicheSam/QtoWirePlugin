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

    }
}
