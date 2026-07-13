using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.Windows;
using Forms = System.Windows.Forms;

namespace QtoWirePlugin
{
    public partial class Commands
    {
        [CommandMethod("QTO_SCOPE_DEFINE_FLOOR")]
        public void QtoScopeDefineFloor()
        {
            DefineScope(QtoXDataHelper.ScopeKindFloor, "樓層框");
        }

        [CommandMethod("QTO_SCOPE_DEFINE_SYSTEM")]
        public void QtoScopeDefineSystem()
        {
            DefineScope(QtoXDataHelper.ScopeKindSystem, "系統框");
        }

        [CommandMethod("QTO_SCOPE_APPLY_SELECTED")]
        public void QtoScopeApplySelected()
        {
            Document document = Application.DocumentManager.MdiActiveDocument;
            if (document == null)
            {
                return;
            }

            Editor editor = document.Editor;
            List<ObjectId> scopeIds = PromptScopeSelection(editor, "\n請選取要套用的樓層框或系統框：");
            if (scopeIds.Count == 0)
            {
                editor.WriteMessage("\n未選取有效的樓層/系統範圍框。");
                return;
            }

            QtoScopeApplyResult preview = QtoScopeService.ApplySelectedScopes(document.Database, scopeIds, true);
            Forms.DialogResult confirm = Forms.MessageBox.Show(
                preview.ToUserSummary() + "\r\n\r\n是否套用以上範圍規則？",
                "套用樓層/系統範圍",
                Forms.MessageBoxButtons.YesNo,
                Forms.MessageBoxIcon.Question);

            if (confirm != Forms.DialogResult.Yes)
            {
                editor.WriteMessage("\n已取消套用樓層/系統範圍。");
                return;
            }

            QtoScopeApplyResult result = QtoScopeService.ApplySelectedScopes(document.Database, scopeIds, false);
            editor.WriteMessage("\nQTO_SCOPE_APPLY_SELECTED: " + ToSingleLine(result));
        }

        [CommandMethod("QTO_SCOPE_APPLY_ALL")]
        public void QtoScopeApplyAll()
        {
            Document document = Application.DocumentManager.MdiActiveDocument;
            if (document == null)
            {
                return;
            }

            QtoScopeApplyResult preview = QtoScopeService.ApplyAllScopes(document.Database, true);
            Forms.DialogResult confirm = Forms.MessageBox.Show(
                preview.ToUserSummary() + "\r\n\r\n是否套用全部樓層/系統範圍框？",
                "套用全部樓層/系統範圍",
                Forms.MessageBoxButtons.YesNo,
                Forms.MessageBoxIcon.Question);

            if (confirm != Forms.DialogResult.Yes)
            {
                document.Editor.WriteMessage("\n已取消套用全部樓層/系統範圍。");
                return;
            }

            QtoScopeApplyResult result = QtoScopeService.ApplyAllScopes(document.Database, false);
            document.Editor.WriteMessage("\nQTO_SCOPE_APPLY_ALL: " + ToSingleLine(result));
        }

        [CommandMethod("QTO_SCOPE_CLEAR")]
        public void QtoScopeClear()
        {
            Document document = Application.DocumentManager.MdiActiveDocument;
            if (document == null)
            {
                return;
            }

            Editor editor = document.Editor;
            List<ObjectId> scopeIds = PromptScopeSelection(editor, "\n請選取要清除設定的樓層框或系統框：");
            if (scopeIds.Count == 0)
            {
                editor.WriteMessage("\n未選取有效的樓層/系統範圍框。");
                return;
            }

            QtoScopeApplyResult result = QtoScopeService.ClearScopes(document.Database, scopeIds);
            QtoScopeApplyResult refresh = QtoScopeService.ApplyAllScopes(document.Database, false);
            editor.WriteMessage("\nQTO_SCOPE_CLEAR: 已清除 " + result.ScopeCount.ToString("0") + " 個範圍框；重新套用 " + ToSingleLine(refresh));
        }

        [CommandMethod("QTO_SCOPE_DYNAMIC_ON")]
        public void QtoScopeDynamicOn()
        {
            Document document = Application.DocumentManager.MdiActiveDocument;
            QtoScopeService.DynamicEnabled = true;
            QtoSyncCommandService.StartSync();
            if (document != null)
            {
                document.Editor.WriteMessage("\n樓層/系統範圍動態套用已開啟。");
            }
        }

        [CommandMethod("QTO_SCOPE_DYNAMIC_OFF")]
        public void QtoScopeDynamicOff()
        {
            Document document = Application.DocumentManager.MdiActiveDocument;
            QtoScopeService.DynamicEnabled = false;
            if (document != null)
            {
                document.Editor.WriteMessage("\n樓層/系統範圍動態套用已關閉。");
            }
        }

