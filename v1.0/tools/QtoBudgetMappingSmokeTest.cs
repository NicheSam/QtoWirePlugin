using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using QtoWirePlugin;

internal static class QtoBudgetMappingSmokeTest
{
    private static int Main(string[] args)
    {
        try
        {
            return Run(args);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.GetType().FullName + ": " + ex.Message);
            if (ex.InnerException != null) Console.Error.WriteLine("Inner: " + ex.InnerException.GetType().FullName + ": " + ex.InnerException.Message);
            return 1;
        }
    }

    private static int Run(string[] args)
    {
        if (args.Length == 0) throw new ArgumentException("需要範例 Excel 路徑。");
        QtoBudgetProjectData project = new QtoBudgetMasterImporter().Import(args[0]);
        int detailCount = project.MasterItems.Count(i => i.RowType == QtoBudgetRowType.Detail);
        Assert(project.MasterItems.Any(i => i.RowType == QtoBudgetRowType.System), "缺少系統大項。");
        Assert(project.MasterItems.Any(i => i.RowType == QtoBudgetRowType.Category), "缺少工程分類。");
        Assert(project.MasterItems.Any(i => i.RowType == QtoBudgetRowType.Parent), "缺少父項。");
        Assert(detailCount > 100 && detailCount < 3000, "明細數異常，可能掃入格式空白列。Count=" + detailCount);
        QtoCompanyBudgetProfile realProfile = QtoCompanyBudgetProfileStore.CreateFromProject(project);
        Assert(realProfile.Items.Select(i => i.CompanyBudgetItemId).Distinct(StringComparer.OrdinalIgnoreCase).Count() == realProfile.Items.Count, "公司預算品項 ID 發生碰撞。");
        TestCompanyBudgetBindings();

        List<QtoBudgetMasterItem> targets = project.MasterItems.Where(i => i.RowType == QtoBudgetRowType.Detail).Take(7).ToList();
        project.MappingRules.Clear();
        AddRule(project, targets[0], QtoQuantityRuleType.SourceQuantity, 1, 0);
        AddRule(project, targets[1], QtoQuantityRuleType.CadLength, 1, 0);
        AddRule(project, targets[2], QtoQuantityRuleType.Fixed, 1, 2);
        AddRule(project, targets[3], QtoQuantityRuleType.Multiplier, 1.2, 0);
        AddRule(project, targets[4], QtoQuantityRuleType.Waste, 20, 0);
        AddRule(project, targets[5], QtoQuantityRuleType.PerFloor, 1.5, 0);
        AddRule(project, targets[6], QtoQuantityRuleType.PerArea, 2, 0);
        IList<QtoMappedBudgetRow> mapped = QtoBudgetMappingEngine.Build(new List<QtoSyncRow>
        {
            new QtoSyncRow { SystemCode = "資訊", Quantity = 10, LengthM = 10, Unit = "點", Floor = "1F", Area = "A" },
            new QtoSyncRow { SystemCode = "資訊", Quantity = 5, LengthM = 20, Unit = "點", Floor = "2F", Area = "B" }
        }, project);
        double[] expected = { 15, 30, 2, 18, 18, 3, 4 };
        for (int i = 0; i < targets.Count; i++)
        {
            double actual = mapped.First(r => r.MasterItem.BudgetItemId == targets[i].BudgetItemId).Quantity;
            Assert(Math.Abs(actual - expected[i]) < 0.0001d, "數量規則錯誤。Index=" + i + "; Quantity=" + actual);
        }
        QtoMappedBudgetRow row = mapped.First(r => r.MasterItem.BudgetItemId == targets[3].BudgetItemId);
        Assert(row.SourceCount == 2 && row.MappingStatus == "confirmed", "mapping 狀態或來源筆數錯誤。");
        Console.WriteLine("Budget mapping smoke passed. Master=" + project.MasterItems.Count + "; Detail=" + detailCount + "; Mapped=" + row.Quantity);
        return 0;
    }

    private static void TestCompanyBudgetBindings()
    {
        QtoBudgetProjectData baseline = CreateBindingFixture("BI-SYS-10", "BI-ITEM-11", 10, 11);
        QtoBudgetProjectData shifted = CreateBindingFixture("BI-SYS-30", "BI-ITEM-31", 30, 31);
        QtoCompanyBudgetProfile first = QtoCompanyBudgetProfileStore.CreateFromProject(baseline);
        QtoCompanyBudgetProfile second = QtoCompanyBudgetProfileStore.CreateFromProject(shifted);
        Assert(first.Items.Single().CompanyBudgetItemId == second.Items.Single().CompanyBudgetItemId, "插入列後公司預算品項 ID 不應改變。");

        QtoCompanyBudgetItem alias = new QtoCompanyBudgetItem
        {
            CompanyBudgetItemId = "CBI-ALIAS",
            CanonicalName = "舊稱資訊插座",
            Unit = "點",
            HierarchyPath = "資訊系統設備工程",
            CanonicalSignature = "不會命中的簽章",
            Status = "active",
            Aliases = new List<string> { "資訊插座" }
        };
        QtoCompanyBudgetItem missing = new QtoCompanyBudgetItem
        {
            CompanyBudgetItemId = "CBI-MISSING", CanonicalName = "不存在的設備", Unit = "台",
            CanonicalSignature = "不存在", Status = "active"
        };
        first.Items.Add(alias);
        first.Items.Add(missing);
        QtoCompanyBudgetProfile rebuilt = QtoCompanyBudgetProfileStore.CreateFromProject(shifted);
        first.Items[0].Aliases.Add("網路資訊插座");
        QtoCompanyBudgetProfileStore.PreserveItemMetadata(first, rebuilt);
        Assert(rebuilt.Items[0].Aliases.Contains("網路資訊插座"), "重建公司基準不得清除既有品項別名。");
        QtoBudgetBindingService.RebuildBindings(shifted, first);
        Assert(shifted.CompanyBindings.Any(b => b.CompanyBudgetItemId == first.Items[0].CompanyBudgetItemId && b.Status == "confirmed"), "精確比對應自動確認。");
        Assert(shifted.CompanyBindings.Any(b => b.CompanyBudgetItemId == "CBI-ALIAS" && b.Status == "candidate"), "別名比對只能成為待確認候選。");
        Assert(shifted.CompanyBindings.Any(b => b.CompanyBudgetItemId == "CBI-MISSING" && b.Status == "needs_review"), "未命中品項必須保留待確認紀錄。");

        QtoBudgetMappingRule projectRule = new QtoBudgetMappingRule
        {
            RuleId = "PROJECT-RULE", SystemCode = "資訊", EquipmentTypeCode = "LAN_OUTLET",
            BudgetItemId = shifted.MasterItems.Single(i => i.RowType == QtoBudgetRowType.Detail).BudgetItemId,
            QuantityRule = QtoQuantityRuleType.SourceQuantity, Status = "confirmed", Scope = "project"
        };
        shifted.MappingRules.Add(projectRule);
        QtoBudgetBindingService.PublishRules(shifted, first, new[] { projectRule, projectRule });
        Assert(first.MappingRules.Count == 1 && !string.IsNullOrWhiteSpace(first.MappingRules[0].CompanyBudgetItemId), "重複發布不得產生重複公司規則。");
        QtoBudgetBindingService.RemovePromotedProjectRules(shifted, new[] { projectRule });
        QtoBudgetBindingService.RebuildBindings(shifted, first);
        Assert(shifted.MappingRules.Count == 1 && string.Equals(shifted.MappingRules[0].Scope, "company", StringComparison.OrdinalIgnoreCase), "公司規則發布後不得與原案件規則重複套用。");
        string profilePath = Path.Combine(Path.GetTempPath(), "qto_company_profile_smoke_" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            QtoCompanyBudgetProfileStore.Save(profilePath, first, 0);
            QtoCompanyBudgetProfile loaded = QtoCompanyBudgetProfileStore.Load(profilePath);
            Assert(loaded != null && loaded.MappingRules.Count == 1 && loaded.Items.Count == first.Items.Count, "公司規則庫儲存後應可完整載回。");
            Assert(loaded.Items[0].Aliases.Contains("網路資訊插座"), "公司品項別名儲存後應可完整載回。");
        }
        finally
        {
            if (File.Exists(profilePath)) File.Delete(profilePath);
            if (File.Exists(profilePath + ".bak")) File.Delete(profilePath + ".bak");
        }
    }

    private static QtoBudgetProjectData CreateBindingFixture(string systemId, string detailId, int systemRow, int detailRow)
    {
        QtoBudgetProjectData data = new QtoBudgetProjectData { SchemaVersion = 1, SourceWorkbookPath = "fixture.xlsx" };
        data.MasterItems.Add(new QtoBudgetMasterItem
        {
            BudgetItemId = systemId, RowType = QtoBudgetRowType.System, SourceSheet = "弱電", SourceRow = systemRow,
            ItemName = "資訊系統設備工程", SortOrder = 0
        });
        data.MasterItems.Add(new QtoBudgetMasterItem
        {
            BudgetItemId = detailId, ParentItemId = systemId, RowType = QtoBudgetRowType.Detail, SourceSheet = "弱電", SourceRow = detailRow,
            ItemName = "資訊插座", Unit = "點", SortOrder = 1
        });
        return data;
    }

    private static void AddRule(QtoBudgetProjectData project, QtoBudgetMasterItem target, string quantityRule, double factor, double fixedQuantity)
    {
        project.MappingRules.Add(new QtoBudgetMappingRule
        {
            RuleId = Guid.NewGuid().ToString("N"), SystemCode = "資訊", BudgetItemId = target.BudgetItemId,
            QuantityRule = quantityRule, Factor = factor, FixedQuantity = fixedQuantity, Status = "confirmed"
        });
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
