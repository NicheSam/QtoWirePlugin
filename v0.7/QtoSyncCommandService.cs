using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using AcApplication = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace QtoWirePlugin
{
    public sealed class QtoCommandResult
    {
        public bool Success { get; set; }
        public string UserMessage { get; set; }
        public string TechnicalDetail { get; set; }
        public int Count { get; set; }
    }

    public static class QtoSyncCommandService
    {
        private const string AutoWorkbookSuffix = "_QTO_SYNC.xlsx";
        private const string UnsavedWorkbookPrefix = "QTO_SYNC_";

        private static QtoChangeQueue changeQueue;
        private static QtoChangeWatcher changeWatcher;

        public static string CurrentExcelPath { get; set; }
        public static QtoValidationResult LastValidationResult { get; private set; }

        public static QtoCommandResult StartSync()
        {
            try
            {
                if (changeQueue == null)
                {
                    changeQueue = new QtoChangeQueue(1000);
                }

                if (changeWatcher == null)
                {
                    changeWatcher = new QtoChangeWatcher(changeQueue);
                }

                changeWatcher.Start();
                return Ok("已開始監看 CAD 變更。", 0);
            }
            catch (Exception ex)
            {
                return Fail("開始同步失敗。", ex);
            }
        }

        public static QtoCommandResult StopSync()
        {
            try
            {
                if (changeWatcher != null)
                {
                    changeWatcher.Stop();
                }

                return Ok("已停止監看 CAD 變更。", 0);
            }
            catch (Exception ex)
            {
                return Fail("停止同步失敗。", ex);
            }
        }

        public static QtoValidationResult ValidateCurrentDrawing()
        {
            Document document = AcApplication.DocumentManager.MdiActiveDocument;
            if (document == null)
            {
                return new QtoValidationResult();
            }

            using (Transaction transaction = document.Database.TransactionManager.StartTransaction())
            {
                QtoValidationResult result = QtoValidateService.ValidateDrawing(transaction, document.Database);
                transaction.Commit();
                LastValidationResult = result;
                return result;
            }
        }

        public static QtoRepairResult RepairCurrentDrawing()
        {
            Document document = AcApplication.DocumentManager.MdiActiveDocument;
            if (document == null)
            {
                return new QtoRepairResult();
            }

            using (DocumentLock documentLock = document.LockDocument())
            using (Transaction transaction = document.Database.TransactionManager.StartTransaction())
            {
                QtoRepairResult result = QtoRepairService.RepairDrawing(transaction, document.Database);
                transaction.Commit();
                return result;
            }
        }

        public static QtoExcelResult FullRebuildExcel(string workbookPath)
        {
            try
            {
                workbookPath = EnsureWorkbookPath(workbookPath);
                if (string.IsNullOrWhiteSpace(workbookPath))
                {
                    return QtoExcelResult.Fail(string.Empty, "已取消更新預算 Excel。", null);
                }

                CurrentExcelPath = workbookPath;
                List<QtoSyncRow> rows = BuildCurrentDrawingRows();
                QtoValidationResult validation = ValidateCurrentDrawing();
                List<QtoSyncLogRow> logs = new List<QtoSyncLogRow>();
                logs.Add(QtoSyncLogger.CreateInfo("FullRebuild", "已執行 CAD 到 Excel 的預算更新。", "Rows=" + rows.Count.ToString("0")));

                QtoExcelAdapter adapter = new QtoExcelAdapter();
                return adapter.FullRebuild(workbookPath, rows, validation.ReviewItems, logs, BuildDefaultSettings());
            }
            catch (Exception ex)
            {
                return QtoExcelResult.Fail(workbookPath, "更新預算 Excel 失敗，AutoCAD 可繼續使用。", ex);
            }
        }

        public static QtoCommandResult OpenCurrentExcel()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(CurrentExcelPath) || !System.IO.File.Exists(CurrentExcelPath))
                {
                    return Ok("尚未選擇可開啟的 Excel 檔案。", 0);
                }

                string technicalDetail;
                if (QtoExcelComWorkbookBridge.TryOpenWorkbookForSync(CurrentExcelPath, out technicalDetail))
                {
                    return Ok("已用同步通道開啟 Excel。", 0);
                }

                Process.Start(CurrentExcelPath);
                QtoCommandResult fallback = Ok("已用一般方式開啟 Excel；若更新仍失敗，請確認 Excel COM 可用。", 0);
                fallback.TechnicalDetail = technicalDetail;
                return fallback;
            }
            catch (Exception ex)
            {
                return Fail("開啟 Excel 失敗。", ex);
            }
        }

        public static List<QtoSyncRow> BuildCurrentDrawingRows()
        {
            List<QtoSyncRow> rows = new List<QtoSyncRow>();
            Document document = AcApplication.DocumentManager.MdiActiveDocument;
            if (document == null)
            {
                return rows;
            }

            using (DocumentLock documentLock = document.LockDocument())
            using (Transaction transaction = document.Database.TransactionManager.StartTransaction())
            {
                BlockTable blockTable = (BlockTable)transaction.GetObject(document.Database.BlockTableId, OpenMode.ForRead);
                BlockTableRecord modelSpace = (BlockTableRecord)transaction.GetObject(blockTable[BlockTableRecord.ModelSpace], OpenMode.ForRead);

                foreach (ObjectId objectId in modelSpace)
                {
                    Entity entity = transaction.GetObject(objectId, OpenMode.ForWrite, false) as Entity;
                    if (!QtoSyncIdService.IsQtoEntity(entity))
                    {
                        continue;
                    }

                    rows.Add(QtoSyncRowBuilder.EnsureAndBuildFromEntity(entity, document.Database, transaction, document.Database.Filename));
                }

                transaction.Commit();
            }

            return rows;
        }

        public static string EnsureWorkbookPath(string workbookPath)
        {
            if (!string.IsNullOrWhiteSpace(workbookPath))
            {
                return workbookPath;
            }

            string defaultPath = BuildDefaultWorkbookPath();
            string writeProblem;
            if (IsWorkbookPathWritable(defaultPath, out writeProblem))
            {
                return defaultPath;
            }

            using (SaveFileDialog dialog = new SaveFileDialog())
            {
                dialog.Title = "另存 QTO 預算 Excel";
                dialog.Filter = "Excel 檔案 (*.xlsx)|*.xlsx|所有檔案 (*.*)|*.*";
                dialog.FileName = Path.GetFileName(defaultPath);
                dialog.InitialDirectory = GetSafeDirectoryName(defaultPath);

                if (dialog.ShowDialog() != DialogResult.OK)
                {
                    return string.Empty;
                }

                return dialog.FileName;
            }
        }

        private static string BuildDefaultWorkbookPath()
        {
            string drawingPath = GetCurrentDrawingPath();
            if (!string.IsNullOrWhiteSpace(drawingPath))
            {
                string drawingDirectory = Path.GetDirectoryName(drawingPath);
                string drawingName = Path.GetFileNameWithoutExtension(drawingPath);
                if (!string.IsNullOrWhiteSpace(drawingDirectory) && !string.IsNullOrWhiteSpace(drawingName))
                {
                    return Path.Combine(drawingDirectory, drawingName + AutoWorkbookSuffix);
                }
            }

            string fallbackDirectory = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            if (string.IsNullOrWhiteSpace(fallbackDirectory) || !Directory.Exists(fallbackDirectory))
            {
                fallbackDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            }

            if (string.IsNullOrWhiteSpace(fallbackDirectory) || !Directory.Exists(fallbackDirectory))
            {
                fallbackDirectory = Environment.CurrentDirectory;
            }

            string fallbackName = UnsavedWorkbookPrefix + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".xlsx";
            return Path.Combine(fallbackDirectory, fallbackName);
        }

        private static string GetCurrentDrawingPath()
        {
            Document document = AcApplication.DocumentManager.MdiActiveDocument;
            if (document == null || document.Database == null)
            {
                return string.Empty;
            }

            string fileName = document.Database.Filename;
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return string.Empty;
            }

            if (!Path.IsPathRooted(fileName))
            {
                return string.Empty;
            }

            string extension = Path.GetExtension(fileName);
            if (!string.Equals(extension, ".dwg", StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            return fileName;
        }

        private static bool IsWorkbookPathWritable(string workbookPath, out string technicalDetail)
        {
            technicalDetail = string.Empty;
            try
            {
                if (string.IsNullOrWhiteSpace(workbookPath))
                {
                    technicalDetail = "workbookPath is null or empty.";
                    return false;
                }

                string fullPath = Path.GetFullPath(workbookPath);
                string directory = Path.GetDirectoryName(fullPath);
                if (string.IsNullOrWhiteSpace(directory))
                {
                    technicalDetail = "workbookPath directory is null or empty.";
                    return false;
                }

                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                if (File.Exists(fullPath))
                {
                    FileAttributes attributes = File.GetAttributes(fullPath);
                    if ((attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                    {
                        technicalDetail = "workbookPath is read-only: " + fullPath;
                        return false;
                    }

                    using (new FileStream(fullPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                    {
                    }

                    return true;
                }

                string probePath = Path.Combine(directory, ".qto_sync_write_probe_" + Guid.NewGuid().ToString("N") + ".tmp");
                using (new FileStream(probePath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                }

                File.Delete(probePath);
                return true;
            }
            catch (Exception ex)
            {
                technicalDetail = ex.ToString();
                return false;
            }
        }

        private static string GetSafeDirectoryName(string path)
        {
            try
            {
                string directory = Path.GetDirectoryName(Path.GetFullPath(path));
                if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
                {
                    return directory;
                }
            }
            catch
            {
            }

            return Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        }

        private static IDictionary<string, string> BuildDefaultSettings()
        {
            Dictionary<string, string> settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            settings["同步方向"] = "CAD_TO_EXCEL_WITH_EXCEL_MANUAL_FIELDS";
            settings["版本"] = "v0.7";
            settings["說明"] = "由 QtoWirePlugin v0.7 依 CAD 更新數量，並保留 Excel 人工編修欄位。";
            return settings;
        }

        private static QtoCommandResult Ok(string message, int count)
        {
            QtoCommandResult result = new QtoCommandResult();
            result.Success = true;
            result.UserMessage = message;
            result.Count = count;
            result.TechnicalDetail = string.Empty;
            return result;
        }

        private static QtoCommandResult Fail(string message, Exception ex)
        {
            QtoCommandResult result = new QtoCommandResult();
            result.Success = false;
            result.UserMessage = message;
            result.TechnicalDetail = ex == null ? string.Empty : ex.ToString();
            return result;
        }
    }
}
