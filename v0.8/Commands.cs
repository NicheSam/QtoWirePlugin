using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;

namespace QtoWirePlugin
{
    public partial class Commands
    {
        private const double OutletSortYTolerance = 0.0001;
        private const double TrayNodeMergeTolerance = 1.0;
        private const double TrayPointTolerance = 0.0001;
        private const double DefaultCalloutArrowLength = 800.0;
        private const double DefaultCalloutTextHeight = 250.0;
        private const double DefaultOutletLabelOffset = 500.0;
        private const double DefaultConduitMlineWidth = 5.0;
        private const string ConduitMlineStyleName = "QTO_CONDUIT_MLINE_W5_BLUE";
        private const string ErrorOutletLayerName = "QTO_ERROR_OUTLET";
        private const string ErrorWireLayerName = "QTO_ERROR_WIRE";
        private const string ErrorJbLayerName = "QTO_ERROR_JB";


        private static Editor GetEditor()
        {
            Document document = Application.DocumentManager.MdiActiveDocument;

            if (document == null)
            {
                return null;
            }

            return document.Editor;
        }

        private static Polyline CreateDoubleLPolyline(Point3d outletPosition, Point3d junctionBoxPosition)
        {
            double midY = (outletPosition.Y + junctionBoxPosition.Y) / 2.0;

            Polyline polyline = new Polyline();
            polyline.SetDatabaseDefaults();
            polyline.Elevation = outletPosition.Z;
            polyline.AddVertexAt(0, new Point2d(outletPosition.X, outletPosition.Y), 0.0, 0.0, 0.0);
            polyline.AddVertexAt(1, new Point2d(outletPosition.X, midY), 0.0, 0.0, 0.0);
            polyline.AddVertexAt(2, new Point2d(junctionBoxPosition.X, midY), 0.0, 0.0, 0.0);
            polyline.AddVertexAt(3, new Point2d(junctionBoxPosition.X, junctionBoxPosition.Y), 0.0, 0.0, 0.0);
            return polyline;
        }

        private static Polyline CreatePolylineFromPoints(List<Point3d> points)
        {
            Polyline polyline = new Polyline();
            polyline.SetDatabaseDefaults();

            if (points == null || points.Count == 0)
            {
                return polyline;
            }

            polyline.Elevation = points[0].Z;

            for (int i = 0; i < points.Count; i++)
            {
                polyline.AddVertexAt(i, new Point2d(points[i].X, points[i].Y), 0.0, 0.0, 0.0);
            }

            polyline.ColorIndex = 256;
            polyline.Linetype = "ByLayer";
            polyline.LineWeight = LineWeight.ByLayer;
            return polyline;
        }

        private static Mline CreateMlineFromPoints(List<Point3d> points, Database database, Transaction transaction, Editor editor)
        {
            Mline mline = new Mline();
            mline.SetDatabaseDefaults();
            mline.Justification = MlineJustification.Zero;
            mline.Scale = 1.0;
            mline.Normal = Vector3d.ZAxis;
            mline.ColorIndex = 256;
            mline.Linetype = "ByLayer";
            mline.LineWeight = LineWeight.ByLayer;

            ObjectId styleId = GetMlineStyleId(database, transaction);

            if (!styleId.IsNull && styleId.IsValid)
            {
                mline.Style = styleId;
                mline.SetFromStyle();
            }

            mline.Justification = MlineJustification.Zero;
            mline.Scale = 1.0;
            mline.Normal = Vector3d.ZAxis;
            mline.ColorIndex = 256;
            mline.Linetype = "ByLayer";
            mline.LineWeight = LineWeight.ByLayer;

            if (points == null)
            {
                return mline;
            }

            foreach (Point3d point in points)
            {
                mline.AppendSegment(ToCurrentUcsPoint(point, editor));
            }

            return mline;
        }

        private static ConduitPolylinePair CreateConduitPolylinePair(List<Point3d> points)
        {
            Polyline centerline = CreatePolylineFromPoints(points);

            if (centerline.NumberOfVertices < 2)
            {
                centerline.Dispose();
                return null;
            }

            double halfWidth = DefaultConduitMlineWidth / 2.0;
            Polyline primary = GetSingleOffsetPolyline(centerline, halfWidth);
            Polyline secondary = GetSingleOffsetPolyline(centerline, -halfWidth);
            centerline.Dispose();

            if (primary == null || secondary == null)
            {
                if (primary != null)
                {
                    primary.Dispose();
                }

                if (secondary != null)
                {
                    secondary.Dispose();
                }

                return null;
            }

            ConduitPolylinePair pair = new ConduitPolylinePair();
            pair.Primary = primary;
            pair.Secondary = secondary;
            return pair;
        }

        private static Polyline GetSingleOffsetPolyline(Polyline centerline, double offsetDistance)
        {
            DBObjectCollection offsetObjects;

            try
            {
                offsetObjects = centerline.GetOffsetCurves(offsetDistance);
            }
            catch
            {
                return null;
            }

            Polyline result = null;

            foreach (DBObject offsetObject in offsetObjects)
            {
                Polyline polyline = offsetObject as Polyline;

                if (polyline != null && result == null)
                {
                    result = polyline;
                    result.SetDatabaseDefaults();
                    result.ColorIndex = 256;
                    result.Linetype = "ByLayer";
                    result.LineWeight = LineWeight.ByLayer;
                    continue;
                }

                offsetObject.Dispose();
            }

            return result;
        }

        private static double GetPointPathLength(List<Point3d> points)
        {
            if (points == null || points.Count < 2)
            {
                return 0.0;
            }

            double length = 0.0;

            for (int i = 1; i < points.Count; i++)
            {
                length += points[i - 1].DistanceTo(points[i]);
            }

            return length;
        }

        private static Point3d ToCurrentUcsPoint(Point3d worldPoint, Editor editor)
        {
            if (editor == null)
            {
                return worldPoint;
            }

            return worldPoint.TransformBy(editor.CurrentUserCoordinateSystem.Inverse());
        }

        private static ObjectId GetMlineStyleId(Database database, Transaction transaction)
        {
            if (database == null)
            {
                return ObjectId.Null;
            }

            if (transaction == null || database.MLStyleDictionaryId.IsNull || !database.MLStyleDictionaryId.IsValid)
            {
                return database.CmlstyleID;
            }

            DBDictionary styleDictionary = transaction.GetObject(database.MLStyleDictionaryId, OpenMode.ForRead, false) as DBDictionary;

            if (styleDictionary == null)
            {
                return database.CmlstyleID;
            }

            if (styleDictionary.Contains(ConduitMlineStyleName))
            {
                return styleDictionary.GetAt(ConduitMlineStyleName);
            }

            styleDictionary.UpgradeOpen();

            MlineStyle style = new MlineStyle();
            style.Name = ConduitMlineStyleName;
            style.Description = "QTO conduit centerline style, width 5.";
            ConfigureConduitMlineStyle(style, database);

            ObjectId styleId = styleDictionary.SetAt(ConduitMlineStyleName, style);
            transaction.AddNewlyCreatedDBObject(style, true);

            return styleId;
        }

        private static void ConfigureConduitMlineStyle(MlineStyle style, Database database)
        {
            if (style == null)
            {
                return;
            }

            while (style.Elements.Count > 0)
            {
                style.Elements.RemoveAt(0);
            }

            ObjectId linetypeId = database != null && !database.ContinuousLinetype.IsNull ? database.ContinuousLinetype : ObjectId.Null;
            Autodesk.AutoCAD.Colors.Color color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByAci, 5);
            double halfWidth = DefaultConduitMlineWidth / 2.0;

            style.Elements.Add(new MlineStyleElement(-halfWidth, color, linetypeId), true);
            style.Elements.Add(new MlineStyleElement(halfWidth, color, linetypeId), true);
            style.ShowMiters = true;
            style.StartSquareCap = false;
            style.EndSquareCap = false;
            style.StartRoundCap = false;
            style.EndRoundCap = false;
            style.Filled = false;
        }

        private static double GetConduitLength(Entity conduit)
        {
            Polyline polyline = conduit as Polyline;

            if (polyline != null)
            {
                return QtoGeometryHelper.GetPolylineLength(polyline);
            }

            Mline mline = conduit as Mline;

            if (mline != null)
            {
                return QtoGeometryHelper.GetMlineLength(mline);
            }

            return 0.0;
        }

        private static double GetConduitLengthM(Entity conduit, double measuredLengthMm, string lengthSource)
        {
            if (string.Equals(lengthSource, "PARALLEL_POLYLINE_CENTER_LENGTH", StringComparison.OrdinalIgnoreCase))
            {
                string storedLength = QtoXDataHelper.GetXDataValue(conduit, QtoXDataHelper.KeyLengthM);
                double parsedLength;

                if (double.TryParse(storedLength, out parsedLength) && parsedLength > 0.0)
                {
                    return parsedLength;
                }
            }

            return QtoGeometryHelper.ConvertMmToM(measuredLengthMm);
        }

