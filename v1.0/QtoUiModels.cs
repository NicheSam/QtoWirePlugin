using System;
using System.Collections.Generic;

namespace QtoWirePlugin
{
    public sealed class QtoSyncOverview
    {
        public string CurrentStatus { get; set; }
        public string ExcelPath { get; set; }
        public string ExcelStatus { get; set; }
        public string CatalogStatus { get; set; }
        public string LastSyncText { get; set; }
        public int ReviewCount { get; set; }
        public int ErrorCount { get; set; }
        public int UnconfiguredBlockCount { get; set; }

        public QtoSyncOverview()
        {
            CurrentStatus = "未同步";
            ExcelPath = "尚未選擇";
            ExcelStatus = "未連接";
            CatalogStatus = "未載入";
            LastSyncText = "尚無紀錄";
        }
    }

    public sealed class QtoRepairPlanItem
    {
        public string IssueType { get; set; }
        public int Count { get; set; }
        public string RepairAction { get; set; }
        public bool CanAutoRepair { get; set; }

        public QtoRepairPlanItem()
        {
            IssueType = string.Empty;
            RepairAction = string.Empty;
        }

        public string CountText
        {
            get { return Count.ToString("0"); }
        }
    }

    internal static class QtoUiSampleData
    {
        public static QtoSyncOverview CreateDefaultOverview()
        {
            return new QtoSyncOverview
            {
                CurrentStatus = "未同步",
                ExcelStatus = "未連接",
                CatalogStatus = "尚未載入",
                LastSyncText = "尚無紀錄",
                ReviewCount = 0,
                ErrorCount = 0,
                UnconfiguredBlockCount = 0
            };
        }

        public static IList<QtoReviewItem> CreateReviewItems()
        {
            return new List<QtoReviewItem>
            {
                new QtoReviewItem
                {
                    ReviewId = "R-001",
                    Severity = "錯誤",
                    Category = "ID 問題",
                    Status = "未處理",
                    UserMessage = "這個物件缺少 QTO_SYNC_ID，暫時無法穩定同步到 Excel。",
                    SuggestedAction = "可由系統補上同步 ID，修復後再重新檢查。",
                    BlockName = "CCTV_CAM_DOME",
                    Floor = "3F",
                    Area = "辦公區",
                    ObjectHandle = "3A1F",
                    CanAutoRepair = true,
                    TechnicalDetail = "ReviewCode=MissingSyncId\r\nField=QTO_SYNC_ID\r\nSource=QtoValidateService"
                },
                new QtoReviewItem
                {
                    ReviewId = "R-002",
                    Severity = "警告",
                    Category = "缺資料",
                    Status = "未處理",
                    UserMessage = "這個圖塊尚未填系統代碼，數量可以列出，但分類會不完整。",
                    SuggestedAction = "請開啟屬性或圖塊資料庫，補上系統代碼。",
                    BlockName = "DATA_OUTLET_2PORT",
                    Floor = "2F",
                    Area = "會議室",
                    ObjectHandle = "42B0",
                    CanAutoRepair = false,
                    TechnicalDetail = "ReviewCode=MissingSystemCode\r\nMissingField=SYSTEM_CODE\r\nSource=QtoValidateService"
                },
                new QtoReviewItem
                {
                    ReviewId = "R-003",
                    Severity = "提醒",
                    Category = "圖塊問題",
                    Status = "未處理",
                    UserMessage = "目前圖塊不在圖塊資料庫內，請確認是否為新設備或臨時圖塊。",
                    SuggestedAction = "若是正式設備，請先加入圖塊資料庫再同步。",
                    BlockName = "NEW_SENSOR_TEMP",
                    Floor = "1F",
                    Area = "機房",
                    ObjectHandle = "51C2",
                    CanAutoRepair = false,
                    TechnicalDetail = "ReviewCode=UnknownBlock\r\nCatalogStatus=unconfigured\r\nSource=QtoReviewService"
                }
            };
        }

        public static IList<QtoRepairPlanItem> CreateRepairItems()
        {
            return new List<QtoRepairPlanItem>
            {
                new QtoRepairPlanItem
                {
                    IssueType = "缺少 QTO_SYNC_ID",
                    Count = 15,
                    RepairAction = "為物件補上新的同步 ID",
                    CanAutoRepair = true
                },
                new QtoRepairPlanItem
                {
                    IssueType = "重複 QTO_SYNC_ID",
                    Count = 3,
                    RepairAction = "保留一筆，其餘物件重新配發同步 ID",
                    CanAutoRepair = true
                },
                new QtoRepairPlanItem
                {
                    IssueType = "缺系統代碼",
                    Count = 8,
                    RepairAction = "需要工程人員判斷系統分類",
                    CanAutoRepair = false
                },
                new QtoRepairPlanItem
                {
                    IssueType = "圖塊不在資料庫",
                    Count = 5,
                    RepairAction = "需要先確認是否加入圖塊資料庫",
                    CanAutoRepair = false
                }
            };
        }
    }
}
