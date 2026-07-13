using System;
using System.Drawing;
using System.Windows.Forms;

namespace QtoWirePlugin
{
    public class QtoSettingsForm : Form
    {
        public QtoSettingsForm()
        {
            Text = "QTO 同步設定";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(660, 500);
            MinimumSize = new Size(620, 460);
            QtoUiTheme.ApplyForm(this);

            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.Padding = QtoUiTheme.FormPadding;
            root.ColumnCount = 1;
            root.RowCount = 2;
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
            Controls.Add(root);

            TabControl tabs = new TabControl();
            tabs.Dock = DockStyle.Fill;
            tabs.TabPages.Add(CreateSyncPage());
            tabs.TabPages.Add(CreateExcelPage());
            tabs.TabPages.Add(CreateCatalogPage());
            tabs.TabPages.Add(CreateReviewPage());
            root.Controls.Add(tabs, 0, 0);

            FlowLayoutPanel actions = new FlowLayoutPanel();
            actions.Dock = DockStyle.Fill;
            actions.FlowDirection = FlowDirection.RightToLeft;
            actions.WrapContents = false;
            actions.Controls.Add(CreateButton("取消", CancelButtonClick));
            actions.Controls.Add(CreateButton("套用設定", SaveButtonClick));
            root.Controls.Add(actions, 0, 1);
        }

        private static TabPage CreateSyncPage()
        {
            TabPage page = new TabPage("同步");
            TableLayoutPanel panel = CreateSettingsPanel();
            panel.Controls.Add(CreateCheckBox("自動同步 CAD 到 Excel", true), 0, 0);
            panel.Controls.Add(CreateCheckBox("CAD 命令結束後再同步", true), 0, 1);
            panel.Controls.Add(CreateNumberRow("同步延遲", "毫秒", 1000, 0, 10000), 0, 2);
            panel.Controls.Add(CreateCheckBox("同步前先檢查問題", true), 0, 3);
            page.Controls.Add(panel);
            return page;
        }

        private static TabPage CreateExcelPage()
        {
            TabPage page = new TabPage("Excel");
            TableLayoutPanel panel = CreateSettingsPanel();
            panel.Controls.Add(CreateCheckBox("啟動時開啟上次使用的 Excel", false), 0, 0);
            panel.Controls.Add(CreateCheckBox("保留系統同步工作表", true), 0, 1);
            panel.Controls.Add(CreateCheckBox("允許回寫欄位設定", false), 0, 2);
            panel.Controls.Add(CreateInfoLabel("未指定檔案時，全圖重建會自動建立同步 Excel。"), 0, 3);
            page.Controls.Add(panel);
            return page;
        }

        private static TabPage CreateCatalogPage()
        {
            TabPage page = new TabPage("圖塊資料庫");
            TableLayoutPanel panel = CreateSettingsPanel();
            panel.Controls.Add(CreatePathRow("Catalog 路徑"), 0, 0);
            panel.Controls.Add(CreatePathRow("圖例 DWG"), 0, 1);
            panel.Controls.Add(CreateCheckBox("載入時檢查目前專案圖塊", true), 0, 2);
            panel.Controls.Add(CreateCheckBox("只提示未設定圖塊", true), 0, 3);
            page.Controls.Add(panel);
            return page;
        }

        private static TabPage CreateReviewPage()
        {
            TabPage page = new TabPage("檢查修復");
            TableLayoutPanel panel = CreateSettingsPanel();
            panel.Controls.Add(CreateCheckBox("缺資料時進入 Review", true), 0, 0);
            panel.Controls.Add(CreateCheckBox("重複 QTO_SYNC_ID 時列入可修復項目", true), 0, 1);
            panel.Controls.Add(CreateCheckBox("修復後寫入同步紀錄", true), 0, 2);
            panel.Controls.Add(CreateInfoLabel("需要工程判斷的問題不會自動修復。"), 0, 3);
            page.Controls.Add(panel);
            return page;
        }

