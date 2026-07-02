using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;

namespace QtoWirePlugin
{
    public static class QtoSyncIdService
    {
        public static string GenerateSyncId()
        {
            return CreateSyncId();
        }

        public static string CreateSyncId()
        {
            return "QTO-" + Guid.NewGuid().ToString("N").ToUpperInvariant();
        }

        public static bool IsValidSyncId(string syncId)
        {
            return !string.IsNullOrWhiteSpace(syncId);
        }

        public static string GetSyncId(Entity entity)
        {
            if (entity == null)
            {
                return string.Empty;
            }

            return QtoXDataHelper.GetXDataValue(entity, QtoXDataHelper.KeyQtoSyncId) ?? string.Empty;
        }

        public static bool IsQtoEntity(Entity entity)
        {
            return entity != null && !string.IsNullOrWhiteSpace(QtoXDataHelper.GetXDataValue(entity, QtoXDataHelper.KeyQtoType));
        }

        public static string EnsureEntitySyncId(Entity entity, Database database, Transaction transaction)
        {
            if (entity == null)
            {
                throw new ArgumentNullException("entity");
            }

            Dictionary<string, string> data = QtoXDataHelper.GetXData(entity);
            string syncId;
            if (data.TryGetValue(QtoXDataHelper.KeyQtoSyncId, out syncId) && IsValidSyncId(syncId))
            {
                return syncId;
            }

            syncId = CreateSyncId();
            data[QtoXDataHelper.KeyQtoSyncId] = syncId;
            data[QtoXDataHelper.KeyQtoSyncStatus] = QtoSyncStatus.Active;
            data[QtoXDataHelper.KeyQtoLastModifiedAt] = DateTime.UtcNow.ToString("o");
            QtoXDataHelper.SetXData(entity, database, transaction, data);
            return syncId;
        }

        public static QtoDuplicateSyncIdScanResult ScanDuplicateSyncIds(Database database, Transaction transaction)
        {
            QtoDuplicateSyncIdScanResult result = new QtoDuplicateSyncIdScanResult();

            if (database == null || transaction == null)
            {
                return result;
            }

            Dictionary<string, List<ObjectId>> bySyncId = new Dictionary<string, List<ObjectId>>(StringComparer.OrdinalIgnoreCase);

            BlockTable blockTable = (BlockTable)transaction.GetObject(database.BlockTableId, OpenMode.ForRead);
            BlockTableRecord modelSpace = (BlockTableRecord)transaction.GetObject(blockTable[BlockTableRecord.ModelSpace], OpenMode.ForRead);

            foreach (ObjectId objectId in modelSpace)
            {
                Entity entity = transaction.GetObject(objectId, OpenMode.ForRead, false) as Entity;
                if (!IsQtoEntity(entity))
                {
                    continue;
                }

                result.QtoEntityCount++;
                string syncId = GetSyncId(entity);
                if (!IsValidSyncId(syncId))
                {
                    result.MissingSyncIdCount++;
                    result.MissingSyncIdObjectIds.Add(objectId);
                    continue;
                }

                List<ObjectId> ids;
                if (!bySyncId.TryGetValue(syncId, out ids))
                {
                    ids = new List<ObjectId>();
                    bySyncId[syncId] = ids;
                }

                ids.Add(objectId);
            }

            foreach (KeyValuePair<string, List<ObjectId>> item in bySyncId)
            {
                if (item.Value.Count <= 1)
                {
                    continue;
                }

                QtoDuplicateSyncIdIssue issue = new QtoDuplicateSyncIdIssue();
                issue.SyncId = item.Key;
                issue.ObjectIds.AddRange(item.Value);
                result.DuplicateIssues.Add(issue);
            }

            return result;
        }

        public static QtoSyncIdRepairResult RepairMissingAndDuplicateSyncIds(Database database, Transaction transaction)
        {
            QtoSyncIdRepairResult repairResult = new QtoSyncIdRepairResult();
            QtoDuplicateSyncIdScanResult scanResult = ScanDuplicateSyncIds(database, transaction);

            foreach (ObjectId objectId in scanResult.MissingSyncIdObjectIds)
            {
                Entity entity = transaction.GetObject(objectId, OpenMode.ForWrite, false) as Entity;
                if (entity == null)
                {
                    continue;
                }

                EnsureEntitySyncId(entity, database, transaction);
                repairResult.MissingCreatedCount++;
            }

            foreach (QtoDuplicateSyncIdIssue issue in scanResult.DuplicateIssues)
            {
                for (int i = 1; i < issue.ObjectIds.Count; i++)
                {
                    Entity entity = transaction.GetObject(issue.ObjectIds[i], OpenMode.ForWrite, false) as Entity;
                    if (entity == null)
                    {
                        continue;
                    }

                    Dictionary<string, string> data = QtoXDataHelper.GetXData(entity);
                    QtoCopyArrayRuleService.MarkCopiedObjectForReview(
                        data,
                        issue.SyncId,
                        CreateSyncId(),
                        QtoCopyArrayRuleService.ReasonCopiedSyncIdRebuilt);
                    data[QtoXDataHelper.KeyQtoLastModifiedAt] = DateTime.UtcNow.ToString("o");
                    QtoXDataHelper.SetXData(entity, database, transaction, data);
                    repairResult.DuplicateRepairedCount++;
                }
            }

            if (repairResult.MissingCreatedCount > 0)
            {
                repairResult.Messages.Add(QtoUserMessageService.SyncIdCreated(repairResult.MissingCreatedCount));
            }

            if (repairResult.DuplicateRepairedCount > 0)
            {
                repairResult.Messages.Add(QtoUserMessageService.DuplicateSyncIdRepaired(repairResult.DuplicateRepairedCount));
            }

            if (repairResult.Messages.Count == 0)
            {
                repairResult.Messages.Add(QtoUserMessageService.NoSyncIdIssueFound());
            }

            return repairResult;
        }
    }
}
