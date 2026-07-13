using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace QtoWirePlugin
{
    public class QtoExcelWorkbookBuilder
    {
        public const string BudgetDraftSheetName = "預算草稿";
        public const string QuantityMatrixSheetName = "數量統整";
        public const string SummarySheetName = "QTO_SUMMARY";
        public const string ReviewSheetName = "檢查清單";
        public const string LiveSheetName = "CAD原始資料";
        public const string LegacyReviewSheetName = "QTO_REVIEW";
        public const string LegacyLiveSheetName = "QTO_LIVE";
        public const string SyncDataSheetName = "QTO_SYNC_DATA";
        public const string LogSheetName = "QTO_LOG";
        public const string SettingsSheetName = "QTO_SETTINGS";

        public QtoExcelResult CreateOrRebuildWorkbook(
            string workbookPath,
            IList<QtoSyncRow> syncRows,
            IList<QtoReviewItem> reviewItems,
            IList<QtoSyncLogRow> logRows,
            IDictionary<string, string> settings)
        {
            if (string.IsNullOrWhiteSpace(workbookPath))
            {
                return new QtoExcelResult
                {
                    Success = false,
                    UserMessage = "未指定 Excel 同步檔路徑。",
                    TechnicalDetail = "workbookPath is null or empty."
                };
            }

            try
            {
                string fullPath = Path.GetFullPath(workbookPath);
                string directory = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                IList<QtoSyncRow> safeSyncRows = syncRows ?? new List<QtoSyncRow>();
                IList<QtoReviewItem> safeReviewItems = reviewItems ?? new List<QtoReviewItem>();
                List<QtoSyncLogRow> safeLogRows = QtoSyncLogger.SafeList(logRows);
                BudgetEditStore editStore = BudgetEditStore.Load(fullPath);
                IList<Dictionary<string, string>> openBudgetRows;
                string openBudgetReadDetail;
                if (QtoExcelComWorkbookBridge.TryReadOpenWorkbookTable(fullPath, BudgetDraftSheetName, out openBudgetRows, out openBudgetReadDetail)
                    && openBudgetRows != null
                    && openBudgetRows.Count > 0)
                {
                    editStore = BudgetEditStore.LoadFromRows(openBudgetRows);
                    safeLogRows.Add(QtoSyncLogger.CreateInfo("EXCEL_COM_READ", "已從開啟中的 Excel 讀取人工編修欄位。", "Rows=" + openBudgetRows.Count.ToString("0", CultureInfo.InvariantCulture)));
                }

                WorkbookBuildPackage package = BuildWorkbookSheets(safeSyncRows, safeReviewItems, safeLogRows, settings, fullPath, editStore);

                safeLogRows.Add(QtoSyncLogger.CreateInfo(
                    "FULL_REBUILD",
                    "Excel 預算工作簿已由 CAD 更新，並保留既有人工編修欄位。",
                    "Rows=" + safeSyncRows.Count.ToString("0", CultureInfo.InvariantCulture)
                    + "; PreservedManualEdits=" + package.PreservedManualEditCount.ToString("0", CultureInfo.InvariantCulture)));

                package = BuildWorkbookSheets(safeSyncRows, safeReviewItems, safeLogRows, settings, fullPath, editStore);
                string excelComUpdateDetail;
                bool updatedOpenWorkbook = QtoExcelComWorkbookBridge.TryUpdateOpenWorkbook(fullPath, package.Worksheets, out excelComUpdateDetail);
                bool updatedExistingWorkbook = false;
                if (updatedOpenWorkbook)
                {
                    safeLogRows.Add(QtoSyncLogger.CreateInfo("EXCEL_COM_UPDATE", "已直接更新開啟中的 Excel 工作簿。", fullPath));
                }
                else
                {
                    if (IsWorkbookLocked(fullPath))
                    {
                        throw new IOException("Excel 檔案目前已開啟且無法由 COM 直接更新。請確認 AutoCAD 與 Excel 不要一個用系統管理員、一個用一般權限開啟，並確認同步主控選到的 Excel 路徑就是目前開啟的檔案。\r\n\r\nCOM 診斷：" + (excelComUpdateDetail ?? string.Empty));
                    }

                    if (File.Exists(fullPath))
                    {
                        string existingWorkbookUpdateDetail;
                        updatedExistingWorkbook = QtoExcelComWorkbookBridge.TryUpdateExistingWorkbookFile(
                            fullPath,
                            package.Worksheets,
                            out existingWorkbookUpdateDetail);
                        if (!updatedExistingWorkbook)
                        {
                            throw new IOException(
                                "既有 Excel 無法在保留其他工作表的前提下更新，因此已停止操作，原檔案未被整本覆寫。\r\n\r\nCOM 診斷："
                                + (existingWorkbookUpdateDetail ?? excelComUpdateDetail ?? string.Empty));
                        }
                    }
                    else
                    {
                        QtoSimpleXlsxWriter.WriteWorkbook(fullPath, package.Worksheets);
                    }
                }

                string successMessage = updatedOpenWorkbook
                    ? "已直接更新開啟中的 Excel 工作簿。"
                    : updatedExistingWorkbook
                        ? "已更新既有 Excel，並保留其他人工工作表。"
                        : "Excel 預算工作簿已建立完成。";
                QtoExcelResult result = QtoExcelResult.Ok(fullPath, successMessage);
                result.Logs.AddRange(safeLogRows);
                result.PreservedManualEditCount = package.PreservedManualEditCount;
                result.BudgetDraftRowCount = package.BudgetDraftRowCount;
                result.BudgetDraftReviewCount = package.BudgetDraftReviewCount;
                return result;
            }
            catch (Exception ex)
            {
                QtoExcelResult result = QtoExcelResult.Fail(workbookPath, "建立或更新 Excel 預算工作簿失敗，AutoCAD 可繼續使用。", ex);
                result.Logs.Add(QtoSyncLogger.CreateError("FULL_REBUILD", result.UserMessage, ex));
                return result;
            }
        }

        private static WorkbookBuildPackage BuildWorkbookSheets(
            IList<QtoSyncRow> syncRows,
            IList<QtoReviewItem> reviewItems,
            IList<QtoSyncLogRow> logRows,
            IDictionary<string, string> settings,
            string fullPath,
            BudgetEditStore editStore)
        {
            QtoBudgetLayoutSettings layout = QtoBudgetLayoutSettings.FromDictionary(settings);
            BudgetDraftBuildResult budgetDraft = BuildBudgetDraft(syncRows, editStore, layout);

            WorkbookBuildPackage package = new WorkbookBuildPackage();
            package.Worksheets = new List<QtoWorksheetData>
            {
                CreateBudgetDraftWorksheet(budgetDraft.Rows, layout),
                CreateQuantityMatrixWorksheet(syncRows),
                CreateReviewWorksheet(MergeBudgetReviewItems(reviewItems, budgetDraft.ReviewRows)),
                CreateLiveWorksheet(syncRows),
                CreateSummaryWorksheet(syncRows),
                CreateSyncDataWorksheet(syncRows),
                CreateLogWorksheet(logRows),
                CreateSettingsWorksheet(settings, fullPath, budgetDraft)
            };
            package.PreservedManualEditCount = budgetDraft.PreservedManualEditCount;
            package.BudgetDraftRowCount = budgetDraft.DetailRowCount;
            package.BudgetDraftReviewCount = budgetDraft.ReviewRowCount;
            return package;
        }

        private static bool IsWorkbookLocked(string fullPath)
        {
            if (string.IsNullOrWhiteSpace(fullPath) || !File.Exists(fullPath))
            {
                return false;
            }

            try
            {
                using (new FileStream(fullPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                }

                return false;
            }
            catch (IOException)
            {
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                return true;
            }
        }

        private static QtoWorksheetData CreateBudgetDraftWorksheet(IList<BudgetDraftRow> rows, QtoBudgetLayoutSettings layout)
        {
            string[] headers = new string[]
            {
                "項次",
                "設備名稱",
                "單位",
                "數量",
                "單價",
                "複價",
                "成本單價",
                "倍數",
                "原成本",
                "工資",
                "廠牌",
                "備註",
                "確認狀態",
                "待確認原因",
                "列類型",
                "預算Key",
                "系統代碼",
                "設備類型",
                "CAD計量型態",
                "線材類型",
                "樓層",
                "區域",
                "空間",
                "來源QTO筆數"
            };

            object[,] values = CreateTable(headers, rows.Count);
            for (int i = 0; i < rows.Count; i++)
            {
                BudgetDraftRow row = rows[i];
                int r = i + 2;
                values[r, 1] = row.ItemNo;
                values[r, 2] = row.ItemName;
                values[r, 3] = row.Unit;
                values[r, 4] = row.IsDetail ? (object)row.Quantity : string.Empty;
                values[r, 5] = row.UnitPrice;
                values[r, 6] = row.IsDetail ? "=D" + r.ToString(CultureInfo.InvariantCulture) + "*E" + r.ToString(CultureInfo.InvariantCulture) : string.Empty;
                values[r, 7] = row.CostUnitPrice;
                values[r, 8] = row.Multiplier;
                values[r, 9] = row.BaseCost;
                values[r, 10] = row.Labor;
                values[r, 11] = row.Brand;
                values[r, 12] = row.Remark;
                values[r, 13] = row.ConfirmStatus;
                values[r, 14] = row.ReviewReason;
                values[r, 15] = row.RowType;
                values[r, 16] = row.BudgetKey;
                values[r, 17] = row.SystemCode;
                values[r, 18] = row.EquipmentTypeCode;
                values[r, 19] = row.CadMeasureType;
                values[r, 20] = row.CableType;
                values[r, 21] = row.Floor;
                values[r, 22] = row.Area;
                values[r, 23] = row.Space;
                values[r, 24] = row.SourceCount;
            }

            QtoWorksheetData worksheet = CreateWorksheet(BudgetDraftSheetName, values, QtoWorksheetVisibility.Visible);
            worksheet.FreezeRows = 1;
            worksheet.AutoFilter = true;
            for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
            {
                if (rows[rowIndex].HiddenByLayout)
                {
                    worksheet.HiddenRows.Add(rowIndex + 2);
                }
            }
            for (int column = 1; column <= headers.Length; column++)
            {
                bool technicalColumn = column >= 15;
                if (technicalColumn || !layout.IsColumnVisible(headers[column - 1]))
                {
                    worksheet.HiddenColumns.Add(column);
                }
            }

            return worksheet;
        }

        private static QtoWorksheetData CreateQuantityMatrixWorksheet(IList<QtoSyncRow> rows)
        {
            string[] floorOrder = new string[] { "B4F", "B3F", "B2F", "B1F", "1F", "2F", "3F", "4F", "5F", "6F", "R1F" };
            List<string> floors = new List<string>(floorOrder);
            foreach (string floor in rows.Select(r => TextOrDefault(r.Floor, "未填樓層")).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(v => v))
            {
                if (!floors.Contains(floor, StringComparer.OrdinalIgnoreCase))
                {
                    floors.Add(floor);
                }
            }

            string[] headers = new string[5 + floors.Count + 1];
            headers[0] = "系統代碼";
            headers[1] = "設備類型";
            headers[2] = "CAD計量型態";
            headers[3] = "線材類型";
            headers[4] = "單位";
            for (int i = 0; i < floors.Count; i++)
            {
                headers[5 + i] = floors[i];
            }
            headers[headers.Length - 1] = "總計";

            List<QuantityMatrixRow> matrixRows = rows
                .GroupBy(r => new
                {
                    SystemCode = TextOrDefault(r.SystemCode, "UNKNOWN"),
                    EquipmentTypeCode = TextOrDefault(r.EquipmentTypeCode, "UNKNOWN"),
                    CadMeasureType = TextOrDefault(r.CadMeasureType, "UNKNOWN"),
                    CableType = TextOrDefault(r.CableType, string.Empty),
                    Unit = TextOrDefault(r.Unit, string.Empty)
                })
                .Select(g => new QuantityMatrixRow
                {
                    SystemCode = g.Key.SystemCode,
                    EquipmentTypeCode = g.Key.EquipmentTypeCode,
                    CadMeasureType = g.Key.CadMeasureType,
                    CableType = g.Key.CableType,
                    Unit = g.Key.Unit,
                    QuantitiesByFloor = g.GroupBy(r => TextOrDefault(r.Floor, "未填樓層")).ToDictionary(x => x.Key, x => x.Sum(r => r.Quantity), StringComparer.OrdinalIgnoreCase)
                })
                .OrderBy(r => r.SystemCode)
                .ThenBy(r => r.EquipmentTypeCode)
                .ThenBy(r => r.CadMeasureType)
                .ThenBy(r => r.CableType)
                .ToList();

            object[,] values = CreateTable(headers, matrixRows.Count);
            for (int i = 0; i < matrixRows.Count; i++)
            {
                QuantityMatrixRow row = matrixRows[i];
                int r = i + 2;
                values[r, 1] = row.SystemCode;
                values[r, 2] = row.EquipmentTypeCode;
                values[r, 3] = row.CadMeasureType;
                values[r, 4] = row.CableType;
                values[r, 5] = row.Unit;
                double total = 0;
                for (int floorIndex = 0; floorIndex < floors.Count; floorIndex++)
                {
                    double quantity;
                    row.QuantitiesByFloor.TryGetValue(floors[floorIndex], out quantity);
                    values[r, 6 + floorIndex] = quantity;
                    total += quantity;
                }
                values[r, 6 + floors.Count] = total;
            }

            QtoWorksheetData worksheet = CreateWorksheet(QuantityMatrixSheetName, values, QtoWorksheetVisibility.Visible);
            worksheet.FreezeRows = 1;
            worksheet.AutoFilter = true;
            return worksheet;
        }

        private static QtoWorksheetData CreateSummaryWorksheet(IList<QtoSyncRow> rows)
        {
            string[] headers = new string[]
            {
                "系統代碼",
                "設備類型",
                "樓層",
                "區域",
                "數量",
                "單位",
                "備註",
                "待確認筆數",
                "最後更新時間"
            };

            List<QtoSummaryRow> summaryRows = BuildSummaryRows(rows);
            object[,] values = CreateTable(headers, summaryRows.Count);

            for (int i = 0; i < summaryRows.Count; i++)
            {
                QtoSummaryRow row = summaryRows[i];
                int r = i + 2;
                values[r, 1] = row.SystemCode;
                values[r, 2] = row.EquipmentType;
                values[r, 3] = row.Floor;
                values[r, 4] = row.Area;
                values[r, 5] = row.Quantity;
                values[r, 6] = row.Unit;
                values[r, 7] = row.Remark;
                values[r, 8] = row.ReviewCount;
                values[r, 9] = row.LastUpdatedAt;
            }

            return CreateWorksheet(SummarySheetName, values, QtoWorksheetVisibility.Hidden);
        }

        private static QtoWorksheetData CreateReviewWorksheet(IList<QtoReviewItem> items)
        {
            string[] headers = new string[]
            {
                "狀態",
                "嚴重性",
                "問題說明",
                "建議處理",
                "系統代碼",
                "設備類型",
                "樓層",
                "區域",
                "圖塊名稱",
                "物件識別碼",
                "技術細節",
                "ReviewId",
                "QTO_SYNC_ID",
                "可自動修復"
            };

            object[,] values = CreateTable(headers, items.Count);
            for (int i = 0; i < items.Count; i++)
            {
                QtoReviewItem item = items[i];
                int r = i + 2;
                values[r, 1] = TextOrDefault(item.Status, "待處理");
                values[r, 2] = item.SeverityDisplay;
                values[r, 3] = item.UserMessageDisplay;
                values[r, 4] = item.SuggestedActionDisplay;
                values[r, 5] = item.SystemCode;
                values[r, 6] = FirstNonEmpty(item.EquipmentTypeName, item.EquipmentTypeCode);
                values[r, 7] = item.Floor;
                values[r, 8] = item.Area;
                values[r, 9] = item.BlockName;
                values[r, 10] = item.ObjectHandle;
                values[r, 11] = item.TechnicalDetail;
                values[r, 12] = item.ReviewId;
                values[r, 13] = item.SyncId;
                values[r, 14] = item.CanAutoRepair ? "Y" : "N";
            }

            QtoWorksheetData worksheet = CreateWorksheet(ReviewSheetName, values, QtoWorksheetVisibility.Visible);
            worksheet.FreezeRows = 1;
            worksheet.AutoFilter = true;
            return worksheet;
        }

        private static QtoWorksheetData CreateLiveWorksheet(IList<QtoSyncRow> rows)
        {
            string[] headers = new string[]
            {
                "QTO_SYNC_ID",
                "來源DWG",
                "物件Handle",
                "CAD計量型態",
                "QTO編號",
                "系統代碼",
                "設備類型代碼",
                "設備類型名稱",
                "圖塊名稱",
                "圖層",
                "樓層",
                "區域",
                "空間",
                "線材類型",
                "管材類型",
                "管徑",
                "數量依據",
                "QTO數量",
                "單位",
                "長度m",
                "同步狀態",
                "待確認原因",
                "最後同步時間",
                "備註"
            };

            object[,] values = CreateTable(headers, rows.Count);
            for (int i = 0; i < rows.Count; i++)
            {
                QtoSyncRow row = rows[i];
                int r = i + 2;
                values[r, 1] = row.SyncId;
                values[r, 2] = row.SourceDwg;
                values[r, 3] = row.ObjectHandle;
                values[r, 4] = row.CadMeasureType;
                values[r, 5] = row.QtoNumber;
                values[r, 6] = row.SystemCode;
                values[r, 7] = row.EquipmentTypeCode;
                values[r, 8] = row.EquipmentTypeName;
                values[r, 9] = row.BlockName;
                values[r, 10] = row.Layer;
                values[r, 11] = row.Floor;
                values[r, 12] = row.Area;
                values[r, 13] = row.Space;
                values[r, 14] = row.CableType;
                values[r, 15] = row.ConduitType;
                values[r, 16] = row.ConduitSize;
                values[r, 17] = row.QuantityBasis;
                values[r, 18] = row.Quantity;
                values[r, 19] = row.Unit;
                values[r, 20] = row.LengthM;
                values[r, 21] = row.SyncStatus;
                values[r, 22] = row.ReviewReason;
                values[r, 23] = row.LastSyncAt;
                values[r, 24] = row.Remark;
            }

            QtoWorksheetData worksheet = CreateWorksheet(LiveSheetName, values, QtoWorksheetVisibility.Hidden);
            worksheet.AutoFitColumns = false;
            return worksheet;
        }

        private static QtoWorksheetData CreateSyncDataWorksheet(IList<QtoSyncRow> rows)
        {
            string[] headers = new string[]
            {
                "SyncId",
                "Handle",
                "SourceDwg",
                "RawXData",
                "CatalogId",
                "Fingerprint",
                "SyncStatus",
                "LastCadModifiedAt",
                "LastExcelModifiedAt",
                "LastSyncAt",
                "BlockName",
                "Layer",
                "QuantityBasis",
                "Quantity",
                "LengthM"
            };

            object[,] values = CreateTable(headers, rows.Count);
            for (int i = 0; i < rows.Count; i++)
            {
                QtoSyncRow row = rows[i];
                int r = i + 2;
                values[r, 1] = row.SyncId;
                values[r, 2] = row.ObjectHandle;
                values[r, 3] = row.SourceDwg;
                values[r, 4] = row.RawXData;
                values[r, 5] = row.CatalogId;
                values[r, 6] = row.Fingerprint;
                values[r, 7] = row.SyncStatus;
                values[r, 8] = row.LastCadModifiedAt;
                values[r, 9] = row.LastExcelModifiedAt;
                values[r, 10] = row.LastSyncAt;
                values[r, 11] = row.BlockName;
                values[r, 12] = row.Layer;
                values[r, 13] = row.QuantityBasis;
                values[r, 14] = row.Quantity;
                values[r, 15] = row.LengthM;
            }

            QtoWorksheetData worksheet = CreateWorksheet(SyncDataSheetName, values, QtoWorksheetVisibility.VeryHidden);
            worksheet.AutoFitColumns = false;
            return worksheet;
        }

        private static QtoWorksheetData CreateLogWorksheet(IList<QtoSyncLogRow> rows)
        {
            string[] headers = new string[]
            {
                "時間",
                "來源",
                "事件類型",
                "使用者訊息",
                "技術細節",
                "QTO_SYNC_ID",
                "ObjectHandle",
                "BlockName",
                "欄位",
                "舊值",
                "新值",
                "結果"
            };

            object[,] values = CreateTable(headers, rows.Count);
            for (int i = 0; i < rows.Count; i++)
            {
                QtoSyncLogRow row = rows[i];
                int r = i + 2;
                values[r, 1] = row.Time;
                values[r, 2] = row.Source;
                values[r, 3] = row.EventType;
                values[r, 4] = row.UserMessage;
                values[r, 5] = row.TechnicalDetail;
                values[r, 6] = row.SyncId;
                values[r, 7] = row.ObjectHandle;
                values[r, 8] = row.BlockName;
                values[r, 9] = row.FieldName;
                values[r, 10] = row.OldValue;
                values[r, 11] = row.NewValue;
                values[r, 12] = row.Result;
            }

            return CreateWorksheet(LogSheetName, values, QtoWorksheetVisibility.Hidden);
        }

        private static QtoWorksheetData CreateSettingsWorksheet(IDictionary<string, string> settings, string workbookPath, BudgetDraftBuildResult budgetDraft)
        {
            Dictionary<string, string> merged = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            merged["WorkbookVersion"] = "v0.8.2";
            merged["SyncDirection"] = "CAD_TO_EXCEL_WITH_EXCEL_MANUAL_FIELDS";
            merged["ExcelToCadEnabled"] = "false";
            merged["WorkbookPath"] = workbookPath;
            merged["LastFullRebuildAt"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            merged["BudgetDraftRows"] = budgetDraft.DetailRowCount.ToString("0", CultureInfo.InvariantCulture);
            merged["BudgetDraftNeedsReview"] = budgetDraft.ReviewRowCount.ToString("0", CultureInfo.InvariantCulture);
            merged["PreservedManualEdits"] = budgetDraft.PreservedManualEditCount.ToString("0", CultureInfo.InvariantCulture);

            if (settings != null)
            {
                foreach (KeyValuePair<string, string> pair in settings)
                {
                    if (!string.IsNullOrWhiteSpace(pair.Key))
                    {
                        merged[pair.Key] = pair.Value ?? string.Empty;
                    }
                }
            }

            string[] headers = new string[] { "Key", "Value" };
            object[,] values = CreateTable(headers, merged.Count);
            int index = 0;
            foreach (KeyValuePair<string, string> pair in merged.OrderBy(p => p.Key))
            {
                int r = index + 2;
                values[r, 1] = pair.Key;
                values[r, 2] = pair.Value;
                index++;
            }

            return CreateWorksheet(SettingsSheetName, values, QtoWorksheetVisibility.VeryHidden);
        }

        private static BudgetDraftBuildResult BuildBudgetDraft(
            IList<QtoSyncRow> rows,
            BudgetEditStore editStore,
            QtoBudgetLayoutSettings layout)
        {
            BudgetDraftBuildResult result = new BudgetDraftBuildResult();
            result.Rows = new List<BudgetDraftRow>();
            result.ReviewRows = new List<BudgetDraftRow>();
            layout = layout ?? QtoBudgetLayoutSettings.CreateDefault();

            var groups = rows
                .GroupBy(r => new BudgetGroupKey(
                    TextOrDefault(r.SystemCode, "UNKNOWN"),
                    TextOrDefault(r.EquipmentTypeCode, "UNKNOWN"),
                    TextOrDefault(r.CadMeasureType, "UNKNOWN"),
                    TextOrDefault(r.CableType, string.Empty),
                    TextOrDefault(r.Unit, string.Empty)))
                .OrderBy(g => layout.GetSystemSortIndex(g.Key.SystemCode))
                .ThenBy(g => g.Key.SystemCode, StringComparer.OrdinalIgnoreCase)
                .ThenBy(g => layout.GetCategorySortIndex(ResolveCategoryName(g.Key.CadMeasureType)))
                .ThenBy(g => g.Key.EquipmentTypeCode)
                .ThenBy(g => g.Key.CableType)
                .ToList();

            int systemIndex = 0;
            foreach (var systemGroup in groups
                .GroupBy(g => g.Key.SystemCode, StringComparer.OrdinalIgnoreCase)
                .OrderBy(g => layout.GetSystemSortIndex(g.Key))
                .ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
            {
                int visibleCategoryIndex = 0;
                bool systemVisible = layout.IsSystemVisible(systemGroup.Key);
                BudgetDraftRow systemRow = new BudgetDraftRow
                {
                    RowType = "系統大項",
                    ItemName = ResolveSystemName(systemGroup.Key),
                    SystemCode = systemGroup.Key,
                    ConfirmStatus = "說明列"
                };
                result.Rows.Add(systemRow);
                bool hasVisibleCategory = false;

                foreach (var categoryGroup in systemGroup
                    .GroupBy(g => ResolveCategoryName(g.Key.CadMeasureType), StringComparer.OrdinalIgnoreCase)
                    .OrderBy(g => layout.GetCategorySortIndex(g.Key))
                    .ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
                {
                    bool categoryVisible = systemVisible && layout.IsCategoryVisible(categoryGroup.Key);
                    BudgetDraftRow categoryRow = new BudgetDraftRow
                    {
                        RowType = "分類列",
                        ItemName = categoryGroup.Key,
                        SystemCode = systemGroup.Key,
                        ConfirmStatus = "說明列"
                    };
                    result.Rows.Add(categoryRow);
                    bool hasVisibleDetail = false;
                    int detailIndex = 0;
                    IEnumerable<IGrouping<BudgetGroupKey, QtoSyncRow>> orderedDetails = categoryGroup
                        .OrderBy(g => layout.ReviewRowsLast && !string.IsNullOrWhiteSpace(BuildBudgetReviewReason(g.Key, g.ToList())) ? 1 : 0)
                        .ThenBy(g => g.Key.EquipmentTypeCode, StringComparer.OrdinalIgnoreCase)
                        .ThenBy(g => g.Key.CableType, StringComparer.OrdinalIgnoreCase);

                    foreach (var group in orderedDetails)
                    {
                        detailIndex++;
                        BudgetDraftRow row = CreateBudgetDetailRow(group.Key, group.ToList(), detailIndex, editStore);
                        row.HiddenByLayout = !categoryVisible
                            || (layout.HideZeroQuantity && Math.Abs(row.Quantity) <= 0.0000001);
                        result.Rows.Add(row);
                        result.DetailRowCount++;
                        if (!row.HiddenByLayout)
                        {
                            hasVisibleDetail = true;
                        }

                        if (row.PreservedManualEdit)
                        {
                            result.PreservedManualEditCount++;
                        }

                        if (string.Equals(row.ConfirmStatus, "待確認", StringComparison.OrdinalIgnoreCase))
                        {
                            result.ReviewRowCount++;
                            result.ReviewRows.Add(row);
                        }
                    }

                    categoryRow.HiddenByLayout = !categoryVisible || !hasVisibleDetail;
                    if (!categoryRow.HiddenByLayout)
                    {
                        visibleCategoryIndex++;
                        categoryRow.ItemNo = ToAlphabet(visibleCategoryIndex);
                        hasVisibleCategory = true;
                    }
                }

                systemRow.HiddenByLayout = !systemVisible || !hasVisibleCategory;
                if (!systemRow.HiddenByLayout)
                {
                    systemIndex++;
                    systemRow.ItemNo = ToChineseNumber(systemIndex);
                }
            }

            return result;
        }

        private static BudgetDraftRow CreateBudgetDetailRow(BudgetGroupKey key, IList<QtoSyncRow> sourceRows, int detailIndex, BudgetEditStore editStore)
        {
            string budgetKey = key.ToKey();
            BudgetManualEdit edit = editStore.Find(budgetKey);
            string reviewReason = BuildBudgetReviewReason(key, sourceRows);
            bool needsReview = !string.IsNullOrWhiteSpace(reviewReason);

            BudgetDraftRow row = new BudgetDraftRow();
            row.RowType = needsReview ? "待確認列" : "正式明細列";
            row.BudgetKey = budgetKey;
            row.ItemNo = detailIndex.ToString("0", CultureInfo.InvariantCulture);
            row.ItemName = FirstNonEmpty(edit.ItemName, BuildDefaultItemName(key, sourceRows));
            row.Unit = FirstNonEmpty(edit.Unit, key.Unit);
            row.Quantity = sourceRows.Sum(r => r.Quantity);
            row.UnitPrice = edit.UnitPrice;
            row.Remark = edit.Remark;
            row.CostUnitPrice = edit.CostUnitPrice;
            row.Multiplier = FirstNonEmpty(edit.Multiplier, string.Empty);
            row.Brand = edit.Brand;
            row.BaseCost = edit.BaseCost;
            row.Labor = edit.Labor;
            row.SystemCode = key.SystemCode;
            row.EquipmentTypeCode = key.EquipmentTypeCode;
            row.CadMeasureType = key.CadMeasureType;
            row.CableType = key.CableType;
            row.Floor = "全部樓層";
            row.Area = "全部區域";
            row.Space = "全部空間";
            row.ConfirmStatus = needsReview ? "待確認" : FirstNonEmpty(edit.ConfirmStatus, "候選");
            row.ReviewReason = reviewReason;
            row.SourceCount = sourceRows.Count;
            row.PreservedManualEdit = edit.HasManualEdit;
            return row;
        }

        private static string BuildBudgetReviewReason(BudgetGroupKey key, IList<QtoSyncRow> sourceRows)
        {
            List<string> reasons = new List<string>();
            if (string.IsNullOrWhiteSpace(key.SystemCode) || TextEquals(key.SystemCode, "UNKNOWN"))
            {
                reasons.Add("缺系統代碼");
            }
            if (string.IsNullOrWhiteSpace(key.EquipmentTypeCode) || TextEquals(key.EquipmentTypeCode, "UNKNOWN"))
            {
                reasons.Add("缺設備類型");
            }
            if (string.IsNullOrWhiteSpace(key.CadMeasureType) || TextEquals(key.CadMeasureType, "UNKNOWN"))
            {
                reasons.Add("缺 CAD 計量型態");
            }
            if (sourceRows.Any(r => string.IsNullOrWhiteSpace(r.Floor)))
            {
                reasons.Add("部分物件缺樓層");
            }
            if (sourceRows.Any(r => !string.IsNullOrWhiteSpace(r.ReviewReason)))
            {
                reasons.Add("來源物件已有待確認原因");
            }

            return string.Join("；", reasons.ToArray());
        }

        private static string BuildDefaultItemName(BudgetGroupKey key, IList<QtoSyncRow> sourceRows)
        {
            string equipment = key.EquipmentTypeCode;
            QtoSyncRow first = sourceRows.FirstOrDefault();
            if (TextEquals(equipment, "UNKNOWN") && first != null && !string.IsNullOrWhiteSpace(first.BlockName))
            {
                equipment = first.BlockName;
            }

            if (!string.IsNullOrWhiteSpace(key.CableType))
            {
                return equipment + " / " + key.CableType;
            }

            return equipment;
        }

        private static IList<QtoReviewItem> MergeBudgetReviewItems(IList<QtoReviewItem> originalItems, IList<BudgetDraftRow> budgetReviewRows)
        {
            List<QtoReviewItem> items = new List<QtoReviewItem>();
            if (originalItems != null)
            {
                items.AddRange(originalItems);
            }

            int index = 0;
            foreach (BudgetDraftRow row in budgetReviewRows)
            {
                index++;
                items.Add(new QtoReviewItem
                {
                    ReviewId = "BUDGET-" + index.ToString("000", CultureInfo.InvariantCulture),
                    Status = "待處理",
                    Severity = "warning",
                    IssueType = "BudgetMappingNeedsReview",
                    Category = "budget",
                    UserMessage = "預算草稿列需要人工確認。",
                    SuggestedAction = "請在預算草稿補齊設備名稱、確認狀態或預算對應。",
                    TechnicalDetail = "BudgetKey=" + row.BudgetKey + "; Reason=" + row.ReviewReason,
                    SystemCode = row.SystemCode,
                    EquipmentTypeCode = row.EquipmentTypeCode,
                    EquipmentTypeName = row.ItemName,
                    Floor = row.Floor,
                    Area = row.Area
                });
            }

            return items;
        }

        private static List<QtoSummaryRow> BuildSummaryRows(IList<QtoSyncRow> rows)
        {
            return rows
                .GroupBy(r => new
                {
                    SystemCode = TextOrDefault(r.SystemCode, "UNKNOWN"),
                    EquipmentType = TextOrDefault(FirstNonEmpty(r.EquipmentTypeName, r.EquipmentTypeCode), "UNKNOWN"),
                    Floor = TextOrDefault(r.Floor, "未填樓層"),
                    Area = TextOrDefault(r.Area, "未填區域"),
                    Unit = TextOrDefault(r.Unit, string.Empty)
                })
                .Select(g => new QtoSummaryRow
                {
                    SystemCode = g.Key.SystemCode,
                    EquipmentType = g.Key.EquipmentType,
                    Floor = g.Key.Floor,
                    Area = g.Key.Area,
                    Quantity = g.Sum(r => r.Quantity),
                    Unit = g.Key.Unit,
                    Remark = "來源物件 " + g.Count().ToString("0", CultureInfo.InvariantCulture) + " 筆",
                    ReviewCount = g.Count(r => !string.IsNullOrWhiteSpace(r.ReviewReason)),
                    LastUpdatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                })
                .OrderBy(r => r.SystemCode)
                .ThenBy(r => r.EquipmentType)
                .ThenBy(r => r.Floor)
                .ThenBy(r => r.Area)
                .ToList();
        }

        private static object[,] CreateTable(string[] headers, int dataRowCount)
        {
            object[,] values = new object[Math.Max(2, dataRowCount + 2), headers.Length + 1];
            for (int i = 0; i < headers.Length; i++)
            {
                values[1, i + 1] = headers[i];
            }

            return values;
        }

        private static QtoWorksheetData CreateWorksheet(string name, object[,] values, QtoWorksheetVisibility visibility)
        {
            return new QtoWorksheetData
            {
                Name = name,
                Values = values,
                Visibility = visibility
            };
        }

        private static string ResolveSystemName(string systemCode)
        {
            Dictionary<string, string> names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "弱電", "弱電系統設備工程" },
                { "停管", "停車管理系統設備工程" },
                { "資訊", "資訊系統設備工程" },
                { "TV", "電視系統設備工程" },
                { "BA", "建築自動化系統工程" },
                { "視聽音響", "視聽音響系統工程" },
                { "緊急廣播", "緊急廣播系統工程" },
                { "ACS", "門禁管制系統設備工程" },
                { "BMS", "中央監控系統工程" },
                { "CCTV", "監視系統設備工程" },
                { "ELV_WIRE", "弱電配管配線工程" },
                { "EMS", "能源管理系統工程" },
                { "IAQ", "空氣品質監測系統工程" },
                { "LAN", "資訊網路系統設備工程" },
                { "PA", "公共廣播系統工程" },
                { "PARK", "停車場收費管理設備工程" },
                { "TEL", "電話系統設備工程" },
                { "TEST_LABOR", "測試調整與工資" },
                { "UNKNOWN", "未分類弱電設備" }
            };

            string name;
            return names.TryGetValue(TextOrDefault(systemCode, "UNKNOWN"), out name) ? name : systemCode + " 系統工程";
        }

        private static string ResolveCategoryName(string cadMeasureType)
        {
            if (TextEquals(cadMeasureType, QtoXDataHelper.TypeWire))
            {
                return "配線工程";
            }
            if (TextEquals(cadMeasureType, QtoXDataHelper.TypeConduitSegment) || TextEquals(cadMeasureType, QtoXDataHelper.TypeTray))
            {
                return "配管工程";
            }
            if (TextEquals(cadMeasureType, QtoXDataHelper.TypeOutlet)
                || TextEquals(cadMeasureType, QtoXDataHelper.TypeJunctionBox)
                || TextEquals(cadMeasureType, QtoXDataHelper.TypeDevice)
                || TextEquals(cadMeasureType, QtoXDataHelper.TypePanel))
            {
                return "設備工程";
            }

            return "其他工程";
        }

        private static string ToChineseNumber(int value)
        {
            string[] values = new string[] { "一", "二", "三", "四", "五", "六", "七", "八", "九", "十" };
            if (value >= 1 && value <= values.Length)
            {
                return values[value - 1];
            }

            return value.ToString("0", CultureInfo.InvariantCulture);
        }

        private static string ToAlphabet(int value)
        {
            if (value >= 1 && value <= 26)
            {
                return ((char)('A' + value - 1)).ToString();
            }

            return value.ToString("0", CultureInfo.InvariantCulture);
        }

        private static string TextOrDefault(string value, string defaultValue)
        {
            return string.IsNullOrWhiteSpace(value) ? defaultValue : value.Trim();
        }

        private static string FirstNonEmpty(string first, string second)
        {
            return !string.IsNullOrWhiteSpace(first) ? first.Trim() : TextOrDefault(second, string.Empty);
        }

        private static bool TextEquals(string left, string right)
        {
            return string.Equals(left ?? string.Empty, right ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        private sealed class WorkbookBuildPackage
        {
            public IList<QtoWorksheetData> Worksheets { get; set; }
            public int PreservedManualEditCount { get; set; }
            public int BudgetDraftRowCount { get; set; }
            public int BudgetDraftReviewCount { get; set; }
        }

        private sealed class BudgetDraftBuildResult
        {
            public IList<BudgetDraftRow> Rows { get; set; }
            public IList<BudgetDraftRow> ReviewRows { get; set; }
            public int DetailRowCount { get; set; }
            public int ReviewRowCount { get; set; }
            public int PreservedManualEditCount { get; set; }
        }

        private sealed class BudgetDraftRow
        {
            public string RowType { get; set; }
            public string BudgetKey { get; set; }
            public string ItemNo { get; set; }
            public string ItemName { get; set; }
            public string Unit { get; set; }
            public double Quantity { get; set; }
            public string UnitPrice { get; set; }
            public string Remark { get; set; }
            public string CostUnitPrice { get; set; }
            public string Multiplier { get; set; }
            public string Brand { get; set; }
            public string BaseCost { get; set; }
            public string Labor { get; set; }
            public string SystemCode { get; set; }
            public string EquipmentTypeCode { get; set; }
            public string CadMeasureType { get; set; }
            public string CableType { get; set; }
            public string Floor { get; set; }
            public string Area { get; set; }
            public string Space { get; set; }
            public string ConfirmStatus { get; set; }
            public string ReviewReason { get; set; }
            public int SourceCount { get; set; }
            public bool PreservedManualEdit { get; set; }
            public bool HiddenByLayout { get; set; }

            public bool IsDetail
            {
                get { return string.Equals(RowType, "正式明細列", StringComparison.OrdinalIgnoreCase) || string.Equals(RowType, "待確認列", StringComparison.OrdinalIgnoreCase); }
            }
        }

        private sealed class QuantityMatrixRow
        {
            public string SystemCode { get; set; }
            public string EquipmentTypeCode { get; set; }
            public string CadMeasureType { get; set; }
            public string CableType { get; set; }
            public string Unit { get; set; }
            public Dictionary<string, double> QuantitiesByFloor { get; set; }
        }

        private sealed class BudgetGroupKey
        {
            public BudgetGroupKey(string systemCode, string equipmentTypeCode, string cadMeasureType, string cableType, string unit)
            {
                SystemCode = systemCode;
                EquipmentTypeCode = equipmentTypeCode;
                CadMeasureType = cadMeasureType;
                CableType = cableType;
                Unit = unit;
            }

            public string SystemCode { get; private set; }
            public string EquipmentTypeCode { get; private set; }
            public string CadMeasureType { get; private set; }
            public string CableType { get; private set; }
            public string Unit { get; private set; }

            public string ToKey()
            {
                return string.Join("|", new string[]
                {
                    SystemCode ?? string.Empty,
                    EquipmentTypeCode ?? string.Empty,
                    CadMeasureType ?? string.Empty,
                    CableType ?? string.Empty,
                    "全部樓層",
                    "全部區域",
                    "全部空間",
                    Unit ?? string.Empty
                });
            }

            public override bool Equals(object obj)
            {
                BudgetGroupKey other = obj as BudgetGroupKey;
                if (other == null)
                {
                    return false;
                }

                return TextEquals(SystemCode, other.SystemCode)
                    && TextEquals(EquipmentTypeCode, other.EquipmentTypeCode)
                    && TextEquals(CadMeasureType, other.CadMeasureType)
                    && TextEquals(CableType, other.CableType)
                    && TextEquals(Unit, other.Unit);
            }

            public override int GetHashCode()
            {
                return StringComparer.OrdinalIgnoreCase.GetHashCode(SystemCode ?? string.Empty)
                    ^ StringComparer.OrdinalIgnoreCase.GetHashCode(EquipmentTypeCode ?? string.Empty)
                    ^ StringComparer.OrdinalIgnoreCase.GetHashCode(CadMeasureType ?? string.Empty)
                    ^ StringComparer.OrdinalIgnoreCase.GetHashCode(CableType ?? string.Empty)
                    ^ StringComparer.OrdinalIgnoreCase.GetHashCode(Unit ?? string.Empty);
            }
        }

        private sealed class BudgetManualEdit
        {
            public string ItemName { get; set; }
            public string Unit { get; set; }
            public string UnitPrice { get; set; }
            public string Remark { get; set; }
            public string CostUnitPrice { get; set; }
            public string Multiplier { get; set; }
            public string Brand { get; set; }
            public string BaseCost { get; set; }
            public string Labor { get; set; }
            public string ConfirmStatus { get; set; }

            public bool HasManualEdit
            {
                get
                {
                    return !string.IsNullOrWhiteSpace(ItemName)
                        || !string.IsNullOrWhiteSpace(UnitPrice)
                        || !string.IsNullOrWhiteSpace(Remark)
                        || !string.IsNullOrWhiteSpace(CostUnitPrice)
                        || !string.IsNullOrWhiteSpace(Multiplier)
                        || !string.IsNullOrWhiteSpace(Brand)
                        || !string.IsNullOrWhiteSpace(BaseCost)
                        || !string.IsNullOrWhiteSpace(Labor)
                        || !string.IsNullOrWhiteSpace(ConfirmStatus);
                }
            }
        }

        private sealed class BudgetEditStore
        {
            private readonly Dictionary<string, BudgetManualEdit> rows;

            private BudgetEditStore(Dictionary<string, BudgetManualEdit> rows)
            {
                this.rows = rows;
            }

            public static BudgetEditStore Load(string workbookPath)
            {
                if (string.IsNullOrWhiteSpace(workbookPath) || !File.Exists(workbookPath))
                {
                    return new BudgetEditStore(new Dictionary<string, BudgetManualEdit>(StringComparer.OrdinalIgnoreCase));
                }

                return LoadFromRows(QtoSimpleXlsxReader.ReadTable(workbookPath, BudgetDraftSheetName));
            }

            public static BudgetEditStore LoadFromRows(IEnumerable<Dictionary<string, string>> sourceRows)
            {
                Dictionary<string, BudgetManualEdit> rows = new Dictionary<string, BudgetManualEdit>(StringComparer.OrdinalIgnoreCase);
                if (sourceRows == null)
                {
                    return new BudgetEditStore(rows);
                }

                foreach (Dictionary<string, string> row in sourceRows)
                {
                    string key = Get(row, "預算Key");
                    if (string.IsNullOrWhiteSpace(key) || rows.ContainsKey(key))
                    {
                        continue;
                    }

                    rows[key] = new BudgetManualEdit
                    {
                        ItemName = Get(row, "設備名稱"),
                        Unit = Get(row, "單位"),
                        UnitPrice = Get(row, "單價"),
                        Remark = Get(row, "備註"),
                        CostUnitPrice = Get(row, "成本單價"),
                        Multiplier = Get(row, "倍數"),
                        Brand = Get(row, "廠牌"),
                        BaseCost = Get(row, "原成本"),
                        Labor = Get(row, "工資"),
                        ConfirmStatus = Get(row, "確認狀態")
                    };
                }

                return new BudgetEditStore(rows);
            }

            public BudgetManualEdit Find(string key)
            {
                BudgetManualEdit edit;
                if (!string.IsNullOrWhiteSpace(key) && rows.TryGetValue(key, out edit))
                {
                    return edit;
                }

                return new BudgetManualEdit();
            }

            private static string Get(Dictionary<string, string> row, string key)
            {
                string value;
                return row != null && row.TryGetValue(key, out value) ? value : string.Empty;
            }
        }
    }
}
