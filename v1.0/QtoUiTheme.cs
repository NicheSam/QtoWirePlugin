using System;
using System.Drawing;
using System.Windows.Forms;

namespace QtoWirePlugin
{
    internal enum QtoButtonRole
    {
        Primary,
        Default,
        Secondary
    }

    internal static class QtoUiTheme
    {
        public static readonly Color WindowBackColor = Color.FromArgb(244, 245, 247);
        public static readonly Color PanelBackColor = Color.FromArgb(255, 255, 255);
        public static readonly Color BorderColor = Color.FromArgb(205, 210, 218);
        public static readonly Color TextColor = Color.FromArgb(32, 38, 46);
        public static readonly Color MutedTextColor = Color.FromArgb(92, 101, 115);
        public static readonly Color PrimaryColor = Color.FromArgb(0, 95, 184);
        public static readonly Color WarningColor = Color.FromArgb(154, 103, 0);
        public static readonly Color ErrorColor = Color.FromArgb(168, 0, 0);
        public static readonly Color SuccessColor = Color.FromArgb(16, 124, 16);

        public static readonly Padding FormPadding = new Padding(12);
        public static readonly Padding GroupPadding = new Padding(10, 20, 10, 10);
        public static readonly Padding CompactGroupPadding = new Padding(8, 18, 8, 8);
        public static readonly Padding ControlMargin = new Padding(4);

        public static Font BaseFont
        {
            get { return SystemFonts.MessageBoxFont; }
        }

        public static Font HeaderFont
        {
            get { return new Font(BaseFont.FontFamily, 14.0f, FontStyle.Bold); }
        }

        public static Font SectionFont
        {
            get { return new Font(BaseFont.FontFamily, 9.0f, FontStyle.Bold); }
        }

        public static void ApplyForm(Form form)
        {
            if (form == null)
            {
                return;
            }

            form.AutoScaleMode = AutoScaleMode.Dpi;
            form.BackColor = WindowBackColor;
            form.Font = BaseFont;
        }

        public static void ApplyPanel(Control control)
        {
            if (control == null)
            {
                return;
            }

            control.BackColor = WindowBackColor;
            control.ForeColor = TextColor;
            control.Font = BaseFont;
        }

        public static GroupBox WrapGroup(string title, Control content)
        {
            GroupBox groupBox = new GroupBox();
            groupBox.Text = title;
            groupBox.Dock = DockStyle.Fill;
            groupBox.Padding = GroupPadding;
            groupBox.BackColor = PanelBackColor;
            groupBox.ForeColor = TextColor;
            groupBox.Font = BaseFont;
            content.Dock = DockStyle.Fill;
            groupBox.Controls.Add(content);
            return groupBox;
        }

        public static Label CreateLabel(string text, ContentAlignment alignment)
        {
            Label label = new Label();
            label.Text = text;
            label.Dock = DockStyle.Fill;
            label.TextAlign = alignment;
            label.ForeColor = TextColor;
            return label;
        }

        public static Label CreateMutedLabel(string text)
        {
            Label label = CreateLabel(text, ContentAlignment.MiddleLeft);
            label.ForeColor = MutedTextColor;
            return label;
        }

        public static Button CreateButton(string text, EventHandler handler, QtoButtonRole role)
        {
            Button button = new Button();
            button.Text = text;
            button.Height = 32;
            button.Margin = ControlMargin;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderColor = role == QtoButtonRole.Primary ? PrimaryColor : BorderColor;
            button.FlatAppearance.BorderSize = 1;
            button.UseVisualStyleBackColor = false;
            button.BackColor = role == QtoButtonRole.Primary ? PrimaryColor : Color.FromArgb(250, 251, 252);
            button.ForeColor = role == QtoButtonRole.Primary ? Color.White : TextColor;
            if (handler != null)
            {
                button.Click += handler;
            }
            return button;
        }

        public static void ApplyReadOnlyTextBox(TextBox textBox)
        {
            if (textBox == null)
            {
                return;
            }

            textBox.BackColor = Color.White;
            textBox.ForeColor = TextColor;
            textBox.BorderStyle = BorderStyle.FixedSingle;
        }

        public static void ApplyGrid(DataGridView grid)
        {
            if (grid == null)
            {
                return;
            }

            grid.BackgroundColor = Color.White;
            grid.BorderStyle = BorderStyle.FixedSingle;
            grid.GridColor = Color.FromArgb(224, 228, 234);
            grid.EnableHeadersVisualStyles = false;
            grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(238, 241, 245);
            grid.ColumnHeadersDefaultCellStyle.ForeColor = TextColor;
            grid.ColumnHeadersDefaultCellStyle.Font = SectionFont;
            grid.DefaultCellStyle.BackColor = Color.White;
            grid.DefaultCellStyle.ForeColor = TextColor;
            grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(218, 232, 252);
            grid.DefaultCellStyle.SelectionForeColor = TextColor;
            grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 249, 251);
            grid.RowTemplate.Height = 26;
        }

        public static void ApplyStatusColor(Label label, string statusText)
        {
            if (label == null)
            {
                return;
            }

            string value = statusText ?? string.Empty;
            if (value.IndexOf("失敗", StringComparison.OrdinalIgnoreCase) >= 0
                || value.IndexOf("錯誤", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                label.ForeColor = ErrorColor;
            }
            else if (value.IndexOf("問題", StringComparison.OrdinalIgnoreCase) >= 0
                || value.IndexOf("確認", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                label.ForeColor = WarningColor;
            }
            else if (value.IndexOf("完成", StringComparison.OrdinalIgnoreCase) >= 0
                || value.IndexOf("更新", StringComparison.OrdinalIgnoreCase) >= 0
                || value.IndexOf("同步中", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                label.ForeColor = SuccessColor;
            }
            else
            {
                label.ForeColor = MutedTextColor;
            }
        }
    }
}
