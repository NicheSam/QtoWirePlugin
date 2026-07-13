namespace QtoWirePlugin
{
    public static class QtoSyncFields
    {
        public const string SyncId = QtoXDataHelper.KeyQtoSyncId;
        public const string SyncStatus = QtoXDataHelper.KeyQtoSyncStatus;
        public const string LastSyncAt = QtoXDataHelper.KeyQtoLastSyncAt;
        public const string LastModifiedAt = QtoXDataHelper.KeyQtoLastModifiedAt;
        public const string SourceCatalogId = QtoXDataHelper.KeyQtoSourceCatalogId;
        public const string CatalogVersion = QtoXDataHelper.KeyQtoCatalogVersion;
        public const string CopySourceId = QtoXDataHelper.KeyQtoCopySourceId;
    }

    public static class QtoSyncXDataKeys
    {
        public const string SyncId = QtoXDataHelper.KeyQtoSyncId;
        public const string SyncStatus = QtoXDataHelper.KeyQtoSyncStatus;
        public const string LastSyncAt = QtoXDataHelper.KeyQtoLastSyncAt;
        public const string LastModifiedAt = QtoXDataHelper.KeyQtoLastModifiedAt;
        public const string SourceCatalogId = QtoXDataHelper.KeyQtoSourceCatalogId;
        public const string CatalogVersion = QtoXDataHelper.KeyQtoCatalogVersion;
        public const string CopySourceId = QtoXDataHelper.KeyQtoCopySourceId;
    }

    public static class QtoSyncStatus
    {
        public const string Active = "active";
        public const string Current = Active;
        public const string NeedsReview = "needs_review";
        public const string Blocked = "blocked";
        public const string Deleted = "deleted";
        public const string Missing = "missing";
    }

    public class QtoDuplicateSyncIdIssue
    {
        public string SyncId { get; set; }
        public System.Collections.Generic.List<Autodesk.AutoCAD.DatabaseServices.ObjectId> ObjectIds { get; set; }

        public QtoDuplicateSyncIdIssue()
        {
            ObjectIds = new System.Collections.Generic.List<Autodesk.AutoCAD.DatabaseServices.ObjectId>();
        }
    }

    public class QtoDuplicateSyncIdScanResult
    {
        public int QtoEntityCount { get; set; }
        public int MissingSyncIdCount { get; set; }
        public System.Collections.Generic.List<Autodesk.AutoCAD.DatabaseServices.ObjectId> MissingSyncIdObjectIds { get; set; }
        public System.Collections.Generic.List<QtoDuplicateSyncIdIssue> DuplicateIssues { get; set; }

        public QtoDuplicateSyncIdScanResult()
        {
            MissingSyncIdObjectIds = new System.Collections.Generic.List<Autodesk.AutoCAD.DatabaseServices.ObjectId>();
            DuplicateIssues = new System.Collections.Generic.List<QtoDuplicateSyncIdIssue>();
        }
    }

    public class QtoSyncIdRepairResult
    {
        public int MissingCreatedCount { get; set; }
        public int DuplicateRepairedCount { get; set; }
        public System.Collections.Generic.List<string> Messages { get; set; }

        public QtoSyncIdRepairResult()
        {
            Messages = new System.Collections.Generic.List<string>();
        }
    }
}
