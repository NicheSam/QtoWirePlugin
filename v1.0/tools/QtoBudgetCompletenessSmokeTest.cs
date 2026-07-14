using System;
using System.Collections.Generic;
using System.Linq;
using QtoWirePlugin;

internal static class QtoBudgetCompletenessSmokeTest
{
    private static int Main()
    {
        try
        {
            List<QtoSyncRow> rows = new List<QtoSyncRow>
            {
                new QtoSyncRow
                {
                    SystemCode = "資訊",
                    EquipmentTypeCode = "DATA_OUTLET",
                    CadMeasureType = "OUTLET",
                    Unit = "點",
                    Quantity = 12
                }
            };
            QtoBudgetProjectData project = new QtoBudgetProjectData { SchemaVersion = 1 };
            project.MasterItems.Add(Detail("B1", "資訊插座", "點"));
            project.MasterItems.Add(Detail("B2", "安裝工資", "式"));
            project.MasterItems.Add(Detail("B3", "另料", "式"));
            project.MappingRules.Add(Rule("R1", "B1", "candidate"));

            QtoBudgetCompletenessResult pending = QtoBudgetCompletenessService.Analyze(rows, project);
            Assert(pending.Summary.PendingCadGroupCount == 1, "Candidate mapping must remain pending.");
            Assert(pending.Issues.Any(issue => issue.IssueType == QtoBudgetCompletenessIssueType.PendingMapping), "Pending issue is missing.");

            project.MappingRules[0].Status = "confirmed";
            QtoBudgetCompletenessResult confirmed = QtoBudgetCompletenessService.Analyze(rows, project);
            Assert(confirmed.Summary.ConfirmedCadGroupCount == 1, "Confirmed mapping must cover the CAD group.");
            Assert(confirmed.Summary.CadSourcedBudgetCount == 1, "Confirmed target must have a CAD source.");
            Assert(confirmed.Summary.MissingSourceBudgetCount == 2, "Unclassified budget rows must stay visible.");

            project.CoverageOverrides.Add(new QtoBudgetCoverageOverride { BudgetItemId = "B2", CoverageMode = QtoBudgetCoverageMode.Manual, UpdatedAt = DateTime.UtcNow });
            project.CoverageOverrides.Add(new QtoBudgetCoverageOverride { BudgetItemId = "B3", CoverageMode = QtoBudgetCoverageMode.Excluded, UpdatedAt = DateTime.UtcNow });
            QtoBudgetCompletenessResult classified = QtoBudgetCompletenessService.Analyze(rows, project);
            Assert(classified.Summary.ManualBudgetCount == 1 && classified.Summary.ExcludedBudgetCount == 1, "Manual and excluded classifications must be preserved.");
            Assert(classified.Summary.MissingSourceBudgetCount == 0, "Classified rows must not remain unresolved.");

            project.MasterItems[0].Unit = "組";
            project.MappingRules.Add(Rule("R2", "B1", "confirmed"));
            QtoBudgetCompletenessResult conflicts = QtoBudgetCompletenessService.Analyze(rows, project);
            Assert(conflicts.Issues.Any(issue => issue.IssueType == QtoBudgetCompletenessIssueType.UnitConflict), "Unit conflict must be reported.");
            Assert(conflicts.Issues.Any(issue => issue.IssueType == QtoBudgetCompletenessIssueType.DuplicateMapping), "Duplicate mapping must be reported.");

            IList<QtoReviewItem> reviewItems = QtoBudgetCompletenessService.ToReviewItems(conflicts);
            Assert(reviewItems.All(item => item.CategoryDisplay == "預算完整性"), "Completeness issues must enter the shared Review category.");

            Console.WriteLine("QtoWirePlugin v1.0.1 budget completeness smoke test passed.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.ToString());
            return 1;
        }
    }

    private static QtoBudgetMasterItem Detail(string id, string name, string unit)
    {
        return new QtoBudgetMasterItem { BudgetItemId = id, RowType = QtoBudgetRowType.Detail, ItemName = name, Unit = unit };
    }

    private static QtoBudgetMappingRule Rule(string id, string budgetItemId, string status)
    {
        return new QtoBudgetMappingRule
        {
            RuleId = id,
            SystemCode = "資訊",
            EquipmentTypeCode = "DATA_OUTLET",
            CadMeasureType = "OUTLET",
            Unit = "點",
            BudgetItemId = budgetItemId,
            QuantityRule = QtoQuantityRuleType.SourceQuantity,
            Status = status,
            UpdatedAt = DateTime.UtcNow
        };
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
