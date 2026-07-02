using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace QtoWirePlugin
{
    public class QtoRepairForm : Form
    {
        private readonly BindingList<QtoRepairPlanItem> autoItems;
        private readonly BindingList<QtoRepairPlanItem> manualItems;
        private readonly DataGridView autoGrid;
        private readonly DataGridView manualGrid;
        private readonly TextBox detailTextBox;

        public QtoRepairForm()
            : this(QtoUiSampleData.CreateRepairItems())
        {
        }

        public QtoRepairForm(IEnumerable<QtoRepairPlanItem> repairItems)
        {
            Text = "QTO 自動修復";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(780, 540);
            MinimumSize = new Size(720, 500);
            QtoUiTheme.ApplyForm(this);

            List<QtoRepairPlanItem> automatic = new List<QtoRepairPlanItem>();
            List<QtoRepairPlanItem> manual = new List<QtoRepairPlanItem>();
            foreach (QtoRepairPlanItem item in repairItems ?? QtoUiSampleData.CreateRepairItems())
            {
                if (item.CanAutoRepair)
                {
                    automatic.Add(item);
                }
                else
                {
                    manual.Add(item);
                }
            }

            autoItems = new BindingList<QtoRepairPlanItem>(automatic);
            manualItems = new BindingList<QtoRepairPlanItem>(manual);

            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.Padding = new Padding(12);
            root.ColumnCount = 1;
            root.RowCount = 4;
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 44));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 34));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 80));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            Controls.Add(root);

            autoGrid = CreateGrid(autoItems);
            manualGrid = CreateGrid(manualItems);
            root.Controls.Add(WrapGroup("可自動修復", autoGrid), 0, 0);
            root.Controls.Add(WrapGroup("需要人工確認", manualGrid), 0, 1);

            detailTextBox = new TextBox();
            detailTextBox.Dock = DockStyle.Fill;
            detailTextBox.Multiline = true;
            detailTextBox.ReadOnly = true;
            detailTextBox.Text = "可自動修復項目代表系統可補上或重配同步 ID。需要人工確認的項目不會直接寫入 CAD。";
            QtoUiTheme.ApplyReadOnlyTextBox(detailTextBox);
            root.Controls.Add(WrapGroup("修復說明", detailTextBox), 0, 2);

            FlowLayoutPanel actions = new FlowLayoutPanel();
            actions.Dock = DockStyle.Fill;
            actions.FlowDirection = FlowDirection.RightToLeft;
            actions.WrapContents = false;
            actions.Controls.Add(CreateButton("取消", CancelButtonClick));
            actions.Controls.Add(CreateButton("查看詳細資訊", DetailButtonClick));
            actions.Controls.Add(CreateButton("確認選取修復", RepairSelectedButtonClick));
            actions.Controls.Add(CreateButton("確認全部可修復", RepairAllButtonClick));
            root.Controls.Add(actions, 0, 3);
        }

        private static DataGridView CreateGrid(object dataSource)
        {
            DataGridView grid = new DataGridView();
            grid.Dock = DockStyle.Fill;
            grid.AutoGenerateColumns = false;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.ReadOnly = true;
            grid.RowHeadersVisible = false;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.MultiSelect = true;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            QtoUiTheme.ApplyGrid(grid);
            grid.Columns.Add(CreateColumn("IssueType", "問題類型", 120));
            grid.Columns.Add(CreateColumn("CountText", "數量", 50));
            grid.Columns.Add(CreateColumn("RepairAction", "系統會做什麼", 260));
            grid.CellFormatting += RepairGridCellFormatting;
            grid.DataSource = dataSource;
            return grid;
        }

        private static DataGridViewTextBoxColumn CreateColumn(string propertyName, string title, int fillWeight)
        {
            DataGridViewTextBoxColumn column = new DataGridViewTextBoxColumn();
            column.DataPropertyName = propertyName;
            column.HeaderText = title;
            column.FillWeight = fillWeight;
            return column;
        }

        private static GroupBox WrapGroup(string title, Control content)
        {
            return QtoUiTheme.WrapGroup(title, content);
        }

        private static Button CreateButton(string text, EventHandler handler)
        {
            QtoButtonRole role = text == "確認全部可修復" ? QtoButtonRole.Primary : QtoButtonRole.Default;
            Button button = QtoUiTheme.CreateButton(text, handler, role);
            button.Width = 128;
            button.Height = 30;
            button.Margin = new Padding(4, 8, 4, 4);
            return button;
        }

        private void RepairAllButtonClick(object sender, EventArgs e)
        {
            int autoCount = CountPlanRows(autoItems);
            int manualCount = CountPlanRows(manualItems);
            MessageBox.Show(this,
                "可由修復服務寫入 CAD 的項目：" + autoCount.ToString("0") + " 項" + Environment.NewLine +
                "只做人工確認、不直接寫 CAD 的項目：" + manualCount.ToString("0") + " 項" + Environment.NewLine +
                "本視窗只確認批次內容；實際寫入由自動修復流程執行。",
                "確認全部可修復",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private void RepairSelectedButtonClick(object sender, EventArgs e)
        {
            List<QtoRepairPlanItem> selected = GetSelectedPlanItems();
            if (selected.Count == 0)
            {
                MessageBox.Show(this, "請先選取要確認的修復項目，可按住 Ctrl 或 Shift 多選。", "確認選取修復", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            int autoCount = 0;
            int manualCount = 0;
            foreach (QtoRepairPlanItem item in selected)
            {
                if (item.CanAutoRepair)
                {
                    autoCount += item.Count;
                }
                else
                {
                    manualCount += item.Count;
                }
            }

            MessageBox.Show(this,
                "已選取 " + selected.Count.ToString("0") + " 類問題。" + Environment.NewLine +
                "可由修復服務寫入 CAD：" + autoCount.ToString("0") + " 項" + Environment.NewLine +
                "需要人工確認、不直接寫 CAD：" + manualCount.ToString("0") + " 項" + Environment.NewLine +
                "目前 Review 視窗送來的批次只做彙整確認；若要實際寫入，請由主控面板執行修復。",
                "確認選取修復",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private void DetailButtonClick(object sender, EventArgs e)
        {
            detailTextBox.Text =
                "可自動修復：" + autoItems.Count.ToString("0") + " 類" + Environment.NewLine +
                "需要人工確認：" + manualItems.Count.ToString("0") + " 類" + Environment.NewLine +
                "會寫入 CAD：缺少或重複同步 ID，由自動修復流程處理。" + Environment.NewLine +
                "不直接寫 CAD：分類、圖塊資料庫、數量依據與其他工程判斷項目。";
        }

        private static void RepairGridCellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            DataGridView grid = sender as DataGridView;
            if (grid == null || e.RowIndex < 0 || e.ColumnIndex < 0)
            {
                return;
            }

            DataGridViewColumn column = grid.Columns[e.ColumnIndex];
            if (column != null && string.Equals(column.DataPropertyName, "IssueType", StringComparison.OrdinalIgnoreCase))
            {
                e.Value = QtoReviewService.ResolveIssueTypeDisplay(e.Value == null ? string.Empty : e.Value.ToString());
                e.FormattingApplied = true;
            }
        }

        private List<QtoRepairPlanItem> GetSelectedPlanItems()
        {
            List<QtoRepairPlanItem> selected = new List<QtoRepairPlanItem>();
            AddSelectedPlanItems(autoGrid, selected);
            AddSelectedPlanItems(manualGrid, selected);
            return selected;
        }

        private static void AddSelectedPlanItems(DataGridView grid, IList<QtoRepairPlanItem> selected)
        {
            if (grid == null)
            {
                return;
            }

            foreach (DataGridViewRow row in grid.SelectedRows)
            {
                QtoRepairPlanItem item = row.DataBoundItem as QtoRepairPlanItem;
                if (item != null && !selected.Contains(item))
                {
                    selected.Add(item);
                }
            }
        }

        private static int CountPlanRows(IEnumerable<QtoRepairPlanItem> planItems)
        {
            int count = 0;
            foreach (QtoRepairPlanItem item in planItems)
            {
                if (item != null)
                {
                    count += item.Count;
                }
            }

            return count;
        }

        private void CancelButtonClick(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }
    }
}
