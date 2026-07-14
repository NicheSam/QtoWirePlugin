using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;

namespace QtoWirePlugin
{
    public sealed class QtoValidationFixtureCommands
    {
        [CommandMethod("QTO_CREATE_VALIDATION_FIXTURE")]
        public void CreateValidationFixture()
        {
            Document document = Application.DocumentManager.MdiActiveDocument;
            if (document == null) return;
            Database database = document.Database;
            using (Transaction tr = database.TransactionManager.StartTransaction())
            {
                BlockTable blockTable = (BlockTable)tr.GetObject(database.BlockTableId, OpenMode.ForRead);
                ObjectId outletDefinition = EnsureBlock(database, tr, blockTable, "QTO_FIXTURE_OUTLET", false);
                ObjectId cameraDefinition = EnsureBlock(database, tr, blockTable, "QTO_FIXTURE_CAMERA", true);
                BlockTableRecord modelSpace = (BlockTableRecord)tr.GetObject(blockTable[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                foreach (ObjectId existingId in modelSpace)
                {
                    Entity existing = tr.GetObject(existingId, OpenMode.ForWrite, false) as Entity;
                    if (existing != null && !existing.IsErased) existing.Erase();
                }

                for (int i = 0; i < 10; i++)
                {
                    AddDevice(database, tr, modelSpace, outletDefinition, new Point3d(1200 + (i % 5) * 1200, 1200 + (i / 5) * 1200, 0), "資訊", "LAN_OUTLET", "1F", "A區", "辦公區");
                }
                for (int i = 0; i < 6; i++)
                {
                    AddDevice(database, tr, modelSpace, cameraDefinition, new Point3d(9200 + (i % 3) * 1400, 1400 + (i / 3) * 1500, 0), "CCTV", "CCTV_CAMERA", "2F", "B區", "公共區");
                }

                AddMeasuredPolyline(database, tr, modelSpace, QtoXDataHelper.TypeWire, "資訊", "CAT6", string.Empty, new Point2d(1200, 5200), new Point2d(6500, 5200));
                AddMeasuredPolyline(database, tr, modelSpace, QtoXDataHelper.TypeWire, "CCTV", "CAT6", string.Empty, new Point2d(9200, 5200), new Point2d(13200, 5200));
                AddMeasuredPolyline(database, tr, modelSpace, QtoXDataHelper.TypeConduitSegment, "資訊", string.Empty, "EMT 25mm", new Point2d(1200, 6000), new Point2d(6500, 6000));
                AddMeasuredPolyline(database, tr, modelSpace, QtoXDataHelper.TypeConduitSegment, "CCTV", string.Empty, "EMT 25mm", new Point2d(9200, 6000), new Point2d(13200, 6000));

                AddScope(database, tr, modelSpace, new Point2d(500, 500), new Point2d(7600, 6800), QtoXDataHelper.ScopeKindFloor, "1F", "一樓測試框");
                AddScope(database, tr, modelSpace, new Point2d(8400, 500), new Point2d(14200, 6800), QtoXDataHelper.ScopeKindFloor, "2F", "二樓測試框");
                AddScope(database, tr, modelSpace, new Point2d(800, 800), new Point2d(7200, 6400), QtoXDataHelper.ScopeKindSystem, "資訊", "資訊系統測試框");
                AddScope(database, tr, modelSpace, new Point2d(8800, 800), new Point2d(13800, 6400), QtoXDataHelper.ScopeKindSystem, "CCTV", "監視系統測試框");

                Line background = new Line(new Point3d(0, 7500, 0), new Point3d(15000, 7500, 0));
                modelSpace.AppendEntity(background);
                tr.AddNewlyCreatedDBObject(background, true);
                tr.Commit();
            }

            string output = Environment.GetEnvironmentVariable("QTO_FIXTURE_OUTPUT");
            if (!string.IsNullOrWhiteSpace(output))
            {
                string directory = Path.GetDirectoryName(output);
                if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory)) Directory.CreateDirectory(directory);
                database.SaveAs(output, DwgVersion.Current);
                document.Editor.WriteMessage("\nQTO validation fixture saved: " + output);
            }
            else
            {
                document.Editor.WriteMessage("\nQTO validation fixture created in the current drawing.");
            }
        }

