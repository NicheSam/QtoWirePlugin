using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;

namespace QtoWirePlugin
{
    public sealed class QtoCopyArrayRuleService
    {
        public const string KeyQtoSyncId = "QTO_SYNC_ID";
        public const string KeyCopySourceSyncId = "QTO_COPY_SOURCE_SYNC_ID";
        public const string KeySyncStatus = "QTO_SYNC_STATUS";
        public const string KeyNeedsConfirmation = "QTO_NEEDS_CONFIRMATION";
        public const string KeyCatalogVersion = "CATALOG_VERSION";
        public const string KeySourceCatalogId = "SOURCE_CATALOG_ID";
        public const string KeyDeviceId = "DEVICE_ID";
        public const string KeyCircuitNo = "CIRCUIT_NO";
        public const string StatusReview = "review";
        public const string StatusDeleted = "deleted";
        public const string StatusMissing = "missing";
        public const string ReasonCopyNumberConfirmRequired = "COPY_NUMBER_CONFIRM_REQUIRED";
        public const string ReasonCopiedSyncIdRebuilt = "COPIED_SYNC_ID_REBUILT";
        public const string ReasonDeletedInCad = "DELETED_IN_CAD";
        public const string ReasonMissingInCad = "MISSING_IN_CAD";

        private static readonly string[] UniqueKeysToClear = new string[]
        {
            QtoXDataHelper.KeyQtoId,
            QtoXDataHelper.KeyOutletId,
            QtoXDataHelper.KeyRouteId,
            KeyDeviceId,
            "UNIQUE_DEVICE_NO",
            "UNIQUE_ROUTE_NO"
        };

        private static readonly string[] NumberKeysToConfirm = new string[]
        {
            QtoXDataHelper.KeyJbId,
            KeyCircuitNo,
            "LOOP_NO",
            "DEVICE_NO",
            "ROUTE_NO"
        };

        public static void MarkCopiedObjectForReview(Dictionary<string, string> data, string sourceSyncId, string newSyncId, string reason)
        {
            if (data == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(sourceSyncId))
            {
                data[KeyCopySourceSyncId] = sourceSyncId;
                data[QtoXDataHelper.KeyQtoCopySourceId] = sourceSyncId;
            }

            if (!string.IsNullOrWhiteSpace(newSyncId))
            {
                data[KeyQtoSyncId] = newSyncId;
            }

            ClearUniqueNumberFields(data);
            MarkForNumberConfirmation(data, string.IsNullOrWhiteSpace(reason) ? ReasonCopyNumberConfirmRequired : reason);
        }

