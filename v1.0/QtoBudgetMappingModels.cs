using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace QtoWirePlugin
{
    public static class QtoBudgetRowType
    {
        public const string System = "system";
        public const string Category = "category";
        public const string Parent = "parent";
        public const string Detail = "detail";
    }

    public static class QtoQuantityRuleType
    {
        public const string SourceQuantity = "source_quantity";
        public const string CadLength = "cad_length";
        public const string Fixed = "fixed";
        public const string Multiplier = "multiplier";
        public const string Waste = "waste";
        public const string PerFloor = "per_floor";
        public const string PerArea = "per_area";
    }

    [DataContract]
    public sealed class QtoBudgetMasterItem
    {
        [DataMember(Order = 1)] public string BudgetItemId { get; set; }
        [DataMember(Order = 2)] public string ParentItemId { get; set; }
        [DataMember(Order = 3)] public string RowType { get; set; }
        [DataMember(Order = 4)] public string SourceSheet { get; set; }
        [DataMember(Order = 5)] public int SourceRow { get; set; }
        [DataMember(Order = 6)] public string ItemNo { get; set; }
        [DataMember(Order = 7)] public string ItemName { get; set; }
        [DataMember(Order = 8)] public string Unit { get; set; }
        [DataMember(Order = 9)] public string UnitPrice { get; set; }
        [DataMember(Order = 10)] public string CostUnitPrice { get; set; }
        [DataMember(Order = 11)] public string Multiplier { get; set; }
        [DataMember(Order = 12)] public string Brand { get; set; }
        [DataMember(Order = 13)] public string BaseCost { get; set; }
        [DataMember(Order = 14)] public string Labor { get; set; }
        [DataMember(Order = 15)] public string Remark { get; set; }
        [DataMember(Order = 16)] public int SortOrder { get; set; }
        [DataMember(Order = 17)] public bool Hidden { get; set; }
    }

    [DataContract]
    public sealed class QtoBudgetMappingRule
    {
        [DataMember(Order = 1)] public string RuleId { get; set; }
        [DataMember(Order = 2)] public string SystemCode { get; set; }
        [DataMember(Order = 3)] public string EquipmentTypeCode { get; set; }
        [DataMember(Order = 4)] public string CadMeasureType { get; set; }
        [DataMember(Order = 5)] public string CableType { get; set; }
        [DataMember(Order = 6)] public string ConduitType { get; set; }
        [DataMember(Order = 7)] public string Unit { get; set; }
        [DataMember(Order = 8)] public string BudgetItemId { get; set; }
        [DataMember(Order = 9)] public string QuantityRule { get; set; }
        [DataMember(Order = 10)] public double Factor { get; set; }
        [DataMember(Order = 11)] public double FixedQuantity { get; set; }
        [DataMember(Order = 12)] public string Status { get; set; }
        [DataMember(Order = 13)] public string Scope { get; set; }
        [DataMember(Order = 14)] public string ReviewReason { get; set; }
        [DataMember(Order = 15)] public DateTime UpdatedAt { get; set; }
        [DataMember(Order = 16)] public string CompanyBudgetItemId { get; set; }
    }

    [DataContract]
    public sealed class QtoProjectBudgetBinding
    {
        [DataMember(Order = 1)] public string CompanyBudgetItemId { get; set; }
        [DataMember(Order = 2)] public string ProjectBudgetItemId { get; set; }
        [DataMember(Order = 3)] public string Status { get; set; }
        [DataMember(Order = 4)] public string MatchReason { get; set; }
        [DataMember(Order = 5)] public DateTime UpdatedAt { get; set; }
    }

    [DataContract]
    public sealed class QtoBudgetProjectData
    {
        public QtoBudgetProjectData()
        {
            MasterItems = new List<QtoBudgetMasterItem>();
            MappingRules = new List<QtoBudgetMappingRule>();
            CompanyBindings = new List<QtoProjectBudgetBinding>();
        }

        [DataMember(Order = 1)] public int SchemaVersion { get; set; }
        [DataMember(Order = 2)] public string SourceWorkbookPath { get; set; }
        [DataMember(Order = 3)] public DateTime ImportedAt { get; set; }
        [DataMember(Order = 4)] public List<QtoBudgetMasterItem> MasterItems { get; set; }
        [DataMember(Order = 5)] public List<QtoBudgetMappingRule> MappingRules { get; set; }
        [DataMember(Order = 6)] public string CompanyProfileId { get; set; }
        [DataMember(Order = 7)] public int CompanyProfileVersion { get; set; }
        [DataMember(Order = 8)] public List<QtoProjectBudgetBinding> CompanyBindings { get; set; }
    }

    public sealed class QtoMappedBudgetRow
    {
        public QtoBudgetMasterItem MasterItem { get; set; }
        public double Quantity { get; set; }
        public int SourceCount { get; set; }
        public string MappingStatus { get; set; }
        public string ReviewReason { get; set; }
    }
}
