using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace QtoWirePlugin
{
    public class QtoBlockLibraryPickerForm : Form
    {
        private readonly QtoBlockCatalogService catalogService;
        private readonly TextBox catalogPathTextBox;
        private readonly DataGridView grid;
        private readonly PictureBox previewBox;
        private readonly Label previewTitleLabel;
        private readonly Label previewMetaLabel;
        private readonly RadioButton insertByOriginRadio;
        private readonly RadioButton insertByCenterRadio;
        private readonly Label statusLabel;
        private readonly TextBox searchTextBox;
        private readonly ComboBox systemFilterComboBox;
        private readonly ComboBox equipmentFilterComboBox;
        private readonly ComboBox statusFilterComboBox;
        private readonly CheckBox showUnconfiguredCheckBox;
        private QtoBlockCatalog catalog;
        private List<QtoBlockCatalogItem> allRows;

        public QtoBlockLibraryPickerForm(string initialCatalogPath)
        {
            catalogService = new QtoBlockCatalogService();
            SelectedCatalogPath = initialCatalogPath ?? string.Empty;

            Text = "插入標準圖塊";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(1080, 640);
            MinimumSize = new Size(900, 540);
            QtoUiTheme.ApplyForm(this);

            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.Padding = QtoUiTheme.FormPadding;
            root.ColumnCount = 1;
            root.RowCount = 5;
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 84));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            Controls.Add(root);

            TableLayoutPanel header = new TableLayoutPanel();
            header.Dock = DockStyle.Fill;
            header.ColumnCount = 1;
            header.RowCount = 2;
            header.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            header.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            root.Controls.Add(header, 0, 0);

            Label title = QtoUiTheme.CreateLabel("從圖塊資料庫插入標準設備", ContentAlignment.MiddleLeft);
            title.Font = QtoUiTheme.HeaderFont;
            header.Controls.Add(title, 0, 0);

            TableLayoutPanel pathRow = new TableLayoutPanel();
            pathRow.Dock = DockStyle.Bottom;
            pathRow.ColumnCount = 3;
            pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 126));
            pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 104));
            catalogPathTextBox = new TextBox();
            catalogPathTextBox.Dock = DockStyle.Fill;
            catalogPathTextBox.Text = SelectedCatalogPath;
            QtoUiTheme.ApplyReadOnlyTextBox(catalogPathTextBox);
            pathRow.Controls.Add(catalogPathTextBox, 0, 0);
            pathRow.Controls.Add(CreateActionButton("選擇圖例 DWG", BrowseButtonClick, QtoButtonRole.Secondary), 1, 0);
            pathRow.Controls.Add(CreateActionButton("更新圖塊", ReloadButtonClick, QtoButtonRole.Default), 2, 0);
            header.Controls.Add(pathRow, 0, 1);

            TableLayoutPanel filters = new TableLayoutPanel();
            filters.Dock = DockStyle.Fill;
            filters.Padding = new Padding(0, 4, 0, 4);
            filters.ColumnCount = 9;
            filters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 48));
            filters.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            filters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 52));
            filters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
            filters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70));
            filters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 125));
            filters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 52));
            filters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
            filters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112));
            filters.Controls.Add(QtoUiTheme.CreateLabel("搜尋", ContentAlignment.MiddleLeft), 0, 0);
            searchTextBox = new TextBox { Dock = DockStyle.Fill };
            searchTextBox.TextChanged += FilterChanged;
            filters.Controls.Add(searchTextBox, 1, 0);
            filters.Controls.Add(QtoUiTheme.CreateLabel("系統", ContentAlignment.MiddleLeft), 2, 0);
            systemFilterComboBox = CreateFilterCombo();
            systemFilterComboBox.SelectedIndexChanged += FilterChanged;
            filters.Controls.Add(systemFilterComboBox, 3, 0);
            filters.Controls.Add(QtoUiTheme.CreateLabel("設備類型", ContentAlignment.MiddleLeft), 4, 0);
            equipmentFilterComboBox = CreateFilterCombo();
            equipmentFilterComboBox.SelectedIndexChanged += FilterChanged;
            filters.Controls.Add(equipmentFilterComboBox, 5, 0);
            filters.Controls.Add(QtoUiTheme.CreateLabel("狀態", ContentAlignment.MiddleLeft), 6, 0);
            statusFilterComboBox = CreateFilterCombo();
            statusFilterComboBox.SelectedIndexChanged += FilterChanged;
            filters.Controls.Add(statusFilterComboBox, 7, 0);
            showUnconfiguredCheckBox = new CheckBox { Text = "顯示待設定", Checked = true, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
            showUnconfiguredCheckBox.CheckedChanged += FilterChanged;
            filters.Controls.Add(showUnconfiguredCheckBox, 8, 0);
            root.Controls.Add(filters, 0, 1);

            SplitContainer split = new SplitContainer();
            split.Dock = DockStyle.Fill;
            split.Orientation = Orientation.Vertical;
            split.Panel1MinSize = 80;
            split.Panel2MinSize = 80;
            split.SizeChanged += delegate { EnsureSafeSplitterDistance(split); };
            Shown += delegate { EnsureSafeSplitterDistance(split); };
            root.Controls.Add(split, 0, 2);

            grid = new DataGridView();
            grid.Dock = DockStyle.Fill;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.AllowUserToResizeRows = false;
            grid.ReadOnly = true;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.MultiSelect = false;
            grid.AutoGenerateColumns = false;
            grid.RowHeadersVisible = false;
            QtoUiTheme.ApplyGrid(grid);
            AddColumn("顯示名稱", "DisplayName", 180);
            AddColumn("圖塊名稱", "BlockName", 160);
            AddColumn("系統代碼", "SystemCode", 90);
            AddColumn("設備類型", "EquipmentTypeCode", 120);
            AddColumn("CAD 計量型態", "QtoType", 120);
            AddColumn("單位", "Unit", 70);
            AddColumn("狀態", "StatusDisplayName", 90);
            grid.SelectionChanged += GridSelectionChanged;
            split.Panel1.Controls.Add(grid);

            TableLayoutPanel previewPanel = new TableLayoutPanel();
            previewPanel.Dock = DockStyle.Fill;
            previewPanel.Padding = new Padding(8, 0, 0, 0);
            previewPanel.ColumnCount = 1;
            previewPanel.RowCount = 3;
            previewPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            previewPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            previewPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
            split.Panel2.Controls.Add(previewPanel);

            previewTitleLabel = QtoUiTheme.CreateLabel("圖塊預覽", ContentAlignment.MiddleLeft);
            previewTitleLabel.Font = QtoUiTheme.SectionFont;
            previewPanel.Controls.Add(previewTitleLabel, 0, 0);

            previewBox = new PictureBox();
            previewBox.Dock = DockStyle.Fill;
            previewBox.BorderStyle = BorderStyle.FixedSingle;
            previewBox.SizeMode = PictureBoxSizeMode.Zoom;
            previewPanel.Controls.Add(previewBox, 0, 1);

            previewMetaLabel = QtoUiTheme.CreateMutedLabel("選取圖塊後顯示預覽。");
            previewMetaLabel.TextAlign = ContentAlignment.TopLeft;
            previewPanel.Controls.Add(previewMetaLabel, 0, 2);

            statusLabel = QtoUiTheme.CreateMutedLabel("請選擇 catalog。");
            root.Controls.Add(statusLabel, 0, 3);

            TableLayoutPanel bottomBar = new TableLayoutPanel();
            bottomBar.Dock = DockStyle.Fill;
            bottomBar.ColumnCount = 2;
            bottomBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            bottomBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 270));
            root.Controls.Add(bottomBar, 0, 4);

            FlowLayoutPanel insertionOptions = new FlowLayoutPanel();
            insertionOptions.Dock = DockStyle.Fill;
            insertionOptions.FlowDirection = FlowDirection.LeftToRight;
            insertionOptions.WrapContents = false;
            insertionOptions.Padding = new Padding(0, 8, 0, 0);
            insertionOptions.Controls.Add(QtoUiTheme.CreateMutedLabel("插入基準："));
            insertByOriginRadio = CreateRadioButton("依圖塊原點", true);
            insertByCenterRadio = CreateRadioButton("依圖形中心", false);
            insertionOptions.Controls.Add(insertByOriginRadio);
            insertionOptions.Controls.Add(insertByCenterRadio);
            bottomBar.Controls.Add(insertionOptions, 0, 0);

            FlowLayoutPanel actions = new FlowLayoutPanel();
            actions.Dock = DockStyle.Fill;
            actions.FlowDirection = FlowDirection.RightToLeft;
            actions.WrapContents = false;
            actions.Controls.Add(CreateActionButton("取消", CancelButtonClick, QtoButtonRole.Default));
            actions.Controls.Add(CreateActionButton("插入選取圖塊", InsertButtonClick, QtoButtonRole.Primary));
            bottomBar.Controls.Add(actions, 1, 0);

            LoadCatalogFromPath(SelectedCatalogPath);
        }

        public string SelectedCatalogPath { get; private set; }
        public QtoBlockCatalog SelectedCatalog { get { return catalog; } }
        public QtoBlockCatalogItem SelectedItem { get; private set; }
        public int DisplayedItemCount { get { return grid.Rows.Count; } }
        public QtoBlockInsertionBaseMode SelectedInsertionBaseMode
        {
            get { return insertByCenterRadio.Checked ? QtoBlockInsertionBaseMode.Center : QtoBlockInsertionBaseMode.Origin; }
        }

        private void AddColumn(string header, string propertyName, int width)
        {
            DataGridViewTextBoxColumn column = new DataGridViewTextBoxColumn();
            column.HeaderText = header;
            column.DataPropertyName = propertyName;
            column.Width = width;
            grid.Columns.Add(column);
        }

        private Button CreateActionButton(string text, EventHandler handler, QtoButtonRole role)
        {
            Button button = QtoUiTheme.CreateButton(text, handler, role);
            button.Width = 118;
            button.Height = 32;
            return button;
        }

        private RadioButton CreateRadioButton(string text, bool isChecked)
        {
            RadioButton radioButton = new RadioButton();
            radioButton.Text = text;
            radioButton.Checked = isChecked;
            radioButton.AutoSize = true;
            radioButton.Margin = new Padding(8, 4, 8, 4);
            radioButton.ForeColor = QtoUiTheme.TextColor;
            return radioButton;
        }

        private static ComboBox CreateFilterCombo()
        {
            return new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
        }

        private static void EnsureSafeSplitterDistance(SplitContainer split)
        {
            if (split == null || !split.IsHandleCreated || split.Width <= 0)
            {
                return;
            }

            int min = Math.Max(split.Panel1MinSize, 1);
            int max = split.Width - Math.Max(split.Panel2MinSize, 1);
            if (max <= min)
            {
                return;
            }

            int desired = (int)Math.Round(split.Width * 0.68);
            desired = Math.Max(min, Math.Min(desired, max));
            if (desired > min && desired < max && split.SplitterDistance != desired)
            {
                try
                {
                    split.SplitterDistance = desired;
                }
                catch (InvalidOperationException)
                {
                }
                catch (ArgumentException)
                {
                }
            }
        }

        private void BrowseButtonClick(object sender, EventArgs e)
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "選擇標準圖例 DWG";
                dialog.Filter = "AutoCAD 圖例 DWG (*.dwg)|*.dwg|既有 QTO 索引 (*.json)|*.json";
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                if (string.Equals(Path.GetExtension(dialog.FileName), ".dwg", StringComparison.OrdinalIgnoreCase))
                {
                    BuildCatalogFromLegendDwg(dialog.FileName);
                }
                else
                {
                    LoadCatalogFromPath(dialog.FileName);
                }
            }
        }

        private void ReloadButtonClick(object sender, EventArgs e)
        {
            string sourceDwgPath = catalog == null ? string.Empty : catalog.SourceLegendDwg ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(sourceDwgPath) && File.Exists(sourceDwgPath))
            {
                BuildCatalogFromLegendDwg(sourceDwgPath);
                return;
            }

            LoadCatalogFromPath(SelectedCatalogPath);
        }

        private void BuildCatalogFromLegendDwg(string legendDwgPath)
        {
            try
            {
                string catalogPath = !string.IsNullOrWhiteSpace(SelectedCatalogPath)
                    && string.Equals(Path.GetExtension(SelectedCatalogPath), ".json", StringComparison.OrdinalIgnoreCase)
                    ? SelectedCatalogPath
                    : catalogService.GetCatalogPathForLegendDwg(legendDwgPath);
                QtoBlockCatalogOperationResult result = catalogService.RescanLegendAndSave(legendDwgPath, catalogPath);
                LoadCatalogFromPath(catalogPath);
                statusLabel.Text = result.UserMessage + " JSON 索引已由外掛自動更新。";
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "無法從圖例 DWG 建立圖塊清單。\r\n\r\n" + ex.Message, "更新圖塊失敗", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void InsertButtonClick(object sender, EventArgs e)
        {
            if (grid.CurrentRow == null)
            {
                MessageBox.Show(this, "請先選擇要插入的圖塊。", "插入標準圖塊", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            SelectedItem = grid.CurrentRow.DataBoundItem as QtoBlockCatalogItem;
            if (SelectedItem == null)
            {
                MessageBox.Show(this, "選取列不是有效的圖塊資料。", "插入標準圖塊", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string normalizedStatus = QtoBlockCatalogStatus.Normalize(SelectedItem.Status);
            if (string.Equals(normalizedStatus, QtoBlockCatalogStatus.Unconfigured, StringComparison.OrdinalIgnoreCase))
            {
                DialogResult confirm = MessageBox.Show(this, "這個圖塊尚未完成分類，插入後會列入檢查清單。是否仍要插入？", "插入待設定圖塊", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (confirm != DialogResult.Yes)
                {
                    return;
                }
            }
            else if (!string.Equals(normalizedStatus, QtoBlockCatalogStatus.Active, StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show(this, "目前狀態為「" + QtoBlockCatalogStatus.GetDisplayName(normalizedStatus) + "」，請先到圖塊庫管理確認後再插入。", "無法插入圖塊", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (string.Equals(normalizedStatus, QtoBlockCatalogStatus.Active, StringComparison.OrdinalIgnoreCase) && !HasRequiredFields(SelectedItem))
            {
                MessageBox.Show(this, "這個圖塊雖標示為使用中，但必要分類欄位不完整。請先到圖塊庫管理補齊。", "圖塊資料不完整", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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

        private void LoadCatalogFromPath(string path)
        {
            SelectedCatalogPath = path ?? string.Empty;
            catalog = catalogService.LoadCatalog(SelectedCatalogPath);
            catalogPathTextBox.Text = !string.IsNullOrWhiteSpace(catalog.SourceLegendDwg)
                ? catalog.SourceLegendDwg
                : SelectedCatalogPath;

            allRows = catalog.Items
                .Where(item => item != null && !string.IsNullOrWhiteSpace(item.BlockName))
                .OrderBy(item => item.SystemCode ?? string.Empty)
                .ThenBy(item => item.DisplayName ?? item.BlockName)
                .ToList();
            RefreshFilterChoices();
            ApplyFilters();
            UpdatePreview();
        }

        private void RefreshFilterChoices()
        {
            SetFilterItems(systemFilterComboBox, "全部系統", allRows.Select(item => item.SystemCode));
            SetFilterItems(equipmentFilterComboBox, "全部設備類型", allRows.Select(item => item.EquipmentTypeCode));
            statusFilterComboBox.Items.Clear();
            statusFilterComboBox.Items.AddRange(new object[] { "可插入", "全部狀態", "使用中", "未設定", "有更新", "有衝突", "找不到來源", "已停用" });
            statusFilterComboBox.SelectedIndex = 0;
        }

        private static void SetFilterItems(ComboBox combo, string allLabel, IEnumerable<string> values)
        {
            combo.Items.Clear();
            combo.Items.Add(allLabel);
            foreach (string value in values.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value))
            {
                combo.Items.Add(value);
            }
            combo.SelectedIndex = 0;
        }

        private void ApplyFilters()
        {
            if (allRows == null)
            {
                return;
            }

            string query = (searchTextBox.Text ?? string.Empty).Trim();
            string system = Convert.ToString(systemFilterComboBox.SelectedItem) ?? "全部系統";
            string equipment = Convert.ToString(equipmentFilterComboBox.SelectedItem) ?? "全部設備類型";
            string statusDisplay = Convert.ToString(statusFilterComboBox.SelectedItem) ?? "可插入";
            string statusCode = StatusCodeFromDisplay(statusDisplay);

            List<QtoBlockCatalogItem> rows = allRows
                .Where(item => MatchesQuery(item, query))
                .Where(item => system == "全部系統" || string.Equals(item.SystemCode, system, StringComparison.OrdinalIgnoreCase))
                .Where(item => equipment == "全部設備類型" || string.Equals(item.EquipmentTypeCode, equipment, StringComparison.OrdinalIgnoreCase))
                .Where(item => MatchesStatus(item, statusDisplay, statusCode))
                .ToList();

            grid.DataSource = rows;
            statusLabel.Text = "顯示 " + rows.Count.ToString("0") + " / " + allRows.Count.ToString("0") + " 個圖塊。";
            UpdatePreview();
        }

        private bool MatchesStatus(QtoBlockCatalogItem item, string display, string code)
        {
            string itemStatus = QtoBlockCatalogStatus.Normalize(item.Status);
            if (display == "可插入")
            {
                return string.Equals(itemStatus, QtoBlockCatalogStatus.Active, StringComparison.OrdinalIgnoreCase)
                    || (showUnconfiguredCheckBox.Checked && string.Equals(itemStatus, QtoBlockCatalogStatus.Unconfigured, StringComparison.OrdinalIgnoreCase));
            }
            if (display == "全部狀態")
            {
                return showUnconfiguredCheckBox.Checked || !string.Equals(itemStatus, QtoBlockCatalogStatus.Unconfigured, StringComparison.OrdinalIgnoreCase);
            }
            return string.Equals(itemStatus, code, StringComparison.OrdinalIgnoreCase);
        }

        private static bool MatchesQuery(QtoBlockCatalogItem item, string query)
        {
            return string.IsNullOrWhiteSpace(query)
                || Contains(item.DisplayName, query)
                || Contains(item.BlockName, query)
                || Contains(item.SystemCode, query)
                || Contains(item.EquipmentTypeCode, query);
        }

        private static bool Contains(string value, string query)
        {
            return !string.IsNullOrWhiteSpace(value) && value.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string StatusCodeFromDisplay(string display)
        {
            switch (display)
            {
                case "使用中": return QtoBlockCatalogStatus.Active;
                case "未設定": return QtoBlockCatalogStatus.Unconfigured;
                case "有更新": return QtoBlockCatalogStatus.Updated;
                case "有衝突": return QtoBlockCatalogStatus.Conflict;
                case "找不到來源": return QtoBlockCatalogStatus.Missing;
                case "已停用": return QtoBlockCatalogStatus.Deprecated;
                default: return string.Empty;
            }
        }

        private static bool HasRequiredFields(QtoBlockCatalogItem item)
        {
            return item != null
                && !string.IsNullOrWhiteSpace(item.DisplayName)
                && !string.IsNullOrWhiteSpace(item.SystemCode)
                && !string.IsNullOrWhiteSpace(item.EquipmentTypeCode)
                && !string.IsNullOrWhiteSpace(item.QtoType)
                && !string.IsNullOrWhiteSpace(item.QuantityBasis)
                && !string.IsNullOrWhiteSpace(item.Unit);
        }

        private void FilterChanged(object sender, EventArgs e)
        {
            ApplyFilters();
        }

        private void GridSelectionChanged(object sender, EventArgs e)
        {
            UpdatePreview();
        }

        private void UpdatePreview()
        {
            QtoBlockCatalogItem item = grid.CurrentRow == null ? null : grid.CurrentRow.DataBoundItem as QtoBlockCatalogItem;
            if (item == null)
            {
                SetPreviewImage(QtoBlockPreviewRenderer.RenderBlockPreview(string.Empty, string.Empty, previewBox.Width, previewBox.Height));
                previewTitleLabel.Text = "圖塊預覽";
                previewMetaLabel.Text = "選取圖塊後顯示預覽。";
                return;
            }

            string sourceDwgPath = ResolveSourceDwgPath(item);
            SetPreviewImage(QtoBlockPreviewRenderer.RenderBlockPreview(sourceDwgPath, item.BlockName, previewBox.Width, previewBox.Height));
            previewTitleLabel.Text = string.IsNullOrWhiteSpace(item.DisplayName) ? item.BlockName : item.DisplayName;
            previewMetaLabel.Text = "圖塊：" + (item.BlockName ?? string.Empty)
                + "\r\n來源：" + (string.IsNullOrWhiteSpace(sourceDwgPath) ? "未設定" : sourceDwgPath);
        }

        private void SetPreviewImage(Image image)
        {
            Image oldImage = previewBox.Image;
            previewBox.Image = image;
            if (oldImage != null)
            {
                oldImage.Dispose();
            }
        }

        private string ResolveSourceDwgPath(QtoBlockCatalogItem item)
        {
            if (item != null && !string.IsNullOrWhiteSpace(item.SourceLegendDwg))
            {
                return item.SourceLegendDwg;
            }

            return catalog == null ? string.Empty : catalog.SourceLegendDwg ?? string.Empty;
        }
    }
}
