using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Packaging;
using System.Linq;
using System.Reflection;
using System.Xml;
using QtoWirePlugin;

internal static class QtoExcelSmokeTest
{
    private const string SpreadsheetNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

    [STAThread]
    private static int Main()
    {
        string workbookPath = Path.Combine(Path.GetTempPath(), "qto-excel-smoke-" + Guid.NewGuid().ToString("N") + ".xlsx");
        string formalWorkbookPath = Path.Combine(Path.GetTempPath(), "qto-formal-budget-smoke-" + Guid.NewGuid().ToString("N") + ".xlsx");
        try
        {
            List<QtoSyncRow> rows = new List<QtoSyncRow>
            {
                CreateRow("sync-cctv", "CCTV", "CCTV_CAMERA", "DEVICE", string.Empty, 2, "台"),
                CreateRow("sync-info", "資訊", "LAN_OUTLET", "OUTLET", "Cat6", 12, "點"),
                CreateRow("sync-ba", "BA", "BA_SENSOR", "DEVICE", string.Empty, 0, "只")
            };

            Dictionary<string, string> settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "BudgetLayout.SystemOrder", "CCTV|資訊|弱電|停管|TV|BA|視聽音響|緊急廣播" },
                { "BudgetLayout.HiddenSystems", "資訊" },
                { "BudgetLayout.CategoryOrder", "設備工程|配線工程|配管工程|其他工程" },
                { "BudgetLayout.VisibleColumns", "項次|設備名稱|單位|數量|單價|複價|備註|確認狀態" },
                { "BudgetLayout.HideZeroQuantity", "true" },
                { "BudgetLayout.ReviewRowsLast", "true" }
            };

            QtoExcelResult result = new QtoExcelAdapter().FullRebuild(
                workbookPath,
                rows,
                new List<QtoReviewItem>(),
                new List<QtoSyncLogRow>(),
                settings);
            Assert(result.Success, "Workbook build should succeed: " + result.TechnicalDetail);
            Assert(File.Exists(workbookPath), "Workbook should be created.");

            string workbookXml = ReadPackageText(workbookPath, "/xl/workbook.xml");
            Assert(workbookXml.Contains("name=\"預算草稿\""), "Budget draft sheet should exist.");
            Assert(GetSheetState(workbookXml, "系統摘要") == string.Empty, "System summary should be a visible user-facing sheet.");
            Assert(workbookXml.Contains("name=\"數量統整\""), "Quantity matrix sheet should exist.");
            Assert(workbookXml.Contains("name=\"檢查清單\""), "Review sheet should use the Chinese user-facing name.");
            Assert(GetSheetState(workbookXml, "CAD原始資料") == "hidden", "Raw CAD sheet should exist and stay available as a normally hidden source layer.");
            Assert(GetSheetState(workbookXml, "QTO_SYNC_DATA") == "veryHidden", "Technical sync sheet should stay very hidden.");

            IList<Dictionary<string, string>> budgetRows = ReadTable(workbookPath, "預算草稿");
            Assert(budgetRows.Any(row => Get(row, "系統代碼") == "CCTV"), "Visible CCTV section should be generated.");
            Assert(budgetRows.Any(row => Get(row, "系統代碼") == "資訊"), "Hidden systems must remain in the budget draft so manual fields are preserved.");
            Assert(budgetRows.Any(row => Get(row, "系統代碼") == "BA"), "Zero-quantity rows must remain in the budget draft so manual fields are preserved.");

            IList<Dictionary<string, string>> rawRows = ReadTable(workbookPath, "CAD原始資料");
            Assert(rawRows.Count == 3, "Raw CAD sheet must retain hidden and zero-quantity source rows.");

            IList<Dictionary<string, string>> settingsRows = ReadTable(workbookPath, "QTO_SETTINGS");
            Assert(settingsRows.Any(row => Get(row, "Key") == "BudgetLayout.SystemOrder"), "Layout rules should persist in the hidden settings sheet.");

            string firstSheetXml = ReadPackageText(workbookPath, "/xl/worksheets/sheet1.xml");
            Assert(firstSheetXml.Contains("<cols>"), "Budget draft should persist hidden-column metadata.");
            Assert(firstSheetXml.Contains("min=\"15\"") && firstSheetXml.Contains("hidden=\"1\""), "Technical columns should be hidden.");
            Assert(CountHiddenRows(firstSheetXml) >= 2, "Hidden systems and zero-quantity items should be represented as hidden rows, not deleted rows.");

