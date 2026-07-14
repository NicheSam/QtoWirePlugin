using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Autodesk.AutoCAD.DatabaseServices;

namespace QtoWirePlugin
{
    public sealed class QtoBudgetCompletenessForm : Form
    {
        private readonly Database database;
        private readonly IList<QtoSyncRow> sourceRows;
        private readonly DataGridView grid;
        private readonly Label summaryLabel;
        private readonly ComboBox categoryFilter;
        private QtoBudgetProjectData project;
        private QtoBudgetCompletenessResult result;

        public QtoBudgetCompletenessForm(Database database, IList<QtoSyncRow> sourceRows)
        {
            this.database = database;
            this.sourceRows = sourceRows ?? new List<QtoSyncRow>();
            Text = "QTO 預算完整性";
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(1180, 720);
            MinimumSize = new Size(900, 580);
            AutoScaleMode = AutoScaleMode.Dpi;
            QtoUiTheme.ApplyForm(this);

            TableLayoutPanel root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, Padding = new Padding(14) };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 68));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 78));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
            Controls.Add(root);

            Panel header = new Panel { Dock = DockStyle.Fill };
            header.Controls.Add(new Label { Text = "檢查 CAD 與正式預算的數量覆蓋", Dock = DockStyle.Top, Height = 34, Font = QtoUiTheme.HeaderFont, ForeColor = QtoUiTheme.TextColor });
            header.Controls.Add(QtoUiTheme.CreateMutedLabel("檢查結果不是正式報價判定；未確認 mapping 不會納入正式合計。"));
            root.Controls.Add(header, 0, 0);

            summaryLabel = QtoUiTheme.CreateMutedLabel(string.Empty);
            summaryLabel.Dock = DockStyle.Fill;
            summaryLabel.Padding = new Padding(10);
            root.Controls.Add(QtoUiTheme.WrapGroup("檢查摘要", summaryLabel), 0, 1);

            FlowLayoutPanel filters = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Padding = new Padding(6) };
            filters.Controls.Add(new Label { Text = "顯示", AutoSize = true, Padding = new Padding(0, 7, 4, 0), ForeColor = QtoUiTheme.MutedTextColor });
            categoryFilter = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 180 };
            categoryFilter.Items.AddRange(new object[] { "需要處理", "全部", "CAD 有、預算無", "預算有、CAD 無", "對應待確認", "規則異常" });
            categoryFilter.SelectedIndex = 0;
            categoryFilter.SelectedIndexChanged += delegate { BindIssues(); };
            filters.Controls.Add(categoryFilter);
            filters.Controls.Add(QtoUiTheme.CreateButton("重新檢查", delegate { Reload(); }, QtoButtonRole.Secondary));
            filters.Controls.Add(QtoUiTheme.CreateButton("開啟預算對應", OpenMapping, QtoButtonRole.Primary));
            root.Controls.Add(filters, 0, 2);

            grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoGenerateColumns = false,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                RowHeadersVisible = false,
                MultiSelect = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };
            QtoUiTheme.ApplyGrid(grid);
            AddColumn("SeverityDisplay", "嚴重性", 55);
            AddColumn("CategoryDisplay", "分類", 90);
            AddColumn("SystemCode", "系統", 70);
            AddColumn("EquipmentTypeCode", "設備類型", 100);
            AddColumn("CadMeasureType", "CAD 計量型態", 100);
            AddColumn("BudgetItemName", "預算品項", 140);
            AddColumn("Message", "問題說明", 210);
            AddColumn("SuggestedAction", "建議處理", 190);
            root.Controls.Add(grid, 0, 3);

            FlowLayoutPanel actions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = System.Windows.Forms.FlowDirection.RightToLeft, WrapContents = false, Padding = new Padding(0, 8, 0, 0) };
            actions.Controls.Add(QtoUiTheme.CreateButton("關閉", delegate { Close(); }, QtoButtonRole.Default));
            actions.Controls.Add(QtoUiTheme.CreateButton("清除人工分類", delegate { SetCoverageMode(null); }, QtoButtonRole.Secondary));
            actions.Controls.Add(QtoUiTheme.CreateButton("標記不適用", delegate { SetCoverageMode(QtoBudgetCoverageMode.Excluded); }, QtoButtonRole.Secondary));
            actions.Controls.Add(QtoUiTheme.CreateButton("標記固定數量", delegate { SetCoverageMode(QtoBudgetCoverageMode.Fixed); }, QtoButtonRole.Secondary));
            actions.Controls.Add(QtoUiTheme.CreateButton("標記人工估算", delegate { SetCoverageMode(QtoBudgetCoverageMode.Manual); }, QtoButtonRole.Primary));
            root.Controls.Add(actions, 0, 4);

            Reload();
        }

        private void Reload()
        {
            project = QtoBudgetProjectStore.Load(database);
            result = QtoBudgetCompletenessService.Analyze(sourceRows, project);
            QtoBudgetCompletenessSummary summary = result.Summary;
            summaryLabel.Text = "CAD 群組 " + summary.CadGroupCount.ToString("0")
                + "：已對應 " + summary.ConfirmedCadGroupCount.ToString("0")
                + "、待確認 " + summary.PendingCadGroupCount.ToString("0")
                + "、未對應／阻擋 " + summary.UnmappedCadGroupCount.ToString("0")
                + "\r\n預算明細 " + summary.BudgetDetailCount.ToString("0")
                + "：CAD 來源 " + summary.CadSourcedBudgetCount.ToString("0")
                + "、人工 " + summary.ManualBudgetCount.ToString("0")
                + "、固定 " + summary.FixedBudgetCount.ToString("0")
                + "、不適用 " + summary.ExcludedBudgetCount.ToString("0")
                + "、尚無來源 " + summary.MissingSourceBudgetCount.ToString("0");
            BindIssues();
        }

        private void BindIssues()
        {
            string filter = categoryFilter.SelectedItem == null ? "需要處理" : categoryFilter.SelectedItem.ToString();
            IEnumerable<QtoBudgetCompletenessIssue> issues = result == null ? Enumerable.Empty<QtoBudgetCompletenessIssue>() : result.Issues;
            if (!string.Equals(filter, "全部", StringComparison.Ordinal) && !string.Equals(filter, "需要處理", StringComparison.Ordinal))
            {
                issues = issues.Where(issue => string.Equals(issue.Category, filter, StringComparison.Ordinal));
            }
            grid.DataSource = new BindingList<QtoBudgetCompletenessIssue>(issues.ToList());
        }

        private void AddColumn(string property, string title, int weight)
        {
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = property, HeaderText = title, FillWeight = weight });
        }

        private List<QtoBudgetCompletenessIssue> SelectedIssues()
        {
            List<QtoBudgetCompletenessIssue> selected = new List<QtoBudgetCompletenessIssue>();
            foreach (DataGridViewRow row in grid.SelectedRows)
            {
                QtoBudgetCompletenessIssue issue = row.DataBoundItem as QtoBudgetCompletenessIssue;
                if (issue != null) selected.Add(issue);
            }
            return selected;
        }

        private void SetCoverageMode(string mode)
        {
            List<string> budgetItemIds = SelectedIssues()
                .Select(issue => issue.BudgetItemId)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (budgetItemIds.Count == 0)
            {
                MessageBox.Show(this, "請選取「預算有、CAD 無」的品項後再設定。", "預算完整性", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            project.CoverageOverrides.RemoveAll(item => budgetItemIds.Contains(item.BudgetItemId, StringComparer.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(mode))
            {
                foreach (string id in budgetItemIds)
                {
                    project.CoverageOverrides.Add(new QtoBudgetCoverageOverride
                    {
                        BudgetItemId = id,
                        CoverageMode = mode,
                        Note = "由預算完整性檢查人工分類。",
                        UpdatedAt = DateTime.UtcNow
                    });
                }
            }
            QtoBudgetProjectStore.Save(database, project);
            Reload();
        }

        private void OpenMapping(object sender, EventArgs e)
        {
            using (QtoBudgetMappingForm form = new QtoBudgetMappingForm(database, sourceRows))
            {
                QtoExternalWindowHost.ShowModal(form, this);
            }
            Reload();
        }
    }
}
