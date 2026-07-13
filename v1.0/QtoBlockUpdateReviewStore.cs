using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;

namespace QtoWirePlugin
{
    public static class QtoBlockUpdateReviewStore
    {
        private const string RecordKey = "QTO_BLOCK_UPDATE_REVIEW";

        public static void Save(Database database, IEnumerable<string> messages)
        {
            ResultBuffer buffer = new ResultBuffer();
            foreach (string message in messages ?? new string[0])
            {
                string text = message ?? string.Empty;
                for (int offset = 0; offset < text.Length; offset += 240)
                    buffer.Add(new TypedValue((int)DxfCode.Text, text.Substring(offset, Math.Min(240, text.Length - offset))));
            }
            using (Transaction tr = database.TransactionManager.StartTransaction())
            {
                DBDictionary nod = tr.GetObject(database.NamedObjectsDictionaryId, OpenMode.ForRead, false) as DBDictionary;
                Xrecord record;
                if (nod.Contains(RecordKey)) record = tr.GetObject(nod.GetAt(RecordKey), OpenMode.ForWrite, false) as Xrecord;
                else { nod.UpgradeOpen(); record = new Xrecord(); nod.SetAt(RecordKey, record); tr.AddNewlyCreatedDBObject(record, true); }
                record.Data = buffer;
                tr.Commit();
            }
        }

        public static IList<QtoReviewItem> Load(Database database)
        {
            List<QtoReviewItem> result = new List<QtoReviewItem>();
            using (Transaction tr = database.TransactionManager.StartOpenCloseTransaction())
            {
                DBDictionary nod = tr.GetObject(database.NamedObjectsDictionaryId, OpenMode.ForRead, false) as DBDictionary;
                if (nod == null || !nod.Contains(RecordKey)) return result;
                Xrecord record = tr.GetObject(nod.GetAt(RecordKey), OpenMode.ForRead, false) as Xrecord;
                int index = 0;
                if (record != null && record.Data != null)
                foreach (TypedValue value in record.Data)
                {
                    string message = Convert.ToString(value.Value) ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(message)) continue;
                    index++;
                    result.Add(new QtoReviewItem
                    {
                        ReviewId = "BLOCK-UPDATE-" + index.ToString("000"), Status = "待處理", Severity = QtoReviewSeverity.Warning,
                        IssueType = QtoReviewIssueType.BlockUpdateNeedsReview, Category = "catalog",
                        UserMessage = "圖塊更新後有屬性或動態參數需要確認。", TechnicalDetail = message,
                        SuggestedAction = "請定位更新後圖塊，確認屬性值與動態參數。"
                    });
                }
            }
            return result;
        }
    }
}
