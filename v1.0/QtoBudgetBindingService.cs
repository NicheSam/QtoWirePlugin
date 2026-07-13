using System;
using System.Collections.Generic;
using System.Linq;

namespace QtoWirePlugin
{
    public static class QtoBudgetBindingService
    {
        public static void RebuildBindings(QtoBudgetProjectData project, QtoCompanyBudgetProfile profile)
        {
            if (project == null) throw new ArgumentNullException("project");
            if (project.CompanyBindings == null) project.CompanyBindings = new List<QtoProjectBudgetBinding>();
            List<QtoProjectBudgetBinding> existingConfirmed = project.CompanyBindings
                .Where(b => b != null && string.Equals(b.Status, "confirmed", StringComparison.OrdinalIgnoreCase))
                .ToList();
            project.CompanyBindings.Clear();
            project.CompanyProfileId = profile == null ? string.Empty : profile.ProfileId;
            project.CompanyProfileVersion = profile == null ? 0 : profile.Version;
            if (profile == null) return;

            Dictionary<string, QtoBudgetMasterItem> byId = project.MasterItems
                .Where(i => i != null && !string.IsNullOrWhiteSpace(i.BudgetItemId))
                .ToDictionary(i => i.BudgetItemId, StringComparer.OrdinalIgnoreCase);
            HashSet<string> companyItemIds = new HashSet<string>(profile.Items
                .Where(i => i != null && !string.IsNullOrWhiteSpace(i.CompanyBudgetItemId))
                .Select(i => i.CompanyBudgetItemId), StringComparer.OrdinalIgnoreCase);
            foreach (QtoProjectBudgetBinding binding in existingConfirmed)
            {
                if (companyItemIds.Contains(binding.CompanyBudgetItemId ?? string.Empty)
                    && byId.ContainsKey(binding.ProjectBudgetItemId ?? string.Empty))
                {
                    project.CompanyBindings.Add(binding);
                }
            }
            HashSet<string> preservedCompanyIds = new HashSet<string>(project.CompanyBindings.Select(b => b.CompanyBudgetItemId), StringComparer.OrdinalIgnoreCase);
            Dictionary<string, List<QtoBudgetMasterItem>> bySignature = new Dictionary<string, List<QtoBudgetMasterItem>>(StringComparer.OrdinalIgnoreCase);
            foreach (QtoBudgetMasterItem item in project.MasterItems.Where(i => i != null && string.Equals(i.RowType, QtoBudgetRowType.Detail, StringComparison.OrdinalIgnoreCase)))
            {
                string hierarchy = QtoCompanyBudgetProfileStore.BuildHierarchyPath(item, byId);
                string signature = QtoCompanyBudgetProfileStore.BuildSignature(item.SourceSheet, hierarchy, item.ItemName, item.Unit);
                List<QtoBudgetMasterItem> list;
                if (!bySignature.TryGetValue(signature, out list))
                {
                    list = new List<QtoBudgetMasterItem>();
                    bySignature[signature] = list;
                }
                list.Add(item);
            }
            foreach (List<QtoBudgetMasterItem> list in bySignature.Values) list.Sort((left, right) => left.SortOrder.CompareTo(right.SortOrder));

            foreach (QtoCompanyBudgetItem companyItem in profile.Items.Where(i => i != null && string.Equals(i.Status, "active", StringComparison.OrdinalIgnoreCase)))
            {
                if (preservedCompanyIds.Contains(companyItem.CompanyBudgetItemId ?? string.Empty)) continue;
                List<QtoBudgetMasterItem> exact;
                if (bySignature.TryGetValue(companyItem.CanonicalSignature ?? string.Empty, out exact)
                    && companyItem.OccurrenceIndex >= 0 && exact.Count > companyItem.OccurrenceIndex)
                {
                    project.CompanyBindings.Add(CreateBinding(companyItem.CompanyBudgetItemId, exact[companyItem.OccurrenceIndex].BudgetItemId, "confirmed", "名稱、單位、階層與同名順序完全相同。"));
                    continue;
                }

                List<QtoBudgetMasterItem> aliasMatches = FindAliasMatches(companyItem, project.MasterItems);
                if (aliasMatches.Count == 1)
                {
                    project.CompanyBindings.Add(CreateBinding(companyItem.CompanyBudgetItemId, aliasMatches[0].BudgetItemId, "candidate", "命中公司別名，需人工確認。"));
                }
                else
                {
                    project.CompanyBindings.Add(CreateBinding(companyItem.CompanyBudgetItemId, string.Empty, "needs_review", aliasMatches.Count > 1 ? "找到多個可能品項。" : "案件預算中找不到對應品項。"));
                }
            }

            MergeCompanyRules(project, profile);
        }

