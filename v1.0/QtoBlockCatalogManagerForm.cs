using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace QtoWirePlugin
{
    public sealed class QtoBlockCatalogManagerForm : Form
    {
        private readonly QtoBlockCatalogService catalogService;
        private readonly QtoDictionaryStore dictionary;
        private readonly TextBox catalogPathTextBox;
        private readonly TextBox searchTextBox;
        private readonly ComboBox systemFilterComboBox;
        private readonly ComboBox statusFilterComboBox;
        private readonly CheckBox showUnconfiguredCheckBox;
        private readonly DataGridView grid;
        private readonly Label statusLabel;
        private readonly PictureBox previewBox;
        private readonly Label previewTitleLabel;
        private readonly Label previewMetaLabel;
        private readonly TextBox displayNameTextBox;
        private readonly TextBox blockNameTextBox;
        private readonly ComboBox systemCodeComboBox;
        private readonly ComboBox equipmentTypeComboBox;
        private readonly ComboBox qtoTypeComboBox;
        private readonly TextBox quantityBasisTextBox;
        private readonly TextBox unitTextBox;
        private readonly TextBox defaultLayerTextBox;
        private readonly TextBox cableTypeTextBox;
        private readonly TextBox conduitTypeTextBox;
        private readonly TextBox conduitSizeTextBox;
        private readonly TextBox budgetItemKeyTextBox;
        private readonly TextBox remarkTextBox;
        private readonly ComboBox statusComboBox;
        private QtoBlockCatalog catalog;
        private bool loadingSelection;
        private bool dirty;
        private bool closingConfirmed;

        public QtoBlockCatalogManagerForm(string initialCatalogPath, QtoDictionaryStore dictionaryStore)
        {
            catalogService = new QtoBlockCatalogService();
            dictionary = dictionaryStore ?? new QtoDictionaryStore();
            CatalogPath = initialCatalogPath ?? string.Empty;

            Text = "QTO 圖塊庫管理";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(1240, 760);
            MinimumSize = new Size(980, 620);
            AutoScaleMode = AutoScaleMode.Dpi;
            QtoUiTheme.ApplyForm(this);

            TableLayoutPanel root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = QtoUiTheme.FormPadding,
                ColumnCount = 1,
                RowCount = 5
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 86));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            Controls.Add(root);

            TableLayoutPanel header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            header.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            header.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            Label title = QtoUiTheme.CreateLabel("管理標準圖塊資料與 QTO 預設值", ContentAlignment.MiddleLeft);
            title.Font = QtoUiTheme.HeaderFont;
            header.Controls.Add(title, 0, 0);

            TableLayoutPanel pathRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4 };
            pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 126));
            pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 104));
            pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 104));
            catalogPathTextBox = new TextBox { Dock = DockStyle.Fill, ReadOnly = true, Text = CatalogPath };
            QtoUiTheme.ApplyReadOnlyTextBox(catalogPathTextBox);
            pathRow.Controls.Add(catalogPathTextBox, 0, 0);
            pathRow.Controls.Add(CreateButton("選擇圖例 DWG", ChooseCatalogClick, QtoButtonRole.Secondary), 1, 0);
            pathRow.Controls.Add(CreateButton("更新圖塊", RescanClick, QtoButtonRole.Default), 2, 0);
            pathRow.Controls.Add(CreateButton("儲存設定", SaveClick, QtoButtonRole.Primary), 3, 0);
            header.Controls.Add(pathRow, 0, 1);
            root.Controls.Add(header, 0, 0);

            TableLayoutPanel filters = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 7, Padding = new Padding(0, 4, 0, 4) };
            filters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 52));
            filters.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            filters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 58));
            filters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
            filters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 58));
            filters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
            filters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
            filters.Controls.Add(QtoUiTheme.CreateLabel("搜尋", ContentAlignment.MiddleLeft), 0, 0);
            searchTextBox = new TextBox { Dock = DockStyle.Fill };
            searchTextBox.TextChanged += FilterChanged;
            filters.Controls.Add(searchTextBox, 1, 0);
            filters.Controls.Add(QtoUiTheme.CreateLabel("系統", ContentAlignment.MiddleLeft), 2, 0);
            systemFilterComboBox = CreateFilterCombo();
            systemFilterComboBox.SelectedIndexChanged += FilterChanged;
            filters.Controls.Add(systemFilterComboBox, 3, 0);
            filters.Controls.Add(QtoUiTheme.CreateLabel("狀態", ContentAlignment.MiddleLeft), 4, 0);
            statusFilterComboBox = CreateFilterCombo();
            statusFilterComboBox.SelectedIndexChanged += FilterChanged;
            filters.Controls.Add(statusFilterComboBox, 5, 0);
            showUnconfiguredCheckBox = new CheckBox { Text = "顯示待設定", Checked = true, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
            showUnconfiguredCheckBox.CheckedChanged += FilterChanged;
            filters.Controls.Add(showUnconfiguredCheckBox, 6, 0);
            root.Controls.Add(filters, 0, 1);

            SplitContainer split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Vertical, Panel1MinSize = 80, Panel2MinSize = 80 };
            split.SizeChanged += delegate { EnsureSafeSplitterDistance(split); };
            Shown += delegate { EnsureSafeSplitterDistance(split); };
            root.Controls.Add(split, 0, 2);

            grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = true,
                AutoGenerateColumns = false,
                RowHeadersVisible = false
            };
            QtoUiTheme.ApplyGrid(grid);
            AddGridColumn("顯示名稱", "DisplayName", 180);
            AddGridColumn("圖塊名稱", "BlockName", 160);
            AddGridColumn("系統", "SystemCode", 80);
            AddGridColumn("設備類型", "EquipmentTypeCode", 120);
            AddGridColumn("計量型態", "QtoType", 110);
            AddGridColumn("狀態", "StatusDisplayName", 90);
            grid.SelectionChanged += GridSelectionChanged;
            split.Panel1.Controls.Add(grid);

            TableLayoutPanel rightHost = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Padding = new Padding(10, 0, 0, 0) };
            rightHost.RowStyles.Add(new RowStyle(SizeType.Absolute, 230));
            rightHost.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            split.Panel2.Controls.Add(rightHost);

            TableLayoutPanel previewPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(0, 0, 8, 8) };
            previewPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            previewPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            previewPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            previewTitleLabel = QtoUiTheme.CreateLabel("圖塊預覽", ContentAlignment.MiddleLeft);
            previewTitleLabel.Font = QtoUiTheme.SectionFont;
            previewBox = new PictureBox { Dock = DockStyle.Fill, BorderStyle = BorderStyle.FixedSingle, SizeMode = PictureBoxSizeMode.Zoom };
            previewMetaLabel = QtoUiTheme.CreateMutedLabel("選取圖塊後顯示預覽。");
            previewMetaLabel.TextAlign = ContentAlignment.TopLeft;
            previewPanel.Controls.Add(previewTitleLabel, 0, 0);
            previewPanel.Controls.Add(previewBox, 0, 1);
            previewPanel.Controls.Add(previewMetaLabel, 0, 2);
            rightHost.Controls.Add(previewPanel, 0, 0);

            Panel propertyHost = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
            TableLayoutPanel properties = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, Padding = new Padding(0, 0, 8, 8) };
            properties.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 118));
            properties.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            propertyHost.Controls.Add(properties);
            rightHost.Controls.Add(propertyHost, 0, 1);

            blockNameTextBox = AddTextField(properties, "圖塊名稱", true);
            displayNameTextBox = AddTextField(properties, "顯示名稱", false);
            systemCodeComboBox = AddComboField(properties, "系統代碼", QtoSystemDefaults.OrderValues(dictionary.SystemCodes));
            equipmentTypeComboBox = AddComboField(properties, "設備類型", dictionary.EquipmentTypes.Keys.OrderBy(value => value).ToArray());
            qtoTypeComboBox = AddComboField(properties, "CAD 計量型態", GetQtoTypes());
            quantityBasisTextBox = AddTextField(properties, "數量依據", false);
            unitTextBox = AddTextField(properties, "單位", false);
            defaultLayerTextBox = AddTextField(properties, "預設圖層", false);
            cableTypeTextBox = AddTextField(properties, "預設線材", false);
            conduitTypeTextBox = AddTextField(properties, "預設管材", false);
            conduitSizeTextBox = AddTextField(properties, "預設管徑", false);
            budgetItemKeyTextBox = AddTextField(properties, "預算候選鍵", false);
            remarkTextBox = AddTextField(properties, "備註", false);
            statusComboBox = AddComboField(properties, "狀態", GetStatusDisplayNames());

            FlowLayoutPanel propertyActions = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 8, 0, 0) };
            propertyActions.Controls.Add(CreateButton("套用目前資料", ApplyCurrentClick, QtoButtonRole.Primary));
            propertyActions.Controls.Add(CreateButton("批次設定", BatchEditClick, QtoButtonRole.Secondary));
            AddFullRow(properties, propertyActions);

            statusLabel = QtoUiTheme.CreateMutedLabel("尚未載入圖塊資料庫。");
            root.Controls.Add(statusLabel, 0, 3);

            FlowLayoutPanel bottom = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
            bottom.Controls.Add(CreateButton("關閉", CloseClick, QtoButtonRole.Default));
            root.Controls.Add(bottom, 0, 4);

            FormClosed += delegate { SetPreviewImage(null); };

            LoadCatalog(CatalogPath);
        }

        public string CatalogPath { get; private set; }

        private void LoadCatalog(string path)
        {
            CatalogPath = path ?? string.Empty;
            catalog = catalogService.LoadCatalog(CatalogPath);
            catalogPathTextBox.Text = !string.IsNullOrWhiteSpace(catalog.SourceLegendDwg)
                ? catalog.SourceLegendDwg
                : CatalogPath;
            dirty = false;
            RefreshFilterChoices();
            ApplyFilters();
        }

        private void RefreshFilterChoices()
        {
            string previousSystem = Convert.ToString(systemFilterComboBox.SelectedItem) ?? "全部系統";
            systemFilterComboBox.Items.Clear();
            systemFilterComboBox.Items.Add("全部系統");
            foreach (string code in catalog.Items.Where(item => item != null).Select(item => item.SystemCode).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value))
            {
                systemFilterComboBox.Items.Add(code);
            }
            systemFilterComboBox.SelectedItem = systemFilterComboBox.Items.Contains(previousSystem) ? previousSystem : "全部系統";

            statusFilterComboBox.Items.Clear();
            statusFilterComboBox.Items.Add("全部狀態");
            foreach (string display in GetStatusDisplayNames())
            {
                statusFilterComboBox.Items.Add(display);
            }
            statusFilterComboBox.SelectedIndex = 0;
        }

        private void ApplyFilters()
        {
            if (catalog == null)
            {
                return;
            }

            string query = (searchTextBox.Text ?? string.Empty).Trim();
            string system = Convert.ToString(systemFilterComboBox.SelectedItem) ?? "全部系統";
            string status = StatusCodeFromDisplay(Convert.ToString(statusFilterComboBox.SelectedItem));

            List<QtoBlockCatalogItem> rows = catalog.Items
                .Where(item => item != null && !string.IsNullOrWhiteSpace(item.BlockName))
                .Where(item => showUnconfiguredCheckBox.Checked || !string.Equals(QtoBlockCatalogStatus.Normalize(item.Status), QtoBlockCatalogStatus.Unconfigured, StringComparison.OrdinalIgnoreCase))
                .Where(item => system == "全部系統" || string.Equals(item.SystemCode, system, StringComparison.OrdinalIgnoreCase))
                .Where(item => string.IsNullOrWhiteSpace(status) || string.Equals(QtoBlockCatalogStatus.Normalize(item.Status), status, StringComparison.OrdinalIgnoreCase))
                .Where(item => MatchesQuery(item, query))
                .OrderBy(item => item.SystemCode ?? string.Empty)
                .ThenBy(item => item.DisplayName ?? item.BlockName)
                .ToList();

            grid.DataSource = rows;
            statusLabel.Text = "顯示 " + rows.Count.ToString("0") + " / " + catalog.Items.Count.ToString("0") + " 個圖塊" + (dirty ? "，有尚未儲存的變更。" : "。");
        }

        private static bool MatchesQuery(QtoBlockCatalogItem item, string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return true;
            }

            return Contains(item.DisplayName, query) || Contains(item.BlockName, query) || Contains(item.SystemCode, query) || Contains(item.EquipmentTypeCode, query);
        }

        private void GridSelectionChanged(object sender, EventArgs e)
        {
            if (loadingSelection)
            {
                return;
            }

            List<QtoBlockCatalogItem> selected = GetSelectedItems();
            if (selected.Count != 1)
            {
                ClearEditor();
                UpdatePreview(null);
                if (selected.Count > 1)
                {
                    statusLabel.Text = "已選取 " + selected.Count.ToString("0") + " 個圖塊，可使用批次設定。";
                }
                return;
            }

            LoadEditor(selected[0]);
            UpdatePreview(selected[0]);
        }

        private void UpdatePreview(QtoBlockCatalogItem item)
        {
            if (item == null)
            {
                SetPreviewImage(QtoBlockPreviewRenderer.RenderBlockPreview(string.Empty, string.Empty, previewBox.Width, previewBox.Height));
                previewTitleLabel.Text = "圖塊預覽";
                previewMetaLabel.Text = "選取圖塊後顯示預覽。";
                return;
            }

            string sourceDwgPath = !string.IsNullOrWhiteSpace(item.SourceLegendDwg)
                ? item.SourceLegendDwg
                : catalog == null ? string.Empty : catalog.SourceLegendDwg ?? string.Empty;
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

        private void LoadEditor(QtoBlockCatalogItem item)
        {
            loadingSelection = true;
            blockNameTextBox.Text = item.BlockName ?? string.Empty;
            displayNameTextBox.Text = item.DisplayName ?? string.Empty;
            systemCodeComboBox.Text = item.SystemCode ?? string.Empty;
            equipmentTypeComboBox.Text = item.EquipmentTypeCode ?? string.Empty;
            qtoTypeComboBox.Text = item.QtoType ?? string.Empty;
            quantityBasisTextBox.Text = item.QuantityBasis ?? string.Empty;
            unitTextBox.Text = item.Unit ?? string.Empty;
            defaultLayerTextBox.Text = item.DefaultLayer ?? string.Empty;
            cableTypeTextBox.Text = item.DefaultCableType ?? string.Empty;
            conduitTypeTextBox.Text = item.DefaultConduitType ?? string.Empty;
            conduitSizeTextBox.Text = item.DefaultConduitSize ?? string.Empty;
            budgetItemKeyTextBox.Text = item.BudgetItemKey ?? string.Empty;
            remarkTextBox.Text = item.Remark ?? string.Empty;
            statusComboBox.Text = QtoBlockCatalogStatus.GetDisplayName(item.Status);
            loadingSelection = false;
        }

        private void ApplyCurrentClick(object sender, EventArgs e)
        {
            List<QtoBlockCatalogItem> selected = GetSelectedItems();
            if (selected.Count != 1)
            {
                MessageBox.Show(this, "請選擇一個圖塊後再套用目前資料；多選時請使用批次設定。", "圖塊庫管理", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            QtoBlockCatalogItem item = selected[0];
            item.DisplayName = displayNameTextBox.Text.Trim();
            item.SystemCode = systemCodeComboBox.Text.Trim();
            item.EquipmentTypeCode = equipmentTypeComboBox.Text.Trim();
            item.QtoType = qtoTypeComboBox.Text.Trim();
            item.QuantityBasis = quantityBasisTextBox.Text.Trim();
            item.Unit = unitTextBox.Text.Trim();
            item.DefaultLayer = defaultLayerTextBox.Text.Trim();
            item.DefaultCableType = cableTypeTextBox.Text.Trim();
            item.DefaultConduitType = conduitTypeTextBox.Text.Trim();
            item.DefaultConduitSize = conduitSizeTextBox.Text.Trim();
            item.BudgetItemKey = budgetItemKeyTextBox.Text.Trim();
            item.Remark = remarkTextBox.Text.Trim();
            item.Status = StatusCodeFromDisplay(statusComboBox.Text);

            string validationMessage;
            if (!ValidateActiveItem(item, out validationMessage))
            {
                item.Status = QtoBlockCatalogStatus.Unconfigured;
                item.RefreshStatusDisplayName();
                statusComboBox.Text = item.StatusDisplayName;
                MessageBox.Show(this, validationMessage + "\r\n\r\n狀態已保留為未設定。", "圖塊資料不完整", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            else
            {
                item.RefreshStatusDisplayName();
            }

            item.LastUpdatedAt = DateTime.Now;
            dirty = true;
            RefreshFilterChoices();
            ApplyFilters();
        }

        private void BatchEditClick(object sender, EventArgs e)
        {
            List<QtoBlockCatalogItem> selected = GetSelectedItems();
            if (selected.Count < 2)
            {
                MessageBox.Show(this, "請先在清單中選取兩個以上的圖塊。", "批次設定", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (QtoBlockCatalogBatchEditForm form = new QtoBlockCatalogBatchEditForm(dictionary))
            {
                if (form.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                foreach (QtoBlockCatalogItem item in selected)
                {
                    form.ApplyTo(item);
                    string validationMessage;
                    if (!ValidateActiveItem(item, out validationMessage))
                    {
                        item.Status = QtoBlockCatalogStatus.Unconfigured;
                    }
                    item.LastUpdatedAt = DateTime.Now;
                    item.RefreshStatusDisplayName();
                }
            }

            dirty = true;
            RefreshFilterChoices();
            ApplyFilters();
        }

        private void SaveClick(object sender, EventArgs e)
        {
            if (GetSelectedItems().Count == 1)
            {
                ApplyCurrentClick(sender, e);
            }

            if (!EnsureCatalogPath())
            {
                return;
            }

            foreach (QtoBlockCatalogItem item in catalog.Items)
            {
                string message;
                if (!ValidateActiveItem(item, out message))
                {
                    item.Status = QtoBlockCatalogStatus.Unconfigured;
                    item.RefreshStatusDisplayName();
                }
            }

            catalogService.SaveCatalog(catalog, CatalogPath);
            dirty = false;
            ApplyFilters();
            MessageBox.Show(this, "圖塊資料庫已儲存。", "圖塊庫管理", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void ChooseCatalogClick(object sender, EventArgs e)
        {
            if (!EnsureChangesSaved())
            {
                return;
            }

            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "選擇標準圖例 DWG";
                dialog.Filter = "AutoCAD 圖例 DWG (*.dwg)|*.dwg|既有 QTO 索引 (*.json)|*.json";
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    if (string.Equals(Path.GetExtension(dialog.FileName), ".dwg", StringComparison.OrdinalIgnoreCase))
                    {
                        BuildCatalogFromLegendDwg(dialog.FileName);
                    }
                    else
                    {
                        LoadCatalog(dialog.FileName);
                    }
                }
            }
        }

        private void RescanClick(object sender, EventArgs e)
        {
            string sourceDwgPath = catalog == null ? string.Empty : catalog.SourceLegendDwg ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(sourceDwgPath) && File.Exists(sourceDwgPath))
            {
                BuildCatalogFromLegendDwg(sourceDwgPath);
                return;
            }

            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "選擇標準圖例 DWG";
                dialog.Filter = "AutoCAD 圖例 DWG (*.dwg)|*.dwg";
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    BuildCatalogFromLegendDwg(dialog.FileName);
                }
            }
        }

        private void BuildCatalogFromLegendDwg(string legendDwgPath)
        {
            try
            {
                string targetCatalogPath = !string.IsNullOrWhiteSpace(CatalogPath)
                    && string.Equals(Path.GetExtension(CatalogPath), ".json", StringComparison.OrdinalIgnoreCase)
                    ? CatalogPath
                    : catalogService.GetCatalogPathForLegendDwg(legendDwgPath);
                if (dirty && !string.IsNullOrWhiteSpace(CatalogPath))
                {
                    catalogService.SaveCatalog(catalog, CatalogPath);
                    dirty = false;
                }

                QtoBlockCatalogOperationResult result = catalogService.RescanLegendAndSave(legendDwgPath, targetCatalogPath);
                CatalogPath = targetCatalogPath;
                LoadCatalog(CatalogPath);
                MessageBox.Show(this, result.UserMessage + "\r\n\r\n分類設定會自動保存在背景索引，不需要手動管理 JSON。", "圖塊清單已更新", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "無法從圖例 DWG 建立圖塊清單。\r\n\r\n" + ex.Message, "更新圖塊失敗", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private bool EnsureCatalogPath()
        {
            if (!string.IsNullOrWhiteSpace(CatalogPath))
            {
                return true;
            }

            MessageBox.Show(this, "請先選擇標準圖例 DWG。外掛會自動建立背景索引，不需要另存 JSON。", "尚未選擇圖例 DWG", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return false;
        }

        private void CloseClick(object sender, EventArgs e)
        {
            Close();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!closingConfirmed && dirty)
            {
                DialogResult result = MessageBox.Show(this, "尚有未儲存的變更，是否先儲存？", "圖塊庫管理", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                if (result == DialogResult.Cancel)
                {
                    e.Cancel = true;
                    return;
                }
                if (result == DialogResult.Yes)
                {
                    SaveClick(this, EventArgs.Empty);
                    if (dirty)
                    {
                        e.Cancel = true;
                        return;
                    }
                }
            }

            closingConfirmed = true;
            base.OnFormClosing(e);
        }

        private bool EnsureChangesSaved()
        {
            if (!dirty)
            {
                return true;
            }

            DialogResult result = MessageBox.Show(this, "切換資料庫前是否儲存目前變更？", "圖塊庫管理", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            if (result == DialogResult.Cancel)
            {
                return false;
            }
            if (result == DialogResult.Yes)
            {
                SaveClick(this, EventArgs.Empty);
                return !dirty;
            }
            return true;
        }

        private static bool ValidateActiveItem(QtoBlockCatalogItem item, out string message)
        {
            message = string.Empty;
            if (item == null || !string.Equals(QtoBlockCatalogStatus.Normalize(item.Status), QtoBlockCatalogStatus.Active, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            List<string> missing = new List<string>();
            if (string.IsNullOrWhiteSpace(item.DisplayName)) missing.Add("顯示名稱");
            if (string.IsNullOrWhiteSpace(item.SystemCode)) missing.Add("系統代碼");
            if (string.IsNullOrWhiteSpace(item.EquipmentTypeCode)) missing.Add("設備類型");
            if (string.IsNullOrWhiteSpace(item.QtoType)) missing.Add("CAD 計量型態");
            if (string.IsNullOrWhiteSpace(item.QuantityBasis)) missing.Add("數量依據");
            if (string.IsNullOrWhiteSpace(item.Unit)) missing.Add("單位");
            if (missing.Count == 0)
            {
                return true;
            }

            message = "設定為使用中前，請補齊：" + string.Join("、", missing.ToArray());
            return false;
        }

        private List<QtoBlockCatalogItem> GetSelectedItems()
        {
            return grid.SelectedRows.Cast<DataGridViewRow>()
                .Select(row => row.DataBoundItem as QtoBlockCatalogItem)
                .Where(item => item != null)
                .Distinct()
                .ToList();
        }

        private void ClearEditor()
        {
            loadingSelection = true;
            foreach (Control control in new Control[] { blockNameTextBox, displayNameTextBox, systemCodeComboBox, equipmentTypeComboBox, qtoTypeComboBox, quantityBasisTextBox, unitTextBox, defaultLayerTextBox, cableTypeTextBox, conduitTypeTextBox, conduitSizeTextBox, budgetItemKeyTextBox, remarkTextBox, statusComboBox })
            {
                control.Text = string.Empty;
            }
            loadingSelection = false;
        }

        private static string[] GetQtoTypes()
        {
            return new[] { QtoXDataHelper.TypeOutlet, QtoXDataHelper.TypeJunctionBox, QtoXDataHelper.TypeWire, QtoXDataHelper.TypeConduitSegment, QtoXDataHelper.TypeTray, QtoXDataHelper.TypeDevice, QtoXDataHelper.TypePanel };
        }

        private static string[] GetStatusDisplayNames()
        {
            return new[] { "使用中", "未設定", "有更新", "有衝突", "找不到來源", "已停用" };
        }

        private static string StatusCodeFromDisplay(string display)
        {
            switch ((display ?? string.Empty).Trim())
            {
                case "使用中": return QtoBlockCatalogStatus.Active;
                case "有更新": return QtoBlockCatalogStatus.Updated;
                case "有衝突": return QtoBlockCatalogStatus.Conflict;
                case "找不到來源": return QtoBlockCatalogStatus.Missing;
                case "已停用": return QtoBlockCatalogStatus.Deprecated;
                case "全部狀態": return string.Empty;
                default: return QtoBlockCatalogStatus.Unconfigured;
            }
        }

        private static bool Contains(string value, string query)
        {
            return !string.IsNullOrWhiteSpace(value) && value.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
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

            int desired = Math.Max(min, Math.Min((int)Math.Round(split.Width * 0.58), max));
            if (desired > min && desired < max)
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

        private void FilterChanged(object sender, EventArgs e)
        {
            ApplyFilters();
        }

        private Button CreateButton(string text, EventHandler handler, QtoButtonRole role)
        {
            Button button = QtoUiTheme.CreateButton(text, handler, role);
            button.Dock = DockStyle.Fill;
            return button;
        }

        private ComboBox CreateFilterCombo()
        {
            return new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
        }

        private void AddGridColumn(string header, string propertyName, int width)
        {
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = header, DataPropertyName = propertyName, Width = width });
        }

        private static TextBox AddTextField(TableLayoutPanel table, string label, bool readOnly)
        {
            TextBox textBox = new TextBox { Dock = DockStyle.Fill, ReadOnly = readOnly };
            QtoUiTheme.ApplyReadOnlyTextBox(textBox);
            AddField(table, label, textBox);
            return textBox;
        }

        private static ComboBox AddComboField(TableLayoutPanel table, string label, IEnumerable<string> values)
        {
            ComboBox combo = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDown };
            combo.Items.AddRange(values.Cast<object>().ToArray());
            AddField(table, label, combo);
            return combo;
        }

        private static void AddField(TableLayoutPanel table, string label, Control control)
        {
            int row = table.RowCount++;
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            table.Controls.Add(QtoUiTheme.CreateLabel(label, ContentAlignment.MiddleLeft), 0, row);
            table.Controls.Add(control, 1, row);
        }

        private static void AddFullRow(TableLayoutPanel table, Control control)
        {
            int row = table.RowCount++;
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            table.Controls.Add(control, 0, row);
            table.SetColumnSpan(control, 2);
        }
    }

    internal sealed class QtoBlockCatalogBatchEditForm : Form
    {
        private readonly List<BatchField> fields;

        public QtoBlockCatalogBatchEditForm(QtoDictionaryStore dictionary)
        {
            fields = new List<BatchField>();
            Text = "批次設定圖塊資料";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(560, 430);
            MinimumSize = new Size(500, 390);
            AutoScaleMode = AutoScaleMode.Dpi;
            QtoUiTheme.ApplyForm(this);

            TableLayoutPanel root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = QtoUiTheme.FormPadding, ColumnCount = 1, RowCount = 2 };
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            Controls.Add(root);

            TableLayoutPanel table = new TableLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, ColumnCount = 3 };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 36));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.Controls.Add(table, 0, 0);
            AddField(table, "SystemCode", "系統代碼", QtoSystemDefaults.OrderValues(dictionary.SystemCodes));
            AddField(table, "QtoType", "CAD 計量型態", new[] { QtoXDataHelper.TypeOutlet, QtoXDataHelper.TypeJunctionBox, QtoXDataHelper.TypeWire, QtoXDataHelper.TypeConduitSegment, QtoXDataHelper.TypeTray, QtoXDataHelper.TypeDevice, QtoXDataHelper.TypePanel });
            AddField(table, "QuantityBasis", "數量依據", new string[0]);
            AddField(table, "Unit", "單位", new[] { "點", "組", "台", "只", "座", "m", "式" });
            AddField(table, "DefaultLayer", "預設圖層", new string[0]);
            AddField(table, "Status", "狀態", new[] { "使用中", "未設定", "已停用" });

            FlowLayoutPanel actions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
            Button cancel = QtoUiTheme.CreateButton("取消", delegate { DialogResult = DialogResult.Cancel; Close(); }, QtoButtonRole.Default);
            Button apply = QtoUiTheme.CreateButton("套用", ApplyClick, QtoButtonRole.Primary);
            actions.Controls.Add(cancel);
            actions.Controls.Add(apply);
            root.Controls.Add(actions, 0, 1);
        }

        public void ApplyTo(QtoBlockCatalogItem item)
        {
            foreach (BatchField field in fields.Where(field => field.Enabled.Checked))
            {
                string value = field.Value.Text.Trim();
                switch (field.Key)
                {
                    case "SystemCode": item.SystemCode = value; break;
                    case "QtoType": item.QtoType = value; break;
                    case "QuantityBasis": item.QuantityBasis = value; break;
                    case "Unit": item.Unit = value; break;
                    case "DefaultLayer": item.DefaultLayer = value; break;
                    case "Status": item.Status = value == "使用中" ? QtoBlockCatalogStatus.Active : value == "已停用" ? QtoBlockCatalogStatus.Deprecated : QtoBlockCatalogStatus.Unconfigured; break;
                }
            }
        }

        private void AddField(TableLayoutPanel table, string key, string label, IEnumerable<string> choices)
        {
            int row = table.RowCount++;
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            CheckBox enabled = new CheckBox { Dock = DockStyle.Fill };
            ComboBox value = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDown };
            value.Items.AddRange(choices.Cast<object>().ToArray());
            table.Controls.Add(enabled, 0, row);
            table.Controls.Add(QtoUiTheme.CreateLabel(label, ContentAlignment.MiddleLeft), 1, row);
            table.Controls.Add(value, 2, row);
            fields.Add(new BatchField { Key = key, Enabled = enabled, Value = value });
        }

        private void ApplyClick(object sender, EventArgs e)
        {
            if (!fields.Any(field => field.Enabled.Checked))
            {
                MessageBox.Show(this, "請至少勾選一個要套用的欄位。", "批次設定", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            DialogResult = DialogResult.OK;
            Close();
        }

        private sealed class BatchField
        {
            public string Key { get; set; }
            public CheckBox Enabled { get; set; }
            public ComboBox Value { get; set; }
        }
    }
}
