using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;

namespace QtoWirePlugin
{
    public class MepCeilingDimCommands
    {
        [CommandMethod("MEPDIMINIT")]
        public void MepDimInit()
        {
            try
            {
                Document document = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
                if (document == null)
                {
                    return;
                }

                Database database = document.Database;

                using (Transaction transaction = database.TransactionManager.StartTransaction())
                {
                    MepLayerHelper.EnsureMepEnvironment(database, transaction);
                    transaction.Commit();
                }

                document.Editor.WriteMessage("\nMEP 天花標註環境已初始化。");
                MepUiHelper.ShowStepMessage(
                    "天花標註初始化完成",
                    "已建立 MEP 尺寸、未知圖塊與基準輔助圖層。\n文字樣式：MEP-DIM-TEXT\n尺寸樣式：MEP-DIM",
                    "下一步：執行「自動標註」，先選閉合範圍或 X 向基準線，再選要標註的圖塊樣本。");
            }
            catch (System.Exception ex)
            {
                WriteCommandError("MEPDIMINIT", ex);
            }
        }

        [CommandMethod("MEPDIMRULE")]
        public void MepDimRule()
        {
            try
            {
                Document document = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
                if (document == null)
                {
                    return;
                }

                Editor editor = document.Editor;
                IList<MepDimRule> rules = MepRuleLoader.GetBuiltInRules();
                StringBuilder builder = new StringBuilder();
                builder.AppendLine("目前使用內建 MEP 天花標註規則：");

                MepUiHelper.WriteHeader(editor, "MEP 天花標註規則");

                foreach (MepDimRule rule in rules)
                {
                    string pointMode = rule.DimensionPointMode == MepDimensionPointMode.BoundingBoxCenter ? "外框中心" : "插入點";
                    string line = rule.BlockName + " => " + rule.Label + " / " + pointMode;
                    if (rule.ShowOpeningSize)
                    {
                        line += " / " + rule.OpeningSize;
                    }

                    editor.WriteMessage("\n" + line);
                    builder.AppendLine(line);
                }

                MepUiHelper.ShowStepMessage(
                    "標註規則檢查完成",
                    builder.ToString(),
                    "目前版本使用內建規則；實際標註時會以你選取的樣本圖塊名稱為準。");
            }
            catch (System.Exception ex)
            {
                WriteCommandError("MEPDIMRULE", ex);
            }
        }

