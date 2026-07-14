using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace QtoWirePlugin
{
    public class QtoEditPropertiesForm : Form
    {
        private readonly CheckBox outletIdCheckBox;
        private readonly TextBox outletIdTextBox;
        private readonly CheckBox jbIdCheckBox;
        private readonly TextBox jbIdTextBox;
        private readonly CheckBox systemCheckBox;
        private readonly TextBox systemTextBox;
        private readonly CheckBox cableTypeCheckBox;
        private readonly TextBox cableTypeTextBox;

        public QtoEditPropertiesForm(string title, int itemCount, bool editOutlet, Dictionary<string, string> defaultValues)
        {
            Text = title;
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(500, editOutlet ? 330 : 250);
            QtoUiTheme.ApplyForm(this);

            Label titleLabel = new Label();
            titleLabel.Text = title;
            titleLabel.Font = new Font(Font.FontFamily, 14.0f, FontStyle.Bold);
            titleLabel.Location = new Point(18, 18);
            titleLabel.AutoSize = true;
            Controls.Add(titleLabel);

            Label countLabel = new Label();
            countLabel.Text = "已選取可編輯物件：" + itemCount + " 個";
            countLabel.Location = new Point(20, 52);
            countLabel.AutoSize = true;
            Controls.Add(countLabel);

            Label hintLabel = new Label();
            hintLabel.Text = "請勾選要批次更新的欄位。未勾選的欄位會保留原值。";
            hintLabel.Location = new Point(20, 78);
            hintLabel.AutoSize = true;
            Controls.Add(hintLabel);

            int y = 112;

            if (editOutlet)
            {
                outletIdCheckBox = CreateCheckBox("出線口編號", 22, y, false);
                outletIdTextBox = CreateTextBox(210, y - 2, GetDefault(defaultValues, QtoXDataHelper.KeyOutletId));
                BindEnabledState(outletIdCheckBox, outletIdTextBox);
                Controls.Add(outletIdCheckBox);
                Controls.Add(outletIdTextBox);
                y += 42;
            }

            jbIdCheckBox = CreateCheckBox("箱體編號", 22, y, false);
            jbIdTextBox = CreateTextBox(210, y - 2, GetDefault(defaultValues, QtoXDataHelper.KeyJbId));
            BindEnabledState(jbIdCheckBox, jbIdTextBox);
            Controls.Add(jbIdCheckBox);
            Controls.Add(jbIdTextBox);
            y += 42;

            systemCheckBox = CreateCheckBox("系統代碼", 22, y, true);
            systemTextBox = CreateTextBox(210, y - 2, GetDefaultOrFallback(defaultValues, QtoXDataHelper.KeySystem, "DATA"));
            BindEnabledState(systemCheckBox, systemTextBox);
            Controls.Add(systemCheckBox);
            Controls.Add(systemTextBox);
            y += 42;

            if (editOutlet)
            {
                cableTypeCheckBox = CreateCheckBox("線材類型", 22, y, true);
                cableTypeTextBox = CreateTextBox(210, y - 2, GetDefaultOrFallback(defaultValues, QtoXDataHelper.KeyCableType, "Cat6"));
                BindEnabledState(cableTypeCheckBox, cableTypeTextBox);
                Controls.Add(cableTypeCheckBox);
                Controls.Add(cableTypeTextBox);
                y += 42;
            }

            Button okButton = QtoUiTheme.CreateButton("套用選取欄位", null, QtoButtonRole.Primary);
            okButton.DialogResult = DialogResult.OK;
            okButton.Location = new Point(270, ClientSize.Height - 48);
            okButton.Size = new Size(112, 30);
            Controls.Add(okButton);

            Button cancelButton = QtoUiTheme.CreateButton("取消", null, QtoButtonRole.Secondary);
            cancelButton.DialogResult = DialogResult.Cancel;
            cancelButton.Location = new Point(394, ClientSize.Height - 48);
            cancelButton.Size = new Size(82, 30);
            Controls.Add(cancelButton);

            AcceptButton = okButton;
            CancelButton = cancelButton;
        }

        private static void BindEnabledState(CheckBox checkBox, TextBox textBox)
        {
            textBox.Enabled = checkBox.Checked;
            checkBox.CheckedChanged += delegate { textBox.Enabled = checkBox.Checked; };
        }

        public bool UpdateOutletId
        {
            get { return outletIdCheckBox != null && outletIdCheckBox.Checked; }
        }

        public string OutletId
        {
            get { return outletIdTextBox == null ? string.Empty : outletIdTextBox.Text.Trim(); }
        }

        public bool UpdateJbId
        {
            get { return jbIdCheckBox != null && jbIdCheckBox.Checked; }
        }

        public string JbId
        {
            get { return jbIdTextBox == null ? string.Empty : jbIdTextBox.Text.Trim(); }
        }

        public bool UpdateSystem
        {
            get { return systemCheckBox != null && systemCheckBox.Checked; }
        }

        public string SystemName
        {
            get { return systemTextBox == null ? string.Empty : systemTextBox.Text.Trim(); }
        }

        public bool UpdateCableType
        {
            get { return cableTypeCheckBox != null && cableTypeCheckBox.Checked; }
        }

        public string CableType
        {
            get { return cableTypeTextBox == null ? string.Empty : cableTypeTextBox.Text.Trim(); }
        }

        private static CheckBox CreateCheckBox(string text, int x, int y, bool isChecked)
        {
            CheckBox checkBox = new CheckBox();
            checkBox.Text = text;
            checkBox.Location = new Point(x, y);
            checkBox.Size = new Size(178, 24);
            checkBox.Checked = isChecked;
            return checkBox;
        }

        private static TextBox CreateTextBox(int x, int y, string text)
        {
            TextBox textBox = new TextBox();
            textBox.Location = new Point(x, y);
            textBox.Size = new Size(260, 25);
            textBox.Text = text;
            return textBox;
        }

        private static string GetDefault(Dictionary<string, string> values, string key)
        {
            string value;

            if (values != null && values.TryGetValue(key, out value))
            {
                return value ?? string.Empty;
            }

            return string.Empty;
        }

        private static string GetDefaultOrFallback(Dictionary<string, string> values, string key, string fallback)
        {
            string value = GetDefault(values, key);
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }
    }
}
