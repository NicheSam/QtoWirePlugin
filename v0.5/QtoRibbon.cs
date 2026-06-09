using System;
using System.Collections.Generic;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Windows;

namespace QtoWirePlugin
{
    public class QtoPluginApplication : IExtensionApplication
    {
        public void Initialize()
        {
            QtoRibbon.CreateRibbonWhenReady();
        }

        public void Terminate()
        {
        }
    }

    public static class QtoRibbon
    {
        private const string TabId = "QTO_WIRE_PLUGIN_TAB";
        private static System.Windows.Forms.Timer ribbonCreateTimer;
        private static int ribbonCreateAttempts;

        public static void CreateRibbon()
        {
            if (!TryCreateRibbon())
            {
                WriteStatusMessage("\nQtoWirePlugin V0.5 ribbon is not ready. It will be created automatically when AutoCAD finishes loading.");
                CreateRibbonWhenReady();
            }
        }

        public static void CreateRibbonWhenReady()
        {
            if (TryCreateRibbon())
            {
                StopRibbonCreateTimer();
                return;
            }

            if (ribbonCreateTimer != null)
            {
                return;
            }

            ribbonCreateAttempts = 0;
            ribbonCreateTimer = new System.Windows.Forms.Timer();
            ribbonCreateTimer.Interval = 500;
            ribbonCreateTimer.Tick += RibbonCreateTimerTick;
            ribbonCreateTimer.Start();
        }

        private static void RibbonCreateTimerTick(object sender, EventArgs e)
        {
            ribbonCreateAttempts++;

            if (TryCreateRibbon())
            {
                StopRibbonCreateTimer();
                return;
            }

            if (ribbonCreateAttempts >= 60)
            {
                StopRibbonCreateTimer();
                WriteStatusMessage("\nQtoWirePlugin V0.5 could not create the ribbon. Run QTO_SHOW_UI after AutoCAD finishes loading.");
            }
        }

        private static void StopRibbonCreateTimer()
        {
            if (ribbonCreateTimer == null)
            {
                return;
            }

            ribbonCreateTimer.Stop();
            ribbonCreateTimer.Dispose();
            ribbonCreateTimer = null;
        }

        private static bool TryCreateRibbon()
        {
            RibbonControl ribbon = ComponentManager.Ribbon;

            if (ribbon == null)
            {
                return false;
            }

            RibbonTab tab = FindTab(ribbon, TabId);

            if (tab == null)
            {
                tab = new RibbonTab();
                tab.Id = TabId;
                tab.Title = "QTO \u5f31\u96fb\u8a08\u7b97";
                ribbon.Tabs.Add(tab);
            }

            List<RibbonPanel> panels = new List<RibbonPanel>();
            panels.Add(CreateWorkflowPanel());
            panels.Add(CreateOutletPanel());
            panels.Add(CreateJunctionBoxPanel());
            panels.Add(CreateTrayPanel());
            panels.Add(CreateReportPanel());

            tab.Panels.Clear();

            foreach (RibbonPanel panel in panels)
            {
                tab.Panels.Add(panel);
            }

            tab.IsActive = true;
            return true;
        }

        private static RibbonTab FindTab(RibbonControl ribbon, string tabId)
        {
            foreach (RibbonTab tab in ribbon.Tabs)
            {
                if (string.Equals(tab.Id, tabId, StringComparison.OrdinalIgnoreCase))
                {
                    return tab;
                }
            }

            return null;
        }

        private static void WriteStatusMessage(string message)
        {
            Document document = Application.DocumentManager.MdiActiveDocument;
            if (document != null)
            {
                document.Editor.WriteMessage(message);
            }
        }

        private static RibbonPanel CreateWorkflowPanel()
        {
            RibbonPanelSource source = new RibbonPanelSource();
            source.Title = "\u6d41\u7a0b";

            source.Items.Add(CreateButton("\u64cd\u4f5c\n\u9762\u677f", "QTO_PANEL", RibbonItemSize.Large));
            source.Items.Add(CreateButton("\u4e00\u9375\n\u9023\u63a5", "QTO_CREATE_CONNECTIONS", RibbonItemSize.Large));

            RibbonPanel panel = new RibbonPanel();
            panel.Source = source;
            return panel;
        }

