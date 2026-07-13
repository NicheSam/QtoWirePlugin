using System;
using System.Drawing;
using System.Windows.Forms;

namespace QtoWirePlugin
{
    internal sealed class QtoBudgetLayoutSettingsForm : Form
    {
        private readonly CheckedListBox systemList;
        private readonly CheckedListBox categoryList;
        private readonly CheckedListBox columnList;
        private readonly CheckBox hideZeroCheckBox;
        private readonly CheckBox reviewLastCheckBox;

        public QtoBudgetLayoutSettingsForm(QtoBudgetLayoutSettings settings)
        {
            Text = "預算表設定";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(820, 610);
            MinimumSize = new Size(700, 520);
            AutoScaleMode = AutoScaleMode.Dpi;
            QtoUiTheme.ApplyForm(this);

            QtoBudgetLayoutSettings source = settings ?? QtoBudgetLayoutSettings.CreateDefault();

            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.Padding = QtoUiTheme.FormPadding;
            root.ColumnCount = 1;
            root.RowCount = 4;
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            Controls.Add(root);

            TableLayoutPanel header = new TableLayoutPanel();
            header.Dock = DockStyle.Fill;
            header.RowCount = 2;
            header.ColumnCount = 1;
            header.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            header.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            Label title = QtoUiTheme.CreateLabel("控制預算草稿的排列與顯示", ContentAlignment.MiddleLeft);
            title.Font = QtoUiTheme.HeaderFont;
            header.Controls.Add(title, 0, 0);
            header.Controls.Add(QtoUiTheme.CreateMutedLabel("取消勾選只會隱藏預算草稿內容，CAD 原始資料與數量統整仍完整保留。"), 0, 1);
            root.Controls.Add(header, 0, 0);

            TabControl tabs = new TabControl();
            tabs.Dock = DockStyle.Fill;
            tabs.TabPages.Add(CreateOrderPage(source, out systemList, out categoryList));
            tabs.TabPages.Add(CreateColumnPage(source, out columnList));
            root.Controls.Add(tabs, 0, 1);

            FlowLayoutPanel options = new FlowLayoutPanel();
            options.Dock = DockStyle.Fill;
            options.FlowDirection = FlowDirection.LeftToRight;
            options.WrapContents = false;
            options.Padding = new Padding(6, 10, 6, 4);
            hideZeroCheckBox = CreateCheckBox("隱藏零數量項目", source.HideZeroQuantity);
            reviewLastCheckBox = CreateCheckBox("待確認項目排在分類最後", source.ReviewRowsLast);
            options.Controls.Add(hideZeroCheckBox);
            options.Controls.Add(reviewLastCheckBox);
            root.Controls.Add(options, 0, 2);

            TableLayoutPanel actions = new TableLayoutPanel();
            actions.Dock = DockStyle.Fill;
            actions.ColumnCount = 2;
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            Button resetButton = QtoUiTheme.CreateButton("恢復預設", ResetButtonClick, QtoButtonRole.Secondary);
            resetButton.Width = 100;
            actions.Controls.Add(resetButton, 0, 0);

            FlowLayoutPanel rightActions = new FlowLayoutPanel();
            rightActions.AutoSize = true;
            rightActions.FlowDirection = FlowDirection.LeftToRight;
            rightActions.WrapContents = false;
            Button cancelButton = QtoUiTheme.CreateButton("取消", CancelButtonClick, QtoButtonRole.Default);
            cancelButton.Width = 96;
            Button saveButton = QtoUiTheme.CreateButton("套用並更新", SaveButtonClick, QtoButtonRole.Primary);
            saveButton.Width = 120;
            rightActions.Controls.Add(cancelButton);
            rightActions.Controls.Add(saveButton);
            actions.Controls.Add(rightActions, 1, 0);
            root.Controls.Add(actions, 0, 3);
        }

        public QtoBudgetLayoutSettings ResultSettings { get; private set; }

        private static TabPage CreateOrderPage(
            QtoBudgetLayoutSettings settings,
            out CheckedListBox systems,
            out CheckedListBox categories)
        {
            TabPage page = new TabPage("系統與分類");
            TableLayoutPanel panel = new TableLayoutPanel();
            panel.Dock = DockStyle.Fill;
            panel.Padding = new Padding(10);
            panel.ColumnCount = 2;
            panel.RowCount = 1;
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

            systems = CreateCheckedList();
            foreach (string system in settings.SystemOrder)
            {
                systems.Items.Add(system, settings.IsSystemVisible(system));
            }

            categories = CreateCheckedList();
            foreach (string category in settings.CategoryOrder)
            {
                categories.Items.Add(category, settings.IsCategoryVisible(category));
            }

            panel.Controls.Add(CreateOrderGroup("系統順序", systems), 0, 0);
            panel.Controls.Add(CreateOrderGroup("工程分類順序", categories), 1, 0);
            page.Controls.Add(panel);
            return page;
        }

        private static TabPage CreateColumnPage(QtoBudgetLayoutSettings settings, out CheckedListBox columns)
        {
            TabPage page = new TabPage("顯示欄位");
            TableLayoutPanel panel = new TableLayoutPanel();
            panel.Dock = DockStyle.Fill;
            panel.Padding = new Padding(14);
            panel.RowCount = 2;
            panel.ColumnCount = 1;
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            panel.Controls.Add(QtoUiTheme.CreateMutedLabel("只勾選日常整理預算時需要看的欄位；技術欄位由系統自動隱藏。"), 0, 0);

            columns = CreateCheckedList();
            columns.MultiColumn = true;
            columns.ColumnWidth = 180;
            foreach (string column in QtoBudgetLayoutSettings.GetAvailableColumns())
            {
                columns.Items.Add(column, settings.IsColumnVisible(column));
            }
            panel.Controls.Add(columns, 0, 1);
            page.Controls.Add(panel);
            return page;
        }

