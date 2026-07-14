using System;
using System.Collections.Generic;
using System.Linq;

namespace QtoWirePlugin
{
    public static class QtoBudgetCompletenessService
    {
        public static QtoBudgetCompletenessResult Analyze(IList<QtoSyncRow> sourceRows, QtoBudgetProjectData project)
        {
            QtoBudgetCompletenessResult result = new QtoBudgetCompletenessResult();
            project = project ?? new QtoBudgetProjectData { SchemaVersion = 1 };
            IList<QtoSyncRow> rows = sourceRows ?? new List<QtoSyncRow>();
            List<QtoBudgetMasterItem> masterItems = (project.MasterItems ?? new List<QtoBudgetMasterItem>()).ToList();
            List<QtoBudgetMappingRule> rules = (project.MappingRules ?? new List<QtoBudgetMappingRule>()).ToList();
            Dictionary<string, QtoBudgetMasterItem> masterById = masterItems
                .Where(item => item != null && !string.IsNullOrWhiteSpace(item.BudgetItemId))
                .GroupBy(item => item.BudgetItemId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

            List<IGrouping<string, QtoSyncRow>> groupedRows = rows
                .Where(row => row != null)
                .GroupBy(BuildSourceKey, StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (IGrouping<string, QtoSyncRow> group in groupedRows)
            {
                QtoSyncRow sample = group.First();
                QtoBudgetSourceGroup sourceGroup = new QtoBudgetSourceGroup
                {
                    Key = group.Key,
                    SystemCode = sample.SystemCode,
                    EquipmentTypeCode = sample.EquipmentTypeCode,
                    CadMeasureType = sample.CadMeasureType,
                    CableType = sample.CableType,
                    ConduitType = sample.ConduitType,
                    Unit = sample.Unit,
                    ObjectCount = group.Count(),
                    Quantity = group.Sum(row => row.Quantity)
                };
                result.SourceGroups.Add(sourceGroup);

                List<QtoBudgetMappingRule> matchingRules = rules.Where(rule => RuleMatches(rule, sample)).ToList();
                List<QtoBudgetMappingRule> confirmedRules = matchingRules.Where(rule => IsStatus(rule, "confirmed")).ToList();
                if (confirmedRules.Count == 0)
                {
                    if (matchingRules.Any(rule => IsStatus(rule, "candidate") || IsStatus(rule, "needs_review")))
                    {
                        result.Summary.PendingCadGroupCount++;
                        AddSourceIssue(result, sourceGroup, QtoBudgetCompletenessIssueType.PendingMapping, "對應待確認", QtoReviewSeverity.Warning,
                            "這組 CAD 計量資料只有待確認的預算對應。", "請開啟預算對應，確認正確品項後再納入正式合計。");
                    }
                    else if (matchingRules.Any(rule => IsStatus(rule, "blocked")))
                    {
                        result.Summary.UnmappedCadGroupCount++;
                        AddSourceIssue(result, sourceGroup, QtoBudgetCompletenessIssueType.BlockedMapping, "規則異常", QtoReviewSeverity.Warning,
                            "這組 CAD 計量資料目前被預算對應規則阻擋。", "請確認此項是否不採用，或重新建立正確對應。");
                    }
                    else
                    {
                        result.Summary.UnmappedCadGroupCount++;
                        AddSourceIssue(result, sourceGroup, QtoBudgetCompletenessIssueType.UnmappedCadSource, "CAD 有、預算無", QtoReviewSeverity.Warning,
                            "圖面已有數量，但尚未建立正式預算對應。", "請在預算對應視窗選擇一個或多個正式預算品項。");
                    }
                    continue;
                }

                int validConfirmedCount = 0;
                foreach (QtoBudgetMappingRule rule in confirmedRules)
                {
                    QtoBudgetMasterItem target;
                    if (!masterById.TryGetValue(rule.BudgetItemId ?? string.Empty, out target)
                        || target.Hidden
                        || !string.Equals(target.RowType, QtoBudgetRowType.Detail, StringComparison.OrdinalIgnoreCase))
                    {
                        AddRuleIssue(result, sourceGroup, rule, null, QtoBudgetCompletenessIssueType.InvalidBudgetTarget, "規則異常", QtoReviewSeverity.Error,
                            "已確認的對應指向不存在、隱藏或非明細預算列。", "請重新選擇有效的正式預算明細。");
                        continue;
                    }

                    validConfirmedCount++;

                    if (RequiresMatchingUnit(rule)
                        && !string.IsNullOrWhiteSpace(sourceGroup.Unit)
                        && !string.IsNullOrWhiteSpace(target.Unit)
                        && !string.Equals(sourceGroup.Unit.Trim(), target.Unit.Trim(), StringComparison.OrdinalIgnoreCase))
                    {
                        AddRuleIssue(result, sourceGroup, rule, target, QtoBudgetCompletenessIssueType.UnitConflict, "規則異常", QtoReviewSeverity.Warning,
                            "CAD 單位與預算品項單位不同，且目前規則沒有明確換算。", "請改用適當數量規則或重新選擇單位一致的品項。");
                    }
                }

                if (validConfirmedCount > 0) result.Summary.ConfirmedCadGroupCount++;
                else result.Summary.UnmappedCadGroupCount++;

                foreach (IGrouping<string, QtoBudgetMappingRule> duplicate in confirmedRules
                    .Where(rule => !string.IsNullOrWhiteSpace(rule.BudgetItemId))
                    .GroupBy(rule => rule.BudgetItemId, StringComparer.OrdinalIgnoreCase)
                    .Where(ruleGroup => ruleGroup.Count() > 1))
                {
                    QtoBudgetMasterItem target;
                    masterById.TryGetValue(duplicate.Key, out target);
                    AddRuleIssue(result, sourceGroup, duplicate.First(), target, QtoBudgetCompletenessIssueType.DuplicateMapping, "規則異常", QtoReviewSeverity.Warning,
                        "相同 CAD 來源重複對應到同一個預算品項，可能造成重複計量。", "請刪除重複規則，只保留需要的正式對應。");
                }
            }

            result.Summary.CadGroupCount = result.SourceGroups.Count;
            AnalyzeBudgetCoverage(result, rows, project, masterItems, rules);
            return result;
        }

        public static IList<QtoReviewItem> ToReviewItems(QtoBudgetCompletenessResult result)
        {
            List<QtoReviewItem> reviewItems = new List<QtoReviewItem>();
            if (result == null) return reviewItems;
            foreach (QtoBudgetCompletenessIssue issue in result.Issues)
            {
                QtoValidationTarget target = string.IsNullOrWhiteSpace(issue.SourceKey) ? null : new QtoValidationTarget
                {
                    SystemCode = issue.SystemCode,
                    EquipmentTypeCode = issue.EquipmentTypeCode,
                    BlockName = issue.BudgetItemName
                };
                QtoReviewItem item = QtoReviewService.CreateReviewItem(
                    issue.IssueType, issue.Severity, issue.Message, issue.TechnicalDetail, target, false, issue.SuggestedAction);
                item.Category = "budget";
                if (string.IsNullOrWhiteSpace(item.BlockName)) item.BlockName = issue.BudgetItemName ?? string.Empty;
                reviewItems.Add(item);
            }
            return reviewItems;
        }

        public static string BuildSourceKey(QtoSyncRow row)
        {
            if (row == null) return string.Empty;
            return string.Join("|", new[]
            {
                Normalize(row.SystemCode), Normalize(row.EquipmentTypeCode), Normalize(row.CadMeasureType),
                Normalize(row.CableType), Normalize(row.ConduitType), Normalize(row.Unit)
            });
        }

        public static bool RuleMatches(QtoBudgetMappingRule rule, QtoSyncRow row)
        {
            if (rule == null || row == null) return false;
            return Match(rule.SystemCode, row.SystemCode)
                && Match(rule.EquipmentTypeCode, row.EquipmentTypeCode)
                && Match(rule.CadMeasureType, row.CadMeasureType)
                && Match(rule.CableType, row.CableType)
                && Match(rule.ConduitType, row.ConduitType)
                && Match(rule.Unit, row.Unit);
        }

        private static void AnalyzeBudgetCoverage(
            QtoBudgetCompletenessResult result,
            IList<QtoSyncRow> rows,
            QtoBudgetProjectData project,
            IList<QtoBudgetMasterItem> masterItems,
            IList<QtoBudgetMappingRule> rules)
        {
            Dictionary<string, QtoBudgetCoverageOverride> overrides = (project.CoverageOverrides ?? new List<QtoBudgetCoverageOverride>())
                .Where(item => item != null && !string.IsNullOrWhiteSpace(item.BudgetItemId))
                .GroupBy(item => item.BudgetItemId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.OrderByDescending(item => item.UpdatedAt).First(), StringComparer.OrdinalIgnoreCase);

            foreach (QtoBudgetMasterItem item in masterItems.Where(item => item != null
                && !item.Hidden
                && string.Equals(item.RowType, QtoBudgetRowType.Detail, StringComparison.OrdinalIgnoreCase)))
            {
                result.Summary.BudgetDetailCount++;
                QtoBudgetCoverageOverride coverage;
                if (overrides.TryGetValue(item.BudgetItemId ?? string.Empty, out coverage))
                {
                    if (string.Equals(coverage.CoverageMode, QtoBudgetCoverageMode.Manual, StringComparison.OrdinalIgnoreCase))
                    {
                        result.Summary.ManualBudgetCount++;
                        continue;
                    }
                    if (string.Equals(coverage.CoverageMode, QtoBudgetCoverageMode.Fixed, StringComparison.OrdinalIgnoreCase))
                    {
                        result.Summary.FixedBudgetCount++;
                        continue;
                    }
                    if (string.Equals(coverage.CoverageMode, QtoBudgetCoverageMode.Excluded, StringComparison.OrdinalIgnoreCase))
                    {
                        result.Summary.ExcludedBudgetCount++;
                        continue;
                    }
                }

                List<QtoBudgetMappingRule> itemRules = rules.Where(rule => IsStatus(rule, "confirmed")
                    && string.Equals(rule.BudgetItemId, item.BudgetItemId, StringComparison.OrdinalIgnoreCase)).ToList();
                bool hasSource = itemRules.Any(rule => rows.Any(row => RuleMatches(rule, row)));
                if (hasSource)
                {
                    result.Summary.CadSourcedBudgetCount++;
                    continue;
                }

                result.Summary.MissingSourceBudgetCount++;
                result.Issues.Add(new QtoBudgetCompletenessIssue
                {
                    IssueType = QtoBudgetCompletenessIssueType.BudgetWithoutCadSource,
                    Category = "預算有、CAD 無",
                    Severity = QtoReviewSeverity.Info,
                    BudgetItemId = item.BudgetItemId,
                    BudgetItemName = item.ItemName,
                    Message = "正式預算品項目前沒有 CAD 數量來源。",
                    SuggestedAction = "若此項由人工估算、固定數量或不需 CAD 計量，請明確標記；否則建立正式對應。",
                    TechnicalDetail = "BudgetItemId=" + (item.BudgetItemId ?? string.Empty) + "; Source=" + (item.SourceSheet ?? string.Empty) + ":" + item.SourceRow.ToString()
                });
            }
        }

        private static void AddSourceIssue(QtoBudgetCompletenessResult result, QtoBudgetSourceGroup group, string type, string category, string severity, string message, string action)
        {
            result.Issues.Add(new QtoBudgetCompletenessIssue
            {
                IssueType = type,
                Category = category,
                Severity = severity,
                SourceKey = group.Key,
                SystemCode = group.SystemCode,
                EquipmentTypeCode = group.EquipmentTypeCode,
                CadMeasureType = group.CadMeasureType,
                Message = message,
                SuggestedAction = action,
                TechnicalDetail = "SourceKey=" + group.Key + "; Objects=" + group.ObjectCount.ToString() + "; Quantity=" + group.Quantity.ToString("0.########")
            });
        }

        private static void AddRuleIssue(QtoBudgetCompletenessResult result, QtoBudgetSourceGroup group, QtoBudgetMappingRule rule, QtoBudgetMasterItem target, string type, string category, string severity, string message, string action)
        {
            result.Issues.Add(new QtoBudgetCompletenessIssue
            {
                IssueType = type,
                Category = category,
                Severity = severity,
                SourceKey = group.Key,
                BudgetItemId = rule == null ? string.Empty : rule.BudgetItemId,
                SystemCode = group.SystemCode,
                EquipmentTypeCode = group.EquipmentTypeCode,
                CadMeasureType = group.CadMeasureType,
                BudgetItemName = target == null ? string.Empty : target.ItemName,
                Message = message,
                SuggestedAction = action,
                TechnicalDetail = "SourceKey=" + group.Key + "; RuleId=" + (rule == null ? string.Empty : rule.RuleId) + "; BudgetItemId=" + (rule == null ? string.Empty : rule.BudgetItemId)
            });
        }

        private static bool RequiresMatchingUnit(QtoBudgetMappingRule rule)
        {
            return string.IsNullOrWhiteSpace(rule.QuantityRule)
                || string.Equals(rule.QuantityRule, QtoQuantityRuleType.SourceQuantity, StringComparison.OrdinalIgnoreCase)
                || string.Equals(rule.QuantityRule, QtoQuantityRuleType.CadLength, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsStatus(QtoBudgetMappingRule rule, string status)
        {
            return rule != null && string.Equals(rule.Status, status, StringComparison.OrdinalIgnoreCase);
        }

        private static bool Match(string expected, string actual)
        {
            return string.IsNullOrWhiteSpace(expected)
                || string.Equals(expected.Trim(), (actual ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase);
        }

        private static string Normalize(string value)
        {
            return (value ?? string.Empty).Trim().ToUpperInvariant();
        }
    }
}
