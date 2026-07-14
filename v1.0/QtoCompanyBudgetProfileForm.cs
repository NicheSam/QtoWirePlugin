using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;
using Autodesk.AutoCAD.DatabaseServices;

namespace QtoWirePlugin
{
    public sealed class QtoCompanyBudgetProfileForm : Form
    {
        private readonly Database database;
        private readonly QtoBudgetProjectData project;
        private QtoCompanyBudgetProfile profile;
        private readonly DataGridView bindingGrid;
        private readonly ComboBox projectItem;
        private readonly Label summary;

        public bool Changed { get; private set; }

        public QtoCompanyBudgetProfileForm(Database database, QtoBudgetProjectData project)
        {
            this.database = database;
            if (project == null) throw new ArgumentNullException("project");
            this.project = project;
            profile = QtoCompanyBudgetProfileStore.Load(database);

            Text = "公司預算規則庫";
            Width = 1080;
            Height = 680;
            MinimumSize = new System.Drawing.Size(820, 520);
            StartPosition = FormStartPosition.CenterParent;
            QtoUiTheme.ApplyForm(this);

            Panel header = new Panel { Dock = DockStyle.Top, Height = 64, Padding = new Padding(12, 8, 12, 4) };
            header.Controls.Add(new Label { Text = "公司預算規則庫", Dock = DockStyle.Top, Height = 30, Font = QtoUiTheme.HeaderFont, ForeColor = QtoUiTheme.TextColor });
            header.Controls.Add(new Label { Text = "先建立公司基準，再逐筆確認案件品項。公司規則不會因案件確認而自動改寫。", Dock = DockStyle.Bottom, Height = 24, ForeColor = QtoUiTheme.MutedTextColor });

            FlowLayoutPanel actions = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 48, Padding = new Padding(10, 8, 10, 4), WrapContents = false, AutoScroll = true };
            actions.Controls.Add(ActionButton("建立／重建公司基準", CreateBaseline, QtoButtonRole.Primary));
            actions.Controls.Add(ActionButton("重新比對案件", RebuildBindings, QtoButtonRole.Default));
            actions.Controls.Add(ActionButton("編輯品項別名", EditAliases, QtoButtonRole.Secondary));
            actions.Controls.Add(ActionButton("儲存並關閉", SaveAndClose, QtoButtonRole.Primary));

            summary = new Label { Dock = DockStyle.Top, Height = 58, Padding = new Padding(12, 8, 12, 6), AutoEllipsis = true, ForeColor = QtoUiTheme.MutedTextColor };
            bindingGrid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AutoGenerateColumns = true,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = true,
                RowHeadersVisible = false,
                BackgroundColor = System.Drawing.SystemColors.Window
            };
            QtoUiTheme.ApplyGrid(bindingGrid);

