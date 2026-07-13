using System;
using System.Collections.Generic;
using System.Linq;

namespace QtoWirePlugin
{
    public static class QtoBudgetMappingEngine
    {
        public static IList<QtoMappedBudgetRow> Build(IList<QtoSyncRow> sourceRows, QtoBudgetProjectData project)
        {
            List<QtoMappedBudgetRow> result = (project == null ? new List<QtoBudgetMasterItem>() : project.MasterItems)
                .OrderBy(i => i.SortOrder)
                .Select(i => new QtoMappedBudgetRow { MasterItem = i, MappingStatus = i.RowType == QtoBudgetRowType.Detail ? "needs_review" : string.Empty })
                .ToList();
            Dictionary<string, QtoMappedBudgetRow> byId = result.ToDictionary(r => r.MasterItem.BudgetItemId, StringComparer.OrdinalIgnoreCase);
            if (project == null)
            {
                return result;
            }

            foreach (QtoBudgetMappingRule rule in project.MappingRules.Where(r => string.Equals(r.Status, "confirmed", StringComparison.OrdinalIgnoreCase)))
            {
                QtoMappedBudgetRow target;
                if (!byId.TryGetValue(rule.BudgetItemId ?? string.Empty, out target))
                {
                    continue;
                }

                List<QtoSyncRow> matches = (sourceRows ?? new List<QtoSyncRow>()).Where(r => Matches(rule, r)).ToList();
                target.Quantity += Calculate(rule, matches);
                target.SourceCount += matches.Count;
                target.MappingStatus = "confirmed";
                target.ReviewReason = string.Empty;
            }

            foreach (QtoMappedBudgetRow row in result.Where(r => r.MasterItem.RowType == QtoBudgetRowType.Detail && r.SourceCount == 0))
            {
                row.ReviewReason = "尚無已確認 mapping 或目前 CAD 沒有符合來源。";
            }
            return result;
        }

        private static bool Matches(QtoBudgetMappingRule rule, QtoSyncRow row)
        {
            return Match(rule.SystemCode, row.SystemCode)
                && Match(rule.EquipmentTypeCode, row.EquipmentTypeCode)
                && Match(rule.CadMeasureType, row.CadMeasureType)
                && Match(rule.CableType, row.CableType)
                && Match(rule.ConduitType, row.ConduitType)
                && Match(rule.Unit, row.Unit);
        }

        private static bool Match(string expected, string actual)
        {
            return string.IsNullOrWhiteSpace(expected) || string.Equals(expected.Trim(), (actual ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase);
        }

        private static double Calculate(QtoBudgetMappingRule rule, IList<QtoSyncRow> rows)
        {
            if (string.Equals(rule.QuantityRule, QtoQuantityRuleType.Fixed, StringComparison.OrdinalIgnoreCase)) return rows.Count == 0 ? 0 : rule.FixedQuantity;
            if (string.Equals(rule.QuantityRule, QtoQuantityRuleType.PerFloor, StringComparison.OrdinalIgnoreCase)) return rows.Select(r => r.Floor ?? string.Empty).Where(v => !string.IsNullOrWhiteSpace(v)).Distinct(StringComparer.OrdinalIgnoreCase).Count() * Factor(rule);
            if (string.Equals(rule.QuantityRule, QtoQuantityRuleType.PerArea, StringComparison.OrdinalIgnoreCase)) return rows.Select(r => r.Area ?? string.Empty).Where(v => !string.IsNullOrWhiteSpace(v)).Distinct(StringComparer.OrdinalIgnoreCase).Count() * Factor(rule);
            double value = string.Equals(rule.QuantityRule, QtoQuantityRuleType.CadLength, StringComparison.OrdinalIgnoreCase) ? rows.Sum(r => r.LengthM) : rows.Sum(r => r.Quantity);
            if (string.Equals(rule.QuantityRule, QtoQuantityRuleType.Waste, StringComparison.OrdinalIgnoreCase)) return value * (1d + rule.Factor / 100d);
            if (string.Equals(rule.QuantityRule, QtoQuantityRuleType.Multiplier, StringComparison.OrdinalIgnoreCase)) return value * Factor(rule);
            return value;
        }

        private static double Factor(QtoBudgetMappingRule rule)
        {
            return Math.Abs(rule.Factor) < 0.0000001d ? 1d : rule.Factor;
        }
    }
}
