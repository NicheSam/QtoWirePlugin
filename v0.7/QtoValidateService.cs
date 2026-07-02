using System;
using System.Collections.Generic;
using System.Globalization;
using Autodesk.AutoCAD.DatabaseServices;

namespace QtoWirePlugin
{
    public static class QtoValidateService
    {
        public static QtoValidationResult ValidateDrawing(Transaction transaction, Database database)
        {
            return ValidateDrawing(transaction, database, null);
        }

        public static QtoValidationResult ValidateDrawing(Transaction transaction, Database database, QtoCatalogSnapshot catalog)
        {
            if (transaction == null)
            {
                throw new ArgumentNullException("transaction");
            }

            if (database == null)
            {
                throw new ArgumentNullException("database");
            }

            List<QtoValidationTarget> targets = CollectTargets(transaction, database);
            return ValidateTargets(targets, catalog);
        }

        public static QtoValidationResult ValidateTargets(IEnumerable<QtoValidationTarget> targets, QtoCatalogSnapshot catalog)
        {
            QtoValidationResult result = new QtoValidationResult();
            Dictionary<string, List<QtoValidationTarget>> bySyncId = new Dictionary<string, List<QtoValidationTarget>>(StringComparer.OrdinalIgnoreCase);

            if (targets == null)
            {
                return result;
            }

            foreach (QtoValidationTarget target in targets)
            {
                if (target == null)
                {
                    continue;
                }

                result.ScannedObjectCount++;
                ValidateSingleTarget(result, target, catalog);

                if (!string.IsNullOrWhiteSpace(target.SyncId))
                {
                    List<QtoValidationTarget> bucket;
                    if (!bySyncId.TryGetValue(target.SyncId, out bucket))
                    {
                        bucket = new List<QtoValidationTarget>();
                        bySyncId[target.SyncId] = bucket;
                    }

                    bucket.Add(target);
                }
            }

            foreach (KeyValuePair<string, List<QtoValidationTarget>> pair in bySyncId)
            {
                if (pair.Value.Count <= 1)
                {
                    continue;
                }

                foreach (QtoValidationTarget duplicate in pair.Value)
                {
                    AddReview(
                        result,
                        QtoReviewIssueType.DuplicateSyncId,
                        QtoReviewSeverity.Error,
                        "多個物件使用同一個 QTO_SYNC_ID。",
                        "QTO_SYNC_ID '" + pair.Key + "' 出現在 " + pair.Value.Count.ToString(CultureInfo.InvariantCulture) + " 個 QTO 物件。",
                        duplicate,
                        true,
                        "可由系統保留一筆，其餘物件重新配發 QTO_SYNC_ID。");
                }
            }

            return result;
        }

        private static void ValidateSingleTarget(QtoValidationResult result, QtoValidationTarget target, QtoCatalogSnapshot catalog)
        {
            if (string.IsNullOrWhiteSpace(target.SyncId))
            {
                string detail = "物件缺少 QTO_SYNC_ID。";
                if (!string.IsNullOrWhiteSpace(target.LegacyQtoId))
                {
                    detail += " 已找到舊版 QTO_ID，但不是 v0.7 使用的同步 ID。";
                }

                AddReview(result, QtoReviewIssueType.MissingSyncId, QtoReviewSeverity.Error, "這個物件缺少 QTO_SYNC_ID。", detail, target, true, "可由系統補上新的 QTO_SYNC_ID。");
            }

            if (string.IsNullOrWhiteSpace(target.SystemCode))
            {
                AddReview(result, QtoReviewIssueType.MissingSystemCode, QtoReviewSeverity.Warning, "這個物件尚未填系統代碼。", "SYSTEM_CODE 未填。", target, false, "請補上正確系統代碼。");
            }

            if (string.IsNullOrWhiteSpace(target.EquipmentTypeCode))
            {
                AddReview(result, QtoReviewIssueType.MissingEquipmentTypeCode, QtoReviewSeverity.Warning, "這個物件尚未填設備類型代碼。", "EQUIPMENT_TYPE_CODE 未填。", target, false, "請補上正確設備類型代碼。");
            }

            if (string.IsNullOrWhiteSpace(target.QuantityBasis))
            {
                AddReview(result, QtoReviewIssueType.MissingQuantityBasis, QtoReviewSeverity.Warning, "這個物件尚未填數量依據。", "QUANTITY_BASIS 未填。", target, false, "請選擇正確數量依據。");
            }

            if (IsLengthBased(target.QuantityBasis) && target.LengthM <= 0.0)
            {
                AddReview(result, QtoReviewIssueType.ZeroLength, QtoReviewSeverity.Warning, "長度型物件的數量為 0。", "LengthM 小於或等於 0。", target, false, "請檢查 CAD 幾何、比例或長度欄位來源。");
            }

            ValidateCatalog(result, target, catalog);
        }

