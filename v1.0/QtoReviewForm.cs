using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace QtoWirePlugin
{
    public class QtoReviewForm : Form
    {
        private readonly List<QtoReviewItem> allItems;
        private readonly BindingList<QtoReviewItem> items;
        private readonly DataGridView reviewGrid;
        private readonly TextBox summaryTextBox;
        private readonly TextBox technicalTextBox;
        private readonly Button technicalButton;
        private ComboBox severityFilter;
        private ComboBox categoryFilter;
        private ComboBox statusFilter;
        private bool technicalVisible;

        public QtoReviewForm()
            : this(QtoUiSampleData.CreateReviewItems())
        {
        }

        public QtoReviewForm(IEnumerable<QtoReviewItem> reviewItems)
        {
            Text = "QTO 檢查清單";
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(980, 640);
            MinimumSize = new Size(880, 560);
            QtoUiTheme.ApplyForm(this);

            allItems = new List<QtoReviewItem>(reviewItems ?? QtoUiSampleData.CreateReviewItems());
            items = new BindingList<QtoReviewItem>(new List<QtoReviewItem>(allItems));

            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.Padding = new Padding(12);
            root.ColumnCount = 1;
            root.RowCount = 4;
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 112));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
            Controls.Add(root);

            root.Controls.Add(CreateFilterPanel(), 0, 0);

            reviewGrid = new DataGridView();
            reviewGrid.Dock = DockStyle.Fill;
            reviewGrid.AutoGenerateColumns = false;
            reviewGrid.AllowUserToAddRows = false;
            reviewGrid.AllowUserToDeleteRows = false;
            reviewGrid.ReadOnly = true;
            reviewGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            reviewGrid.MultiSelect = true;
            reviewGrid.RowHeadersVisible = false;
            reviewGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            reviewGrid.DataSource = items;
            QtoUiTheme.ApplyGrid(reviewGrid);
            reviewGrid.SelectionChanged += ReviewGridSelectionChanged;
            reviewGrid.CellDoubleClick += ReviewGridCellDoubleClick;
            AddColumn("SeverityDisplay", "嚴重性", 70);
            AddColumn("CategoryDisplay", "問題類型", 100);
            AddColumn("UserMessageDisplay", "問題說明", 260);
            AddColumn("SuggestedActionDisplay", "建議處理", 180);
            AddColumn("BlockName", "圖塊名稱", 120);
            AddLocationColumn();
            AddAutoRepairColumn();
            root.Controls.Add(reviewGrid, 0, 1);

            TableLayoutPanel detailPanel = new TableLayoutPanel();
            detailPanel.Dock = DockStyle.Fill;
            detailPanel.ColumnCount = 1;
            detailPanel.RowCount = 2;
            detailPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            detailPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 0));
            summaryTextBox = new TextBox();
            summaryTextBox.Dock = DockStyle.Fill;
            summaryTextBox.Multiline = true;
            summaryTextBox.ReadOnly = true;
            summaryTextBox.ScrollBars = ScrollBars.Vertical;
            QtoUiTheme.ApplyReadOnlyTextBox(summaryTextBox);
            detailPanel.Controls.Add(summaryTextBox, 0, 0);
            technicalTextBox = new TextBox();
            technicalTextBox.Dock = DockStyle.Fill;
            technicalTextBox.Multiline = true;
            technicalTextBox.ReadOnly = true;
            technicalTextBox.ScrollBars = ScrollBars.Vertical;
            technicalTextBox.Visible = false;
            QtoUiTheme.ApplyReadOnlyTextBox(technicalTextBox);
            detailPanel.Controls.Add(technicalTextBox, 0, 1);
            root.Controls.Add(WrapGroup("問題摘要", detailPanel), 0, 2);

            FlowLayoutPanel actions = new FlowLayoutPanel();
            actions.Dock = DockStyle.Fill;
            actions.FlowDirection = System.Windows.Forms.FlowDirection.RightToLeft;
            actions.WrapContents = false;
            actions.Controls.Add(CreateButton("關閉", CloseButtonClick));
            technicalButton = CreateButton("查看技術資訊", TechnicalButtonClick);
            actions.Controls.Add(technicalButton);
            actions.Controls.Add(CreateButton("修復選取項目", AutoRepairButtonClick));
            actions.Controls.Add(CreateButton("開啟屬性", OpenPropertiesButtonClick));
            actions.Controls.Add(CreateButton("定位物件", LocateButtonClick));
            root.Controls.Add(actions, 0, 3);

            if (reviewGrid.Rows.Count > 0)
            {
                reviewGrid.Rows[0].Selected = true;
                UpdateDetail();
            }
        }

        private Control CreateFilterPanel()
        {
            TableLayoutPanel panel = new TableLayoutPanel();
            panel.Dock = DockStyle.Fill;
            panel.ColumnCount = 6;
            panel.RowCount = 1;
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 64));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 64));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 64));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34));

            panel.Controls.Add(CreateLabel("嚴重性"), 0, 0);
            severityFilter = CreateCombo(new string[] { "全部", "錯誤", "警告", "提醒" });
            severityFilter.SelectedIndexChanged += FilterChanged;
            panel.Controls.Add(severityFilter, 1, 0);
            panel.Controls.Add(CreateLabel("類型"), 2, 0);
            categoryFilter = CreateCombo(new string[] { "全部", "欄位缺漏", "圖塊資料庫", "同步 ID", "數量異常", "同步差異", "預算完整性" });
            categoryFilter.SelectedIndexChanged += FilterChanged;
            panel.Controls.Add(categoryFilter, 3, 0);
            panel.Controls.Add(CreateLabel("狀態"), 4, 0);
            statusFilter = CreateCombo(new string[] { "未處理", "已處理", "忽略", "全部" });
            statusFilter.SelectedIndexChanged += FilterChanged;
            panel.Controls.Add(statusFilter, 5, 0);
            return WrapGroup("篩選", panel);
        }

        private void AddColumn(string propertyName, string title, int fillWeight)
        {
            DataGridViewTextBoxColumn column = new DataGridViewTextBoxColumn();
            column.DataPropertyName = propertyName;
            column.HeaderText = title;
            column.FillWeight = fillWeight;
            reviewGrid.Columns.Add(column);
        }

        private void AddLocationColumn()
        {
            DataGridViewTextBoxColumn column = new DataGridViewTextBoxColumn();
            column.HeaderText = "物件定位資訊";
            column.FillWeight = 160;
            reviewGrid.Columns.Add(column);
            reviewGrid.CellFormatting += ReviewGridCellFormatting;
        }

        private void AddAutoRepairColumn()
        {
            DataGridViewTextBoxColumn column = new DataGridViewTextBoxColumn();
            column.HeaderText = "自動修復";
            column.FillWeight = 90;
            reviewGrid.Columns.Add(column);
        }

        private static Label CreateLabel(string text)
        {
            Label label = new Label();
            label.Text = text;
            label.Dock = DockStyle.Fill;
            label.TextAlign = ContentAlignment.MiddleLeft;
            label.ForeColor = QtoUiTheme.TextColor;
            return label;
        }

        private static ComboBox CreateCombo(string[] items)
        {
            ComboBox comboBox = new ComboBox();
            comboBox.Dock = DockStyle.Fill;
            comboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBox.Items.AddRange(items);
            comboBox.SelectedIndex = 0;
            comboBox.FlatStyle = FlatStyle.System;
            return comboBox;
        }

        private void FilterChanged(object sender, EventArgs e)
        {
            ApplyFilters();
        }

        private void ApplyFilters()
        {
            if (severityFilter == null || categoryFilter == null || statusFilter == null)
            {
                return;
            }

            string severity = Convert.ToString(severityFilter.SelectedItem);
            string category = Convert.ToString(categoryFilter.SelectedItem);
            string status = Convert.ToString(statusFilter.SelectedItem);
            List<QtoReviewItem> filtered = allItems.Where(item =>
                (severity == "全部" || string.Equals(item.SeverityDisplay, severity, StringComparison.OrdinalIgnoreCase))
                && (category == "全部" || string.Equals(item.CategoryDisplay, category, StringComparison.OrdinalIgnoreCase))
                && (status == "全部" || string.Equals(GetStatusDisplay(item), status, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            items.RaiseListChangedEvents = false;
            items.Clear();
            foreach (QtoReviewItem item in filtered)
            {
                items.Add(item);
            }
            items.RaiseListChangedEvents = true;
            items.ResetBindings();

            if (reviewGrid != null && reviewGrid.Rows.Count > 0)
            {
                reviewGrid.ClearSelection();
                reviewGrid.Rows[0].Selected = true;
                reviewGrid.CurrentCell = reviewGrid.Rows[0].Cells[0];
            }
            UpdateDetail();
        }

        private static string GetStatusDisplay(QtoReviewItem item)
        {
            return item == null || string.IsNullOrWhiteSpace(item.Status) ? "未處理" : item.Status;
        }

        private static GroupBox WrapGroup(string title, Control content)
        {
            return QtoUiTheme.WrapGroup(title, content);
        }

        private static Button CreateButton(string text, EventHandler handler)
        {
            QtoButtonRole role = text == "修復選取項目" ? QtoButtonRole.Primary : QtoButtonRole.Default;
            Button button = QtoUiTheme.CreateButton(text, handler, role);
            button.Width = 132;
            button.Height = 30;
            button.Margin = new Padding(4, 8, 4, 4);
            return button;
        }

        private QtoReviewItem GetSelectedItem()
        {
            if (reviewGrid.CurrentRow == null)
            {
                return null;
            }

            return reviewGrid.CurrentRow.DataBoundItem as QtoReviewItem;
        }

        private List<QtoReviewItem> GetSelectedItems()
        {
            List<QtoReviewItem> selected = new List<QtoReviewItem>();
            foreach (DataGridViewRow row in reviewGrid.SelectedRows)
            {
                QtoReviewItem item = row.DataBoundItem as QtoReviewItem;
                if (item != null && !selected.Contains(item))
                {
                    selected.Add(item);
                }
            }

            if (selected.Count == 0)
            {
                QtoReviewItem current = GetSelectedItem();
                if (current != null)
                {
                    selected.Add(current);
                }
            }

            return selected;
        }

        private void ReviewGridSelectionChanged(object sender, EventArgs e)
        {
            UpdateDetail();
        }

        private void ReviewGridCellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            LocateButtonClick(sender, EventArgs.Empty);
        }

        private void ReviewGridCellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= items.Count)
            {
                return;
            }

            if (reviewGrid.Columns[e.ColumnIndex].HeaderText == "物件定位資訊")
            {
                e.Value = FormatLocation(items[e.RowIndex]);
                e.FormattingApplied = true;
            }
            else if (reviewGrid.Columns[e.ColumnIndex].HeaderText == "自動修復")
            {
                e.Value = items[e.RowIndex].CanAutoRepair ? "可自動修復" : "需人工確認";
                e.FormattingApplied = true;
            }
        }

        private void UpdateDetail()
        {
            List<QtoReviewItem> selected = GetSelectedItems();
            if (selected.Count == 0)
            {
                summaryTextBox.Text = string.Empty;
                technicalTextBox.Text = string.Empty;
                return;
            }

            if (selected.Count > 1)
            {
                int autoCount = 0;
                int manualCount = 0;
                foreach (QtoReviewItem selectedItem in selected)
                {
                    if (selectedItem.CanAutoRepair)
                    {
                        autoCount++;
                    }
                    else
                    {
                        manualCount++;
                    }
                }

                summaryTextBox.Text =
                    "已選取：" + selected.Count.ToString("0") + " 項" + Environment.NewLine +
                    "可自動修復：" + autoCount.ToString("0") + " 項" + Environment.NewLine +
                    "需人工確認：" + manualCount.ToString("0") + " 項" + Environment.NewLine +
                    "建議：可先按「修復選取項目」查看批次修復內容；需人工確認的項目不會直接寫入 CAD。";
                technicalTextBox.Text = "多選狀態下預設不展開技術細節。請改選單一項目查看原始檢查資訊。";
                return;
            }

            QtoReviewItem item = selected[0];
            summaryTextBox.Text =
                "問題：" + item.UserMessageDisplay + Environment.NewLine +
                "建議：" + item.SuggestedActionDisplay + Environment.NewLine +
                "位置：" + FormatLocation(item) + Environment.NewLine +
                "自動修復：" + (item.CanAutoRepair ? "可自動修復" : "需人工確認");
            technicalTextBox.Text =
                "問題項目：" + item.IssueTypeDisplay + Environment.NewLine +
                "原始類型：" + (item.IssueType ?? string.Empty) + Environment.NewLine +
                "技術細節：" + Environment.NewLine +
                (item.TechnicalDetail ?? string.Empty);
        }

        private static string FormatLocation(QtoReviewItem item)
        {
            if (item == null)
            {
                return string.Empty;
            }

            string floor = string.IsNullOrWhiteSpace(item.Floor) ? "未填樓層" : item.Floor;
            string area = string.IsNullOrWhiteSpace(item.Area) ? "未填區域" : item.Area;
            string handle = string.IsNullOrWhiteSpace(item.ObjectHandle) ? "無 Handle" : "Handle " + item.ObjectHandle;
            return floor + " / " + area + " / " + handle;
        }

        private void LocateButtonClick(object sender, EventArgs e)
        {
            List<ObjectId> objectIds = ResolveSelectedObjectIds();
            if (objectIds.Count == 0)
            {
                MessageBox.Show(this, "選取問題沒有可定位的 CAD 物件，可能是全域設定或 Excel 差異。", "定位物件", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                SelectAndZoom(objectIds);
                WindowState = FormWindowState.Minimized;
            }
            catch (System.Exception ex)
            {
                MessageBox.Show(this, "目前無法定位物件，請先結束正在執行的 CAD 指令後再試。\r\n\r\n" + ex.Message, "定位物件", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void AutoRepairButtonClick(object sender, EventArgs e)
        {
            List<QtoReviewItem> selected = GetSelectedItems();
            if (selected.Count == 0)
            {
                MessageBox.Show(this, "請先選取要處理的問題。", "修復選取項目", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            int autoCount = 0;
            foreach (QtoReviewItem item in selected)
            {
                if (item.CanAutoRepair)
                {
                    autoCount++;
                }
            }

            if (autoCount > 0)
            {
                DialogResult confirm = MessageBox.Show(this,
                    "將修復選取項目中可安全處理的同步 ID 問題。" + Environment.NewLine +
                    "可自動修復：" + autoCount.ToString("0") + " 項" + Environment.NewLine +
                    "需要人工確認：" + (selected.Count - autoCount).ToString("0") + " 項" + Environment.NewLine + Environment.NewLine +
                    "只會補上或重新配發 QTO_SYNC_ID，不會替你判斷系統、設備或數量。是否繼續？",
                    "修復選取項目",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);
                if (confirm != DialogResult.Yes)
                {
                    return;
                }

                List<string> handles = selected
                    .Where(item => item.CanAutoRepair && !string.IsNullOrWhiteSpace(item.ObjectHandle))
                    .Select(item => item.ObjectHandle)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                QtoRepairResult repairResult = QtoSyncCommandService.RepairCurrentDrawing(handles);
                QtoValidationResult validation = QtoSyncCommandService.ValidateCurrentDrawing();
                allItems.Clear();
                allItems.AddRange(validation.ReviewItems);
                ApplyFilters();
                MessageBox.Show(this,
                    "已修復：" + repairResult.RepairedCount.ToString("0") + " 項" + Environment.NewLine +
                    "重新檢查後仍需確認：" + validation.ReviewItems.Count.ToString("0") + " 項",
                    "修復選取項目",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            MessageBox.Show(this, "選取項目都需要人工確認，不能直接自動修復。請使用「定位物件」或「開啟屬性」補齊資料；圖塊分類問題請到「管理圖塊庫」處理。", "修復選取項目", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void TechnicalButtonClick(object sender, EventArgs e)
        {
            technicalVisible = !technicalVisible;
            technicalTextBox.Visible = technicalVisible;
            technicalButton.Text = technicalVisible ? "隱藏技術資訊" : "查看技術資訊";
            TableLayoutPanel panel = technicalTextBox.Parent as TableLayoutPanel;
            if (panel != null)
            {
                panel.RowStyles[1].Height = technicalVisible ? 88 : 0;
                panel.RowStyles[1].SizeType = SizeType.Absolute;
            }
        }

        private void OpenPropertiesButtonClick(object sender, EventArgs e)
        {
            List<ObjectId> objectIds = ResolveSelectedObjectIds();
            if (objectIds.Count == 0)
            {
                MessageBox.Show(this, "選取問題沒有可開啟的 CAD 物件。", "開啟屬性", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                SelectInCad(objectIds);
                QtoPropertyPaletteHost.Show();
                WindowState = FormWindowState.Minimized;
            }
            catch (System.Exception ex)
            {
                MessageBox.Show(this, "目前無法開啟所選物件的 QTO 屬性，請先結束正在執行的 CAD 指令後再試。\r\n\r\n" + ex.Message, "開啟屬性", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private List<ObjectId> ResolveSelectedObjectIds()
        {
            Document document = Autodesk.AutoCAD.ApplicationServices.Core.Application.DocumentManager.MdiActiveDocument;
            List<ObjectId> result = new List<ObjectId>();
            if (document == null)
            {
                return result;
            }

            foreach (QtoReviewItem item in GetSelectedItems())
            {
                long handleValue;
                if (item == null || string.IsNullOrWhiteSpace(item.ObjectHandle)
                    || !long.TryParse(item.ObjectHandle, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out handleValue))
                {
                    continue;
                }

                try
                {
                    ObjectId objectId = document.Database.GetObjectId(false, new Handle(handleValue), 0);
                    if (!objectId.IsNull && !objectId.IsErased && !result.Contains(objectId))
                    {
                        result.Add(objectId);
                    }
                }
                catch
                {
                }
            }

            return result;
        }

        private static void SelectInCad(IList<ObjectId> objectIds)
        {
            Document document = Autodesk.AutoCAD.ApplicationServices.Core.Application.DocumentManager.MdiActiveDocument;
            if (document == null || objectIds == null || objectIds.Count == 0)
            {
                return;
            }

            using (DocumentLock documentLock = document.LockDocument())
            {
                document.Editor.SetImpliedSelection(objectIds.ToArray());
            }
        }

        private static void SelectAndZoom(IList<ObjectId> objectIds)
        {
            Document document = Autodesk.AutoCAD.ApplicationServices.Core.Application.DocumentManager.MdiActiveDocument;
            if (document == null || objectIds == null || objectIds.Count == 0)
            {
                return;
            }

            Extents3d? extents = null;
            using (DocumentLock documentLock = document.LockDocument())
            using (Transaction transaction = document.Database.TransactionManager.StartTransaction())
            {
                document.Editor.SetImpliedSelection(objectIds.ToArray());
                foreach (ObjectId objectId in objectIds)
                {
                    Entity entity = transaction.GetObject(objectId, OpenMode.ForRead, false) as Entity;
                    if (entity == null)
                    {
                        continue;
                    }

                    try
                    {
                        Extents3d entityExtents = entity.GeometricExtents;
                        if (extents.HasValue)
                        {
                            Extents3d combined = extents.Value;
                            combined.AddExtents(entityExtents);
                            extents = combined;
                        }
                        else
                        {
                            extents = entityExtents;
                        }
                    }
                    catch
                    {
                    }
                }
                transaction.Commit();
            }

            if (!extents.HasValue)
            {
                return;
            }

            using (DocumentLock documentLock = document.LockDocument())
            using (ViewTableRecord view = document.Editor.GetCurrentView())
            {
                Matrix3d worldToDisplay = Matrix3d.PlaneToWorld(view.ViewDirection);
                worldToDisplay = Matrix3d.Displacement(view.Target - Point3d.Origin) * worldToDisplay;
                worldToDisplay = Matrix3d.Rotation(-view.ViewTwist, view.ViewDirection, view.Target) * worldToDisplay;
                worldToDisplay = worldToDisplay.Inverse();
                Extents3d displayExtents = extents.Value;
                displayExtents.TransformBy(worldToDisplay);

                double width = Math.Max(10.0, (displayExtents.MaxPoint.X - displayExtents.MinPoint.X) * 1.6);
                double height = Math.Max(10.0, (displayExtents.MaxPoint.Y - displayExtents.MinPoint.Y) * 1.6);
                double viewRatio = view.Height <= 0 ? 1.0 : view.Width / view.Height;
                if (width / height > viewRatio)
                {
                    height = width / viewRatio;
                }
                else
                {
                    width = height * viewRatio;
                }

                view.Width = width;
                view.Height = height;
                view.CenterPoint = new Point2d(
                    (displayExtents.MinPoint.X + displayExtents.MaxPoint.X) / 2.0,
                    (displayExtents.MinPoint.Y + displayExtents.MaxPoint.Y) / 2.0);
                document.Editor.SetCurrentView(view);
            }
        }

        private static IList<QtoRepairPlanItem> BuildRepairPlanItems(IList<QtoReviewItem> selected)
        {
            Dictionary<string, QtoRepairPlanItem> plansByIssueType = new Dictionary<string, QtoRepairPlanItem>(StringComparer.OrdinalIgnoreCase);
            foreach (QtoReviewItem item in selected)
            {
                if (item == null)
                {
                    continue;
                }

                string issueType = string.IsNullOrWhiteSpace(item.IssueType) ? item.CategoryDisplay : item.IssueType;
                QtoRepairPlanItem plan;
                if (!plansByIssueType.TryGetValue(issueType, out plan))
                {
                    plan = new QtoRepairPlanItem();
                    plan.IssueType = issueType;
                    plan.Count = 0;
                    plan.CanAutoRepair = item.CanAutoRepair;
                    plan.RepairAction = item.CanAutoRepair
                        ? QtoReviewService.ResolveSuggestedAction(item.IssueType, item.SuggestedAction)
                        : "需要工程人員確認；系統不會直接寫入 CAD。";
                    plansByIssueType[issueType] = plan;
                }

                plan.Count++;
                plan.CanAutoRepair = plan.CanAutoRepair && item.CanAutoRepair;
            }

            return new List<QtoRepairPlanItem>(plansByIssueType.Values);
        }

        private void CloseButtonClick(object sender, EventArgs e)
        {
            Close();
        }
    }
}
