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
        private readonly Label selectionHint;
        private readonly TextBox sourceSearch;
        private readonly TextBox budgetSearch;
        private readonly TextBox budgetPath;
        private readonly CheckBox detailOnlyCheck;
        private readonly Button importButton;
        private readonly Button createRuleButton;
        private readonly Button confirmButton;
        private readonly Button blockButton;
        private readonly Button deleteButton;
        private readonly Button promoteButton;
        private readonly Button companyRulesButton;
        private readonly Button moveUpButton;
        private readonly Button moveDownButton;
        private readonly Button visibilityButton;
        private readonly Button saveButton;
        private bool dirty;

        public bool Changed { get; private set; }

        public QtoBudgetMappingForm(Database database, IList<QtoSyncRow> sourceRows)
        {
            this.database = database;
            this.sourceRows = sourceRows ?? new List<QtoSyncRow>();
            project = QtoBudgetProjectStore.Load(database);
            Text = "QTO 預算對應";
            Width = 1380;
            Height = 840;
            MinimumSize = new System.Drawing.Size(1080, 680);
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = new System.Drawing.Font("Microsoft JhengHei UI", 9F);
            QtoUiTheme.ApplyForm(this);

            sourceSearch = new TextBox { Width = 220, BorderStyle = BorderStyle.FixedSingle };
            sourceSearch.TextChanged += delegate { LoadData(); };
            budgetSearch = new TextBox { Width = 220, BorderStyle = BorderStyle.FixedSingle };
            budgetSearch.TextChanged += delegate { LoadData(); };
            budgetPath = new TextBox { Dock = DockStyle.Fill, ReadOnly = true, BorderStyle = BorderStyle.FixedSingle, TabStop = false };
            QtoUiTheme.ApplyReadOnlyTextBox(budgetPath);
            detailOnlyCheck = new CheckBox { Text = "只顯示可對應明細", AutoSize = true, Checked = true, Padding = new Padding(8, 2, 0, 0) };
            detailOnlyCheck.CheckedChanged += delegate { LoadData(); };

            status = QtoUiTheme.CreateMutedLabel(string.Empty);
            status.AutoEllipsis = true;
            selectionHint = QtoUiTheme.CreateMutedLabel("請先匯入預算 Excel。");
            selectionHint.AutoSize = true;
            selectionHint.Padding = new Padding(8, 8, 8, 0);

            sourceGrid = CreateGrid(true);
            masterGrid = CreateGrid(true);
            ruleGrid = CreateGrid(true);
            sourceGrid.SelectionChanged += delegate { UpdateUiState(); };
            masterGrid.SelectionChanged += delegate { UpdateUiState(); };
            ruleGrid.SelectionChanged += delegate { UpdateUiState(); };
            masterGrid.DataBindingComplete += delegate { StyleBudgetRows(); };

            quantityRule = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 180 };
            quantityRule.DataSource = new[]
            {
                new Choice("原始數量", QtoQuantityRuleType.SourceQuantity), new Choice("CAD 長度", QtoQuantityRuleType.CadLength),
                new Choice("固定數量", QtoQuantityRuleType.Fixed), new Choice("乘數", QtoQuantityRuleType.Multiplier),
                new Choice("耗損率 (%)", QtoQuantityRuleType.Waste), new Choice("每樓層一筆", QtoQuantityRuleType.PerFloor),
                new Choice("每區域一筆", QtoQuantityRuleType.PerArea)
            };
            quantityRule.DisplayMember = "Text";
            factor = new NumericUpDown { DecimalPlaces = 2, Maximum = 10000, Minimum = 0, Value = 1, Width = 100 };

            importButton = CreateActionButton("選擇預算 Excel", ImportBudget, 132, QtoButtonRole.Primary);
            createRuleButton = CreateActionButton("建立對應", AddRules, 118, QtoButtonRole.Primary);
            confirmButton = CreateActionButton("確認選取", ConfirmRules, 96, QtoButtonRole.Default);
            blockButton = CreateActionButton("標為不採用", BlockRules, 102, QtoButtonRole.Default);
            deleteButton = CreateActionButton("刪除", DeleteRules, 76, QtoButtonRole.Default);
            promoteButton = CreateActionButton("升級為公司規則", PromoteRules, 130, QtoButtonRole.Secondary);
            companyRulesButton = CreateActionButton("公司規則庫", OpenCompanyRules, 108, QtoButtonRole.Secondary);
            moveUpButton = CreateActionButton("上移", MoveMasterUp, 68, QtoButtonRole.Secondary);
            moveDownButton = CreateActionButton("下移", MoveMasterDown, 68, QtoButtonRole.Secondary);
            visibilityButton = CreateActionButton("顯示／隱藏", ToggleMasterVisibility, 96, QtoButtonRole.Secondary);
            saveButton = CreateActionButton("儲存變更", SaveProject, 104, QtoButtonRole.Primary);

            SplitContainer selectionSplit = new SplitContainer { Dock = DockStyle.Fill, SplitterWidth = 6 };
            selectionSplit.Panel1MinSize = 360;
            selectionSplit.Panel2MinSize = 360;
            selectionSplit.Panel1.Controls.Add(BuildSelectionPane("2A  CAD 計量來源（可多選）", "搜尋 CAD 資料", sourceSearch, sourceGrid, null));
            selectionSplit.Panel2.Controls.Add(BuildSelectionPane("2B  正式預算品項（可多選）", "搜尋名稱、項次或單位", budgetSearch, masterGrid, BuildBudgetRowActions()));

            TableLayoutPanel selectionPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = QtoUiTheme.WindowBackColor };
            selectionPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            selectionPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 58F));
            selectionPanel.Controls.Add(selectionSplit, 0, 0);
            selectionPanel.Controls.Add(BuildMappingBar(), 0, 1);

            SplitContainer vertical = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterWidth = 6 };
            vertical.Panel1MinSize = 320;
            vertical.Panel2MinSize = 180;
            vertical.Panel1.Controls.Add(selectionPanel);
            vertical.Panel2.Controls.Add(BuildRulePanel());

            TableLayoutPanel root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(14), BackColor = QtoUiTheme.WindowBackColor };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 68F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 74F));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.Controls.Add(BuildHeader(), 0, 0);
            root.Controls.Add(BuildBudgetSourcePanel(), 0, 1);
            root.Controls.Add(vertical, 0, 2);
            Controls.Add(root);

            Shown += delegate
            {
                SetSafeSplitter(selectionSplit, (int)(selectionSplit.Width * 0.47));
                SetSafeSplitter(vertical, (int)(vertical.Height * 0.62));
            };
            MergeCompanyRules();
            LoadData();
        }

        private static Button CreateActionButton(string text, EventHandler handler, int width, QtoButtonRole role)
        {
            Button button = QtoUiTheme.CreateButton(text, handler, role);
            button.Width = width;
            return button;
        }

        private Control BuildHeader()
        {
            Panel panel = new Panel { Dock = DockStyle.Fill, BackColor = QtoUiTheme.WindowBackColor };
            Label title = new Label { Text = "預算對應", Dock = DockStyle.Top, Height = 34, Font = QtoUiTheme.HeaderFont, ForeColor = QtoUiTheme.TextColor, TextAlign = System.Drawing.ContentAlignment.MiddleLeft };
            Label description = QtoUiTheme.CreateMutedLabel("依序選擇預算 Excel、建立 CAD 與預算品項的對應，再確認並儲存。未確認規則不會納入正式合計。");
            description.Dock = DockStyle.Fill;
            panel.Controls.Add(description);
            panel.Controls.Add(title);
            return panel;
        }

        private Control BuildBudgetSourcePanel()
        {
            TableLayoutPanel panel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 2, BackColor = QtoUiTheme.PanelBackColor, Padding = new Padding(10, 8, 10, 8), Margin = new Padding(0, 0, 0, 8) };
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112F));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 144F));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 26F));
            panel.Controls.Add(new Label { Text = "1  預算來源", Dock = DockStyle.Fill, Font = QtoUiTheme.SectionFont, ForeColor = QtoUiTheme.TextColor, TextAlign = System.Drawing.ContentAlignment.MiddleLeft }, 0, 0);
            panel.Controls.Add(budgetPath, 1, 0);
            panel.Controls.Add(importButton, 2, 0);
            panel.Controls.Add(status, 1, 1);
            panel.SetColumnSpan(status, 2);
            return panel;
        }

        private Control BuildSelectionPane(string title, string searchLabel, TextBox search, DataGridView grid, Control footer)
        {
            TableLayoutPanel panel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = footer == null ? 3 : 4, BackColor = QtoUiTheme.PanelBackColor, Padding = new Padding(8) };
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 38F));
            panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            if (footer != null) panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F));
            panel.Controls.Add(new Label { Text = title, Dock = DockStyle.Fill, Font = QtoUiTheme.SectionFont, ForeColor = QtoUiTheme.TextColor, TextAlign = System.Drawing.ContentAlignment.MiddleLeft }, 0, 0);
            FlowLayoutPanel searchBar = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = System.Windows.Forms.FlowDirection.LeftToRight, WrapContents = false, Padding = new Padding(0, 5, 0, 3) };
            searchBar.Controls.Add(new Label { Text = searchLabel, AutoSize = true, Padding = new Padding(0, 4, 6, 0), ForeColor = QtoUiTheme.MutedTextColor });
            searchBar.Controls.Add(search);
            if (ReferenceEquals(grid, masterGrid)) searchBar.Controls.Add(detailOnlyCheck);
            panel.Controls.Add(searchBar, 0, 1);
            panel.Controls.Add(grid, 0, 2);
            if (footer != null) panel.Controls.Add(footer, 0, 3);
            return panel;
        }

        private Control BuildBudgetRowActions()
        {
            FlowLayoutPanel panel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = System.Windows.Forms.FlowDirection.LeftToRight, WrapContents = false, Padding = new Padding(0, 4, 0, 0) };
            panel.Controls.Add(new Label { Text = "預算列整理", AutoSize = true, Padding = new Padding(0, 8, 6, 0), ForeColor = QtoUiTheme.MutedTextColor });
            panel.Controls.Add(moveUpButton);
            panel.Controls.Add(moveDownButton);
            panel.Controls.Add(visibilityButton);
            return panel;
        }

        private Control BuildMappingBar()
        {
            FlowLayoutPanel panel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = System.Windows.Forms.FlowDirection.LeftToRight, WrapContents = false, AutoScroll = true, BackColor = QtoUiTheme.PanelBackColor, Padding = new Padding(8, 8, 8, 6) };
            panel.Controls.Add(new Label { Text = "2C  建立對應", AutoSize = true, Font = QtoUiTheme.SectionFont, Padding = new Padding(0, 8, 12, 0), ForeColor = QtoUiTheme.TextColor });
            panel.Controls.Add(new Label { Text = "數量規則", AutoSize = true, Padding = new Padding(0, 8, 5, 0), ForeColor = QtoUiTheme.MutedTextColor });
            panel.Controls.Add(quantityRule);
            panel.Controls.Add(new Label { Text = "數值／倍數", AutoSize = true, Padding = new Padding(10, 8, 5, 0), ForeColor = QtoUiTheme.MutedTextColor });
            panel.Controls.Add(factor);
            panel.Controls.Add(createRuleButton);
            panel.Controls.Add(selectionHint);
            return panel;
        }

        private Control BuildRulePanel()
        {
            TableLayoutPanel panel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = QtoUiTheme.PanelBackColor, Padding = new Padding(8) };
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));
            panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            FlowLayoutPanel actions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = System.Windows.Forms.FlowDirection.LeftToRight, WrapContents = false, AutoScroll = true };
            actions.Controls.Add(new Label { Text = "3  檢查並儲存", AutoSize = true, Font = QtoUiTheme.SectionFont, Padding = new Padding(0, 10, 12, 0), ForeColor = QtoUiTheme.TextColor });
            actions.Controls.Add(confirmButton);
            actions.Controls.Add(blockButton);
            actions.Controls.Add(deleteButton);
            actions.Controls.Add(new Label { Text = "公司規則", AutoSize = true, Padding = new Padding(14, 10, 4, 0), ForeColor = QtoUiTheme.MutedTextColor });
            actions.Controls.Add(companyRulesButton);
            actions.Controls.Add(promoteButton);
            actions.Controls.Add(CreateActionButton("檢查完整性", OpenCompleteness, 108, QtoButtonRole.Secondary));
            actions.Controls.Add(saveButton);
            panel.Controls.Add(actions, 0, 0);
            panel.Controls.Add(ruleGrid, 0, 1);
            return panel;
        }

        private static void SetSafeSplitter(SplitContainer split, int desired)
        {
            int available = split.Orientation == Orientation.Vertical ? split.Width : split.Height;
            int minimum = split.Panel1MinSize;
            int maximum = Math.Max(minimum, available - split.Panel2MinSize - split.SplitterWidth);
            split.SplitterDistance = Math.Max(minimum, Math.Min(desired, maximum));
        }

        private void OpenCompleteness(object sender, EventArgs e)
        {
            if (dirty) SaveProject(sender, e);
            using (QtoBudgetCompletenessForm form = new QtoBudgetCompletenessForm(database, sourceRows))
            {
                QtoExternalWindowHost.ShowModal(form, this);
            }
            project = QtoBudgetProjectStore.Load(database);
            LoadData();
        }

        private static DataGridView CreateGrid(bool multiSelect)
        {
            DataGridView grid = new DataGridView
            {
                Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false,
                AutoGenerateColumns = true, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = multiSelect,
                RowHeadersVisible = false, BackgroundColor = System.Drawing.SystemColors.Window,
                AllowUserToResizeRows = false, ClipboardCopyMode = DataGridViewClipboardCopyMode.EnableWithoutHeaderText
            };
            QtoUiTheme.ApplyGrid(grid);
            return grid;
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
            IEnumerable<QtoBudgetMasterItem> budgetItems = project.MasterItems;
            if (detailOnlyCheck != null && detailOnlyCheck.Checked)
            {
                budgetItems = budgetItems.Where(i => string.Equals(i.RowType, QtoBudgetRowType.Detail, StringComparison.OrdinalIgnoreCase));
            }
            masterGrid.DataSource = new BindingList<BudgetItemView>(budgetItems
                .Where(i => string.IsNullOrWhiteSpace(budgetFilter) || JoinSearch(i.SourceSheet, i.ItemNo, i.ItemName, i.Unit, i.RowType, BuildBudgetPath(i)).IndexOf(budgetFilter, StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderBy(i => i.SortOrder)
                .Select(i => new BudgetItemView
                {
                    Item = i,
                    來源 = i.SourceSheet,
                    項次 = i.ItemNo,
                    階層 = RowTypeText(i.RowType),
                    分類 = BuildBudgetPath(i),
                    名稱 = i.ItemName,
                    單位 = i.Unit,
                    顯示狀態 = i.Hidden ? "隱藏" : "顯示"
                }).ToList());
            ruleGrid.DataSource = new BindingList<RuleView>(project.MappingRules.Select(ToView).ToList());
            SetNumberFormat(sourceGrid, "數量");
            sourceGrid.ClearSelection();
            masterGrid.ClearSelection();
            ruleGrid.ClearSelection();
            budgetPath.Text = string.IsNullOrWhiteSpace(project.SourceWorkbookPath) ? "尚未選擇預算 Excel" : project.SourceWorkbookPath;
            UpdateUiState();
        }

        private void ImportBudget(object sender, EventArgs e)
        {
            using (OpenFileDialog dialog = new OpenFileDialog { Title = "選擇既有預算 Excel", Filter = "Excel 活頁簿 (*.xlsx)|*.xlsx" })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    QtoBudgetProjectData imported = new QtoBudgetMasterImporter().Import(dialog.FileName);
                    imported.MappingRules.AddRange(project.MappingRules.Where(r => imported.MasterItems.Any(i => i.BudgetItemId == r.BudgetItemId)));
                    project = imported;
                    MergeCompanyRules();
                    MarkChanged();
                    LoadData();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "無法匯入這份預算 Excel。\r\n\r\n" + ex.Message, "匯入預算", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
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
            int created = 0;
            int duplicate = 0;
            foreach (SourceGroup source in sources)
            foreach (QtoBudgetMasterItem target in targets)
            {
                if (project.MappingRules.Any(r => IsSameMapping(r, source, target.BudgetItemId)))
                {
                    duplicate++;
                    continue;
                }
                project.MappingRules.Add(new QtoBudgetMappingRule
                {
                    RuleId = Guid.NewGuid().ToString("N"), SystemCode = source.系統, EquipmentTypeCode = source.設備類型,
                    CadMeasureType = source.CAD計量型態, CableType = source.線材, ConduitType = source.管材,
                    Unit = source.單位, BudgetItemId = target.BudgetItemId, QuantityRule = choice == null ? QtoQuantityRuleType.SourceQuantity : choice.Value,
                    Factor = (double)factor.Value, FixedQuantity = (double)factor.Value, Status = "candidate", Scope = "project", UpdatedAt = DateTime.UtcNow
                });
                created++;
            }
            if (created == 0)
            {
                MessageBox.Show(this, duplicate > 0 ? "選取的對應已經存在，沒有重複建立規則。" : "沒有建立任何對應。", "建立預算對應", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            MarkChanged();
            LoadData();
            if (duplicate > 0)
            {
                MessageBox.Show(this, "已建立 " + created + " 筆對應，並略過 " + duplicate + " 筆重複對應。", "建立預算對應", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void ConfirmRules(object sender, EventArgs e)
        {
            List<RuleView> selected = SelectedRuleViews();
            if (selected.Count == 0)
            {
                MessageBox.Show(this, "請先在下方選擇要確認的對應規則。", "確認預算對應", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            foreach (RuleView view in selected)
            {
                QtoBudgetMappingRule rule = project.MappingRules.First(r => r.RuleId == view.RuleId);
                rule.Status = "confirmed";
                rule.ReviewReason = string.Empty;
                rule.UpdatedAt = DateTime.UtcNow;
            }
            MarkChanged();
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
                MarkChanged();
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
                    MarkChanged();
                    MergeCompanyRules();
                    LoadData();
                }
            }
        }

        private void BlockRules(object sender, EventArgs e)
        {
            List<RuleView> selected = SelectedRuleViews();
            if (selected.Count == 0)
            {
                MessageBox.Show(this, "請先選擇不採用的對應規則。", "預算對應", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            foreach (RuleView view in selected)
            {
                QtoBudgetMappingRule rule = project.MappingRules.First(r => r.RuleId == view.RuleId);
                rule.Status = "blocked";
                rule.ReviewReason = "使用者阻擋此 mapping。";
                rule.UpdatedAt = DateTime.UtcNow;
            }
            MarkChanged();
            LoadData();
        }

        private void DeleteRules(object sender, EventArgs e)
        {
            HashSet<string> ids = new HashSet<string>(SelectedRuleViews().Select(v => v.RuleId), StringComparer.OrdinalIgnoreCase);
            if (ids.Count == 0)
            {
                MessageBox.Show(this, "請先選擇要刪除的對應規則。", "預算對應", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (MessageBox.Show(this, "確定刪除選取的 " + ids.Count + " 筆對應規則？", "刪除預算對應", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            project.MappingRules.RemoveAll(r => ids.Contains(r.RuleId));
            MarkChanged();
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
            MarkChanged();
            LoadData();
        }

        private void ToggleMasterVisibility(object sender, EventArgs e)
        {
            foreach (QtoBudgetMasterItem item in masterGrid.SelectedRows.Cast<DataGridViewRow>()
                .Select(r => r.DataBoundItem as BudgetItemView)
                .Where(v => v != null && v.Item != null)
                .Select(v => v.Item)) item.Hidden = !item.Hidden;
            MarkChanged();
            LoadData();
        }

        private static string JoinSearch(params string[] values) { return string.Join(" ", values ?? new string[0]); }

        private void MarkChanged()
        {
            Changed = true;
            dirty = true;
            UpdateUiState();
        }

        private void UpdateUiState()
        {
            if (project == null || sourceGrid == null || masterGrid == null || ruleGrid == null) return;
            bool hasBudget = project.MasterItems.Any(i => string.Equals(i.RowType, QtoBudgetRowType.Detail, StringComparison.OrdinalIgnoreCase));
            int selectedSources = sourceGrid.SelectedRows.Count;
            int selectedTargets = masterGrid.SelectedRows.Cast<DataGridViewRow>()
                .Select(r => r.DataBoundItem as BudgetItemView)
                .Count(v => v != null && v.Item != null && string.Equals(v.Item.RowType, QtoBudgetRowType.Detail, StringComparison.OrdinalIgnoreCase));
            List<RuleView> selectedRules = SelectedRuleViews();

            masterGrid.Enabled = hasBudget;
            createRuleButton.Enabled = hasBudget && selectedSources > 0 && selectedTargets > 0;
            confirmButton.Enabled = selectedRules.Count > 0;
            blockButton.Enabled = selectedRules.Count > 0;
            deleteButton.Enabled = selectedRules.Count > 0;
            promoteButton.Enabled = selectedRules.Any(view => string.Equals(view.狀態, "已確認", StringComparison.OrdinalIgnoreCase));
            moveUpButton.Enabled = hasBudget && masterGrid.SelectedRows.Count == 1;
            moveDownButton.Enabled = hasBudget && masterGrid.SelectedRows.Count == 1;
            visibilityButton.Enabled = hasBudget && masterGrid.SelectedRows.Count > 0;
            saveButton.Enabled = dirty;

            selectionHint.Text = !hasBudget
                ? "請先完成步驟 1。"
                : selectedSources == 0 || selectedTargets == 0
                    ? "請在左右各選至少一列。"
                    : "已選 CAD " + selectedSources + " 列、預算明細 " + selectedTargets + " 列。";

            if (!hasBudget)
            {
                status.Text = "尚未匯入預算格式。請先選擇案件使用的 Excel 預算書。";
            }
            else
            {
                int confirmed = project.MappingRules.Count(r => string.Equals(r.Status, "confirmed", StringComparison.OrdinalIgnoreCase));
                int pending = project.MappingRules.Count(r => !string.Equals(r.Status, "confirmed", StringComparison.OrdinalIgnoreCase) && !string.Equals(r.Status, "blocked", StringComparison.OrdinalIgnoreCase));
                status.Text = "可對應明細 " + project.MasterItems.Count(i => string.Equals(i.RowType, QtoBudgetRowType.Detail, StringComparison.OrdinalIgnoreCase))
                    + " 筆　已確認 " + confirmed + " 筆　待確認 " + pending + " 筆";
            }
            QtoUiTheme.ApplyStatusColor(status, status.Text);
        }

        private string BuildBudgetPath(QtoBudgetMasterItem item)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.ParentItemId)) return string.Empty;
            List<string> names = new List<string>();
            string parentId = item.ParentItemId;
            for (int depth = 0; depth < 4 && !string.IsNullOrWhiteSpace(parentId); depth++)
            {
                QtoBudgetMasterItem parent = project.MasterItems.FirstOrDefault(i => string.Equals(i.BudgetItemId, parentId, StringComparison.OrdinalIgnoreCase));
                if (parent == null) break;
                if (!string.IsNullOrWhiteSpace(parent.ItemName)) names.Add(parent.ItemName);
                parentId = parent.ParentItemId;
            }
            names.Reverse();
            return string.Join(" / ", names);
        }

        private static bool IsSameMapping(QtoBudgetMappingRule rule, SourceGroup source, string budgetItemId)
        {
            return rule != null
                && string.Equals(rule.BudgetItemId, budgetItemId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(rule.SystemCode ?? string.Empty, source.系統 ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                && string.Equals(rule.EquipmentTypeCode ?? string.Empty, source.設備類型 ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                && string.Equals(rule.CadMeasureType ?? string.Empty, source.CAD計量型態 ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                && string.Equals(rule.CableType ?? string.Empty, source.線材 ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                && string.Equals(rule.ConduitType ?? string.Empty, source.管材 ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                && string.Equals(rule.Unit ?? string.Empty, source.單位 ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        private static void SetNumberFormat(DataGridView grid, string columnName)
        {
            if (grid == null || !grid.Columns.Contains(columnName)) return;
            grid.Columns[columnName].DefaultCellStyle.Format = "0.###";
            grid.Columns[columnName].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        }

        private void StyleBudgetRows()
        {
            foreach (DataGridViewRow row in masterGrid.Rows)
            {
                BudgetItemView view = row.DataBoundItem as BudgetItemView;
                if (view == null || view.Item == null) continue;
                bool detail = string.Equals(view.Item.RowType, QtoBudgetRowType.Detail, StringComparison.OrdinalIgnoreCase);
                row.DefaultCellStyle.ForeColor = detail ? QtoUiTheme.TextColor : QtoUiTheme.MutedTextColor;
                row.DefaultCellStyle.Font = detail ? Font : QtoUiTheme.SectionFont;
            }
        }

        private void SaveProject(object sender, EventArgs e)
        {
            try
            {
                QtoBudgetProjectStore.Save(database, project);
                dirty = false;
                UpdateUiState();
                MessageBox.Show(this, "案件預算主檔與對應規則已儲存到目前 DWG。", "預算對應", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "無法儲存預算對應。\r\n\r\n" + ex.Message, "預算對應", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (dirty)
            {
                DialogResult choice = MessageBox.Show(
                    this,
                    "預算對應有尚未儲存的變更。\r\n\r\n選擇「是」儲存後關閉；選擇「否」放棄本次變更。",
                    "關閉預算對應",
                    MessageBoxButtons.YesNoCancel,
                    MessageBoxIcon.Question);
                if (choice == DialogResult.Cancel)
                {
                    e.Cancel = true;
                    return;
                }
                if (choice == DialogResult.No)
                {
                    dirty = false;
                    Changed = false;
                    base.OnFormClosing(e);
                    return;
                }
                try
                {
                    QtoBudgetProjectStore.Save(database, project);
                    dirty = false;
                }
                catch (Exception ex)
                {
                    e.Cancel = true;
                    MessageBox.Show(this, "預算對應尚未儲存，視窗將保持開啟。\r\n\r\n" + ex.Message, "預算對應", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
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
                MarkChanged();
            }
        }

        private sealed class Choice { public Choice(string text, string value) { Text = text; Value = value; } public string Text { get; private set; } public string Value { get; private set; } }
        private sealed class SourceGroup { public string 系統 { get; set; } public string 設備類型 { get; set; } public string CAD計量型態 { get; set; } public string 線材 { get; set; } public string 管材 { get; set; } public string 單位 { get; set; } public int 物件數 { get; set; } public double 數量 { get; set; } }
        private sealed class BudgetItemView { [Browsable(false)] public QtoBudgetMasterItem Item { get; set; } public string 來源 { get; set; } public string 項次 { get; set; } public string 階層 { get; set; } public string 分類 { get; set; } public string 名稱 { get; set; } public string 單位 { get; set; } public string 顯示狀態 { get; set; } }
        private sealed class RuleView { [Browsable(false)] public string RuleId { get; set; } public string 系統 { get; set; } public string 設備類型 { get; set; } public string 計量型態 { get; set; } public string 對應預算品項 { get; set; } public string 數量規則 { get; set; } public string 狀態 { get; set; } public string 規則來源 { get; set; } }
    }
}
