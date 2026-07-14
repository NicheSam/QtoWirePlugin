using System;
using System.Drawing;
using System.Windows.Forms;
using Autodesk.AutoCAD.ApplicationServices;

namespace QtoWirePlugin
{
    internal static class QtoWorkflowFormHost
    {
        private static QtoWorkflowForm form;

        public static void Show()
        {
            if (form == null || form.IsDisposed)
            {
                form = new QtoWorkflowForm();
                form.FormClosed += delegate { form = null; };
                QtoExternalWindowHost.ShowModeless(form);
                return;
            }

            if (form.WindowState == FormWindowState.Minimized)
            {
                form.WindowState = FormWindowState.Normal;
            }

            form.BringToFront();
            form.Activate();
        }
    }

    public class QtoWorkflowForm : Form
    {
        private readonly TextBox statusTextBox;

        public QtoWorkflowForm()
        {
            Text = "QTO 弱電配線操作面板";
            StartPosition = FormStartPosition.CenterScreen;
            Width = 960;
            Height = 720;
            MinimumSize = new Size(760, 560);
            AutoScaleMode = AutoScaleMode.Dpi;
            QtoUiTheme.ApplyForm(this);

            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.Padding = QtoUiTheme.FormPadding;
            root.ColumnCount = 1;
            root.RowCount = 3;
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            Controls.Add(root);

            Panel header = new Panel { Dock = DockStyle.Fill };
            Label titleLabel = new Label
            {
                Text = "QTO 弱電工作流程",
                Font = QtoUiTheme.HeaderFont,
                AutoSize = true,
                Location = new Point(0, 2)
            };
            Label hintLabel = new Label
            {
                Text = "依目前工作選擇分頁；常用繪圖在前，整理、預算與報表分開處理。",
                ForeColor = QtoUiTheme.MutedTextColor,
                AutoSize = true,
                Location = new Point(2, 34)
            };
            header.Controls.Add(titleLabel);
            header.Controls.Add(hintLabel);
            root.Controls.Add(header, 0, 0);

            TabControl tabs = new TabControl { Dock = DockStyle.Fill };
            tabs.TabPages.Add(CreateWorkflowTab("繪圖與連接", new[]
            {
                CreateActionGroup("案件開始", "新案先確認圖塊庫；Excel、預算與範圍框可在需要時再設定，不阻擋繪圖。", new[]
                {
                    CreateFlowButton("案件設定", "QTO_PROJECT_SETUP", true)
                }),
                CreateActionGroup("1  放置與標記", "先建立有 QTO 資訊的設備、出線口與箱體。", new[]
                {
                    CreateFlowButton("連續放置設備", "QTO_PLACE_CATALOG_CONTINUOUS", true),
                    CreateFlowButton("標記出線口", "QTO_MARK_OUTLETS", false),
                    CreateFlowButton("標記箱體", "QTO_MARK_JB", false)
                }),
                CreateActionGroup("2  編輯與連接", "選取後修改屬性，再建立出線口、箱體與配線關係。", new[]
                {
                    CreateFlowButton("屬性面板", "QTO_PROPERTY_PANEL", true),
                    CreateFlowButton("編輯出線口", "QTO_EDIT_OUTLET_PROPERTIES", false),
                    CreateFlowButton("編輯箱體", "QTO_EDIT_JB_PROPERTIES", false),
                    CreateFlowButton("指向箱體", "QTO_CALLOUT_TO_JB", false),
                    CreateFlowButton("編號標註", "QTO_LABEL_OUTLETS", false),
                    CreateFlowButton("一鍵連接", "QTO_CREATE_CONNECTIONS", false)
                }),
                CreateActionGroup("3  線管與線槽", "需要路徑計量時再使用；一般設備放置不必進入此區。", new[]
                {
                    CreateFlowButton("直接繪製線管", "QTO_DRAW_QTO_PATH", true),
                    CreateFlowButton("標記線槽", "QTO_MARK_TRAY", false),
                    CreateFlowButton("批次尋路", "QTO_BATCH_ROUTE_BY_TRAY", false),
                    CreateFlowButton("管段到線槽", "QTO_CONDUIT_TO_TRAY", false),
                    CreateFlowButton("清除線槽屬性", "QTO_CLEAR_TRAY_PROPERTIES", false)
                })
            }));
            tabs.TabPages.Add(CreateWorkflowTab("整理與檢查", new[]
            {
                CreateActionGroup("範圍與分類", "用樓層框、系統框或條件選取批次整理既有 QTO 物件。", new[]
                {
                    CreateFlowButton("範圍管理", "QTO_SCOPE_MANAGER", true),
                    CreateFlowButton("選取相同 QTO", "QTO_SELECT_SAME_QTO", false),
                    CreateFlowButton("轉換既有物件", "QTO_CONVERT_LEGACY_OBJECTS", false)
                }),
                CreateActionGroup("問題處理", "先檢查，再在同一份清單中定位、修復或人工確認。", new[]
                {
                    CreateFlowButton("檢查與修復", "QTO_VALIDATE", true),
                    CreateFlowButton("刪除出線口資訊", "QTO_DELETE_OUTLET_INFO", false),
                    CreateFlowButton("刪除箱體資訊", "QTO_DELETE_JB_INFO", false)
                })
            }));
            tabs.TabPages.Add(CreateWorkflowTab("預算與圖塊", new[]
            {
                CreateActionGroup("預算流程", "先連結預算 Excel，再建立並確認 CAD 計量群組與預算品項的對應。", new[]
                {
                    CreateFlowButton("同步主控", "QTO_PANEL", true),
                    CreateFlowButton("預算對應", "QTO_BUDGET_MAPPING", true),
                    CreateFlowButton("預算完整性", "QTO_BUDGET_COMPLETENESS", true),
                    CreateFlowButton("更新預算 Excel", "QTO_SYNC_FULL_REBUILD", false)
                }),
                CreateActionGroup("標準圖塊", "插入日常使用圖塊；資料庫管理與專案更新只在需要時開啟。", new[]
                {
                    CreateFlowButton("插入標準圖塊", "QTO_INSERT_CATALOG_BLOCK", true),
                    CreateFlowButton("管理圖塊庫", "QTO_BLOCK_LIBRARY_MANAGER", false),
                    CreateFlowButton("更新專案圖塊", "QTO_UPDATE_PROJECT_BLOCKS", false)
                })
            }));
            tabs.TabPages.Add(CreateWorkflowTab("報表", new[]
            {
                CreateActionGroup("檢查與交付", "報表是檢查與整理用途；預算 Excel 請從同步主控處理。", new[]
                {
                    CreateFlowButton("出線口清單", "QTO_EXPORT_OUTLETS", true),
                    CreateFlowButton("配線關係明細", "QTO_CHECK_WIRE", false),
                    CreateFlowButton("線段明細", "QTO_EXPORT_JB_SUMMARY", false),
                    CreateFlowButton("管段明細", "QTO_EXPORT_CONDUIT_SUMMARY", false),
                    CreateFlowButton("預算前置 CSV", "QTO_EXPORT_BUDGET_INPUT", false)
                })
            }));
            root.Controls.Add(tabs, 0, 1);

            statusTextBox = new TextBox();
            statusTextBox.Dock = DockStyle.Fill;
            statusTextBox.ReadOnly = true;
            statusTextBox.Text = "就緒。";
            QtoUiTheme.ApplyReadOnlyTextBox(statusTextBox);
            root.Controls.Add(statusTextBox, 0, 2);
        }

