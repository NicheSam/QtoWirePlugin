namespace QtoWirePlugin
{
    public static class QtoUserMessageService
    {
        public static string SyncIdCreated(int count)
        {
            return "已補上 " + count + " 個 QTO_SYNC_ID。";
        }

        public static string DuplicateSyncIdFound(int duplicateGroupCount)
        {
            return "發現 " + duplicateGroupCount + " 組重複 QTO_SYNC_ID，請執行修復。";
        }

        public static string DuplicateSyncIdRepaired(int count)
        {
            return "已修復 " + count + " 個重複 QTO_SYNC_ID。";
        }

        public static string MissingSyncIdFound(int count)
        {
            return "發現 " + count + " 個物件缺少 QTO_SYNC_ID。";
        }

        public static string NoSyncIdIssueFound()
        {
            return "未發現 QTO_SYNC_ID 問題。";
        }
    }
}
