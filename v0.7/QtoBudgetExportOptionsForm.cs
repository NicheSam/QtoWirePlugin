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
            Width = 540;
            Height = 390;

            Label titleLabel = new Label();
            titleLabel.Text = "請勾選要匯出的 CSV 資訊";
            titleLabel.Font = new Font(SystemFonts.MessageBoxFont.FontFamily, 12.0f, FontStyle.Bold);
            titleLabel.Location = new Point(18, 16);
            titleLabel.AutoSize = true;
            Controls.Add(titleLabel);

            Label summaryLabel = new Label();
            summaryLabel.Text = "目前可匯出筆數：" + rowCount + "；需人工確認：" + reviewCount;
            summaryLabel.Location = new Point(20, 46);
            summaryLabel.AutoSize = true;
            Controls.Add(summaryLabel);

            objectIdentityCheckBox = CreateCheckBox("物件識別", "來源DWG、物件識別碼、圖塊名稱、圖層", 22, 82);
            cadQuantityCheckBox = CreateCheckBox("CAD計量", "CAD計量型態、QTO編號、數量依據、QTO數量、單位、長度", 22, 122);
            systemEquipmentCheckBox = CreateCheckBox("系統與設備", "系統代碼、設備類型代碼、設備類型名稱", 22, 162);
            locationRoutingCheckBox = CreateCheckBox("位置與配線", "樓層、區域、空間、線材類型、管線類型、管線尺寸、線槽尺寸", 22, 202);
            reviewStatusCheckBox = CreateCheckBox("對應檢查", "對應狀態、待確認原因、資料來源規則", 22, 242);

            Controls.Add(objectIdentityCheckBox);
            Controls.Add(cadQuantityCheckBox);
            Controls.Add(systemEquipmentCheckBox);
            Controls.Add(locationRoutingCheckBox);
            Controls.Add(reviewStatusCheckBox);

            Button allButton = CreateButton("全選", 22, 300, 72, SelectAllButtonClick);
            Button clearButton = CreateButton("清除", 102, 300, 72, ClearButtonClick);
            Button okButton = CreateButton("確定", 346, 300, 72, OkButtonClick);
            Button cancelButton = CreateButton("取消", 426, 300, 72, CancelButtonClick);
            AcceptButton = okButton;
            CancelButton = cancelButton;

            Controls.Add(allButton);
            Controls.Add(clearButton);
            Controls.Add(okButton);
            Controls.Add(cancelButton);
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
