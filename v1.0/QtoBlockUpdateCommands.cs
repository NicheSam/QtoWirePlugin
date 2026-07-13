using System;
using System.IO;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;

namespace QtoWirePlugin
{
    public partial class Commands
    {
        [CommandMethod("QTO_UPDATE_PROJECT_BLOCKS")]
        public void QtoUpdateProjectBlocks()
        {
            Document document = Application.DocumentManager.MdiActiveDocument;
            if (document == null) return;
            try
            {
                if (string.IsNullOrWhiteSpace(document.Database.Filename))
                {
                    Application.ShowAlertDialog("請先儲存 DWG，再執行圖塊更新。");
                    return;
                }
                string catalogPath = QtoProjectCatalogContext.GetPreferredCatalogPath(document.Database);
                if (string.IsNullOrWhiteSpace(catalogPath) || !File.Exists(catalogPath))
                {
                    Application.ShowAlertDialog("找不到本案圖塊資料庫，請先到圖塊庫管理設定。");
                    return;
                }
                QtoBlockCatalog catalog = new QtoBlockCatalogService().LoadCatalog(catalogPath);
                document.Editor.Command("_.UNDO", "_Begin");
                bool undoEnded = false;
                try
                {
                    using (QtoBlockUpdateForm form = new QtoBlockUpdateForm(document.Database, catalog))
                    {
                        QtoExternalWindowHost.ShowModal(form, null);
                    }
                }
                catch
                {
                    document.Editor.Command("_.UNDO", "_End");
                    undoEnded = true;
                    document.Editor.Command("_.UNDO", "1");
                    throw;
                }
                finally
                {
                    if (!undoEnded) document.Editor.Command("_.UNDO", "_End");
                }
            }
            catch (System.Exception ex)
            {
                WriteCommandError("QTO_UPDATE_PROJECT_BLOCKS", ex);
            }
        }
    }
}