        public static void MergeCompanyRules(QtoBudgetProjectData project, QtoCompanyBudgetProfile profile)
        {
            if (project == null) return;
            if (project.MappingRules == null) project.MappingRules = new List<QtoBudgetMappingRule>();
            project.MappingRules.RemoveAll(r => string.Equals(r.Scope, "company", StringComparison.OrdinalIgnoreCase));
            if (profile == null || profile.MappingRules == null || project.CompanyBindings == null) return;

            Dictionary<string, QtoProjectBudgetBinding> bindings = project.CompanyBindings
                .Where(b => b != null && string.Equals(b.Status, "confirmed", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(b.ProjectBudgetItemId))
                .GroupBy(b => b.CompanyBudgetItemId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
            foreach (QtoBudgetMappingRule companyRule in profile.MappingRules.Where(r => r != null && string.Equals(r.Status, "confirmed", StringComparison.OrdinalIgnoreCase)))
            {
                QtoProjectBudgetBinding binding;
                if (!bindings.TryGetValue(companyRule.CompanyBudgetItemId ?? string.Empty, out binding)) continue;
                QtoBudgetMappingRule rule = Clone(companyRule);
                rule.RuleId = "COMP-" + companyRule.RuleId + "-" + binding.ProjectBudgetItemId;
                rule.BudgetItemId = binding.ProjectBudgetItemId;
                rule.Scope = "company";
                project.MappingRules.Add(rule);
            }
        }

        public static int PublishRules(QtoBudgetProjectData project, QtoCompanyBudgetProfile profile, IEnumerable<QtoBudgetMappingRule> selectedRules)
        {
            if (project == null || profile == null) throw new InvalidOperationException("尚未建立公司預算基準。");
            Dictionary<string, QtoProjectBudgetBinding> byProjectItem = (project.CompanyBindings ?? new List<QtoProjectBudgetBinding>())
                .Where(b => b != null && string.Equals(b.Status, "confirmed", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(b.ProjectBudgetItemId))
                .GroupBy(b => b.ProjectBudgetItemId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
            int count = 0;
            foreach (QtoBudgetMappingRule source in (selectedRules ?? new List<QtoBudgetMappingRule>()).Where(r => r != null && string.Equals(r.Status, "confirmed", StringComparison.OrdinalIgnoreCase)))
            {
                QtoProjectBudgetBinding binding;
                if (!byProjectItem.TryGetValue(source.BudgetItemId ?? string.Empty, out binding)) continue;
                QtoBudgetMappingRule companyRule = Clone(source);
                companyRule.RuleId = "CR-" + Guid.NewGuid().ToString("N");
                companyRule.BudgetItemId = string.Empty;
                companyRule.CompanyBudgetItemId = binding.CompanyBudgetItemId;
                companyRule.Scope = "company";
                companyRule.UpdatedAt = DateTime.UtcNow;
                profile.MappingRules.RemoveAll(r => IsSameRule(r, companyRule));
                profile.MappingRules.Add(companyRule);
                count++;
            }
            return count;
        }

        public static int RemovePromotedProjectRules(QtoBudgetProjectData project, IEnumerable<QtoBudgetMappingRule> selectedRules)
        {
            if (project == null || project.MappingRules == null) return 0;
            Dictionary<string, QtoProjectBudgetBinding> byProjectItem = (project.CompanyBindings ?? new List<QtoProjectBudgetBinding>())
                .Where(b => b != null && string.Equals(b.Status, "confirmed", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(b.ProjectBudgetItemId))
                .GroupBy(b => b.ProjectBudgetItemId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
            HashSet<string> promotedIds = new HashSet<string>((selectedRules ?? new List<QtoBudgetMappingRule>())
                .Where(r => r != null
                    && string.Equals(r.Status, "confirmed", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(r.Scope, "company", StringComparison.OrdinalIgnoreCase)
                    && byProjectItem.ContainsKey(r.BudgetItemId ?? string.Empty))
                .Select(r => r.RuleId), StringComparer.OrdinalIgnoreCase);
            return project.MappingRules.RemoveAll(r => r != null && promotedIds.Contains(r.RuleId ?? string.Empty));
        }

        private static List<QtoBudgetMasterItem> FindAliasMatches(QtoCompanyBudgetItem companyItem, IEnumerable<QtoBudgetMasterItem> projectItems)
        {
            HashSet<string> aliases = new HashSet<string>((companyItem.Aliases ?? new List<string>()).Select(QtoCompanyBudgetProfileStore.NormalizeText), StringComparer.OrdinalIgnoreCase);
            if (aliases.Count == 0) return new List<QtoBudgetMasterItem>();
            string unit = QtoCompanyBudgetProfileStore.NormalizeText(companyItem.Unit);
            return (projectItems ?? new List<QtoBudgetMasterItem>())
                .Where(i => i != null
                    && string.Equals(i.RowType, QtoBudgetRowType.Detail, StringComparison.OrdinalIgnoreCase)
                    && aliases.Contains(QtoCompanyBudgetProfileStore.NormalizeText(i.ItemName))
                    && string.Equals(unit, QtoCompanyBudgetProfileStore.NormalizeText(i.Unit), StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        private static QtoProjectBudgetBinding CreateBinding(string companyId, string projectId, string status, string reason)
        {
            return new QtoProjectBudgetBinding { CompanyBudgetItemId = companyId, ProjectBudgetItemId = projectId, Status = status, MatchReason = reason, UpdatedAt = DateTime.UtcNow };
        }

        private static bool IsSameRule(QtoBudgetMappingRule left, QtoBudgetMappingRule right)
        {
            return left != null && right != null
                && Same(left.CompanyBudgetItemId, right.CompanyBudgetItemId)
                && Same(left.SystemCode, right.SystemCode)
                && Same(left.EquipmentTypeCode, right.EquipmentTypeCode)
                && Same(left.CadMeasureType, right.CadMeasureType)
                && Same(left.CableType, right.CableType)
                && Same(left.ConduitType, right.ConduitType)
                && Same(left.Unit, right.Unit)
                && Same(left.QuantityRule, right.QuantityRule)
                && Math.Abs(left.Factor - right.Factor) < 0.0000001d
                && Math.Abs(left.FixedQuantity - right.FixedQuantity) < 0.0000001d;
        }

        private static bool Same(string left, string right)
        {
            return string.Equals((left ?? string.Empty).Trim(), (right ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase);
        }

        private static QtoBudgetMappingRule Clone(QtoBudgetMappingRule source)
        {
            return new QtoBudgetMappingRule
            {
                RuleId = source.RuleId, SystemCode = source.SystemCode, EquipmentTypeCode = source.EquipmentTypeCode,
                CadMeasureType = source.CadMeasureType, CableType = source.CableType, ConduitType = source.ConduitType,
                Unit = source.Unit, BudgetItemId = source.BudgetItemId, CompanyBudgetItemId = source.CompanyBudgetItemId,
                QuantityRule = source.QuantityRule, Factor = source.Factor, FixedQuantity = source.FixedQuantity,
                Status = source.Status, Scope = source.Scope, ReviewReason = source.ReviewReason, UpdatedAt = source.UpdatedAt
            };
        }
    }
}
