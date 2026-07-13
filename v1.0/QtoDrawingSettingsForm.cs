using System;
using System.Linq;
using System.Windows.Forms;
using Autodesk.AutoCAD.DatabaseServices;

namespace QtoWirePlugin
{
    public sealed class QtoDrawingSettings
    {
        public string ObjectKind { get; set; }
        public string SystemCode { get; set; }
        public string EquipmentTypeCode { get; set; }
        public string MaterialType { get; set; }
    }

    public sealed class QtoDrawingSettingsForm : Form
    {
        private readonly bool conversionMode;
        private readonly ComboBox objectKind;
        private readonly ComboBox systemCode;
        private readonly ComboBox equipmentType;
        private readonly TextBox materialType;
        private readonly Label equipmentLabel;
        private readonly Label materialLabel;

        public QtoDrawingSettings Settings { get; private set; }

        public QtoDrawingSettingsForm(bool conversionMode, Database database = null)
        {
            this.conversionMode = conversionMode;
            Text = conversionMode ? "轉換既有物件" : "直接繪製 QTO 線管";
            Width = 520;
            Height = 320;
            MinimumSize = new System.Drawing.Size(460, 300);
            StartPosition = FormStartPosition.CenterParent;
            AutoScaleMode = AutoScaleMode.Dpi;
            QtoUiTheme.ApplyForm(this);

            TableLayoutPanel table = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(18), ColumnCount = 2, RowCount = 6 };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            table.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));

            objectKind = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            objectKind.Items.AddRange(conversionMode
                ? new object[] { "設備", "出線口", "箱體", "盤箱", "配線", "管段" }
                : new object[] { "配線", "管段" });
            objectKind.SelectedIndex = 0;
            objectKind.SelectedIndexChanged += delegate { UpdateLabels(); };
            systemCode = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            systemCode.Items.AddRange(QtoSystemDefaults.GetValues());
            systemCode.SelectedIndex = Math.Min(2, systemCode.Items.Count - 1);
            equipmentType = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDown };
            QtoDictionaryStore dictionary = QtoDictionaryLoader.LoadDefault(database);
            foreach (QtoEquipmentTypeDefinition definition in dictionary.EquipmentTypes.Values
                .Where(value => value != null)
                .OrderBy(value => value.EquipmentTypeName ?? value.EquipmentTypeCode, StringComparer.CurrentCulture))
            {
                equipmentType.Items.Add(new EquipmentChoice(definition.EquipmentTypeCode, definition.EquipmentTypeName));
            }
            materialType = new TextBox { Dock = DockStyle.Fill, Text = "Cat6" };
            AddRow(table, 0, "物件類型", objectKind);
            AddRow(table, 1, "系統", systemCode);
            equipmentLabel = AddRow(table, 2, "設備類型", equipmentType);
            materialLabel = AddRow(table, 3, "線材／管材", materialType);
            table.Controls.Add(new Label { Text = "完成後按 Enter 結束繪製；既有 QTO 物件不會被覆蓋。", Dock = DockStyle.Fill, AutoSize = false, ForeColor = System.Drawing.SystemColors.GrayText }, 0, 4);
            table.SetColumnSpan(table.GetControlFromPosition(0, 4), 2);

            FlowLayoutPanel actions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = System.Windows.Forms.FlowDirection.RightToLeft, WrapContents = false };
            Button ok = QtoUiTheme.CreateButton("開始", null, QtoButtonRole.Primary);
            ok.Width = 96;
            ok.DialogResult = DialogResult.OK;
            Button cancel = QtoUiTheme.CreateButton("取消", null, QtoButtonRole.Default);
            cancel.Width = 96;
            cancel.DialogResult = DialogResult.Cancel;
            ok.Click += SaveSettings;
            actions.Controls.Add(ok);
            actions.Controls.Add(cancel);
            table.Controls.Add(actions, 0, 5);
            table.SetColumnSpan(actions, 2);
            Controls.Add(table);
            AcceptButton = ok;
            CancelButton = cancel;
            UpdateLabels();
        }

        private static Label AddRow(TableLayoutPanel table, int row, string text, Control control)
        {
            Label label = new Label { Text = text, Dock = DockStyle.Fill, TextAlign = System.Drawing.ContentAlignment.MiddleLeft };
            table.Controls.Add(label, 0, row);
            table.Controls.Add(control, 1, row);
            return label;
        }

        private void UpdateLabels()
        {
            string kind = Convert.ToString(objectKind.SelectedItem) ?? string.Empty;
            bool line = kind == "配線" || kind == "管段";
            equipmentType.Enabled = conversionMode && !line;
            materialType.Enabled = line;
            equipmentLabel.ForeColor = equipmentType.Enabled ? System.Drawing.SystemColors.ControlText : System.Drawing.SystemColors.GrayText;
            materialLabel.Text = kind == "管段" ? "管材類型" : "線材類型";
            if (kind == "管段" && string.Equals(materialType.Text.Trim(), "Cat6", StringComparison.OrdinalIgnoreCase)) materialType.Text = "EMT 25mm";
            if (kind == "配線" && materialType.Text.Trim().StartsWith("EMT", StringComparison.OrdinalIgnoreCase)) materialType.Text = "Cat6";
        }

        private void SaveSettings(object sender, EventArgs e)
        {
            Settings = new QtoDrawingSettings
            {
                ObjectKind = Convert.ToString(objectKind.SelectedItem) ?? string.Empty,
                SystemCode = Convert.ToString(systemCode.SelectedItem) ?? string.Empty,
                EquipmentTypeCode = equipmentType.SelectedItem is EquipmentChoice
                    ? ((EquipmentChoice)equipmentType.SelectedItem).Code
                    : ParseEquipmentCode(equipmentType.Text),
                MaterialType = materialType.Text.Trim()
            };
        }

        private static string ParseEquipmentCode(string value)
        {
            string text = (value ?? string.Empty).Trim();
            int open = text.LastIndexOf('(');
            int close = text.LastIndexOf(')');
            return open >= 0 && close > open ? text.Substring(open + 1, close - open - 1).Trim() : text;
        }

        private sealed class EquipmentChoice
        {
            public EquipmentChoice(string code, string name)
            {
                Code = code ?? string.Empty;
                Name = name ?? string.Empty;
            }

            public string Code { get; private set; }
            public string Name { get; private set; }

            public override string ToString()
            {
                return string.IsNullOrWhiteSpace(Name) ? Code : Name + " (" + Code + ")";
            }
        }
    }
}