        private static RibbonPanel CreateOutletPanel()
        {
            RibbonPanelSource source = new RibbonPanelSource();
            source.Title = "\u51fa\u7dda\u53e3";

            source.Items.Add(CreateButton("\u6a19\u8a18\n\u51fa\u7dda\u53e3", "QTO_MARK_OUTLETS", RibbonItemSize.Large));
            source.Items.Add(CreateButton("\u7de8\u865f\n\u6a19\u8a3b", "QTO_LABEL_OUTLETS", RibbonItemSize.Large));
            source.Items.Add(CreateButton("\u6307\u5411\n\u7bb1\u9ad4", "QTO_CALLOUT_TO_JB", RibbonItemSize.Large));
            source.Items.Add(CreateButton("\u7de8\u8f2f\n\u51fa\u7dda\u53e3", "QTO_EDIT_OUTLET_PROPERTIES", RibbonItemSize.Large));
            source.Items.Add(CreateButton("\u522a\u9664\n\u51fa\u7dda\u53e3", "QTO_DELETE_OUTLET_INFO", RibbonItemSize.Large));

            RibbonPanel panel = new RibbonPanel();
            panel.Source = source;
            return panel;
        }

        private static RibbonPanel CreateJunctionBoxPanel()
        {
            RibbonPanelSource source = new RibbonPanelSource();
            source.Title = "\u7bb1\u9ad4";

            source.Items.Add(CreateButton("\u6a19\u8a18\n\u7bb1\u9ad4", "QTO_MARK_JB", RibbonItemSize.Large));
            source.Items.Add(CreateButton("\u7de8\u8f2f\n\u7bb1\u9ad4", "QTO_EDIT_JB_PROPERTIES", RibbonItemSize.Large));
            source.Items.Add(CreateButton("\u522a\u9664\n\u7bb1\u9ad4", "QTO_DELETE_JB_INFO", RibbonItemSize.Large));

            RibbonPanel panel = new RibbonPanel();
            panel.Source = source;
            return panel;
        }

        private static RibbonPanel CreateTrayPanel()
        {
            RibbonPanelSource source = new RibbonPanelSource();
            source.Title = "\u7dda\u69fd";

            source.Items.Add(CreateButton("\u6a19\u8a18\n\u7dda\u69fd", "QTO_MARK_TRAY", RibbonItemSize.Large));
            source.Items.Add(CreateButton("\u6279\u6b21\n\u5c0b\u8def", "QTO_BATCH_ROUTE_BY_TRAY", RibbonItemSize.Large));
            source.Items.Add(CreateButton("\u7ba1\u6bb5\u5230\n\u7dda\u69fd", "QTO_CONDUIT_TO_TRAY", RibbonItemSize.Large));
            source.Items.Add(CreateButton("\u6e05\u9664\u7dda\u69fd\n\u5c6c\u6027", "QTO_CLEAR_TRAY_PROPERTIES", RibbonItemSize.Large));

            RibbonPanel panel = new RibbonPanel();
            panel.Source = source;
            return panel;
        }

        private static RibbonPanel CreateReportPanel()
        {
            RibbonPanelSource source = new RibbonPanelSource();
            source.Title = "\u5831\u8868";

            source.Items.Add(CreateButton("\u51fa\u7dda\u53e3\n\u6e05\u55ae", "QTO_EXPORT_OUTLETS", RibbonItemSize.Large));
            source.Items.Add(CreateButton("\u914d\u7dda\u95dc\u4fc2\n\u660e\u7d30", "QTO_CHECK_WIRE", RibbonItemSize.Large));
            source.Items.Add(CreateButton("\u7dda\u6bb5\n\u660e\u7d30", "QTO_EXPORT_JB_SUMMARY", RibbonItemSize.Large));
            source.Items.Add(CreateButton("\u7ba1\u6bb5\n\u660e\u7d30", "QTO_EXPORT_CONDUIT_SUMMARY", RibbonItemSize.Large));

            RibbonPanel panel = new RibbonPanel();
            panel.Source = source;
            return panel;
        }

        private static RibbonButton CreateButton(string text, string commandName, RibbonItemSize size)
        {
            RibbonButton button = new RibbonButton();
            button.Text = text;
            button.ShowText = true;
            button.Size = size;
            button.Orientation = System.Windows.Controls.Orientation.Vertical;
            TrySetButtonImages(button, commandName);
            button.ToolTip = GetCommandDescription(commandName);
            button.CommandHandler = new RibbonCommandHandler(commandName);
            return button;
        }

        private static void TrySetButtonImages(RibbonButton button, string commandName)
        {
            try
            {
                button.Image = CreateRibbonIcon(commandName, 16);
                button.LargeImage = CreateRibbonIcon(commandName, 32);
                button.ShowImage = true;
            }
            catch
            {
                button.ShowImage = false;
            }
        }

