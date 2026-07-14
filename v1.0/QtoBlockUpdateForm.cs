using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;
using Autodesk.AutoCAD.DatabaseServices;

namespace QtoWirePlugin
{
    public sealed class QtoBlockUpdateForm : Form
    {
        private readonly Database database;
        private readonly QtoBlockUpdateService service;
        private readonly DataGridView grid;
        private readonly TextBox detail;
        private readonly PictureBox currentPreview;
        private readonly PictureBox standardPreview;
        private readonly Timer previewTimer;
        private BindingList<QtoBlockUpdateCandidate> candidates;

        public QtoBlockUpdateForm(Database database, QtoBlockCatalog catalog)
        {
            this.database = database;
            service = new QtoBlockUpdateService();
            previewTimer = new Timer { Interval = 180 };
            previewTimer.Tick += delegate { previewTimer.Stop(); ShowDetail(); };
            Text = "更新專案圖塊";
            Width = 1120; Height = 700; MinimumSize = new System.Drawing.Size(900, 560); StartPosition = FormStartPosition.CenterScreen;
            QtoUiTheme.ApplyForm(this);
            candidates = new BindingList<QtoBlockUpdateCandidate>(service.Analyze(database, catalog).ToList());
            grid = new DataGridView
            {
                Dock = DockStyle.Fill, AutoGenerateColumns = false, AllowUserToAddRows = false, AllowUserToDeleteRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = true, RowHeadersVisible = false,
                BackgroundColor = System.Drawing.SystemColors.Window
            };
            QtoUiTheme.ApplyGrid(grid);
            grid.Columns.Add(new DataGridViewCheckBoxColumn { DataPropertyName = "Selected", HeaderText = "更新", Width = 50 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "DisplayName", HeaderText = "顯示名稱", Width = 180, ReadOnly = true });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "BlockName", HeaderText = "圖塊名稱", Width = 160, ReadOnly = true });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Status", HeaderText = "狀態", Width = 120, ReadOnly = true });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "ReferenceCount", HeaderText = "實例數", Width = 70, ReadOnly = true });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "AttributeDifference", HeaderText = "屬性差異", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, ReadOnly = true });
            grid.DataSource = candidates;
            grid.SelectionChanged += delegate { SchedulePreview(); };

            detail = new TextBox { Dock = DockStyle.Bottom, Height = 92, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical };
            currentPreview = new PictureBox { Dock = DockStyle.Fill, BackColor = System.Drawing.Color.FromArgb(45, 55, 66), SizeMode = PictureBoxSizeMode.Zoom };
            standardPreview = new PictureBox { Dock = DockStyle.Fill, BackColor = System.Drawing.Color.FromArgb(45, 55, 66), SizeMode = PictureBoxSizeMode.Zoom };
            TableLayoutPanel previews = new TableLayoutPanel { Dock = DockStyle.Bottom, Height = 220, ColumnCount = 2, RowCount = 1, Padding = new Padding(6) };
            previews.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            previews.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            previews.Controls.Add(WrapPreview("專案目前圖塊", currentPreview), 0, 0);
            previews.Controls.Add(WrapPreview("公司標準圖塊", standardPreview), 1, 0);
            FlowLayoutPanel buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 52, FlowDirection = System.Windows.Forms.FlowDirection.RightToLeft, Padding = new Padding(8), AutoScroll = true };
            Button apply = QtoUiTheme.CreateButton("更新勾選圖塊", ApplyUpdates, QtoButtonRole.Primary);
            apply.Width = 140;
            Button cancel = QtoUiTheme.CreateButton("關閉", null, QtoButtonRole.Secondary);
            cancel.Width = 90;
            cancel.DialogResult = DialogResult.Cancel;
            Button selectUpdates = QtoUiTheme.CreateButton("勾選所有可更新", null, QtoButtonRole.Default);
            selectUpdates.Width = 140;
            selectUpdates.Click += delegate { foreach (QtoBlockUpdateCandidate item in candidates) item.Selected = item.CanUpdate; grid.Refresh(); };
            buttons.Controls.Add(cancel); buttons.Controls.Add(apply); buttons.Controls.Add(selectUpdates);
            Panel header = new Panel { Dock = DockStyle.Top, Height = 64, Padding = new Padding(12, 8, 12, 4) };
            header.Controls.Add(new Label { Text = "更新專案圖塊", Dock = DockStyle.Top, Height = 30, Font = QtoUiTheme.HeaderFont, ForeColor = QtoUiTheme.TextColor });
            header.Controls.Add(new Label { Text = candidates.Count == 0 ? "目前沒有可檢查的專案圖塊。" : "先比較專案與標準圖塊，再勾選要更新的項目。更新前不會修改圖面。", Dock = DockStyle.Bottom, Height = 24, ForeColor = QtoUiTheme.MutedTextColor });
            Controls.Add(grid); Controls.Add(previews); Controls.Add(detail); Controls.Add(buttons); Controls.Add(header);
            SchedulePreview();
        }

        private void SchedulePreview()
        {
            previewTimer.Stop();
            previewTimer.Start();
        }

        private static Control WrapPreview(string title, Control preview)
        {
            GroupBox group = new GroupBox { Text = title, Dock = DockStyle.Fill, Padding = new Padding(8) };
            group.Controls.Add(preview);
            return group;
        }

        private void ShowDetail()
        {
            QtoBlockUpdateCandidate item = grid.CurrentRow == null ? null : grid.CurrentRow.DataBoundItem as QtoBlockUpdateCandidate;
            detail.Text = item == null ? string.Empty : "來源：" + item.SourceDwg + Environment.NewLine + item.DynamicDifference + Environment.NewLine + item.QtoImpact + Environment.NewLine + item.Detail;
            SetPreview(currentPreview, item == null ? null : QtoBlockPreviewRenderer.RenderCurrentBlockPreview(database, item.BlockName, 420, 170));
            SetPreview(standardPreview, item == null ? null : QtoBlockPreviewRenderer.RenderBlockPreview(item.SourceDwg, item.BlockName, 420, 170));
        }

        private static void SetPreview(PictureBox target, System.Drawing.Image image)
        {
            System.Drawing.Image previous = target.Image;
            target.Image = image;
            if (previous != null) previous.Dispose();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            previewTimer.Stop();
            previewTimer.Dispose();
            SetPreview(currentPreview, null);
            SetPreview(standardPreview, null);
            base.OnFormClosed(e);
        }

        private void ApplyUpdates(object sender, EventArgs e)
        {
            grid.EndEdit();
            List<QtoBlockUpdateCandidate> selected = candidates.Where(c => c.Selected && c.CanUpdate).ToList();
            if (selected.Count == 0) { MessageBox.Show(this, "沒有勾選可更新圖塊。", "更新專案圖塊"); return; }
            DialogResult confirm = MessageBox.Show(this, "將更新 " + selected.Count + " 個圖塊定義，並套用到既有實例。\r\n位置、旋轉、比例、同名屬性及 QTO 資訊會保留。是否繼續？", "確認更新", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (confirm != DialogResult.Yes) return;
            QtoBlockUpdateResult result = service.Apply(database, selected);
            MessageBox.Show(this, "已更新定義 " + result.UpdatedDefinitionCount + " 個、實例 " + result.UpdatedReferenceCount + " 個。\r\n需人工確認 " + result.ReviewCount + " 項。", "更新完成", MessageBoxButtons.OK, result.ReviewCount > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
