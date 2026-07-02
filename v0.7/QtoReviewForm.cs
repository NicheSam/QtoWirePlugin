using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace QtoWirePlugin
{
    public class QtoReviewForm : Form
    {
        private readonly BindingList<QtoReviewItem> items;
        private readonly DataGridView reviewGrid;
        private readonly TextBox summaryTextBox;
        private readonly TextBox technicalTextBox;
        private readonly Button technicalButton;
        private bool technicalVisible;

        public QtoReviewForm()
            : this(QtoUiSampleData.CreateReviewItems())
        {
        }

        public QtoReviewForm(IEnumerable<QtoReviewItem> reviewItems)
        {
            Text = "QTO Review 清單";
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(980, 640);
            MinimumSize = new Size(880, 560);
            QtoUiTheme.ApplyForm(this);

            items = new BindingList<QtoReviewItem>(new List<QtoReviewItem>(reviewItems ?? QtoUiSampleData.CreateReviewItems()));

            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.Padding = new Padding(12);
            root.ColumnCount = 1;
            root.RowCount = 4;
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 112));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
            Controls.Add(root);

            root.Controls.Add(CreateFilterPanel(), 0, 0);

            reviewGrid = new DataGridView();
            reviewGrid.Dock = DockStyle.Fill;
            reviewGrid.AutoGenerateColumns = false;
            reviewGrid.AllowUserToAddRows = false;
            reviewGrid.AllowUserToDeleteRows = false;
            reviewGrid.ReadOnly = true;
            reviewGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            reviewGrid.MultiSelect = true;
            reviewGrid.RowHeadersVisible = false;
            reviewGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            reviewGrid.DataSource = items;
            QtoUiTheme.ApplyGrid(reviewGrid);
            reviewGrid.SelectionChanged += ReviewGridSelectionChanged;
            reviewGrid.CellDoubleClick += ReviewGridCellDoubleClick;
            AddColumn("SeverityDisplay", "嚴重性", 70);
            AddColumn("CategoryDisplay", "問題類型", 100);
            AddColumn("UserMessageDisplay", "問題說明", 260);
            AddColumn("SuggestedActionDisplay", "建議處理", 180);
            AddColumn("BlockName", "圖塊名稱", 120);
            AddLocationColumn();
            AddAutoRepairColumn();
            root.Controls.Add(reviewGrid, 0, 1);

            TableLayoutPanel detailPanel = new TableLayoutPanel();
            detailPanel.Dock = DockStyle.Fill;
            detailPanel.ColumnCount = 1;
            detailPanel.RowCount = 2;
            detailPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            detailPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 0));
            summaryTextBox = new TextBox();
            summaryTextBox.Dock = DockStyle.Fill;
            summaryTextBox.Multiline = true;
            summaryTextBox.ReadOnly = true;
            summaryTextBox.ScrollBars = ScrollBars.Vertical;
            QtoUiTheme.ApplyReadOnlyTextBox(summaryTextBox);
            detailPanel.Controls.Add(summaryTextBox, 0, 0);
            technicalTextBox = new TextBox();
            technicalTextBox.Dock = DockStyle.Fill;
            technicalTextBox.Multiline = true;
            technicalTextBox.ReadOnly = true;
            technicalTextBox.ScrollBars = ScrollBars.Vertical;
            technicalTextBox.Visible = false;
            QtoUiTheme.ApplyReadOnlyTextBox(technicalTextBox);
            detailPanel.Controls.Add(technicalTextBox, 0, 1);
            root.Controls.Add(WrapGroup("問題摘要", detailPanel), 0, 2);

            FlowLayoutPanel actions = new FlowLayoutPanel();
            actions.Dock = DockStyle.Fill;
            actions.FlowDirection = FlowDirection.RightToLeft;
            actions.WrapContents = false;
            actions.Controls.Add(CreateButton("關閉", CloseButtonClick));
            technicalButton = CreateButton("查看技術資訊", TechnicalButtonClick);
            actions.Controls.Add(technicalButton);
            actions.Controls.Add(CreateButton("忽略選取", IgnoreSelectedButtonClick));
            actions.Controls.Add(CreateButton("標記選取已處理", MarkSelectedHandledButtonClick));
            actions.Controls.Add(CreateButton("修復選取項目", AutoRepairButtonClick));
            actions.Controls.Add(CreateButton("開啟屬性", StubButtonClick));
            actions.Controls.Add(CreateButton("定位物件", LocateButtonClick));
            root.Controls.Add(actions, 0, 3);

            if (reviewGrid.Rows.Count > 0)
            {
                reviewGrid.Rows[0].Selected = true;
                UpdateDetail();
            }
        }

        private Control CreateFilterPanel()
        {
            TableLayoutPanel panel = new TableLayoutPanel();
            panel.Dock = DockStyle.Fill;
            panel.ColumnCount = 6;
            panel.RowCount = 1;
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 64));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 64));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 64));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34));

            panel.Controls.Add(CreateLabel("嚴重性"), 0, 0);
            panel.Controls.Add(CreateCombo(new string[] { "全部", "錯誤", "警告", "提醒" }), 1, 0);
            panel.Controls.Add(CreateLabel("類型"), 2, 0);
            panel.Controls.Add(CreateCombo(new string[] { "全部", "缺資料", "圖塊問題", "ID 問題", "Excel 問題" }), 3, 0);
            panel.Controls.Add(CreateLabel("狀態"), 4, 0);
            panel.Controls.Add(CreateCombo(new string[] { "未處理", "已處理", "忽略", "全部" }), 5, 0);
            return WrapGroup("篩選", panel);
        }

        private void AddColumn(string propertyName, string title, int fillWeight)
        {
            DataGridViewTextBoxColumn column = new DataGridViewTextBoxColumn();
            column.DataPropertyName = propertyName;
            column.HeaderText = title;
            column.FillWeight = fillWeight;
            reviewGrid.Columns.Add(column);
        }

        private void AddLocationColumn()
        {
            DataGridViewTextBoxColumn column = new DataGridViewTextBoxColumn();
            column.HeaderText = "物件定位資訊";
            column.FillWeight = 160;
            reviewGrid.Columns.Add(column);
            reviewGrid.CellFormatting += ReviewGridCellFormatting;
        }

        private void AddAutoRepairColumn()
        {
            DataGridViewTextBoxColumn column = new DataGridViewTextBoxColumn();
            column.HeaderText = "自動修復";
            column.FillWeight = 90;
            reviewGrid.Columns.Add(column);
        }

        private static Label CreateLabel(string text)
        {
            Label label = new Label();
            label.Text = text;
            label.Dock = DockStyle.Fill;
            label.TextAlign = ContentAlignment.MiddleLeft;
            label.ForeColor = QtoUiTheme.TextColor;
            return label;
        }

        private static ComboBox CreateCombo(string[] items)
        {
            ComboBox comboBox = new ComboBox();
            comboBox.Dock = DockStyle.Fill;
            comboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBox.Items.AddRange(items);
            comboBox.SelectedIndex = 0;
            comboBox.FlatStyle = FlatStyle.System;
            return comboBox;
        }

        private static GroupBox WrapGroup(string title, Control content)
        {
            return QtoUiTheme.WrapGroup(title, content);
        }

        private static Button CreateButton(string text, EventHandler handler)
        {
            QtoButtonRole role = text == "修復選取項目" ? QtoButtonRole.Primary : QtoButtonRole.Default;
            Button button = QtoUiTheme.CreateButton(text, handler, role);
            button.Width = 132;
            button.Height = 30;
            button.Margin = new Padding(4, 8, 4, 4);
            return button;
        }

        private QtoReviewItem GetSelectedItem()
        {
            if (reviewGrid.CurrentRow == null)
            {
                return null;
            }

            return reviewGrid.CurrentRow.DataBoundItem as QtoReviewItem;
        }

        private List<QtoReviewItem> GetSelectedItems()
        {
            List<QtoReviewItem> selected = new List<QtoReviewItem>();
            foreach (DataGridViewRow row in reviewGrid.SelectedRows)
            {
                QtoReviewItem item = row.DataBoundItem as QtoReviewItem;
                if (item != null && !selected.Contains(item))
                {
                    selected.Add(item);
                }
            }

            if (selected.Count == 0)
            {
                QtoReviewItem current = GetSelectedItem();
                if (current != null)
                {
                    selected.Add(current);
                }
            }

            return selected;
        }

        private void ReviewGridSelectionChanged(object sender, EventArgs e)
        {
            UpdateDetail();
        }

        private void ReviewGridCellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            LocateButtonClick(sender, EventArgs.Empty);
        }

        private void ReviewGridCellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= items.Count)
            {
                return;
            }

            if (reviewGrid.Columns[e.ColumnIndex].HeaderText == "物件定位資訊")
            {
                e.Value = FormatLocation(items[e.RowIndex]);
                e.FormattingApplied = true;
            }
            else if (reviewGrid.Columns[e.ColumnIndex].HeaderText == "自動修復")
            {
                e.Value = items[e.RowIndex].CanAutoRepair ? "可自動修復" : "需人工確認";
                e.FormattingApplied = true;
            }
        }

        private void UpdateDetail()
        {
            List<QtoReviewItem> selected = GetSelectedItems();
            if (selected.Count == 0)
            {
                summaryTextBox.Text = string.Empty;
                technicalTextBox.Text = string.Empty;
                return;
            }

            if (selected.Count > 1)
            {
                int autoCount = 0;
                int manualCount = 0;
                foreach (QtoReviewItem selectedItem in selected)
                {
                    if (selectedItem.CanAutoRepair)
                    {
                        autoCount++;
                    }
                    else
                    {
                        manualCount++;
                    }
                }

                summaryTextBox.Text =
                    "已選取：" + selected.Count.ToString("0") + " 項" + Environment.NewLine +
                    "可自動修復：" + autoCount.ToString("0") + " 項" + Environment.NewLine +
                    "需人工確認：" + manualCount.ToString("0") + " 項" + Environment.NewLine +
                    "建議：可先按「修復選取項目」查看批次修復內容；需人工確認的項目不會直接寫入 CAD。";
                technicalTextBox.Text = "多選狀態下預設不展開技術細節。請改選單一項目查看原始檢查資訊。";
                return;
            }

            QtoReviewItem item = selected[0];
            summaryTextBox.Text =
                "問題：" + item.UserMessageDisplay + Environment.NewLine +
                "建議：" + item.SuggestedActionDisplay + Environment.NewLine +
                "位置：" + FormatLocation(item) + Environment.NewLine +
                "自動修復：" + (item.CanAutoRepair ? "可自動修復" : "需人工確認");
            technicalTextBox.Text =
                "問題項目：" + item.IssueTypeDisplay + Environment.NewLine +
                "原始類型：" + (item.IssueType ?? string.Empty) + Environment.NewLine +
                "技術細節：" + Environment.NewLine +
                (item.TechnicalDetail ?? string.Empty);
        }

        private static string FormatLocation(QtoReviewItem item)
        {
            if (item == null)
            {
                return string.Empty;
            }

            string floor = string.IsNullOrWhiteSpace(item.Floor) ? "未填樓層" : item.Floor;
            string area = string.IsNullOrWhiteSpace(item.Area) ? "未填區域" : item.Area;
            string handle = string.IsNullOrWhiteSpace(item.ObjectHandle) ? "無 Handle" : "Handle " + item.ObjectHandle;
            return floor + " / " + area + " / " + handle;
        }

        private void LocateButtonClick(object sender, EventArgs e)
        {
            QtoReviewItem item = GetSelectedItem();
            string location = item == null ? "未選取問題。" : FormatLocation(item);
            MessageBox.Show(this, "目前選取位置：" + location + Environment.NewLine + "請依此資訊回到圖面確認物件。", "定位物件", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void AutoRepairButtonClick(object sender, EventArgs e)
        {
            List<QtoReviewItem> selected = GetSelectedItems();
            if (selected.Count == 0)
            {
                MessageBox.Show(this, "請先選取要處理的問題。", "修復選取項目", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            int autoCount = 0;
            foreach (QtoReviewItem item in selected)
            {
                if (item.CanAutoRepair)
                {
                    autoCount++;
                }
            }

            if (autoCount > 0)
            {
                QtoDialogService.ShowRepair(BuildRepairPlanItems(selected), this);

                if (autoCount < selected.Count)
                {
                    MessageBox.Show(this, "選取項目中有 " + (selected.Count - autoCount).ToString("0") + " 項需要人工確認，系統不會直接寫入 CAD。", "修復選取項目", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return;
            }

            MessageBox.Show(this, "選取項目都需要工程人員確認，不能直接自動修復。可先標記已處理或忽略，或回到屬性/圖塊資料庫補資料。", "修復選取項目", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void TechnicalButtonClick(object sender, EventArgs e)
        {
            technicalVisible = !technicalVisible;
            technicalTextBox.Visible = technicalVisible;
            technicalButton.Text = technicalVisible ? "隱藏技術資訊" : "查看技術資訊";
            TableLayoutPanel panel = technicalTextBox.Parent as TableLayoutPanel;
            if (panel != null)
            {
                panel.RowStyles[1].Height = technicalVisible ? 88 : 0;
                panel.RowStyles[1].SizeType = SizeType.Absolute;
            }
        }

        private void StubButtonClick(object sender, EventArgs e)
        {
            MessageBox.Show(this, "這個批次動作不會直接修改圖面。請先用「定位物件」回到 CAD 確認，或用「標記選取已處理」整理清單。", "QTO Review", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void MarkSelectedHandledButtonClick(object sender, EventArgs e)
        {
            MarkSelectedStatus("已處理", "已將選取項目標記為已處理。");
        }

        private void IgnoreSelectedButtonClick(object sender, EventArgs e)
        {
            MarkSelectedStatus("忽略", "已將選取項目標記為忽略。");
        }

        private void MarkSelectedStatus(string status, string message)
        {
            List<QtoReviewItem> selected = GetSelectedItems();
            if (selected.Count == 0)
            {
                MessageBox.Show(this, "請先選取要處理的問題。", "QTO Review", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            foreach (QtoReviewItem item in selected)
            {
                item.Status = status;
            }

            reviewGrid.Refresh();
            UpdateDetail();
            MessageBox.Show(this, message + Environment.NewLine + "筆數：" + selected.Count.ToString("0"), "QTO Review", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private static IList<QtoRepairPlanItem> BuildRepairPlanItems(IList<QtoReviewItem> selected)
        {
            Dictionary<string, QtoRepairPlanItem> plansByIssueType = new Dictionary<string, QtoRepairPlanItem>(StringComparer.OrdinalIgnoreCase);
            foreach (QtoReviewItem item in selected)
            {
                if (item == null)
                {
                    continue;
                }

                string issueType = string.IsNullOrWhiteSpace(item.IssueType) ? item.CategoryDisplay : item.IssueType;
                QtoRepairPlanItem plan;
                if (!plansByIssueType.TryGetValue(issueType, out plan))
                {
                    plan = new QtoRepairPlanItem();
                    plan.IssueType = issueType;
                    plan.Count = 0;
                    plan.CanAutoRepair = item.CanAutoRepair;
                    plan.RepairAction = item.CanAutoRepair
                        ? QtoReviewService.ResolveSuggestedAction(item.IssueType, item.SuggestedAction)
                        : "需要工程人員確認；系統不會直接寫入 CAD。";
                    plansByIssueType[issueType] = plan;
                }

                plan.Count++;
                plan.CanAutoRepair = plan.CanAutoRepair && item.CanAutoRepair;
            }

            return new List<QtoRepairPlanItem>(plansByIssueType.Values);
        }

        private void CloseButtonClick(object sender, EventArgs e)
        {
            Close();
        }
    }
}