        [CommandMethod("QTO_VALIDATE_VALIDATION_FIXTURE")]
        public void ValidateValidationFixture()
        {
            Document document = Application.DocumentManager.MdiActiveDocument;
            if (document == null) return;
            CreateValidationFixture();
            QtoScopeApplyResult scopeResult = QtoScopeService.ApplyAllScopes(document.Database, false);
            QtoScopeDefinition selectedFloorScope = QtoScopeService.GetScopes(document.Database)
                .FirstOrDefault(scope => string.Equals(scope.Kind, QtoXDataHelper.ScopeKindFloor, StringComparison.OrdinalIgnoreCase));
            if (selectedFloorScope != null)
            {
                QtoScopeService.ApplySelectedScopes(document.Database, new List<ObjectId> { selectedFloorScope.ObjectId }, false);
            }
            List<QtoSyncRow> rows = QtoSyncCommandService.BuildCurrentDrawingRows();
            int informationCount = rows.Count(r => string.Equals(r.SystemCode, "資訊", StringComparison.OrdinalIgnoreCase));
            int cctvCount = rows.Count(r => string.Equals(r.SystemCode, "CCTV", StringComparison.OrdinalIgnoreCase));
            int floor1Count = rows.Count(r => string.Equals(r.Floor, "1F", StringComparison.OrdinalIgnoreCase));
            int floor2Count = rows.Count(r => string.Equals(r.Floor, "2F", StringComparison.OrdinalIgnoreCase));
            bool selectedScopePreserved = floor1Count == 12 && floor2Count == 8;
            List<ObjectId> systemScopeIds = QtoScopeService.GetScopes(document.Database)
                .Where(scope => string.Equals(scope.Kind, QtoXDataHelper.ScopeKindSystem, StringComparison.OrdinalIgnoreCase))
                .Select(scope => scope.ObjectId)
                .ToList();
            QtoScopeService.ClearScopes(document.Database, systemScopeIds);
            RestoreFixtureSystems(document.Database);
            QtoScopeService.ApplyAllScopes(document.Database, false);
            List<QtoSyncRow> floorOnlyRows = QtoSyncCommandService.BuildCurrentDrawingRows();
            bool scopeKindsIndependent = floorOnlyRows.Count(r => string.Equals(r.SystemCode, "資訊", StringComparison.OrdinalIgnoreCase)) == 12
                && floorOnlyRows.Count(r => string.Equals(r.SystemCode, "CCTV", StringComparison.OrdinalIgnoreCase)) == 8;
            bool blockUpdatePreserved = ValidateBlockUpdate(document.Database);
            bool drawingToolsWriteQto = ValidateDrawingTools(document.Database);
            bool reviewSelectedRepair = ValidateSelectedReviewRepair(document.Database);
            bool reviewRelationshipChecks = ValidateReviewRelationshipChecks();
            bool budgetStoreRoundTrip = ValidateBudgetStoreRoundTrip(document.Database);
            int largeFixtureCount;
            long largeRowsMilliseconds;
            long largeReviewMilliseconds;
            bool largePerformance = ValidateLargeDrawingPerformance(document.Database, out largeFixtureCount, out largeRowsMilliseconds, out largeReviewMilliseconds);
            bool passed = rows.Count == 20 && informationCount == 12 && cctvCount == 8 && selectedScopePreserved && scopeKindsIndependent && blockUpdatePreserved && drawingToolsWriteQto && reviewSelectedRepair && reviewRelationshipChecks && budgetStoreRoundTrip && largePerformance && scopeResult.ErrorCount == 0;
            StringBuilder report = new StringBuilder();
            report.AppendLine("Status=" + (passed ? "PASS" : "FAIL"));
            report.AppendLine("QtoRows=" + rows.Count);
            report.AppendLine("InformationRows=" + informationCount);
            report.AppendLine("CctvRows=" + cctvCount);
            report.AppendLine("Floor1Rows=" + floor1Count);
            report.AppendLine("Floor2Rows=" + floor2Count);
            report.AppendLine("ScopeUpdated=" + scopeResult.UpdatedCount);
            report.AppendLine("ScopeErrors=" + scopeResult.ErrorCount);
            report.AppendLine("SelectedScopePreserved=" + selectedScopePreserved);
            report.AppendLine("ScopeKindsIndependent=" + scopeKindsIndependent);
            report.AppendLine("BlockUpdatePreserved=" + blockUpdatePreserved);
            report.AppendLine("DrawingToolsWriteQto=" + drawingToolsWriteQto);
            report.AppendLine("ReviewSelectedRepair=" + reviewSelectedRepair);
            report.AppendLine("ReviewRelationshipChecks=" + reviewRelationshipChecks);
            report.AppendLine("BudgetStoreRoundTrip=" + budgetStoreRoundTrip);
            report.AppendLine("LargeFixtureCount=" + largeFixtureCount);
            report.AppendLine("LargeRowsMs=" + largeRowsMilliseconds);
            report.AppendLine("LargeReviewMs=" + largeReviewMilliseconds);
            report.AppendLine("LargePerformance=" + largePerformance);
            string output = Environment.GetEnvironmentVariable("QTO_FIXTURE_REPORT");
            if (!string.IsNullOrWhiteSpace(output)) File.WriteAllText(output, report.ToString(), new UTF8Encoding(false));
            document.Editor.WriteMessage("\n" + report.ToString());
            if (!passed) throw new InvalidOperationException("QTO validation fixture result did not match the expected counts.");
        }