        private static GroupBox CreateOrderGroup(string title, CheckedListBox list)
        {
            GroupBox group = new GroupBox();
            group.Text = title;
            group.Dock = DockStyle.Fill;
            group.Padding = QtoUiTheme.GroupPadding;

            TableLayoutPanel panel = new TableLayoutPanel();
            panel.Dock = DockStyle.Fill;
            panel.ColumnCount = 1;
            panel.RowCount = 2;
            panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            panel.Controls.Add(list, 0, 0);

            FlowLayoutPanel actions = new FlowLayoutPanel();
            actions.Dock = DockStyle.Fill;
            actions.FlowDirection = FlowDirection.LeftToRight;
            actions.WrapContents = false;
            Button up = QtoUiTheme.CreateButton("上移", delegate { MoveSelected(list, -1); }, QtoButtonRole.Secondary);
            Button down = QtoUiTheme.CreateButton("下移", delegate { MoveSelected(list, 1); }, QtoButtonRole.Secondary);
            up.Width = 82;
            down.Width = 82;
            actions.Controls.Add(up);
            actions.Controls.Add(down);
            panel.Controls.Add(actions, 0, 1);
            group.Controls.Add(panel);
            return group;
        }

        private static CheckedListBox CreateCheckedList()
        {
            CheckedListBox list = new CheckedListBox();
            list.Dock = DockStyle.Fill;
            list.CheckOnClick = true;
            list.IntegralHeight = false;
            list.BorderStyle = BorderStyle.FixedSingle;
            list.BackColor = Color.White;
            list.ForeColor = QtoUiTheme.TextColor;
            return list;
        }

        private static CheckBox CreateCheckBox(string text, bool isChecked)
        {
            CheckBox checkBox = new CheckBox();
            checkBox.Text = text;
            checkBox.Checked = isChecked;
            checkBox.AutoSize = true;
            checkBox.Margin = new Padding(8, 4, 22, 4);
            return checkBox;
        }

        private static void MoveSelected(CheckedListBox list, int delta)
        {
            int index = list.SelectedIndex;
            int newIndex = index + delta;
            if (index < 0 || newIndex < 0 || newIndex >= list.Items.Count)
            {
                return;
            }

            object item = list.Items[index];
            bool isChecked = list.GetItemChecked(index);
            list.Items.RemoveAt(index);
            list.Items.Insert(newIndex, item);
            list.SetItemChecked(newIndex, isChecked);
            list.SelectedIndex = newIndex;
        }

        private void ResetButtonClick(object sender, EventArgs e)
        {
            ApplySettingsToControls(QtoBudgetLayoutSettings.CreateDefault());
        }

        private void ApplySettingsToControls(QtoBudgetLayoutSettings settings)
        {
            systemList.Items.Clear();
            foreach (string system in settings.SystemOrder)
            {
                systemList.Items.Add(system, settings.IsSystemVisible(system));
            }

            categoryList.Items.Clear();
            foreach (string category in settings.CategoryOrder)
            {
                categoryList.Items.Add(category, settings.IsCategoryVisible(category));
            }

            columnList.Items.Clear();
            foreach (string column in QtoBudgetLayoutSettings.GetAvailableColumns())
            {
                columnList.Items.Add(column, settings.IsColumnVisible(column));
            }

            hideZeroCheckBox.Checked = settings.HideZeroQuantity;
            reviewLastCheckBox.Checked = settings.ReviewRowsLast;
        }

        private void SaveButtonClick(object sender, EventArgs e)
        {
            if (systemList.CheckedItems.Count == 0
                || categoryList.CheckedItems.Count == 0
                || columnList.CheckedItems.Count == 0)
            {
                MessageBox.Show(
                    this,
                    "系統、工程分類與顯示欄位都至少要保留一項。",
                    "預算表設定",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            QtoBudgetLayoutSettings settings = new QtoBudgetLayoutSettings();
            CopyList(systemList, settings.SystemOrder, settings.HiddenSystems);
            CopyList(categoryList, settings.CategoryOrder, settings.HiddenCategories);
            for (int i = 0; i < columnList.Items.Count; i++)
            {
                if (columnList.GetItemChecked(i))
                {
                    settings.VisibleColumns.Add(Convert.ToString(columnList.Items[i]));
                }
            }

            settings.HideZeroQuantity = hideZeroCheckBox.Checked;
            settings.ReviewRowsLast = reviewLastCheckBox.Checked;
            settings.Normalize();
            ResultSettings = settings;
            DialogResult = DialogResult.OK;
            Close();
        }

        private static void CopyList(CheckedListBox source, System.Collections.Generic.ICollection<string> order, System.Collections.Generic.ISet<string> hidden)
        {
            for (int i = 0; i < source.Items.Count; i++)
            {
                string value = Convert.ToString(source.Items[i]);
                order.Add(value);
                if (!source.GetItemChecked(i))
                {
                    hidden.Add(value);
                }
            }
        }

        private void CancelButtonClick(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }
    }
}
