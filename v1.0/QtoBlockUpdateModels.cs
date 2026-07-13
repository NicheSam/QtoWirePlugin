using System.Collections.Generic;

namespace QtoWirePlugin
{
    public static class QtoBlockUpdateStatus
    {
        public const string Current = "已是最新版";
        public const string UpdateAvailable = "有新版可更新";
        public const string MissingSource = "來源缺失";
        public const string Unsupported = "不支援更新";
        public const string ProjectMissing = "專案尚未使用";
    }

    public sealed class QtoBlockUpdateCandidate
    {
        public bool Selected { get; set; }
        public string CatalogId { get; set; }
        public string BlockName { get; set; }
        public string DisplayName { get; set; }
        public string Status { get; set; }
        public string SourceDwg { get; set; }
        public int ReferenceCount { get; set; }
        public string AttributeDifference { get; set; }
        public string DynamicDifference { get; set; }
        public string QtoImpact { get; set; }
        public string Detail { get; set; }
        public QtoBlockCatalogItem CatalogItem { get; set; }
        public bool CanUpdate { get { return Status == QtoBlockUpdateStatus.UpdateAvailable; } }
    }

    public sealed class QtoBlockUpdateResult
    {
        public QtoBlockUpdateResult() { Messages = new List<string>(); }
        public int UpdatedDefinitionCount { get; set; }
        public int UpdatedReferenceCount { get; set; }
        public int ReviewCount { get; set; }
        public List<string> Messages { get; private set; }
    }
}
