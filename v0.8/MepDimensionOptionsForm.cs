using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace QtoWirePlugin
{
    public class MepDimensionOptionsForm : Form
    {
        private readonly RadioButton chainWithBoundaryRadioButton;
        private readonly RadioButton spacingOnlyRadioButton;
        private readonly RadioButton boundaryOnlyRadioButton;
        private readonly ComboBox dimStyleComboBox;
        private readonly CheckBox xDimensionCheckBox;
        private readonly CheckBox yDimensionCheckBox;
        private readonly CheckBox unknownCheckBox;
        private readonly CheckBox topBoundaryCheckBox;
        private readonly CheckBox bottomBoundaryCheckBox;
        private readonly CheckBox leftBoundaryCheckBox;
        private readonly CheckBox rightBoundaryCheckBox;

        public MepDimensionOptionsForm(IList<string> dimStyleNames, string currentDimStyleName)
        {
            Text = "MEP 天花標註設定";
            StartPosition = FormStartPosition.CenterScreen;
            Width = 640;
            Height = 470;
            MinimumSize = new Size(640, 470);
            MaximizeBox = false;
            MinimizeBox = false;
            FormBorderStyle = FormBorderStyle.FixedDialog;

            Label titleLabel = new Label();
            titleLabel.Text = "選擇本次要使用的圖面標註型式";
            titleLabel.Font = new Font(Font.FontFamily, 13.0f, FontStyle.Bold);
            titleLabel.AutoSize = true;
            titleLabel.Location = new Point(18, 18);
            Controls.Add(titleLabel);

            Label hintLabel = new Label();
            hintLabel.Text = "這裡會讀取目前 DWG 已建立的 DIMSTYLE，例如 SLASHM、SLASHL。";
            hintLabel.AutoSize = true;
            hintLabel.Location = new Point(20, 50);
            Controls.Add(hintLabel);

            Label dimStyleLabel = new Label();
            dimStyleLabel.Text = "標註型式";
            dimStyleLabel.AutoSize = true;
            dimStyleLabel.Location = new Point(20, 86);
            Controls.Add(dimStyleLabel);

            dimStyleComboBox = new ComboBox();
            dimStyleComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            dimStyleComboBox.Location = new Point(92, 82);
            dimStyleComboBox.Size = new Size(320, 24);
            Controls.Add(dimStyleComboBox);

            if (dimStyleNames != null)
            {
                foreach (string name in dimStyleNames)
                {
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        dimStyleComboBox.Items.Add(name);
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(currentDimStyleName) && dimStyleComboBox.Items.Contains(currentDimStyleName))
            {
                dimStyleComboBox.SelectedItem = currentDimStyleName;
            }
            else if (dimStyleComboBox.Items.Count > 0)
            {
                dimStyleComboBox.SelectedIndex = 0;
            }

            GroupBox modeGroup = new GroupBox();
            modeGroup.Text = "尺寸產生方式";
            modeGroup.Location = new Point(18, 122);
            modeGroup.Size = new Size(586, 150);
            Controls.Add(modeGroup);

            chainWithBoundaryRadioButton = new RadioButton();
            chainWithBoundaryRadioButton.Text = "燈具矩陣：代表列/欄標中心距，外圈依勾選方向補邊界定位";
            chainWithBoundaryRadioButton.Location = new Point(18, 30);
            chainWithBoundaryRadioButton.Size = new Size(550, 24);
            chainWithBoundaryRadioButton.Checked = true;
            modeGroup.Controls.Add(chainWithBoundaryRadioButton);

            spacingOnlyRadioButton = new RadioButton();
            spacingOnlyRadioButton.Text = "只標代表列/欄設備間距";
            spacingOnlyRadioButton.Location = new Point(18, 66);
            spacingOnlyRadioButton.Size = new Size(550, 24);
            modeGroup.Controls.Add(spacingOnlyRadioButton);

            boundaryOnlyRadioButton = new RadioButton();
            boundaryOnlyRadioButton.Text = "只標外圈邊界定位";
            boundaryOnlyRadioButton.Location = new Point(18, 102);
            boundaryOnlyRadioButton.Size = new Size(550, 24);
            modeGroup.Controls.Add(boundaryOnlyRadioButton);

            GroupBox boundaryGroup = new GroupBox();
            boundaryGroup.Text = "外圈定位方向";
            boundaryGroup.Location = new Point(18, 286);
            boundaryGroup.Size = new Size(586, 56);
            Controls.Add(boundaryGroup);

            topBoundaryCheckBox = CreateBoundaryCheckBox("上", 18);
            bottomBoundaryCheckBox = CreateBoundaryCheckBox("下", 92);
            leftBoundaryCheckBox = CreateBoundaryCheckBox("左", 166);
            rightBoundaryCheckBox = CreateBoundaryCheckBox("右", 240);
            boundaryGroup.Controls.Add(topBoundaryCheckBox);
            boundaryGroup.Controls.Add(bottomBoundaryCheckBox);
            boundaryGroup.Controls.Add(leftBoundaryCheckBox);
            boundaryGroup.Controls.Add(rightBoundaryCheckBox);

            GroupBox optionGroup = new GroupBox();
            optionGroup.Text = "輸出內容";
            optionGroup.Location = new Point(18, 354);
            optionGroup.Size = new Size(586, 56);
            Controls.Add(optionGroup);

            xDimensionCheckBox = new CheckBox();
            xDimensionCheckBox.Text = "X 向尺寸";
            xDimensionCheckBox.Location = new Point(18, 24);
            xDimensionCheckBox.Size = new Size(110, 22);
            xDimensionCheckBox.Checked = true;
            optionGroup.Controls.Add(xDimensionCheckBox);

            yDimensionCheckBox = new CheckBox();
            yDimensionCheckBox.Text = "Y 向尺寸";
            yDimensionCheckBox.Location = new Point(140, 24);
            yDimensionCheckBox.Size = new Size(110, 22);
            yDimensionCheckBox.Checked = true;
            optionGroup.Controls.Add(yDimensionCheckBox);

            unknownCheckBox = new CheckBox();
            unknownCheckBox.Text = "標出 UNKNOWN";
            unknownCheckBox.Location = new Point(262, 24);
            unknownCheckBox.Size = new Size(160, 22);
            unknownCheckBox.Checked = true;
            optionGroup.Controls.Add(unknownCheckBox);

            Button okButton = new Button();
            okButton.Text = "產生標註";
            okButton.DialogResult = DialogResult.OK;
            okButton.Location = new Point(412, 416);
            okButton.Size = new Size(92, 30);
            Controls.Add(okButton);

            Button cancelButton = new Button();
            cancelButton.Text = "取消";
            cancelButton.DialogResult = DialogResult.Cancel;
            cancelButton.Location = new Point(512, 416);
            cancelButton.Size = new Size(92, 30);
            Controls.Add(cancelButton);

            AcceptButton = okButton;
            CancelButton = cancelButton;
        }

        public MepDimensionOptions GetOptions()
        {
            MepDimensionOptions options = new MepDimensionOptions();

            if (spacingOnlyRadioButton.Checked)
            {
                options.Mode = MepDimensionMode.SpacingOnly;
            }
            else if (boundaryOnlyRadioButton.Checked)
            {
                options.Mode = MepDimensionMode.BoundaryOnly;
            }
            else
            {
                options.Mode = MepDimensionMode.ChainWithBoundary;
            }

            options.DimStyleName = dimStyleComboBox.SelectedItem as string ?? string.Empty;
            options.GenerateXDimensions = xDimensionCheckBox.Checked;
            options.GenerateYDimensions = yDimensionCheckBox.Checked;
            options.MarkUnknownBlocks = unknownCheckBox.Checked;
            options.RowTolerance = MepDimensionDefaults.RowTolerance;
            options.ColumnTolerance = MepDimensionDefaults.ColumnTolerance;
            options.InternalOffset = MepDimensionDefaults.DimensionOffset;
            options.BoundaryOffset = MepDimensionDefaults.DimensionOffset;
            options.BoundaryTop = topBoundaryCheckBox.Checked;
            options.BoundaryBottom = bottomBoundaryCheckBox.Checked;
            options.BoundaryLeft = leftBoundaryCheckBox.Checked;
            options.BoundaryRight = rightBoundaryCheckBox.Checked;

            return options;
        }

        private static CheckBox CreateBoundaryCheckBox(string text, int x)
        {
            CheckBox checkBox = new CheckBox();
            checkBox.Text = text;
            checkBox.Location = new Point(x, 24);
            checkBox.Size = new Size(62, 22);
            checkBox.Checked = true;
            return checkBox;
        }
    }
}
