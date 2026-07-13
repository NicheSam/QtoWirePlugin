using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace QtoWirePlugin
{
    public class QtoSelectSameOptions
    {
        public bool MatchEquipmentTypeCode { get; set; }
        public bool MatchQtoType { get; set; }
        public bool MatchSystemCode { get; set; }
        public bool MatchCableType { get; set; }
        public string EquipmentTypeCode { get; set; }
        public string QtoType { get; set; }
        public string SystemCode { get; set; }
        public string CableType { get; set; }

        public static QtoSelectSameOptions FromXData(Dictionary<string, string> data)
        {
            return new QtoSelectSameOptions
            {
                MatchEquipmentTypeCode = HasValue(Get(data, QtoXDataHelper.KeyEquipmentTypeCode)),
                MatchQtoType = HasValue(Get(data, QtoXDataHelper.KeyQtoType)),
                MatchSystemCode = HasValue(Get(data, QtoXDataHelper.KeySystemCode)),
                MatchCableType = HasValue(Get(data, QtoXDataHelper.KeyCableType)),
                EquipmentTypeCode = Get(data, QtoXDataHelper.KeyEquipmentTypeCode),
                QtoType = Get(data, QtoXDataHelper.KeyQtoType),
                SystemCode = Get(data, QtoXDataHelper.KeySystemCode),
                CableType = Get(data, QtoXDataHelper.KeyCableType)
            };
        }

        public bool HasAnySelectedCondition()
        {
            return MatchEquipmentTypeCode
                || MatchQtoType
                || MatchSystemCode
                || MatchCableType;
        }

        public bool Matches(Dictionary<string, string> data)
        {
            if (MatchEquipmentTypeCode && !MatchesValue(data, QtoXDataHelper.KeyEquipmentTypeCode, EquipmentTypeCode))
            {
                return false;
            }

            if (MatchQtoType && !MatchesValue(data, QtoXDataHelper.KeyQtoType, QtoType))
            {
                return false;
            }

            if (MatchSystemCode && !MatchesValue(data, QtoXDataHelper.KeySystemCode, SystemCode))
            {
                return false;
            }

            if (MatchCableType && !MatchesValue(data, QtoXDataHelper.KeyCableType, CableType))
            {
                return false;
            }

            return HasAnySelectedCondition();
        }

        private static bool MatchesValue(Dictionary<string, string> data, string key, string expectedValue)
        {
            string actualValue = Get(data, key);
            return string.Equals(
                Normalize(actualValue),
                Normalize(expectedValue),
                StringComparison.OrdinalIgnoreCase);
        }

        private static string Get(Dictionary<string, string> data, string key)
        {
            if (data == null || string.IsNullOrWhiteSpace(key))
            {
                return string.Empty;
            }

            string value;
            if (data.TryGetValue(key, out value))
            {
                return value ?? string.Empty;
            }

            return string.Empty;
        }

        private static bool HasValue(string value)
        {
            return !string.IsNullOrWhiteSpace(value);
        }

        private static string Normalize(string value)
        {
            return (value ?? string.Empty).Trim();
        }
    }

    public class QtoSelectSameOptionsForm : Form
    {
        private readonly CheckBox equipmentTypeCheckBox;
        private readonly CheckBox qtoTypeCheckBox;
        private readonly CheckBox systemCodeCheckBox;
        private readonly CheckBox cableTypeCheckBox;
        private readonly ComboBox equipmentTypeComboBox;
        private readonly ComboBox qtoTypeComboBox;
        private readonly ComboBox systemCodeComboBox;
        private readonly ComboBox cableTypeComboBox;

        public QtoSelectSameOptionsForm(QtoSelectSameOptions sourceOptions, QtoDictionaryStore dictionary, List<string> cableTypes)
        {
            if (sourceOptions == null)
            {
                sourceOptions = new QtoSelectSameOptions();
            }

            Text = "選取相同 QTO";
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Width = 560;
            Height = 330;

            Label titleLabel = new Label();
            titleLabel.Text = "選擇要比對的 QTO 資訊";
            titleLabel.Font = new Font(SystemFonts.MessageBoxFont.FontFamily, 11.0f, FontStyle.Bold);
            titleLabel.Location = new Point(18, 16);
            titleLabel.AutoSize = true;
            Controls.Add(titleLabel);

            Label hintLabel = new Label();
            hintLabel.Text = "勾選條件並指定值，按確定後會選取模型空間中相同資訊的圖塊與線段。";
            hintLabel.Location = new Point(20, 44);
            hintLabel.AutoSize = true;
            Controls.Add(hintLabel);

            equipmentTypeCheckBox = CreateCheckBox("設備類型", 22, 82);
            equipmentTypeComboBox = CreateComboBox(142, 78, GetEquipmentTypeDisplays(dictionary));
            qtoTypeCheckBox = CreateCheckBox("CAD計量型態", 22, 120);
            qtoTypeComboBox = CreateComboBox(142, 116, GetQtoTypeDisplays());
            systemCodeCheckBox = CreateCheckBox("系統代碼", 22, 158);
            systemCodeComboBox = CreateComboBox(142, 154, GetSystemCodes(dictionary));
            cableTypeCheckBox = CreateCheckBox("線材類型", 22, 196);
            cableTypeComboBox = CreateComboBox(142, 192, ToArray(cableTypes));

            SetInitialValue(equipmentTypeCheckBox, equipmentTypeComboBox, ToEquipmentTypeDisplay(sourceOptions.EquipmentTypeCode, dictionary));
            SetInitialValue(qtoTypeCheckBox, qtoTypeComboBox, ToQtoTypeDisplay(sourceOptions.QtoType));
            SetInitialValue(systemCodeCheckBox, systemCodeComboBox, sourceOptions.SystemCode);
            SetInitialValue(cableTypeCheckBox, cableTypeComboBox, sourceOptions.CableType);

            Controls.Add(equipmentTypeCheckBox);
            Controls.Add(equipmentTypeComboBox);
            Controls.Add(qtoTypeCheckBox);
            Controls.Add(qtoTypeComboBox);
            Controls.Add(systemCodeCheckBox);
            Controls.Add(systemCodeComboBox);
            Controls.Add(cableTypeCheckBox);
            Controls.Add(cableTypeComboBox);

            Button okButton = CreateButton("確定", 372, 248, 72, OkButtonClick);
            Button cancelButton = CreateButton("取消", 452, 248, 72, CancelButtonClick);
            AcceptButton = okButton;
            CancelButton = cancelButton;

            Controls.Add(okButton);
            Controls.Add(cancelButton);
        }

        public QtoSelectSameOptions Options
        {
            get
            {
                return new QtoSelectSameOptions
                {
                    MatchEquipmentTypeCode = equipmentTypeCheckBox.Checked && !string.IsNullOrWhiteSpace(equipmentTypeComboBox.Text),
                    MatchQtoType = qtoTypeCheckBox.Checked && !string.IsNullOrWhiteSpace(qtoTypeComboBox.Text),
                    MatchSystemCode = systemCodeCheckBox.Checked && !string.IsNullOrWhiteSpace(systemCodeComboBox.Text),
                    MatchCableType = cableTypeCheckBox.Checked && !string.IsNullOrWhiteSpace(cableTypeComboBox.Text),
                    EquipmentTypeCode = ToEquipmentTypeCode(equipmentTypeComboBox.Text),
                    QtoType = ToQtoTypeCode(qtoTypeComboBox.Text),
                    SystemCode = systemCodeComboBox.Text.Trim(),
                    CableType = cableTypeComboBox.Text.Trim()
                };
            }
        }

        private static CheckBox CreateCheckBox(string text, int x, int y)
        {
            CheckBox checkBox = new CheckBox();
            checkBox.Text = text;
            checkBox.Location = new Point(x, y);
            checkBox.Width = 110;
            checkBox.Height = 24;
            return checkBox;
        }

        private static ComboBox CreateComboBox(int x, int y, string[] values)
        {
            ComboBox comboBox = new ComboBox();
            comboBox.Location = new Point(x, y);
            comboBox.Size = new Size(380, 25);
            comboBox.DropDownStyle = ComboBoxStyle.DropDown;
            if (values != null)
            {
                comboBox.Items.AddRange(values);
            }

            return comboBox;
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

        private static void SetInitialValue(CheckBox checkBox, ComboBox comboBox, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                checkBox.Checked = false;
                comboBox.Text = string.Empty;
                return;
            }

            checkBox.Checked = true;
            comboBox.Text = value;
        }

        private void OkButtonClick(object sender, EventArgs e)
        {
            if (!Options.HasAnySelectedCondition())
            {
                MessageBox.Show(this, "請至少勾選一個比對條件，並指定要比對的值。", "選取相同 QTO", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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

        private static string[] GetEquipmentTypeDisplays(QtoDictionaryStore dictionary)
        {
            List<string> values = new List<string>();
            if (dictionary != null)
            {
                foreach (string code in dictionary.EquipmentTypes.Keys)
                {
                    values.Add(ToEquipmentTypeDisplay(code, dictionary));
                }
            }

            values.Sort(StringComparer.OrdinalIgnoreCase);
            return values.ToArray();
        }

        private static string[] GetSystemCodes(QtoDictionaryStore dictionary)
        {
            return QtoSystemDefaults.OrderValues(dictionary == null ? null : dictionary.SystemCodes);
        }

        private static string[] GetQtoTypeDisplays()
        {
            return new string[]
            {
                ToQtoTypeDisplay(QtoXDataHelper.TypeOutlet),
                ToQtoTypeDisplay(QtoXDataHelper.TypeJunctionBox),
                ToQtoTypeDisplay(QtoXDataHelper.TypeWire),
                ToQtoTypeDisplay(QtoXDataHelper.TypeConduitSegment),
                ToQtoTypeDisplay(QtoXDataHelper.TypeTray),
                ToQtoTypeDisplay(QtoXDataHelper.TypeDevice),
                ToQtoTypeDisplay(QtoXDataHelper.TypePanel)
            };
        }

        private static string[] ToArray(List<string> values)
        {
            return values == null ? new string[0] : values.ToArray();
        }

        private static string ToEquipmentTypeDisplay(string code, QtoDictionaryStore dictionary)
        {
            string value = code == null ? string.Empty : code.Trim();
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            QtoEquipmentTypeDefinition definition = dictionary == null ? null : dictionary.FindEquipment(value);
            if (definition == null || string.IsNullOrWhiteSpace(definition.EquipmentTypeName))
            {
                return value;
            }

            return definition.EquipmentTypeName.Trim() + " (" + definition.EquipmentTypeCode + ")";
        }

        private static string ToEquipmentTypeCode(string display)
        {
            string value = display == null ? string.Empty : display.Trim();
            int open = value.LastIndexOf('(');
            int close = value.LastIndexOf(')');
            if (open >= 0 && close > open)
            {
                return value.Substring(open + 1, close - open - 1).Trim();
            }

            return value;
        }

        private static string ToQtoTypeDisplay(string code)
        {
            string value = code == null ? string.Empty : code.Trim();
            if (string.Equals(value, QtoXDataHelper.TypeOutlet, StringComparison.OrdinalIgnoreCase))
            {
                return "出線口 (OUTLET)";
            }

            if (string.Equals(value, QtoXDataHelper.TypeJunctionBox, StringComparison.OrdinalIgnoreCase))
            {
                return "箱體/接線箱 (JUNCTION_BOX)";
            }

            if (string.Equals(value, QtoXDataHelper.TypeWire, StringComparison.OrdinalIgnoreCase))
            {
                return "配線 (WIRE)";
            }

            if (string.Equals(value, QtoXDataHelper.TypeConduitSegment, StringComparison.OrdinalIgnoreCase))
            {
                return "管段 (CONDUIT_SEGMENT)";
            }

            if (string.Equals(value, QtoXDataHelper.TypeTray, StringComparison.OrdinalIgnoreCase))
            {
                return "線槽 (TRAY)";
            }

            if (string.Equals(value, QtoXDataHelper.TypeDevice, StringComparison.OrdinalIgnoreCase))
            {
                return "設備 (DEVICE)";
            }

            if (string.Equals(value, QtoXDataHelper.TypePanel, StringComparison.OrdinalIgnoreCase))
            {
                return "盤箱/設備盤 (PANEL)";
            }

            return value;
        }

        private static string ToQtoTypeCode(string display)
        {
            string value = display == null ? string.Empty : display.Trim();
            int open = value.LastIndexOf('(');
            int close = value.LastIndexOf(')');
            if (open >= 0 && close > open)
            {
                return value.Substring(open + 1, close - open - 1).Trim();
            }

            return value;
        }
    }
}
