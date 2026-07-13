using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace QtoWirePlugin
{
    [DataContract]
    public sealed class QtoCompanyBudgetItem
    {
        public QtoCompanyBudgetItem()
        {
            Aliases = new List<string>();
        }

        [DataMember(Order = 1)] public string CompanyBudgetItemId { get; set; }
        [DataMember(Order = 2)] public string CanonicalName { get; set; }
        [DataMember(Order = 3)] public string Unit { get; set; }
        [DataMember(Order = 4)] public string HierarchyPath { get; set; }
        [DataMember(Order = 5)] public string SourceSheetHint { get; set; }
        [DataMember(Order = 6)] public string CanonicalSignature { get; set; }
        [DataMember(Order = 7)] public List<string> Aliases { get; set; }
        [DataMember(Order = 8)] public int OccurrenceIndex { get; set; }
        [DataMember(Order = 9)] public string Status { get; set; }
    }

    [DataContract]
    public sealed class QtoCompanyBudgetProfile
    {
        public QtoCompanyBudgetProfile()
        {
            SchemaVersion = 2;
            Items = new List<QtoCompanyBudgetItem>();
            MappingRules = new List<QtoBudgetMappingRule>();
        }

        [DataMember(Order = 1)] public int SchemaVersion { get; set; }
        [DataMember(Order = 2)] public string ProfileId { get; set; }
        [DataMember(Order = 3)] public int Version { get; set; }
        [DataMember(Order = 4)] public string BaselineWorkbookPath { get; set; }
        [DataMember(Order = 5)] public string BaselineWorkbookFingerprint { get; set; }
        [DataMember(Order = 6)] public DateTime UpdatedAt { get; set; }
        [DataMember(Order = 7)] public List<QtoCompanyBudgetItem> Items { get; set; }
        [DataMember(Order = 8)] public List<QtoBudgetMappingRule> MappingRules { get; set; }
    }
}
