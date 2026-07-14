using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace QtoWirePlugin
{
    internal static class QtoSyncMainPaletteHost
    {
        private static Form hostForm;

        public static void Show()
        {
            EnsureWindow();
            if (!hostForm.Visible)
            {
                QtoExternalWindowHost.ShowModeless(hostForm);
            }

            hostForm.Activate();
        }

        private static void EnsureWindow()
        {
            if (hostForm != null && !hostForm.IsDisposed)
            {
                return;
            }

            QtoSyncMainPalette panel = new QtoSyncMainPalette();
            panel.Dock = DockStyle.Fill;

            hostForm = new Form();
            hostForm.Text = "QTO 同步主控";
            hostForm.StartPosition = FormStartPosition.CenterScreen;
            hostForm.Size = new Size(900, 760);
            hostForm.MinimumSize = new Size(760, 620);
            hostForm.Controls.Add(panel);
            QtoUiTheme.ApplyForm(hostForm);
            hostForm.FormClosed += delegate
            {
                hostForm = null;
            };
        }
    }

    public class QtoSyncMainPalette : UserControl
    {
        private readonly Label statusValueLabel;
        private readonly Label excelValueLabel;
        private readonly Label catalogValueLabel;
        private readonly Label lastSyncValueLabel;
        private readonly Label budgetMappingValueLabel;
        private readonly Label reviewCountLabel;
        private readonly Label errorCountLabel;
        private readonly Label unconfiguredBlockCountLabel;
        private readonly TextBox excelPathTextBox;
        private readonly ListBox logListBox;
        private QtoSyncOverview overview;

        public QtoSyncMainPalette()
        {
            overview = QtoUiSampleData.CreateDefaultOverview();
            QtoSyncCommandService.RestoreWorkbookPathForActiveDrawing();
            if (!string.IsNullOrWhiteSpace(QtoSyncCommandService.CurrentExcelPath))
            {
                overview.ExcelPath = QtoSyncCommandService.CurrentExcelPath;
                overview.ExcelStatus = "已連結";
            }
            QtoSyncCommandService.AutoSyncCompleted += OnAutoSyncCompleted;

            Width = 500;
            Height = 720;
            MinimumSize = new Size(460, 660);
            AutoScroll = true;
            QtoUiTheme.ApplyPanel(this);

            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Top;
            root.AutoSize = true;
            root.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            root.Padding = QtoUiTheme.FormPadding;
            root.ColumnCount = 1;
            root.RowCount = 6;
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 220));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 150));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 180));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 160));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 230));
            Controls.Add(root);

            TableLayoutPanel headerPanel = new TableLayoutPanel();
            headerPanel.Dock = DockStyle.Fill;
            headerPanel.ColumnCount = 1;
            headerPanel.RowCount = 2;
            headerPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            headerPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            Label titleLabel = QtoUiTheme.CreateLabel("QTO 同步主控", ContentAlignment.BottomLeft);
            titleLabel.Font = QtoUiTheme.HeaderFont;
            headerPanel.Controls.Add(titleLabel, 0, 0);

            Label subtitleLabel = QtoUiTheme.CreateMutedLabel("啟用後，相關 CAD 指令完成時自動更新 Excel，並保留人工編修欄位。");
            headerPanel.Controls.Add(subtitleLabel, 0, 1);
            root.Controls.Add(headerPanel, 0, 0);

            TableLayoutPanel statusPanel = CreateGridPanel(5);
            AddSection(statusPanel, "同步狀態", 0);
            statusValueLabel = AddValueRow(statusPanel, 1, "目前狀態");
            excelValueLabel = AddValueRow(statusPanel, 2, "Excel");
            catalogValueLabel = AddValueRow(statusPanel, 3, "圖塊資料庫");
            budgetMappingValueLabel = AddValueRow(statusPanel, 4, "預算對應");
            lastSyncValueLabel = AddValueRow(statusPanel, 5, "最後同步");
            root.Controls.Add(WrapGroup("狀態", statusPanel), 0, 1);

            TableLayoutPanel excelPanel = new TableLayoutPanel();
            excelPanel.Dock = DockStyle.Fill;
            excelPanel.ColumnCount = 2;
            excelPanel.RowCount = 3;
            excelPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            excelPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112));
            excelPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            excelPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
            excelPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            excelPanel.Padding = new Padding(8, 4, 8, 6);

            excelPathTextBox = new TextBox();
            excelPathTextBox.Dock = DockStyle.Fill;
            excelPathTextBox.ReadOnly = true;
            excelPathTextBox.ScrollBars = ScrollBars.Horizontal;
            excelPathTextBox.Text = overview.ExcelPath;
            QtoUiTheme.ApplyReadOnlyTextBox(excelPathTextBox);
            excelPanel.Controls.Add(excelPathTextBox, 0, 0);
            excelPanel.SetColumnSpan(excelPathTextBox, 2);
            Label excelHintLabel = QtoUiTheme.CreateMutedLabel("未建立時會自動產生；也可選擇既有預算 Excel 後更新。");
            excelPanel.Controls.Add(excelHintLabel, 0, 1);
            excelPanel.SetColumnSpan(excelHintLabel, 2);
            excelPanel.Controls.Add(CreateButton("選擇既有 Excel", SelectExcelButtonClick), 0, 2);
            excelPanel.Controls.Add(CreateButton("開啟 Excel", OpenExcelButtonClick), 1, 2);
            root.Controls.Add(WrapGroup("Excel", excelPanel), 0, 2);

            TableLayoutPanel actionPanel = new TableLayoutPanel();
            actionPanel.Dock = DockStyle.Fill;
            actionPanel.ColumnCount = 2;
            actionPanel.RowCount = 3;
            actionPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
            actionPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            actionPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            actionPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            actionPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            actionPanel.Padding = new Padding(8, 6, 8, 6);
            actionPanel.Controls.Add(CreateButton("立即重整 Excel", FullRebuildButtonClick, QtoButtonRole.Secondary), 0, 0);
            actionPanel.Controls.Add(CreateWorkflowText("自動同步失敗或版面需強制重套時使用。"), 1, 0);
            actionPanel.Controls.Add(CreateButton("檢查與修復", ValidateButtonClick, QtoButtonRole.Default), 0, 1);
            actionPanel.Controls.Add(CreateWorkflowText("開啟檢查清單，可多選、定位、開啟屬性或安全修復。"), 1, 1);

            TableLayoutPanel syncSwitchPanel = new TableLayoutPanel();
            syncSwitchPanel.Dock = DockStyle.Fill;
            syncSwitchPanel.ColumnCount = 2;
            syncSwitchPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            syncSwitchPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            syncSwitchPanel.Controls.Add(CreateButton("啟用自動同步", StartSyncButtonClick, QtoButtonRole.Primary), 0, 0);
            syncSwitchPanel.Controls.Add(CreateButton("暫停自動同步", StopSyncButtonClick, QtoButtonRole.Secondary), 1, 0);
            actionPanel.Controls.Add(syncSwitchPanel, 0, 2);
            actionPanel.SetColumnSpan(syncSwitchPanel, 2);
            root.Controls.Add(WrapGroup("作業流程", actionPanel), 0, 3);

            TableLayoutPanel pendingPanel = CreateGridPanel(3);
            AddSection(pendingPanel, "待處理", 0);
            reviewCountLabel = AddValueRow(pendingPanel, 1, "需確認");
            errorCountLabel = AddValueRow(pendingPanel, 2, "錯誤");
            unconfiguredBlockCountLabel = AddValueRow(pendingPanel, 3, "未設定圖塊");
            root.Controls.Add(WrapGroup("待處理", pendingPanel), 0, 4);

            TableLayoutPanel bottomPanel = new TableLayoutPanel();
            bottomPanel.Dock = DockStyle.Fill;
            bottomPanel.ColumnCount = 1;
            bottomPanel.RowCount = 2;
            bottomPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            bottomPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            bottomPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            FlowLayoutPanel quickPanel = new FlowLayoutPanel();
            quickPanel.Dock = DockStyle.Fill;
            quickPanel.Padding = new Padding(0, 4, 0, 0);
            quickPanel.WrapContents = false;
            quickPanel.Controls.Add(CreateSmallButton("檢查清單", ReviewButtonClick));
            quickPanel.Controls.Add(CreateSmallButton("預算完整性", CompletenessButtonClick));
            quickPanel.Controls.Add(CreateSmallButton("同步紀錄", LogButtonClick));
            quickPanel.Controls.Add(CreateSmallButton("預算表設定", SettingsButtonClick));
            bottomPanel.Controls.Add(quickPanel, 0, 0);

            logListBox = new ListBox();
            logListBox.Dock = DockStyle.Fill;
            logListBox.IntegralHeight = false;
            logListBox.BackColor = Color.White;
            logListBox.BorderStyle = BorderStyle.FixedSingle;
            logListBox.Items.Add("尚未開始同步。");
            bottomPanel.Controls.Add(logListBox, 0, 1);
            root.Controls.Add(WrapGroup("同步紀錄", bottomPanel), 0, 5);

            RefreshOverview();
        }

        public void SetOverview(QtoSyncOverview syncOverview)
        {
            overview = syncOverview ?? QtoUiSampleData.CreateDefaultOverview();
            if (!string.IsNullOrWhiteSpace(overview.ExcelPath)
                && !string.Equals(overview.ExcelPath, "尚未選擇", StringComparison.Ordinal))
            {
                QtoSyncCommandService.LinkWorkbookToActiveDrawing(overview.ExcelPath);
            }
            RefreshOverview();
        }

        private void RefreshOverview()
        {
            statusValueLabel.Text = overview.CurrentStatus;
            excelValueLabel.Text = overview.ExcelStatus;
            catalogValueLabel.Text = overview.CatalogStatus;
            budgetMappingValueLabel.Text = GetBudgetMappingStatus();
            lastSyncValueLabel.Text = overview.LastSyncText;
            excelPathTextBox.Text = GetExcelDisplayPath();
            reviewCountLabel.Text = overview.ReviewCount.ToString("0");
            errorCountLabel.Text = overview.ErrorCount.ToString("0");
            unconfiguredBlockCountLabel.Text = overview.UnconfiguredBlockCount.ToString("0");
            QtoUiTheme.ApplyStatusColor(statusValueLabel, overview.CurrentStatus);
            QtoUiTheme.ApplyStatusColor(excelValueLabel, overview.ExcelStatus);
        }

        private static string GetBudgetMappingStatus()
        {
            Autodesk.AutoCAD.ApplicationServices.Document document = Autodesk.AutoCAD.ApplicationServices.Core.Application.DocumentManager.MdiActiveDocument;
            if (document == null) return "尚未載入圖面";
            try
            {
                QtoBudgetProjectData data = QtoBudgetProjectStore.Load(document.Database);
                int confirmed = data.MappingRules.Count(r => string.Equals(r.Status, "confirmed", StringComparison.OrdinalIgnoreCase));
                int pending = data.MappingRules.Count(r => string.Equals(r.Status, "candidate", StringComparison.OrdinalIgnoreCase) || string.Equals(r.Status, "needs_review", StringComparison.OrdinalIgnoreCase));
                int blocked = data.MappingRules.Count(r => string.Equals(r.Status, "blocked", StringComparison.OrdinalIgnoreCase));
                return "已確認 " + confirmed + "／待確認 " + pending + "／阻擋 " + blocked;
            }
            catch { return "讀取失敗"; }
        }

        private static GroupBox WrapGroup(string title, Control content)
        {
            return QtoUiTheme.WrapGroup(title, content);
        }

        private static TableLayoutPanel CreateGridPanel(int dataRows)
        {
            TableLayoutPanel panel = new TableLayoutPanel();
            panel.Dock = DockStyle.Fill;
            panel.ColumnCount = 2;
            panel.RowCount = dataRows + 1;
            panel.Padding = new Padding(10, 8, 10, 8);
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            for (int i = 1; i < panel.RowCount; i++)
            {
                panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
            }

            return panel;
        }

        private static void AddSection(TableLayoutPanel panel, string title, int row)
        {
            Label label = new Label();
            label.Text = title;
            label.Font = QtoUiTheme.SectionFont;
            label.Dock = DockStyle.Fill;
            label.TextAlign = ContentAlignment.MiddleLeft;
            label.ForeColor = QtoUiTheme.TextColor;
            panel.Controls.Add(label, 0, row);
            panel.SetColumnSpan(label, 2);
        }

        private static Label AddValueRow(TableLayoutPanel panel, int row, string labelText)
        {
            Label label = new Label();
            label.Text = labelText;
            label.Dock = DockStyle.Fill;
            label.TextAlign = ContentAlignment.MiddleLeft;
            label.ForeColor = QtoUiTheme.MutedTextColor;
            panel.Controls.Add(label, 0, row);

            Label value = new Label();
            value.Text = "-";
            value.Dock = DockStyle.Fill;
            value.TextAlign = ContentAlignment.MiddleLeft;
            value.ForeColor = QtoUiTheme.TextColor;
            panel.Controls.Add(value, 1, row);
            return value;
        }

        private static Button CreateButton(string text, EventHandler handler)
        {
            return CreateButton(text, handler, QtoButtonRole.Default);
        }

        private static Button CreateButton(string text, EventHandler handler, QtoButtonRole role)
        {
            Button button = QtoUiTheme.CreateButton(text, handler, role);
            button.Dock = DockStyle.Fill;
            return button;
        }

        private static Button CreateSmallButton(string text, EventHandler handler)
        {
            Button button = QtoUiTheme.CreateButton(text, handler, QtoButtonRole.Secondary);
            button.Width = 106;
            button.Height = 30;
            button.Margin = new Padding(4, 2, 4, 2);
            return button;
        }

        private static Label CreateWorkflowText(string text)
        {
            Label label = QtoUiTheme.CreateMutedLabel(text);
            label.Padding = new Padding(6, 0, 0, 0);
            return label;
        }

        private string GetExcelDisplayPath()
        {
            if (string.IsNullOrWhiteSpace(overview.ExcelPath)
                || string.Equals(overview.ExcelPath, "尚未選擇", StringComparison.Ordinal))
            {
                return "尚未連結；請先選擇既有 Excel，或從案件設定明確建立";
            }

            return overview.ExcelPath;
        }

        private void SelectExcelButtonClick(object sender, EventArgs e)
        {
            using (System.Windows.Forms.OpenFileDialog dialog = new System.Windows.Forms.OpenFileDialog())
            {
                dialog.Title = "選擇同步 Excel";
                dialog.Filter = "Excel 檔案 (*.xlsx;*.xlsm;*.xls)|*.xlsx;*.xlsm;*.xls|所有檔案 (*.*)|*.*";

                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                overview.ExcelPath = dialog.FileName;
                QtoSyncCommandService.LinkWorkbookToActiveDrawing(dialog.FileName);
                overview.ExcelStatus = "已選擇";
                overview.CurrentStatus = "尚未開始同步";
                RefreshOverview();
                AddLog("已選擇 Excel：" + dialog.FileName);
            }
        }

        private void OpenExcelButtonClick(object sender, EventArgs e)
        {
            QtoCommandResult result = QtoSyncCommandService.OpenCurrentExcel();
            AddLog(result.UserMessage);
            if (!result.Success)
            {
                MessageBox.Show(this, result.UserMessage, "開啟 Excel", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void StartSyncButtonClick(object sender, EventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(overview.ExcelPath)
                && !string.Equals(overview.ExcelPath, "尚未選擇", StringComparison.Ordinal))
            {
                QtoSyncCommandService.LinkWorkbookToActiveDrawing(overview.ExcelPath);
            }

            QtoCommandResult result = QtoSyncCommandService.StartSync();
            overview.CurrentStatus = result.Success ? "自動同步中" : "同步失敗";
            if (!string.IsNullOrWhiteSpace(QtoSyncCommandService.CurrentExcelPath))
            {
                overview.ExcelPath = QtoSyncCommandService.CurrentExcelPath;
                overview.ExcelStatus = result.Success ? "已連結" : "更新失敗";
            }
            if (result.Success)
            {
                overview.LastSyncText = DateTime.Now.ToString("yyyy/MM/dd HH:mm");
            }
            RefreshOverview();
            AddLog(result.UserMessage);
            if (!result.Success)
            {
                MessageBox.Show(this, result.UserMessage + "\r\n\r\n" + FirstLine(result.TechnicalDetail), "啟用自動同步", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void StopSyncButtonClick(object sender, EventArgs e)
        {
            QtoCommandResult result = QtoSyncCommandService.StopSync();
            overview.CurrentStatus = "已暫停";
            RefreshOverview();
            AddLog(result.UserMessage);
        }

        private void FullRebuildButtonClick(object sender, EventArgs e)
        {
            Button button = sender as Button;
            if (button != null)
            {
                button.Enabled = false;
            }

            try
            {
                overview.CurrentStatus = "更新中";
                RefreshOverview();

                QtoExcelResult result = QtoSyncCommandService.FullRebuildExcel(QtoSyncCommandService.CurrentExcelPath);
                if (!string.IsNullOrWhiteSpace(result.WorkbookPath))
                {
                    overview.ExcelPath = result.WorkbookPath;
                    overview.ExcelStatus = result.Success ? "已更新" : "更新失敗";
                    overview.LastSyncText = DateTime.Now.ToString("yyyy/MM/dd HH:mm");
                }

                overview.CurrentStatus = result.Success ? "更新完成" : "更新失敗";
                RefreshOverview();
                AddLog(result.UserMessage);
                string message = result.UserMessage;
                if (result.Success)
                {
                    message += "\r\n\r\n預算草稿列數：" + result.BudgetDraftRowCount.ToString("0");
                    message += "\r\n需人工確認：" + result.BudgetDraftReviewCount.ToString("0");
                    message += "\r\n保留人工編修：" + result.PreservedManualEditCount.ToString("0");
                }
                if (!result.Success && !string.IsNullOrWhiteSpace(result.TechnicalDetail))
                {
                    string detail = FirstLine(result.TechnicalDetail);
                    AddLog("失敗原因：" + detail);
                    message += "\r\n\r\n原因：" + detail;
                }

                MessageBox.Show(this, message, "更新預算 Excel", MessageBoxButtons.OK, result.Success ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                overview.CurrentStatus = "更新失敗";
                overview.ExcelStatus = "更新失敗";
                RefreshOverview();
                AddLog("更新預算 Excel 失敗：" + ex.Message);
                MessageBox.Show(this, "更新預算 Excel 失敗，AutoCAD 可繼續使用。\r\n\r\n" + ex.Message, "更新預算 Excel", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                if (button != null)
                {
                    button.Enabled = true;
                }
            }
        }

        private void ValidateButtonClick(object sender, EventArgs e)
        {
            QtoValidationResult result = QtoSyncCommandService.ValidateCurrentDrawing();
            overview.CurrentStatus = result.HasIssues ? "有問題待確認" : "檢查完成";
            overview.ReviewCount = result.ReviewItems.Count;
            overview.ErrorCount = result.HasErrors ? 1 : 0;
            overview.UnconfiguredBlockCount = 0;
            RefreshOverview();
            AddLog("檢查完成：需確認 " + result.ReviewItems.Count.ToString("0") + " 項。");
            QtoDialogService.ShowReview(result.ReviewItems);
        }

        private void ReviewButtonClick(object sender, EventArgs e)
        {
            QtoValidationResult result = QtoSyncCommandService.LastValidationResult ?? QtoSyncCommandService.ValidateCurrentDrawing();
            QtoDialogService.ShowReview(result.ReviewItems);
        }

        private void CompletenessButtonClick(object sender, EventArgs e)
        {
            Autodesk.AutoCAD.ApplicationServices.Document document = Autodesk.AutoCAD.ApplicationServices.Core.Application.DocumentManager.MdiActiveDocument;
            if (document == null) return;
            using (QtoBudgetCompletenessForm form = new QtoBudgetCompletenessForm(document.Database, QtoSyncCommandService.BuildCurrentDrawingRows()))
            {
                QtoExternalWindowHost.ShowModal(form, FindForm());
            }
            RefreshOverview();
        }

        private void LogButtonClick(object sender, EventArgs e)
        {
            ScrollControlIntoView(logListBox);
            if (logListBox.Items.Count > 0)
            {
                logListBox.SelectedIndex = logListBox.Items.Count - 1;
            }
            logListBox.Focus();
        }

        private void SettingsButtonClick(object sender, EventArgs e)
        {
            QtoBudgetLayoutSettings current = QtoBudgetLayoutSettingsService.Load(QtoSyncCommandService.CurrentExcelPath);
            QtoBudgetLayoutSettings updated;
            if (!QtoDialogService.EditBudgetLayout(current, this, out updated))
            {
                return;
            }

            QtoBudgetLayoutSettingsService.Set(QtoSyncCommandService.CurrentExcelPath, updated);
            QtoExcelResult result = QtoSyncCommandService.FullRebuildExcel(QtoSyncCommandService.CurrentExcelPath);
            if (!string.IsNullOrWhiteSpace(result.WorkbookPath))
            {
                overview.ExcelPath = result.WorkbookPath;
                overview.ExcelStatus = result.Success ? "已更新" : "更新失敗";
            }
            overview.CurrentStatus = result.Success
                ? (QtoSyncCommandService.IsAutoSyncActive ? "自動同步中" : "版面已更新")
                : "版面更新失敗";
            overview.LastSyncText = DateTime.Now.ToString("yyyy/MM/dd HH:mm");
            RefreshOverview();
            AddLog(result.Success ? "預算表排列與顯示設定已套用。" : result.UserMessage);
            if (!result.Success)
            {
                MessageBox.Show(this, result.UserMessage + "\r\n\r\n" + FirstLine(result.TechnicalDetail), "預算表設定", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void OnAutoSyncCompleted(object sender, QtoExcelSyncCompletedEventArgs e)
        {
            if (IsDisposed || e == null)
            {
                return;
            }

            if (InvokeRequired)
            {
                BeginInvoke(new Action<object, QtoExcelSyncCompletedEventArgs>(OnAutoSyncCompleted), sender, e);
                return;
            }

            QtoExcelResult result = e.Result;
            if (result == null)
            {
                return;
            }

            overview.CurrentStatus = result.Success ? "自動同步中" : "自動同步待處理";
            overview.ExcelStatus = result.Success ? "已自動更新" : "更新未完成";
            if (!string.IsNullOrWhiteSpace(result.WorkbookPath))
            {
                overview.ExcelPath = result.WorkbookPath;
            }
            overview.LastSyncText = DateTime.Now.ToString("yyyy/MM/dd HH:mm");
            RefreshOverview();
            AddLog(result.Success ? "CAD 變更已自動同步到 Excel。" : result.UserMessage);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                QtoSyncCommandService.AutoSyncCompleted -= OnAutoSyncCompleted;
            }
            base.Dispose(disposing);
        }

        private void AddLog(string message)
        {
            logListBox.Items.Insert(0, DateTime.Now.ToString("HH:mm") + "  " + message);
        }

        private static string FirstLine(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            foreach (string line in lines)
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    return line.Trim();
                }
            }

            return string.Empty;
        }

    }
}
