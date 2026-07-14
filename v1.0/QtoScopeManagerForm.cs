using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;

namespace QtoWirePlugin
{
    public sealed class QtoScopeManagerForm : Form
    {
        private readonly Document document;
        private readonly DataGridView grid;
        private readonly Label summary;
        private readonly CheckBox dynamicEnabled;
        private readonly Timer previewTimer;

        public QtoScopeManagerForm(Document document)
        {
            this.document = document;
            previewTimer = new Timer { Interval = 220 };
            previewTimer.Tick += delegate { previewTimer.Stop(); ShowPreview(); };
            Text = "樓層與系統範圍管理";
            Width = 900;
            Height = 600;
            MinimumSize = new System.Drawing.Size(720, 480);
            StartPosition = FormStartPosition.CenterParent;
            QtoUiTheme.ApplyForm(this);

            Panel header = new Panel { Dock = DockStyle.Top, Height = 66, Padding = new Padding(12, 8, 12, 4) };
            header.Controls.Add(new Label { Text = "管理樓層框與系統框", Dock = DockStyle.Top, Height = 30, Font = QtoUiTheme.HeaderFont, ForeColor = QtoUiTheme.TextColor });
            header.Controls.Add(new Label { Text = "選取範圍框後會自動預覽影響；套用只修改已有 QTO 資訊的物件。", Dock = DockStyle.Bottom, Height = 24, ForeColor = QtoUiTheme.MutedTextColor });

            FlowLayoutPanel actions = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 54, Padding = new Padding(10, 8, 10, 8), WrapContents = false, AutoScroll = true };
            actions.Controls.Add(ActionButton("套用選取", ApplySelected, QtoButtonRole.Primary));
            actions.Controls.Add(ActionButton("套用全部", ApplyAll, QtoButtonRole.Default));
            actions.Controls.Add(ActionButton("在 CAD 選取", SelectInCad, QtoButtonRole.Default));
            actions.Controls.Add(ActionButton("重新整理", RefreshRows, QtoButtonRole.Secondary));
            actions.Controls.Add(ActionButton("清除所選框線設定", ClearSelected, QtoButtonRole.Secondary));
            dynamicEnabled = new CheckBox { Text = "動態套用", AutoSize = true, Checked = QtoScopeService.DynamicEnabled, Padding = new Padding(12, 5, 0, 0) };
            dynamicEnabled.CheckedChanged += ToggleDynamic;
            actions.Controls.Add(dynamicEnabled);

            summary = new Label { Dock = DockStyle.Bottom, Height = 46, Padding = new Padding(12, 8, 12, 8), AutoEllipsis = true, ForeColor = QtoUiTheme.MutedTextColor };
            grid = new DataGridView
            {
                Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false,
                AutoGenerateColumns = true, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = true,
                RowHeadersVisible = false, BackgroundColor = System.Drawing.SystemColors.Window
            };
            QtoUiTheme.ApplyGrid(grid);
            grid.SelectionChanged += delegate { SchedulePreview(); };
            Controls.Add(grid);
            Controls.Add(summary);
            Controls.Add(actions);
            Controls.Add(header);
            LoadRows();
        }

        private static Button ActionButton(string text, EventHandler handler, QtoButtonRole role)
        {
            Button button = QtoUiTheme.CreateButton(text, handler, role);
            button.AutoSize = true;
            button.Margin = new Padding(0, 0, 8, 0);
            return button;
        }

        private void LoadRows()
        {
            List<ScopeView> rows = QtoScopeService.GetScopes(document.Database).Select(scope => new ScopeView
            {
                ObjectId = scope.ObjectId,
                類型 = string.Equals(scope.Kind, QtoXDataHelper.ScopeKindFloor, StringComparison.OrdinalIgnoreCase) ? "樓層框" : "系統框",
                名稱 = scope.Name,
                套用值 = scope.Value,
                優先序 = ToLocalTime(scope.PriorityTicks)
            }).ToList();
            grid.DataSource = new BindingList<ScopeView>(rows);
            if (grid.Columns["ObjectId"] != null) grid.Columns["ObjectId"].Visible = false;
            summary.Text = rows.Count == 0 ? "目前圖面沒有樓層框或系統框。" : "共 " + rows.Count + " 個範圍框。選取列可查看預計更新數量。";
        }

        private void RefreshRows(object sender, EventArgs e)
        {
            QtoScopeService.InvalidateScopeCache(document.Database);
            LoadRows();
        }

        private List<ObjectId> SelectedIds()
        {
            return grid.SelectedRows.Cast<DataGridViewRow>().Select(r => r.DataBoundItem as ScopeView).Where(v => v != null).Select(v => v.ObjectId).ToList();
        }

        private void ShowPreview()
        {
            List<ObjectId> ids = SelectedIds();
            if (ids.Count == 0) return;
            QtoScopeApplyResult preview = QtoScopeService.ApplySelectedScopes(document.Database, ids, true);
            summary.Text = "已選 " + ids.Count + " 個框｜掃描 QTO " + preview.ScannedQtoCount + "｜預計更新 " + preview.UpdatedCount + "｜預計清空 " + preview.ClearedCount + "｜不變 " + preview.UnchangedCount;
        }

        private void SchedulePreview()
        {
            previewTimer.Stop();
            previewTimer.Start();
        }

        private void PreviewSelected(object sender, EventArgs e)
        {
            ShowPreview();
        }

        private void SelectInCad(object sender, EventArgs e)
        {
            List<ObjectId> ids = SelectedIds();
            if (ids.Count == 0) return;
            document.Editor.SetImpliedSelection(ids.ToArray());
            MessageBox.Show(this, "已在 CAD 中選取範圍框。關閉本視窗後即可縮放或編輯。", "範圍管理", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void ApplySelected(object sender, EventArgs e)
        {
            List<ObjectId> ids = SelectedIds();
            if (ids.Count == 0) return;
            QtoScopeApplyResult result = QtoScopeService.ApplySelectedScopes(document.Database, ids, false);
            summary.Text = result.ToUserSummary().Replace("\r\n", "｜");
        }

        private void ApplyAll(object sender, EventArgs e)
        {
            QtoScopeApplyResult result = QtoScopeService.ApplyAllScopes(document.Database, false);
            summary.Text = result.ToUserSummary().Replace("\r\n", "｜");
        }

        private void ClearSelected(object sender, EventArgs e)
        {
            List<ObjectId> ids = SelectedIds();
            if (ids.Count == 0) return;
            if (MessageBox.Show(this, "只會移除所選框線的樓層／系統範圍設定，不會刪除框線。確定繼續？", "清除範圍設定", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            QtoScopeService.ClearScopes(document.Database, ids);
            LoadRows();
        }

        private void ToggleDynamic(object sender, EventArgs e)
        {
            QtoScopeService.DynamicEnabled = dynamicEnabled.Checked;
            if (dynamicEnabled.Checked) QtoSyncCommandService.StartScopeTracking(); else QtoSyncCommandService.StopScopeTracking();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            previewTimer.Stop();
            previewTimer.Dispose();
            base.OnFormClosed(e);
        }

        private static string ToLocalTime(long ticks)
        {
            if (ticks <= 0) return "未設定";
            try { return new DateTime(ticks, DateTimeKind.Utc).ToLocalTime().ToString("yyyy/MM/dd HH:mm:ss"); }
            catch { return "未設定"; }
        }

        private sealed class ScopeView
        {
            public ObjectId ObjectId { get; set; }
            public string 類型 { get; set; }
            public string 名稱 { get; set; }
            public string 套用值 { get; set; }
            public string 優先序 { get; set; }
        }
    }
}