            QtoBudgetProjectData project = new QtoBudgetProjectData { SchemaVersion = 1, SourceWorkbookPath = "sample.xlsx" };
            project.MasterItems.Add(new QtoBudgetMasterItem { BudgetItemId = "BI-SYS", RowType = QtoBudgetRowType.System, ItemNo = "一", ItemName = "監視系統設備工程", SortOrder = 0 });
            project.MasterItems.Add(new QtoBudgetMasterItem { BudgetItemId = "BI-SMOKE", ParentItemId = "BI-SYS", RowType = QtoBudgetRowType.Detail, ItemNo = "1", ItemName = "網路攝影機", Unit = "台", SortOrder = 1 });
            project.MappingRules.Add(new QtoBudgetMappingRule { RuleId = "RULE-SMOKE", SystemCode = "CCTV", EquipmentTypeCode = "CCTV_CAMERA", BudgetItemId = "BI-SMOKE", QuantityRule = QtoQuantityRuleType.SourceQuantity, Status = "confirmed", Scope = "project" });
            result = new QtoExcelAdapter().FullRebuild(formalWorkbookPath, rows, new List<QtoReviewItem>(), new List<QtoSyncLogRow>(), settings, project);
            Assert(result.Success, "Formal budget rebuild should succeed: " + result.TechnicalDetail);
            Assert(result.BudgetDraftRowCount == 1 && result.BudgetDraftReviewCount == 0, "Formal budget counts should come from confirmed mapping.");
            workbookXml = ReadPackageText(formalWorkbookPath, "/xl/workbook.xml");
            Assert(GetSheetState(workbookXml, "QTO_BUDGET_MASTER") == "veryHidden", "Budget master sheet should be very hidden.");
            Assert(GetSheetState(workbookXml, "QTO_MAPPING_RULES") == "veryHidden", "Mapping rules sheet should be very hidden.");
            Assert(GetSheetState(workbookXml, "QTO_BUDGET_BINDINGS") == "veryHidden", "Company budget bindings sheet should be very hidden.");
            budgetRows = ReadTable(formalWorkbookPath, "預算草稿");
            Dictionary<string, string> mappedRow = budgetRows.FirstOrDefault(row => Get(row, "預算Key") == "BI-SMOKE");
            Assert(mappedRow != null && Get(mappedRow, "數量") == "2", "Confirmed mapping should write the CAD quantity to the formal budget row.");

            Console.WriteLine("QtoWirePlugin v1.0.0 Beta Excel smoke test passed.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.ToString());
            return 1;
        }
        finally
        {
            if (File.Exists(workbookPath))
            {
                File.Delete(workbookPath);
            }
            if (File.Exists(formalWorkbookPath))
            {
                File.Delete(formalWorkbookPath);
            }
        }
    }

    private static QtoSyncRow CreateRow(string syncId, string system, string equipment, string cadType, string cable, double quantity, string unit)
    {
        return new QtoSyncRow
        {
            SyncId = syncId,
            SourceDwg = "smoke.dwg",
            ObjectHandle = syncId,
            SystemCode = system,
            EquipmentTypeCode = equipment,
            EquipmentTypeName = equipment,
            CadMeasureType = cadType,
            CableType = cable,
            Floor = "1F",
            Area = "A區",
            Space = "測試空間",
            QuantityBasis = "point_count",
            Quantity = quantity,
            Unit = unit,
            SyncStatus = "confirmed"
        };
    }

    private static IList<Dictionary<string, string>> ReadTable(string workbookPath, string sheetName)
    {
        Assembly assembly = typeof(QtoExcelAdapter).Assembly;
        Type readerType = assembly.GetType("QtoWirePlugin.QtoSimpleXlsxReader", true);
        MethodInfo method = readerType.GetMethod("ReadTable", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        object value = method.Invoke(null, new object[] { workbookPath, sheetName });
        List<Dictionary<string, string>> rows = new List<Dictionary<string, string>>();
        foreach (object item in (IEnumerable)value)
        {
            rows.Add((Dictionary<string, string>)item);
        }
        return rows;
    }

    private static string ReadPackageText(string workbookPath, string partPath)
    {
        using (Package package = Package.Open(workbookPath, FileMode.Open, FileAccess.Read))
        {
            Uri uri = PackUriHelper.CreatePartUri(new Uri(partPath, UriKind.Relative));
            using (StreamReader reader = new StreamReader(package.GetPart(uri).GetStream(FileMode.Open, FileAccess.Read)))
            {
                return reader.ReadToEnd();
            }
        }
    }

    private static int CountHiddenRows(string worksheetXml)
    {
        XmlDocument document = new XmlDocument();
        document.LoadXml(worksheetXml);
        XmlNamespaceManager namespaces = new XmlNamespaceManager(document.NameTable);
        namespaces.AddNamespace("ss", SpreadsheetNs);
        XmlNodeList nodes = document.SelectNodes("/ss:worksheet/ss:sheetData/ss:row[@hidden='1']", namespaces);
        return nodes == null ? 0 : nodes.Count;
    }

    private static string GetSheetState(string workbookXml, string sheetName)
    {
        XmlDocument document = new XmlDocument();
        document.LoadXml(workbookXml);
        XmlNamespaceManager namespaces = new XmlNamespaceManager(document.NameTable);
        namespaces.AddNamespace("ss", SpreadsheetNs);
        XmlNode node = document.SelectSingleNode("/ss:workbook/ss:sheets/ss:sheet[@name='" + sheetName + "']", namespaces);
        if (node == null || node.Attributes["state"] == null)
        {
            return string.Empty;
        }
        return node.Attributes["state"].Value;
    }

    private static string Get(Dictionary<string, string> row, string key)
    {
        string value;
        return row != null && row.TryGetValue(key, out value) ? value : string.Empty;
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
