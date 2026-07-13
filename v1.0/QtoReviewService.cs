using System;

namespace QtoWirePlugin
{
    public static class QtoReviewService
    {
        public static QtoReviewItem CreateReviewItem(
            string issueType,
            string severity,
            string userMessage,
            string technicalDetail,
            QtoValidationTarget target,
            bool canAutoRepair,
            string suggestedAction)
        {
            QtoReviewItem item = new QtoReviewItem();
            item.ReviewId = Guid.NewGuid().ToString("N");
            item.IssueType = issueType ?? string.Empty;
            item.Category = ResolveCategory(issueType);
            item.Severity = string.IsNullOrWhiteSpace(severity) ? QtoReviewSeverity.Warning : severity;
            item.UserMessage = ResolveUserMessage(issueType, userMessage);
            item.TechnicalDetail = technicalDetail ?? string.Empty;
            item.CanAutoRepair = canAutoRepair && CanAutoRepair(issueType);
            item.SuggestedAction = ResolveSuggestedAction(issueType, suggestedAction);

            if (target != null)
            {
                item.SyncId = target.SyncId ?? string.Empty;
                item.ObjectId = target.ObjectId ?? string.Empty;
                item.ObjectHandle = target.ObjectHandle ?? string.Empty;
                item.BlockName = target.BlockName ?? string.Empty;
                item.SystemCode = target.SystemCode ?? string.Empty;
                item.EquipmentTypeCode = target.EquipmentTypeCode ?? string.Empty;
                item.Floor = target.Floor ?? string.Empty;
                item.Area = target.Area ?? string.Empty;
            }

            return item;
        }

