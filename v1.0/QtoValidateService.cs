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
            List<QtoValidationTarget> validTargets = new List<QtoValidationTarget>();

            if (targets == null)
            {
                return result;
            }

            bool catalogLoaded = catalog != null && catalog.IsLoaded;
            if (!catalogLoaded)
            {
                AddReview(
                    result,
                    QtoReviewIssueType.CatalogNotLoaded,
                    QtoReviewSeverity.Info,
                    "本圖面尚未設定或無法讀取圖塊資料庫。",
                    string.IsNullOrWhiteSpace(catalog == null ? string.Empty : catalog.LoadError) ? "目前 DWG 沒有可讀取的 catalog 路徑。" : catalog.LoadError,
                    new QtoValidationTarget(),
                    false,
                    "請開啟圖塊庫管理，選擇或建立本案使用的圖塊資料庫。");
            }

            foreach (QtoValidationTarget target in targets)
            {
                if (target == null)
                {
                    continue;
                }

                result.ScannedObjectCount++;
                validTargets.Add(target);
                ValidateSingleTarget(result, target, catalogLoaded ? catalog : null);

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

            ValidateRelationships(result, validTargets);

            return result;
        }

        private static void ValidateSingleTarget(QtoValidationResult result, QtoValidationTarget target, QtoCatalogSnapshot catalog)
        {
            if (string.IsNullOrWhiteSpace(target.SyncId))
            {
                string detail = "物件缺少 QTO_SYNC_ID。";
                if (!string.IsNullOrWhiteSpace(target.LegacyQtoId))
                {
                    detail += " 已找到舊版 QTO_ID，但不是目前使用的同步 ID。";
                }

                AddReview(result, QtoReviewIssueType.MissingSyncId, QtoReviewSeverity.Error, "這個物件缺少 QTO_SYNC_ID。", detail, target, true, "可由系統補上新的 QTO_SYNC_ID。");
            }

            if (string.IsNullOrWhiteSpace(target.SystemCode))
            {
                AddReview(result, QtoReviewIssueType.MissingSystemCode, QtoReviewSeverity.Warning, "這個物件尚未填系統代碼。", "SYSTEM_CODE 未填。", target, false, "請補上正確系統代碼。");
            }

            if (RequiresEquipmentType(target.QtoType) && string.IsNullOrWhiteSpace(target.EquipmentTypeCode))
            {
                AddReview(result, QtoReviewIssueType.MissingEquipmentTypeCode, QtoReviewSeverity.Warning, "這個物件尚未填設備類型代碼。", "EQUIPMENT_TYPE_CODE 未填。", target, false, "請補上正確設備類型代碼。");
            }

            if (string.Equals(target.QtoType, QtoXDataHelper.TypeWire, StringComparison.OrdinalIgnoreCase)
                && string.IsNullOrWhiteSpace(target.CableType))
            {
                AddReview(result, QtoReviewIssueType.MissingCableType, QtoReviewSeverity.Warning, "這條配線尚未指定線材。", "CABLE_TYPE 未填，預算草稿無法判斷配線品項。", target, false, "請開啟屬性，選擇正確的線材類型。");
            }

            if (string.Equals(target.QtoType, QtoXDataHelper.TypeConduitSegment, StringComparison.OrdinalIgnoreCase)
                && string.IsNullOrWhiteSpace(target.ConduitType))
            {
                AddReview(result, QtoReviewIssueType.MissingConduitType, QtoReviewSeverity.Warning, "這段管線尚未指定管材。", "CONDUIT_TYPE 未填，預算草稿無法判斷配管品項。", target, false, "請開啟屬性，選擇正確的管材類型。");
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

        private static bool RequiresEquipmentType(string qtoType)
        {
            return string.Equals(qtoType, QtoXDataHelper.TypeOutlet, StringComparison.OrdinalIgnoreCase)
                || string.Equals(qtoType, QtoXDataHelper.TypeJunctionBox, StringComparison.OrdinalIgnoreCase)
                || string.Equals(qtoType, QtoXDataHelper.TypeDevice, StringComparison.OrdinalIgnoreCase)
                || string.Equals(qtoType, QtoXDataHelper.TypePanel, StringComparison.OrdinalIgnoreCase);
        }

        private static void ValidateRelationships(QtoValidationResult result, IList<QtoValidationTarget> targets)
        {
            Dictionary<string, List<QtoValidationTarget>> outlets = new Dictionary<string, List<QtoValidationTarget>>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, List<QtoValidationTarget>> junctionBoxes = new Dictionary<string, List<QtoValidationTarget>>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, List<QtoValidationTarget>> wiresByOutlet = new Dictionary<string, List<QtoValidationTarget>>(StringComparer.OrdinalIgnoreCase);

            foreach (QtoValidationTarget target in targets)
            {
                if (string.Equals(target.QtoType, QtoXDataHelper.TypeOutlet, StringComparison.OrdinalIgnoreCase))
                {
                    if (string.IsNullOrWhiteSpace(target.OutletId))
                    {
                        AddReview(result, QtoReviewIssueType.MissingOutletId, QtoReviewSeverity.Warning, "這個出線口尚未編號。", "OUTLET_ID 未填。", target, false, "請使用「編號標註」或開啟屬性補上出線口編號。");
                    }
                    else
                    {
                        AddToLookup(outlets, target.OutletId, target);
                    }

                    if (string.IsNullOrWhiteSpace(target.JunctionBoxId))
                    {
                        AddReview(result, QtoReviewIssueType.MissingOutletJunctionBox, QtoReviewSeverity.Warning, "這個出線口尚未指定箱體。", "JB_ID 未填。", target, false, "請使用「指向箱體」指定所屬箱體。");
                    }
                }
                else if (string.Equals(target.QtoType, QtoXDataHelper.TypeJunctionBox, StringComparison.OrdinalIgnoreCase))
                {
                    if (string.IsNullOrWhiteSpace(target.JunctionBoxId))
                    {
                        AddReview(result, QtoReviewIssueType.MissingJunctionBoxId, QtoReviewSeverity.Warning, "這個箱體尚未編號。", "JB_ID 未填。", target, false, "請使用「標記箱體」或開啟屬性補上箱體編號。");
                    }
                    else
                    {
                        AddToLookup(junctionBoxes, target.JunctionBoxId, target);
                    }
                }
                else if (string.Equals(target.QtoType, QtoXDataHelper.TypeWire, StringComparison.OrdinalIgnoreCase))
                {
                    bool hasRelationship = !string.IsNullOrWhiteSpace(target.RouteId)
                        || !string.IsNullOrWhiteSpace(target.OutletId)
                        || !string.IsNullOrWhiteSpace(target.JunctionBoxId);
                    if (hasRelationship)
                    {
                        if (string.IsNullOrWhiteSpace(target.OutletId))
                        {
                            AddReview(result, QtoReviewIssueType.MissingOutletId, QtoReviewSeverity.Warning, "這條配線尚未指定出線口。", "已建立配線關聯，但 OUTLET_ID 未填。", target, false, "請重新指定配線關聯或使用「編輯配線」。");
                        }
                        else
                        {
                            AddToLookup(wiresByOutlet, target.OutletId, target);
                        }

                        if (string.IsNullOrWhiteSpace(target.JunctionBoxId))
                        {
                            AddReview(result, QtoReviewIssueType.MissingJunctionBoxId, QtoReviewSeverity.Warning, "這條配線尚未指定箱體。", "已建立配線關聯，但 JB_ID 未填。", target, false, "請重新指定配線關聯或使用「編輯配線」。");
                        }
                    }
                }
            }

            AddDuplicateReviews(result, outlets, QtoReviewIssueType.DuplicateOutletId, "出線口編號重複", "請重新執行編號或開啟屬性修正重複的 OUTLET_ID。");
            AddDuplicateReviews(result, junctionBoxes, QtoReviewIssueType.DuplicateJunctionBoxId, "箱體編號重複", "請重新標記箱體或開啟屬性修正重複的 JB_ID。");

            foreach (KeyValuePair<string, List<QtoValidationTarget>> pair in outlets)
            {
                List<QtoValidationTarget> wires;
                if (!wiresByOutlet.TryGetValue(pair.Key, out wires))
                {
                    foreach (QtoValidationTarget outlet in pair.Value)
                    {
                        AddReview(result, QtoReviewIssueType.MissingWire, QtoReviewSeverity.Warning, "這個出線口尚未找到配線。", "OUTLET_ID '" + pair.Key + "' 沒有對應配線。", outlet, false, "若本案需要配線計量，請建立配線；不納入配線者可保留待確認。");
                    }
                }
                else if (wires.Count > 1)
                {
                    foreach (QtoValidationTarget wire in wires)
                    {
                        AddReview(result, QtoReviewIssueType.MultipleWires, QtoReviewSeverity.Warning, "同一出線口對應多條配線。", "OUTLET_ID '" + pair.Key + "' 對應 " + wires.Count.ToString(CultureInfo.InvariantCulture) + " 條配線。", wire, false, "請確認是否重複配線，或是否應拆成不同出線口編號。");
                    }
                }
            }

            foreach (KeyValuePair<string, List<QtoValidationTarget>> pair in wiresByOutlet)
            {
                if (outlets.ContainsKey(pair.Key))
                {
                    continue;
                }

                foreach (QtoValidationTarget wire in pair.Value)
                {
                    AddReview(result, QtoReviewIssueType.OutletReferenceMissing, QtoReviewSeverity.Warning, "配線指定的出線口不存在。", "找不到 OUTLET_ID '" + pair.Key + "'。", wire, false, "請重新指定配線關聯，或補回對應的出線口。");
                }
            }

            foreach (QtoValidationTarget target in targets)
            {
                if (!string.Equals(target.QtoType, QtoXDataHelper.TypeWire, StringComparison.OrdinalIgnoreCase)
                    || string.IsNullOrWhiteSpace(target.JunctionBoxId)
                    || junctionBoxes.ContainsKey(target.JunctionBoxId))
                {
                    continue;
                }

                AddReview(result, QtoReviewIssueType.JunctionBoxReferenceMissing, QtoReviewSeverity.Warning, "配線指定的箱體不存在。", "找不到 JB_ID '" + target.JunctionBoxId + "'。", target, false, "請重新指定配線關聯，或補回對應的箱體。");
            }
        }

        private static void AddToLookup(Dictionary<string, List<QtoValidationTarget>> lookup, string key, QtoValidationTarget target)
        {
            List<QtoValidationTarget> items;
            if (!lookup.TryGetValue(key, out items))
            {
                items = new List<QtoValidationTarget>();
                lookup[key] = items;
            }
            items.Add(target);
        }

        private static void AddDuplicateReviews(QtoValidationResult result, Dictionary<string, List<QtoValidationTarget>> lookup, string issueType, string title, string action)
        {
            foreach (KeyValuePair<string, List<QtoValidationTarget>> pair in lookup)
            {
                if (pair.Value.Count <= 1)
                {
                    continue;
                }

                foreach (QtoValidationTarget target in pair.Value)
                {
                    AddReview(result, issueType, QtoReviewSeverity.Warning, title + "：" + pair.Key, "同一編號出現在 " + pair.Value.Count.ToString(CultureInfo.InvariantCulture) + " 個物件。", target, false, action);
                }
            }
        }

        private static void ValidateCatalog(QtoValidationResult result, QtoValidationTarget target, QtoCatalogSnapshot catalog)
        {
            if (!target.IsBlockReference)
            {
                return;
            }

            if (catalog == null || !catalog.IsLoaded)
            {
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
            else if (string.Equals(item.Status, QtoCatalogItemStatus.Conflict, StringComparison.OrdinalIgnoreCase))
            {
                AddReview(result, QtoReviewIssueType.CatalogConflict, QtoReviewSeverity.Warning, "圖塊資料存在衝突。", "圖塊資料 '" + (item.CatalogId ?? string.Empty) + "' 需要在圖塊庫管理中處理衝突。", target, false, "請開啟圖塊庫管理，確認重複圖塊或來源變更後再使用。");
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
            if (string.IsNullOrWhiteSpace(target.SystemCode))
            {
                target.SystemCode = Get(data, QtoXDataHelper.KeySystem);
            }
            target.EquipmentTypeCode = Get(data, QtoXDataHelper.KeyEquipmentTypeCode);
            target.QuantityBasis = Get(data, QtoXDataHelper.KeyQuantityBasis);
            target.CableType = Get(data, QtoXDataHelper.KeyCableType);
            target.ConduitType = Get(data, QtoXDataHelper.KeyConduitType);
            target.OutletId = Get(data, QtoXDataHelper.KeyOutletId);
            target.JunctionBoxId = Get(data, QtoXDataHelper.KeyJbId);
            target.RouteId = Get(data, QtoXDataHelper.KeyRouteId);
            target.Floor = Get(data, QtoXDataHelper.KeyFloor);
            target.Area = Get(data, QtoXDataHelper.KeyArea);
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
