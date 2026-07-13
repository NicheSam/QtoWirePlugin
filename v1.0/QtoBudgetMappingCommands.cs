using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Runtime;

namespace QtoWirePlugin
{
    public partial class Commands
    {
        [CommandMethod("QTO_BUDGET_MAPPING")]
        public void QtoBudgetMapping()
        {
            Document document = Application.DocumentManager.MdiActiveDocument;
            if (document == null) return;
            using (QtoBudgetMappingForm form = new QtoBudgetMappingForm(document.Database, QtoSyncCommandService.BuildCurrentDrawingRows()))
            {
                QtoExternalWindowHost.ShowModal(form, null);
                if (form.Changed && !string.IsNullOrWhiteSpace(QtoSyncCommandService.CurrentExcelPath))
                {
                    QtoExcelResult result = QtoSyncCommandService.FullRebuildExcel(QtoSyncCommandService.CurrentExcelPath);
                    if (!result.Success)
                    {
                        Application.ShowAlertDialog("預算對應已保存，但 Excel 更新失敗。\n" + result.UserMessage);
                        if (!string.IsNullOrWhiteSpace(result.TechnicalDetail)) document.Editor.WriteMessage("\n" + result.TechnicalDetail);
                    }
                }
            }
        }
    }
}
