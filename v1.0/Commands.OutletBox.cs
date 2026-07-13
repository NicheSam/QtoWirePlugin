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

    }
}
