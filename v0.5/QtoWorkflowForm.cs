using System;
using System.Drawing;
using System.Windows.Forms;
using Autodesk.AutoCAD.ApplicationServices;

namespace QtoWirePlugin
{
    public class QtoWorkflowForm : Form
    {
        private readonly TextBox statusTextBox;

        public QtoWorkflowForm()
        {
            Text = "QTO 弱電配線操作面板";
            StartPosition = FormStartPosition.CenterScreen;
            Width = 1040;
            Height = 640;
            MinimumSize = new Size(980, 600);

            Label titleLabel = new Label();
            titleLabel.Text = "QTO 弱電配線操作面板";
            titleLabel.Font = new Font(Font.FontFamily, 16.0f, FontStyle.Bold);
            titleLabel.AutoSize = true;
            titleLabel.Location = new Point(18, 18);
            Controls.Add(titleLabel);

            Label hintLabel = new Label();
            hintLabel.Text = "建議流程：標記出線口與箱體 → 編輯屬性 → 指定線槽 → 批次尋路 → 匯出線段明細。";
            hintLabel.AutoSize = true;
            hintLabel.Location = new Point(20, 56);
            Controls.Add(hintLabel);

            Panel diagramPanel = CreateDiagramPanel(700, 18, 300, 184);
            Controls.Add(diagramPanel);

            GroupBox markGroup = CreateGroup("標記與屬性", 18, 88, 650, 126);
            Controls.Add(markGroup);
            markGroup.Controls.Add(CreateButton("標記出線口", 18, 30, 132, 36, "QTO_MARK_OUTLETS"));
            markGroup.Controls.Add(CreateButton("標記箱體", 162, 30, 132, 36, "QTO_MARK_JB"));
            markGroup.Controls.Add(CreateButton("編輯出線口", 306, 30, 148, 36, "QTO_EDIT_OUTLET_PROPERTIES"));
            markGroup.Controls.Add(CreateButton("編輯箱體", 466, 30, 132, 36, "QTO_EDIT_JB_PROPERTIES"));
            markGroup.Controls.Add(CreateButton("刪除出線口", 18, 76, 132, 36, "QTO_DELETE_OUTLET_INFO"));
            markGroup.Controls.Add(CreateButton("刪除箱體", 162, 76, 132, 36, "QTO_DELETE_JB_INFO"));
            markGroup.Controls.Add(CreateButton("指向箱體", 306, 76, 148, 36, "QTO_CALLOUT_TO_JB"));
            markGroup.Controls.Add(CreateButton("編號標註", 466, 76, 132, 36, "QTO_LABEL_OUTLETS"));

            GroupBox routeGroup = CreateGroup("配線", 18, 228, 650, 94);
            Controls.Add(routeGroup);
            routeGroup.Controls.Add(CreateButton("一鍵連接", 18, 34, 132, 36, "QTO_CREATE_CONNECTIONS"));

            GroupBox trayGroup = CreateGroup("線槽", 18, 336, 650, 94);
            Controls.Add(trayGroup);
            trayGroup.Controls.Add(CreateButton("標記線槽", 18, 34, 132, 36, "QTO_MARK_TRAY"));
            trayGroup.Controls.Add(CreateButton("批次尋路", 162, 34, 132, 36, "QTO_BATCH_ROUTE_BY_TRAY"));
            trayGroup.Controls.Add(CreateButton("清除線槽屬性", 306, 34, 160, 36, "QTO_CLEAR_TRAY_PROPERTIES"));
            trayGroup.Controls.Add(CreateButton("管段到線槽", 478, 34, 132, 36, "QTO_CONDUIT_TO_TRAY"));

            GroupBox reportGroup = CreateGroup("報表", 18, 444, 982, 94);
            Controls.Add(reportGroup);
            reportGroup.Controls.Add(CreateButton("出線口清單", 18, 34, 132, 36, "QTO_EXPORT_OUTLETS"));
            reportGroup.Controls.Add(CreateButton("配線關係明細", 162, 34, 160, 36, "QTO_CHECK_WIRE"));
            reportGroup.Controls.Add(CreateButton("線段明細", 334, 34, 132, 36, "QTO_EXPORT_JB_SUMMARY"));
            reportGroup.Controls.Add(CreateButton("管段明細", 478, 34, 132, 36, "QTO_EXPORT_CONDUIT_SUMMARY"));

            statusTextBox = new TextBox();
            statusTextBox.Location = new Point(18, 558);
            statusTextBox.Width = 982;
            statusTextBox.Height = 32;
            statusTextBox.ReadOnly = true;
            statusTextBox.Text = "就緒。";
            Controls.Add(statusTextBox);
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
