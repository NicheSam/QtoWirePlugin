using System;
using System.IO;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Forms = System.Windows.Forms;

namespace QtoWirePlugin
{
    public partial class Commands
    {
        [CommandMethod("QTO_BLOCK_LIBRARY_RESCAN")]
        public void QtoBlockLibraryRescan()
        {
            try
            {
                Document document = Application.DocumentManager.MdiActiveDocument;
                if (document == null)
                {
                    return;
                }

                QtoBlockCatalogService service = new QtoBlockCatalogService();
                string legendDwgPath = PromptLegendDwgPath();
                if (string.IsNullOrWhiteSpace(legendDwgPath))
                {
                    return;
                }

                string catalogPath = PromptCatalogSavePath(service.GetDefaultCatalogPath(document.Name));
                if (string.IsNullOrWhiteSpace(catalogPath))
                {
                    return;
                }

                QtoBlockCatalogOperationResult result = service.RescanLegendAndSave(legendDwgPath, catalogPath);
                QtoProjectCatalogContext.SetCatalogPath(document.Database, catalogPath);
                Forms.MessageBox.Show(result.UserMessage + "\r\n\r\nCatalog：" + catalogPath, "重建圖塊資料庫", Forms.MessageBoxButtons.OK, Forms.MessageBoxIcon.Information);
                document.Editor.WriteMessage("\nQTO_BLOCK_LIBRARY_RESCAN: " + result.UserMessage);
            }
            catch (System.Exception ex)
            {
                WriteCommandError("QTO_BLOCK_LIBRARY_RESCAN", ex);
            }
        }

        [CommandMethod("QTO_BLOCK_LIBRARY_MANAGER")]
        public void QtoBlockLibraryManager()
        {
            try
            {
                Document document = Application.DocumentManager.MdiActiveDocument;
                if (document == null)
                {
                    return;
                }

                string initialCatalogPath = QtoProjectCatalogContext.GetPreferredCatalogPath(document.Database);
                QtoDictionaryStore dictionary = QtoDictionaryLoader.LoadDefault(document.Database);
                using (QtoBlockCatalogManagerForm form = new QtoBlockCatalogManagerForm(initialCatalogPath, dictionary))
                {
                    QtoExternalWindowHost.ShowModal(form, null);
                    if (!string.IsNullOrWhiteSpace(form.CatalogPath) && File.Exists(form.CatalogPath))
                    {
                        QtoProjectCatalogContext.SetCatalogPath(document.Database, form.CatalogPath);
                        document.Editor.WriteMessage("\nQTO_BLOCK_LIBRARY_MANAGER: 本圖面已連結圖塊資料庫 " + form.CatalogPath);
                    }
                }
            }
            catch (System.Exception ex)
            {
                WriteCommandError("QTO_BLOCK_LIBRARY_MANAGER", ex);
            }
        }

        [CommandMethod("QTO_INSERT_CATALOG_BLOCK")]
        public void QtoInsertCatalogBlock()
        {
            try
            {
                Document document = Application.DocumentManager.MdiActiveDocument;
                if (document == null)
                {
                    return;
                }

                string defaultCatalogPath = QtoProjectCatalogContext.GetPreferredCatalogPath(document.Database);
                using (QtoBlockLibraryPickerForm form = new QtoBlockLibraryPickerForm(defaultCatalogPath))
                {
                    Forms.DialogResult dialogResult = Application.ShowModalDialog(form);
                    if (dialogResult != Forms.DialogResult.OK || form.SelectedItem == null)
                    {
                        return;
                    }

                    QtoProjectCatalogContext.SetCatalogPath(document.Database, form.SelectedCatalogPath);

                    Editor editor = document.Editor;
                    string promptText = form.SelectedInsertionBaseMode == QtoBlockInsertionBaseMode.Center
                        ? "\n請指定標準圖塊圖形中心位置："
                        : "\n請指定標準圖塊插入點：";
                    PromptPointResult pointResult = editor.GetPoint(promptText);
                    if (pointResult.Status != PromptStatus.OK)
                    {
                        editor.WriteMessage("\n已取消插入標準圖塊。");
                        return;
                    }

                    QtoBlockLibraryInsertService insertService = new QtoBlockLibraryInsertService();
                    QtoBlockInsertResult result;
                    using (document.LockDocument())
                    {
                        result = insertService.InsertCatalogBlock(document.Database, form.SelectedCatalog, form.SelectedItem, pointResult.Value, form.SelectedInsertionBaseMode);
                    }

                    editor.WriteMessage("\nQTO_INSERT_CATALOG_BLOCK: " + result.UserMessage);
                }
            }
            catch (System.Exception ex)
            {
                WriteCommandError("QTO_INSERT_CATALOG_BLOCK", ex);
            }
        }

        private static string PromptLegendDwgPath()
        {
            using (Forms.OpenFileDialog dialog = new Forms.OpenFileDialog())
            {
                dialog.Title = "選擇標準圖例 DWG";
                dialog.Filter = "AutoCAD DWG (*.dwg)|*.dwg|所有檔案 (*.*)|*.*";
                if (dialog.ShowDialog() != Forms.DialogResult.OK)
                {
                    return string.Empty;
                }

                return dialog.FileName;
            }
        }

        private static string PromptCatalogSavePath(string defaultPath)
        {
            using (Forms.SaveFileDialog dialog = new Forms.SaveFileDialog())
            {
                dialog.Title = "儲存 QTO 圖塊資料庫";
                dialog.Filter = "QTO 圖塊資料庫 (*.json)|*.json|所有檔案 (*.*)|*.*";
                dialog.FileName = string.IsNullOrWhiteSpace(defaultPath) ? QtoBlockCatalogService.DefaultCatalogFileName : Path.GetFileName(defaultPath);
                dialog.InitialDirectory = ResolveInitialDirectory(defaultPath);
                if (dialog.ShowDialog() != Forms.DialogResult.OK)
                {
                    return string.Empty;
                }

                return dialog.FileName;
            }
        }

        private static string ResolveInitialDirectory(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            }

            string directory = Path.GetDirectoryName(path);
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            {
                return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            }

            return directory;
        }
    }
}
