using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace QtoWirePlugin
{
    public static class QtoBlockPreviewRenderer
    {
        private const int Padding = 18;

        public static Bitmap RenderBlockPreview(string sourceDwgPath, string blockName, int width, int height)
        {
            Bitmap bitmap = new Bitmap(Math.Max(width, 120), Math.Max(height, 120));

            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.Clear(Color.FromArgb(45, 55, 66));

                if (string.IsNullOrWhiteSpace(blockName))
                {
                    DrawCenteredText(graphics, bitmap.Size, "未選取圖塊");
                    return bitmap;
                }

                if (string.IsNullOrWhiteSpace(sourceDwgPath) || !File.Exists(sourceDwgPath))
                {
                    DrawCenteredText(graphics, bitmap.Size, "找不到圖例 DWG");
                    return bitmap;
                }

                try
                {
                    DrawBlock(graphics, bitmap.Size, sourceDwgPath, blockName);
                }
                catch
                {
                    DrawCenteredText(graphics, bitmap.Size, "無法產生預覽");
                }
            }

            return bitmap;
        }

        public static Bitmap RenderCurrentBlockPreview(Database database, string blockName, int width, int height)
        {
            Bitmap bitmap = new Bitmap(Math.Max(width, 120), Math.Max(height, 120));
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.Clear(Color.FromArgb(45, 55, 66));
                if (database == null || string.IsNullOrWhiteSpace(blockName))
                {
                    DrawCenteredText(graphics, bitmap.Size, "專案內找不到圖塊");
                    return bitmap;
                }
                try
                {
                    using (Transaction transaction = database.TransactionManager.StartOpenCloseTransaction())
                    {
                        BlockTable blockTable = (BlockTable)transaction.GetObject(database.BlockTableId, OpenMode.ForRead);
                        if (!blockTable.Has(blockName))
                        {
                            DrawCenteredText(graphics, bitmap.Size, "專案內找不到圖塊");
                            return bitmap;
                        }
                        BlockTableRecord blockRecord = (BlockTableRecord)transaction.GetObject(blockTable[blockName], OpenMode.ForRead);
                        Extents3d? extents = GetBlockExtents(transaction, blockRecord);
                        if (!extents.HasValue)
                        {
                            DrawCenteredText(graphics, bitmap.Size, "圖塊沒有可預覽幾何");
                            return bitmap;
                        }
                        PreviewTransform transform = new PreviewTransform(extents.Value, bitmap.Size);
                        foreach (ObjectId objectId in blockRecord)
                        {
                            Entity entity = transaction.GetObject(objectId, OpenMode.ForRead, false) as Entity;
                            if (entity != null) DrawEntity(graphics, transaction, entity, transform);
                        }
                    }
                }
                catch
                {
                    DrawCenteredText(graphics, bitmap.Size, "無法產生專案預覽");
                }
            }
            return bitmap;
        }

        private static void DrawBlock(Graphics graphics, Size canvasSize, string sourceDwgPath, string blockName)
        {
            using (Database database = new Database(false, true))
            {
                database.ReadDwgFile(sourceDwgPath, FileShare.ReadWrite, true, string.Empty);
                database.CloseInput(true);

                using (Transaction transaction = database.TransactionManager.StartTransaction())
                {
                    BlockTable blockTable = (BlockTable)transaction.GetObject(database.BlockTableId, OpenMode.ForRead);
                    if (!blockTable.Has(blockName))
                    {
                        DrawCenteredText(graphics, canvasSize, "圖例內找不到圖塊");
                        return;
                    }

                    BlockTableRecord blockRecord = (BlockTableRecord)transaction.GetObject(blockTable[blockName], OpenMode.ForRead);
                    Extents3d? extents = GetBlockExtents(transaction, blockRecord);
                    if (!extents.HasValue)
                    {
                        DrawCenteredText(graphics, canvasSize, "圖塊沒有可預覽幾何");
                        return;
                    }

                    PreviewTransform transform = new PreviewTransform(extents.Value, canvasSize);
                    foreach (ObjectId objectId in blockRecord)
                    {
                        Entity entity = transaction.GetObject(objectId, OpenMode.ForRead, false) as Entity;
                        if (entity == null)
                        {
                            continue;
                        }

                        DrawEntity(graphics, transaction, entity, transform);
                    }

                    transaction.Commit();
                }
            }
        }

        private static Extents3d? GetBlockExtents(Transaction transaction, BlockTableRecord blockRecord)
        {
            Extents3d? merged = null;

            foreach (ObjectId objectId in blockRecord)
            {
                Entity entity = transaction.GetObject(objectId, OpenMode.ForRead, false) as Entity;
                if (entity == null)
                {
                    continue;
                }

                try
                {
                    Extents3d entityExtents = entity.GeometricExtents;
                    if (!merged.HasValue)
                    {
                        merged = entityExtents;
                    }
                    else
                    {
                        Extents3d value = merged.Value;
                        value.AddExtents(entityExtents);
                        merged = value;
                    }
                }
                catch (Autodesk.AutoCAD.Runtime.Exception)
                {
                }
                catch (InvalidOperationException)
                {
                }
            }

            return merged;
        }

        private static void DrawEntity(Graphics graphics, Transaction transaction, Entity entity, PreviewTransform transform)
        {
            using (Pen pen = CreatePen(entity))
            using (Brush brush = new SolidBrush(pen.Color))
            {
                Line line = entity as Line;
                if (line != null)
                {
                    graphics.DrawLine(pen, transform.Map(line.StartPoint), transform.Map(line.EndPoint));
                    return;
                }

                Polyline polyline = entity as Polyline;
                if (polyline != null)
                {
                    DrawPolyline(graphics, pen, polyline, transform);
                    return;
                }

                Circle circle = entity as Circle;
                if (circle != null)
                {
                    RectangleF bounds = transform.MapCircle(circle.Center, circle.Radius);
                    graphics.DrawEllipse(pen, bounds);
                    return;
                }

                Arc arc = entity as Arc;
                if (arc != null)
                {
                    RectangleF bounds = transform.MapCircle(arc.Center, arc.Radius);
                    float startAngle = (float)(-arc.StartAngle * 180.0 / Math.PI);
                    float sweepAngle = (float)(-(NormalizeEndAngle(arc.StartAngle, arc.EndAngle) - arc.StartAngle) * 180.0 / Math.PI);
                    graphics.DrawArc(pen, bounds, startAngle, sweepAngle);
                    return;
                }

                DBText dbText = entity as DBText;
                if (dbText != null)
                {
                    DrawText(graphics, brush, dbText.TextString, transform.Map(dbText.Position));
                    return;
                }

                MText mText = entity as MText;
                if (mText != null)
                {
                    DrawText(graphics, brush, mText.Contents, transform.Map(mText.Location));
                    return;
                }

                AttributeDefinition attribute = entity as AttributeDefinition;
                if (attribute != null)
                {
                    DrawText(graphics, brush, attribute.TextString, transform.Map(attribute.Position));
                    return;
                }

                BlockReference blockReference = entity as BlockReference;
                if (blockReference != null)
                {
                    DrawFallbackExtents(graphics, pen, blockReference, transform);
                    return;
                }

                DrawFallbackExtents(graphics, pen, entity, transform);
            }
        }

        private static void DrawPolyline(Graphics graphics, Pen pen, Polyline polyline, PreviewTransform transform)
        {
            if (polyline.NumberOfVertices <= 0)
            {
                return;
            }

            for (int i = 0; i + 1 < polyline.NumberOfVertices; i++)
            {
                graphics.DrawLine(pen, transform.Map(polyline.GetPoint3dAt(i)), transform.Map(polyline.GetPoint3dAt(i + 1)));
            }

            if (polyline.Closed && polyline.NumberOfVertices > 1)
            {
                graphics.DrawLine(pen, transform.Map(polyline.GetPoint3dAt(polyline.NumberOfVertices - 1)), transform.Map(polyline.GetPoint3dAt(0)));
            }
        }

        private static void DrawText(Graphics graphics, Brush brush, string text, PointF location)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            string shortText = text.Length > 24 ? text.Substring(0, 24) : text;
            using (System.Drawing.Font font = new System.Drawing.Font(SystemFonts.MessageBoxFont.FontFamily, 8.0f, FontStyle.Regular))
            {
                graphics.DrawString(shortText, font, brush, location);
            }
        }

        private static void DrawFallbackExtents(Graphics graphics, Pen pen, Entity entity, PreviewTransform transform)
        {
            try
            {
                Extents3d extents = entity.GeometricExtents;
                RectangleF bounds = transform.MapExtents(extents);
                graphics.DrawRectangle(pen, bounds.X, bounds.Y, bounds.Width, bounds.Height);
            }
            catch (Autodesk.AutoCAD.Runtime.Exception)
            {
            }
            catch (InvalidOperationException)
            {
            }
        }

        private static Pen CreatePen(Entity entity)
        {
            Color color = Color.FromArgb(230, 236, 244);
            try
            {
                if (entity.Color != null && entity.Color.ColorMethod != Autodesk.AutoCAD.Colors.ColorMethod.ByLayer)
                {
                    color = entity.Color.ColorValue;
                }
            }
            catch
            {
            }

            Pen pen = new Pen(color, 1.6f);
            pen.LineJoin = LineJoin.Round;
            pen.StartCap = LineCap.Round;
            pen.EndCap = LineCap.Round;
            return pen;
        }

        private static double NormalizeEndAngle(double startAngle, double endAngle)
        {
            while (endAngle < startAngle)
            {
                endAngle += Math.PI * 2.0;
            }

            return endAngle;
        }

        private static void DrawCenteredText(Graphics graphics, Size canvasSize, string text)
        {
            using (Brush brush = new SolidBrush(Color.FromArgb(220, 226, 234)))
            using (System.Drawing.Font font = new System.Drawing.Font(SystemFonts.MessageBoxFont.FontFamily, 10.0f, FontStyle.Regular))
            {
                SizeF textSize = graphics.MeasureString(text, font);
                PointF location = new PointF((canvasSize.Width - textSize.Width) / 2.0f, (canvasSize.Height - textSize.Height) / 2.0f);
                graphics.DrawString(text, font, brush, location);
            }
        }

        private class PreviewTransform
        {
            private readonly Extents3d extents;
            private readonly float scale;
            private readonly float offsetX;
            private readonly float offsetY;

            public PreviewTransform(Extents3d extents, Size canvasSize)
            {
                this.extents = extents;
                double width = Math.Max(extents.MaxPoint.X - extents.MinPoint.X, 1.0);
                double height = Math.Max(extents.MaxPoint.Y - extents.MinPoint.Y, 1.0);
                float availableWidth = Math.Max(canvasSize.Width - Padding * 2, 1);
                float availableHeight = Math.Max(canvasSize.Height - Padding * 2, 1);
                scale = (float)Math.Min(availableWidth / width, availableHeight / height);
                offsetX = (float)((canvasSize.Width - width * scale) / 2.0);
                offsetY = (float)((canvasSize.Height - height * scale) / 2.0);
            }

            public PointF Map(Point3d point)
            {
                float x = offsetX + (float)((point.X - extents.MinPoint.X) * scale);
                float y = offsetY + (float)((extents.MaxPoint.Y - point.Y) * scale);
                return new PointF(x, y);
            }

            public RectangleF MapCircle(Point3d center, double radius)
            {
                PointF topLeft = Map(new Point3d(center.X - radius, center.Y + radius, center.Z));
                PointF bottomRight = Map(new Point3d(center.X + radius, center.Y - radius, center.Z));
                return RectangleF.FromLTRB(topLeft.X, topLeft.Y, bottomRight.X, bottomRight.Y);
            }

            public RectangleF MapExtents(Extents3d targetExtents)
            {
                PointF topLeft = Map(new Point3d(targetExtents.MinPoint.X, targetExtents.MaxPoint.Y, 0));
                PointF bottomRight = Map(new Point3d(targetExtents.MaxPoint.X, targetExtents.MinPoint.Y, 0));
                return RectangleF.FromLTRB(topLeft.X, topLeft.Y, bottomRight.X, bottomRight.Y);
            }
        }
    }
}
