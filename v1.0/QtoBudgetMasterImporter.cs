using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace QtoWirePlugin
{
    public sealed class QtoBudgetMasterImporter
    {
        private static readonly HashSet<string> IgnoredSheets = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "總表", "統整", "預算草稿", "數量統整", "檢查清單", "CAD原始資料",
            "QTO_SYNC_DATA", "QTO_LOG", "QTO_SETTINGS", "QTO_BUDGET_MASTER", "QTO_MAPPING_RULES"
        };

        public QtoBudgetProjectData Import(string workbookPath)
        {
            QtoBudgetProjectData data = new QtoBudgetProjectData
            {
                SchemaVersion = 1,
                SourceWorkbookPath = workbookPath,
                ImportedAt = DateTime.UtcNow
            };

            int sortOrder = 0;
            foreach (string sheet in QtoSimpleXlsxReader.GetSheetNames(workbookPath))
            {
                if (IgnoredSheets.Contains(sheet))
                {
                    continue;
                }

                ImportSheet(sheet, QtoSimpleXlsxReader.ReadRawRows(workbookPath, sheet), data.MasterItems, ref sortOrder);
            }

            if (!data.MasterItems.Any(i => string.Equals(i.RowType, QtoBudgetRowType.Detail, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException("找不到含『項次、設備名稱、單位』的正式預算明細。");
            }

            return data;
        }

        private static void ImportSheet(string sheet, SortedDictionary<int, Dictionary<int, string>> rows, IList<QtoBudgetMasterItem> target, ref int sortOrder)
        {
            int headerRow = rows.FirstOrDefault(r => Contains(r.Value, "項次") && Contains(r.Value, "名稱")).Key;
            if (headerRow <= 0)
            {
                return;
            }

            string systemId = string.Empty;
            string categoryId = string.Empty;
            string parentId = string.Empty;
            int consecutiveEmpty = 0;
            foreach (KeyValuePair<int, Dictionary<int, string>> row in rows.Where(r => r.Key > headerRow))
            {
                string itemNo = Get(row.Value, 1).Trim();
                string name = Get(row.Value, 2).Trim();
                string unit = Get(row.Value, 3).Trim();
                if (string.IsNullOrWhiteSpace(itemNo) && string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(unit))
                {
                    consecutiveEmpty++;
                    if (consecutiveEmpty >= 50)
                    {
                        break;
                    }
                    continue;
                }
                consecutiveEmpty = 0;
                if (string.IsNullOrWhiteSpace(name) || IsTotal(name))
                {
                    continue;
                }

                string rowType = ResolveRowType(itemNo, unit);
                string parent = rowType == QtoBudgetRowType.System ? string.Empty
                    : rowType == QtoBudgetRowType.Category ? systemId
                    : rowType == QtoBudgetRowType.Parent ? categoryId
                    : !string.IsNullOrWhiteSpace(parentId) ? parentId : categoryId;
                QtoBudgetMasterItem item = new QtoBudgetMasterItem
                {
                    BudgetItemId = BuildStableId(sheet, row.Key, itemNo, name, unit),
                    ParentItemId = parent,
                    RowType = rowType,
                    SourceSheet = sheet,
                    SourceRow = row.Key,
                    ItemNo = itemNo,
                    ItemName = name,
                    Unit = unit,
                    UnitPrice = Get(row.Value, 5),
                    Remark = Join(Get(row.Value, 7), Get(row.Value, 15)),
                    CostUnitPrice = Get(row.Value, 8),
                    Multiplier = Get(row.Value, 9),
                    Brand = Get(row.Value, 10),
                    BaseCost = Get(row.Value, 11),
                    Labor = Get(row.Value, 12),
                    SortOrder = sortOrder++
                };
                target.Add(item);

                if (rowType == QtoBudgetRowType.System)
                {
                    systemId = item.BudgetItemId;
                    categoryId = string.Empty;
                    parentId = string.Empty;
                }
                else if (rowType == QtoBudgetRowType.Category)
                {
                    categoryId = item.BudgetItemId;
                    parentId = string.Empty;
                }
                else if (rowType == QtoBudgetRowType.Parent)
                {
                    parentId = item.BudgetItemId;
                }
            }
        }

        private static string ResolveRowType(string itemNo, string unit)
        {
            if (!string.IsNullOrWhiteSpace(unit)) return QtoBudgetRowType.Detail;
            if (itemNo.Length == 1 && itemNo[0] >= 'A' && itemNo[0] <= 'Z') return QtoBudgetRowType.Category;
            if (itemNo.StartsWith("-", StringComparison.Ordinal)) return QtoBudgetRowType.Detail;
            int number;
            if (int.TryParse(itemNo, NumberStyles.Integer, CultureInfo.InvariantCulture, out number)) return QtoBudgetRowType.Parent;
            return QtoBudgetRowType.System;
        }

        private static bool Contains(IDictionary<int, string> row, string text)
        {
            return row.Values.Any(v => (v ?? string.Empty).Replace(" ", string.Empty).IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static string Get(IDictionary<int, string> row, int column)
        {
            string value;
            return row.TryGetValue(column, out value) ? value ?? string.Empty : string.Empty;
        }

        private static bool IsTotal(string value)
        {
            string text = (value ?? string.Empty).Replace(" ", string.Empty);
            return text == "小計" || text == "合計" || text == "總計" || text.EndsWith("小計", StringComparison.Ordinal);
        }

        private static string Join(string first, string second)
        {
            if (string.IsNullOrWhiteSpace(first)) return second ?? string.Empty;
            if (string.IsNullOrWhiteSpace(second)) return first;
            return first + "；" + second;
        }

        private static string BuildStableId(string sheet, int row, string itemNo, string name, string unit)
        {
            string raw = string.Join("|", sheet, row.ToString(CultureInfo.InvariantCulture), itemNo, name, unit);
            using (SHA1 sha = SHA1.Create())
            {
                return "BI-" + BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(raw))).Replace("-", string.Empty).Substring(0, 16);
            }
        }
    }
}
