using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace QtoWirePlugin
{
    public sealed class QtoDrawingConversionResult
    {
        public int ConvertedCount { get; set; }
        public int SkippedCount { get; set; }
    }

    public static class QtoDrawingService
    {
        public static ObjectId CreatePath(Database database, IEnumerable<Point2d> sourcePoints, QtoDrawingSettings settings)
        {
            if (database == null) throw new ArgumentNullException("database");
            if (settings == null) throw new ArgumentNullException("settings");
            List<Point2d> points = (sourcePoints ?? new List<Point2d>()).ToList();
            if (points.Count < 2) throw new InvalidOperationException("至少需要兩個點才能建立線管。");
            using (Transaction tr = database.TransactionManager.StartTransaction())
            {
                BlockTable table = (BlockTable)tr.GetObject(database.BlockTableId, OpenMode.ForRead);
                BlockTableRecord model = (BlockTableRecord)tr.GetObject(table[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                Polyline polyline = new Polyline(points.Count);
                for (int i = 0; i < points.Count; i++) polyline.AddVertexAt(i, points[i], 0, 0, 0);
                ObjectId id = model.AppendEntity(polyline);
                tr.AddNewlyCreatedDBObject(polyline, true);
                WritePathQto(polyline, database, tr, settings);
                tr.Commit();
                return id;
            }
        }

        public static QtoDrawingConversionResult ConvertLegacyObjects(Database database, IEnumerable<ObjectId> sourceIds, QtoDrawingSettings settings)
        {
            if (database == null) throw new ArgumentNullException("database");
            if (settings == null) throw new ArgumentNullException("settings");
            QtoDrawingConversionResult result = new QtoDrawingConversionResult();
            using (Transaction tr = database.TransactionManager.StartTransaction())
            {
                BlockTable table = (BlockTable)tr.GetObject(database.BlockTableId, OpenMode.ForRead);
                BlockTableRecord model = (BlockTableRecord)tr.GetObject(table[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                foreach (ObjectId objectId in sourceIds ?? new List<ObjectId>())
                {
                    Entity entity = tr.GetObject(objectId, OpenMode.ForWrite, false) as Entity;
                    if (entity == null || QtoSyncIdService.IsQtoEntity(entity) || entity.OwnerId != model.ObjectId)
                    {
                        result.SkippedCount++;
                        continue;
                    }

                    bool lineKind = settings.ObjectKind == "配線" || settings.ObjectKind == "管段";
                    if (lineKind)
                    {
                        Polyline polyline = entity as Polyline;
                        Line line = entity as Line;
                        if (polyline == null && line != null)
                        {
                            polyline = new Polyline(2);
                            polyline.AddVertexAt(0, new Point2d(line.StartPoint.X, line.StartPoint.Y), 0, 0, 0);
                            polyline.AddVertexAt(1, new Point2d(line.EndPoint.X, line.EndPoint.Y), 0, 0, 0);
                            polyline.SetPropertiesFrom(line);
                            model.AppendEntity(polyline);
                            tr.AddNewlyCreatedDBObject(polyline, true);
                            line.Erase();
                        }
                        if (polyline == null)
                        {
                            result.SkippedCount++;
                            continue;
                        }
                        WritePathQto(polyline, database, tr, settings);
                    }
                    else
                    {
                        BlockReference block = entity as BlockReference;
                        if (block == null)
                        {
                            result.SkippedCount++;
                            continue;
                        }
                        WriteDeviceQto(block, database, tr, settings);
                    }
                    result.ConvertedCount++;
                }
                tr.Commit();
            }
            return result;
        }

        private static void WritePathQto(Entity entity, Database database, Transaction tr, QtoDrawingSettings settings)
        {
            bool conduit = settings.ObjectKind == "管段";
            QtoLayerHelper.MoveEntityToLayer(
                entity,
                database,
                tr,
                conduit ? QtoLayerHelper.ConduitLayerName : QtoLayerHelper.WireLayerName);
            Dictionary<string, string> data = new Dictionary<string, string>
            {
                { QtoXDataHelper.KeyQtoType, conduit ? QtoXDataHelper.TypeConduitSegment : QtoXDataHelper.TypeWire },
                { QtoXDataHelper.KeySystemCode, settings.SystemCode },
                { QtoXDataHelper.KeyQuantityBasis, "length" },
                { QtoXDataHelper.KeyQtoUnit, "m" },
                { QtoXDataHelper.KeyQtoSyncId, Guid.NewGuid().ToString("N") },
                { QtoXDataHelper.KeyMappingStatus, "needs_review" }
            };
            data[conduit ? QtoXDataHelper.KeyConduitType : QtoXDataHelper.KeyCableType] = settings.MaterialType;
            QtoXDataHelper.SetXData(entity, database, tr, data);
        }

        private static void WriteDeviceQto(BlockReference block, Database database, Transaction tr, QtoDrawingSettings settings)
        {
            string qtoType = settings.ObjectKind == "出線口" ? QtoXDataHelper.TypeOutlet
                : settings.ObjectKind == "箱體" ? QtoXDataHelper.TypeJunctionBox
                : settings.ObjectKind == "盤箱" ? QtoXDataHelper.TypePanel : QtoXDataHelper.TypeDevice;
            QtoXDataHelper.SetXData(block, database, tr, new Dictionary<string, string>
            {
                { QtoXDataHelper.KeyQtoType, qtoType },
                { QtoXDataHelper.KeySystemCode, settings.SystemCode },
                { QtoXDataHelper.KeyEquipmentTypeCode, settings.EquipmentTypeCode },
                { QtoXDataHelper.KeyQuantityBasis, "point_count" },
                { QtoXDataHelper.KeyQtoUnit, "point" },
                { QtoXDataHelper.KeyQtoSyncId, Guid.NewGuid().ToString("N") },
                { QtoXDataHelper.KeyMappingStatus, "needs_review" }
            });
        }
    }
}
