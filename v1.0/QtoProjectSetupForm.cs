using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Autodesk.AutoCAD.ApplicationServices;

namespace QtoWirePlugin
{
    public sealed class QtoProjectSetupForm : Form
    {
        private readonly Document document;
        private readonly TableLayoutPanel statusTable;
        private readonly Label summary;

        public QtoProjectSetupForm(Document document)
        {
            this.document = document;
            Text = "QTO 案件設定";
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(920, 650);
            MinimumSize = new Size(760, 560);
            AutoScaleMode = AutoScaleMode.Dpi;
            QtoUiTheme.ApplyForm(this);

            TableLayoutPanel root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Padding = new Padding(16)
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
            Controls.Add(root);

            Panel header = new Panel { Dock = DockStyle.Fill };
            header.Controls.Add(new Label
            {
                Text = "案件開始前先確認必要設定",
                Dock = DockStyle.Top,
                Height = 36,
                Font = QtoUiTheme.HeaderFont,
                ForeColor = QtoUiTheme.TextColor
            });
            header.Controls.Add(QtoUiTheme.CreateMutedLabel("這裡只整理既有設定入口；未設定預算或範圍框不會阻擋繪圖。"));
            root.Controls.Add(header, 0, 0);

            summary = QtoUiTheme.CreateMutedLabel(string.Empty);
            summary.Dock = DockStyle.Fill;
            summary.Padding = new Padding(10, 10, 10, 8);
            root.Controls.Add(QtoUiTheme.WrapGroup("目前圖面", summary), 0, 1);

            statusTable = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                ColumnCount = 4,
                Padding = new Padding(8)
            };
            statusTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
            statusTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
            statusTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            statusTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
            root.Controls.Add(QtoUiTheme.WrapGroup("案件狀態", statusTable), 0, 2);

            FlowLayoutPanel actions = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Padding = new Padding(0, 8, 0, 0)
            };
            actions.Controls.Add(QtoUiTheme.CreateButton("開始繪圖", delegate { Close(); }, QtoButtonRole.Primary));
            actions.Controls.Add(QtoUiTheme.CreateButton("重新整理", delegate { RefreshStatus(); }, QtoButtonRole.Secondary));
            root.Controls.Add(actions, 0, 3);

