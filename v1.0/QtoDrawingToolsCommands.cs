using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using AcApplication = Autodesk.AutoCAD.ApplicationServices.Application;

namespace QtoWirePlugin
{
    public partial class Commands
    {
        [CommandMethod("QTO_PLACE_CATALOG_CONTINUOUS")]
        public void QtoPlaceCatalogContinuous()
        {
            try
            {
                Document document = AcApplication.DocumentManager.MdiActiveDocument;
                if (document == null) return;
                string catalogPath = QtoProjectCatalogContext.GetPreferredCatalogPath(document.Database);
                using (QtoBlockLibraryPickerForm form = new QtoBlockLibraryPickerForm(catalogPath))
                {
                    if (AcApplication.ShowModalDialog(form) != System.Windows.Forms.DialogResult.OK || form.SelectedItem == null) return;
                    QtoProjectCatalogContext.SetCatalogPath(document.Database, form.SelectedCatalogPath);
                    int count = 0;
                    while (true)
                    {
                        PromptPointOptions options = new PromptPointOptions("\n指定設備位置，按 Enter 結束：") { AllowNone = true };
                        PromptPointResult point = document.Editor.GetPoint(options);
                        if (point.Status != PromptStatus.OK) break;
                        QtoBlockInsertResult result;
                        using (document.LockDocument())
                        {
                            result = new QtoBlockLibraryInsertService().InsertCatalogBlock(document.Database, form.SelectedCatalog, form.SelectedItem, point.Value, form.SelectedInsertionBaseMode);
                        }
                        if (!result.Success) throw new InvalidOperationException(result.UserMessage + " " + result.TechnicalMessage);
                        count++;
                    }
                    document.Editor.WriteMessage("\n已連續放置標準設備 " + count + " 個。");
                }
            }
            catch (System.Exception ex) { WriteCommandError("QTO_PLACE_CATALOG_CONTINUOUS", ex); }
        }

        [CommandMethod("QTO_DRAW_QTO_PATH")]
        public void QtoDrawQtoPath()
        {
            try
            {
                Document document = AcApplication.DocumentManager.MdiActiveDocument;
                if (document == null) return;
                QtoDrawingSettings settings;
                using (QtoDrawingSettingsForm form = new QtoDrawingSettingsForm(false, document.Database))
                {
                    if (AcApplication.ShowModalDialog(form) != System.Windows.Forms.DialogResult.OK || form.Settings == null) return;
                    settings = form.Settings;
                }
                List<Point2d> points = CollectPolylinePoints(document.Editor);
                if (points.Count < 2) return;
                QtoDrawingService.CreatePath(document.Database, points, settings);
                document.Editor.WriteMessage("\n已建立 1 段 " + settings.ObjectKind + "，共 " + points.Count + " 個頂點。");
            }
            catch (System.Exception ex) { WriteCommandError("QTO_DRAW_QTO_PATH", ex); }
        }

        [CommandMethod("QTO_CONVERT_LEGACY_OBJECTS")]
        public void QtoConvertLegacyObjects()
        {
            try
            {
                Document document = AcApplication.DocumentManager.MdiActiveDocument;
                if (document == null) return;
                PromptSelectionOptions options = new PromptSelectionOptions { MessageForAdding = "\n選取要轉成 QTO 的圖塊、線段或聚合線：" };
                PromptSelectionResult selection = document.Editor.GetSelection(options);
                if (selection.Status != PromptStatus.OK) return;
                QtoDrawingSettings settings;
                using (QtoDrawingSettingsForm form = new QtoDrawingSettingsForm(true, document.Database))
                {
                    if (AcApplication.ShowModalDialog(form) != System.Windows.Forms.DialogResult.OK || form.Settings == null) return;
                    settings = form.Settings;
                }

                QtoDrawingConversionResult result = QtoDrawingService.ConvertLegacyObjects(
                    document.Database,
                    selection.Value.Cast<SelectedObject>().Select(selected => selected.ObjectId),
                    settings);
                document.Editor.WriteMessage("\n已轉換 " + result.ConvertedCount + " 個物件；跳過 " + result.SkippedCount + " 個。");
            }
            catch (System.Exception ex) { WriteCommandError("QTO_CONVERT_LEGACY_OBJECTS", ex); }
        }

        private static List<Point2d> CollectPolylinePoints(Editor editor)
        {
            List<Point2d> points = new List<Point2d>();
            while (true)
            {
                PromptPointOptions options = new PromptPointOptions(points.Count == 0 ? "\n指定起點：" : "\n指定下一點，按 Enter 完成：") { AllowNone = points.Count >= 2 };
                if (points.Count > 0)
                {
                    Point2d previous = points[points.Count - 1];
                    options.UseBasePoint = true;
                    options.BasePoint = new Point3d(previous.X, previous.Y, 0);
                    options.UseDashedLine = true;
                }
                PromptPointResult result = editor.GetPoint(options);
                if (result.Status != PromptStatus.OK) break;
                points.Add(new Point2d(result.Value.X, result.Value.Y));
            }
            return points;
        }

    }
}
