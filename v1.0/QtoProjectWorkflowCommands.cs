using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Runtime;

namespace QtoWirePlugin
{
    public partial class Commands
    {
        [CommandMethod("QTO_PROJECT_SETUP")]
        public void QtoProjectSetup()
        {
            Document document = Application.DocumentManager.MdiActiveDocument;
            if (document == null) return;
            using (QtoProjectSetupForm form = new QtoProjectSetupForm(document))
            {
                QtoExternalWindowHost.ShowModal(form, null);
            }
        }

        [CommandMethod("QTO_BUDGET_COMPLETENESS")]
        public void QtoBudgetCompleteness()
        {
            Document document = Application.DocumentManager.MdiActiveDocument;
            if (document == null) return;
            using (QtoBudgetCompletenessForm form = new QtoBudgetCompletenessForm(document.Database, QtoSyncCommandService.BuildCurrentDrawingRows()))
            {
                QtoExternalWindowHost.ShowModal(form, null);
            }
        }
    }
}