        private static ImageSource CreateRibbonIcon(string commandName, int size)
        {
            DrawingVisual visual = new DrawingVisual();

            using (DrawingContext context = visual.RenderOpen())
            {
                DrawCommandIcon(context, commandName, size);
            }

            RenderTargetBitmap bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            bitmap.Freeze();
            return bitmap;
        }

        private static void DrawCommandIcon(DrawingContext context, string commandName, int size)
        {
            double scale = size / 32.0;
            Pen cyanPen = new Pen(new SolidColorBrush(Color.FromRgb(108, 190, 230)), 2.0 * scale);
            Pen whitePen = new Pen(new SolidColorBrush(Color.FromRgb(236, 242, 248)), 1.6 * scale);
            Pen amberPen = new Pen(new SolidColorBrush(Color.FromRgb(255, 188, 72)), 2.0 * scale);
            Pen redPen = new Pen(new SolidColorBrush(Color.FromRgb(232, 82, 82)), 2.4 * scale);
            Brush cyanBrush = new SolidColorBrush(Color.FromRgb(108, 190, 230));
            Brush amberBrush = new SolidColorBrush(Color.FromRgb(255, 188, 72));
            Brush redBrush = new SolidColorBrush(Color.FromRgb(232, 82, 82));

            context.DrawRectangle(new SolidColorBrush(Color.FromArgb(0, 0, 0, 0)), null, new System.Windows.Rect(0, 0, size, size));

            if (commandName.IndexOf("OUTLET", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                DrawOutletSymbol(context, cyanPen, cyanBrush, scale);
            }
            else if (commandName.IndexOf("JB", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                DrawBoxSymbol(context, amberPen, amberBrush, scale);
            }
            else if (commandName.IndexOf("TRAY", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                DrawTraySymbol(context, cyanPen, amberPen, scale);
            }
            else if (commandName.IndexOf("CHECK", StringComparison.OrdinalIgnoreCase) >= 0 || commandName.IndexOf("EXPORT", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                DrawReportSymbol(context, whitePen, cyanPen, scale);
            }
            else if (commandName.IndexOf("CONNECTION", StringComparison.OrdinalIgnoreCase) >= 0 || commandName.IndexOf("ROUTE", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                DrawRouteSymbol(context, cyanPen, amberPen, scale);
            }
            else
            {
                DrawPanelSymbol(context, whitePen, cyanPen, scale);
            }

            if (commandName.IndexOf("DELETE", StringComparison.OrdinalIgnoreCase) >= 0 || commandName.IndexOf("CLEAR", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                context.DrawLine(redPen, Point(22, 8, scale), Point(29, 15, scale));
                context.DrawLine(redPen, Point(29, 8, scale), Point(22, 15, scale));
            }
            else if (commandName.IndexOf("EDIT", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                context.DrawLine(amberPen, Point(20, 24, scale), Point(28, 16, scale));
                context.DrawEllipse(amberBrush, null, Point(28, 16, scale), 2.0 * scale, 2.0 * scale);
            }
            else if (commandName.IndexOf("MARK", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                context.DrawEllipse(amberBrush, null, Point(24, 9, scale), 3.0 * scale, 3.0 * scale);
            }

        }

        private static void DrawOutletSymbol(DrawingContext context, Pen pen, Brush brush, double scale)
        {
            context.DrawEllipse(null, pen, Point(12, 15, scale), 6 * scale, 6 * scale);
            context.DrawLine(pen, Point(12, 21, scale), Point(12, 27, scale));
            context.DrawLine(pen, Point(6, 27, scale), Point(18, 27, scale));
            context.DrawEllipse(brush, null, Point(12, 15, scale), 2 * scale, 2 * scale);
        }

        private static void DrawBoxSymbol(DrawingContext context, Pen pen, Brush brush, double scale)
        {
            context.DrawRectangle(null, pen, Rect(7, 8, 15, 15, scale));
            context.DrawLine(pen, Point(10, 12, scale), Point(19, 12, scale));
            context.DrawLine(pen, Point(10, 17, scale), Point(19, 17, scale));
            context.DrawEllipse(brush, null, Point(22, 23, scale), 3 * scale, 3 * scale);
        }

        private static void DrawTraySymbol(DrawingContext context, Pen trayPen, Pen amberPen, double scale)
        {
            context.DrawLine(trayPen, Point(5, 23, scale), Point(14, 23, scale));
            context.DrawLine(trayPen, Point(14, 23, scale), Point(14, 10, scale));
            context.DrawLine(trayPen, Point(14, 10, scale), Point(26, 10, scale));
            context.DrawLine(amberPen, Point(7, 27, scale), Point(25, 27, scale));
        }

        private static void DrawRouteSymbol(DrawingContext context, Pen wirePen, Pen trayPen, double scale)
        {
            context.DrawLine(trayPen, Point(6, 23, scale), Point(23, 23, scale));
            context.DrawLine(trayPen, Point(23, 23, scale), Point(23, 10, scale));
            context.DrawLine(wirePen, Point(7, 8, scale), Point(7, 16, scale));
            context.DrawLine(wirePen, Point(7, 16, scale), Point(15, 16, scale));
            context.DrawLine(wirePen, Point(15, 16, scale), Point(15, 23, scale));
            context.DrawEllipse(null, wirePen, Point(7, 8, scale), 4 * scale, 4 * scale);
        }

        private static void DrawReportSymbol(DrawingContext context, Pen pagePen, Pen linePen, double scale)
        {
            context.DrawRectangle(null, pagePen, Rect(8, 6, 16, 20, scale));
            context.DrawLine(linePen, Point(11, 11, scale), Point(21, 11, scale));
            context.DrawLine(linePen, Point(11, 16, scale), Point(21, 16, scale));
            context.DrawLine(linePen, Point(11, 21, scale), Point(18, 21, scale));
        }

        private static void DrawPanelSymbol(DrawingContext context, Pen pagePen, Pen accentPen, double scale)
        {
            context.DrawRectangle(null, pagePen, Rect(6, 7, 20, 18, scale));
            context.DrawLine(accentPen, Point(10, 12, scale), Point(22, 12, scale));
            context.DrawLine(accentPen, Point(10, 17, scale), Point(22, 17, scale));
            context.DrawLine(accentPen, Point(10, 22, scale), Point(17, 22, scale));
        }

        private static System.Windows.Point Point(double x, double y, double scale)
        {
            return new System.Windows.Point(x * scale, y * scale);
        }

        private static System.Windows.Rect Rect(double x, double y, double width, double height, double scale)
        {
            return new System.Windows.Rect(x * scale, y * scale, width * scale, height * scale);
        }

        private static string GetCommandDescription(string commandName)
        {
            switch (commandName)
            {
                case "QTO_PANEL":
                    return "\u958b\u555f\u4e2d\u6587\u64cd\u4f5c\u9762\u677f\uff0c\u9069\u5408\u6279\u6b21\u6216\u591a\u6b65\u9a5f\u6d41\u7a0b\u3002";
                case "QTO_DELETE_OUTLET_INFO":
                    return "\u6e05\u9664\u9078\u53d6\u5716\u584a\u7684\u51fa\u7dda\u53e3 QTO \u8cc7\u6599\u3002";
                case "QTO_DELETE_JB_INFO":
                    return "\u6e05\u9664\u9078\u53d6\u5716\u584a\u7684\u7bb1\u9ad4 QTO \u8cc7\u6599\u3002";
                case "QTO_EDIT_OUTLET_PROPERTIES":
                    return "\u6846\u9078\u5df2\u6a19\u8a18\u51fa\u7dda\u53e3\uff0c\u6279\u6b21\u7de8\u8f2f OUTLET_ID\u3001JB_ID\u3001SYSTEM \u8207 CABLE_TYPE\u3002";
                case "QTO_EDIT_JB_PROPERTIES":
                    return "\u6846\u9078\u5df2\u6a19\u8a18\u7bb1\u9ad4\uff0c\u6279\u6b21\u7de8\u8f2f JB_ID \u8207 SYSTEM\u3002";
                case "QTO_CALLOUT_TO_JB":
                    return "\u6846\u9078\u51fa\u7dda\u53e3\u6216\u5716\u584a\uff0c\u6307\u5b9a\u7bb1\u9ad4\u5f8c\u7522\u751f\u6307\u5411\u7bb1\u9ad4\u7684\u77ed\u7bad\u982d\u8207 to\u7bb1\u9ad4\u540d\u7a31\u6a19\u8a3b\u3002";
                case "QTO_LABEL_OUTLETS":
                    return "\u6846\u9078\u51fa\u7dda\u53e3\u5716\u584a\uff0c\u5728\u5716\u584a\u4e0a\u65b9\u6279\u6b21\u7522\u751f OUTLET_ID \u7de8\u865f\u6a19\u8a3b\u3002";
                case "QTO_EXPORT_OUTLETS":
                    return "\u532f\u51fa\u6240\u6709\u51fa\u7dda\u53e3\u8cc7\u6599\uff0c\u7528\u4f86\u6aa2\u67e5\u7de8\u865f\u8207\u7bb1\u9ad4\u5c0d\u61c9\u3002";
                case "QTO_CHECK_WIRE":
                    return "\u53ea\u6aa2\u67e5\u8cc7\u6599\u95dc\u4fc2\u4e26\u8f38\u51fa CSV\uff0c\u4e0d\u6539\u8b8a\u5716\u9762\u7269\u4ef6\u3002";
                case "QTO_HIGHLIGHT_ERRORS":
                    return "\u628a\u6aa2\u67e5\u5230\u7684\u932f\u8aa4\u7269\u4ef6\u79fb\u5230 QTO_ERROR \u5716\u5c64\uff0c\u65b9\u4fbf\u5728\u5716\u9762\u627e\u5230\u554f\u984c\u3002";
                case "QTO_CLEAR_ERROR_HIGHLIGHT":
                    return "\u6e05\u9664\u932f\u8aa4\u4e0a\u8272\u8207 QTO_ERROR \u6a19\u8a18\u3002";
                case "QTO_RECALC_WIRE":
                    return "\u91cd\u65b0\u8a08\u7b97\u5df2\u7d81\u5b9a Polyline \u9577\u5ea6\uff0c\u9069\u5408\u8abf\u6574\u7dda\u8def\u5f8c\u57f7\u884c\u3002";
                case "QTO_EXPORT_JB_SUMMARY":
                    return "\u8f38\u51fa\u7dda\u6bb5\u660e\u7d30\uff0c\u5305\u542b\u6bcf\u500b\u51fa\u7dda\u53e3\u9577\u5ea6\u660e\u7d30\u8207\u7bb1\u9ad4\u5c0f\u8a08\u3002";
                case "QTO_EXPORT_CONDUIT_SUMMARY":
                    return "\u8f38\u51fa QTO_CONDUIT \u7ba1\u6bb5\u660e\u7d30\uff0c\u4e26\u91cd\u65b0\u8a08\u7b97\u6bcf\u689d\u7ba1\u6bb5\u9577\u5ea6\u3002";
                case "QTO_MARK_TRAY":
                    return "\u5c07 Polyline \u6a19\u8a18\u70ba\u7dda\u69fd\uff0c\u5f8c\u7e8c\u53ef\u4f9d\u7dda\u69fd\u7db2\u8def\u5c0b\u8def\u3002";
                case "QTO_SCAN_TRAY_NETWORK":
                    return "\u6383\u63cf\u7dda\u69fd Polyline\uff0c\u6aa2\u67e5\u7dda\u69fd\u7aef\u9ede\u9023\u63a5\u72c0\u614b\u3002";
                case "QTO_ROUTE_BY_TRAY":
                    return "\u4f9d\u6307\u5b9a\u7dda\u69fd\u7db2\u8def\uff0c\u7522\u751f\u51fa\u7dda\u53e3\u5230\u7d50\u7dda\u7bb1\u7684\u914d\u7dda\u3002";
                case "QTO_BATCH_ROUTE_BY_TRAY":
                    return "\u5c0d\u591a\u500b\u51fa\u7dda\u53e3\u6279\u6b21\u4f9d\u7dda\u69fd\u5c0b\u8def\u3002";
                case "QTO_CONDUIT_TO_TRAY":
                    return "\u6846\u9078\u7dda\u69fd\u8207\u51fa\u7dda\u53e3\u5716\u584a\uff0c\u6279\u6b21\u5efa\u7acb\u5230\u6700\u8fd1\u7dda\u69fd\u7684\u6b63\u4ea4\u7ba1\u6bb5\u4e26\u7d71\u8a08\u9577\u5ea6\u3002";
                case "QTO_CLEAR_TRAY_PROPERTIES":
                    return "\u6e05\u9664\u9078\u53d6 Polyline \u4e0a\u7684\u7dda\u69fd QTO \u5c6c\u6027\u3002";
                default:
                    return commandName;
            }
        }
    }

    public class RibbonCommandHandler : ICommand
    {
        private readonly string commandName;

        public RibbonCommandHandler(string commandName)
        {
            this.commandName = commandName;
        }

        public event EventHandler CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object parameter)
        {
            return true;
        }

        public void Execute(object parameter)
        {
            Document document = Application.DocumentManager.MdiActiveDocument;

            if (document == null)
            {
                return;
            }

            document.SendStringToExecute(commandName + " ", true, false, false);
        }
    }
}