            TableLayoutPanel assignPanel = new TableLayoutPanel { Dock = DockStyle.Bottom, Height = 58, Padding = new Padding(10, 9, 10, 8), ColumnCount = 4, RowCount = 1 };
            assignPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112));
            assignPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            assignPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 116));
            assignPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 104));
            projectItem = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            assignPanel.Controls.Add(new Label { Text = "案件預算品項", Dock = DockStyle.Fill, TextAlign = System.Drawing.ContentAlignment.MiddleLeft }, 0, 0);
            assignPanel.Controls.Add(projectItem, 1, 0);
            assignPanel.Controls.Add(ActionButton("指定給選取列", AssignProjectItem, QtoButtonRole.Default), 2, 0);
            assignPanel.Controls.Add(ActionButton("確認選取", ConfirmSelected, QtoButtonRole.Primary), 3, 0);

            Controls.Add(bindingGrid);
            Controls.Add(assignPanel);
            Controls.Add(summary);
            Controls.Add(actions);
            Controls.Add(header);
            LoadView();
        }

        private static Button ActionButton(string text, EventHandler handler, QtoButtonRole role)
        {
            Button button = QtoUiTheme.CreateButton(text, handler, role);
            button.AutoSize = true;
            button.Dock = DockStyle.Fill;
            button.Margin = new Padding(4, 0, 4, 0);
            return button;
        }

        private void LoadView()
        {
            List<ProjectItemChoice> choices = project.MasterItems
                .Where(i => i != null && string.Equals(i.RowType, QtoBudgetRowType.Detail, StringComparison.OrdinalIgnoreCase))
                .OrderBy(i => i.SortOrder)
                .Select(i => new ProjectItemChoice(i.BudgetItemId, i.SourceSheet + "｜" + i.ItemName + "｜" + i.Unit))
                .ToList();
            projectItem.DataSource = choices;
            projectItem.DisplayMember = "Text";

            Dictionary<string, QtoCompanyBudgetItem> companyItems = profile == null
                ? new Dictionary<string, QtoCompanyBudgetItem>(StringComparer.OrdinalIgnoreCase)
                : profile.Items.Where(i => i != null).GroupBy(i => i.CompanyBudgetItemId, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
            Dictionary<string, QtoBudgetMasterItem> projectItems = project.MasterItems
                .Where(i => i != null && !string.IsNullOrWhiteSpace(i.BudgetItemId))
                .GroupBy(i => i.BudgetItemId, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            List<BindingView> rows = new List<BindingView>();
            foreach (QtoProjectBudgetBinding binding in project.CompanyBindings ?? new List<QtoProjectBudgetBinding>())
            {
                QtoCompanyBudgetItem company;
                QtoBudgetMasterItem item;
                companyItems.TryGetValue(binding.CompanyBudgetItemId ?? string.Empty, out company);
                projectItems.TryGetValue(binding.ProjectBudgetItemId ?? string.Empty, out item);
                rows.Add(new BindingView
                {
                    CompanyBudgetItemId = binding.CompanyBudgetItemId,
                    公司基準品項 = company == null ? "基準品項已不存在" : company.CanonicalName,
                    案件預算品項 = item == null ? "尚未指定" : item.ItemName,
                    單位 = company == null ? string.Empty : company.Unit,
                    別名 = company == null ? string.Empty : string.Join("、", company.Aliases ?? new List<string>()),
                    狀態 = StatusText(binding.Status),
                    說明 = binding.MatchReason
                });
            }
            bindingGrid.DataSource = new BindingList<BindingView>(rows);
            if (bindingGrid.Columns["CompanyBudgetItemId"] != null) bindingGrid.Columns["CompanyBudgetItemId"].Visible = false;

            int confirmed = (project.CompanyBindings ?? new List<QtoProjectBudgetBinding>()).Count(b => string.Equals(b.Status, "confirmed", StringComparison.OrdinalIgnoreCase));
            int review = (project.CompanyBindings ?? new List<QtoProjectBudgetBinding>()).Count - confirmed;
            summary.Text = profile == null
                ? "尚未建立公司預算基準。請先匯入案件預算，再按「建立／重建公司基準」。"
                : "公司基準版本 " + profile.Version + "｜基準品項 " + profile.Items.Count + " 筆｜已確認 " + confirmed + " 筆｜待確認 " + review + " 筆\r\n規則庫位置：" + QtoCompanyBudgetProfileStore.GetProfilePath(database);
        }

        private void CreateBaseline(object sender, EventArgs e)
        {
            if (project.MasterItems == null || project.MasterItems.Count == 0)
            {
                MessageBox.Show(this, "請先回到預算對應視窗匯入案件預算。", "公司預算規則庫", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (profile != null && MessageBox.Show(this, "重建會以目前案件預算更新公司基準品項。可辨識的公司 mapping 會保留，無法對應的品項需重新確認。確定繼續？", "重建公司基準", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;

            int expectedVersion = profile == null ? 0 : profile.Version;
            QtoCompanyBudgetProfile replacement = QtoCompanyBudgetProfileStore.CreateFromProject(project);
            QtoCompanyBudgetProfileStore.PreserveItemMetadata(profile, replacement);
            profile = replacement;
            QtoBudgetBindingService.RebuildBindings(project, profile);
            if (expectedVersion == 0)
            {
                QtoBudgetBindingService.PublishRules(project, profile, QtoBudgetProjectStore.LoadCompanyRules(database));
            }
            QtoCompanyBudgetProfileStore.Save(database, profile, expectedVersion);
            QtoBudgetBindingService.RebuildBindings(project, profile);
            Changed = true;
            LoadView();
        }

        private void EditAliases(object sender, EventArgs e)
        {
            if (profile == null)
            {
                MessageBox.Show(this, "請先建立公司預算基準。", "品項別名", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            List<string> selected = SelectedCompanyItemIds().Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (selected.Count != 1)
            {
                MessageBox.Show(this, "請只選擇一個公司基準品項，再編輯別名。", "品項別名", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            QtoCompanyBudgetItem item = profile.Items.FirstOrDefault(i => i != null && string.Equals(i.CompanyBudgetItemId, selected[0], StringComparison.OrdinalIgnoreCase));
            if (item == null) return;
            using (AliasEditorForm form = new AliasEditorForm(item.CanonicalName, item.Aliases))
            {
                if (form.ShowDialog(this) != DialogResult.OK) return;
                List<string> originalAliases = new List<string>(item.Aliases ?? new List<string>());
                try
                {
                    int expectedVersion = profile.Version;
                    item.Aliases = form.Aliases
                        .Where(a => !string.Equals(QtoCompanyBudgetProfileStore.NormalizeText(a), QtoCompanyBudgetProfileStore.NormalizeText(item.CanonicalName), StringComparison.OrdinalIgnoreCase))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();
                    QtoCompanyBudgetProfileStore.Save(database, profile, expectedVersion);
                    QtoBudgetBindingService.RebuildBindings(project, profile);
                    Changed = true;
                    LoadView();
                }
                catch (Exception ex)
                {
                    item.Aliases = originalAliases;
                    MessageBox.Show(this, "別名儲存失敗：" + ex.Message, "品項別名", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }

        private void RebuildBindings(object sender, EventArgs e)
        {
            if (profile == null) return;
            QtoBudgetBindingService.RebuildBindings(project, profile);
            Changed = true;
            LoadView();
        }

        private void AssignProjectItem(object sender, EventArgs e)
        {
            ProjectItemChoice choice = projectItem.SelectedItem as ProjectItemChoice;
            if (choice == null) return;
            foreach (string companyId in SelectedCompanyItemIds())
            {
                QtoProjectBudgetBinding binding = project.CompanyBindings.FirstOrDefault(b => string.Equals(b.CompanyBudgetItemId, companyId, StringComparison.OrdinalIgnoreCase));
                if (binding == null) continue;
                binding.ProjectBudgetItemId = choice.Value;
                binding.Status = "candidate";
                binding.MatchReason = "已由使用者指定，等待確認。";
                binding.UpdatedAt = DateTime.UtcNow;
            }
            Changed = true;
            LoadView();
        }

        private void ConfirmSelected(object sender, EventArgs e)
        {
            foreach (string companyId in SelectedCompanyItemIds())
            {
                QtoProjectBudgetBinding binding = project.CompanyBindings.FirstOrDefault(b => string.Equals(b.CompanyBudgetItemId, companyId, StringComparison.OrdinalIgnoreCase));
                if (binding == null || string.IsNullOrWhiteSpace(binding.ProjectBudgetItemId)) continue;
                binding.Status = "confirmed";
                binding.MatchReason = "已由使用者確認。";
                binding.UpdatedAt = DateTime.UtcNow;
            }
            if (profile != null) QtoBudgetBindingService.MergeCompanyRules(project, profile);
            Changed = true;
            LoadView();
        }

        private List<string> SelectedCompanyItemIds()
        {
            return bindingGrid.SelectedRows.Cast<DataGridViewRow>()
                .Select(r => r.DataBoundItem as BindingView)
                .Where(v => v != null && !string.IsNullOrWhiteSpace(v.CompanyBudgetItemId))
                .Select(v => v.CompanyBudgetItemId).ToList();
        }

        private void SaveAndClose(object sender, EventArgs e)
        {
            QtoBudgetProjectStore.Save(database, project);
            DialogResult = DialogResult.OK;
            Close();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (Changed) QtoBudgetProjectStore.Save(database, project);
            base.OnFormClosing(e);
        }

        private static string StatusText(string status)
        {
            if (string.Equals(status, "confirmed", StringComparison.OrdinalIgnoreCase)) return "已確認";
            if (string.Equals(status, "candidate", StringComparison.OrdinalIgnoreCase)) return "待確認";
            if (string.Equals(status, "blocked", StringComparison.OrdinalIgnoreCase)) return "已阻擋";
            return "未對應";
        }

        private sealed class ProjectItemChoice
        {
            public ProjectItemChoice(string value, string text) { Value = value; Text = text; }
            public string Value { get; private set; }
            public string Text { get; private set; }
        }

        private sealed class BindingView
        {
            public string CompanyBudgetItemId { get; set; }
            public string 公司基準品項 { get; set; }
            public string 案件預算品項 { get; set; }
            public string 單位 { get; set; }
            public string 別名 { get; set; }
            public string 狀態 { get; set; }
            public string 說明 { get; set; }
        }

        private sealed class AliasEditorForm : Form
        {
            private readonly TextBox aliases;

            public AliasEditorForm(string canonicalName, IEnumerable<string> values)
            {
                Text = "編輯品項別名";
                Width = 560;
                Height = 430;
                MinimumSize = new System.Drawing.Size(460, 340);
                StartPosition = FormStartPosition.CenterParent;
                Font = new System.Drawing.Font("Microsoft JhengHei UI", 9F);

                Label title = new Label
                {
                    Dock = DockStyle.Top,
                    Height = 58,
                    Padding = new Padding(12, 10, 12, 4),
                    Text = "公司基準品項：" + (canonicalName ?? string.Empty) + "\r\n每行輸入一個案件中可能出現的名稱。"
                };
                aliases = new TextBox
                {
                    Dock = DockStyle.Fill,
                    Multiline = true,
                    ScrollBars = ScrollBars.Vertical,
                    AcceptsReturn = true,
                    Padding = new Padding(8),
                    Text = string.Join(Environment.NewLine, values ?? new List<string>())
                };
                FlowLayoutPanel actions = new FlowLayoutPanel
                {
                    Dock = DockStyle.Bottom,
                    Height = 52,
                    FlowDirection = System.Windows.Forms.FlowDirection.RightToLeft,
                    Padding = new Padding(10, 9, 10, 7)
                };
                Button save = new Button { Text = "儲存", Width = 92, Height = 30, DialogResult = DialogResult.OK };
                Button cancel = new Button { Text = "取消", Width = 92, Height = 30, DialogResult = DialogResult.Cancel };
                actions.Controls.Add(save);
                actions.Controls.Add(cancel);
                Controls.Add(aliases);
                Controls.Add(title);
                Controls.Add(actions);
                AcceptButton = save;
                CancelButton = cancel;
            }

            public List<string> Aliases
            {
                get
                {
                    return aliases.Lines
                        .Select(line => (line ?? string.Empty).Trim())
                        .Where(line => line.Length > 0)
                        .ToList();
                }
            }
        }
    }
}