        public static bool CanAutoRepair(string issueType)
        {
            return string.Equals(issueType, QtoReviewIssueType.MissingSyncId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(issueType, QtoReviewIssueType.DuplicateSyncId, StringComparison.OrdinalIgnoreCase);
        }

        public static string ResolveSeverityDisplay(string severity)
        {
            if (string.Equals(severity, QtoReviewSeverity.Error, StringComparison.OrdinalIgnoreCase)
                || string.Equals(severity, "錯誤", StringComparison.OrdinalIgnoreCase))
            {
                return "錯誤";
            }

            if (string.Equals(severity, QtoReviewSeverity.Warning, StringComparison.OrdinalIgnoreCase)
                || string.Equals(severity, "警告", StringComparison.OrdinalIgnoreCase))
            {
                return "警告";
            }

            return "提醒";
        }

        public static string ResolveIssueTypeDisplay(string issueType)
        {
            if (string.Equals(issueType, QtoReviewIssueType.MissingSyncId, StringComparison.OrdinalIgnoreCase))
            {
                return "缺少 QTO_SYNC_ID";
            }

            if (string.Equals(issueType, QtoReviewIssueType.DuplicateSyncId, StringComparison.OrdinalIgnoreCase))
            {
                return "重複 QTO_SYNC_ID";
            }

            if (string.Equals(issueType, QtoReviewIssueType.MissingSystemCode, StringComparison.OrdinalIgnoreCase))
            {
                return "缺少系統代碼";
            }

            if (string.Equals(issueType, QtoReviewIssueType.MissingEquipmentTypeCode, StringComparison.OrdinalIgnoreCase))
            {
                return "缺少設備類型代碼";
            }

            if (string.Equals(issueType, QtoReviewIssueType.MissingQuantityBasis, StringComparison.OrdinalIgnoreCase))
            {
                return "缺少數量依據";
            }

            if (string.Equals(issueType, QtoReviewIssueType.ZeroLength, StringComparison.OrdinalIgnoreCase))
            {
                return "長度數量為 0";
            }

            if (string.Equals(issueType, QtoReviewIssueType.CatalogNotLoaded, StringComparison.OrdinalIgnoreCase))
            {
                return "圖塊資料庫未載入";
            }

            if (string.Equals(issueType, QtoReviewIssueType.CatalogMissing, StringComparison.OrdinalIgnoreCase))
            {
                return "圖塊不在資料庫";
            }

            if (string.Equals(issueType, QtoReviewIssueType.CatalogDeprecated, StringComparison.OrdinalIgnoreCase))
            {
                return "圖塊資料已停用";
            }

            if (string.Equals(issueType, QtoReviewIssueType.CatalogUpdated, StringComparison.OrdinalIgnoreCase))
            {
                return "圖塊資料有更新";
            }

            if (string.Equals(issueType, QtoReviewIssueType.CatalogConflict, StringComparison.OrdinalIgnoreCase))
            {
                return "圖塊資料有衝突";
            }

            if (string.Equals(issueType, QtoReviewIssueType.CadObjectMissingInExcel, StringComparison.OrdinalIgnoreCase))
            {
                return "CAD 物件未同步到 Excel";
            }

            if (string.Equals(issueType, QtoReviewIssueType.ExcelRowMissingCadObject, StringComparison.OrdinalIgnoreCase))
            {
                return "Excel 列找不到 CAD 物件";
            }

            if (string.Equals(issueType, QtoReviewIssueType.BlockUpdateNeedsReview, StringComparison.OrdinalIgnoreCase))
            {
                return "圖塊更新後需確認";
            }

            return string.IsNullOrWhiteSpace(issueType) ? "未分類問題" : issueType;
        }

        public static string ResolveCategoryDisplay(string categoryOrIssueType)
        {
            if (string.Equals(categoryOrIssueType, "sync", StringComparison.OrdinalIgnoreCase)
                || string.Equals(categoryOrIssueType, "ID 問題", StringComparison.OrdinalIgnoreCase)
                || string.Equals(categoryOrIssueType, "同步 ID", StringComparison.OrdinalIgnoreCase)
                || string.Equals(categoryOrIssueType, QtoReviewIssueType.MissingSyncId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(categoryOrIssueType, QtoReviewIssueType.DuplicateSyncId, StringComparison.OrdinalIgnoreCase))
            {
                return "同步 ID";
            }

            if (string.Equals(categoryOrIssueType, "catalog", StringComparison.OrdinalIgnoreCase)
                || string.Equals(categoryOrIssueType, "圖塊問題", StringComparison.OrdinalIgnoreCase)
                || string.Equals(categoryOrIssueType, "圖塊資料庫", StringComparison.OrdinalIgnoreCase)
                || string.Equals(categoryOrIssueType, QtoReviewIssueType.CatalogNotLoaded, StringComparison.OrdinalIgnoreCase)
                || string.Equals(categoryOrIssueType, QtoReviewIssueType.CatalogMissing, StringComparison.OrdinalIgnoreCase)
                || string.Equals(categoryOrIssueType, QtoReviewIssueType.CatalogDeprecated, StringComparison.OrdinalIgnoreCase)
                || string.Equals(categoryOrIssueType, QtoReviewIssueType.CatalogUpdated, StringComparison.OrdinalIgnoreCase)
                || string.Equals(categoryOrIssueType, QtoReviewIssueType.CatalogConflict, StringComparison.OrdinalIgnoreCase)
                || string.Equals(categoryOrIssueType, QtoReviewIssueType.BlockUpdateNeedsReview, StringComparison.OrdinalIgnoreCase))
            {
                return "圖塊資料庫";
            }

            if (string.Equals(categoryOrIssueType, "quantity", StringComparison.OrdinalIgnoreCase)
                || string.Equals(categoryOrIssueType, "數量異常", StringComparison.OrdinalIgnoreCase)
                || string.Equals(categoryOrIssueType, QtoReviewIssueType.ZeroLength, StringComparison.OrdinalIgnoreCase))
            {
                return "數量異常";
            }

            if (string.Equals(categoryOrIssueType, "mapping", StringComparison.OrdinalIgnoreCase)
                || string.Equals(categoryOrIssueType, "缺資料", StringComparison.OrdinalIgnoreCase)
                || string.Equals(categoryOrIssueType, "欄位缺漏", StringComparison.OrdinalIgnoreCase)
                || string.Equals(categoryOrIssueType, QtoReviewIssueType.MissingSystemCode, StringComparison.OrdinalIgnoreCase)
                || string.Equals(categoryOrIssueType, QtoReviewIssueType.MissingEquipmentTypeCode, StringComparison.OrdinalIgnoreCase)
                || string.Equals(categoryOrIssueType, QtoReviewIssueType.MissingQuantityBasis, StringComparison.OrdinalIgnoreCase))
            {
                return "欄位缺漏";
            }

            if (string.Equals(categoryOrIssueType, QtoReviewIssueType.CadObjectMissingInExcel, StringComparison.OrdinalIgnoreCase)
                || string.Equals(categoryOrIssueType, QtoReviewIssueType.ExcelRowMissingCadObject, StringComparison.OrdinalIgnoreCase))
            {
                return "同步差異";
            }

            return string.IsNullOrWhiteSpace(categoryOrIssueType) ? "未分類" : categoryOrIssueType;
        }

        public static string ResolveUserMessage(string issueType, string fallback)
        {
            if (string.Equals(issueType, QtoReviewIssueType.MissingSyncId, StringComparison.OrdinalIgnoreCase))
            {
                return "這個物件缺少 QTO_SYNC_ID，暫時無法穩定同步到 Excel。";
            }

            if (string.Equals(issueType, QtoReviewIssueType.DuplicateSyncId, StringComparison.OrdinalIgnoreCase))
            {
                return "多個物件使用同一個 QTO_SYNC_ID，可能造成 Excel 對應錯亂。";
            }

            if (string.Equals(issueType, QtoReviewIssueType.MissingSystemCode, StringComparison.OrdinalIgnoreCase))
            {
                return "這個物件尚未填系統代碼，分類與彙總會不完整。";
            }

            if (string.Equals(issueType, QtoReviewIssueType.MissingEquipmentTypeCode, StringComparison.OrdinalIgnoreCase))
            {
                return "這個物件尚未填設備類型代碼，設備分類會不完整。";
            }

            if (string.Equals(issueType, QtoReviewIssueType.MissingQuantityBasis, StringComparison.OrdinalIgnoreCase))
            {
                return "這個物件尚未填數量依據，後續計量可能無法判斷單位或算法。";
            }

            if (string.Equals(issueType, QtoReviewIssueType.ZeroLength, StringComparison.OrdinalIgnoreCase))
            {
                return "這個長度型物件的數量為 0，請確認幾何或長度來源。";
            }

            if (string.Equals(issueType, QtoReviewIssueType.CatalogNotLoaded, StringComparison.OrdinalIgnoreCase))
            {
                return "尚未載入圖塊資料庫，因此無法檢查圖塊是否為正式設備。";
            }

            if (string.Equals(issueType, QtoReviewIssueType.CatalogMissing, StringComparison.OrdinalIgnoreCase))
            {
                return "這個圖塊不在圖塊資料庫內，請確認是否為新設備或臨時圖塊。";
            }

            if (string.Equals(issueType, QtoReviewIssueType.CatalogDeprecated, StringComparison.OrdinalIgnoreCase))
            {
                return "這個圖塊在資料庫中已標示停用，請確認是否需要替換。";
            }

            if (string.Equals(issueType, QtoReviewIssueType.CatalogUpdated, StringComparison.OrdinalIgnoreCase))
            {
                return "這個圖塊資料有新版，請確認是否套用更新。";
            }

            if (string.Equals(issueType, QtoReviewIssueType.CatalogConflict, StringComparison.OrdinalIgnoreCase))
            {
                return "這個圖塊資料存在衝突，暫時不應作為標準圖塊使用。";
            }

            if (string.Equals(issueType, QtoReviewIssueType.CadObjectMissingInExcel, StringComparison.OrdinalIgnoreCase))
            {
                return "CAD 物件尚未同步到 Excel，兩邊資料目前不一致。";
            }

            if (string.Equals(issueType, QtoReviewIssueType.ExcelRowMissingCadObject, StringComparison.OrdinalIgnoreCase))
            {
                return "Excel 列找不到對應的 CAD 物件，請確認是否已刪除或搬移。";
            }

            return fallback ?? string.Empty;
        }

        public static string ResolveSuggestedAction(string issueType, string fallback)
        {
            if (string.Equals(issueType, QtoReviewIssueType.MissingSyncId, StringComparison.OrdinalIgnoreCase))
            {
                return "可由系統補上新的 QTO_SYNC_ID，修復後請重新檢查。";
            }

            if (string.Equals(issueType, QtoReviewIssueType.DuplicateSyncId, StringComparison.OrdinalIgnoreCase))
            {
                return "可由系統保留一筆，其餘物件重新配發 QTO_SYNC_ID。";
            }

            if (string.Equals(issueType, QtoReviewIssueType.MissingSystemCode, StringComparison.OrdinalIgnoreCase))
            {
                return "請開啟屬性或圖塊資料庫，補上正確系統代碼。";
            }

            if (string.Equals(issueType, QtoReviewIssueType.MissingEquipmentTypeCode, StringComparison.OrdinalIgnoreCase))
            {
                return "請補上正確設備類型代碼，再重新同步。";
            }

            if (string.Equals(issueType, QtoReviewIssueType.MissingQuantityBasis, StringComparison.OrdinalIgnoreCase))
            {
                return "請選擇正確數量依據，例如點數、長度或面積。";
            }

            if (string.Equals(issueType, QtoReviewIssueType.MissingCableType, StringComparison.OrdinalIgnoreCase))
            {
                return "請開啟屬性，選擇正確的線材類型。";
            }

            if (string.Equals(issueType, QtoReviewIssueType.MissingConduitType, StringComparison.OrdinalIgnoreCase))
            {
                return "請開啟屬性，選擇正確的管材類型。";
            }

            if (string.Equals(issueType, QtoReviewIssueType.ZeroLength, StringComparison.OrdinalIgnoreCase))
            {
                return "請檢查 CAD 幾何、比例或長度欄位來源。";
            }

            if (string.Equals(issueType, QtoReviewIssueType.CatalogNotLoaded, StringComparison.OrdinalIgnoreCase))
            {
                return "請先載入圖塊資料庫，再重新執行檢查。";
            }

            if (string.Equals(issueType, QtoReviewIssueType.CatalogMissing, StringComparison.OrdinalIgnoreCase))
            {
                return "若為正式設備，請加入圖塊資料庫；若為臨時圖塊，請人工確認。";
            }

            if (string.Equals(issueType, QtoReviewIssueType.CatalogDeprecated, StringComparison.OrdinalIgnoreCase))
            {
                return "請替換為現行圖塊，或確認本案仍允許沿用。";
            }

            if (string.Equals(issueType, QtoReviewIssueType.CatalogUpdated, StringComparison.OrdinalIgnoreCase))
            {
                return "請檢查新版圖塊資料，確認後再套用更新。";
            }

            if (string.Equals(issueType, QtoReviewIssueType.CatalogConflict, StringComparison.OrdinalIgnoreCase))
            {
                return "請開啟圖塊庫管理，確認重複資料或來源變更。";
            }

            if (string.Equals(issueType, QtoReviewIssueType.CadObjectMissingInExcel, StringComparison.OrdinalIgnoreCase))
            {
                return "請執行同步或全圖重建，讓 Excel 補上這個 CAD 物件。";
            }

            if (string.Equals(issueType, QtoReviewIssueType.ExcelRowMissingCadObject, StringComparison.OrdinalIgnoreCase))
            {
                return "請確認 CAD 物件是否已刪除；必要時移除或重建 Excel 對應列。";
            }

            return fallback ?? string.Empty;
        }

        public static string ResolveCategory(string issueType)
        {
            if (string.Equals(issueType, QtoReviewIssueType.MissingSyncId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(issueType, QtoReviewIssueType.DuplicateSyncId, StringComparison.OrdinalIgnoreCase))
            {
                return "同步 ID";
            }

            if (string.Equals(issueType, QtoReviewIssueType.CatalogNotLoaded, StringComparison.OrdinalIgnoreCase)
                || string.Equals(issueType, QtoReviewIssueType.CatalogMissing, StringComparison.OrdinalIgnoreCase)
                || string.Equals(issueType, QtoReviewIssueType.CatalogDeprecated, StringComparison.OrdinalIgnoreCase)
                || string.Equals(issueType, QtoReviewIssueType.CatalogUpdated, StringComparison.OrdinalIgnoreCase)
                || string.Equals(issueType, QtoReviewIssueType.CatalogConflict, StringComparison.OrdinalIgnoreCase))
            {
                return "圖塊資料庫";
            }

            if (string.Equals(issueType, QtoReviewIssueType.ZeroLength, StringComparison.OrdinalIgnoreCase))
            {
                return "數量異常";
            }

            if (string.Equals(issueType, QtoReviewIssueType.MissingCableType, StringComparison.OrdinalIgnoreCase)
                || string.Equals(issueType, QtoReviewIssueType.MissingConduitType, StringComparison.OrdinalIgnoreCase))
            {
                return "材料缺漏";
            }

            if (string.Equals(issueType, QtoReviewIssueType.MissingOutletId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(issueType, QtoReviewIssueType.MissingJunctionBoxId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(issueType, QtoReviewIssueType.MissingOutletJunctionBox, StringComparison.OrdinalIgnoreCase)
                || string.Equals(issueType, QtoReviewIssueType.DuplicateOutletId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(issueType, QtoReviewIssueType.DuplicateJunctionBoxId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(issueType, QtoReviewIssueType.MissingWire, StringComparison.OrdinalIgnoreCase)
                || string.Equals(issueType, QtoReviewIssueType.MultipleWires, StringComparison.OrdinalIgnoreCase)
                || string.Equals(issueType, QtoReviewIssueType.OutletReferenceMissing, StringComparison.OrdinalIgnoreCase)
                || string.Equals(issueType, QtoReviewIssueType.JunctionBoxReferenceMissing, StringComparison.OrdinalIgnoreCase))
            {
                return "配線關聯";
            }

            if (string.Equals(issueType, QtoReviewIssueType.CadObjectMissingInExcel, StringComparison.OrdinalIgnoreCase)
                || string.Equals(issueType, QtoReviewIssueType.ExcelRowMissingCadObject, StringComparison.OrdinalIgnoreCase))
            {
                return "同步差異";
            }

            return "欄位缺漏";
        }
    }
}
