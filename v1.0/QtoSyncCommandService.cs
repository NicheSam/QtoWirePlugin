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
        private static Timer autoSyncTimer;
        private static bool autoSyncActive;
        private static bool scopeTrackingRequested;
        private static bool workbookUpdateInProgress;
        private static bool pendingAutoSync;
        private static int autoSyncRetryCount;
        private static string autoSyncDrawingIdentity;
        private static string currentExcelPath;
        private static string currentExcelDrawingIdentity;

        public static string CurrentExcelPath
        {
            get { return currentExcelPath; }
            set
            {
                currentExcelPath = value ?? string.Empty;
                currentExcelDrawingIdentity = GetDocumentIdentity(AcApplication.DocumentManager.MdiActiveDocument);
            }
        }
        public static QtoValidationResult LastValidationResult { get; private set; }
        public static bool IsAutoSyncActive { get { return autoSyncActive; } }
        public static QtoExcelResult LastAutoSyncResult { get; private set; }

        public static event EventHandler<QtoExcelSyncCompletedEventArgs> AutoSyncCompleted;

        public static QtoCommandResult StartSync()
        {
            try
            {
                RestoreWorkbookPathForActiveDrawing();
                string workbookPath = EnsureWorkbookPath(CurrentExcelPath);
                if (string.IsNullOrWhiteSpace(workbookPath))
                {
                    return Ok("已取消啟用自動同步。", 0);
                }

                LinkWorkbookToActiveDrawing(workbookPath);
                QtoExcelResult initialUpdate = FullRebuildExcel(workbookPath);
                if (!initialUpdate.Success)
                {
                    QtoCommandResult failure = new QtoCommandResult();
                    failure.Success = false;
                    failure.UserMessage = "無法啟用自動同步：初次更新 Excel 失敗。";
                    failure.TechnicalDetail = initialUpdate.TechnicalDetail;
                    return failure;
                }

                if (changeQueue == null)
                {
                    changeQueue = new QtoChangeQueue(1000);
                }

                if (changeWatcher == null)
                {
                    changeWatcher = new QtoChangeWatcher(changeQueue);
                    changeWatcher.ChangesProcessed += OnCadChangesProcessed;
                }

                EnsureAutoSyncTimer();
                pendingAutoSync = false;
                autoSyncRetryCount = 0;
                autoSyncDrawingIdentity = GetDocumentIdentity(AcApplication.DocumentManager.MdiActiveDocument);
                autoSyncActive = true;
                changeWatcher.Start();
                QtoCommandResult openResult = OpenCurrentExcel();
                QtoCommandResult started = Ok(
                    openResult.Success
                        ? "已啟用自動同步並開啟 Excel；相關 CAD 指令完成後會自動更新。"
                        : "已啟用自動同步；Excel 未自動開啟，但仍會更新檔案。",
                    initialUpdate.BudgetDraftRowCount);
                started.TechnicalDetail = openResult.TechnicalDetail;
                return started;
            }
            catch (Exception ex)
            {
                return Fail("開始同步失敗。", ex);
            }
        }

        public static void StartScopeTracking()
        {
            scopeTrackingRequested = true;
            EnsureChangeWatcherStarted();
        }

        public static void StopScopeTracking()
        {
            scopeTrackingRequested = false;
            if (!autoSyncActive && changeWatcher != null)
            {
                changeWatcher.Stop();
            }
        }

        public static QtoCommandResult StopSync()
        {
            try
            {
                autoSyncActive = false;
                pendingAutoSync = false;
                autoSyncRetryCount = 0;
                autoSyncDrawingIdentity = string.Empty;
                if (autoSyncTimer != null)
                {
                    autoSyncTimer.Stop();
                }

                if (!scopeTrackingRequested && changeWatcher != null)
                {
                    changeWatcher.Stop();
                }

                return Ok("已暫停自動同步；CAD 與 Excel 內容不會再自動刷新。", 0);
            }
            catch (Exception ex)
            {
                return Fail("停止同步失敗。", ex);
            }
        }

        public static void Shutdown()
        {
            autoSyncActive = false;
            scopeTrackingRequested = false;
            pendingAutoSync = false;
            autoSyncDrawingIdentity = string.Empty;
            if (autoSyncTimer != null)
            {
                autoSyncTimer.Stop();
                autoSyncTimer.Dispose();
                autoSyncTimer = null;
            }

            if (changeWatcher != null)
            {
                changeWatcher.ChangesProcessed -= OnCadChangesProcessed;
                changeWatcher.Stop();
                changeWatcher.Dispose();
                changeWatcher = null;
            }

            if (changeQueue != null)
            {
                changeQueue.Dispose();
                changeQueue = null;
            }
        }

        public static QtoValidationResult ValidateCurrentDrawing()
        {
            Document document = AcApplication.DocumentManager.MdiActiveDocument;
            if (document == null)
            {
                return new QtoValidationResult();
            }

            QtoValidationResult result;
            using (DocumentLock documentLock = document.LockDocument())
            using (Transaction transaction = document.Database.TransactionManager.StartTransaction())
            {
                QtoCatalogSnapshot catalog = QtoProjectCatalogContext.LoadSnapshot(document.Database);
                result = QtoValidateService.ValidateDrawing(transaction, document.Database, catalog);
                transaction.Commit();
            }

            result.ReviewItems.AddRange(QtoBlockUpdateReviewStore.Load(document.Database));
            QtoBudgetProjectData budgetProject = QtoBudgetProjectStore.Load(document.Database);
            if (budgetProject.MasterItems.Count > 0 || budgetProject.MappingRules.Count > 0)
            {
                QtoBudgetCompletenessResult completeness = QtoBudgetCompletenessService.Analyze(BuildCurrentDrawingRows(), budgetProject);
                result.ReviewItems.AddRange(QtoBudgetCompletenessService.ToReviewItems(completeness));
            }
            LastValidationResult = result;
            return result;
        }

        public static QtoRepairResult RepairCurrentDrawing()
        {
            return RepairCurrentDrawing(null);
        }

        public static QtoRepairResult RepairCurrentDrawing(IEnumerable<string> objectHandles)
        {
            Document document = AcApplication.DocumentManager.MdiActiveDocument;
            if (document == null)
            {
                return new QtoRepairResult();
            }

            using (DocumentLock documentLock = document.LockDocument())
            using (Transaction transaction = document.Database.TransactionManager.StartTransaction())
            {
                QtoRepairResult result = objectHandles == null
                    ? QtoRepairService.RepairDrawing(transaction, document.Database)
                    : QtoRepairService.RepairSelected(transaction, document.Database, objectHandles);
                transaction.Commit();
                return result;
            }
        }

        public static QtoExcelResult FullRebuildExcel(string workbookPath)
        {
            string activeIdentity = GetDocumentIdentity(AcApplication.DocumentManager.MdiActiveDocument);
            if (!string.Equals(activeIdentity, currentExcelDrawingIdentity, StringComparison.OrdinalIgnoreCase))
            {
                string stalePath = currentExcelPath;
                string linkedPath = RestoreWorkbookPathForActiveDrawing();
                if (string.IsNullOrWhiteSpace(workbookPath)
                    || string.Equals(workbookPath, stalePath, StringComparison.OrdinalIgnoreCase))
                {
                    workbookPath = linkedPath;
                }
            }

            if (workbookUpdateInProgress)
            {
                return QtoExcelResult.Fail(
                    workbookPath,
                    "Excel 正在更新，已略過重複要求。",
                    new InvalidOperationException("A workbook update is already in progress."));
            }

            workbookUpdateInProgress = true;
            try
            {
                workbookPath = EnsureWorkbookPath(workbookPath);
                if (string.IsNullOrWhiteSpace(workbookPath))
                {
                    return QtoExcelResult.Fail(string.Empty, "已取消更新預算 Excel。", null);
                }

                LinkWorkbookToActiveDrawing(workbookPath);
                List<QtoSyncRow> rows = BuildCurrentDrawingRows();
                QtoValidationResult validation = ValidateCurrentDrawing();
                List<QtoSyncLogRow> logs = new List<QtoSyncLogRow>();
                logs.Add(QtoSyncLogger.CreateInfo("FullRebuild", "已執行 CAD 到 Excel 的預算更新。", "Rows=" + rows.Count.ToString("0")));

                QtoBudgetLayoutSettings layout = QtoBudgetLayoutSettingsService.Load(workbookPath);
                QtoExcelAdapter adapter = new QtoExcelAdapter();
                Document activeDocument = AcApplication.DocumentManager.MdiActiveDocument;
                QtoBudgetProjectData budgetProject = activeDocument == null ? null : QtoBudgetProjectStore.Load(activeDocument.Database);
                QtoExcelResult result = adapter.FullRebuild(workbookPath, rows, validation.ReviewItems, logs, BuildDefaultSettings(layout), budgetProject);
                if (result.Success)
                {
                    QtoBudgetLayoutSettingsService.Set(workbookPath, layout);
                }
                return result;
            }
            catch (Exception ex)
            {
                return QtoExcelResult.Fail(workbookPath, "更新預算 Excel 失敗，AutoCAD 可繼續使用。", ex);
            }
            finally
            {
                workbookUpdateInProgress = false;
                if (changeQueue != null)
                {
                    changeQueue.Clear();
                }
            }
        }

        public static QtoCommandResult OpenCurrentExcel()
        {
            try
            {
                RestoreWorkbookPathForActiveDrawing();
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

        public static string RestoreWorkbookPathForActiveDrawing()
        {
            Document document = AcApplication.DocumentManager.MdiActiveDocument;
            string identity = GetDocumentIdentity(document);
            if (string.Equals(identity, currentExcelDrawingIdentity, StringComparison.OrdinalIgnoreCase))
            {
                return currentExcelPath ?? string.Empty;
            }

            currentExcelDrawingIdentity = identity;
            currentExcelPath = document == null
                ? string.Empty
                : QtoProjectCatalogContext.GetConfiguredWorkbookPath(document.Database);
            return currentExcelPath ?? string.Empty;
        }

        public static void LinkWorkbookToActiveDrawing(string workbookPath)
        {
            Document document = AcApplication.DocumentManager.MdiActiveDocument;
            currentExcelPath = workbookPath ?? string.Empty;
            currentExcelDrawingIdentity = GetDocumentIdentity(document);
            if (document != null && !string.IsNullOrWhiteSpace(workbookPath))
            {
                QtoProjectCatalogContext.SetWorkbookPath(document.Database, workbookPath);
            }
        }

        public static void ClearWorkbookForActiveDrawing()
        {
            Document document = AcApplication.DocumentManager.MdiActiveDocument;
            currentExcelPath = string.Empty;
            currentExcelDrawingIdentity = GetDocumentIdentity(document);
            if (document != null)
            {
                QtoProjectCatalogContext.ClearWorkbookPath(document.Database);
            }
        }

        private static void EnsureAutoSyncTimer()
        {
            if (autoSyncTimer != null)
            {
                return;
            }

            autoSyncTimer = new Timer();
            autoSyncTimer.Interval = 1200;
            autoSyncTimer.Tick += AutoSyncTimerTick;
        }

        private static void EnsureChangeWatcherStarted()
        {
            if (changeQueue == null)
            {
                changeQueue = new QtoChangeQueue(1000);
            }

            if (changeWatcher == null)
            {
                changeWatcher = new QtoChangeWatcher(changeQueue);
                changeWatcher.ChangesProcessed += OnCadChangesProcessed;
            }

            changeWatcher.Start();
        }

        private static void OnCadChangesProcessed(object sender, QtoChangeBatchEventArgs e)
        {
            if (!autoSyncActive || string.IsNullOrWhiteSpace(CurrentExcelPath) || e == null || e.Changes == null)
            {
                return;
            }

            bool hasRelevantChange = false;
            foreach (QtoCadChange change in e.Changes)
            {
                if (change != null
                    && change.Kind != QtoCadChangeKind.Error
                    && (string.IsNullOrWhiteSpace(autoSyncDrawingIdentity)
                        || string.IsNullOrWhiteSpace(change.DatabaseFileName)
                        || IsSameDrawing(autoSyncDrawingIdentity, change.DatabaseFileName)))
                {
                    hasRelevantChange = true;
                    break;
                }
            }

            if (!hasRelevantChange)
            {
                return;
            }

            EnsureAutoSyncTimer();
            pendingAutoSync = true;
            autoSyncRetryCount = 0;
            autoSyncTimer.Stop();
            autoSyncTimer.Interval = 1200;
            autoSyncTimer.Start();
        }

        private static void AutoSyncTimerTick(object sender, EventArgs e)
        {
            if (autoSyncTimer != null)
            {
                autoSyncTimer.Stop();
            }

            if (!autoSyncActive || !pendingAutoSync || workbookUpdateInProgress || string.IsNullOrWhiteSpace(CurrentExcelPath))
            {
                return;
            }

            Document activeDocument = AcApplication.DocumentManager.MdiActiveDocument;
            if (activeDocument == null)
            {
                pendingAutoSync = false;
                return;
            }

            if (!string.IsNullOrWhiteSpace(autoSyncDrawingIdentity)
                && !IsSameDrawing(autoSyncDrawingIdentity, GetDocumentIdentity(activeDocument)))
            {
                pendingAutoSync = false;
                QtoExcelResult wrongDrawingResult = QtoExcelResult.Fail(
                    CurrentExcelPath,
                    "已切換到其他圖面，本次自動同步已略過，避免更新到錯誤的 Excel。",
                    new InvalidOperationException("Active drawing no longer matches the drawing linked to automatic synchronization."));
                LastAutoSyncResult = wrongDrawingResult;
                RaiseAutoSyncCompleted(wrongDrawingResult);
                return;
            }

            if (activeDocument != null && !string.IsNullOrWhiteSpace(activeDocument.CommandInProgress))
            {
                autoSyncTimer.Interval = 750;
                autoSyncTimer.Start();
                return;
            }

            pendingAutoSync = false;
            QtoExcelResult result = FullRebuildExcel(CurrentExcelPath);
            LastAutoSyncResult = result;

            if (!result.Success && IsTemporaryExcelBusy(result) && autoSyncRetryCount < 3)
            {
                autoSyncRetryCount++;
                pendingAutoSync = true;
                autoSyncTimer.Interval = 2500;
                autoSyncTimer.Start();
                return;
            }

            autoSyncRetryCount = 0;
            RaiseAutoSyncCompleted(result);

            Document document = AcApplication.DocumentManager.MdiActiveDocument;
            if (document != null && document.Editor != null)
            {
                document.Editor.WriteMessage(result.Success
                    ? "\nQTO 已自動更新預算 Excel。"
                    : "\nQTO 自動更新 Excel 未完成；可在同步主控查看原因或使用立即重整。");
            }
        }

        private static bool IsTemporaryExcelBusy(QtoExcelResult result)
        {
            string detail = result == null ? string.Empty : result.TechnicalDetail ?? string.Empty;
            return detail.IndexOf("rejected", StringComparison.OrdinalIgnoreCase) >= 0
                || detail.IndexOf("RPC_E_CALL_REJECTED", StringComparison.OrdinalIgnoreCase) >= 0
                || detail.IndexOf("0x80010001", StringComparison.OrdinalIgnoreCase) >= 0
                || detail.IndexOf("0x8001010A", StringComparison.OrdinalIgnoreCase) >= 0
                || detail.IndexOf("拒絕", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void RaiseAutoSyncCompleted(QtoExcelResult result)
        {
            EventHandler<QtoExcelSyncCompletedEventArgs> handler = AutoSyncCompleted;
            if (handler != null)
            {
                handler(null, new QtoExcelSyncCompletedEventArgs(result, true));
            }
        }

        private static string GetDocumentIdentity(Document document)
        {
            if (document == null || document.Database == null)
            {
                return string.Empty;
            }

            string value = document.Database.Filename;
            if (string.IsNullOrWhiteSpace(value))
            {
                value = document.Name;
            }

            try
            {
                return Path.GetFullPath(value);
            }
            catch
            {
                return value ?? string.Empty;
            }
        }

        private static bool IsSameDrawing(string left, string right)
        {
            return string.Equals(
                (left ?? string.Empty).Trim(),
                (right ?? string.Empty).Trim(),
                StringComparison.OrdinalIgnoreCase);
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
            if (File.Exists(defaultPath))
            {
                return defaultPath;
            }

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

        private static IDictionary<string, string> BuildDefaultSettings(QtoBudgetLayoutSettings layout)
        {
            Dictionary<string, string> settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            settings["同步方向"] = "CAD_TO_EXCEL_WITH_EXCEL_MANUAL_FIELDS";
            settings["版本"] = "v1.0.1";
            settings["說明"] = "由 QtoWirePlugin v1.0.1 在 CAD 指令完成後自動更新數量，並保留 Excel 人工編修欄位。";
            QtoBudgetLayoutSettingsService.AddTo(settings, layout);
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
