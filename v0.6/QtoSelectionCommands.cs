using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;

namespace QtoWirePlugin
{
    public partial class Commands
    {
        private static readonly string[] SelectUntaggedKeys =
        {
            QtoXDataHelper.KeyQtoType,
            QtoXDataHelper.KeyEquipmentTypeCode,
            QtoXDataHelper.KeySystemCode,
            QtoXDataHelper.KeyCableType,
            QtoXDataHelper.KeyFloor,
            QtoXDataHelper.KeyArea,
            QtoXDataHelper.KeySpace
        };

        [CommandMethod("QTO_SELECT_UNTAGGED")]
        public void QtoSelectUntagged()
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
                List<ObjectId> targetIds = new List<ObjectId>();
                int scannedCount = 0;

                using (Transaction transaction = database.TransactionManager.StartTransaction())
                {
                    BlockTableRecord modelSpace = GetModelSpace(transaction, database);
                    foreach (ObjectId objectId in modelSpace)
                    {
                        Entity entity = transaction.GetObject(objectId, OpenMode.ForRead, false) as Entity;
                        if (!IsQtoSelectionTarget(entity))
                        {
                            continue;
                        }

                        scannedCount++;
                        Dictionary<string, string> data = QtoXDataHelper.GetXData(entity);
                        if (!HasAnyEffectiveValue(data, SelectUntaggedKeys))
                        {
                            targetIds.Add(objectId);
                        }
                    }

                    transaction.Commit();
                }

                SetEditorSelection(editor, targetIds);
                editor.WriteMessage(
                    "\nQTO_SELECT_UNTAGGED 已選取 " + targetIds.Count
                    + " 個未標註 QTO 資訊的圖塊/線段。掃描目標數：" + scannedCount + "。");
            }
            catch (System.Exception ex)
            {
                WriteCommandError("QTO_SELECT_UNTAGGED", ex);
            }
        }

        [CommandMethod("QTO_SELECT_SAME_QTO")]
        public void QtoSelectSameQto()
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
                QtoDictionaryStore dictionary = QtoDictionaryLoader.LoadDefault(database);
                Dictionary<string, string> sourceData = TryGetFirstSelectedQtoData(editor, database);
                List<string> cableTypes = CollectExistingValues(database, QtoXDataHelper.KeyCableType);

                QtoSelectSameOptions sourceOptions = QtoSelectSameOptions.FromXData(sourceData);
                QtoSelectSameOptions selectedOptions;
                using (QtoSelectSameOptionsForm optionsForm = new QtoSelectSameOptionsForm(sourceOptions, dictionary, cableTypes))
                {
                    System.Windows.Forms.DialogResult result = Application.ShowModalDialog(optionsForm);
                    if (result != System.Windows.Forms.DialogResult.OK)
                    {
                        editor.WriteMessage("\n已取消 QTO_SELECT_SAME_QTO。");
                        return;
                    }

                    selectedOptions = optionsForm.Options;
                }

                List<ObjectId> targetIds = new List<ObjectId>();
                int scannedCount = 0;

                using (Transaction transaction = database.TransactionManager.StartTransaction())
                {
                    BlockTableRecord modelSpace = GetModelSpace(transaction, database);
                    foreach (ObjectId objectId in modelSpace)
                    {
                        Entity entity = transaction.GetObject(objectId, OpenMode.ForRead, false) as Entity;
                        if (!IsQtoSelectionTarget(entity))
                        {
                            continue;
                        }

                        scannedCount++;
                        Dictionary<string, string> data = QtoXDataHelper.GetXData(entity);
                        if (selectedOptions.Matches(data))
                        {
                            targetIds.Add(objectId);
                        }
                    }

                    transaction.Commit();
                }

                SetEditorSelection(editor, targetIds);
                editor.WriteMessage(
                    "\nQTO_SELECT_SAME_QTO 已選取 " + targetIds.Count
                    + " 個符合條件的圖塊/線段。掃描目標數：" + scannedCount + "。");
            }
            catch (System.Exception ex)
            {
                WriteCommandError("QTO_SELECT_SAME_QTO", ex);
            }
        }

        private static bool IsQtoSelectionTarget(Entity entity)
        {
            return entity is BlockReference || entity is Curve;
        }

        private static Dictionary<string, string> TryGetFirstSelectedQtoData(Editor editor, Database database)
        {
            Dictionary<string, string> empty = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (editor == null || database == null)
            {
                return empty;
            }

            PromptSelectionResult impliedSelection = editor.SelectImplied();
            if (impliedSelection.Status != PromptStatus.OK || impliedSelection.Value == null || impliedSelection.Value.Count == 0)
            {
                return empty;
            }

            ObjectId sourceId = impliedSelection.Value[0].ObjectId;
            if (sourceId.IsNull)
            {
                return empty;
            }

            using (Transaction transaction = database.TransactionManager.StartTransaction())
            {
                Entity sourceEntity = transaction.GetObject(sourceId, OpenMode.ForRead, false) as Entity;
                if (!IsQtoSelectionTarget(sourceEntity))
                {
                    transaction.Commit();
                    return empty;
                }

                Dictionary<string, string> data = QtoXDataHelper.GetXData(sourceEntity);
                transaction.Commit();
                return data;
            }
        }

        private static List<string> CollectExistingValues(Database database, string key)
        {
            List<string> values = new List<string>();
            if (database == null || string.IsNullOrWhiteSpace(key))
            {
                return values;
            }

            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (Transaction transaction = database.TransactionManager.StartTransaction())
            {
                BlockTableRecord modelSpace = GetModelSpace(transaction, database);
                foreach (ObjectId objectId in modelSpace)
                {
                    Entity entity = transaction.GetObject(objectId, OpenMode.ForRead, false) as Entity;
                    if (!IsQtoSelectionTarget(entity))
                    {
                        continue;
                    }

                    Dictionary<string, string> data = QtoXDataHelper.GetXData(entity);
                    string value;
                    if (data.TryGetValue(key, out value) && !string.IsNullOrWhiteSpace(value) && seen.Add(value.Trim()))
                    {
                        values.Add(value.Trim());
                    }
                }

                transaction.Commit();
            }

            values.Sort(StringComparer.OrdinalIgnoreCase);
            return values;
        }

        private static bool HasAnyEffectiveValue(Dictionary<string, string> data, string[] keys)
        {
            if (data == null || keys == null)
            {
                return false;
            }

            foreach (string key in keys)
            {
                string value;
                if (data.TryGetValue(key, out value) && !string.IsNullOrWhiteSpace(value))
                {
                    return true;
                }
            }

            return false;
        }

        private static void SetEditorSelection(Editor editor, List<ObjectId> targetIds)
        {
            if (editor == null)
            {
                return;
            }

            if (targetIds == null || targetIds.Count == 0)
            {
                editor.SetImpliedSelection(new ObjectId[0]);
                return;
            }

            editor.SetImpliedSelection(targetIds.ToArray());
        }
    }
}
