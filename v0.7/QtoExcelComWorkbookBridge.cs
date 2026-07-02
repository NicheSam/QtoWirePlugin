using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;

namespace QtoWirePlugin
{
    internal static class QtoExcelComWorkbookBridge
    {
        private const int XlSheetVisible = -1;
        private const int XlSheetHidden = 0;
        private const int XlSheetVeryHidden = 2;
        private static dynamic cachedExcel;
        private static dynamic cachedWorkbook;
        private static string cachedWorkbookPath;

        [DllImport("ole32.dll")]
        private static extern int GetRunningObjectTable(int reserved, out IRunningObjectTable runningObjectTable);

        [DllImport("ole32.dll")]
        private static extern int CreateBindCtx(int reserved, out IBindCtx bindContext);

        public static bool TryReadOpenWorkbookTable(
            string workbookPath,
            string sheetName,
            out IList<Dictionary<string, string>> rows,
            out string technicalDetail)
        {
            rows = new List<Dictionary<string, string>>();
            technicalDetail = string.Empty;

            dynamic excel = null;
            dynamic workbook = null;
            dynamic worksheet = null;
            dynamic usedRange = null;

            try
            {
                workbook = TryGetCachedWorkbook(workbookPath);
                if (workbook == null)
                {
                    excel = GetRunningExcel();
                    if (excel == null)
                    {
                        technicalDetail = "Excel is not running or AutoCAD cannot access the running Excel instance.";
                    }
                    else
                    {
                        workbook = FindOpenWorkbook(excel, workbookPath);
                    }

                    if (workbook == null)
                    {
                        workbook = FindOpenWorkbookFromRunningObjectTable(workbookPath, out technicalDetail);
                        if (workbook == null)
                        {
                            technicalDetail = BuildWorkbookNotFoundMessage(workbookPath, technicalDetail);
                            return false;
                        }
                    }
                }

                worksheet = FindWorksheet(workbook, sheetName);
                if (worksheet == null)
                {
                    technicalDetail = "Target worksheet is not found: " + sheetName;
                    return false;
                }

                usedRange = worksheet.UsedRange;
                int rowCount = Convert.ToInt32(usedRange.Rows.Count, CultureInfo.InvariantCulture);
                int columnCount = Convert.ToInt32(usedRange.Columns.Count, CultureInfo.InvariantCulture);
                if (rowCount < 2 || columnCount < 1)
                {
                    return true;
                }

                object value = usedRange.Value2;
                object[,] values = value as object[,];
                if (values == null)
                {
                    return true;
                }

                Dictionary<int, string> headers = new Dictionary<int, string>();
                for (int column = 1; column <= columnCount; column++)
                {
                    string header = ConvertCell(values[1, column]).Trim();
                    if (!string.IsNullOrWhiteSpace(header))
                    {
                        headers[column] = header;
                    }
                }

                for (int row = 2; row <= rowCount; row++)
                {
                    Dictionary<string, string> item = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    bool hasValue = false;
                    foreach (KeyValuePair<int, string> header in headers)
                    {
                        string cellValue = ConvertCell(values[row, header.Key]);
                        item[header.Value] = cellValue;
                        if (!string.IsNullOrWhiteSpace(cellValue))
                        {
                            hasValue = true;
                        }
                    }

                    if (hasValue)
                    {
                        rows.Add(item);
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                technicalDetail = ex.ToString();
                rows = new List<Dictionary<string, string>>();
                return false;
            }
            finally
            {
                Release(usedRange);
                Release(worksheet);
                if (!IsCachedWorkbook(workbook))
                {
                    Release(workbook);
                }
                Release(excel);
            }
        }

        public static bool TryUpdateOpenWorkbook(
            string workbookPath,
            IList<QtoWorksheetData> worksheets,
            out string technicalDetail)
        {
            technicalDetail = string.Empty;

            dynamic excel = null;
            dynamic workbook = null;

            try
            {
                workbook = TryGetCachedWorkbook(workbookPath);
                if (workbook == null)
                {
                    excel = GetRunningExcel();
                    if (excel == null)
                    {
                        technicalDetail = "Excel is not running or AutoCAD cannot access the running Excel instance.";
                    }
                    else
                    {
                        workbook = FindOpenWorkbook(excel, workbookPath);
                    }

                    if (workbook == null)
                    {
                        workbook = FindOpenWorkbookFromRunningObjectTable(workbookPath, out technicalDetail);
                        if (workbook == null)
                        {
                            technicalDetail = BuildWorkbookNotFoundMessage(workbookPath, technicalDetail);
                            return false;
                        }
                    }
                }

                if (worksheets != null)
                {
                    foreach (QtoWorksheetData worksheet in worksheets)
                    {
                        WriteWorksheet(workbook, worksheet);
                    }
                }

                workbook.Save();
                return true;
            }
            catch (Exception ex)
            {
                technicalDetail = ex.ToString();
                return false;
            }
            finally
            {
                if (!IsCachedWorkbook(workbook))
                {
                    Release(workbook);
                }
                Release(excel);
            }
        }

        public static bool TryOpenWorkbookForSync(string workbookPath, out string technicalDetail)
        {
            technicalDetail = string.Empty;
            if (string.IsNullOrWhiteSpace(workbookPath) || !File.Exists(workbookPath))
            {
                technicalDetail = "Workbook path does not exist: " + (workbookPath ?? string.Empty);
                return false;
            }

            try
            {
                dynamic workbook = TryGetCachedWorkbook(workbookPath);
                if (workbook != null)
                {
                    workbook.Application.Visible = true;
                    workbook.Activate();
                    return true;
                }

                dynamic excel = GetRunningExcel();
                if (excel == null)
                {
                    Type excelType = Type.GetTypeFromProgID("Excel.Application");
                    if (excelType == null)
                    {
                        technicalDetail = "Excel.Application ProgID is not available.";
                        return false;
                    }

                    excel = Activator.CreateInstance(excelType);
                }

                excel.Visible = true;
                workbook = FindOpenWorkbook(excel, workbookPath);
                if (workbook == null)
                {
                    workbook = excel.Workbooks.Open(Path.GetFullPath(workbookPath));
                }

                cachedExcel = excel;
                cachedWorkbook = workbook;
                cachedWorkbookPath = Path.GetFullPath(workbookPath);
                workbook.Activate();
                return true;
            }
            catch (Exception ex)
            {
                technicalDetail = ex.ToString();
                return false;
            }
        }

        private static dynamic TryGetCachedWorkbook(string workbookPath)
        {
            if (cachedWorkbook == null || string.IsNullOrWhiteSpace(cachedWorkbookPath))
            {
                return null;
            }

            if (!IsSameWorkbook(cachedWorkbookPath, workbookPath))
            {
                return null;
            }

            try
            {
                string fullName = Convert.ToString(cachedWorkbook.FullName, CultureInfo.InvariantCulture);
                return IsSameWorkbook(fullName, workbookPath) ? cachedWorkbook : null;
            }
            catch
            {
                cachedWorkbook = null;
                cachedWorkbookPath = string.Empty;
                return null;
            }
        }

        private static bool IsCachedWorkbook(object workbook)
        {
            return workbook != null && cachedWorkbook != null && object.ReferenceEquals(workbook, cachedWorkbook);
        }

        private static dynamic GetRunningExcel()
        {
            try
            {
                return Marshal.GetActiveObject("Excel.Application");
            }
            catch
            {
                return null;
            }
        }

        private static dynamic FindOpenWorkbook(dynamic excel, string workbookPath)
        {
            if (excel == null || string.IsNullOrWhiteSpace(workbookPath))
            {
                return null;
            }

            string target = NormalizePath(workbookPath);
            dynamic workbooks = null;
            try
            {
                workbooks = excel.Workbooks;
                int count = Convert.ToInt32(workbooks.Count, CultureInfo.InvariantCulture);
                for (int i = 1; i <= count; i++)
                {
                    dynamic workbook = workbooks[i];
                    string fullName = string.Empty;
                    try
                    {
                        fullName = Convert.ToString(workbook.FullName, CultureInfo.InvariantCulture);
                        if (IsSameWorkbook(fullName, target))
                        {
                            return workbook;
                        }
                    }
                    catch
                    {
                    }

                    Release(workbook);
                }
            }
            finally
            {
                Release(workbooks);
            }

            return null;
        }

        private static dynamic FindOpenWorkbookFromRunningObjectTable(string workbookPath, out string technicalDetail)
        {
            technicalDetail = string.Empty;
            if (string.IsNullOrWhiteSpace(workbookPath))
            {
                technicalDetail = "workbookPath is empty.";
                return null;
            }

            IRunningObjectTable runningObjectTable = null;
            IEnumMoniker enumMoniker = null;
            IBindCtx bindContext = null;
            StringBuilder seenObjects = new StringBuilder();

            try
            {
                if (GetRunningObjectTable(0, out runningObjectTable) != 0 || runningObjectTable == null)
                {
                    technicalDetail = "Running Object Table is not available.";
                    return null;
                }

                if (CreateBindCtx(0, out bindContext) != 0 || bindContext == null)
                {
                    technicalDetail = "COM bind context is not available.";
                    return null;
                }

                runningObjectTable.EnumRunning(out enumMoniker);
                if (enumMoniker == null)
                {
                    technicalDetail = "Running Object Table has no objects.";
                    return null;
                }

                IMoniker[] monikers = new IMoniker[1];
                IntPtr fetched = IntPtr.Zero;
                while (enumMoniker.Next(1, monikers, fetched) == 0)
                {
                    IMoniker moniker = monikers[0];
                    string displayName = GetDisplayName(bindContext, moniker);
                    if (!string.IsNullOrWhiteSpace(displayName) && IsLikelyExcelObject(displayName))
                    {
                        if (seenObjects.Length > 0)
                        {
                            seenObjects.Append(" | ");
                        }

                        seenObjects.Append(displayName);
                    }

                    object runningObject = null;
                    try
                    {
                        runningObjectTable.GetObject(moniker, out runningObject);
                        dynamic workbook = TryResolveWorkbook(runningObject, workbookPath);
                        if (workbook != null)
                        {
                            return workbook;
                        }
                    }
                    catch
                    {
                    }
                    finally
                    {
                        Release(runningObject);
                        Release(moniker);
                    }
                }

                technicalDetail = seenObjects.Length == 0
                    ? "No Excel object was found in Running Object Table."
                    : "Excel objects found, but target workbook path did not match. Objects: " + seenObjects.ToString();
                return null;
            }
            catch (Exception ex)
            {
                technicalDetail = ex.ToString();
                return null;
            }
            finally
            {
                Release(enumMoniker);
                Release(bindContext);
                Release(runningObjectTable);
            }
        }

        private static dynamic TryResolveWorkbook(object runningObject, string workbookPath)
        {
            if (runningObject == null)
            {
                return null;
            }

            dynamic candidate = runningObject;

            try
            {
                string fullName = Convert.ToString(candidate.FullName, CultureInfo.InvariantCulture);
                if (IsSameWorkbook(fullName, workbookPath))
                {
                    return candidate;
                }
            }
            catch
            {
            }

            try
            {
                dynamic application = candidate.Application;
                dynamic workbook = FindOpenWorkbook(application, workbookPath);
                if (workbook != null)
                {
                    return workbook;
                }
            }
            catch
            {
            }

            try
            {
                return FindOpenWorkbook(candidate, workbookPath);
            }
            catch
            {
                return null;
            }
        }

        private static dynamic FindWorksheet(dynamic workbook, string sheetName)
        {
            if (workbook == null || string.IsNullOrWhiteSpace(sheetName))
            {
                return null;
            }

            dynamic worksheets = null;
            try
            {
                worksheets = workbook.Worksheets;
                int count = Convert.ToInt32(worksheets.Count, CultureInfo.InvariantCulture);
                for (int i = 1; i <= count; i++)
                {
                    dynamic worksheet = worksheets[i];
                    string name = string.Empty;
                    try
                    {
                        name = Convert.ToString(worksheet.Name, CultureInfo.InvariantCulture);
                        if (string.Equals(name, sheetName, StringComparison.OrdinalIgnoreCase))
                        {
                            return worksheet;
                        }
                    }
                    catch
                    {
                    }

                    Release(worksheet);
                }
            }
            finally
            {
                Release(worksheets);
            }

            return null;
        }

        private static void WriteWorksheet(dynamic workbook, QtoWorksheetData worksheetData)
        {
            if (workbook == null || worksheetData == null)
            {
                return;
            }

            dynamic worksheet = null;
            dynamic cells = null;
            dynamic firstCell = null;
            dynamic lastCell = null;
            dynamic targetRange = null;

            try
            {
                worksheet = FindWorksheet(workbook, worksheetData.Name);
                if (worksheet == null)
                {
                    dynamic worksheets = workbook.Worksheets;
                    try
                    {
                        worksheet = worksheets.Add();
                        worksheet.Name = worksheetData.Name;
                    }
                    finally
                    {
                        Release(worksheets);
                    }
                }

                cells = worksheet.Cells;
                cells.Clear();

                object[,] source = worksheetData.Values;
                int rowCount = source == null ? 1 : Math.Max(1, source.GetLength(0) - 1);
                int columnCount = source == null ? 1 : Math.Max(1, source.GetLength(1) - 1);
                Array values = Array.CreateInstance(typeof(object), new int[] { rowCount, columnCount }, new int[] { 1, 1 });
                for (int row = 1; row <= rowCount; row++)
                {
                    for (int column = 1; column <= columnCount; column++)
                    {
                        values.SetValue(source == null ? null : source[row, column], row, column);
                    }
                }

                firstCell = cells[1, 1];
                lastCell = cells[rowCount, columnCount];
                targetRange = worksheet.Range[firstCell, lastCell];
                targetRange.Value2 = values;
                worksheet.Columns.AutoFit();
                worksheet.Visible = ToExcelVisibility(worksheetData.Visibility);
            }
            finally
            {
                Release(targetRange);
                Release(lastCell);
                Release(firstCell);
                Release(cells);
                Release(worksheet);
            }
        }

        private static int ToExcelVisibility(QtoWorksheetVisibility visibility)
        {
            if (visibility == QtoWorksheetVisibility.VeryHidden)
            {
                return XlSheetVeryHidden;
            }

            if (visibility == QtoWorksheetVisibility.Hidden)
            {
                return XlSheetHidden;
            }

            return XlSheetVisible;
        }

        private static string NormalizePath(string path)
        {
            try
            {
                return Path.GetFullPath(path ?? string.Empty).TrimEnd('\\', '/');
            }
            catch
            {
                return path ?? string.Empty;
            }
        }

        private static bool IsSameWorkbook(string workbookFullName, string targetPath)
        {
            string source = NormalizePath(workbookFullName);
            string target = NormalizePath(targetPath);
            if (string.Equals(source, target, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            string sourceName = Path.GetFileName(source);
            string targetName = Path.GetFileName(target);
            return !string.IsNullOrWhiteSpace(sourceName)
                && !string.IsNullOrWhiteSpace(targetName)
                && string.Equals(sourceName, targetName, StringComparison.OrdinalIgnoreCase)
                && IsExcelWorkbookName(sourceName);
        }

        private static bool IsExcelWorkbookName(string fileName)
        {
            string extension = Path.GetExtension(fileName ?? string.Empty);
            return string.Equals(extension, ".xlsx", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ".xlsm", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ".xls", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsLikelyExcelObject(string displayName)
        {
            return displayName.IndexOf("Excel", StringComparison.OrdinalIgnoreCase) >= 0
                || displayName.IndexOf(".xlsx", StringComparison.OrdinalIgnoreCase) >= 0
                || displayName.IndexOf(".xlsm", StringComparison.OrdinalIgnoreCase) >= 0
                || displayName.IndexOf(".xls", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string GetDisplayName(IBindCtx bindContext, IMoniker moniker)
        {
            try
            {
                string displayName;
                moniker.GetDisplayName(bindContext, null, out displayName);
                return displayName ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string BuildWorkbookNotFoundMessage(string workbookPath, string technicalDetail)
        {
            return "找不到已開啟的目標 Excel 工作簿。目標路徑：" + (workbookPath ?? string.Empty)
                + "\r\n可能原因：AutoCAD 與 Excel 權限層級不同、Excel 開在另一個執行個體、目前工作簿尚未儲存、或外掛目前選到的 Excel 路徑不是畫面上那一本。"
                + "\r\n技術資訊：" + (technicalDetail ?? string.Empty);
        }

        private static string ConvertCell(object value)
        {
            if (value == null)
            {
                return string.Empty;
            }

            return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        }

        private static void Release(object comObject)
        {
            try
            {
                if (comObject != null && Marshal.IsComObject(comObject))
                {
                    Marshal.ReleaseComObject(comObject);
                }
            }
            catch
            {
            }
        }
    }
}
