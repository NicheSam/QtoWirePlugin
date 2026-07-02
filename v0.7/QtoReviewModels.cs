using System;
using System.Collections.Generic;

namespace QtoWirePlugin
{
    public static class QtoReviewSeverity
    {
        public const string Info = "info";
        public const string Warning = "warning";
        public const string Error = "error";
    }

    public static class QtoReviewIssueType
    {
        public const string MissingSyncId = "MissingSyncId";
        public const string DuplicateSyncId = "DuplicateSyncId";
        public const string MissingSystemCode = "MissingSystemCode";
        public const string MissingEquipmentTypeCode = "MissingEquipmentTypeCode";
        public const string MissingQuantityBasis = "MissingQuantityBasis";
        public const string ZeroLength = "ZeroLength";
        public const string CatalogNotLoaded = "CatalogNotLoaded";
        public const string CatalogMissing = "CatalogMissing";
        public const string CatalogDeprecated = "CatalogDeprecated";
        public const string CatalogUpdated = "CatalogUpdated";
        public const string CadObjectMissingInExcel = "CadObjectMissingInExcel";
        public const string ExcelRowMissingCadObject = "ExcelRowMissingCadObject";
    }

    public static class QtoCatalogItemStatus
    {
        public const string Active = "active";
        public const string Deprecated = "deprecated";
        public const string Missing = "missing";
        public const string Updated = "updated";
        public const string Unconfigured = "unconfigured";
    }

    public class QtoValidationResult
    {
        public QtoValidationResult()
        {
            ReviewItems = new List<QtoReviewItem>();
        }

        public List<QtoReviewItem> ReviewItems { get; private set; }
        public int ScannedObjectCount { get; set; }

        public bool HasIssues
        {
            get { return ReviewItems.Count > 0; }
        }

        public bool HasErrors
        {
            get
            {
                foreach (QtoReviewItem item in ReviewItems)
                {
                    if (string.Equals(item.Severity, QtoReviewSeverity.Error, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }

                return false;
            }
        }
    }

    public class QtoValidationTarget
    {
        public string ObjectId { get; set; }
        public string ObjectHandle { get; set; }
        public string QtoType { get; set; }
        public string SyncId { get; set; }
        public string LegacyQtoId { get; set; }
        public string SystemCode { get; set; }
        public string EquipmentTypeCode { get; set; }
        public string QuantityBasis { get; set; }
        public string BlockName { get; set; }
        public string SourceCatalogId { get; set; }
        public string CatalogVersion { get; set; }
        public double LengthM { get; set; }
        public bool IsBlockReference { get; set; }
    }

    public class QtoCatalogSnapshot
    {
        private readonly Dictionary<string, QtoCatalogItem> itemsByBlockName;

        public QtoCatalogSnapshot()
        {
            itemsByBlockName = new Dictionary<string, QtoCatalogItem>(StringComparer.OrdinalIgnoreCase);
            IsLoaded = false;
        }

        public bool IsLoaded { get; set; }

        public void Add(QtoCatalogItem item)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.BlockName))
            {
                return;
            }

            itemsByBlockName[item.BlockName] = item;
        }

        public bool TryFindByBlockName(string blockName, out QtoCatalogItem item)
        {
            item = null;
            if (string.IsNullOrWhiteSpace(blockName))
            {
                return false;
            }

            return itemsByBlockName.TryGetValue(blockName, out item);
        }
    }

    public class QtoCatalogItem
    {
        public string CatalogId { get; set; }
        public string BlockName { get; set; }
        public string SystemCode { get; set; }
        public string EquipmentTypeCode { get; set; }
        public string QuantityBasis { get; set; }
        public string CatalogVersion { get; set; }
        public string Status { get; set; }
    }

    public class QtoRepairResult
    {
        public QtoRepairResult()
        {
            Items = new List<QtoRepairItem>();
        }

        public List<QtoRepairItem> Items { get; private set; }

        public int RepairedCount
        {
            get
            {
                int count = 0;
                foreach (QtoRepairItem item in Items)
                {
                    if (item.Repaired)
                    {
                        count++;
                    }
                }

                return count;
            }
        }
    }

    public class QtoRepairItem
    {
        public string IssueType { get; set; }
        public string ObjectId { get; set; }
        public string ObjectHandle { get; set; }
        public string OldSyncId { get; set; }
        public string NewSyncId { get; set; }
        public bool Repaired { get; set; }
        public string Message { get; set; }
    }
}
