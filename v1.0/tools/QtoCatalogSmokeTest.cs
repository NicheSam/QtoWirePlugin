using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using QtoWirePlugin;

internal static class QtoCatalogSmokeTest
{
    [STAThread]
    private static int Main(string[] args)
    {
        string currentStep = "啟動";
        try
        {
            currentStep = "檢查 fixture";
            if (args.Length == 0 || !File.Exists(args[0]))
            {
                throw new InvalidOperationException("Fixture catalog path is required.");
            }

            string fixturePath = Path.GetFullPath(args[0]);
            QtoBlockCatalogService service = new QtoBlockCatalogService();
            currentStep = "檢查系統預設分類";
            Assert(
                QtoSystemDefaults.GetValues().SequenceEqual(new[] { "弱電", "停管", "資訊", "TV", "CCTV", "BA", "視聽音響", "緊急廣播" }),
                "Default system categories should preserve the agreed eight-item order.");
            currentStep = "檢查 DWG 索引路徑";
            Assert(service.GetCatalogPathForLegendDwg(@"C:\Sample\WeakCurrentLegend.dwg").EndsWith("WeakCurrentLegend.qto_catalog.json", StringComparison.OrdinalIgnoreCase), "Legend DWG should map to an automatic sibling catalog path.");
            currentStep = "載入 catalog";
            QtoBlockCatalog catalog = service.LoadCatalog(fixturePath);
            Assert(catalog.Items.Count == 3, "Fixture catalog should contain three items.");

            currentStep = "檢查同步 ID 掃描條件";
            Assert(!QtoChangePerformancePolicy.RequiresSyncIdScan(new[] { QtoCadChangeKind.Modified }), "Modified QTO objects should not trigger a full sync-id scan.");
            Assert(QtoChangePerformancePolicy.RequiresSyncIdScan(new[] { QtoCadChangeKind.Added }), "Added QTO objects should trigger duplicate sync-id detection.");
            Assert(!QtoChangePerformancePolicy.ShouldUseBackgroundDebounce(false), "Queue timer should stay stopped when no batch subscriber exists.");

            QtoBlockCatalogItem active = catalog.Items.Single(item => item.BlockName == "SAMPLE_LAN_OUTLET");
            Assert(active.Status == QtoBlockCatalogStatus.Active, "Active status should be preserved.");
            Assert(active.BudgetItemKey == "LAN_OUTLET", "Budget candidate key should be preserved.");

            currentStep = "檢查插入器預設篩選";
            using (QtoBlockLibraryPickerForm picker = new QtoBlockLibraryPickerForm(fixturePath))
            {
                Assert(picker.DisplayedItemCount == 2, "Picker should initially show active and unconfigured catalog items.");
            }

            currentStep = "檢查 catalog snapshot";
            QtoCatalogSnapshot snapshot = QtoCatalogSnapshotBuilder.Build(catalog);
            QtoCatalogItem snapshotItem;
            Assert(snapshot.IsLoaded && snapshot.TryFindByBlockName(active.BlockName, out snapshotItem), "Catalog snapshot should contain the active block.");

            currentStep = "檢查 Review";
            List<QtoValidationTarget> targets = new List<QtoValidationTarget>
            {
                new QtoValidationTarget
                {
                    ObjectHandle = "A1",
                    SyncId = "sync-a1",
                    SystemCode = "LAN",
                    EquipmentTypeCode = "LAN_OUTLET",
                    QuantityBasis = "point_count",
                    BlockName = "SAMPLE_LAN_OUTLET",
                    CatalogVersion = "1",
                    IsBlockReference = true
                }
            };

            QtoValidationResult withoutCatalog = QtoValidateService.ValidateTargets(targets, null);
            Assert(withoutCatalog.ReviewItems.Count(item => item.IssueType == QtoReviewIssueType.CatalogNotLoaded) == 1, "Missing catalog should create one global review item.");

            QtoValidationResult withCatalog = QtoValidateService.ValidateTargets(targets, snapshot);
            Assert(withCatalog.ReviewItems.All(item => item.IssueType != QtoReviewIssueType.CatalogNotLoaded), "Loaded catalog should remove the global missing-catalog review.");

            QtoBlockCatalogItem deprecated = catalog.Items.Single(item => item.Status == QtoBlockCatalogStatus.Deprecated);
            QtoValidationResult deprecatedResult = QtoValidateService.ValidateTargets(
                new[]
                {
                    new QtoValidationTarget
                    {
                        ObjectHandle = "A2",
                        SyncId = "sync-a2",
                        SystemCode = deprecated.SystemCode,
                        EquipmentTypeCode = deprecated.EquipmentTypeCode,
                        QuantityBasis = deprecated.QuantityBasis,
                        BlockName = deprecated.BlockName,
                        CatalogVersion = deprecated.Version,
                        IsBlockReference = true
                    }
                },
                snapshot);
            Assert(deprecatedResult.ReviewItems.Any(item => item.IssueType == QtoReviewIssueType.CatalogDeprecated), "Deprecated catalog items should enter Review.");

            currentStep = "檢查 JSON round trip";
            string roundTripPath = Path.Combine(Path.GetTempPath(), "qto-catalog-smoke-" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                service.SaveCatalog(catalog, roundTripPath);
                QtoBlockCatalog roundTrip = service.LoadCatalog(roundTripPath);
                QtoBlockCatalogItem saved = roundTrip.Items.Single(item => item.BlockName == active.BlockName);
                Assert(saved.DisplayName == active.DisplayName && saved.BudgetItemKey == active.BudgetItemKey, "Catalog round trip should preserve manual fields.");
            }
            finally
            {
                if (File.Exists(roundTripPath))
                {
                    File.Delete(roundTripPath);
                }
            }

            if (!args.Contains("--no-ui", StringComparer.OrdinalIgnoreCase))
            {
                currentStep = "檢查視窗啟動";
                Application.EnableVisualStyles();
                ShowAndClose(new QtoBlockLibraryPickerForm(fixturePath));
                ShowAndClose(new QtoBlockCatalogManagerForm(fixturePath, new QtoDictionaryStore()));
                ShowAndClose(new QtoWorkflowForm());
                ShowAndClose(new QtoBudgetExportOptionsForm(12, 3));
                ShowAndClose(new QtoSelectSameOptionsForm(new QtoSelectSameOptions(), new QtoDictionaryStore(), new List<string> { "Cat6" }));
                ShowAndClose(new QtoEditPropertiesForm("批次編輯出線口", 3, true, new Dictionary<string, string>()));
            }

            Console.WriteLine("QtoWirePlugin v1.0.1 catalog smoke test passed.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Smoke step failed: " + currentStep);
            Console.Error.WriteLine("Exception type: " + ex.GetType().FullName);
            try
            {
                Console.Error.WriteLine("Message: " + ex.Message);
            }
            catch
            {
                Console.Error.WriteLine("Message: <unavailable>");
            }
            return 1;
        }
    }

    private static void ShowAndClose(Form form)
    {
        using (form)
        {
            form.Show();
            Application.DoEvents();
            form.Close();
            Application.DoEvents();
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
