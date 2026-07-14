using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace QtoWirePlugin
{
    public class QtoBudgetExportOptions
    {
        public bool IncludeObjectIdentity { get; set; }
        public bool IncludeCadQuantity { get; set; }
        public bool IncludeSystemEquipment { get; set; }
        public bool IncludeLocationRouting { get; set; }
        public bool IncludeReviewStatus { get; set; }

        public static QtoBudgetExportOptions CreateDefault()
        {
            return new QtoBudgetExportOptions
            {
                IncludeObjectIdentity = true,
                IncludeCadQuantity = true,
                IncludeSystemEquipment = true,
                IncludeLocationRouting = true,
                IncludeReviewStatus = true
            };
        }

        public string BuildContentDescription()
        {
            List<string> parts = new List<string>();
            if (IncludeObjectIdentity)
            {
                parts.Add("物件識別");
            }

            if (IncludeCadQuantity)
            {
                parts.Add("CAD計量");
            }

            if (IncludeSystemEquipment)
            {
                parts.Add("系統與設備");
            }

            if (IncludeLocationRouting)
            {
                parts.Add("位置與配線");
            }

            if (IncludeReviewStatus)
            {
                parts.Add("對應檢查");
            }

            return parts.Count == 0 ? "未選擇" : string.Join("、", parts.ToArray());
        }
    }

    public class QtoBudgetExportOptionsForm : Form
    {
        private readonly CheckBox objectIdentityCheckBox;
        private readonly CheckBox cadQuantityCheckBox;
        private readonly CheckBox systemEquipmentCheckBox;
        private readonly CheckBox locationRoutingCheckBox;
        private readonly CheckBox reviewStatusCheckBox;

        public QtoBudgetExportOptionsForm(int rowCount, int reviewCount)
        {
            Text = "選擇匯出資訊";
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Width = 680;
            Height = 460;
            MinimumSize = new Size(560, 400);
            QtoUiTheme.ApplyForm(this);

            TableLayoutPanel root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = QtoUiTheme.FormPadding, ColumnCount = 1, RowCount = 3 };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
            Panel header = new Panel { Dock = DockStyle.Fill };
            header.Controls.Add(new Label { Text = "匯出預算前置 CSV", Dock = DockStyle.Top, Height = 30, Font = QtoUiTheme.HeaderFont, ForeColor = QtoUiTheme.TextColor });
            header.Controls.Add(new Label { Text = "可匯出 " + rowCount + " 筆，其中 " + reviewCount + " 筆需人工確認。此檔用於預算檢查，不是正式報價表。", Dock = DockStyle.Bottom, Height = 32, ForeColor = QtoUiTheme.MutedTextColor });
            root.Controls.Add(header, 0, 0);

            FlowLayoutPanel options = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, FlowDirection = System.Windows.Forms.FlowDirection.TopDown, WrapContents = false, BackColor = QtoUiTheme.PanelBackColor, Padding = new Padding(12) };

            objectIdentityCheckBox = CreateCheckBox("物件識別", "來源 DWG、物件識別碼、圖塊名稱、圖層", 0, 0);
            cadQuantityCheckBox = CreateCheckBox("CAD 計量", "CAD 計量型態、QTO 編號、數量依據、數量、單位、長度", 0, 0);
            systemEquipmentCheckBox = CreateCheckBox("系統與設備", "系統代碼、設備類型代碼、設備類型名稱", 0, 0);
            locationRoutingCheckBox = CreateCheckBox("位置與配線", "樓層、區域、空間、線材、管線與線槽資料", 0, 0);
            reviewStatusCheckBox = CreateCheckBox("對應檢查", "對應狀態、待確認原因、資料來源規則", 0, 0);

            options.Controls.Add(objectIdentityCheckBox);
            options.Controls.Add(cadQuantityCheckBox);
            options.Controls.Add(systemEquipmentCheckBox);
            options.Controls.Add(locationRoutingCheckBox);
            options.Controls.Add(reviewStatusCheckBox);
            options.SizeChanged += delegate { foreach (Control control in options.Controls) control.Width = Math.Max(460, options.ClientSize.Width - 30); };
            root.Controls.Add(options, 0, 1);

            FlowLayoutPanel buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = System.Windows.Forms.FlowDirection.RightToLeft, WrapContents = false, Padding = new Padding(0, 8, 0, 0) };
            Button allButton = QtoUiTheme.CreateButton("全選", SelectAllButtonClick, QtoButtonRole.Secondary);
            Button clearButton = QtoUiTheme.CreateButton("全部取消", ClearButtonClick, QtoButtonRole.Secondary);
            Button okButton = QtoUiTheme.CreateButton("匯出 CSV", OkButtonClick, QtoButtonRole.Primary);
            Button cancelButton = QtoUiTheme.CreateButton("取消", CancelButtonClick, QtoButtonRole.Secondary);
            AcceptButton = okButton;
            CancelButton = cancelButton;
            buttons.Controls.Add(cancelButton);
            buttons.Controls.Add(okButton);
            buttons.Controls.Add(clearButton);
            buttons.Controls.Add(allButton);
            root.Controls.Add(buttons, 0, 2);
            Controls.Add(root);
        }

        public QtoBudgetExportOptions Options
        {
            get
            {
                return new QtoBudgetExportOptions
                {
                    IncludeObjectIdentity = objectIdentityCheckBox.Checked,
                    IncludeCadQuantity = cadQuantityCheckBox.Checked,
                    IncludeSystemEquipment = systemEquipmentCheckBox.Checked,
                    IncludeLocationRouting = locationRoutingCheckBox.Checked,
                    IncludeReviewStatus = reviewStatusCheckBox.Checked
                };
            }
        }

        private static CheckBox CreateCheckBox(string title, string detail, int x, int y)
        {
            CheckBox checkBox = new CheckBox();
            checkBox.Checked = true;
            checkBox.Text = title + "：" + detail;
            checkBox.Location = new Point(x, y);
            checkBox.Width = 480;
            checkBox.Height = 30;
            return checkBox;
        }

        private static Button CreateButton(string text, int x, int y, int width, EventHandler handler)
        {
            Button button = new Button();
            button.Text = text;
            button.Location = new Point(x, y);
            button.Size = new Size(width, 30);
            button.Click += handler;
            return button;
        }

        private void SelectAllButtonClick(object sender, EventArgs e)
        {
            SetAllChecked(true);
        }

        private void ClearButtonClick(object sender, EventArgs e)
        {
            SetAllChecked(false);
        }

        private void OkButtonClick(object sender, EventArgs e)
        {
            if (!objectIdentityCheckBox.Checked
                && !cadQuantityCheckBox.Checked
                && !systemEquipmentCheckBox.Checked
                && !locationRoutingCheckBox.Checked
                && !reviewStatusCheckBox.Checked)
            {
                MessageBox.Show(this, "請至少勾選一種要匯出的資訊。", "無法匯出", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            DialogResult = DialogResult.OK;
            Close();
        }

        private void CancelButtonClick(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        private void SetAllChecked(bool isChecked)
        {
            objectIdentityCheckBox.Checked = isChecked;
            cadQuantityCheckBox.Checked = isChecked;
            systemEquipmentCheckBox.Checked = isChecked;
            locationRoutingCheckBox.Checked = isChecked;
            reviewStatusCheckBox.Checked = isChecked;
        }
    }
}