        private static bool ValidateBudgetStoreRoundTrip(Database database)
        {
            QtoBudgetProjectData project = new QtoBudgetProjectData { SchemaVersion = 1 };
            project.MappingRules.Add(new QtoBudgetMappingRule { RuleId = "EMPTY-DATE-RULE", Status = "candidate" });
            project.CompanyBindings.Add(new QtoProjectBudgetBinding { CompanyBudgetItemId = "COMPANY-1", ProjectBudgetItemId = "PROJECT-1", Status = "candidate" });
            QtoBudgetProjectStore.Save(database, project);
            QtoBudgetProjectData loaded = QtoBudgetProjectStore.Load(database);
            return loaded.ImportedAt > DateTime.MinValue
                && loaded.MappingRules.Count == 1
                && loaded.MappingRules[0].UpdatedAt > DateTime.MinValue
                && loaded.CompanyBindings.Count == 1
                && loaded.CompanyBindings[0].UpdatedAt > DateTime.MinValue;
        }

        private static bool ValidateReviewRelationshipChecks()
        {
            QtoCatalogSnapshot catalog = new QtoCatalogSnapshot { IsLoaded = true };
            List<QtoValidationTarget> normal = new List<QtoValidationTarget>
            {
                new QtoValidationTarget { ObjectHandle = "R1", QtoType = QtoXDataHelper.TypeOutlet, SyncId = "S1", SystemCode = "資訊", EquipmentTypeCode = "DATA_OUTLET", QuantityBasis = "point_count", OutletId = "O-100", JunctionBoxId = "JB-100" },
                new QtoValidationTarget { ObjectHandle = "R2", QtoType = QtoXDataHelper.TypeJunctionBox, SyncId = "S2", SystemCode = "資訊", EquipmentTypeCode = "DATA_JB", QuantityBasis = "point_count", JunctionBoxId = "JB-100" },
                new QtoValidationTarget { ObjectHandle = "R3", QtoType = QtoXDataHelper.TypeWire, SyncId = "S3", SystemCode = "資訊", QuantityBasis = "length", CableType = "Cat6", LengthM = 12.0, OutletId = "O-100", JunctionBoxId = "JB-100", RouteId = "ROUTE-100" }
            };
            QtoValidationResult normalResult = QtoValidateService.ValidateTargets(normal, catalog);

            QtoValidationResult directWireResult = QtoValidateService.ValidateTargets(new[]
            {
                new QtoValidationTarget { ObjectHandle = "R4", QtoType = QtoXDataHelper.TypeWire, SyncId = "S4", SystemCode = "CCTV", QuantityBasis = "length", CableType = "Cat6", LengthM = 8.0 }
            }, catalog);

            QtoValidationResult missingCableResult = QtoValidateService.ValidateTargets(new[]
            {
                new QtoValidationTarget { ObjectHandle = "R5", QtoType = QtoXDataHelper.TypeWire, SyncId = "S5", SystemCode = "CCTV", QuantityBasis = "length", LengthM = 8.0 }
            }, catalog);

            QtoValidationResult duplicateOutletResult = QtoValidateService.ValidateTargets(new[]
            {
                new QtoValidationTarget { ObjectHandle = "R6", QtoType = QtoXDataHelper.TypeOutlet, SyncId = "S6", SystemCode = "資訊", EquipmentTypeCode = "DATA_OUTLET", QuantityBasis = "point_count", OutletId = "O-200", JunctionBoxId = "JB-200" },
                new QtoValidationTarget { ObjectHandle = "R7", QtoType = QtoXDataHelper.TypeOutlet, SyncId = "S7", SystemCode = "資訊", EquipmentTypeCode = "DATA_OUTLET", QuantityBasis = "point_count", OutletId = "O-200", JunctionBoxId = "JB-200" }
            }, catalog);

            return normalResult.ReviewItems.Count == 0
                && directWireResult.ReviewItems.Count == 0
                && missingCableResult.ReviewItems.Any(item => string.Equals(item.IssueType, QtoReviewIssueType.MissingCableType, StringComparison.OrdinalIgnoreCase))
                && !missingCableResult.ReviewItems.Any(item => string.Equals(item.IssueType, QtoReviewIssueType.MissingEquipmentTypeCode, StringComparison.OrdinalIgnoreCase))
                && duplicateOutletResult.ReviewItems.Any(item => string.Equals(item.IssueType, QtoReviewIssueType.DuplicateOutletId, StringComparison.OrdinalIgnoreCase));
        }

