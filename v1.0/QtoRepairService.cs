using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;

namespace QtoWirePlugin
{
    public static class QtoRepairService
    {
        public static QtoRepairResult RepairDrawing(Transaction transaction, Database database)
        {
            return RepairDrawing(transaction, database, null);
        }

        public static QtoRepairResult RepairSelected(Transaction transaction, Database database, IEnumerable<string> objectHandles)
        {
            HashSet<string> selectedHandles = new HashSet<string>(
                objectHandles ?? new string[0],
                StringComparer.OrdinalIgnoreCase);
            return RepairDrawing(transaction, database, selectedHandles);
        }

        private static QtoRepairResult RepairDrawing(Transaction transaction, Database database, HashSet<string> selectedHandles)
        {
            if (transaction == null)
            {
                throw new ArgumentNullException("transaction");
            }

            if (database == null)
            {
                throw new ArgumentNullException("database");
            }

            BlockTable blockTable = (BlockTable)transaction.GetObject(database.BlockTableId, OpenMode.ForRead);
            BlockTableRecord modelSpace = (BlockTableRecord)transaction.GetObject(blockTable[BlockTableRecord.ModelSpace], OpenMode.ForRead);
            List<Entity> qtoEntities = new List<Entity>();

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

                qtoEntities.Add(entity);
            }

            QtoRepairResult result = new QtoRepairResult();
            foreach (Entity entity in qtoEntities)
            {
                Dictionary<string, string> data = QtoXDataHelper.GetXData(entity);
                string syncId = Get(data, QtoSyncXDataKeys.SyncId);
                if (!string.IsNullOrWhiteSpace(syncId) || !ShouldRepair(entity, selectedHandles))
                {
                    continue;
                }

                string newSyncId = AssignNewSyncId(entity, database, transaction, data);
                result.Items.Add(BuildRepairItem(QtoReviewIssueType.MissingSyncId, entity, string.Empty, newSyncId, true, "Assigned missing QTO_SYNC_ID."));
            }

            IEnumerable<IGrouping<string, Entity>> duplicateGroups = qtoEntities
                .Select(entity => new { Entity = entity, SyncId = Get(QtoXDataHelper.GetXData(entity), QtoSyncXDataKeys.SyncId) })
                .Where(item => !string.IsNullOrWhiteSpace(item.SyncId))
                .GroupBy(item => item.SyncId, item => item.Entity, StringComparer.OrdinalIgnoreCase)
                .Where(group => group.Count() > 1);

            foreach (IGrouping<string, Entity> group in duplicateGroups)
            {
                List<Entity> groupEntities = group.ToList();
                List<Entity> selectedEntities = groupEntities.Where(entity => ShouldRepair(entity, selectedHandles)).ToList();
                if (selectedEntities.Count == 0)
                {
                    continue;
                }

                Entity survivor = groupEntities.FirstOrDefault(entity => !selectedEntities.Contains(entity)) ?? groupEntities[0];
                foreach (Entity entity in selectedEntities)
                {
                    if (entity.ObjectId == survivor.ObjectId)
                    {
                        continue;
                    }

                    Dictionary<string, string> data = QtoXDataHelper.GetXData(entity);
                    string newSyncId = AssignNewSyncId(entity, database, transaction, data);
                    QtoCopyArrayRuleService.MarkCopiedObjectForReview(
                        data,
                        group.Key,
                        newSyncId,
                        QtoCopyArrayRuleService.ReasonCopiedSyncIdRebuilt);
                    QtoXDataHelper.SetXData(entity, database, transaction, data);
                    result.Items.Add(BuildRepairItem(QtoReviewIssueType.DuplicateSyncId, entity, group.Key, newSyncId, true, "Replaced duplicate QTO_SYNC_ID and cleared copied unique numbers."));
                }
            }

            return result;
        }

        private static bool ShouldRepair(Entity entity, HashSet<string> selectedHandles)
        {
            return selectedHandles == null
                || selectedHandles.Contains(entity.ObjectId.Handle.ToString());
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
