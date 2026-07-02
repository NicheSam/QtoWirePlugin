using System;
using System.Collections.Generic;

namespace QtoWirePlugin
{
    public class QtoSyncRow
    {
        public string SyncId { get; set; }
        public string SourceDwg { get; set; }
        public string ObjectHandle { get; set; }
        public string CadMeasureType { get; set; }
        public string QtoNumber { get; set; }
        public string SystemCode { get; set; }
        public string EquipmentTypeCode { get; set; }
        public string EquipmentTypeName { get; set; }
        public string BlockName { get; set; }
        public string Layer { get; set; }
        public string Floor { get; set; }
        public string Area { get; set; }
        public string Space { get; set; }
        public string CableType { get; set; }
        public string ConduitType { get; set; }
        public string ConduitSize { get; set; }
        public string QuantityBasis { get; set; }
        public double Quantity { get; set; }
        public string Unit { get; set; }
        public double LengthM { get; set; }
        public string SyncStatus { get; set; }
        public string ReviewReason { get; set; }
        public string LastSyncAt { get; set; }
        public string Remark { get; set; }
        public string RawXData { get; set; }
        public string CatalogId { get; set; }
        public string Fingerprint { get; set; }
        public string LastCadModifiedAt { get; set; }
        public string LastExcelModifiedAt { get; set; }
    }

    public class QtoReviewItem
    {
        public string ReviewId { get; set; }
        public string Status { get; set; }
        public string Severity { get; set; }
        public string IssueType { get; set; }
        public string Category { get; set; }
        public string UserMessage { get; set; }
        public string TechnicalDetail { get; set; }
        public string SyncId { get; set; }
        public string ObjectId { get; set; }
        public string ObjectHandle { get; set; }
        public string BlockName { get; set; }
        public string SuggestedAction { get; set; }
        public bool CanAutoRepair { get; set; }
        public string SystemCode { get; set; }
        public string EquipmentTypeCode { get; set; }
        public string EquipmentTypeName { get; set; }
        public string Floor { get; set; }
        public string Area { get; set; }

        public string SeverityDisplay
        {
            get { return QtoReviewService.ResolveSeverityDisplay(Severity); }
        }

        public string IssueTypeDisplay
        {
            get { return QtoReviewService.ResolveIssueTypeDisplay(IssueType); }
        }

        public string CategoryDisplay
        {
            get
            {
                string source = string.IsNullOrWhiteSpace(Category) ? IssueType : Category;
                return QtoReviewService.ResolveCategoryDisplay(source);
            }
        }

        public string UserMessageDisplay
        {
            get { return QtoReviewService.ResolveUserMessage(IssueType, UserMessage); }
        }

        public string SuggestedActionDisplay
        {
            get { return QtoReviewService.ResolveSuggestedAction(IssueType, SuggestedAction); }
        }
    }

    public class QtoSyncLogRow
    {
        public string Time { get; set; }
        public string Source { get; set; }
        public string EventType { get; set; }
        public string UserMessage { get; set; }
        public string TechnicalDetail { get; set; }
        public string SyncId { get; set; }
        public string ObjectHandle { get; set; }
        public string BlockName { get; set; }
        public string FieldName { get; set; }
        public string OldValue { get; set; }
        public string NewValue { get; set; }
        public string Result { get; set; }
    }

    public class QtoExcelResult
    {
        public bool Success { get; set; }
        public string WorkbookPath { get; set; }
        public string UserMessage { get; set; }
        public string TechnicalDetail { get; set; }
        public int PreservedManualEditCount { get; set; }
        public int BudgetDraftRowCount { get; set; }
        public int BudgetDraftReviewCount { get; set; }
        public List<QtoSyncLogRow> Logs { get; private set; }

        public QtoExcelResult()
        {
            Logs = new List<QtoSyncLogRow>();
        }

        public static QtoExcelResult Ok(string workbookPath, string userMessage)
        {
            return new QtoExcelResult
            {
                Success = true,
                WorkbookPath = workbookPath,
                UserMessage = userMessage
            };
        }

        public static QtoExcelResult Fail(string workbookPath, string userMessage, Exception ex)
        {
            return new QtoExcelResult
            {
                Success = false,
                WorkbookPath = workbookPath,
                UserMessage = userMessage,
                TechnicalDetail = ex == null ? string.Empty : ex.ToString()
            };
        }
    }

    internal class QtoSummaryRow
    {
        public string SystemCode { get; set; }
        public string EquipmentType { get; set; }
        public string Floor { get; set; }
        public string Area { get; set; }
        public double Quantity { get; set; }
        public string Unit { get; set; }
        public string Remark { get; set; }
        public int ReviewCount { get; set; }
        public string LastUpdatedAt { get; set; }
    }
}