        private static void ValidateCatalog(QtoValidationResult result, QtoValidationTarget target, QtoCatalogSnapshot catalog)
        {
            if (!target.IsBlockReference)
            {
                return;
            }

            if (catalog == null || !catalog.IsLoaded)
            {
                AddReview(result, QtoReviewIssueType.CatalogNotLoaded, QtoReviewSeverity.Info, "尚未載入圖塊資料庫。", "未提供圖塊資料庫快照給 QtoValidateService。", target, false, "請先載入圖塊資料庫，再重新執行檢查。");
                return;
            }

            QtoCatalogItem item;
            if (!catalog.TryFindByBlockName(target.BlockName, out item))
            {
                AddReview(result, QtoReviewIssueType.CatalogMissing, QtoReviewSeverity.Warning, "圖塊不在圖塊資料庫內。", "圖塊名稱 '" + (target.BlockName ?? string.Empty) + "' 未在圖塊資料庫找到。", target, false, "若為正式設備，請加入圖塊資料庫；若為臨時圖塊，請人工確認。");
                return;
            }

            if (string.Equals(item.Status, QtoCatalogItemStatus.Missing, StringComparison.OrdinalIgnoreCase))
            {
                AddReview(result, QtoReviewIssueType.CatalogMissing, QtoReviewSeverity.Warning, "圖塊資料來源缺失。", "圖塊資料 '" + (item.CatalogId ?? string.Empty) + "' 的狀態為 missing。", target, false, "請確認來源圖塊是否仍有效。");
            }
            else if (string.Equals(item.Status, QtoCatalogItemStatus.Deprecated, StringComparison.OrdinalIgnoreCase))
            {
                AddReview(result, QtoReviewIssueType.CatalogDeprecated, QtoReviewSeverity.Warning, "圖塊資料已標示停用。", "圖塊資料 '" + (item.CatalogId ?? string.Empty) + "' 的狀態為 deprecated。", target, false, "請替換為現行圖塊，或確認本案仍允許沿用。");
            }
            else if (string.Equals(item.Status, QtoCatalogItemStatus.Updated, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(target.CatalogVersion, item.CatalogVersion, StringComparison.OrdinalIgnoreCase))
            {
                AddReview(result, QtoReviewIssueType.CatalogUpdated, QtoReviewSeverity.Info, "圖塊資料有新版。", "本案圖塊版本 '" + (target.CatalogVersion ?? string.Empty) + "' 與資料庫版本 '" + (item.CatalogVersion ?? string.Empty) + "' 不一致。", target, false, "請檢查新版圖塊資料，確認後再套用更新。");
            }
        }

        private static List<QtoValidationTarget> CollectTargets(Transaction transaction, Database database)
        {
            List<QtoValidationTarget> targets = new List<QtoValidationTarget>();
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

                targets.Add(BuildTarget(transaction, entity, data));
            }

            return targets;
        }

        private static QtoValidationTarget BuildTarget(Transaction transaction, Entity entity, Dictionary<string, string> data)
        {
            QtoValidationTarget target = new QtoValidationTarget();
            target.ObjectId = entity.ObjectId.ToString();
            target.ObjectHandle = entity.ObjectId.Handle.ToString();
            target.QtoType = Get(data, QtoXDataHelper.KeyQtoType);
            target.SyncId = Get(data, QtoSyncXDataKeys.SyncId);
            target.LegacyQtoId = Get(data, QtoXDataHelper.KeyQtoId);
            target.SystemCode = Get(data, QtoXDataHelper.KeySystemCode);
            target.EquipmentTypeCode = Get(data, QtoXDataHelper.KeyEquipmentTypeCode);
            target.QuantityBasis = Get(data, QtoXDataHelper.KeyQuantityBasis);
            target.SourceCatalogId = Get(data, QtoSyncXDataKeys.SourceCatalogId);
            target.CatalogVersion = Get(data, QtoSyncXDataKeys.CatalogVersion);
            target.LengthM = GetLengthM(entity, data);
            target.IsBlockReference = entity is BlockReference;
            target.BlockName = GetBlockName(transaction, entity);
            return target;
        }

        private static double GetLengthM(Entity entity, Dictionary<string, string> data)
        {
            double value;
            if (double.TryParse(Get(data, QtoXDataHelper.KeyLengthM), NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            {
                return value;
            }

            Polyline polyline = entity as Polyline;
            if (polyline != null)
            {
                return QtoGeometryHelper.ConvertMmToM(QtoGeometryHelper.GetPolylineLength(polyline));
            }

            return 0.0;
        }

        private static string GetBlockName(Transaction transaction, Entity entity)
        {
            BlockReference blockReference = entity as BlockReference;
            if (blockReference == null)
            {
                return string.Empty;
            }

            ObjectId blockTableRecordId = blockReference.IsDynamicBlock ? blockReference.DynamicBlockTableRecord : blockReference.BlockTableRecord;
            BlockTableRecord blockTableRecord = transaction.GetObject(blockTableRecordId, OpenMode.ForRead, false) as BlockTableRecord;
            return blockTableRecord == null ? string.Empty : blockTableRecord.Name;
        }

        private static bool IsLengthBased(string quantityBasis)
        {
            if (string.IsNullOrWhiteSpace(quantityBasis))
            {
                return false;
            }

            return quantityBasis.IndexOf("length", StringComparison.OrdinalIgnoreCase) >= 0
                || quantityBasis.IndexOf("_m", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void AddReview(QtoValidationResult result, string issueType, string severity, string userMessage, string technicalDetail, QtoValidationTarget target, bool canAutoRepair, string suggestedAction)
        {
            result.ReviewItems.Add(QtoReviewService.CreateReviewItem(issueType, severity, userMessage, technicalDetail, target, canAutoRepair, suggestedAction));
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