        private static TableLayoutPanel CreateSettingsPanel()
        {
            TableLayoutPanel panel = new TableLayoutPanel();
            panel.Dock = DockStyle.Fill;
            panel.Padding = new Padding(14);
            panel.BackColor = QtoUiTheme.PanelBackColor;
            panel.ColumnCount = 1;
            panel.RowCount = 6;
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (int i = 0; i < panel.RowCount; i++)
            {
                panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            }

            return panel;
        }

        private static CheckBox CreateCheckBox(string text, bool isChecked)
        {
            CheckBox checkBox = new CheckBox();
            checkBox.Text = text;
            checkBox.Checked = isChecked;
            checkBox.Dock = DockStyle.Fill;
            checkBox.TextAlign = ContentAlignment.MiddleLeft;
            checkBox.ForeColor = QtoUiTheme.TextColor;
            return checkBox;
        }

        private static Control CreateNumberRow(string labelText, string unitText, int value, int min, int max)
        {
            TableLayoutPanel panel = new TableLayoutPanel();
            panel.Dock = DockStyle.Fill;
            panel.ColumnCount = 3;
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            Label label = new Label();
            label.Text = labelText;
            label.Dock = DockStyle.Fill;
            label.TextAlign = ContentAlignment.MiddleLeft;
            label.ForeColor = QtoUiTheme.TextColor;
            panel.Controls.Add(label, 0, 0);

            NumericUpDown number = new NumericUpDown();
            number.Minimum = min;
            number.Maximum = max;
            number.Value = value;
            number.Dock = DockStyle.Fill;
            number.BorderStyle = BorderStyle.FixedSingle;
            panel.Controls.Add(number, 1, 0);

            Label unit = new Label();
            unit.Text = unitText;
            unit.Dock = DockStyle.Fill;
            unit.TextAlign = ContentAlignment.MiddleLeft;
            unit.ForeColor = QtoUiTheme.MutedTextColor;
            panel.Controls.Add(unit, 2, 0);
            return panel;
        }

        private static Control CreatePathRow(string labelText)
        {
            TableLayoutPanel panel = new TableLayoutPanel();
            panel.Dock = DockStyle.Fill;
            panel.ColumnCount = 3;
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));

            Label label = new Label();
            label.Text = labelText;
            label.Dock = DockStyle.Fill;
            label.TextAlign = ContentAlignment.MiddleLeft;
            label.ForeColor = QtoUiTheme.TextColor;
            panel.Controls.Add(label, 0, 0);

            TextBox textBox = new TextBox();
            textBox.Dock = DockStyle.Fill;
            textBox.ReadOnly = true;
            textBox.Text = "尚未選擇";
            QtoUiTheme.ApplyReadOnlyTextBox(textBox);
            panel.Controls.Add(textBox, 1, 0);

            Button button = QtoUiTheme.CreateButton("選擇", null, QtoButtonRole.Secondary);
            button.Dock = DockStyle.Fill;
            panel.Controls.Add(button, 2, 0);
            return panel;
        }

        private static Label CreateInfoLabel(string text)
        {
            Label label = new Label();
            label.Text = text;
            label.Dock = DockStyle.Fill;
            label.TextAlign = ContentAlignment.MiddleLeft;
            label.ForeColor = QtoUiTheme.MutedTextColor;
            return label;
        }

        private static Button CreateButton(string text, EventHandler handler)
        {
            Button button = QtoUiTheme.CreateButton(text, handler, text == "套用設定" ? QtoButtonRole.Primary : QtoButtonRole.Default);
            button.Width = 96;
            button.Height = 30;
            button.Margin = new Padding(4, 8, 4, 4);
            return button;
        }

        private void SaveButtonClick(object sender, EventArgs e)
        {
            MessageBox.Show(this, "目前設定只套用於本次視窗操作，尚未寫入設定檔。", "QTO 同步設定", MessageBoxButtons.OK, MessageBoxIcon.Information);
            DialogResult = DialogResult.OK;
            Close();
        }

        private void CancelButtonClick(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }
    }
}
