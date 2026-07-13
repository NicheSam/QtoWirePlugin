using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;

namespace QtoWirePlugin
{
    public partial class Commands
    {
        [CommandMethod("QTO_PROPERTY_PANEL")]
        public void QtoPropertyPanel()
        {
            try
            {
                QtoPropertyPaletteHost.Show();
            }
            catch (System.Exception ex)
            {
                WriteCommandError("QTO_PROPERTY_PANEL", ex);
            }
        }

        [CommandMethod("QTO_EXPORT_BUDGET_INPUT")]
        public void QtoExportBudgetInput()
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
                List<QtoBudgetInputRow> rows;

                using (Transaction transaction = database.TransactionManager.StartTransaction())
                {
                    rows = QtoBudgetInputBuilder.BuildRows(transaction, database, dictionary);
                    transaction.Commit();
                }

                int reviewCount = CountBudgetRowsWithReview(rows);
                QtoBudgetExportOptions options;
                using (QtoBudgetExportOptionsForm optionsForm = new QtoBudgetExportOptionsForm(rows.Count, reviewCount))
                {
                    System.Windows.Forms.DialogResult result = Application.ShowModalDialog(optionsForm);
                    if (result != System.Windows.Forms.DialogResult.OK)
                    {
                        editor.WriteMessage("\n已取消匯出 QTO 預算前置 CSV。");
                        return;
                    }

                    options = optionsForm.Options;
                }

                string outputPath = PromptCsvOutputPath(database, "QTO_BUDGET_INPUT_");
                if (string.IsNullOrWhiteSpace(outputPath))
                {
                    editor.WriteMessage("\n已取消匯出 QTO 預算前置 CSV。");
                    return;
                }

                QtoCsvExporter.WriteCsvFile(outputPath, QtoBudgetInputBuilder.ToCsvRows(rows, options));
                editor.WriteMessage("\n已匯出 QTO 預算前置 CSV：" + outputPath);
                editor.WriteMessage("\n匯出筆數：" + rows.Count);
                editor.WriteMessage("\n需人工確認：" + reviewCount);
                if (!string.IsNullOrWhiteSpace(dictionary.LoadWarning))
                {
                    editor.WriteMessage("\n字典警告：" + dictionary.LoadWarning);
                }

                ShowStepMessage(
                    "QTO 預算前置 CSV 已匯出",
                    "匯出內容：" + options.BuildContentDescription() + "\n\n匯出筆數：" + rows.Count + "\n需人工確認：" + reviewCount + "\nCSV：" + outputPath,
                    "用途：匯入工作台後，用來產生預算草稿、檢查尚未列預算或需要人工確認的項目。");
            }
            catch (System.Exception ex)
            {
                WriteCommandError("QTO_EXPORT_BUDGET_INPUT", ex);
            }
        }

        private static int CountBudgetRowsWithReview(List<QtoBudgetInputRow> rows)
        {
            int count = 0;
            if (rows == null)
            {
                return count;
            }

            foreach (QtoBudgetInputRow row in rows)
            {
                if (!string.IsNullOrWhiteSpace(row.ReviewReason)
                    || string.Equals(row.MappingStatus, "needs_review", System.StringComparison.OrdinalIgnoreCase)
                    || string.Equals(row.MappingStatus, "blocked", System.StringComparison.OrdinalIgnoreCase))
                {
                    count++;
                }
            }

            return count;
        }
    }
}
