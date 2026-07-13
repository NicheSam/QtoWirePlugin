using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;

namespace QtoWirePlugin
{
    public partial class Commands
    {
        [CommandMethod("QTO_SYNC_START")]
        public void QtoSyncStart()
        {
            WriteResult("QTO_SYNC_START", QtoSyncCommandService.StartSync());
        }

        [CommandMethod("QTO_SYNC_STOP")]
        public void QtoSyncStop()
        {
            WriteResult("QTO_SYNC_STOP", QtoSyncCommandService.StopSync());
        }

        [CommandMethod("QTO_SYNC_FULL_REBUILD")]
        public void QtoSyncFullRebuild()
        {
            QtoExcelResult result = QtoSyncCommandService.FullRebuildExcel(QtoSyncCommandService.CurrentExcelPath);
            Editor editor = Application.DocumentManager.MdiActiveDocument.Editor;
            editor.WriteMessage("\nQTO_SYNC_FULL_REBUILD: " + result.UserMessage);
            if (!string.IsNullOrWhiteSpace(result.WorkbookPath))
            {
                editor.WriteMessage("\nExcel: " + result.WorkbookPath);
            }
        }

        [CommandMethod("QTO_SYNC_OPEN_EXCEL")]
        public void QtoSyncOpenExcel()
        {
            WriteResult("QTO_SYNC_OPEN_EXCEL", QtoSyncCommandService.OpenCurrentExcel());
        }

        [CommandMethod("QTO_VALIDATE")]
        public void QtoValidate()
        {
            QtoValidationResult result = QtoSyncCommandService.ValidateCurrentDrawing();
            QtoDialogService.ShowReview(result.ReviewItems);
            Editor editor = Application.DocumentManager.MdiActiveDocument.Editor;
            editor.WriteMessage("\nQTO_VALIDATE: 已掃描 " + result.ScannedObjectCount.ToString("0") + " 個 QTO 物件，需確認 " + result.ReviewItems.Count.ToString("0") + " 項。");
        }

        [CommandMethod("QTO_REPAIR")]
        public void QtoRepair()
        {
            QtoValidationResult result = QtoSyncCommandService.ValidateCurrentDrawing();
            QtoDialogService.ShowReview(result.ReviewItems);
            Editor editor = Application.DocumentManager.MdiActiveDocument.Editor;
            editor.WriteMessage("\nQTO_REPAIR: 已開啟檢查清單，請選取要修復或人工確認的項目。");
        }

        [CommandMethod("QTO_REVIEW")]
        public void QtoReview()
        {
            QtoValidationResult result = QtoSyncCommandService.LastValidationResult ?? QtoSyncCommandService.ValidateCurrentDrawing();
            QtoDialogService.ShowReview(result.ReviewItems);
        }

        [CommandMethod("QTO_SETTINGS")]
        public void QtoSettings()
        {
            QtoSyncMainPaletteHost.Show();
        }

        private static System.Collections.Generic.List<QtoRepairPlanItem> ToRepairPlanItems(QtoRepairResult result)
        {
            System.Collections.Generic.Dictionary<string, int> counts = new System.Collections.Generic.Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);
            if (result != null)
            {
                foreach (QtoRepairItem item in result.Items)
                {
                    string key = string.IsNullOrWhiteSpace(item.IssueType) ? "自動修復" : item.IssueType;
                    counts[key] = counts.ContainsKey(key) ? counts[key] + 1 : 1;
                }
            }

            System.Collections.Generic.List<QtoRepairPlanItem> plans = new System.Collections.Generic.List<QtoRepairPlanItem>();
            foreach (System.Collections.Generic.KeyValuePair<string, int> item in counts)
            {
                plans.Add(new QtoRepairPlanItem
                {
                    IssueType = item.Key,
                    Count = item.Value,
                    RepairAction = "已由系統修復，請重新執行檢查確認。",
                    CanAutoRepair = true
                });
            }

            if (plans.Count == 0)
            {
                plans.Add(new QtoRepairPlanItem
                {
                    IssueType = "沒有可自動修復項目",
                    Count = 0,
                    RepairAction = "請回到檢查清單查看是否仍有人工確認項目。",
                    CanAutoRepair = false
                });
            }

            return plans;
        }

        private static void WriteResult(string commandName, QtoCommandResult result)
        {
            Editor editor = Application.DocumentManager.MdiActiveDocument.Editor;
            editor.WriteMessage("\n" + commandName + ": " + result.UserMessage);
            if (!result.Success && !string.IsNullOrWhiteSpace(result.TechnicalDetail))
            {
                editor.WriteMessage("\n" + result.TechnicalDetail);
            }
        }
    }
}