        private static string GetConduitLengthSource(Entity conduit, string existingLengthSource)
        {
            if (!string.IsNullOrWhiteSpace(existingLengthSource))
            {
                return existingLengthSource;
            }

            if (conduit is Mline)
            {
                return "MLINE_VERTEX_LENGTH";
            }

            if (conduit is Polyline)
            {
                return "POLYLINE_LENGTH";
            }

            return "UNKNOWN";
        }

        private static List<Point3d> BuildOrthogonalRouteToPoint(Point3d startPoint, Point3d endPoint)
        {
            List<Point3d> points = new List<Point3d>();
            AddRoutePoint(points, startPoint);

            double deltaX = Math.Abs(endPoint.X - startPoint.X);
            double deltaY = Math.Abs(endPoint.Y - startPoint.Y);

            if (deltaX > TrayPointTolerance && deltaY > TrayPointTolerance)
            {
                Point3d bendPoint;

                if (deltaX >= deltaY)
                {
                    bendPoint = new Point3d(endPoint.X, startPoint.Y, startPoint.Z);
                }
                else
                {
                    bendPoint = new Point3d(startPoint.X, endPoint.Y, startPoint.Z);
                }

                AddRoutePoint(points, bendPoint);
            }

            AddRoutePoint(points, endPoint);
            return points;
        }

        private static DBText CreateOutletLabelText(Point3d outletPosition, string outletId, double textHeight, double yOffset)
        {
            Point3d labelPosition = new Point3d(outletPosition.X, outletPosition.Y + yOffset, outletPosition.Z);
            DBText label = new DBText();
            label.SetDatabaseDefaults();
            label.TextString = outletId ?? string.Empty;
            label.Height = textHeight;
            label.Position = labelPosition;
            label.HorizontalMode = TextHorizontalMode.TextCenter;
            label.VerticalMode = TextVerticalMode.TextVerticalMid;
            label.AlignmentPoint = labelPosition;
            label.Rotation = 0.0;
            return label;
        }

        private static void DeleteExistingOutletLabels(BlockTableRecord modelSpace, Transaction transaction, string outletId)
        {
            if (modelSpace == null || transaction == null || string.IsNullOrWhiteSpace(outletId))
            {
                return;
            }

            List<ObjectId> labelObjectIds = new List<ObjectId>();

            foreach (ObjectId objectId in modelSpace)
            {
                Entity entity = transaction.GetObject(objectId, OpenMode.ForRead, false) as Entity;

                if (entity == null || !QtoXDataHelper.HasQtoType(entity, QtoXDataHelper.TypeOutletLabel))
                {
                    continue;
                }

                string existingOutletId = QtoXDataHelper.GetXDataValue(entity, QtoXDataHelper.KeyOutletId);

                if (string.Equals(existingOutletId, outletId, StringComparison.OrdinalIgnoreCase))
                {
                    labelObjectIds.Add(objectId);
                }
            }

            foreach (ObjectId labelObjectId in labelObjectIds)
            {
                Entity label = transaction.GetObject(labelObjectId, OpenMode.ForWrite, false) as Entity;

                if (label != null)
                {
                    label.Erase();
                }
            }
        }

        private static void CreateCalloutAnnotation(BlockTableRecord modelSpace, Database database, Transaction transaction, BlockReference sourceBlock, BlockReference junctionBox, string junctionBoxId, double arrowLength, double textHeight)
        {
            Point3d sourcePoint = sourceBlock.Position;
            Point3d targetPoint = junctionBox.Position;
            Vector3d direction = new Vector3d(targetPoint.X - sourcePoint.X, targetPoint.Y - sourcePoint.Y, 0.0);

            if (direction.Length <= 0.0001)
            {
                direction = Vector3d.XAxis;
            }

            direction = direction.GetNormal();
            double sourceToTargetDistance = sourcePoint.DistanceTo(targetPoint);
            double effectiveArrowLength = arrowLength;

            if (sourceToTargetDistance > 0.0001)
            {
                effectiveArrowLength = Math.Min(arrowLength, sourceToTargetDistance * 0.6);
            }

            effectiveArrowLength = Math.Max(effectiveArrowLength, textHeight * 1.5);
            Point3d arrowStart = sourcePoint + direction.MultiplyBy(textHeight * 0.35);
            Point3d arrowEnd = arrowStart + direction.MultiplyBy(effectiveArrowLength);
            Polyline arrowLine = CreateTwoPointPolyline(arrowStart, arrowEnd);
            AppendCalloutEntity(modelSpace, database, transaction, arrowLine, junctionBoxId);

            double arrowHeadLength = Math.Min(effectiveArrowLength * 0.28, textHeight * 1.4);
            arrowHeadLength = Math.Max(arrowHeadLength, textHeight * 0.8);
            Vector3d backDirection = direction.Negate();
            Vector3d leftHeadDirection = RotateVector2d(backDirection, 25.0);
            Vector3d rightHeadDirection = RotateVector2d(backDirection, -25.0);

            Polyline leftHead = CreateTwoPointPolyline(arrowEnd, arrowEnd + leftHeadDirection.MultiplyBy(arrowHeadLength));
            Polyline rightHead = CreateTwoPointPolyline(arrowEnd, arrowEnd + rightHeadDirection.MultiplyBy(arrowHeadLength));
            AppendCalloutEntity(modelSpace, database, transaction, leftHead, junctionBoxId);
            AppendCalloutEntity(modelSpace, database, transaction, rightHead, junctionBoxId);

            Vector3d textOffsetDirection = new Vector3d(-direction.Y, direction.X, 0.0);
            Point3d textPosition = arrowStart + direction.MultiplyBy(effectiveArrowLength * 0.5) + textOffsetDirection.MultiplyBy(textHeight * 0.75);
            DBText label = new DBText();
            label.SetDatabaseDefaults();
            label.Position = textPosition;
            label.Height = textHeight;
            label.TextString = "to" + junctionBoxId;
            label.Rotation = 0.0;
            AppendCalloutEntity(modelSpace, database, transaction, label, junctionBoxId);
        }

        private static Polyline CreateTwoPointPolyline(Point3d startPoint, Point3d endPoint)
        {
            Polyline polyline = new Polyline();
            polyline.SetDatabaseDefaults();
            polyline.Elevation = startPoint.Z;
            polyline.AddVertexAt(0, new Point2d(startPoint.X, startPoint.Y), 0.0, 0.0, 0.0);
            polyline.AddVertexAt(1, new Point2d(endPoint.X, endPoint.Y), 0.0, 0.0, 0.0);
            polyline.ColorIndex = 256;
            polyline.Linetype = "ByLayer";
            polyline.LineWeight = LineWeight.ByLayer;
            return polyline;
        }

        private static Vector3d RotateVector2d(Vector3d vector, double degrees)
        {
            double radians = degrees * Math.PI / 180.0;
            double cos = Math.Cos(radians);
            double sin = Math.Sin(radians);
            return new Vector3d(
                vector.X * cos - vector.Y * sin,
                vector.X * sin + vector.Y * cos,
                0.0).GetNormal();
        }

        private static void AppendCalloutEntity(BlockTableRecord modelSpace, Database database, Transaction transaction, Entity entity, string junctionBoxId)
        {
            modelSpace.AppendEntity(entity);
            transaction.AddNewlyCreatedDBObject(entity, true);
            QtoLayerHelper.MoveEntityToLayer(entity, database, transaction, QtoLayerHelper.CalloutLayerName);

            Dictionary<string, string> data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            data[QtoXDataHelper.KeyQtoType] = QtoXDataHelper.TypeCallout;
            data[QtoXDataHelper.KeyJbId] = junctionBoxId ?? string.Empty;
            QtoXDataHelper.SetXData(entity, database, transaction, data);
        }

        private static void AssignBlockToJunctionBox(BlockReference sourceBlock, Database database, Transaction transaction, string junctionBoxId)
        {
            Dictionary<string, string> data = QtoXDataHelper.GetXData(sourceBlock);

            if (!data.ContainsKey(QtoXDataHelper.KeyQtoType) || string.IsNullOrWhiteSpace(data[QtoXDataHelper.KeyQtoType]))
            {
                data[QtoXDataHelper.KeyQtoType] = QtoXDataHelper.TypeOutlet;
            }

            data[QtoXDataHelper.KeyJbId] = junctionBoxId ?? string.Empty;

            if (!data.ContainsKey(QtoXDataHelper.KeySystem))
            {
                data[QtoXDataHelper.KeySystem] = "DATA";
            }

            QtoXDataHelper.SetXData(sourceBlock, database, transaction, data);
        }