        private TabPage CreateWorkflowTab(string title, Control[] groups)
        {
            TabPage tab = new TabPage(title) { BackColor = QtoUiTheme.WindowBackColor, Padding = new Padding(8) };
            FlowLayoutPanel flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                FlowDirection = System.Windows.Forms.FlowDirection.TopDown,
                WrapContents = false,
                Padding = new Padding(2)
            };
            flow.SizeChanged += delegate
            {
                foreach (Control control in flow.Controls)
                {
                    control.Width = Math.Max(500, flow.ClientSize.Width - 28);
                }
            };
            foreach (Control group in groups)
            {
                group.Width = 820;
                flow.Controls.Add(group);
            }
            tab.Controls.Add(flow);
            return tab;
        }

        private Control CreateActionGroup(string title, string hint, Button[] buttons)
        {
            GroupBox group = new GroupBox
            {
                Text = title,
                Height = 116,
                Padding = QtoUiTheme.GroupPadding,
                BackColor = QtoUiTheme.PanelBackColor
            };
            Label hintLabel = new Label
            {
                Text = hint,
                Dock = DockStyle.Top,
                Height = 26,
                ForeColor = QtoUiTheme.MutedTextColor
            };
            FlowLayoutPanel actions = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                WrapContents = false,
                Padding = new Padding(0, 4, 0, 0)
            };
            actions.Controls.AddRange(buttons);
            group.Controls.Add(actions);
            group.Controls.Add(hintLabel);
            return group;
        }

        private Button CreateFlowButton(string text, string commandName, bool primary)
        {
            Button button = CreateButton(text, 0, 0, 158, 34, commandName);
            button.Margin = new Padding(0, 0, 8, 0);
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderColor = primary ? QtoUiTheme.PrimaryColor : QtoUiTheme.BorderColor;
            button.BackColor = primary ? QtoUiTheme.PrimaryColor : Color.White;
            button.ForeColor = primary ? Color.White : QtoUiTheme.TextColor;
            button.UseVisualStyleBackColor = false;
            return button;
        }

        private static GroupBox CreateGroup(string title, int x, int y, int width, int height)
        {
            GroupBox groupBox = new GroupBox();
            groupBox.Text = title;
            groupBox.Location = new Point(x, y);
            groupBox.Size = new Size(width, height);
            return groupBox;
        }

        private static Panel CreateDiagramPanel(int x, int y, int width, int height)
        {
            Panel panel = new Panel();
            panel.Location = new Point(x, y);
            panel.Size = new Size(width, height);
            panel.BackColor = Color.White;
            panel.BorderStyle = BorderStyle.FixedSingle;
            panel.Paint += DiagramPanelPaint;
            return panel;
        }

        private static void DiagramPanelPaint(object sender, PaintEventArgs e)
        {
            Graphics graphics = e.Graphics;
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            graphics.Clear(Color.White);

            using (Pen trayPen = new Pen(Color.FromArgb(70, 90, 120), 5.0f))
            using (Pen wirePen = new Pen(Color.FromArgb(44, 132, 87), 2.4f))
            using (Brush outletBrush = new SolidBrush(Color.FromArgb(32, 126, 190)))
            using (Brush jbBrush = new SolidBrush(Color.FromArgb(230, 142, 45)))
            using (Brush textBrush = new SolidBrush(Color.FromArgb(40, 40, 40)))
            using (Font labelFont = new Font(SystemFonts.MessageBoxFont.FontFamily, 9.0f, FontStyle.Regular))
            {
                Point trayStart = new Point(54, 118);
                Point trayTurn = new Point(160, 118);
                Point trayEnd = new Point(238, 76);
                graphics.DrawLines(trayPen, new Point[] { trayStart, trayTurn, trayEnd });

                Point outletA = new Point(48, 44);
                Point outletB = new Point(100, 64);
                Point outletC = new Point(150, 38);
                Point box = new Point(248, 68);

                DrawWire(graphics, wirePen, outletA, new Point(54, 118));
                DrawWire(graphics, wirePen, outletB, new Point(100, 118));
                DrawWire(graphics, wirePen, outletC, new Point(150, 118));

                graphics.FillEllipse(outletBrush, outletA.X - 8, outletA.Y - 8, 16, 16);
                graphics.FillEllipse(outletBrush, outletB.X - 8, outletB.Y - 8, 16, 16);
                graphics.FillEllipse(outletBrush, outletC.X - 8, outletC.Y - 8, 16, 16);
                graphics.FillRectangle(jbBrush, box.X - 13, box.Y - 13, 26, 26);

                graphics.DrawString("出線口", labelFont, textBrush, 26, 18);
                graphics.DrawString("線槽路徑", labelFont, textBrush, 112, 132);
                graphics.DrawString("箱體", labelFont, textBrush, 232, 28);
                graphics.DrawString("先標記，再編輯屬性，最後沿線槽產生配線", labelFont, textBrush, 20, 156);
            }
        }

        private static void DrawWire(Graphics graphics, Pen pen, Point outlet, Point trayPoint)
        {
            Point mid = new Point(outlet.X, trayPoint.Y);
            graphics.DrawLines(pen, new Point[] { outlet, mid, trayPoint });
        }

        private Button CreateButton(string text, int x, int y, int width, int height, string commandName)
        {
            Button button = new Button();
            button.Text = text;
            button.Location = new Point(x, y);
            button.Size = new Size(width, height);
            button.Tag = commandName;
            button.Image = CreateButtonIcon(commandName);
            button.ImageAlign = ContentAlignment.MiddleLeft;
            button.TextAlign = ContentAlignment.MiddleCenter;
            button.Padding = new Padding(6, 0, 6, 0);
            button.Click += RunCommandButtonClick;
            return button;
        }

        private static Bitmap CreateButtonIcon(string commandName)
        {
            Bitmap bitmap = new Bitmap(24, 24);

            using (Graphics graphics = Graphics.FromImage(bitmap))
            using (Pen cyanPen = new Pen(Color.FromArgb(108, 190, 230), 2.0f))
            using (Pen amberPen = new Pen(Color.FromArgb(255, 188, 72), 2.0f))
            using (Pen whitePen = new Pen(Color.FromArgb(80, 88, 96), 1.6f))
            using (Pen redPen = new Pen(Color.FromArgb(232, 82, 82), 2.2f))
            using (Brush cyanBrush = new SolidBrush(Color.FromArgb(108, 190, 230)))
            using (Brush amberBrush = new SolidBrush(Color.FromArgb(255, 188, 72)))
            {
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                graphics.Clear(Color.Transparent);

                if (commandName.IndexOf("OUTLET", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    graphics.DrawEllipse(cyanPen, 4, 7, 10, 10);
                    graphics.DrawLine(cyanPen, 9, 17, 9, 21);
                    graphics.DrawLine(cyanPen, 4, 21, 14, 21);
                    graphics.FillEllipse(cyanBrush, 8, 11, 3, 3);
                }
                else if (commandName.IndexOf("JB", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    graphics.DrawRectangle(amberPen, 4, 5, 12, 12);
                    graphics.DrawLine(amberPen, 7, 9, 14, 9);
                    graphics.DrawLine(amberPen, 7, 13, 14, 13);
                }
                else if (commandName.IndexOf("TRAY", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    graphics.DrawLine(cyanPen, 3, 18, 10, 18);
                    graphics.DrawLine(cyanPen, 10, 18, 10, 7);
                    graphics.DrawLine(cyanPen, 10, 7, 20, 7);
                    graphics.DrawLine(amberPen, 4, 21, 20, 21);
                }
                else if (commandName.IndexOf("EXPORT", StringComparison.OrdinalIgnoreCase) >= 0 || commandName.IndexOf("CHECK", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    graphics.DrawRectangle(whitePen, 5, 3, 14, 17);
                    graphics.DrawLine(cyanPen, 8, 8, 17, 8);
                    graphics.DrawLine(cyanPen, 8, 12, 17, 12);
                    graphics.DrawLine(cyanPen, 8, 16, 14, 16);
                }
                else
                {
                    graphics.DrawRectangle(whitePen, 4, 5, 16, 14);
                    graphics.DrawLine(cyanPen, 7, 10, 17, 10);
                    graphics.DrawLine(cyanPen, 7, 14, 17, 14);
                }

                if (commandName.IndexOf("DELETE", StringComparison.OrdinalIgnoreCase) >= 0 || commandName.IndexOf("CLEAR", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    graphics.DrawLine(redPen, 16, 5, 22, 11);
                    graphics.DrawLine(redPen, 22, 5, 16, 11);
                }
                else if (commandName.IndexOf("EDIT", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    graphics.DrawLine(amberPen, 15, 19, 22, 12);
                    graphics.FillEllipse(amberBrush, 20, 10, 4, 4);
                }
                else if (commandName.IndexOf("MARK", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    graphics.FillEllipse(amberBrush, 17, 5, 5, 5);
                }
            }

            return bitmap;
        }

        private void RunCommandButtonClick(object sender, EventArgs e)
        {
            Button button = sender as Button;

            if (button == null)
            {
                return;
            }

            string commandName = button.Tag as string;

            if (string.IsNullOrWhiteSpace(commandName))
            {
                return;
            }

            Document document = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;

            if (document == null)
            {
                statusTextBox.Text = "找不到目前開啟的 AutoCAD 圖面。";
                return;
            }

            statusTextBox.Text = "已執行：" + button.Text;
            document.SendStringToExecute(commandName + " ", true, false, false);
        }
    }
}
