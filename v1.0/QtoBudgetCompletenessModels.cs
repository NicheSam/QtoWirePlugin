using System.Collections.Generic;

namespace QtoWirePlugin
{
    public static class QtoBudgetCompletenessIssueType
    {
        public const string UnmappedCadSource = "BudgetUnmappedCadSource";
        public const string PendingMapping = "BudgetPendingMapping";
        public const string BlockedMapping = "BudgetBlockedMapping";
        public const string BudgetWithoutCadSource = "BudgetWithoutCadSource";
        public const string InvalidBudgetTarget = "BudgetInvalidTarget";
        public const string UnitConflict = "BudgetUnitConflict";
        public const string DuplicateMapping = "BudgetDuplicateMapping";
    }

    public sealed class QtoBudgetSourceGroup
    {
        public string Key { get; set; }
        public string SystemCode { get; set; }
        public string EquipmentTypeCode { get; set; }
        public string CadMeasureType { get; set; }
        public string CableType { get; set; }
        public string ConduitType { get; set; }
        public string Unit { get; set; }
        public int ObjectCount { get; set; }
        public double Quantity { get; set; }
    }

    public sealed class QtoBudgetCompletenessIssue
    {
        public string IssueType { get; set; }
        public string Category { get; set; }
        public string Severity { get; set; }
        public string SourceKey { get; set; }
        public string BudgetItemId { get; set; }
        public string SystemCode { get; set; }
        public string EquipmentTypeCode { get; set; }
        public string CadMeasureType { get; set; }
        public string BudgetItemName { get; set; }
        public string Message { get; set; }
        public string SuggestedAction { get; set; }
        public string TechnicalDetail { get; set; }

        public string SeverityDisplay { get { return QtoReviewService.ResolveSeverityDisplay(Severity); } }
        public string CategoryDisplay { get { return Category ?? string.Empty; } }
    }

    public sealed class QtoBudgetCompletenessSummary
    {
        public int CadGroupCount { get; set; }
        public int ConfirmedCadGroupCount { get; set; }
        public int PendingCadGroupCount { get; set; }
        public int UnmappedCadGroupCount { get; set; }
        public int BudgetDetailCount { get; set; }
        public int CadSourcedBudgetCount { get; set; }
        public int ManualBudgetCount { get; set; }
        public int FixedBudgetCount { get; set; }
        public int ExcludedBudgetCount { get; set; }
        public int MissingSourceBudgetCount { get; set; }
    }

    public sealed class QtoBudgetCompletenessResult
    {
        public QtoBudgetCompletenessResult()
        {
            Summary = new QtoBudgetCompletenessSummary();
            Issues = new List<QtoBudgetCompletenessIssue>();
            SourceGroups = new List<QtoBudgetSourceGroup>();
        }

        public QtoBudgetCompletenessSummary Summary { get; private set; }
        public List<QtoBudgetCompletenessIssue> Issues { get; private set; }
        public List<QtoBudgetSourceGroup> SourceGroups { get; private set; }
    }
}