        private static string GetJunctionBoxDisplayName(Transaction transaction, BlockReference junctionBox)
        {
            string junctionBoxId = QtoXDataHelper.GetXDataValue(junctionBox, QtoXDataHelper.KeyJbId);

            if (!string.IsNullOrWhiteSpace(junctionBoxId))
            {
                return junctionBoxId.Trim();
            }

            string blockName = GetBlockName(transaction, junctionBox);

            if (!string.IsNullOrWhiteSpace(blockName))
            {
                return blockName.Trim();
            }

            return "JB";
        }

        private static Dictionary<string, string> BuildWireXData(Polyline polyline, Transaction transaction, Database database, string outletId, string jbId, string system, string cableType, string routeMode, string routeStatus)
        {
            Dictionary<string, string> data = QtoXDataHelper.GetXData(polyline);
            data[QtoXDataHelper.KeyQtoType] = QtoXDataHelper.TypeWire;
            data[QtoXDataHelper.KeyRouteId] = GetOrCreateRouteId(data, transaction, database);
            data[QtoXDataHelper.KeyRouteMode] = string.IsNullOrWhiteSpace(routeMode) ? QtoXDataHelper.RouteModeLegacyManual : routeMode;
            data[QtoXDataHelper.KeyRouteStatus] = string.IsNullOrWhiteSpace(routeStatus) ? QtoXDataHelper.RouteStatusDraft : routeStatus;
            data[QtoXDataHelper.KeyOutletId] = outletId ?? string.Empty;
            data[QtoXDataHelper.KeyJbId] = jbId ?? string.Empty;
            data[QtoXDataHelper.KeySystem] = system ?? string.Empty;
            data[QtoXDataHelper.KeyCableType] = cableType ?? string.Empty;
            data[QtoXDataHelper.KeyLengthM] = QtoGeometryHelper.ConvertMmToM(QtoGeometryHelper.GetPolylineLength(polyline)).ToString("0.000");
            data[QtoXDataHelper.KeyLengthSource] = "POLYLINE_LENGTH";
            return data;
        }

        private static string GetOrCreateRouteId(Dictionary<string, string> data, Transaction transaction, Database database)
        {
            string existing = GetDictionaryValue(data, QtoXDataHelper.KeyRouteId);

            if (!string.IsNullOrWhiteSpace(existing))
            {
                return existing;
            }

            return "ROUTE-" + GetNextRouteNumber(transaction, database).ToString("0000");
        }

        private static string GetDictionaryValue(Dictionary<string, string> data, string key)
        {
            string value;

            if (data != null && data.TryGetValue(key, out value))
            {
                return value;
            }

            return string.Empty;
        }

        private static PromptEntityResult PromptOutletEntity(Editor editor, string message)
        {
            PromptEntityOptions options = new PromptEntityOptions(message);
            options.SetRejectMessage("\n物件必須是 QTO 出線口圖塊。");
            options.AddAllowedClass(typeof(BlockReference), true);
            return editor.GetEntity(options);
        }

        private static PromptEntityResult PromptJunctionBoxEntity(Editor editor, string message)
        {
            PromptEntityOptions options = new PromptEntityOptions(message);
            options.SetRejectMessage("\n物件必須是 QTO 結線箱圖塊。");
            options.AddAllowedClass(typeof(BlockReference), true);
            return editor.GetEntity(options);
        }

        private static void RunSelectedTrayBatchRoutePolylineOnly()
        {
            Document document = Application.DocumentManager.MdiActiveDocument;

            if (document == null)
            {
                return;
            }

            Editor editor = document.Editor;
            Database database = document.Database;

            PromptSelectionOptions traySelectionOptions = new PromptSelectionOptions();
            traySelectionOptions.MessageForAdding = "\n請選取本次要使用的線槽 Polyline：";
            PromptSelectionResult traySelectionResult = editor.GetSelection(traySelectionOptions);

            if (traySelectionResult.Status != PromptStatus.OK)
            {
                editor.WriteMessage("\n已取消批次線槽尋路。");
                return;
            }

            PromptSelectionOptions outletSelectionOptions = new PromptSelectionOptions();
            outletSelectionOptions.MessageForAdding = "\n請框選要沿線槽回到箱體的出線口：";
            PromptSelectionResult outletSelectionResult = editor.GetSelection(outletSelectionOptions);

            if (outletSelectionResult.Status != PromptStatus.OK)
            {
                editor.WriteMessage("\n已取消批次線槽尋路。");
                return;
            }

            PromptEntityResult junctionBoxResult = PromptJunctionBoxEntity(editor, "\n請點選箱體：");

            if (junctionBoxResult.Status != PromptStatus.OK)
            {
                editor.WriteMessage("\n已取消批次線槽尋路。");
                return;
            }

            List<ObjectId> trayObjectIds = new List<ObjectId>();

            foreach (SelectedObject selectedTray in traySelectionResult.Value)
            {
                if (selectedTray != null)
                {
                    trayObjectIds.Add(selectedTray.ObjectId);
                }
            }

            int outletCount = 0;
            int successCount = 0;
            int failureCount = 0;
            List<string> errorMessages = new List<string>();

            foreach (SelectedObject selectedOutlet in outletSelectionResult.Value)
            {
                if (selectedOutlet == null)
                {
                    failureCount++;
                    errorMessages.Add("選取物件無效");
                    continue;
                }

                outletCount++;
                TrayRouteResult routeResult = RouteOutletToJunctionBoxBySelectedTrays(database, selectedOutlet.ObjectId, junctionBoxResult.ObjectId, trayObjectIds);

                if (routeResult.Success)
                {
                    successCount++;
                    editor.WriteMessage("\n已產生配線：" + routeResult.OutletId + " -> " + routeResult.JbId + "，長度 " + routeResult.LengthM.ToString("0.000") + " m");
                }
                else
                {
                    failureCount++;
                    string outletLabel = string.IsNullOrWhiteSpace(routeResult.OutletId) ? selectedOutlet.ObjectId.ToString() : routeResult.OutletId;
                    errorMessages.Add(outletLabel + "：" + routeResult.ErrorMessage);
                }
            }

            editor.WriteMessage("\n批次線槽尋路完成。");
            editor.WriteMessage("\n線槽數量：" + trayObjectIds.Count);
            editor.WriteMessage("\n出線口數量：" + outletCount);
            editor.WriteMessage("\n成功產生聚合線：" + successCount);
            editor.WriteMessage("\n失敗或略過：" + failureCount);

            foreach (string errorMessage in errorMessages)
            {
                editor.WriteMessage("\n" + errorMessage);
            }

            string message = "線槽數量：" + trayObjectIds.Count + "\n出線口數量：" + outletCount + "\n成功產生聚合線：" + successCount + "\n失敗或略過：" + failureCount;

            if (errorMessages.Count > 0)
            {
                message += "\n\n部分出線口未產生，請查看命令列錯誤訊息。";
            }

            ShowStepMessage(
                "批次線槽尋路完成",
                message,
                "下一步可以執行「線段明細」或「配線關係明細」輸出 CSV 報表。");
        }

