using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;

namespace QtoWirePlugin
{
    public static class QtoRepairService
    {
        public static QtoRepairResult RepairDrawing(Transaction transaction, Database database)
        {
            if (transaction == null)
            {
                throw new ArgumentNullException("transaction");
            }

            if (database == null)
            {
                throw new ArgumentNullException("database");
            }

            QtoRepairResult result = new QtoRepairResult();
            Dictionary<string, ObjectId> firstBySyncId = new Dictionary<string, ObjectId>(StringComparer.OrdinalIgnoreCase);

            BlockTable blockTable = (BlockTable)transaction.GetObject(database.BlockTableId, OpenMode.ForRead);
            BlockTableRecord modelSpace = (BlockTableRecord)transaction.GetObject(blockTable[BlockTableRecord.ModelSpace], OpenMode.ForRead);

            foreach (ObjectId objectId in modelSpace)
            {
                Entity entity = transaction.GetObject(objectId, OpenMode.ForRead, false) as Entity;
                if (entity == null)
                {
                    continue;
                }

                Dictionary<string, string> data = QtoXDataHelper.GetXData(entity);
                if (!data.ContainsKey(QtoXDataHelper.KeyQtoType))
                {
                    continue;
                }

                string syncId = Get(data, QtoSyncXDataKeys.SyncId);
                if (string.IsNullOrWhiteSpace(syncId))
                {
                    string newSyncId = AssignNewSyncId(entity, database, transaction, data);
                    result.Items.Add(BuildRepairItem(QtoReviewIssueType.MissingSyncId, entity, string.Empty, newSyncId, true, "Assigned missing QTO_SYNC_ID."));
                    firstBySyncId[newSyncId] = objectId;
                    continue;
                }

                if (firstBySyncId.ContainsKey(syncId))
                {
                    string newSyncId = AssignNewSyncId(entity, database, transaction, data);
                    QtoCopyArrayRuleService.MarkCopiedObjectForReview(
                        data,
                        syncId,
                        newSyncId,
                        QtoCopyArrayRuleService.ReasonCopiedSyncIdRebuilt);
                    QtoXDataHelper.SetXData(entity, database, transaction, data);
                    result.Items.Add(BuildRepairItem(QtoReviewIssueType.DuplicateSyncId, entity, syncId, newSyncId, true, "Replaced duplicate QTO_SYNC_ID and cleared copied unique numbers."));
                    firstBySyncId[newSyncId] = objectId;
                    continue;
                }

                firstBySyncId[syncId] = objectId;
            }

            return result;
        }

        public static QtoSyncRow MarkDeleted(QtoSyncRow row, string reason)
        {
            return MarkStatus(row, QtoSyncStatus.Deleted, reason);
        }

        public static QtoSyncRow MarkMissing(QtoSyncRow row, string reason)
        {
            return MarkStatus(row, QtoSyncStatus.Missing, reason);
        }

        public static bool IsAutoRepairable(string issueType)
        {
            return QtoReviewService.CanAutoRepair(issueType);
        }

        private static string AssignNewSyncId(Entity entity, Database database, Transaction transaction, Dictionary<string, string> data)
        {
            string newSyncId = QtoSyncIdService.CreateSyncId();
            data[QtoSyncXDataKeys.SyncId] = newSyncId;
            data[QtoSyncXDataKeys.SyncStatus] = QtoSyncStatus.Active;
            data[QtoSyncXDataKeys.LastModifiedAt] = DateTime.UtcNow.ToString("o");
            QtoXDataHelper.SetXData(entity, database, transaction, data);
            return newSyncId;
        }

        private static QtoSyncRow MarkStatus(QtoSyncRow row, string status, string reason)
        {
            if (row == null)
            {
                row = new QtoSyncRow();
            }

            row.SyncStatus = status;
            row.ReviewReason = reason ?? string.Empty;
            row.LastCadModifiedAt = DateTime.UtcNow.ToString("o");
            return row;
        }

        private static QtoRepairItem BuildRepairItem(string issueType, Entity entity, string oldSyncId, string newSyncId, bool repaired, string message)
        {
            QtoRepairItem item = new QtoRepairItem();
            item.IssueType = issueType;
            item.ObjectId = entity.ObjectId.ToString();
            item.ObjectHandle = entity.ObjectId.Handle.ToString();
            item.OldSyncId = oldSyncId ?? string.Empty;
            item.NewSyncId = newSyncId ?? string.Empty;
            item.Repaired = repaired;
            item.Message = message ?? string.Empty;
            return item;
        }

        private static string Get(Dictionary<string, string> data, string key)
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
