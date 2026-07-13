using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Autodesk.AutoCAD.DatabaseServices;

namespace QtoWirePlugin
{
    public sealed class QtoBudgetMappingForm : Form
    {
        private readonly Database database;
        private readonly IList<QtoSyncRow> sourceRows;
        private QtoBudgetProjectData project;
        private readonly DataGridView sourceGrid;
        private readonly DataGridView masterGrid;
        private readonly DataGridView ruleGrid;
        private readonly ComboBox quantityRule;
        private readonly NumericUpDown factor;
        private readonly Label status;
        private readonly ToolStripTextBox sourceSearch;
        private readonly ToolStripTextBox budgetSearch;

        public bool Changed { get; private set; }

        public QtoBudgetMappingForm(Database database, IList<QtoSyncRow> sourceRows)
        {
            this.database = database;
            this.sourceRows = sourceRows ?? new List<QtoSyncRow>();
            project = QtoBudgetProjectStore.Load(database);
            MergeCompanyRules();
            Text = "QTO 預算對應";
            Width = 1280;
            Height = 780;
            MinimumSize = new System.Drawing.Size(980, 620);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new System.Drawing.Font("Microsoft JhengHei UI", 9F);

            ToolStrip tools = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden, Dock = DockStyle.Top };
            tools.Items.Add(Button("匯入預算格式", ImportBudget));
            tools.Items.Add(new ToolStripLabel("CAD 搜尋"));
            sourceSearch = new ToolStripTextBox { Width = 110 };
            sourceSearch.TextChanged += delegate { LoadData(); };
            tools.Items.Add(sourceSearch);
            tools.Items.Add(new ToolStripLabel("預算搜尋"));
            budgetSearch = new ToolStripTextBox { Width = 110 };
            budgetSearch.TextChanged += delegate { LoadData(); };
            tools.Items.Add(budgetSearch);
            tools.Items.Add(new ToolStripSeparator());
            tools.Items.Add(Button("建立對應", AddRules));
            tools.Items.Add(Button("確認選取規則", ConfirmRules));
            tools.Items.Add(Button("阻擋選取規則", BlockRules));
            tools.Items.Add(Button("刪除選取規則", DeleteRules));
            tools.Items.Add(Button("加入公司規則", PromoteRules));
            tools.Items.Add(Button("公司規則庫", OpenCompanyRules));
            tools.Items.Add(Button("上移", MoveMasterUp));
            tools.Items.Add(Button("下移", MoveMasterDown));
            tools.Items.Add(Button("顯示／隱藏", ToggleMasterVisibility));
            tools.Items.Add(new ToolStripSeparator());
            tools.Items.Add(Button("儲存", SaveProject));

