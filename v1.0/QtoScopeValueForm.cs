using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace QtoWirePlugin
{
    internal sealed class QtoScopeValueForm : Form
    {
        private readonly ComboBox valueComboBox;

        public string ScopeValue
        {
            get { return valueComboBox.Text == null ? string.Empty : valueComboBox.Text.Trim(); }
        }

        public QtoScopeValueForm(string title, string labelText, IEnumerable<string> values)
        {
            Text = title;
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(420, 180);
            MinimumSize = new Size(380, 160);
            MaximizeBox = false;
            MinimizeBox = false;
            QtoUiTheme.ApplyForm(this);

            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.Padding = new Padding(16);
            root.ColumnCount = 1;
            root.RowCount = 3;
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            Controls.Add(root);

            Label label = QtoUiTheme.CreateLabel(labelText, ContentAlignment.MiddleLeft);
            label.Dock = DockStyle.Fill;
            root.Controls.Add(label, 0, 0);

            valueComboBox = new ComboBox();
            valueComboBox.Dock = DockStyle.Fill;
            valueComboBox.DropDownStyle = ComboBoxStyle.DropDown;
            valueComboBox.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
            valueComboBox.AutoCompleteSource = AutoCompleteSource.ListItems;
            valueComboBox.BackColor = Color.White;
            valueComboBox.ForeColor = QtoUiTheme.TextColor;
            foreach (string value in NormalizeValues(values))
            {
                valueComboBox.Items.Add(value);
            }

            if (valueComboBox.Items.Count > 0)
            {
                valueComboBox.SelectedIndex = 0;
            }

            root.Controls.Add(valueComboBox, 0, 1);

            FlowLayoutPanel actions = new FlowLayoutPanel();
            actions.Dock = DockStyle.Fill;
            actions.FlowDirection = FlowDirection.RightToLeft;
            actions.WrapContents = false;

            Button okButton = QtoUiTheme.CreateButton("確定", OkButtonClick, QtoButtonRole.Primary);
            AcceptButton = okButton;
            actions.Controls.Add(okButton);

            Button cancelButton = QtoUiTheme.CreateButton("取消", null, QtoButtonRole.Secondary);
            cancelButton.Click += delegate
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };
            CancelButton = cancelButton;
            actions.Controls.Add(cancelButton);

            root.Controls.Add(actions, 0, 2);
        }

        private void OkButtonClick(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(ScopeValue))
            {
                MessageBox.Show(this, "請選擇或輸入有效值。", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            DialogResult = DialogResult.OK;
            Close();
        }

        private static IEnumerable<string> NormalizeValues(IEnumerable<string> values)
        {
            List<string> result = new List<string>();
            if (values != null)
            {
                foreach (string value in values)
                {
                    if (!string.IsNullOrWhiteSpace(value) && !result.Contains(value.Trim(), StringComparer.OrdinalIgnoreCase))
                    {
                        result.Add(value.Trim());
                    }
                }
            }

            return result.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToList();
        }
    }
}
