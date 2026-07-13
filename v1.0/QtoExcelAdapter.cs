using System.Collections.Generic;

namespace QtoWirePlugin
{
    public class QtoExcelAdapter
    {
        private readonly QtoExcelWorkbookBuilder _workbookBuilder;

        public QtoExcelAdapter()
        {
            _workbookBuilder = new QtoExcelWorkbookBuilder();
        }

        public QtoExcelResult FullRebuild(string workbookPath, List<QtoSyncRow> syncRows)
        {
            return FullRebuild(workbookPath, syncRows, null, null, null);
        }

        public QtoExcelResult FullRebuild(
            string workbookPath,
            List<QtoSyncRow> syncRows,
            List<QtoReviewItem> reviewItems,
            List<QtoSyncLogRow> logRows)
        {
            return FullRebuild(workbookPath, syncRows, reviewItems, logRows, null);
        }

        public QtoExcelResult FullRebuild(
            string workbookPath,
            List<QtoSyncRow> syncRows,
            List<QtoReviewItem> reviewItems,
            List<QtoSyncLogRow> logRows,
            IDictionary<string, string> settings)
        {
            return FullRebuild(workbookPath, syncRows, reviewItems, logRows, settings, null);
        }

        public QtoExcelResult FullRebuild(
            string workbookPath,
            List<QtoSyncRow> syncRows,
            List<QtoReviewItem> reviewItems,
            List<QtoSyncLogRow> logRows,
            IDictionary<string, string> settings,
            QtoBudgetProjectData budgetProject)
        {
            return _workbookBuilder.CreateOrRebuildWorkbook(
                workbookPath,
                syncRows ?? new List<QtoSyncRow>(),
                reviewItems ?? new List<QtoReviewItem>(),
                logRows ?? new List<QtoSyncLogRow>(),
                settings,
                budgetProject);
        }
    }
}
