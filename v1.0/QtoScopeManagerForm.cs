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

        public QtoScopeManagerForm(Document document)
        {
            this.document = document;
            Text = "樓層與系統範圍管理";
            Width = 900;
            Height = 600;
            MinimumSize = new System.Drawing.Size(720, 480);
            StartPosition = FormStartPosition.CenterParent;
            Font = new System.Drawing.Font("Microsoft JhengHei UI", 9F);

            FlowLayoutPanel actions = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 48, Padding = new Padding(10, 8, 10, 4), WrapContents = false };
            actions.Controls.Add(ActionButton("重新整理", RefreshRows));
            actions.Controls.Add(ActionButton("在 CAD 選取", SelectInCad));
            actions.Controls.Add(ActionButton("檢查影響", PreviewSelected));
            actions.Controls.Add(ActionButton("套用選取", ApplySelected));
            actions.Controls.Add(ActionButton("套用全部", ApplyAll));
            actions.Controls.Add(ActionButton("清除選取框線設定", ClearSelected));
            dynamicEnabled = new CheckBox { Text = "動態套用", AutoSize = true, Checked = QtoScopeService.DynamicEnabled, Padding = new Padding(12, 5, 0, 0) };
            dynamicEnabled.CheckedChanged += ToggleDynamic;
            actions.Controls.Add(dynamicEnabled);

            summary = new Label { Dock = DockStyle.Bottom, Height = 72, Padding = new Padding(12, 8, 12, 8), AutoEllipsis = true };
            grid = new DataGridView
            {
                Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false,
                AutoGenerateColumns = true, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = true,
                RowHeadersVisible = false, BackgroundColor = System.Drawing.SystemColors.Window
            };
            Controls.Add(grid);
            Controls.Add(summary);
            Controls.Add(actions);
            LoadRows();
        }

        private static Button ActionButton(string text, EventHandler handler)
        {
            Button button = new Button { Text = text, AutoSize = true, Height = 30, Margin = new Padding(0, 0, 8, 0) };
            button.Click += handler;
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
