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

    }
}