            status = new Label { Dock = DockStyle.Top, Height = 42, Padding = new Padding(10, 8, 10, 4), AutoEllipsis = true };
            SplitContainer vertical = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = 390 };
            SplitContainer top = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 570 };
            sourceGrid = CreateGrid(true);
            masterGrid = CreateGrid(true);
            top.Panel1.Controls.Add(Wrap("CAD 計量群組（可多選）", sourceGrid));
            top.Panel2.Controls.Add(Wrap("正式預算明細（可多選）", masterGrid));
            vertical.Panel1.Controls.Add(top);

            Panel rulePanel = new Panel { Dock = DockStyle.Fill };
            FlowLayoutPanel options = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 40, Padding = new Padding(8, 6, 8, 4) };
            options.Controls.Add(new Label { Text = "數量規則", AutoSize = true, Padding = new Padding(0, 6, 4, 0) });
            quantityRule = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 180 };
            quantityRule.DataSource = new[]
            {
                new Choice("原始數量", QtoQuantityRuleType.SourceQuantity), new Choice("CAD 長度", QtoQuantityRuleType.CadLength),
                new Choice("固定數量", QtoQuantityRuleType.Fixed), new Choice("乘數", QtoQuantityRuleType.Multiplier),
                new Choice("耗損率 (%)", QtoQuantityRuleType.Waste), new Choice("每樓層一筆", QtoQuantityRuleType.PerFloor),
                new Choice("每區域一筆", QtoQuantityRuleType.PerArea)
            };
            quantityRule.DisplayMember = "Text";
            options.Controls.Add(quantityRule);
            options.Controls.Add(new Label { Text = "數值／倍數", AutoSize = true, Padding = new Padding(12, 6, 4, 0) });
            factor = new NumericUpDown { DecimalPlaces = 2, Maximum = 10000, Minimum = 0, Value = 1, Width = 100 };
            options.Controls.Add(factor);
            ruleGrid = CreateGrid(true);
            rulePanel.Controls.Add(ruleGrid);
            rulePanel.Controls.Add(options);
            vertical.Panel2.Controls.Add(rulePanel);

            Controls.Add(vertical);
            Controls.Add(status);
            Controls.Add(tools);
            LoadData();
        }

        private static ToolStripButton Button(string text, EventHandler handler)
        {
            ToolStripButton button = new ToolStripButton(text) { DisplayStyle = ToolStripItemDisplayStyle.Text };
            button.Click += handler;
            return button;
        }

        private static Control Wrap(string title, Control content)
        {
            Panel panel = new Panel { Dock = DockStyle.Fill };
            panel.Controls.Add(content);
            panel.Controls.Add(new Label { Text = title, Dock = DockStyle.Top, Height = 28, Padding = new Padding(6, 6, 0, 0), Font = new System.Drawing.Font("Microsoft JhengHei UI", 9F, System.Drawing.FontStyle.Bold) });
            return panel;
        }

        private static DataGridView CreateGrid(bool multiSelect)
        {
            return new DataGridView
            {
                Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false,
                AutoGenerateColumns = true, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = multiSelect,
                RowHeadersVisible = false, BackgroundColor = System.Drawing.SystemColors.Window
            };
        }

        private void LoadData()
        {
            string sourceFilter = sourceSearch == null ? string.Empty : sourceSearch.Text.Trim();
            string budgetFilter = budgetSearch == null ? string.Empty : budgetSearch.Text.Trim();
            sourceGrid.DataSource = new BindingList<SourceGroup>(sourceRows.GroupBy(r => new
            {
                r.SystemCode, r.EquipmentTypeCode, r.CadMeasureType, r.CableType, r.ConduitType, r.Unit
            }).Select(g => new SourceGroup
            {
                系統 = g.Key.SystemCode, 設備類型 = g.Key.EquipmentTypeCode,
                CAD計量型態 = g.Key.CadMeasureType, 線材 = g.Key.CableType,
                管材 = g.Key.ConduitType, 單位 = g.Key.Unit, 物件數 = g.Count(), 數量 = g.Sum(r => r.Quantity)
            }).Where(g => string.IsNullOrWhiteSpace(sourceFilter) || JoinSearch(g.系統, g.設備類型, g.CAD計量型態, g.線材, g.管材).IndexOf(sourceFilter, StringComparison.OrdinalIgnoreCase) >= 0)
            .OrderBy(g => g.系統).ThenBy(g => g.設備類型).ToList());
            masterGrid.DataSource = new BindingList<BudgetItemView>(project.MasterItems
                .Where(i => string.IsNullOrWhiteSpace(budgetFilter) || JoinSearch(i.SourceSheet, i.ItemNo, i.ItemName, i.Unit, i.RowType).IndexOf(budgetFilter, StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderBy(i => i.SortOrder)
                .Select(i => new BudgetItemView
                {
                    Item = i,
                    來源 = i.SourceSheet,
                    項次 = i.ItemNo,
                    階層 = RowTypeText(i.RowType),
                    名稱 = i.ItemName,
                    單位 = i.Unit,
                    顯示狀態 = i.Hidden ? "隱藏" : "顯示"
                }).ToList());
            ruleGrid.DataSource = new BindingList<RuleView>(project.MappingRules.Select(ToView).ToList());
            status.Text = string.IsNullOrWhiteSpace(project.SourceWorkbookPath)
                ? "尚未匯入正式預算格式。"
                : "預算來源：" + project.SourceWorkbookPath + "　明細 " + project.MasterItems.Count(i => i.RowType == QtoBudgetRowType.Detail) + " 筆　規則 " + project.MappingRules.Count + " 筆　公司品項已確認 " + project.CompanyBindings.Count(b => string.Equals(b.Status, "confirmed", StringComparison.OrdinalIgnoreCase)) + " 筆";
        }

        private void ImportBudget(object sender, EventArgs e)
        {
            using (OpenFileDialog dialog = new OpenFileDialog { Title = "選擇既有預算 Excel", Filter = "Excel 活頁簿 (*.xlsx)|*.xlsx" })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                QtoBudgetProjectData imported = new QtoBudgetMasterImporter().Import(dialog.FileName);
                imported.MappingRules.AddRange(project.MappingRules.Where(r => imported.MasterItems.Any(i => i.BudgetItemId == r.BudgetItemId)));
                project = imported;
                MergeCompanyRules();
                Changed = true;
                LoadData();
            }
        }

        private void AddRules(object sender, EventArgs e)
        {
            List<SourceGroup> sources = sourceGrid.SelectedRows.Cast<DataGridViewRow>().Select(r => r.DataBoundItem as SourceGroup).Where(v => v != null).ToList();
            List<QtoBudgetMasterItem> targets = masterGrid.SelectedRows.Cast<DataGridViewRow>()
                .Select(r => r.DataBoundItem as BudgetItemView)
                .Where(v => v != null && v.Item != null && v.Item.RowType == QtoBudgetRowType.Detail)
                .Select(v => v.Item).ToList();
            if (sources.Count == 0 || targets.Count == 0)
            {
                MessageBox.Show(this, "請至少選擇一個 CAD 計量群組與一個正式預算明細。", "建立預算對應", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            Choice choice = quantityRule.SelectedItem as Choice;
            foreach (SourceGroup source in sources)
            foreach (QtoBudgetMasterItem target in targets)
            {
                project.MappingRules.Add(new QtoBudgetMappingRule
                {
                    RuleId = Guid.NewGuid().ToString("N"), SystemCode = source.系統, EquipmentTypeCode = source.設備類型,
                    CadMeasureType = source.CAD計量型態, CableType = source.線材, ConduitType = source.管材,
                    Unit = source.單位, BudgetItemId = target.BudgetItemId, QuantityRule = choice == null ? QtoQuantityRuleType.SourceQuantity : choice.Value,
                    Factor = (double)factor.Value, FixedQuantity = (double)factor.Value, Status = "candidate", Scope = "project", UpdatedAt = DateTime.UtcNow
                });
            }
            Changed = true;
            LoadData();
        }

        private void ConfirmRules(object sender, EventArgs e)
        {
            foreach (RuleView view in ruleGrid.SelectedRows.Cast<DataGridViewRow>().Select(r => r.DataBoundItem as RuleView).Where(v => v != null))
            {
                QtoBudgetMappingRule rule = project.MappingRules.First(r => r.RuleId == view.RuleId);
                rule.Status = "confirmed";
                rule.ReviewReason = string.Empty;
                rule.UpdatedAt = DateTime.UtcNow;
            }
            Changed = true;
            LoadData();
        }

        private void PromoteRules(object sender, EventArgs e)
        {
            QtoCompanyBudgetProfile profile = QtoCompanyBudgetProfileStore.Load(database);
            if (profile == null)
            {
                MessageBox.Show(this, "請先開啟「公司規則庫」，以正式預算建立公司基準。", "公司規則", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            List<QtoBudgetMappingRule> selected = SelectedRuleViews()
                .Select(v => project.MappingRules.FirstOrDefault(r => r.RuleId == v.RuleId))
                .Where(r => r != null).ToList();
            int expectedVersion = profile.Version;
            int count = QtoBudgetBindingService.PublishRules(project, profile, selected);
            if (count > 0)
            {
                QtoCompanyBudgetProfileStore.Save(database, profile, expectedVersion);
                QtoBudgetBindingService.RemovePromotedProjectRules(project, selected);
                QtoBudgetBindingService.RebuildBindings(project, profile);
                Changed = true;
                LoadData();
            }
            MessageBox.Show(this, count == 0 ? "沒有可發布的規則。請先確認 mapping，並完成公司品項比對。" : "已加入公司規則 " + count + " 筆。", "公司規則", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void OpenCompanyRules(object sender, EventArgs e)
        {
            using (QtoCompanyBudgetProfileForm form = new QtoCompanyBudgetProfileForm(database, project))
            {
                form.ShowDialog(this);
                if (form.Changed)
                {
                    Changed = true;
                    MergeCompanyRules();
                    LoadData();
                }
            }
        }

        private void BlockRules(object sender, EventArgs e)
        {
            foreach (RuleView view in SelectedRuleViews())
            {
                QtoBudgetMappingRule rule = project.MappingRules.First(r => r.RuleId == view.RuleId);
                rule.Status = "blocked";
                rule.ReviewReason = "使用者阻擋此 mapping。";
                rule.UpdatedAt = DateTime.UtcNow;
            }
            Changed = true;
            LoadData();
        }

        private void DeleteRules(object sender, EventArgs e)
        {
            HashSet<string> ids = new HashSet<string>(SelectedRuleViews().Select(v => v.RuleId), StringComparer.OrdinalIgnoreCase);
            project.MappingRules.RemoveAll(r => ids.Contains(r.RuleId));
            Changed = true;
            LoadData();
        }

        private List<RuleView> SelectedRuleViews()
        {
            return ruleGrid.SelectedRows.Cast<DataGridViewRow>().Select(r => r.DataBoundItem as RuleView).Where(v => v != null).ToList();
        }

        private void MoveMasterUp(object sender, EventArgs e) { MoveMaster(-1); }
        private void MoveMasterDown(object sender, EventArgs e) { MoveMaster(1); }

        private void MoveMaster(int direction)
        {
            BudgetItemView selectedView = masterGrid.CurrentRow == null ? null : masterGrid.CurrentRow.DataBoundItem as BudgetItemView;
            QtoBudgetMasterItem selected = selectedView == null ? null : selectedView.Item;
            if (selected == null) return;
            List<QtoBudgetMasterItem> ordered = project.MasterItems.OrderBy(i => i.SortOrder).ToList();
            int index = ordered.IndexOf(selected);
            int swapIndex = index + direction;
            if (index < 0 || swapIndex < 0 || swapIndex >= ordered.Count) return;
            int value = ordered[index].SortOrder;
            ordered[index].SortOrder = ordered[swapIndex].SortOrder;
            ordered[swapIndex].SortOrder = value;
            Changed = true;
            LoadData();
        }

        private void ToggleMasterVisibility(object sender, EventArgs e)
        {
            foreach (QtoBudgetMasterItem item in masterGrid.SelectedRows.Cast<DataGridViewRow>()
                .Select(r => r.DataBoundItem as BudgetItemView)
                .Where(v => v != null && v.Item != null)
                .Select(v => v.Item)) item.Hidden = !item.Hidden;
            Changed = true;
            LoadData();
        }

        private static string JoinSearch(params string[] values) { return string.Join(" ", values ?? new string[0]); }

        private void SaveProject(object sender, EventArgs e)
        {
            QtoBudgetProjectStore.Save(database, project);
            Changed = true;
            MessageBox.Show(this, "案件預算主檔與 mapping 已保存到 DWG。", "預算對應", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (Changed) QtoBudgetProjectStore.Save(database, project);
            base.OnFormClosing(e);
        }

        private RuleView ToView(QtoBudgetMappingRule rule)
        {
            QtoBudgetMasterItem item = project.MasterItems.FirstOrDefault(i => i.BudgetItemId == rule.BudgetItemId);
            return new RuleView
            {
                RuleId = rule.RuleId,
                系統 = rule.SystemCode,
                設備類型 = rule.EquipmentTypeCode,
                計量型態 = rule.CadMeasureType,
                對應預算品項 = item == null ? rule.BudgetItemId : item.ItemName,
                數量規則 = QuantityRuleText(rule.QuantityRule),
                狀態 = MappingStatusText(rule.Status),
                規則來源 = string.Equals(rule.Scope, "company", StringComparison.OrdinalIgnoreCase) ? "公司規則" : "本案規則"
            };
        }

        private static string RowTypeText(string value)
        {
            if (string.Equals(value, QtoBudgetRowType.System, StringComparison.OrdinalIgnoreCase)) return "系統大項";
            if (string.Equals(value, QtoBudgetRowType.Category, StringComparison.OrdinalIgnoreCase)) return "工程分類";
            if (string.Equals(value, QtoBudgetRowType.Parent, StringComparison.OrdinalIgnoreCase)) return "父項";
            if (string.Equals(value, QtoBudgetRowType.Detail, StringComparison.OrdinalIgnoreCase)) return "計價明細";
            return "其他";
        }

        private static string QuantityRuleText(string value)
        {
            if (string.Equals(value, QtoQuantityRuleType.CadLength, StringComparison.OrdinalIgnoreCase)) return "CAD 長度";
            if (string.Equals(value, QtoQuantityRuleType.Fixed, StringComparison.OrdinalIgnoreCase)) return "固定數量";
            if (string.Equals(value, QtoQuantityRuleType.Multiplier, StringComparison.OrdinalIgnoreCase)) return "來源數量乘倍數";
            if (string.Equals(value, QtoQuantityRuleType.Waste, StringComparison.OrdinalIgnoreCase)) return "增加耗損率";
            if (string.Equals(value, QtoQuantityRuleType.PerFloor, StringComparison.OrdinalIgnoreCase)) return "每樓層一筆";
            if (string.Equals(value, QtoQuantityRuleType.PerArea, StringComparison.OrdinalIgnoreCase)) return "每區域一筆";
            return "原始數量";
        }

        private static string MappingStatusText(string value)
        {
            if (string.Equals(value, "confirmed", StringComparison.OrdinalIgnoreCase)) return "已確認";
            if (string.Equals(value, "blocked", StringComparison.OrdinalIgnoreCase)) return "已阻擋";
            if (string.Equals(value, "candidate", StringComparison.OrdinalIgnoreCase)) return "待確認";
            return "需檢查";
        }

        private void MergeCompanyRules()
        {
            QtoCompanyBudgetProfile profile = QtoCompanyBudgetProfileStore.Load(database);
            if (profile != null
                && (!string.Equals(project.CompanyProfileId, profile.ProfileId, StringComparison.OrdinalIgnoreCase)
                    || project.CompanyProfileVersion != profile.Version))
            {
                QtoBudgetBindingService.RebuildBindings(project, profile);
                Changed = true;
            }
        }

        private sealed class Choice { public Choice(string text, string value) { Text = text; Value = value; } public string Text { get; private set; } public string Value { get; private set; } }
        private sealed class SourceGroup { public string 系統 { get; set; } public string 設備類型 { get; set; } public string CAD計量型態 { get; set; } public string 線材 { get; set; } public string 管材 { get; set; } public string 單位 { get; set; } public int 物件數 { get; set; } public double 數量 { get; set; } }
        private sealed class BudgetItemView { [Browsable(false)] public QtoBudgetMasterItem Item { get; set; } public string 來源 { get; set; } public string 項次 { get; set; } public string 階層 { get; set; } public string 名稱 { get; set; } public string 單位 { get; set; } public string 顯示狀態 { get; set; } }
        private sealed class RuleView { [Browsable(false)] public string RuleId { get; set; } public string 系統 { get; set; } public string 設備類型 { get; set; } public string 計量型態 { get; set; } public string 對應預算品項 { get; set; } public string 數量規則 { get; set; } public string 狀態 { get; set; } public string 規則來源 { get; set; } }
    }
}