        [CommandMethod("MEPCEILDIM")]
        public void MepCeilDim()
        {
            try
            {
                Document document = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
                if (document == null)
                {
                    return;
                }

                Editor editor = document.Editor;
                Database database = document.Database;

                MepUiHelper.WriteHeader(editor, "MEP 天花設備自動標註");
                editor.WriteMessage("\n流程：1. 選閉合範圍或 X 向基準線  2. 選樣本圖塊  3. 框選範圍或自動偵測  4. 選標註形式");

                PromptEntityResult firstResult = PromptAreaOrReference(editor);
                if (firstResult.Status != PromptStatus.OK)
                {
                    editor.WriteMessage("\n已取消 MEPCEILDIM。");
                    return;
                }

                PromptSelectionResult sampleResult = PromptSampleBlocks(editor);
                if (sampleResult.Status != PromptStatus.OK)
                {
                    editor.WriteMessage("\n已取消 MEPCEILDIM。");
                    return;
                }

                MepSelectionBoundary boundary = null;
                MepReferenceLine xReference = null;
                MepReferenceLine yReference = null;
                List<ObjectId> targetBlockIds = new List<ObjectId>();
                List<string> sampleBlockNames = new List<string>();
                string modeDescription;

                using (Transaction transaction = database.TransactionManager.StartTransaction())
                {
                    Entity firstEntity = transaction.GetObject(firstResult.ObjectId, OpenMode.ForRead) as Entity;
                    string error;

                    if (MepGeometryService.TryCreateBoundary(firstEntity, out boundary, out error))
                    {
                        modeDescription = "閉合 Polyline 範圍模式";
                        CollectSampleBlockNames(sampleResult.Value, transaction, sampleBlockNames);
                        targetBlockIds = FindMatchingBlocksInBoundary(database, transaction, boundary, sampleBlockNames);
                    }
                    else
                    {
                        modeDescription = "雙基準線模式";
                        if (!MepGeometryService.TryCreateReferenceLine(firstEntity, firstResult.PickedPoint, out xReference, out error))
                        {
                            editor.WriteMessage("\n第一個物件無法作為 X 向基準線：" + error);
                            return;
                        }

                        if (!MepGeometryService.IsNearVertical(xReference))
                        {
                            editor.WriteMessage("\nX 向定位基準線應接近垂直。若要用區域，請第一步選閉合 Polyline。");
                            return;
                        }

                        PromptEntityResult yReferenceResult = PromptReferenceLine(editor, "\n請選取 Y 向定位基準線（通常是下側牆線、柱線或水平軸線）：");
                        if (yReferenceResult.Status != PromptStatus.OK)
                        {
                            editor.WriteMessage("\n已取消 MEPCEILDIM。");
                            return;
                        }

                        Entity yReferenceEntity = transaction.GetObject(yReferenceResult.ObjectId, OpenMode.ForRead) as Entity;
                        if (!MepGeometryService.TryCreateReferenceLine(yReferenceEntity, yReferenceResult.PickedPoint, out yReference, out error))
                        {
                            editor.WriteMessage("\nY 向基準線無法使用：" + error);
                            return;
                        }

                        if (!MepGeometryService.IsNearHorizontal(yReference))
                        {
                            editor.WriteMessage("\nY 向定位基準線應接近水平。");
                            return;
                        }

                        CollectSampleBlockNames(sampleResult.Value, transaction, sampleBlockNames);
                    }

                    transaction.Commit();
                }

                if (sampleBlockNames.Count == 0)
                {
                    editor.WriteMessage("\n沒有取得可用的樣本圖塊名稱。");
                    return;
                }

                if (boundary == null)
                {
                    PromptSelectionResult rangeResult = PromptRangeBlocks(editor);
                    if (rangeResult.Status != PromptStatus.OK)
                    {
                        editor.WriteMessage("\n已取消 MEPCEILDIM。");
                        return;
                    }

                    using (Transaction transaction = database.TransactionManager.StartTransaction())
                    {
                        targetBlockIds = FilterSelectionByBlockNames(rangeResult.Value, transaction, sampleBlockNames);
                        transaction.Commit();
                    }
                }

                if (targetBlockIds.Count == 0)
                {
                    MepUiHelper.ShowStepMessage(
                        "沒有找到符合的圖塊",
                        "模式：" + modeDescription + "\n樣本圖塊：" + string.Join(", ", sampleBlockNames.ToArray()),
                        "請確認樣本圖塊名稱是否正確，或框選範圍內是否包含同名圖塊。");
                    return;
                }

                MepDimensionOptions options = PromptDimensionOptions(database);
                if (options == null)
                {
                    editor.WriteMessage("\n已取消產生標註。");
                    return;
                }

                if (!options.GenerateXDimensions && !options.GenerateYDimensions)
                {
                    editor.WriteMessage("\n未勾選任何尺寸方向，已取消。");
                    return;
                }

                MepCeilingDimResult result = new MepCeilingDimResult();
                result.SelectedCount = targetBlockIds.Count;
                List<string> unknownNames = new List<string>();

                using (Transaction transaction = database.TransactionManager.StartTransaction())
                {
                    MepLayerHelper.EnsureMepEnvironment(database, transaction);
                    BlockTable blockTable = (BlockTable)transaction.GetObject(database.BlockTableId, OpenMode.ForRead);
                    BlockTableRecord modelSpace = (BlockTableRecord)transaction.GetObject(blockTable[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                    List<MepEquipmentItem> items = BuildEquipmentItems(targetBlockIds, transaction, result, unknownNames);
                    MepDimensionPlan plan = CreateLightGridDimensionPlan(items, boundary, xReference, yReference, options);

                    if (!ConfirmDimensionPlan(plan))
                    {
                        editor.WriteMessage("\n已取消產生標註。");
                        return;
                    }

                    RenderDimensionPlan(modelSpace, database, transaction, plan, options, result);

                    if (options.MarkUnknownBlocks)
                    {
                        CreateUnknownMarks(modelSpace, database, transaction, items, result);
                    }

                    transaction.Commit();
                }

                editor.WriteMessage("\n模式：" + modeDescription);
                editor.WriteMessage("\n樣本圖塊：" + string.Join(", ", sampleBlockNames.ToArray()));
                editor.WriteMessage("\n符合圖塊：" + result.ProcessedCount);
                editor.WriteMessage("\n未知圖塊：" + result.UnknownCount);
                editor.WriteMessage("\n需覆核：" + result.ReviewCount);
                editor.WriteMessage("\n尺寸數量：" + result.CreatedDimensionCount);
                editor.WriteMessage("\nUNKNOWN 標記：" + result.CreatedAnnotationCount);

                string unknownMessage = unknownNames.Count == 0 ? "無" : string.Join(", ", unknownNames.ToArray());
                MepUiHelper.ShowStepMessage(
                    "天花自動標註完成",
                    "模式：" + modeDescription + "\n符合圖塊：" + result.ProcessedCount + "\n建立尺寸：" + result.CreatedDimensionCount + "\nUNKNOWN 標記：" + result.CreatedAnnotationCount + "\n未知圖塊：" + result.UnknownCount + "\n需人工覆核：" + result.ReviewCount + "\n未知圖塊名稱：" + unknownMessage,
                    "下一步：檢查連續尺寸是否需要拉開或刪減。這版不自動產生設備標籤。");
            }
            catch (System.Exception ex)
            {
                WriteCommandError("MEPCEILDIM", ex);
            }
        }

        private static PromptEntityResult PromptAreaOrReference(Editor editor)
        {
            PromptEntityOptions options = new PromptEntityOptions("\n請選取閉合 Polyline 標註範圍，或選取 X 向定位基準線：");
            options.SetRejectMessage("\n物件必須是 Line 或 Polyline。");
            options.AddAllowedClass(typeof(Line), true);
            options.AddAllowedClass(typeof(Polyline), true);
            return editor.GetEntity(options);
        }

        private static PromptEntityResult PromptReferenceLine(Editor editor, string message)
        {
            PromptEntityOptions options = new PromptEntityOptions(message);
            options.SetRejectMessage("\n基準線必須是 Line 或 Polyline。");
            options.AddAllowedClass(typeof(Line), true);
            options.AddAllowedClass(typeof(Polyline), true);
            return editor.GetEntity(options);
        }

        private static PromptSelectionResult PromptSampleBlocks(Editor editor)
        {
            PromptSelectionOptions options = new PromptSelectionOptions();
            options.MessageForAdding = "\n請選取要標註的圖塊樣本（可多選；同名圖塊會被納入）：";
            SelectionFilter filter = new SelectionFilter(new TypedValue[] { new TypedValue((int)DxfCode.Start, "INSERT") });
            return editor.GetSelection(options, filter);
        }

        private static PromptSelectionResult PromptRangeBlocks(Editor editor)
        {
            PromptSelectionOptions options = new PromptSelectionOptions();
            options.MessageForAdding = "\n請框選要標註的範圍，系統會自動保留與樣本同名的圖塊：";
            SelectionFilter filter = new SelectionFilter(new TypedValue[] { new TypedValue((int)DxfCode.Start, "INSERT") });
            return editor.GetSelection(options, filter);
        }

        private static MepDimensionOptions PromptDimensionOptions(Database database)
        {
            List<string> dimStyleNames;
            string currentDimStyleName;

            using (Transaction transaction = database.TransactionManager.StartTransaction())
            {
                dimStyleNames = GetDimStyleNames(database, transaction);
                currentDimStyleName = MepLayerHelper.GetCurrentDimStyleName(database, transaction);
                transaction.Commit();
            }

            using (MepDimensionOptionsForm form = new MepDimensionOptionsForm(dimStyleNames, currentDimStyleName))
            {
                DialogResult result = Autodesk.AutoCAD.ApplicationServices.Application.ShowModalDialog(form);
                if (result != DialogResult.OK)
                {
                    return null;
                }

                return form.GetOptions();
            }
        }

        private static List<string> GetDimStyleNames(Database database, Transaction transaction)
        {
            List<string> names = new List<string>();
            DimStyleTable table = (DimStyleTable)transaction.GetObject(database.DimStyleTableId, OpenMode.ForRead);

            foreach (ObjectId objectId in table)
            {
                DimStyleTableRecord record = (DimStyleTableRecord)transaction.GetObject(objectId, OpenMode.ForRead);

                if (!record.IsDependent)
                {
                    names.Add(record.Name);
                }
            }

            names.Sort(StringComparer.OrdinalIgnoreCase);
            return names;
        }

        private static void CollectSampleBlockNames(SelectionSet selectionSet, Transaction transaction, List<string> sampleBlockNames)
        {
            foreach (SelectedObject selectedObject in selectionSet)
            {
                if (selectedObject == null)
                {
                    continue;
                }

                BlockReference block = transaction.GetObject(selectedObject.ObjectId, OpenMode.ForRead, false) as BlockReference;
                if (block == null)
                {
                    continue;
                }

                AddUnique(sampleBlockNames, GetEffectiveBlockName(block, transaction));
            }
        }

        private static List<ObjectId> FindMatchingBlocksInBoundary(Database database, Transaction transaction, MepSelectionBoundary boundary, List<string> sampleBlockNames)
        {
            List<ObjectId> blockIds = new List<ObjectId>();
            BlockTable blockTable = (BlockTable)transaction.GetObject(database.BlockTableId, OpenMode.ForRead);
            BlockTableRecord modelSpace = (BlockTableRecord)transaction.GetObject(blockTable[BlockTableRecord.ModelSpace], OpenMode.ForRead);

            foreach (ObjectId objectId in modelSpace)
            {
                BlockReference block = transaction.GetObject(objectId, OpenMode.ForRead, false) as BlockReference;
                if (block == null)
                {
                    continue;
                }

                string blockName = GetEffectiveBlockName(block, transaction);
                if (!ContainsName(sampleBlockNames, blockName))
                {
                    continue;
                }

                MepDimRule rule = MepRuleLoader.FindRule(blockName);
                bool usedFallback;
                Point3d point;
                MepGeometryService.TryGetBlockDimensionPoint(block, rule, out point, out usedFallback);

                if (MepGeometryService.IsPointInsideBoundary(point, boundary))
                {
                    blockIds.Add(objectId);
                }
            }

            return blockIds;
        }

        private static List<ObjectId> FilterSelectionByBlockNames(SelectionSet selectionSet, Transaction transaction, List<string> sampleBlockNames)
        {
            List<ObjectId> blockIds = new List<ObjectId>();

            foreach (SelectedObject selectedObject in selectionSet)
            {
                if (selectedObject == null)
                {
                    continue;
                }

                BlockReference block = transaction.GetObject(selectedObject.ObjectId, OpenMode.ForRead, false) as BlockReference;
                if (block == null)
                {
                    continue;
                }

                string blockName = GetEffectiveBlockName(block, transaction);
                if (ContainsName(sampleBlockNames, blockName))
                {
                    blockIds.Add(selectedObject.ObjectId);
                }
            }

            return blockIds;
        }

        private static List<MepEquipmentItem> BuildEquipmentItems(List<ObjectId> targetBlockIds, Transaction transaction, MepCeilingDimResult result, List<string> unknownNames)
        {
            List<MepEquipmentItem> items = new List<MepEquipmentItem>();

            foreach (ObjectId objectId in targetBlockIds)
            {
                BlockReference block = transaction.GetObject(objectId, OpenMode.ForRead, false) as BlockReference;
                if (block == null)
                {
                    result.ErrorCount++;
                    continue;
                }

                MepEquipmentItem item = BuildEquipmentItem(block, transaction);

                if (item.Rule == null)
                {
                    result.UnknownCount++;
                    AddUnique(unknownNames, item.BlockName);
                }
                else if (item.Rule.RequiresReview || item.UsedFallbackPoint)
                {
                    result.ReviewCount++;
                }

                items.Add(item);
                result.ProcessedCount++;
            }

            return items;
        }

        private static MepEquipmentItem BuildEquipmentItem(BlockReference block, Transaction transaction)
        {
            MepEquipmentItem item = new MepEquipmentItem();
            item.SourceObjectId = block.ObjectId;
            item.BlockName = GetEffectiveBlockName(block, transaction);
            item.InsertionPoint = MepGeometryService.Flatten(block.Position);
            item.Rule = MepRuleLoader.FindRule(item.BlockName);

            bool usedFallback;
            Point3d dimensionPoint;
            MepGeometryService.TryGetBlockDimensionPoint(block, item.Rule, out dimensionPoint, out usedFallback);
            item.DimensionPoint = dimensionPoint;
            item.UsedFallbackPoint = usedFallback;

            return item;
        }

        private static MepDimensionPlan CreateLightGridDimensionPlan(List<MepEquipmentItem> items, MepSelectionBoundary boundary, MepReferenceLine xReference, MepReferenceLine yReference, MepDimensionOptions options)
        {
            MepDimensionPlan plan = new MepDimensionPlan();
            plan.Dimensions = new List<MepPlannedDimension>();

            if (items.Count == 0)
            {
                plan.Grid = new MepLightGrid();
                plan.Grid.Rows = new List<MepGridLine>();
                plan.Grid.Columns = new List<MepGridLine>();
                plan.Summary = "沒有可標註的燈具。";
                return plan;
            }

            plan.Grid = BuildLightGrid(items, options.RowTolerance, options.ColumnTolerance, boundary);

            if (options.Mode == MepDimensionMode.BoundaryOnly)
            {
                PlanBoundaryDimensions(plan, items, boundary, xReference, yReference, options);
                plan.Summary = BuildPlanSummary(plan, items, options);
                return plan;
            }

            if (options.GenerateXDimensions)
            {
                PlanRepresentativeRowDimensions(plan, boundary, options);
            }

            if (options.GenerateYDimensions)
            {
                PlanRepresentativeColumnDimensions(plan, boundary, options);
            }

            if (options.Mode == MepDimensionMode.ChainWithBoundary)
            {
                PlanBoundaryDimensions(plan, items, boundary, xReference, yReference, options);
            }

            plan.Summary = BuildPlanSummary(plan, items, options);
            return plan;
        }

        private static MepLightGrid BuildLightGrid(List<MepEquipmentItem> items, double rowTolerance, double columnTolerance, MepSelectionBoundary boundary)
        {
            MepLightGrid grid = new MepLightGrid();
            grid.Rows = BuildGridLines(items, true, rowTolerance);
            grid.Columns = BuildGridLines(items, false, columnTolerance);
            double centerX = GetCenterX(items, boundary);
            double centerY = GetCenterY(items, boundary);
            grid.RepresentativeRow = PickRepresentativeLine(grid.Rows, centerY);
            grid.RepresentativeColumn = PickRepresentativeLine(grid.Columns, centerX);
            return grid;
        }

        private static List<MepGridLine> BuildGridLines(List<MepEquipmentItem> items, bool byY, double tolerance)
        {
            List<List<MepEquipmentItem>> groups = GroupByCoordinate(items, byY, tolerance);
            List<MepGridLine> lines = new List<MepGridLine>();

            for (int i = 0; i < groups.Count; i++)
            {
                List<MepEquipmentItem> group = groups[i];
                if (byY)
                {
                    group.Sort(CompareByX);
                }
                else
                {
                    group.Sort(CompareByY);
                }

                MepGridLine line = new MepGridLine();
                line.Index = i + 1;
                line.Items = group;
                line.Coordinate = byY ? AverageY(group) : AverageX(group);
                lines.Add(line);
            }

            return lines;
        }

        private static MepGridLine PickRepresentativeLine(List<MepGridLine> lines, double center)
        {
            MepGridLine best = null;

            foreach (MepGridLine line in lines)
            {
                if (best == null)
                {
                    best = line;
                    continue;
                }

                if (line.Items.Count > best.Items.Count)
                {
                    best = line;
                    continue;
                }

                if (line.Items.Count == best.Items.Count && Math.Abs(line.Coordinate - center) < Math.Abs(best.Coordinate - center))
                {
                    best = line;
                }
            }

            return best;
        }

        private static void PlanRepresentativeRowDimensions(MepDimensionPlan plan, MepSelectionBoundary boundary, MepDimensionOptions options)
        {
            if (plan.Grid == null || plan.Grid.RepresentativeRow == null || plan.Grid.RepresentativeRow.Items.Count < 2)
            {
                return;
            }

            List<MepEquipmentItem> row = new List<MepEquipmentItem>(plan.Grid.RepresentativeRow.Items);
            row.Sort(CompareByX);
            double centerY = boundary != null ? (boundary.Extents.MinPoint.Y + boundary.Extents.MaxPoint.Y) / 2.0 : plan.Grid.RepresentativeRow.Coordinate;
            double direction = plan.Grid.RepresentativeRow.Coordinate >= centerY ? 1.0 : -1.0;
            double dimLineY = plan.Grid.RepresentativeRow.Coordinate + direction * options.InternalOffset;

            PlanAdjacentHorizontalDimensions(plan, row, dimLineY, "代表列中心距");
        }

        private static void PlanRepresentativeColumnDimensions(MepDimensionPlan plan, MepSelectionBoundary boundary, MepDimensionOptions options)
        {
            if (plan.Grid == null || plan.Grid.RepresentativeColumn == null || plan.Grid.RepresentativeColumn.Items.Count < 2)
            {
                return;
            }

            List<MepEquipmentItem> column = new List<MepEquipmentItem>(plan.Grid.RepresentativeColumn.Items);
            column.Sort(CompareByY);
            double centerX = boundary != null ? (boundary.Extents.MinPoint.X + boundary.Extents.MaxPoint.X) / 2.0 : plan.Grid.RepresentativeColumn.Coordinate;
            double direction = plan.Grid.RepresentativeColumn.Coordinate >= centerX ? 1.0 : -1.0;
            double dimLineX = plan.Grid.RepresentativeColumn.Coordinate + direction * options.InternalOffset;

            PlanAdjacentVerticalDimensions(plan, column, dimLineX, "代表欄中心距");
        }

        private static void PlanBoundaryDimensions(MepDimensionPlan plan, List<MepEquipmentItem> items, MepSelectionBoundary boundary, MepReferenceLine xReference, MepReferenceLine yReference, MepDimensionOptions options)
        {
            if (boundary == null)
            {
                return;
            }

            if (options.GenerateXDimensions && (options.BoundaryTop || options.BoundaryBottom))
            {
                List<MepEquipmentItem> columns = GetColumnRepresentatives(plan.Grid);
                columns.Sort(CompareByX);

                if (options.BoundaryTop)
                {
                    double dimLineY = boundary.Extents.MaxPoint.Y + options.BoundaryOffset;
                    PlanBoundaryHorizontalChain(plan, columns, boundary.Extents.MinPoint.X, boundary.Extents.MaxPoint.X, boundary.Extents.MaxPoint.Y, dimLineY, "上方外圈定位");
                }

                if (options.BoundaryBottom)
                {
                    double dimLineY = boundary.Extents.MinPoint.Y - options.BoundaryOffset;
                    PlanBoundaryHorizontalChain(plan, columns, boundary.Extents.MinPoint.X, boundary.Extents.MaxPoint.X, boundary.Extents.MinPoint.Y, dimLineY, "下方外圈定位");
                }
            }

            if (options.GenerateYDimensions && (options.BoundaryLeft || options.BoundaryRight))
            {
                List<MepEquipmentItem> rows = GetRowRepresentatives(plan.Grid);
                rows.Sort(CompareByY);

                if (options.BoundaryLeft)
                {
                    double dimLineX = boundary.Extents.MinPoint.X - options.BoundaryOffset;
                    PlanBoundaryVerticalChain(plan, rows, boundary.Extents.MinPoint.Y, boundary.Extents.MaxPoint.Y, boundary.Extents.MinPoint.X, dimLineX, "左側外圈定位");
                }

                if (options.BoundaryRight)
                {
                    double dimLineX = boundary.Extents.MaxPoint.X + options.BoundaryOffset;
                    PlanBoundaryVerticalChain(plan, rows, boundary.Extents.MinPoint.Y, boundary.Extents.MaxPoint.Y, boundary.Extents.MaxPoint.X, dimLineX, "右側外圈定位");
                }
            }
        }

        private static void PlanAdjacentHorizontalDimensions(MepDimensionPlan plan, List<MepEquipmentItem> row, double dimLineY, string purpose)
        {
            for (int i = 0; i + 1 < row.Count; i++)
            {
                AddPlannedDimension(
                    plan,
                    MepPlannedDimensionOrientation.Horizontal,
                    row[i].DimensionPoint,
                    row[i + 1].DimensionPoint,
                    dimLineY,
                    MepLayerHelper.DimXLayerName,
                    row[i],
                    purpose);
            }
        }

        private static void PlanAdjacentVerticalDimensions(MepDimensionPlan plan, List<MepEquipmentItem> column, double dimLineX, string purpose)
        {
            for (int i = 0; i + 1 < column.Count; i++)
            {
                AddPlannedDimension(
                    plan,
                    MepPlannedDimensionOrientation.Vertical,
                    column[i].DimensionPoint,
                    column[i + 1].DimensionPoint,
                    dimLineX,
                    MepLayerHelper.DimYLayerName,
                    column[i],
                    purpose);
            }
        }

        private static void PlanBoundaryHorizontalChain(MepDimensionPlan plan, List<MepEquipmentItem> representatives, double minX, double maxX, double boundaryY, double dimLineY, string purpose)
        {
            if (representatives.Count == 0)
            {
                return;
            }

            List<Point3d> points = new List<Point3d>();
            List<MepEquipmentItem> items = new List<MepEquipmentItem>();
            points.Add(new Point3d(minX, boundaryY, 0.0));
            items.Add(representatives[0]);

            foreach (MepEquipmentItem item in representatives)
            {
                points.Add(new Point3d(item.DimensionPoint.X, boundaryY, 0.0));
                items.Add(item);
            }

            points.Add(new Point3d(maxX, boundaryY, 0.0));
            items.Add(representatives[representatives.Count - 1]);

            for (int i = 0; i + 1 < points.Count; i++)
            {
                AddPlannedDimension(plan, MepPlannedDimensionOrientation.Horizontal, points[i], points[i + 1], dimLineY, MepLayerHelper.DimXLayerName, items[Math.Min(i, items.Count - 1)], purpose);
            }
        }

        private static void PlanBoundaryVerticalChain(MepDimensionPlan plan, List<MepEquipmentItem> representatives, double minY, double maxY, double boundaryX, double dimLineX, string purpose)
        {
            if (representatives.Count == 0)
            {
                return;
            }

            List<Point3d> points = new List<Point3d>();
            List<MepEquipmentItem> items = new List<MepEquipmentItem>();
            points.Add(new Point3d(boundaryX, minY, 0.0));
            items.Add(representatives[0]);

            foreach (MepEquipmentItem item in representatives)
            {
                points.Add(new Point3d(boundaryX, item.DimensionPoint.Y, 0.0));
                items.Add(item);
            }

            points.Add(new Point3d(boundaryX, maxY, 0.0));
            items.Add(representatives[representatives.Count - 1]);

            for (int i = 0; i + 1 < points.Count; i++)
            {
                AddPlannedDimension(plan, MepPlannedDimensionOrientation.Vertical, points[i], points[i + 1], dimLineX, MepLayerHelper.DimYLayerName, items[Math.Min(i, items.Count - 1)], purpose);
            }
        }

        private static void AddPlannedDimension(MepDimensionPlan plan, MepPlannedDimensionOrientation orientation, Point3d startPoint, Point3d endPoint, double dimensionLineCoordinate, string layerName, MepEquipmentItem sourceItem, string purpose)
        {
            if (startPoint.DistanceTo(endPoint) <= 0.0001)
            {
                return;
            }

            MepPlannedDimension dimension = new MepPlannedDimension();
            dimension.Orientation = orientation;
            dimension.StartPoint = startPoint;
            dimension.EndPoint = endPoint;
            dimension.DimensionLineCoordinate = dimensionLineCoordinate;
            dimension.LayerName = layerName;
            dimension.SourceItem = sourceItem;
            dimension.Purpose = purpose;
            plan.Dimensions.Add(dimension);
        }

        private static List<MepEquipmentItem> GetColumnRepresentatives(MepLightGrid grid)
        {
            List<MepEquipmentItem> representatives = new List<MepEquipmentItem>();
            if (grid == null || grid.Columns == null)
            {
                return representatives;
            }

            foreach (MepGridLine column in grid.Columns)
            {
                if (column.Items.Count > 0)
                {
                    column.Items.Sort(CompareByY);
                    representatives.Add(column.Items[column.Items.Count / 2]);
                }
            }

            return representatives;
        }

        private static List<MepEquipmentItem> GetRowRepresentatives(MepLightGrid grid)
        {
            List<MepEquipmentItem> representatives = new List<MepEquipmentItem>();
            if (grid == null || grid.Rows == null)
            {
                return representatives;
            }

            foreach (MepGridLine row in grid.Rows)
            {
                if (row.Items.Count > 0)
                {
                    row.Items.Sort(CompareByX);
                    representatives.Add(row.Items[row.Items.Count / 2]);
                }
            }

            return representatives;
        }

        private static bool ConfirmDimensionPlan(MepDimensionPlan plan)
        {
            DialogResult result = MessageBox.Show(plan.Summary + "\n\n是否產生這些尺寸？", "MEP 天花標註確認", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            return result == DialogResult.Yes;
        }

        private static void RenderDimensionPlan(BlockTableRecord modelSpace, Database database, Transaction transaction, MepDimensionPlan plan, MepDimensionOptions options, MepCeilingDimResult result)
        {
            foreach (MepPlannedDimension planned in plan.Dimensions)
            {
                RotatedDimension dimension;
                if (planned.Orientation == MepPlannedDimensionOrientation.Horizontal)
                {
                    dimension = MepDimensionEngine.CreateHorizontalDimension(planned.StartPoint, planned.EndPoint, planned.DimensionLineCoordinate, database, transaction, options.DimStyleName);
                }
                else
                {
                    dimension = MepDimensionEngine.CreateVerticalDimension(planned.StartPoint, planned.EndPoint, planned.DimensionLineCoordinate, database, transaction, options.DimStyleName);
                }

                AppendMepEntity(modelSpace, database, transaction, dimension, planned.LayerName, MepDimensionEngine.BuildDimensionXData(planned.SourceItem));
                result.CreatedDimensionCount++;
            }
        }

        private static string BuildPlanSummary(MepDimensionPlan plan, List<MepEquipmentItem> items, MepDimensionOptions options)
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("標註型式：" + options.DimStyleName);
            builder.AppendLine("燈具數量：" + items.Count);
            builder.AppendLine("判斷列數：" + (plan.Grid != null && plan.Grid.Rows != null ? plan.Grid.Rows.Count : 0));
            builder.AppendLine("判斷欄數：" + (plan.Grid != null && plan.Grid.Columns != null ? plan.Grid.Columns.Count : 0));

            if (plan.Grid != null && plan.Grid.RepresentativeRow != null)
            {
                builder.AppendLine("代表列：第 " + plan.Grid.RepresentativeRow.Index + " 列，燈具 " + plan.Grid.RepresentativeRow.Items.Count + " 個");
            }

            if (plan.Grid != null && plan.Grid.RepresentativeColumn != null)
            {
                builder.AppendLine("代表欄：第 " + plan.Grid.RepresentativeColumn.Index + " 欄，燈具 " + plan.Grid.RepresentativeColumn.Items.Count + " 個");
            }

            builder.AppendLine("尺寸偏移：約 20cm，產生後可手動移動");
            builder.AppendLine("外圈方向：" + BuildBoundaryDirectionText(options));
            builder.AppendLine("預計尺寸數量：" + plan.Dimensions.Count);

            return builder.ToString();
        }

        private static string BuildBoundaryDirectionText(MepDimensionOptions options)
        {
            List<string> values = new List<string>();
            if (options.BoundaryTop)
            {
                values.Add("上");
            }
            if (options.BoundaryBottom)
            {
                values.Add("下");
            }
            if (options.BoundaryLeft)
            {
                values.Add("左");
            }
            if (options.BoundaryRight)
            {
                values.Add("右");
            }

            return values.Count == 0 ? "無" : string.Join("/", values.ToArray());
        }

        private static void CreateUnknownMarks(BlockTableRecord modelSpace, Database database, Transaction transaction, List<MepEquipmentItem> items, MepCeilingDimResult result)
        {
            foreach (MepEquipmentItem item in items)
            {
                if (item.Rule != null)
                {
                    continue;
                }

                DBText unknown = MepAnnotationEngine.CreateUnknownText(item.BlockName, item.DimensionPoint, database, transaction);
                AppendMepEntity(modelSpace, database, transaction, unknown, MepLayerHelper.UnknownLayerName, MepAnnotationEngine.BuildXData(item, MepXDataHelper.TypeUnknown));
                result.CreatedAnnotationCount++;
            }
        }

        private static void AppendMepEntity(BlockTableRecord modelSpace, Database database, Transaction transaction, Entity entity, string layerName, Dictionary<string, string> xdata)
        {
            modelSpace.AppendEntity(entity);
            transaction.AddNewlyCreatedDBObject(entity, true);
            MepLayerHelper.MoveEntityToLayer(entity, database, transaction, layerName);
            MepXDataHelper.SetXData(entity, database, transaction, xdata);
        }

        private static List<List<MepEquipmentItem>> GroupByCoordinate(List<MepEquipmentItem> items, bool byY, double tolerance)
        {
            List<MepEquipmentItem> sorted = new List<MepEquipmentItem>(items);
            if (byY)
            {
                sorted.Sort(CompareByY);
            }
            else
            {
                sorted.Sort(CompareByX);
            }

            List<List<MepEquipmentItem>> groups = new List<List<MepEquipmentItem>>();

            foreach (MepEquipmentItem item in sorted)
            {
                double value = byY ? item.DimensionPoint.Y : item.DimensionPoint.X;
                bool added = false;

                foreach (List<MepEquipmentItem> group in groups)
                {
                    double groupValue = byY ? AverageY(group) : AverageX(group);
                    if (Math.Abs(value - groupValue) <= tolerance)
                    {
                        group.Add(item);
                        added = true;
                        break;
                    }
                }

                if (!added)
                {
                    List<MepEquipmentItem> group = new List<MepEquipmentItem>();
                    group.Add(item);
                    groups.Add(group);
                }
            }

            return groups;
        }

        private static double GetGroupingTolerance(List<MepEquipmentItem> items, MepSelectionBoundary boundary)
        {
            double span = GetMaxSpan(items, boundary);
            return Clamp(span * 0.03, 20.0, 300.0);
        }

        private static double GetOffset(List<MepEquipmentItem> items, MepSelectionBoundary boundary)
        {
            double span = GetMaxSpan(items, boundary);
            return Clamp(span * 0.06, 80.0, 800.0);
        }

        private static double GetMaxSpan(List<MepEquipmentItem> items, MepSelectionBoundary boundary)
        {
            if (boundary != null)
            {
                return Math.Max(
                    boundary.Extents.MaxPoint.X - boundary.Extents.MinPoint.X,
                    boundary.Extents.MaxPoint.Y - boundary.Extents.MinPoint.Y);
            }

            double minX = items[0].DimensionPoint.X;
            double maxX = items[0].DimensionPoint.X;
            double minY = items[0].DimensionPoint.Y;
            double maxY = items[0].DimensionPoint.Y;

            foreach (MepEquipmentItem item in items)
            {
                minX = Math.Min(minX, item.DimensionPoint.X);
                maxX = Math.Max(maxX, item.DimensionPoint.X);
                minY = Math.Min(minY, item.DimensionPoint.Y);
                maxY = Math.Max(maxY, item.DimensionPoint.Y);
            }

            return Math.Max(maxX - minX, maxY - minY);
        }

        private static double GetCenterX(List<MepEquipmentItem> items, MepSelectionBoundary boundary)
        {
            if (boundary != null)
            {
                return (boundary.Extents.MinPoint.X + boundary.Extents.MaxPoint.X) / 2.0;
            }

            return AverageX(items);
        }

        private static double GetCenterY(List<MepEquipmentItem> items, MepSelectionBoundary boundary)
        {
            if (boundary != null)
            {
                return (boundary.Extents.MinPoint.Y + boundary.Extents.MaxPoint.Y) / 2.0;
            }

            return AverageY(items);
        }

        private static double AverageX(List<MepEquipmentItem> items)
        {
            double total = 0.0;
            foreach (MepEquipmentItem item in items)
            {
                total += item.DimensionPoint.X;
            }

            return total / items.Count;
        }

        private static double AverageY(List<MepEquipmentItem> items)
        {
            double total = 0.0;
            foreach (MepEquipmentItem item in items)
            {
                total += item.DimensionPoint.Y;
            }

            return total / items.Count;
        }

        private static double Clamp(double value, double min, double max)
        {
            return Math.Max(min, Math.Min(max, value));
        }

        private static int CompareByX(MepEquipmentItem left, MepEquipmentItem right)
        {
            return left.DimensionPoint.X.CompareTo(right.DimensionPoint.X);
        }

        private static int CompareByY(MepEquipmentItem left, MepEquipmentItem right)
        {
            return left.DimensionPoint.Y.CompareTo(right.DimensionPoint.Y);
        }

        private static bool ContainsName(List<string> names, string name)
        {
            foreach (string existing in names)
            {
                if (string.Equals(existing, name, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static string GetEffectiveBlockName(BlockReference block, Transaction transaction)
        {
            try
            {
                if (block.IsDynamicBlock)
                {
                    BlockTableRecord dynamicRecord = (BlockTableRecord)transaction.GetObject(block.DynamicBlockTableRecord, OpenMode.ForRead);
                    return dynamicRecord.Name;
                }

                BlockTableRecord record = (BlockTableRecord)transaction.GetObject(block.BlockTableRecord, OpenMode.ForRead);
                return record.Name;
            }
            catch
            {
                return block.Name;
            }
        }

        private static void AddUnique(List<string> values, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            foreach (string existing in values)
            {
                if (string.Equals(existing, value, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            values.Add(value);
        }

        private static void WriteCommandError(string commandName, System.Exception ex)
        {
            Document document = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;

            if (document != null)
            {
                document.Editor.WriteMessage("\n" + commandName + " 執行失敗：" + ex.Message);
            }
        }
    }
}