        public IList<QtoCadChange> ApplyRules(Database database, IEnumerable<QtoCadChange> changes)
        {
            List<QtoCadChange> output = new List<QtoCadChange>();
            if (database == null || changes == null)
            {
                return output;
            }

            List<QtoCadChange> input = new List<QtoCadChange>();
            foreach (QtoCadChange change in changes)
            {
                if (change != null)
                {
                    input.Add(change.Clone());
                }
            }

            foreach (QtoCadChange change in input)
            {
                if (change.Kind == QtoCadChangeKind.Deleted)
                {
                    output.Add(BuildDeletedOrMissingEvent(change, false));
                }
            }

            try
            {
                using (Transaction tr = database.TransactionManager.StartTransaction())
                {
                    List<QtoCadChangeKind> inputKinds = new List<QtoCadChangeKind>();
                    foreach (QtoCadChange change in input)
                    {
                        inputKinds.Add(change.Kind);
                    }

                    Dictionary<string, int> syncIdCounts = QtoChangePerformancePolicy.RequiresSyncIdScan(inputKinds)
                        ? BuildSyncIdCounts(database, tr)
                        : new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                    Dictionary<string, int> sourceCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                    List<QtoCadChange> repairedAddedChanges = new List<QtoCadChange>();

                    foreach (QtoCadChange change in input)
                    {
                        if (change.Kind != QtoCadChangeKind.Added)
                        {
                            continue;
                        }

                        QtoCadChange repaired = ApplyAddedObjectRules(database, tr, change, syncIdCounts);
                        if (repaired == null)
                        {
                            continue;
                        }

                        repairedAddedChanges.Add(repaired);

                        if (!string.IsNullOrWhiteSpace(repaired.SourceSyncId))
                        {
                            int count;
                            sourceCounts.TryGetValue(repaired.SourceSyncId, out count);
                            sourceCounts[repaired.SourceSyncId] = count + 1;
                        }
                    }

                    foreach (QtoCadChange repaired in repairedAddedChanges)
                    {
                        int count;
                        if (!string.IsNullOrWhiteSpace(repaired.SourceSyncId) &&
                            sourceCounts.TryGetValue(repaired.SourceSyncId, out count) &&
                            count > 1)
                        {
                            repaired.Kind = QtoCadChangeKind.ArrayItem;
                        }

                        output.Add(repaired);
                    }

                    tr.Commit();
                }
            }
            catch (Exception ex)
            {
                QtoCadChange error = new QtoCadChange();
                error.Kind = QtoCadChangeKind.Error;
                error.ErrorMessage = ex.Message;
                output.Add(error);
            }

            foreach (QtoCadChange change in input)
            {
                if (change.Kind == QtoCadChangeKind.Modified ||
                    change.Kind == QtoCadChangeKind.Missing ||
                    change.Kind == QtoCadChangeKind.Error)
                {
                    output.Add(change.Clone());
                }
            }

            return output;
        }