        private static void RunSelectedTrayBatchRoute()
        {
            if (DateTime.Now.Ticks >= 0)
            {
                RunSelectedTrayBatchRoutePolylineOnly();
                return;
            }

            Document document = Application.DocumentManager.MdiActiveDocument;

            if (document == null)
            {
                return;
            }

            Editor editor = document.Editor;
            Database database = document.Database;

            PromptSelectionOptions traySelectionOptions = new PromptSelectionOptions();
            traySelectionOptions.MessageForAdding = "\n請選取本次要使用的線槽 Polyline：";
            PromptSelectionResult traySelectionResult = editor.GetSelection(traySelectionOptions);

            if (traySelectionResult.Status != PromptStatus.OK)
            {
                editor.WriteMessage("\n已取消批次線槽尋路。");
                return;
            }

            PromptSelectionOptions outletSelectionOptions = new PromptSelectionOptions();
            outletSelectionOptions.MessageForAdding = "\n請框選要沿線槽回到箱體的出線口：";
            PromptSelectionResult outletSelectionResult = editor.GetSelection(outletSelectionOptions);

            if (outletSelectionResult.Status != PromptStatus.OK)
            {
                editor.WriteMessage("\n已取消批次線槽尋路。");
                return;
            }

            PromptEntityResult junctionBoxResult = PromptJunctionBoxEntity(editor, "\n請點選箱體：");

            if (junctionBoxResult.Status != PromptStatus.OK)
            {
                editor.WriteMessage("\n已取消批次線槽尋路。");
                return;
            }

            List<ObjectId> trayObjectIds = new List<ObjectId>();

            foreach (SelectedObject selectedTray in traySelectionResult.Value)
            {
                if (selectedTray != null)
                {
                    trayObjectIds.Add(selectedTray.ObjectId);
                }
            }

            List<string[]> rows = new List<string[]>();
            rows.Add(new string[] { "出線口編號", "箱體編號", "配線物件ID", "長度_公尺", "狀態", "錯誤訊息" });
            int outletCount = 0;
            int successCount = 0;
            int failureCount = 0;

            foreach (SelectedObject selectedOutlet in outletSelectionResult.Value)
            {
                if (selectedOutlet == null)
                {
                    failureCount++;
                    rows.Add(new string[] { string.Empty, string.Empty, string.Empty, "0.000", "錯誤", "選取物件無效" });
                    continue;
                }

                outletCount++;
                TrayRouteResult routeResult = RouteOutletToJunctionBoxBySelectedTrays(database, selectedOutlet.ObjectId, junctionBoxResult.ObjectId, trayObjectIds);

                if (routeResult.Success)
                {
                    successCount++;
                    rows.Add(new string[] { routeResult.OutletId, routeResult.JbId, routeResult.WireObjectId.ToString(), routeResult.LengthM.ToString("0.000"), "正常", string.Empty });
                }
                else
                {
                    failureCount++;
                    rows.Add(new string[] { routeResult.OutletId, routeResult.JbId, string.Empty, "0.000", "錯誤", routeResult.ErrorMessage });
                }
            }

            string outputPath = PromptCsvOutputPath(database, "QTO_BATCH_ROUTE_BY_TRAY_");

            if (!string.IsNullOrWhiteSpace(outputPath))
            {
                QtoCsvExporter.WriteCsvFile(outputPath, rows);
            }

            editor.WriteMessage("\n批次線槽尋路完成。");
            editor.WriteMessage("\n線槽數量：" + trayObjectIds.Count);
            editor.WriteMessage("\n出線口數量：" + outletCount);
            editor.WriteMessage("\n成功產生配線：" + successCount);
            editor.WriteMessage("\n失敗或略過：" + failureCount);
            editor.WriteMessage("\nCSV：" + (outputPath ?? "未輸出"));
            ShowStepMessage(
                "批次線槽尋路完成",
                "線槽數量：" + trayObjectIds.Count + "\n出線口數量：" + outletCount + "\n成功產生配線：" + successCount + "\n失敗或略過：" + failureCount + "\nCSV：" + (outputPath ?? "未輸出"),
                "下一步建議執行「線段明細」或「配線關係明細」，確認每個出線口到箱體的長度與關係。");
        }

        private static TrayRouteResult RouteOutletToJunctionBoxBySelectedTrays(Database database, ObjectId outletObjectId, ObjectId junctionBoxObjectId, List<ObjectId> trayObjectIds)
        {
            TrayRouteResult result = new TrayRouteResult();

            using (Transaction transaction = database.TransactionManager.StartTransaction())
            {
                BlockReference outlet = transaction.GetObject(outletObjectId, OpenMode.ForRead, false) as BlockReference;
                BlockReference junctionBox = transaction.GetObject(junctionBoxObjectId, OpenMode.ForRead, false) as BlockReference;

                if (outlet == null || !QtoXDataHelper.HasQtoType(outlet, QtoXDataHelper.TypeOutlet))
                {
                    result.ErrorMessage = "選取的物件不是已標記的出線口。";
                    return result;
                }

                if (junctionBox == null || !QtoXDataHelper.HasQtoType(junctionBox, QtoXDataHelper.TypeJunctionBox))
                {
                    result.ErrorMessage = "選取的箱體不是已標記的 QTO 箱體。";
                    return result;
                }

                string outletId = QtoXDataHelper.GetXDataValue(outlet, QtoXDataHelper.KeyOutletId) ?? string.Empty;
                string jbId = QtoXDataHelper.GetXDataValue(junctionBox, QtoXDataHelper.KeyJbId) ?? string.Empty;
                string system = QtoXDataHelper.GetXDataValue(outlet, QtoXDataHelper.KeySystem) ?? string.Empty;
                string cableType = QtoXDataHelper.GetXDataValue(outlet, QtoXDataHelper.KeyCableType) ?? string.Empty;
                result.OutletId = outletId;
                result.JbId = jbId;

                if (string.IsNullOrWhiteSpace(outletId))
                {
                    result.ErrorMessage = "缺少 OUTLET_ID，請先標記或重新編號出線口。";
                    return result;
                }

                if (string.IsNullOrWhiteSpace(jbId))
                {
                    result.ErrorMessage = "缺少 JB_ID，請先將出線口指定到箱體。";
                    return result;
                }

                List<SelectedTraySegment> segments = BuildSelectedTraySegments(transaction, trayObjectIds);

                if (segments.Count == 0)
                {
                    result.ErrorMessage = "沒有可用的線槽 Polyline。";
                    return result;
                }

                SelectedTrayProjection outletProjection = FindClosestTrayProjection(outlet.Position, segments);
                SelectedTrayProjection boxProjection = FindClosestTrayProjection(junctionBox.Position, segments);

                if (outletProjection == null || boxProjection == null)
                {
                    result.ErrorMessage = "無法將出線口或箱體投影到線槽。";
                    return result;
                }

                SelectedTrayGraph graph = SelectedTrayGraph.Build(segments, outletProjection, boxProjection);
                List<Point3d> trayPath;

                if (!graph.TryFindPath(outletProjection.NodeId, boxProjection.NodeId, out trayPath))
                {
                    result.ErrorMessage = "選取的線槽之間沒有連通，請確認線槽端點有相接。";
                    return result;
                }

                List<Point3d> routePoints = new List<Point3d>();
                AddRoutePoint(routePoints, outlet.Position);

                foreach (Point3d point in trayPath)
                {
                    AddRoutePoint(routePoints, point);
                }

                AddRoutePoint(routePoints, junctionBox.Position);

                if (routePoints.Count < 2)
                {
                    result.ErrorMessage = "產生的配線路徑點不足。";
                    return result;
                }

                BlockTableRecord modelSpace = GetModelSpace(transaction, database);
                modelSpace.UpgradeOpen();
                Polyline wire = CreatePolylineFromPoints(routePoints);
                modelSpace.AppendEntity(wire);
                transaction.AddNewlyCreatedDBObject(wire, true);
                QtoLayerHelper.MoveEntityToLayer(wire, database, transaction, QtoLayerHelper.WireLayerName);

                Dictionary<string, string> data = BuildWireXData(
                    wire,
                    transaction,
                    database,
                    outletId,
                    jbId,
                    system,
                    cableType,
                    QtoXDataHelper.RouteModeTrayNetwork,
                    QtoXDataHelper.RouteStatusDraft);
                QtoXDataHelper.SetXData(wire, database, transaction, data);

                result.Success = true;
                result.WireObjectId = wire.ObjectId;
                result.LengthM = QtoGeometryHelper.ConvertMmToM(QtoGeometryHelper.GetPolylineLength(wire));
                transaction.Commit();
            }

            return result;
        }

        private static List<SelectedTraySegment> BuildSelectedTraySegments(Transaction transaction, List<ObjectId> trayObjectIds)
        {
            List<SelectedTraySegment> segments = new List<SelectedTraySegment>();
            int segmentId = 0;

            if (trayObjectIds == null)
            {
                return segments;
            }

            foreach (ObjectId trayObjectId in trayObjectIds)
            {
                Polyline tray = transaction.GetObject(trayObjectId, OpenMode.ForRead, false) as Polyline;

                if (tray == null || tray.NumberOfVertices < 2)
                {
                    continue;
                }

                for (int i = 0; i < tray.NumberOfVertices - 1; i++)
                {
                    Point3d start = tray.GetPoint3dAt(i);
                    Point3d end = tray.GetPoint3dAt(i + 1);
                    double length = start.DistanceTo(end);

                    if (length <= TrayPointTolerance)
                    {
                        continue;
                    }

                    segments.Add(new SelectedTraySegment(segmentId, start, end, length));
                    segmentId++;
                }
            }

            return segments;
        }

        private static SelectedTrayProjection FindClosestTrayProjection(Point3d sourcePoint, List<SelectedTraySegment> segments)
        {
            SelectedTrayProjection closest = null;

            foreach (SelectedTraySegment segment in segments)
            {
                double t;
                Point3d point = ProjectPointToSegment(sourcePoint, segment.Start, segment.End, out t);
                double distance = sourcePoint.DistanceTo(point);

                if (closest == null || distance < closest.Distance)
                {
                    closest = new SelectedTrayProjection(segment, point, t, distance);
                }
            }

            return closest;
        }