        private static bool ValidateLargeDrawingPerformance(Database database, out int fixtureCount, out long rowsMilliseconds, out long reviewMilliseconds)
        {
            const int targetCount = 2000;
            const long maximumStageMilliseconds = 15000;
            List<ObjectId> temporaryIds = new List<ObjectId>(targetCount);
            fixtureCount = 0;
            rowsMilliseconds = long.MaxValue;
            reviewMilliseconds = long.MaxValue;
            int baselineRows = QtoSyncCommandService.BuildCurrentDrawingRows().Count;

            try
            {
                using (Transaction tr = database.TransactionManager.StartTransaction())
                {
                    BlockTable table = (BlockTable)tr.GetObject(database.BlockTableId, OpenMode.ForRead);
                    BlockTableRecord model = (BlockTableRecord)tr.GetObject(table[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                    for (int index = 0; index < targetCount; index++)
                    {
                        double x = 30000.0 + (index % 100) * 120.0;
                        double y = 30000.0 + (index / 100) * 120.0;
                        Line line = new Line(new Point3d(x, y, 0), new Point3d(x + 100.0, y, 0));
                        ObjectId objectId = model.AppendEntity(line);
                        tr.AddNewlyCreatedDBObject(line, true);
                        temporaryIds.Add(objectId);
                        QtoXDataHelper.SetXData(line, database, tr, new Dictionary<string, string>
                        {
                            { QtoXDataHelper.KeyQtoType, QtoXDataHelper.TypeWire },
                            { QtoXDataHelper.KeySystemCode, "資訊" },
                            { QtoXDataHelper.KeyCableType, "Cat6" },
                            { QtoXDataHelper.KeyQuantityBasis, "length" },
                            { QtoXDataHelper.KeyQtoUnit, "m" },
                            { QtoXDataHelper.KeyQtoSyncId, "LARGE-" + index.ToString("D5") }
                        });
                    }
                    tr.Commit();
                }

                fixtureCount = temporaryIds.Count;
                System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
                List<QtoSyncRow> rows = QtoSyncCommandService.BuildCurrentDrawingRows();
                stopwatch.Stop();
                rowsMilliseconds = stopwatch.ElapsedMilliseconds;

                QtoValidationResult validation;
                stopwatch.Restart();
                using (Transaction tr = database.TransactionManager.StartOpenCloseTransaction())
                {
                    validation = QtoValidateService.ValidateDrawing(tr, database, new QtoCatalogSnapshot { IsLoaded = true });
                }
                stopwatch.Stop();
                reviewMilliseconds = stopwatch.ElapsedMilliseconds;

                return fixtureCount == targetCount
                    && rows.Count >= baselineRows + targetCount
                    && validation.ScannedObjectCount >= baselineRows + targetCount
                    && rowsMilliseconds <= maximumStageMilliseconds
                    && reviewMilliseconds <= maximumStageMilliseconds;
            }
            finally
            {
                using (Transaction tr = database.TransactionManager.StartTransaction())
                {
                    foreach (ObjectId objectId in temporaryIds)
                    {
                        if (objectId.IsNull || objectId.IsErased)
                        {
                            continue;
                        }

                        Entity entity = tr.GetObject(objectId, OpenMode.ForWrite, false) as Entity;
                        if (entity != null && !entity.IsErased)
                        {
                            entity.Erase();
                        }
                    }
                    tr.Commit();
                }
            }
        }

        private static bool ValidateDrawingTools(Database database)
        {
            ObjectId lineId;
            ObjectId blockId;
            using (Transaction tr = database.TransactionManager.StartTransaction())
            {
                BlockTable table = (BlockTable)tr.GetObject(database.BlockTableId, OpenMode.ForRead);
                BlockTableRecord model = (BlockTableRecord)tr.GetObject(table[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                Line line = new Line(new Point3d(16000, 1000, 0), new Point3d(18000, 1000, 0));
                lineId = model.AppendEntity(line);
                tr.AddNewlyCreatedDBObject(line, true);
                BlockReference block = new BlockReference(new Point3d(16000, 2500, 0), table["QTO_FIXTURE_CAMERA"]);
                blockId = model.AppendEntity(block);
                tr.AddNewlyCreatedDBObject(block, true);
                tr.Commit();
            }

            QtoDrawingConversionResult lineResult = QtoDrawingService.ConvertLegacyObjects(database, new[] { lineId }, new QtoDrawingSettings
            {
                ObjectKind = "配線",
                SystemCode = "視聽音響",
                MaterialType = "TEST_CABLE"
            });
            QtoDrawingConversionResult blockResult = QtoDrawingService.ConvertLegacyObjects(database, new[] { blockId }, new QtoDrawingSettings
            {
                ObjectKind = "設備",
                SystemCode = "視聽音響",
                EquipmentTypeCode = "TEST_DEVICE"
            });
            QtoDrawingService.CreatePath(database, new[] { new Point2d(16000, 4000), new Point2d(17500, 4000), new Point2d(18000, 5000) }, new QtoDrawingSettings
            {
                ObjectKind = "管段",
                SystemCode = "視聽音響",
                MaterialType = "TEST_CONDUIT"
            });
            List<QtoSyncRow> rows = QtoSyncCommandService.BuildCurrentDrawingRows();
            return lineResult.ConvertedCount == 1
                && blockResult.ConvertedCount == 1
                && rows.Count == 23
                && rows.Any(row => string.Equals(row.CableType, "TEST_CABLE", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(row.Layer, QtoLayerHelper.WireLayerName, StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(row.SyncId))
                && rows.Any(row => string.Equals(row.ConduitType, "TEST_CONDUIT", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(row.Layer, QtoLayerHelper.ConduitLayerName, StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(row.SyncId))
                && rows.Any(row => string.Equals(row.EquipmentTypeCode, "TEST_DEVICE", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(row.SyncId));
        }

        private static bool ValidateSelectedReviewRepair(Database database)
        {
            const string duplicateSyncId = "review_duplicate_fixture";
            ObjectId preservedId = ObjectId.Null;
            ObjectId selectedDuplicateId = ObjectId.Null;
            ObjectId selectedMissingId = ObjectId.Null;
            try
            {
                using (Transaction tr = database.TransactionManager.StartTransaction())
                {
                    BlockTable table = (BlockTable)tr.GetObject(database.BlockTableId, OpenMode.ForRead);
                    BlockTableRecord model = (BlockTableRecord)tr.GetObject(table[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                    preservedId = AddRepairFixtureDevice(database, tr, model, table["QTO_FIXTURE_OUTLET"], new Point3d(20000, 1000, 0), duplicateSyncId);
                    selectedDuplicateId = AddRepairFixtureDevice(database, tr, model, table["QTO_FIXTURE_OUTLET"], new Point3d(21000, 1000, 0), duplicateSyncId);
                    selectedMissingId = AddRepairFixtureDevice(database, tr, model, table["QTO_FIXTURE_OUTLET"], new Point3d(22000, 1000, 0), string.Empty);
                    tr.Commit();
                }

                QtoRepairResult repairResult;
                using (Transaction tr = database.TransactionManager.StartTransaction())
                {
                    repairResult = QtoRepairService.RepairSelected(tr, database, new[]
                    {
                        selectedDuplicateId.Handle.ToString(),
                        selectedMissingId.Handle.ToString()
                    });
                    tr.Commit();
                }

                using (Transaction tr = database.TransactionManager.StartOpenCloseTransaction())
                {
                    string preservedSyncId = GetSyncId(tr, preservedId);
                    string selectedDuplicateSyncId = GetSyncId(tr, selectedDuplicateId);
                    string selectedMissingSyncId = GetSyncId(tr, selectedMissingId);
                    return repairResult.RepairedCount == 2
                        && string.Equals(preservedSyncId, duplicateSyncId, StringComparison.OrdinalIgnoreCase)
                        && !string.IsNullOrWhiteSpace(selectedDuplicateSyncId)
                        && !string.Equals(selectedDuplicateSyncId, duplicateSyncId, StringComparison.OrdinalIgnoreCase)
                        && !string.IsNullOrWhiteSpace(selectedMissingSyncId);
                }
            }
            finally
            {
                using (Transaction tr = database.TransactionManager.StartTransaction())
                {
                    foreach (ObjectId objectId in new[] { preservedId, selectedDuplicateId, selectedMissingId })
                    {
                        if (objectId.IsNull || objectId.IsErased) continue;
                        Entity entity = tr.GetObject(objectId, OpenMode.ForWrite, false) as Entity;
                        if (entity != null && !entity.IsErased) entity.Erase();
                    }
                    tr.Commit();
                }
            }
        }

        private static ObjectId AddRepairFixtureDevice(Database database, Transaction tr, BlockTableRecord model, ObjectId definitionId, Point3d point, string syncId)
        {
            BlockReference block = new BlockReference(point, definitionId);
            ObjectId objectId = model.AppendEntity(block);
            tr.AddNewlyCreatedDBObject(block, true);
            Dictionary<string, string> data = new Dictionary<string, string>
            {
                { QtoXDataHelper.KeyQtoType, QtoXDataHelper.TypeOutlet },
                { QtoXDataHelper.KeySystemCode, "資訊" },
                { QtoXDataHelper.KeyEquipmentTypeCode, "REVIEW_REPAIR_FIXTURE" },
                { QtoXDataHelper.KeyQuantityBasis, "point_count" },
                { QtoXDataHelper.KeyQtoUnit, "point" }
            };
            if (!string.IsNullOrWhiteSpace(syncId)) data[QtoXDataHelper.KeyQtoSyncId] = syncId;
            QtoXDataHelper.SetXData(block, database, tr, data);
            return objectId;
        }

        private static string GetSyncId(Transaction tr, ObjectId objectId)
        {
            Entity entity = tr.GetObject(objectId, OpenMode.ForRead, false) as Entity;
            if (entity == null) return string.Empty;
            Dictionary<string, string> data = QtoXDataHelper.GetXData(entity);
            string syncId;
            return data.TryGetValue(QtoXDataHelper.KeyQtoSyncId, out syncId) ? syncId ?? string.Empty : string.Empty;
        }

        private static bool ValidateBlockUpdate(Database database)
        {
            const string blockName = "QTO_FIXTURE_OUTLET";
            List<string> beforeIds = QtoSyncCommandService.BuildCurrentDrawingRows()
                .Where(row => string.Equals(row.BlockName, blockName, StringComparison.OrdinalIgnoreCase))
                .Select(row => row.SyncId ?? string.Empty)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .ToList();
            string sourcePath = Path.Combine(Path.GetTempPath(), "qto_block_update_fixture_" + Guid.NewGuid().ToString("N") + ".dwg");
            try
            {
                CreateBlockUpdateSource(sourcePath, blockName);
                QtoBlockUpdateResult result = new QtoBlockUpdateService().Apply(database, new[]
                {
                    new QtoBlockUpdateCandidate
                    {
                        Selected = true,
                        BlockName = blockName,
                        SourceDwg = sourcePath,
                        Status = QtoBlockUpdateStatus.UpdateAvailable
                    }
                });
                List<QtoSyncRow> afterRows = QtoSyncCommandService.BuildCurrentDrawingRows();
                List<string> afterIds = afterRows
                    .Where(row => string.Equals(row.BlockName, blockName, StringComparison.OrdinalIgnoreCase))
                    .Select(row => row.SyncId ?? string.Empty)
                    .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                return result.UpdatedDefinitionCount == 1
                    && result.UpdatedReferenceCount == 10
                    && afterRows.Count == 20
                    && beforeIds.SequenceEqual(afterIds, StringComparer.OrdinalIgnoreCase)
                    && BlockHasAttribute(database, blockName, "測試屬性");
            }
            finally
            {
                try { if (File.Exists(sourcePath)) File.Delete(sourcePath); } catch { }
            }
        }

        private static void CreateBlockUpdateSource(string path, string blockName)
        {
            using (Database source = new Database(true, true))
            {
                using (Transaction tr = source.TransactionManager.StartTransaction())
                {
                    BlockTable table = (BlockTable)tr.GetObject(source.BlockTableId, OpenMode.ForWrite);
                    BlockTableRecord definition = new BlockTableRecord { Name = blockName, Origin = Point3d.Origin };
                    table.Add(definition);
                    tr.AddNewlyCreatedDBObject(definition, true);
                    Circle circle = new Circle(Point3d.Origin, Vector3d.ZAxis, 320);
                    definition.AppendEntity(circle);
                    tr.AddNewlyCreatedDBObject(circle, true);
                    AttributeDefinition attribute = new AttributeDefinition
                    {
                        Tag = "測試屬性",
                        Prompt = "測試屬性",
                        TextString = "標準值",
                        Position = new Point3d(0, -420, 0),
                        Height = 120
                    };
                    definition.AppendEntity(attribute);
                    tr.AddNewlyCreatedDBObject(attribute, true);
                    tr.Commit();
                }
                source.SaveAs(path, DwgVersion.Current);
            }
        }

        private static bool BlockHasAttribute(Database database, string blockName, string tag)
        {
            using (Transaction tr = database.TransactionManager.StartOpenCloseTransaction())
            {
                BlockTable table = (BlockTable)tr.GetObject(database.BlockTableId, OpenMode.ForRead);
                if (!table.Has(blockName)) return false;
                BlockTableRecord definition = (BlockTableRecord)tr.GetObject(table[blockName], OpenMode.ForRead);
                return definition.Cast<ObjectId>()
                    .Select(id => tr.GetObject(id, OpenMode.ForRead, false) as AttributeDefinition)
                    .Any(attribute => attribute != null && string.Equals(attribute.Tag, tag, StringComparison.OrdinalIgnoreCase));
            }
        }

        private static void RestoreFixtureSystems(Database database)
        {
            using (Transaction tr = database.TransactionManager.StartTransaction())
            {
                BlockTable table = (BlockTable)tr.GetObject(database.BlockTableId, OpenMode.ForRead);
                BlockTableRecord modelSpace = (BlockTableRecord)tr.GetObject(table[BlockTableRecord.ModelSpace], OpenMode.ForRead);
                foreach (ObjectId objectId in modelSpace)
                {
                    Entity entity = tr.GetObject(objectId, OpenMode.ForRead, false) as Entity;
                    if (entity == null || QtoScopeService.IsScopeEntity(entity)) continue;
                    Dictionary<string, string> data = QtoXDataHelper.GetXData(entity);
                    string qtoType;
                    string floor;
                    if (!data.TryGetValue(QtoXDataHelper.KeyQtoType, out qtoType) || string.IsNullOrWhiteSpace(qtoType)) continue;
                    data.TryGetValue(QtoXDataHelper.KeyFloor, out floor);
                    data[QtoXDataHelper.KeySystemCode] = string.Equals(floor, "1F", StringComparison.OrdinalIgnoreCase) ? "資訊" : "CCTV";
                    entity.UpgradeOpen();
                    QtoXDataHelper.SetXData(entity, database, tr, data);
                }
                tr.Commit();
            }
        }

        private static ObjectId EnsureBlock(Database database, Transaction tr, BlockTable blockTable, string name, bool camera)
        {
            if (blockTable.Has(name)) return blockTable[name];
            blockTable.UpgradeOpen();
            BlockTableRecord definition = new BlockTableRecord { Name = name, Origin = Point3d.Origin };
            ObjectId id = blockTable.Add(definition);
            tr.AddNewlyCreatedDBObject(definition, true);
            Circle circle = new Circle(Point3d.Origin, Vector3d.ZAxis, 220);
            definition.AppendEntity(circle);
            tr.AddNewlyCreatedDBObject(circle, true);
            if (camera)
            {
                Line lens = new Line(new Point3d(220, 0, 0), new Point3d(520, 180, 0));
                definition.AppendEntity(lens);
                tr.AddNewlyCreatedDBObject(lens, true);
            }
            return id;
        }

        private static void AddDevice(Database database, Transaction tr, BlockTableRecord modelSpace, ObjectId definitionId, Point3d point, string system, string equipment, string floor, string area, string space)
        {
            BlockReference block = new BlockReference(point, definitionId);
            modelSpace.AppendEntity(block);
            tr.AddNewlyCreatedDBObject(block, true);
            QtoXDataHelper.SetXData(block, database, tr, new Dictionary<string, string>
            {
                { QtoXDataHelper.KeyQtoType, QtoXDataHelper.TypeOutlet },
                { QtoXDataHelper.KeySystemCode, system },
                { QtoXDataHelper.KeyEquipmentTypeCode, equipment },
                { QtoXDataHelper.KeyQuantityBasis, "point_count" },
                { QtoXDataHelper.KeyQtoUnit, "point" },
                { QtoXDataHelper.KeyFloor, floor },
                { QtoXDataHelper.KeyArea, area },
                { QtoXDataHelper.KeySpace, space },
                { QtoXDataHelper.KeyQtoSyncId, Guid.NewGuid().ToString("N") },
                { QtoXDataHelper.KeyMappingStatus, "needs_review" }
            });
        }

        private static void AddMeasuredPolyline(Database database, Transaction tr, BlockTableRecord modelSpace, string qtoType, string system, string cable, string conduit, Point2d start, Point2d end)
        {
            Polyline polyline = new Polyline();
            polyline.AddVertexAt(0, start, 0, 0, 0);
            polyline.AddVertexAt(1, end, 0, 0, 0);
            modelSpace.AppendEntity(polyline);
            tr.AddNewlyCreatedDBObject(polyline, true);
            QtoXDataHelper.SetXData(polyline, database, tr, new Dictionary<string, string>
            {
                { QtoXDataHelper.KeyQtoType, qtoType },
                { QtoXDataHelper.KeySystemCode, system },
                { QtoXDataHelper.KeyCableType, cable },
                { QtoXDataHelper.KeyConduitType, conduit },
                { QtoXDataHelper.KeyQuantityBasis, "length" },
                { QtoXDataHelper.KeyQtoUnit, "m" },
                { QtoXDataHelper.KeyQtoSyncId, Guid.NewGuid().ToString("N") },
                { QtoXDataHelper.KeyMappingStatus, "needs_review" }
            });
        }

        private static void AddScope(Database database, Transaction tr, BlockTableRecord modelSpace, Point2d min, Point2d max, string kind, string value, string name)
        {
            Polyline scope = new Polyline(4) { Closed = true };
            scope.AddVertexAt(0, min, 0, 0, 0);
            scope.AddVertexAt(1, new Point2d(max.X, min.Y), 0, 0, 0);
            scope.AddVertexAt(2, max, 0, 0, 0);
            scope.AddVertexAt(3, new Point2d(min.X, max.Y), 0, 0, 0);
            modelSpace.AppendEntity(scope);
            tr.AddNewlyCreatedDBObject(scope, true);
            QtoXDataHelper.SetXData(scope, database, tr, new Dictionary<string, string>
            {
                { QtoXDataHelper.KeyQtoScopeKind, kind },
                { QtoXDataHelper.KeyQtoScopeValue, value },
                { QtoXDataHelper.KeyQtoScopeName, name },
                { QtoXDataHelper.KeyQtoScopePriorityTicks, DateTime.UtcNow.Ticks.ToString() }
            });
        }
    }
}
