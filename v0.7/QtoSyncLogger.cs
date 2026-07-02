using System;
using System.Collections.Generic;

namespace QtoWirePlugin
{
    public static class QtoSyncLogger
    {
        public static QtoSyncLogRow CreateInfo(string eventType, string userMessage, string technicalDetail)
        {
            return Create("QtoWirePlugin", eventType, userMessage, technicalDetail, null, null, null, null, null, null, "OK");
        }

        public static QtoSyncLogRow CreateError(string eventType, string userMessage, Exception ex)
        {
            return Create("QtoWirePlugin", eventType, userMessage, ex == null ? string.Empty : ex.ToString(), null, null, null, null, null, null, "ERROR");
        }

        public static QtoSyncLogRow Create(
            string source,
            string eventType,
            string userMessage,
            string technicalDetail,
            string syncId,
            string objectHandle,
            string blockName,
            string fieldName,
            string oldValue,
            string newValue,
            string result)
        {
            return new QtoSyncLogRow
            {
                Time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                Source = source ?? "QtoWirePlugin",
                EventType = eventType ?? string.Empty,
                UserMessage = userMessage ?? string.Empty,
                TechnicalDetail = technicalDetail ?? string.Empty,
                SyncId = syncId ?? string.Empty,
                ObjectHandle = objectHandle ?? string.Empty,
                BlockName = blockName ?? string.Empty,
                FieldName = fieldName ?? string.Empty,
                OldValue = oldValue ?? string.Empty,
                NewValue = newValue ?? string.Empty,
                Result = result ?? string.Empty
            };
        }

        public static List<QtoSyncLogRow> SafeList(IEnumerable<QtoSyncLogRow> rows)
        {
            return rows == null ? new List<QtoSyncLogRow>() : new List<QtoSyncLogRow>(rows);
        }
    }
}