        private static Point3d ProjectPointToSegment(Point3d point, Point3d start, Point3d end, out double t)
        {
            Vector3d segment = end - start;
            double lengthSquared = segment.DotProduct(segment);

            if (lengthSquared <= TrayPointTolerance)
            {
                t = 0.0;
                return start;
            }

            t = ((point - start).DotProduct(segment)) / lengthSquared;
            t = Math.Max(0.0, Math.Min(1.0, t));
            return start + (segment * t);
        }

        private static void AddRoutePoint(List<Point3d> points, Point3d point)
        {
            if (points.Count == 0 || points[points.Count - 1].DistanceTo(point) > TrayPointTolerance)
            {
                points.Add(point);
            }
        }

        private class SelectedTraySegment
        {
            public SelectedTraySegment(int id, Point3d start, Point3d end, double length)
            {
                Id = id;
                Start = start;
                End = end;
                Length = length;
            }

            public int Id { get; private set; }
            public Point3d Start { get; private set; }
            public Point3d End { get; private set; }
            public double Length { get; private set; }
        }

        private class SelectedTrayProjection
        {
            public SelectedTrayProjection(SelectedTraySegment segment, Point3d point, double t, double distance)
            {
                Segment = segment;
                Point = point;
                T = t;
                Distance = distance;
                NodeId = -1;
            }

            public SelectedTraySegment Segment { get; private set; }
            public Point3d Point { get; private set; }
            public double T { get; private set; }
            public double Distance { get; private set; }
            public int NodeId { get; set; }
        }

        private class SelectedTrayGraphPoint
        {
            public SelectedTrayGraphPoint(Point3d point, double t)
            {
                Point = point;
                T = t;
                NodeId = -1;
            }

            public Point3d Point { get; private set; }
            public double T { get; private set; }
            public int NodeId { get; set; }
        }

        private class SelectedTrayEdge
        {
            public SelectedTrayEdge(int toNodeId, double length)
            {
                ToNodeId = toNodeId;
                Length = length;
            }

            public int ToNodeId { get; private set; }
            public double Length { get; private set; }
        }

        private class SelectedTrayGraph
        {
            private readonly List<Point3d> nodes = new List<Point3d>();
            private readonly Dictionary<int, List<SelectedTrayEdge>> edges = new Dictionary<int, List<SelectedTrayEdge>>();

            public static SelectedTrayGraph Build(List<SelectedTraySegment> segments, SelectedTrayProjection outletProjection, SelectedTrayProjection boxProjection)
            {
                SelectedTrayGraph graph = new SelectedTrayGraph();

                foreach (SelectedTraySegment segment in segments)
                {
                    List<SelectedTrayGraphPoint> points = new List<SelectedTrayGraphPoint>();
                    points.Add(new SelectedTrayGraphPoint(segment.Start, 0.0));
                    points.Add(new SelectedTrayGraphPoint(segment.End, 1.0));

                    if (outletProjection != null && outletProjection.Segment.Id == segment.Id)
                    {
                        points.Add(new SelectedTrayGraphPoint(outletProjection.Point, outletProjection.T));
                    }

                    if (boxProjection != null && boxProjection.Segment.Id == segment.Id)
                    {
                        points.Add(new SelectedTrayGraphPoint(boxProjection.Point, boxProjection.T));
                    }

                    points.Sort(delegate (SelectedTrayGraphPoint left, SelectedTrayGraphPoint right)
                    {
                        return left.T.CompareTo(right.T);
                    });

                    for (int i = 0; i < points.Count; i++)
                    {
                        points[i].NodeId = graph.GetOrAddNode(points[i].Point);
                    }

                    for (int i = 0; i < points.Count - 1; i++)
                    {
                        int fromNodeId = points[i].NodeId;
                        int toNodeId = points[i + 1].NodeId;

                        if (fromNodeId == toNodeId)
                        {
                            continue;
                        }

                        graph.AddEdge(fromNodeId, toNodeId, points[i].Point.DistanceTo(points[i + 1].Point));
                    }
                }

                if (outletProjection != null)
                {
                    outletProjection.NodeId = graph.GetOrAddNode(outletProjection.Point);
                }

                if (boxProjection != null)
                {
                    boxProjection.NodeId = graph.GetOrAddNode(boxProjection.Point);
                }

                return graph;
            }

            public bool TryFindPath(int startNodeId, int endNodeId, out List<Point3d> path)
            {
                path = new List<Point3d>();

                if (startNodeId < 0 || endNodeId < 0 || startNodeId >= nodes.Count || endNodeId >= nodes.Count)
                {
                    return false;
                }

                int count = nodes.Count;
                double[] distances = new double[count];
                int[] previous = new int[count];
                bool[] visited = new bool[count];

                for (int i = 0; i < count; i++)
                {
                    distances[i] = double.MaxValue;
                    previous[i] = -1;
                }

                distances[startNodeId] = 0.0;

                for (int i = 0; i < count; i++)
                {
                    int current = -1;
                    double currentDistance = double.MaxValue;

                    for (int nodeId = 0; nodeId < count; nodeId++)
                    {
                        if (!visited[nodeId] && distances[nodeId] < currentDistance)
                        {
                            current = nodeId;
                            currentDistance = distances[nodeId];
                        }
                    }

                    if (current == -1)
                    {
                        break;
                    }

                    if (current == endNodeId)
                    {
                        break;
                    }

                    visited[current] = true;
                    List<SelectedTrayEdge> nodeEdges;

                    if (!edges.TryGetValue(current, out nodeEdges))
                    {
                        continue;
                    }

                    foreach (SelectedTrayEdge edge in nodeEdges)
                    {
                        if (visited[edge.ToNodeId])
                        {
                            continue;
                        }

                        double nextDistance = distances[current] + edge.Length;

                        if (nextDistance < distances[edge.ToNodeId])
                        {
                            distances[edge.ToNodeId] = nextDistance;
                            previous[edge.ToNodeId] = current;
                        }
                    }
                }

                if (distances[endNodeId] == double.MaxValue)
                {
                    return false;
                }

                List<int> nodePath = new List<int>();
                int pathNodeId = endNodeId;

                while (pathNodeId != -1)
                {
                    nodePath.Add(pathNodeId);

                    if (pathNodeId == startNodeId)
                    {
                        break;
                    }

                    pathNodeId = previous[pathNodeId];
                }

                if (nodePath[nodePath.Count - 1] != startNodeId)
                {
                    return false;
                }

                nodePath.Reverse();

                foreach (int nodeId in nodePath)
                {
                    path.Add(nodes[nodeId]);
                }

                return true;
            }

            private int GetOrAddNode(Point3d point)
            {
                for (int i = 0; i < nodes.Count; i++)
                {
                    if (nodes[i].DistanceTo(point) <= TrayNodeMergeTolerance)
                    {
                        return i;
                    }
                }

                int nodeId = nodes.Count;
                nodes.Add(point);
                edges[nodeId] = new List<SelectedTrayEdge>();
                return nodeId;
            }

            private void AddEdge(int fromNodeId, int toNodeId, double length)
            {
                if (length <= TrayPointTolerance)
                {
                    return;
                }

                edges[fromNodeId].Add(new SelectedTrayEdge(toNodeId, length));
                edges[toNodeId].Add(new SelectedTrayEdge(fromNodeId, length));
            }
        }