        private static void DefineScope(string kind, string label)
        {
            Document document = Application.DocumentManager.MdiActiveDocument;
            if (document == null)
            {
                return;
            }

            Editor editor = document.Editor;
            PromptEntityOptions entityOptions = new PromptEntityOptions("\n請選取閉合 Polyline 作為" + label + "：");
            entityOptions.SetRejectMessage("\n範圍框必須是閉合 Polyline。");
            entityOptions.AddAllowedClass(typeof(Polyline), true);
            PromptEntityResult entityResult = editor.GetEntity(entityOptions);
            if (entityResult.Status != PromptStatus.OK)
            {
                return;
            }

            string value = PromptScopeValue(document, kind);
            if (string.IsNullOrWhiteSpace(value))
            {
                editor.WriteMessage("\n未輸入有效值，已取消。");
                return;
            }

            string scopeName = (kind == QtoXDataHelper.ScopeKindSystem ? "系統框 " : "樓層框 ") + value;
            QtoScopeApplyResult result = QtoScopeService.DefineScope(document.Database, entityResult.ObjectId, kind, value, scopeName);
            if (result.ErrorCount > 0)
            {
                editor.WriteMessage("\n" + result.ErrorMessage);
                return;
            }

            QtoSyncCommandService.StartSync();
            QtoScopeApplyResult refresh = QtoScopeService.ApplyAllScopes(document.Database, false);
            editor.WriteMessage("\n已設定" + label + "：" + value + "；已重新套用 " + ToSingleLine(refresh));
        }

        private static string PromptScopeValue(Document document, string kind)
        {
            bool isSystem = string.Equals(kind, QtoXDataHelper.ScopeKindSystem, System.StringComparison.OrdinalIgnoreCase);
            IEnumerable<string> values = isSystem ? GetSystemCodeOptions(document) : GetFloorOptions();
            string title = isSystem ? "設定系統框" : "設定樓層框";
            string label = isSystem ? "選擇或輸入系統代碼" : "選擇或輸入樓層";
            using (QtoScopeValueForm form = new QtoScopeValueForm(title, label, values))
            {
                Forms.DialogResult result = Application.ShowModalDialog(form);
                if (result != Forms.DialogResult.OK)
                {
                    return string.Empty;
                }

                return form.ScopeValue;
            }
        }

        private static IEnumerable<string> GetFloorOptions()
        {
            return new string[]
            {
                "B3F",
                "B2F",
                "B1F",
                "1F",
                "2F",
                "3F",
                "4F",
                "5F",
                "6F",
                "RF"
            };
        }

        private static IEnumerable<string> GetSystemCodeOptions(Document document)
        {
            QtoDictionaryStore dictionary = QtoDictionaryLoader.LoadDefault(document == null ? null : document.Database);
            return QtoSystemDefaults.OrderValues(dictionary == null ? null : dictionary.SystemCodes);
        }

        private static List<ObjectId> PromptScopeSelection(Editor editor, string message)
        {
            List<ObjectId> scopeIds = new List<ObjectId>();
            if (editor == null)
            {
                return scopeIds;
            }

            PromptSelectionResult implied = editor.SelectImplied();
            if (implied.Status == PromptStatus.OK && implied.Value != null && implied.Value.Count > 0)
            {
                scopeIds.AddRange(FilterScopeIds(implied.Value));
                if (scopeIds.Count > 0)
                {
                    return scopeIds;
                }
            }

            PromptSelectionOptions options = new PromptSelectionOptions();
            options.MessageForAdding = message;
            SelectionFilter filter = new SelectionFilter(new TypedValue[]
            {
                new TypedValue((int)DxfCode.Start, "LWPOLYLINE")
            });
            PromptSelectionResult selected = editor.GetSelection(options, filter);
            if (selected.Status != PromptStatus.OK || selected.Value == null)
            {
                return scopeIds;
            }

            scopeIds.AddRange(FilterScopeIds(selected.Value));
            return scopeIds;
        }

        private static List<ObjectId> FilterScopeIds(SelectionSet selectionSet)
        {
            List<ObjectId> scopeIds = new List<ObjectId>();
            Document document = Application.DocumentManager.MdiActiveDocument;
            if (selectionSet == null || document == null)
            {
                return scopeIds;
            }

            using (Transaction tr = document.Database.TransactionManager.StartTransaction())
            {
                foreach (SelectedObject selectedObject in selectionSet)
                {
                    if (selectedObject == null || selectedObject.ObjectId.IsNull)
                    {
                        continue;
                    }

                    Entity entity = tr.GetObject(selectedObject.ObjectId, OpenMode.ForRead, false) as Entity;
                    if (QtoScopeService.IsScopeEntity(entity))
                    {
                        scopeIds.Add(selectedObject.ObjectId);
                    }
                }

                tr.Commit();
            }

            return scopeIds;
        }

        private static string ToSingleLine(QtoScopeApplyResult result)
        {
            if (result == null)
            {
                return "無結果。";
            }

            return "範圍框 " + result.ScopeCount.ToString("0")
                + "，掃描 QTO " + result.ScannedQtoCount.ToString("0")
                + "，更新 " + result.UpdatedCount.ToString("0")
                + "，清空 " + result.ClearedCount.ToString("0")
                + "，跳過無 QTO " + result.SkippedNoQtoCount.ToString("0")
                + "。";
        }
    }
}
