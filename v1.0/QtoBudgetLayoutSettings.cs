using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace QtoWirePlugin
{
    internal sealed class QtoBudgetLayoutSettings
    {
        public const string SystemOrderKey = "BudgetLayout.SystemOrder";
        public const string HiddenSystemsKey = "BudgetLayout.HiddenSystems";
        public const string CategoryOrderKey = "BudgetLayout.CategoryOrder";
        public const string HiddenCategoriesKey = "BudgetLayout.HiddenCategories";
        public const string VisibleColumnsKey = "BudgetLayout.VisibleColumns";
        public const string HideZeroQuantityKey = "BudgetLayout.HideZeroQuantity";
        public const string ReviewRowsLastKey = "BudgetLayout.ReviewRowsLast";

        private static readonly string[] DefaultCategoryOrder =
        {
            "設備工程",
            "配線工程",
            "配管工程",
            "其他工程"
        };

        private static readonly string[] DefaultVisibleColumns =
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
            "待確認原因"
        };

        public QtoBudgetLayoutSettings()
        {
            SystemOrder = new List<string>();
            HiddenSystems = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            CategoryOrder = new List<string>();
            HiddenCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            VisibleColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        public List<string> SystemOrder { get; private set; }
        public HashSet<string> HiddenSystems { get; private set; }
        public List<string> CategoryOrder { get; private set; }
        public HashSet<string> HiddenCategories { get; private set; }
        public HashSet<string> VisibleColumns { get; private set; }
        public bool HideZeroQuantity { get; set; }
        public bool ReviewRowsLast { get; set; }

        public static QtoBudgetLayoutSettings CreateDefault()
        {
            QtoBudgetLayoutSettings settings = new QtoBudgetLayoutSettings();
            settings.SystemOrder.AddRange(QtoSystemDefaults.GetValues());
            settings.CategoryOrder.AddRange(DefaultCategoryOrder);
            settings.VisibleColumns.UnionWith(DefaultVisibleColumns);
            settings.HideZeroQuantity = true;
            settings.ReviewRowsLast = true;
            return settings;
        }

        public static QtoBudgetLayoutSettings FromDictionary(IDictionary<string, string> values)
        {
            QtoBudgetLayoutSettings settings = CreateDefault();
            if (values == null)
            {
                return settings;
            }

            ApplyOrderedList(values, SystemOrderKey, settings.SystemOrder, QtoSystemDefaults.GetValues());
            ApplySet(values, HiddenSystemsKey, settings.HiddenSystems);
            ApplyOrderedList(values, CategoryOrderKey, settings.CategoryOrder, DefaultCategoryOrder);
            ApplySet(values, HiddenCategoriesKey, settings.HiddenCategories);

            string visibleColumns;
            if (values.TryGetValue(VisibleColumnsKey, out visibleColumns))
            {
                settings.VisibleColumns.Clear();
                settings.VisibleColumns.UnionWith(Split(visibleColumns));
            }

            settings.HideZeroQuantity = ReadBoolean(values, HideZeroQuantityKey, settings.HideZeroQuantity);
            settings.ReviewRowsLast = ReadBoolean(values, ReviewRowsLastKey, settings.ReviewRowsLast);
            settings.Normalize();
            return settings;
        }

        public QtoBudgetLayoutSettings Clone()
        {
            QtoBudgetLayoutSettings clone = new QtoBudgetLayoutSettings();
            clone.SystemOrder.AddRange(SystemOrder);
            clone.HiddenSystems.UnionWith(HiddenSystems);
            clone.CategoryOrder.AddRange(CategoryOrder);
            clone.HiddenCategories.UnionWith(HiddenCategories);
            clone.VisibleColumns.UnionWith(VisibleColumns);
            clone.HideZeroQuantity = HideZeroQuantity;
            clone.ReviewRowsLast = ReviewRowsLast;
            return clone;
        }

        public void Normalize()
        {
            NormalizeList(SystemOrder, QtoSystemDefaults.GetValues());
            NormalizeList(CategoryOrder, DefaultCategoryOrder);
            HiddenSystems.RemoveWhere(string.IsNullOrWhiteSpace);
            HiddenCategories.RemoveWhere(string.IsNullOrWhiteSpace);
            VisibleColumns.RemoveWhere(string.IsNullOrWhiteSpace);
        }

        public bool IsSystemVisible(string systemCode)
        {
            return !HiddenSystems.Contains(systemCode ?? string.Empty);
        }

        public bool IsCategoryVisible(string category)
        {
            return !HiddenCategories.Contains(category ?? string.Empty);
        }

        public bool IsColumnVisible(string columnName)
        {
            return VisibleColumns.Contains(columnName ?? string.Empty);
        }

        public int GetSystemSortIndex(string systemCode)
        {
            return GetSortIndex(SystemOrder, systemCode);
        }

        public int GetCategorySortIndex(string category)
        {
            return GetSortIndex(CategoryOrder, category);
        }

        public IDictionary<string, string> ToDictionary()
        {
            Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            values[SystemOrderKey] = Join(SystemOrder);
            values[HiddenSystemsKey] = Join(HiddenSystems.OrderBy(v => v, StringComparer.OrdinalIgnoreCase));
            values[CategoryOrderKey] = Join(CategoryOrder);
            values[HiddenCategoriesKey] = Join(HiddenCategories.OrderBy(v => v, StringComparer.OrdinalIgnoreCase));
            values[VisibleColumnsKey] = Join(VisibleColumns.OrderBy(v => v, StringComparer.OrdinalIgnoreCase));
            values[HideZeroQuantityKey] = HideZeroQuantity ? "true" : "false";
            values[ReviewRowsLastKey] = ReviewRowsLast ? "true" : "false";
            return values;
        }

        public static string[] GetAvailableCategories()
        {
            return (string[])DefaultCategoryOrder.Clone();
        }

        public static string[] GetAvailableColumns()
        {
            return (string[])DefaultVisibleColumns.Clone();
        }

        private static void ApplyOrderedList(
            IDictionary<string, string> values,
            string key,
            IList<string> target,
            IEnumerable<string> defaults)
        {
            string raw;
            if (values.TryGetValue(key, out raw))
            {
                target.Clear();
                foreach (string value in Split(raw))
                {
                    target.Add(value);
                }
            }

            NormalizeList(target, defaults);
        }

        private static void ApplySet(IDictionary<string, string> values, string key, ISet<string> target)
        {
            string raw;
            if (!values.TryGetValue(key, out raw))
            {
                return;
            }

            target.Clear();
            target.UnionWith(Split(raw));
        }

        private static void NormalizeList(IList<string> target, IEnumerable<string> defaults)
        {
            List<string> normalized = new List<string>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string value in target.Concat(defaults ?? new string[0]))
            {
                string trimmed = (value ?? string.Empty).Trim();
                if (!string.IsNullOrWhiteSpace(trimmed) && seen.Add(trimmed))
                {
                    normalized.Add(trimmed);
                }
            }

            target.Clear();
            foreach (string value in normalized)
            {
                target.Add(value);
            }
        }

        private static int GetSortIndex(IList<string> values, string candidate)
        {
            for (int i = 0; i < values.Count; i++)
            {
                if (string.Equals(values[i], candidate ?? string.Empty, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return values.Count + 1000;
        }

        private static bool ReadBoolean(IDictionary<string, string> values, string key, bool defaultValue)
        {
            string raw;
            bool result;
            return values.TryGetValue(key, out raw) && bool.TryParse(raw, out result) ? result : defaultValue;
        }

        private static IEnumerable<string> Split(string value)
        {
            return (value ?? string.Empty)
                .Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(v => v.Trim())
                .Where(v => !string.IsNullOrWhiteSpace(v));
        }

        private static string Join(IEnumerable<string> values)
        {
            return string.Join("|", (values ?? new string[0]).ToArray());
        }
    }

    internal static class QtoBudgetLayoutSettingsService
    {
        private const string DefaultCacheKey = "__DEFAULT__";
        private static readonly object Gate = new object();
        private static readonly Dictionary<string, QtoBudgetLayoutSettings> Cache =
            new Dictionary<string, QtoBudgetLayoutSettings>(StringComparer.OrdinalIgnoreCase);

        public static QtoBudgetLayoutSettings Load(string workbookPath)
        {
            string key = NormalizeKey(workbookPath);
            lock (Gate)
            {
                QtoBudgetLayoutSettings cached;
                if (Cache.TryGetValue(key, out cached))
                {
                    return cached.Clone();
                }
            }

            IDictionary<string, string> values = ReadWorkbookSettings(workbookPath);
            QtoBudgetLayoutSettings settings = values.Count > 0
                ? QtoBudgetLayoutSettings.FromDictionary(values)
                : GetDefaultOrNew();
            Set(workbookPath, settings);
            return settings.Clone();
        }

        public static void Set(string workbookPath, QtoBudgetLayoutSettings settings)
        {
            string key = NormalizeKey(workbookPath);
            QtoBudgetLayoutSettings safe = settings == null ? QtoBudgetLayoutSettings.CreateDefault() : settings.Clone();
            safe.Normalize();
            lock (Gate)
            {
                Cache[key] = safe;
                if (key == DefaultCacheKey)
                {
                    Cache[DefaultCacheKey] = safe.Clone();
                }
            }
        }

        public static void AddTo(IDictionary<string, string> target, QtoBudgetLayoutSettings settings)
        {
            if (target == null)
            {
                return;
            }

            foreach (KeyValuePair<string, string> pair in (settings ?? QtoBudgetLayoutSettings.CreateDefault()).ToDictionary())
            {
                target[pair.Key] = pair.Value;
            }
        }

        private static QtoBudgetLayoutSettings GetDefaultOrNew()
        {
            lock (Gate)
            {
                QtoBudgetLayoutSettings cached;
                if (Cache.TryGetValue(DefaultCacheKey, out cached))
                {
                    return cached.Clone();
                }
            }

            return QtoBudgetLayoutSettings.CreateDefault();
        }

        private static IDictionary<string, string> ReadWorkbookSettings(string workbookPath)
        {
            Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(workbookPath) || !File.Exists(workbookPath))
            {
                return values;
            }

            IList<Dictionary<string, string>> rows;
            string technicalDetail;
            if (!QtoExcelComWorkbookBridge.TryReadOpenWorkbookTable(
                    workbookPath,
                    QtoExcelWorkbookBuilder.SettingsSheetName,
                    out rows,
                    out technicalDetail))
            {
                rows = QtoSimpleXlsxReader.ReadTable(workbookPath, QtoExcelWorkbookBuilder.SettingsSheetName);
            }

            foreach (Dictionary<string, string> row in rows ?? new List<Dictionary<string, string>>())
            {
                string settingKey;
                string settingValue;
                if (row.TryGetValue("Key", out settingKey) && !string.IsNullOrWhiteSpace(settingKey))
                {
                    row.TryGetValue("Value", out settingValue);
                    values[settingKey] = settingValue ?? string.Empty;
                }
            }

            return values;
        }

        private static string NormalizeKey(string workbookPath)
        {
            if (string.IsNullOrWhiteSpace(workbookPath))
            {
                return DefaultCacheKey;
            }

            try
            {
                return Path.GetFullPath(workbookPath);
            }
            catch
            {
                return workbookPath.Trim();
            }
        }
    }
}