        private static TrayRouteResult RouteOutletToJunctionBoxByTray(Database database, ObjectId outletObjectId, ObjectId junctionBoxObjectId, double tolerance)
        {
            TrayRouteResult result = new TrayRouteResult();

            using (Transaction transaction = database.TransactionManager.StartTransaction())
            {
                BlockReference outlet = transaction.GetObject(outletObjectId, OpenMode.ForRead, false) as BlockReference;
                BlockReference junctionBox = transaction.GetObject(junctionBoxObjectId, OpenMode.ForRead, false) as BlockReference;

                if (outlet == null || !QtoXDataHelper.HasQtoType(outlet, QtoXDataHelper.TypeOutlet))
                {
                    result.ErrorMessage = "選取的出線口不是 QTO 出線口。";
                    return result;
                }

                if (junctionBox == null || !QtoXDataHelper.HasQtoType(junctionBox, QtoXDataHelper.TypeJunctionBox))
                {
                    result.ErrorMessage = "選取的結線箱不是 QTO 結線箱。";
                    return result;
                }

                string outletId = QtoXDataHelper.GetXDataValue(outlet, QtoXDataHelper.KeyOutletId) ?? string.Empty;
                string jbId = QtoXDataHelper.GetXDataValue(junctionBox, QtoXDataHelper.KeyJbId) ?? string.Empty;
                string system = QtoXDataHelper.GetXDataValue(outlet, QtoXDataHelper.KeySystem) ?? string.Empty;
                string cableType = QtoXDataHelper.GetXDataValue(outlet, QtoXDataHelper.KeyCableType) ?? string.Empty;
                result.OutletId = outletId;
                result.JbId = jbId;

                if (string.IsNullOrWhiteSpace(outletId))
                {
                    result.ErrorMessage = "缺少 OUTLET_ID。";
                    return result;
                }

                if (string.IsNullOrWhiteSpace(jbId))
                {
                    result.ErrorMessage = "缺少 JB_ID。";
                    return result;
                }

                TrayNetworkScanResult scanResult = new TrayNetworkScanResult();
                QtoTrayNetwork network = QtoTrayNetwork.Build(transaction, database, tolerance, scanResult);
                List<Point3d> routePoints;
                string errorMessage;

                if (!network.TryFindPath(outlet.Position, junctionBox.Position, out routePoints, out errorMessage))
                {
                    result.ErrorMessage = errorMessage;
                    return result;
                }

                BlockTableRecord modelSpace = GetModelSpace(transaction, database);
                modelSpace.UpgradeOpen();
                Polyline wire = CreatePolylineFromPoints(routePoints);
                modelSpace.AppendEntity(wire);
                transaction.AddNewlyCreatedDBObject(wire, true);
                QtoLayerHelper.MoveEntityToLayer(wire, database, transaction, QtoLayerHelper.WireLayerName);

                Dictionary<string, string> data = BuildWireXData(
                    wire,
                    transaction,
                    database,
                    outletId,
                    jbId,
                    system,
                    cableType,
                    QtoXDataHelper.RouteModeTrayNetwork,
                    QtoXDataHelper.RouteStatusDraft);
                QtoXDataHelper.SetXData(wire, database, transaction, data);

                result.Success = true;
                result.WireObjectId = wire.ObjectId;
                result.LengthM = QtoGeometryHelper.ConvertMmToM(QtoGeometryHelper.GetPolylineLength(wire));
                transaction.Commit();
            }

            return result;
        }

        private static int GetNextOutletNumber(Transaction transaction, Database database)
        {
            int maxNumber = 0;
            List<OutletInfo> outlets = QtoScanner.GetAllQtoOutlets(transaction, database);

            foreach (OutletInfo outlet in outlets)
            {
                maxNumber = Math.Max(maxNumber, ParseNumberWithPrefix(outlet.OutletId, "O-"));
            }

            return maxNumber + 1;
        }

        private static int GetNextJunctionBoxNumber(Transaction transaction, Database database)
        {
            int maxNumber = 0;
            List<JunctionBoxInfo> junctionBoxes = QtoScanner.GetAllQtoJunctionBoxes(transaction, database);

            foreach (JunctionBoxInfo junctionBox in junctionBoxes)
            {
                maxNumber = Math.Max(maxNumber, ParseNumberWithPrefix(junctionBox.JbId, "JB-"));
            }

            return maxNumber + 1;
        }

        private static int GetNextRouteNumber(Transaction transaction, Database database)
        {
            int maxNumber = 0;
            List<WireInfo> wires = QtoScanner.GetAllQtoWires(transaction, database);

            foreach (WireInfo wire in wires)
            {
                maxNumber = Math.Max(maxNumber, ParseNumberWithPrefix(wire.RouteId, "ROUTE-"));
            }

            return maxNumber + 1;
        }

        private static int GetNextTrayNumber(Transaction transaction, Database database)
        {
            int maxNumber = 0;
            List<TrayInfo> trays = QtoScanner.GetAllQtoTrays(transaction, database);

            foreach (TrayInfo tray in trays)
            {
                maxNumber = Math.Max(maxNumber, ParseNumberWithPrefix(tray.TrayId, "TRAY-"));
            }

            return maxNumber + 1;
        }

        private static int ParseNumberWithPrefix(string value, string prefix)
        {
            if (string.IsNullOrWhiteSpace(value) || string.IsNullOrWhiteSpace(prefix))
            {
                return 0;
            }

            if (!value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }

            int number;

            if (int.TryParse(value.Substring(prefix.Length), out number))
            {
                return number;
            }

            return 0;
        }

        private static List<JunctionBoxSummaryInfo> BuildJunctionBoxSummaries(List<OutletInfo> outlets, List<WireInfo> wires)
        {
            Dictionary<string, JunctionBoxSummaryInfo> summaries = new Dictionary<string, JunctionBoxSummaryInfo>(StringComparer.OrdinalIgnoreCase);

            if (outlets != null)
            {
                foreach (OutletInfo outlet in outlets)
                {
                    if (string.IsNullOrWhiteSpace(outlet.JbId))
                    {
                        continue;
                    }

                    JunctionBoxSummaryInfo summary = GetOrCreateSummary(summaries, outlet.JbId, outlet.System, outlet.CableType);
                    summary.OutletCount++;
                }
            }

            if (wires != null)
            {
                foreach (WireInfo wire in wires)
                {
                    if (string.IsNullOrWhiteSpace(wire.JbId))
                    {
                        continue;
                    }

                    JunctionBoxSummaryInfo summary = GetOrCreateSummary(summaries, wire.JbId, wire.System, wire.CableType);
                    summary.WireCount++;
                    summary.TotalLengthM += wire.LengthM;
                }
            }

            List<JunctionBoxSummaryInfo> result = new List<JunctionBoxSummaryInfo>(summaries.Values);
            result.Sort(CompareJunctionBoxSummary);
            return result;
        }

        private static JunctionBoxSummaryInfo GetOrCreateSummary(Dictionary<string, JunctionBoxSummaryInfo> summaries, string jbId, string system, string cableType)
        {
            string key = (jbId ?? string.Empty) + "|" + (system ?? string.Empty) + "|" + (cableType ?? string.Empty);
            JunctionBoxSummaryInfo summary;

            if (summaries.TryGetValue(key, out summary))
            {
                return summary;
            }

            summary = new JunctionBoxSummaryInfo();
            summary.JbId = jbId ?? string.Empty;
            summary.System = system ?? string.Empty;
            summary.CableType = cableType ?? string.Empty;
            summaries.Add(key, summary);
            return summary;
        }

        private static int CompareJunctionBoxSummary(JunctionBoxSummaryInfo left, JunctionBoxSummaryInfo right)
        {
            int jbCompare = string.Compare(left.JbId, right.JbId, StringComparison.OrdinalIgnoreCase);

            if (jbCompare != 0)
            {
                return jbCompare;
            }

            int systemCompare = string.Compare(left.System, right.System, StringComparison.OrdinalIgnoreCase);

            if (systemCompare != 0)
            {
                return systemCompare;
            }

            return string.Compare(left.CableType, right.CableType, StringComparison.OrdinalIgnoreCase);
        }

        private static string PromptStringWithDefault(Editor editor, string message, string defaultValue)
        {
            PromptStringOptions options = new PromptStringOptions(message + " <" + defaultValue + ">\uff1a");
            options.AllowSpaces = true;
            options.DefaultValue = defaultValue;
            options.UseDefaultValue = true;

            PromptResult result = editor.GetString(options);

            if (result.Status != PromptStatus.OK)
            {
                return defaultValue;
            }

            if (string.IsNullOrWhiteSpace(result.StringResult))
            {
                return defaultValue;
            }

            return result.StringResult.Trim();
        }

        private static bool PromptYesNo(Editor editor, string message, bool defaultValue)
        {
            System.Windows.Forms.MessageBoxButtons buttons = System.Windows.Forms.MessageBoxButtons.YesNo;
            System.Windows.Forms.MessageBoxIcon icon = System.Windows.Forms.MessageBoxIcon.Question;
            System.Windows.Forms.MessageBoxDefaultButton defaultButton = defaultValue ? System.Windows.Forms.MessageBoxDefaultButton.Button1 : System.Windows.Forms.MessageBoxDefaultButton.Button2;
            System.Windows.Forms.DialogResult result = System.Windows.Forms.MessageBox.Show(
                message.Replace("[Yes/No]", string.Empty).Trim(),
                "\u8acb\u78ba\u8a8d",
                buttons,
                icon,
                defaultButton);

            return result == System.Windows.Forms.DialogResult.Yes;
        }

        private static ExportScopeSelection PromptExportScope(Editor editor, string reportName)
        {
            ExportScopeMode mode = ShowExportScopeDialog(reportName);

            if (mode == ExportScopeMode.Cancel)
            {
                return null;
            }

            ExportScopeSelection selection = new ExportScopeSelection();
            selection.Mode = mode;
            selection.ObjectIds = new HashSet<ObjectId>();

            if (mode == ExportScopeMode.All)
            {
                return selection;
            }

            PromptSelectionOptions options = new PromptSelectionOptions();
            options.MessageForAdding = "\n請框選要匯出的範圍：";
            PromptSelectionResult result = editor.GetSelection(options);

            if (result.Status != PromptStatus.OK)
            {
                return null;
            }

            foreach (SelectedObject selectedObject in result.Value)
            {
                if (selectedObject != null)
                {
                    selection.ObjectIds.Add(selectedObject.ObjectId);
                }
            }

            return selection;
        }