            RefreshStatus();
        }

        private void RefreshStatus()
        {
            statusTable.SuspendLayout();
            statusTable.Controls.Clear();
            statusTable.RowStyles.Clear();
            statusTable.RowCount = 1;
            AddHeader();

            string drawingName = document == null || string.IsNullOrWhiteSpace(document.Name) ? "未命名圖面" : Path.GetFileName(document.Name);
            summary.Text = drawingName + "　｜　設定狀態會隨 DWG 保存；不會因開啟圖面自動建立 Excel。";
            if (document == null)
            {
                AddRow("圖面", "未設定", "沒有可用的 DWG。", null);
                statusTable.ResumeLayout();
                return;
            }

            string catalog = QtoProjectCatalogContext.GetConfiguredCatalogPath(document.Database);
            AddRow("圖塊資料庫", File.Exists(catalog) ? "已完成" : "未設定",
                File.Exists(catalog) ? catalog : "新案使用標準圖塊時建議先設定；接手舊圖可稍後處理。",
                CreateButton("管理圖塊庫", OpenCatalog));

            string workbook = QtoSyncCommandService.RestoreWorkbookPathForActiveDrawing();
            AddRow("預算 Excel", File.Exists(workbook) ? "已完成" : "未設定",
                File.Exists(workbook) ? workbook : "需要同步預算時再明確選擇或建立，不會自動產生。",
                CreateWorkbookButtons());

            QtoBudgetProjectData project = QtoBudgetProjectStore.Load(document.Database);
            AddRow("正式預算格式", project.MasterItems.Count > 0 ? "已完成" : "未設定",
                project.MasterItems.Count > 0
                    ? "來源：" + (project.SourceWorkbookPath ?? string.Empty) + "；品項 " + project.MasterItems.Count.ToString("0") + " 筆"
                    : "繪圖期間可略過，準備估算時再匯入正式預算。",
                CreateButton("匯入／預算對應", OpenBudgetMapping));

            int confirmed = project.MappingRules.Count(rule => string.Equals(rule.Status, "confirmed", StringComparison.OrdinalIgnoreCase));
            int pending = project.MappingRules.Count(rule => string.Equals(rule.Status, "candidate", StringComparison.OrdinalIgnoreCase)
                || string.Equals(rule.Status, "needs_review", StringComparison.OrdinalIgnoreCase));
            AddRow("預算對應", confirmed > 0 && pending == 0 ? "已完成" : (confirmed > 0 ? "需確認" : "未設定"),
                "已確認 " + confirmed.ToString("0") + "；待確認 " + pending.ToString("0"),
                CreateButton("檢查完整性", OpenCompleteness));

            var scopes = QtoScopeService.GetScopes(document.Database);
            int floorCount = scopes.Count(scope => string.Equals(scope.Kind, QtoXDataHelper.ScopeKindFloor, StringComparison.OrdinalIgnoreCase));
            int systemCount = scopes.Count(scope => string.Equals(scope.Kind, QtoXDataHelper.ScopeKindSystem, StringComparison.OrdinalIgnoreCase));
            AddRow("樓層／系統範圍", floorCount + systemCount > 0 ? "可開始" : "未設定",
                "樓層框 " + floorCount.ToString("0") + "；系統框 " + systemCount.ToString("0") + "。未設定不阻擋繪圖。",
                CreateButton("範圍管理", OpenScopes));

            AddRow("自動同步", QtoSyncCommandService.IsAutoSyncActive ? "已完成" : "可開始",
                QtoSyncCommandService.IsAutoSyncActive ? "CAD 指令完成後會更新已連結的 Excel。" : "預設暫停；需要時才啟用。",
                CreateButton(QtoSyncCommandService.IsAutoSyncActive ? "暫停同步" : "啟用同步", ToggleSync));

            statusTable.ResumeLayout();
        }

        private void AddHeader()
        {
            statusTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            AddCell("項目", 0, 0, true);
            AddCell("狀態", 1, 0, true);
            AddCell("目前內容", 2, 0, true);
            AddCell("操作", 3, 0, true);
        }

        private void AddRow(string title, string state, string detail, Control action)
        {
            int row = statusTable.RowCount++;
            statusTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
            AddCell(title, 0, row, false);
            Label stateLabel = AddCell(state, 1, row, false);
            QtoUiTheme.ApplyStatusColor(stateLabel, state);
            AddCell(detail, 2, row, false);
            if (action != null)
            {
                action.Dock = DockStyle.Fill;
                action.Margin = new Padding(4, 8, 4, 8);
                statusTable.Controls.Add(action, 3, row);
            }
        }

        private Label AddCell(string text, int column, int row, bool header)
        {
            Label label = new Label
            {
                Text = text,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true,
                Padding = new Padding(6, 0, 6, 0),
                Font = header ? QtoUiTheme.SectionFont : Font,
                ForeColor = header ? QtoUiTheme.TextColor : QtoUiTheme.MutedTextColor,
                BackColor = header ? QtoUiTheme.PanelBackColor : Color.Transparent
            };
            statusTable.Controls.Add(label, column, row);
            return label;
        }

        private Button CreateButton(string text, EventHandler handler)
        {
            return QtoUiTheme.CreateButton(text, handler, QtoButtonRole.Secondary);
        }

        private Control CreateWorkbookButtons()
        {
            FlowLayoutPanel panel = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Margin = Padding.Empty };
            Button select = CreateButton("選擇", SelectWorkbook);
            select.Width = 78;
            Button create = CreateButton("建立", CreateWorkbook);
            create.Width = 78;
            panel.Controls.Add(select);
            panel.Controls.Add(create);
            return panel;
        }

        private void OpenCatalog(object sender, EventArgs e)
        {
            string path = QtoProjectCatalogContext.GetPreferredCatalogPath(document.Database);
            using (QtoBlockCatalogManagerForm form = new QtoBlockCatalogManagerForm(path, QtoDictionaryLoader.LoadDefault(document.Database)))
            {
                QtoExternalWindowHost.ShowModal(form, this);
                if (!string.IsNullOrWhiteSpace(form.CatalogPath) && File.Exists(form.CatalogPath))
                {
                    QtoProjectCatalogContext.SetCatalogPath(document.Database, form.CatalogPath);
                }
            }
            RefreshStatus();
        }

        private void SelectWorkbook(object sender, EventArgs e)
        {
            using (OpenFileDialog dialog = new OpenFileDialog { Title = "選擇本圖面的預算 Excel", Filter = "Excel 檔案 (*.xlsx;*.xlsm)|*.xlsx;*.xlsm|所有檔案 (*.*)|*.*" })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                QtoSyncCommandService.LinkWorkbookToActiveDrawing(dialog.FileName);
            }
            RefreshStatus();
        }

        private void CreateWorkbook(object sender, EventArgs e)
        {
            using (SaveFileDialog dialog = new SaveFileDialog { Title = "建立本圖面的預算 Excel", Filter = "Excel 活頁簿 (*.xlsx)|*.xlsx", DefaultExt = "xlsx", AddExtension = true, FileName = Path.GetFileNameWithoutExtension(document.Name) + "_QTO預算.xlsx" })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                QtoSyncCommandService.LinkWorkbookToActiveDrawing(dialog.FileName);
                QtoExcelResult result = QtoSyncCommandService.FullRebuildExcel(dialog.FileName);
                if (!result.Success) MessageBox.Show(this, result.UserMessage, "建立預算 Excel", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            RefreshStatus();
        }

        private void OpenBudgetMapping(object sender, EventArgs e)
        {
            using (QtoBudgetMappingForm form = new QtoBudgetMappingForm(document.Database, QtoSyncCommandService.BuildCurrentDrawingRows()))
            {
                QtoExternalWindowHost.ShowModal(form, this);
            }
            RefreshStatus();
        }

        private void OpenCompleteness(object sender, EventArgs e)
        {
            using (QtoBudgetCompletenessForm form = new QtoBudgetCompletenessForm(document.Database, QtoSyncCommandService.BuildCurrentDrawingRows()))
            {
                QtoExternalWindowHost.ShowModal(form, this);
            }
            RefreshStatus();
        }

        private void OpenScopes(object sender, EventArgs e)
        {
            using (QtoScopeManagerForm form = new QtoScopeManagerForm(document))
            {
                QtoExternalWindowHost.ShowModal(form, this);
            }
            RefreshStatus();
        }

        private void ToggleSync(object sender, EventArgs e)
        {
            QtoCommandResult result = QtoSyncCommandService.IsAutoSyncActive ? QtoSyncCommandService.StopSync() : QtoSyncCommandService.StartSync();
            if (!result.Success) MessageBox.Show(this, result.UserMessage, "自動同步", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            RefreshStatus();
        }
    }
}