        public QtoCadChange BuildDeletedOrMissingEvent(QtoCadChange change, bool isMissing)
        {
            QtoCadChange result = change == null ? new QtoCadChange() : change.Clone();
            result.Kind = isMissing ? QtoCadChangeKind.Missing : QtoCadChangeKind.Deleted;

            if (string.IsNullOrWhiteSpace(result.SyncId) && result.XDataSnapshot != null)
            {
                result.SyncId = GetValueOrEmpty(result.XDataSnapshot, KeyQtoSyncId);
            }

            if (string.IsNullOrWhiteSpace(result.QtoType) && result.XDataSnapshot != null)
            {
                result.QtoType = GetValueOrEmpty(result.XDataSnapshot, QtoXDataHelper.KeyQtoType);
            }

            if (result.XDataSnapshot == null)
            {
                result.XDataSnapshot = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            result.XDataSnapshot[KeySyncStatus] = isMissing ? StatusMissing : StatusDeleted;
            result.XDataSnapshot[QtoXDataHelper.KeyReviewReason] = isMissing ? ReasonMissingInCad : ReasonDeletedInCad;
            result.XDataSnapshot[KeyNeedsConfirmation] = "true";
            return result;
        }

        public string GenerateSyncId()
        {
            return "qto-" + Guid.NewGuid().ToString("N");
        }

        private QtoCadChange ApplyAddedObjectRules(
            Database database,
            Transaction tr,
            QtoCadChange change,
            Dictionary<string, int> syncIdCounts)
        {
            if (change.ObjectId.IsNull || change.ObjectId.IsErased)
            {
                return BuildDeletedOrMissingEvent(change, true);
            }

            Entity entity = tr.GetObject(change.ObjectId, OpenMode.ForWrite, false) as Entity;
            if (entity == null)
            {
                return null;
            }

            Dictionary<string, string> data = QtoXDataHelper.GetXData(entity);
            if (data.Count == 0 || string.IsNullOrWhiteSpace(GetValueOrEmpty(data, QtoXDataHelper.KeyQtoType)))
            {
                return null;
            }

            string currentSyncId = GetValueOrEmpty(data, KeyQtoSyncId);
            bool hadCopiedSyncId = !string.IsNullOrWhiteSpace(currentSyncId);
            bool duplicateSyncId = hadCopiedSyncId && GetCount(syncIdCounts, currentSyncId) > 1;

            QtoCadChange result = change.Clone();
            result.XDataSnapshot = new Dictionary<string, string>(data, StringComparer.OrdinalIgnoreCase);
            result.QtoType = GetValueOrEmpty(data, QtoXDataHelper.KeyQtoType);

            if (hadCopiedSyncId)
            {
                MarkCopiedObjectForReview(
                    data,
                    currentSyncId,
                    GenerateSyncId(),
                    duplicateSyncId ? ReasonCopiedSyncIdRebuilt : ReasonCopyNumberConfirmRequired);
                QtoXDataHelper.SetXData(entity, database, tr, data);

                result.Kind = QtoCadChangeKind.CopiedOrPasted;
                result.SourceSyncId = currentSyncId;
                result.SyncId = data[KeyQtoSyncId];
                result.XDataSnapshot = new Dictionary<string, string>(data, StringComparer.OrdinalIgnoreCase);
                return result;
            }

            data[KeyQtoSyncId] = GenerateSyncId();
            QtoXDataHelper.SetXData(entity, database, tr, data);

            result.Kind = QtoCadChangeKind.Added;
            result.SyncId = data[KeyQtoSyncId];
            result.XDataSnapshot = new Dictionary<string, string>(data, StringComparer.OrdinalIgnoreCase);
            return result;
        }

        private static Dictionary<string, int> BuildSyncIdCounts(Database database, Transaction tr)
        {
            Dictionary<string, int> counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            BlockTable blockTable = (BlockTable)tr.GetObject(database.BlockTableId, OpenMode.ForRead);
            BlockTableRecord modelSpace = (BlockTableRecord)tr.GetObject(blockTable[BlockTableRecord.ModelSpace], OpenMode.ForRead);

            foreach (ObjectId objectId in modelSpace)
            {
                Entity entity = tr.GetObject(objectId, OpenMode.ForRead, false) as Entity;
                if (entity == null)
                {
                    continue;
                }

                string syncId = QtoXDataHelper.GetXDataValue(entity, KeyQtoSyncId);
                if (string.IsNullOrWhiteSpace(syncId))
                {
                    continue;
                }

                int count;
                counts.TryGetValue(syncId, out count);
                counts[syncId] = count + 1;
            }

            return counts;
        }

        private static void ClearUniqueNumberFields(Dictionary<string, string> data)
        {
            foreach (string key in UniqueKeysToClear)
            {
                if (data.ContainsKey(key))
                {
                    data.Remove(key);
                }
            }
        }

        private static void MarkForNumberConfirmation(Dictionary<string, string> data, string reason)
        {
            data[KeySyncStatus] = StatusReview;
            data[KeyNeedsConfirmation] = "true";
            AppendReason(data, reason);

            foreach (string key in NumberKeysToConfirm)
            {
                string value = GetValueOrEmpty(data, key);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    AppendReason(data, key + "_CONFIRM_REQUIRED");
                }
            }
        }

        private static void AppendReason(Dictionary<string, string> data, string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                return;
            }

            string existing = GetValueOrEmpty(data, QtoXDataHelper.KeyReviewReason);
            if (existing.IndexOf(reason, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return;
            }

            data[QtoXDataHelper.KeyReviewReason] =
                string.IsNullOrWhiteSpace(existing) ? reason : existing + ";" + reason;
        }

        private static int GetCount(Dictionary<string, int> counts, string key)
        {
            int count;
            if (counts != null && counts.TryGetValue(key, out count))
            {
                return count;
            }

            return 0;
        }

        private static string GetValueOrEmpty(Dictionary<string, string> data, string key)
        {
            string value;
            if (data != null && data.TryGetValue(key, out value))
            {
                return value ?? string.Empty;
            }

            return string.Empty;
        }
    }
}