        private static ExportScopeMode ShowExportScopeDialog(string reportName)
        {
            ExportScopeMode selectedMode = ExportScopeMode.Cancel;

            using (System.Windows.Forms.Form form = new System.Windows.Forms.Form())
            using (System.Windows.Forms.Label label = new System.Windows.Forms.Label())
            using (System.Windows.Forms.Button allButton = new System.Windows.Forms.Button())
            using (System.Windows.Forms.Button selectionButton = new System.Windows.Forms.Button())
            using (System.Windows.Forms.Button cancelButton = new System.Windows.Forms.Button())
            {
                form.Text = "選擇匯出範圍";
                form.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
                form.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
                form.ClientSize = new System.Drawing.Size(360, 132);
                form.MaximizeBox = false;
                form.MinimizeBox = false;
                form.ShowInTaskbar = false;

                label.Text = "請選擇「" + (reportName ?? "報表") + "」的匯出範圍：";
                label.AutoSize = false;
                label.Location = new System.Drawing.Point(16, 16);
                label.Size = new System.Drawing.Size(328, 32);

                allButton.Text = "全部匯出";
                allButton.Location = new System.Drawing.Point(16, 70);
                allButton.Size = new System.Drawing.Size(96, 32);
                allButton.Click += delegate
                {
                    selectedMode = ExportScopeMode.All;
                    form.DialogResult = System.Windows.Forms.DialogResult.OK;
                    form.Close();
                };

                selectionButton.Text = "框選範圍";
                selectionButton.Location = new System.Drawing.Point(132, 70);
                selectionButton.Size = new System.Drawing.Size(96, 32);
                selectionButton.Click += delegate
                {
                    selectedMode = ExportScopeMode.Selection;
                    form.DialogResult = System.Windows.Forms.DialogResult.OK;
                    form.Close();
                };

                cancelButton.Text = "取消";
                cancelButton.Location = new System.Drawing.Point(248, 70);
                cancelButton.Size = new System.Drawing.Size(96, 32);
                cancelButton.Click += delegate
                {
                    selectedMode = ExportScopeMode.Cancel;
                    form.DialogResult = System.Windows.Forms.DialogResult.Cancel;
                    form.Close();
                };

                form.Controls.Add(label);
                form.Controls.Add(allButton);
                form.Controls.Add(selectionButton);
                form.Controls.Add(cancelButton);
                form.AcceptButton = allButton;
                form.CancelButton = cancelButton;

                Application.ShowModalDialog(form);
            }

            return selectedMode;
        }

        private static bool ObjectIdInExportScope(ObjectId objectId, ExportScopeSelection exportScope)
        {
            if (exportScope == null || exportScope.Mode == ExportScopeMode.All)
            {
                return true;
            }

            return exportScope.ObjectIds != null && exportScope.ObjectIds.Contains(objectId);
        }

        private static List<OutletInfo> FilterOutletsByScope(List<OutletInfo> outlets, ExportScopeSelection exportScope)
        {
            List<OutletInfo> filtered = new List<OutletInfo>();

            if (outlets == null)
            {
                return filtered;
            }

            foreach (OutletInfo outlet in outlets)
            {
                if (ObjectIdInExportScope(outlet.ObjectId, exportScope))
                {
                    filtered.Add(outlet);
                }
            }

            return filtered;
        }

        private static List<JunctionBoxInfo> FilterJunctionBoxesByScope(List<JunctionBoxInfo> junctionBoxes, ExportScopeSelection exportScope)
        {
            List<JunctionBoxInfo> filtered = new List<JunctionBoxInfo>();

            if (junctionBoxes == null)
            {
                return filtered;
            }

            foreach (JunctionBoxInfo junctionBox in junctionBoxes)
            {
                if (ObjectIdInExportScope(junctionBox.ObjectId, exportScope))
                {
                    filtered.Add(junctionBox);
                }
            }

            return filtered;
        }

        private static List<WireInfo> FilterWiresByScope(List<WireInfo> wires, ExportScopeSelection exportScope)
        {
            List<WireInfo> filtered = new List<WireInfo>();

            if (wires == null)
            {
                return filtered;
            }

            foreach (WireInfo wire in wires)
            {
                if (ObjectIdInExportScope(wire.ObjectId, exportScope))
                {
                    filtered.Add(wire);
                }
            }

            return filtered;
        }

        private static string PromptCsvOutputPath(Database database, string prefix)
        {
            string defaultPath = QtoCsvExporter.BuildDefaultOutputPath(database, prefix);

            using (System.Windows.Forms.SaveFileDialog dialog = new System.Windows.Forms.SaveFileDialog())
            {
                dialog.Title = "\u9078\u64c7 CSV \u8f38\u51fa\u4f4d\u7f6e";
                dialog.Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*";
                dialog.FileName = System.IO.Path.GetFileName(defaultPath);
                dialog.InitialDirectory = System.IO.Path.GetDirectoryName(defaultPath);
                dialog.OverwritePrompt = true;
                dialog.AddExtension = true;
                dialog.DefaultExt = "csv";

                if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                {
                    return null;
                }

                return dialog.FileName;
            }
        }

        private static BlockTableRecord GetModelSpace(Transaction transaction, Database database)
        {
            BlockTable blockTable = (BlockTable)transaction.GetObject(database.BlockTableId, OpenMode.ForRead);
            return (BlockTableRecord)transaction.GetObject(blockTable[BlockTableRecord.ModelSpace], OpenMode.ForRead);
        }

        private static bool LayerExists(Database database, Transaction transaction, string layerName)
        {
            if (string.IsNullOrWhiteSpace(layerName))
            {
                return false;
            }

            LayerTable layerTable = (LayerTable)transaction.GetObject(database.LayerTableId, OpenMode.ForRead);
            return layerTable.Has(layerName);
        }

        private static string GetErrorLayerName(string itemType)
        {
            if (string.Equals(itemType, "OUTLET", StringComparison.OrdinalIgnoreCase))
            {
                return ErrorOutletLayerName;
            }

            if (string.Equals(itemType, "WIRE", StringComparison.OrdinalIgnoreCase))
            {
                return ErrorWireLayerName;
            }

            if (string.Equals(itemType, "JUNCTION_BOX", StringComparison.OrdinalIgnoreCase))
            {
                return ErrorJbLayerName;
            }

            return string.Empty;
        }

        private static string GetBlockName(Transaction transaction, BlockReference blockReference)
        {
            ObjectId blockTableRecordId = blockReference.BlockTableRecord;

            if (blockReference.IsDynamicBlock)
            {
                blockTableRecordId = blockReference.DynamicBlockTableRecord;
            }

            BlockTableRecord blockTableRecord = transaction.GetObject(blockTableRecordId, OpenMode.ForRead, false) as BlockTableRecord;

            if (blockTableRecord == null)
            {
                return string.Empty;
            }

            return blockTableRecord.Name;
        }

        private static double PromptDoubleWithDefault(Editor editor, string message, double defaultValue)
        {
            PromptDoubleOptions options = new PromptDoubleOptions(message + " <" + defaultValue.ToString("0.###") + ">\uff1a");
            options.AllowNegative = false;
            options.AllowZero = false;
            options.AllowNone = true;
            options.DefaultValue = defaultValue;
            options.UseDefaultValue = true;

            PromptDoubleResult result = editor.GetDouble(options);

            if (result.Status != PromptStatus.OK)
            {
                return defaultValue;
            }

            return result.Value;
        }

        private static string BuildSequentialJbId(string input, int index)
        {
            string prefix = string.IsNullOrWhiteSpace(input) ? "JB" : input.Trim();
            int dashIndex = prefix.LastIndexOf('-');

            if (dashIndex > 0 && dashIndex + 1 < prefix.Length)
            {
                string suffix = prefix.Substring(dashIndex + 1);
                int parsedNumber;

                if (int.TryParse(suffix, out parsedNumber))
                {
                    prefix = prefix.Substring(0, dashIndex);
                }
            }

            return prefix + "-" + index.ToString("00");
        }

        private static int CompareOutletSelectionItems(OutletSelectionItem left, OutletSelectionItem right)
        {
            double yDelta = right.Y - left.Y;

            if (Math.Abs(yDelta) > OutletSortYTolerance)
            {
                return yDelta > 0 ? 1 : -1;
            }

            return left.X.CompareTo(right.X);
        }

        private static List<CheckResult> BuildWireCheckResults(List<OutletInfo> outlets, List<JunctionBoxInfo> junctionBoxes, List<WireInfo> wires)
        {
            List<CheckResult> results = new List<CheckResult>();
            Dictionary<string, bool> outletIds = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, bool> junctionBoxIds = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, int> wireCountByOutletId = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (OutletInfo outlet in outlets)
            {
                if (!string.IsNullOrWhiteSpace(outlet.OutletId) && !outletIds.ContainsKey(outlet.OutletId))
                {
                    outletIds.Add(outlet.OutletId, true);
                }
            }

            foreach (JunctionBoxInfo junctionBox in junctionBoxes)
            {
                if (!string.IsNullOrWhiteSpace(junctionBox.JbId) && !junctionBoxIds.ContainsKey(junctionBox.JbId))
                {
                    junctionBoxIds.Add(junctionBox.JbId, true);
                }
            }

            foreach (WireInfo wire in wires)
            {
                if (string.IsNullOrWhiteSpace(wire.OutletId))
                {
                    continue;
                }

                if (!wireCountByOutletId.ContainsKey(wire.OutletId))
                {
                    wireCountByOutletId.Add(wire.OutletId, 0);
                }

                wireCountByOutletId[wire.OutletId]++;
            }

            foreach (OutletInfo outlet in outlets)
            {
                List<string> errors = new List<string>();

                if (string.IsNullOrWhiteSpace(outlet.OutletId))
                {
                    errors.Add("Missing OUTLET_ID");
                }

                if (string.IsNullOrWhiteSpace(outlet.JbId))
                {
                    errors.Add("Missing JB_ID");
                }

                if (!string.IsNullOrWhiteSpace(outlet.OutletId))
                {
                    if (!wireCountByOutletId.ContainsKey(outlet.OutletId))
                    {
                        errors.Add("Missing wire");
                    }
                    else if (wireCountByOutletId[outlet.OutletId] > 1)
                    {
                        errors.Add("Multiple wires for OUTLET_ID");
                    }
                }

                results.Add(BuildCheckResult("OUTLET", outlet.ObjectId.ToString(), outlet.OutletId, outlet.JbId, outlet.System, outlet.CableType, 0.0, errors));
            }

            foreach (JunctionBoxInfo junctionBox in junctionBoxes)
            {
                List<string> errors = new List<string>();

                if (string.IsNullOrWhiteSpace(junctionBox.JbId))
                {
                    errors.Add("Missing JB_ID");
                }

                results.Add(BuildCheckResult("JUNCTION_BOX", junctionBox.ObjectId.ToString(), string.Empty, junctionBox.JbId, junctionBox.System, string.Empty, 0.0, errors));
            }

            foreach (WireInfo wire in wires)
            {
                List<string> errors = new List<string>();

                if (string.IsNullOrWhiteSpace(wire.OutletId))
                {
                    errors.Add("Missing OUTLET_ID");
                }
                else
                {
                    if (!outletIds.ContainsKey(wire.OutletId))
                    {
                        errors.Add("OUTLET_ID does not exist");
                    }

                    if (wireCountByOutletId.ContainsKey(wire.OutletId) && wireCountByOutletId[wire.OutletId] > 1)
                    {
                        errors.Add("Multiple wires for OUTLET_ID");
                    }
                }

                if (string.IsNullOrWhiteSpace(wire.JbId))
                {
                    errors.Add("Missing JB_ID");
                }
                else if (!junctionBoxIds.ContainsKey(wire.JbId))
                {
                    errors.Add("JB_ID does not exist");
                }

                if (wire.LengthM <= 0.0)
                {
                    errors.Add("Length is zero");
                }

                if (!string.Equals(wire.Layer, QtoLayerHelper.WireLayerName, StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add("Wire is not on QTO_WIRE layer");
                }

                results.Add(BuildCheckResult("WIRE", wire.ObjectId.ToString(), wire.OutletId, wire.JbId, wire.System, wire.CableType, wire.LengthM, errors));
            }

            return results;
        }

        private static CheckResult BuildCheckResult(string itemType, string objectId, string outletId, string jbId, string system, string cableType, double lengthM, List<string> errors)
        {
            CheckResult result = new CheckResult();
            result.ItemType = itemType;
            result.ObjectId = objectId;
            result.OutletId = outletId ?? string.Empty;
            result.JbId = jbId ?? string.Empty;
            result.System = system ?? string.Empty;
            result.CableType = cableType ?? string.Empty;
            result.LengthM = lengthM;

            if (errors == null || errors.Count == 0)
            {
                result.Status = "OK";
                result.ErrorMessage = string.Empty;
            }
            else
            {
                result.Status = "ERROR";
                result.ErrorMessage = string.Join("; ", errors.ToArray());
            }

            return result;
        }

        private static int CountStatus(List<CheckResult> results, string status)
        {
            int count = 0;

            foreach (CheckResult result in results)
            {
                if (string.Equals(result.Status, status, StringComparison.OrdinalIgnoreCase))
                {
                    count++;
                }
            }

            return count;
        }

        private static string BuildWireRecalcErrorMessage(WireInfo wire)
        {
            List<string> errors = new List<string>();

            if (string.IsNullOrWhiteSpace(wire.OutletId))
            {
                errors.Add("Missing OUTLET_ID");
            }

            if (string.IsNullOrWhiteSpace(wire.JbId))
            {
                errors.Add("Missing JB_ID");
            }

            if (wire.LengthM <= 0.0)
            {
                errors.Add("Length is zero");
            }

            return string.Join("; ", errors.ToArray());
        }

        private static string BuildBatchOutletErrorMessage(BatchOutletItem outlet)
        {
            List<string> errors = new List<string>();

            if (string.IsNullOrWhiteSpace(outlet.OutletId))
            {
                errors.Add("Missing OUTLET_ID");
            }

            if (string.IsNullOrWhiteSpace(outlet.JbId))
            {
                errors.Add("Missing JB_ID");
            }

            return string.Join("; ", errors.ToArray());
        }

        private static double GetOutletToWireEndpointDistance(Point3d outletPosition, BatchWireItem wire)
        {
            double startDistance = outletPosition.DistanceTo(wire.StartPoint);
            double endDistance = outletPosition.DistanceTo(wire.EndPoint);

            if (startDistance <= endDistance)
            {
                return startDistance;
            }

            return endDistance;
        }

        private static int CompareBatchMatchCandidates(BatchMatchCandidate left, BatchMatchCandidate right)
        {
            int distanceCompare = left.Distance.CompareTo(right.Distance);

            if (distanceCompare != 0)
            {
                return distanceCompare;
            }

            int outletCompare = left.OutletIndex.CompareTo(right.OutletIndex);

            if (outletCompare != 0)
            {
                return outletCompare;
            }

            return left.WireIndex.CompareTo(right.WireIndex);
        }

        private static void ShowStepMessage(string title, string completedMessage, string nextMessage)
        {
            string message = completedMessage;

            if (!string.IsNullOrWhiteSpace(nextMessage))
            {
                message = message + "\n\n" + nextMessage;
            }

            System.Windows.Forms.MessageBox.Show(
                message,
                title,
                System.Windows.Forms.MessageBoxButtons.OK,
                System.Windows.Forms.MessageBoxIcon.Information);
        }

        private static void WriteCommandError(string commandName, System.Exception ex)
        {
            Editor editor = GetEditor();

            if (editor != null)
            {
                editor.WriteMessage("\n" + commandName + " \u57f7\u884c\u5931\u6557\uff1a" + ex.Message);
            }
        }

        private class OutletSelectionItem
        {
            public ObjectId ObjectId { get; set; }
            public double X { get; set; }
            public double Y { get; set; }
            public string ExistingValue { get; set; }
        }

        private class BatchOutletItem
        {
            public ObjectId ObjectId { get; set; }
            public Point3d Position { get; set; }
            public string OutletId { get; set; }
            public string JbId { get; set; }
            public string System { get; set; }
            public string CableType { get; set; }
            public bool IsMatched { get; set; }
            public bool Reported { get; set; }
            public ObjectId MatchedWireObjectId { get; set; }
            public double MatchDistance { get; set; }
        }

        private class BatchWireItem
        {
            public ObjectId ObjectId { get; set; }
            public Point3d StartPoint { get; set; }
            public Point3d EndPoint { get; set; }
            public bool IsMatched { get; set; }
            public ObjectId MatchedOutletObjectId { get; set; }
        }

        private class BatchMatchCandidate
        {
            public int OutletIndex { get; set; }
            public int WireIndex { get; set; }
            public double Distance { get; set; }
        }

        private class ConduitPolylinePair
        {
            public Polyline Primary { get; set; }
            public Polyline Secondary { get; set; }
        }

        private enum ExportScopeMode
        {
            Cancel,
            All,
            Selection
        }

        private class ExportScopeSelection
        {
            public ExportScopeMode Mode { get; set; }
            public HashSet<ObjectId> ObjectIds { get; set; }
        }
    }
}
