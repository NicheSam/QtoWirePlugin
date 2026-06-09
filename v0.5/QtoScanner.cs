using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;

namespace QtoWirePlugin
{
    public static class QtoScanner
    {
        public static List<OutletInfo> GetAllQtoOutlets(Transaction tr, Database db)
        {
            List<OutletInfo> outlets = new List<OutletInfo>();

            BlockTableRecord modelSpace = GetModelSpace(tr, db);

            foreach (ObjectId objectId in modelSpace)
            {
                BlockReference blockReference = tr.GetObject(objectId, OpenMode.ForRead, false) as BlockReference;

                if (blockReference == null)
                {
                    continue;
                }

                if (!QtoXDataHelper.HasQtoType(blockReference, QtoXDataHelper.TypeOutlet))
                {
                    continue;
                }

                OutletInfo outlet = new OutletInfo();
                outlet.ObjectId = blockReference.ObjectId;
                outlet.OutletId = QtoXDataHelper.GetXDataValue(blockReference, QtoXDataHelper.KeyOutletId) ?? string.Empty;
                outlet.JbId = QtoXDataHelper.GetXDataValue(blockReference, QtoXDataHelper.KeyJbId) ?? string.Empty;
                outlet.System = QtoXDataHelper.GetXDataValue(blockReference, QtoXDataHelper.KeySystem) ?? string.Empty;
                outlet.CableType = QtoXDataHelper.GetXDataValue(blockReference, QtoXDataHelper.KeyCableType) ?? string.Empty;
                outlet.BlockName = GetBlockName(tr, blockReference);
                outlet.Position = blockReference.Position;

                outlets.Add(outlet);
            }

            return outlets;
        }

        public static List<JunctionBoxInfo> GetAllQtoJunctionBoxes(Transaction tr, Database db)
        {
            List<JunctionBoxInfo> junctionBoxes = new List<JunctionBoxInfo>();

            BlockTableRecord modelSpace = GetModelSpace(tr, db);

            foreach (ObjectId objectId in modelSpace)
            {
                BlockReference blockReference = tr.GetObject(objectId, OpenMode.ForRead, false) as BlockReference;

                if (blockReference == null)
                {
                    continue;
                }

                if (!QtoXDataHelper.HasQtoType(blockReference, QtoXDataHelper.TypeJunctionBox))
                {
                    continue;
                }

                JunctionBoxInfo junctionBox = new JunctionBoxInfo();
                junctionBox.ObjectId = blockReference.ObjectId;
                junctionBox.JbId = QtoXDataHelper.GetXDataValue(blockReference, QtoXDataHelper.KeyJbId) ?? string.Empty;
                junctionBox.System = QtoXDataHelper.GetXDataValue(blockReference, QtoXDataHelper.KeySystem) ?? string.Empty;
                junctionBox.BlockName = GetBlockName(tr, blockReference);
                junctionBox.Position = blockReference.Position;

                junctionBoxes.Add(junctionBox);
            }

            return junctionBoxes;
        }

        public static List<WireInfo> GetAllQtoWires(Transaction tr, Database db)
        {
            List<WireInfo> wires = new List<WireInfo>();

            BlockTableRecord modelSpace = GetModelSpace(tr, db);

            foreach (ObjectId objectId in modelSpace)
            {
                Polyline polyline = tr.GetObject(objectId, OpenMode.ForRead, false) as Polyline;

                if (polyline == null)
                {
                    continue;
                }

                if (!QtoXDataHelper.HasQtoType(polyline, QtoXDataHelper.TypeWire))
                {
                    continue;
                }

                double lengthMm = QtoGeometryHelper.GetPolylineLength(polyline);

                WireInfo wire = new WireInfo();
                wire.ObjectId = polyline.ObjectId;
                wire.RouteId = QtoXDataHelper.GetXDataValue(polyline, QtoXDataHelper.KeyRouteId) ?? string.Empty;
                wire.OutletId = QtoXDataHelper.GetXDataValue(polyline, QtoXDataHelper.KeyOutletId) ?? string.Empty;
                wire.JbId = QtoXDataHelper.GetXDataValue(polyline, QtoXDataHelper.KeyJbId) ?? string.Empty;
                wire.System = QtoXDataHelper.GetXDataValue(polyline, QtoXDataHelper.KeySystem) ?? string.Empty;
                wire.CableType = QtoXDataHelper.GetXDataValue(polyline, QtoXDataHelper.KeyCableType) ?? string.Empty;
                wire.LengthM = QtoGeometryHelper.ConvertMmToM(lengthMm);
                wire.LengthSource = QtoXDataHelper.GetXDataValue(polyline, QtoXDataHelper.KeyLengthSource) ?? string.Empty;
                wire.Layer = polyline.Layer ?? string.Empty;

                wires.Add(wire);
            }

            return wires;
        }

        public static List<TrayInfo> GetAllQtoTrays(Transaction tr, Database db)
        {
            List<TrayInfo> trays = new List<TrayInfo>();
            BlockTableRecord modelSpace = GetModelSpace(tr, db);

            foreach (ObjectId objectId in modelSpace)
            {
                Polyline polyline = tr.GetObject(objectId, OpenMode.ForRead, false) as Polyline;

                if (polyline == null)
                {
                    continue;
                }

                if (!QtoXDataHelper.HasQtoType(polyline, QtoXDataHelper.TypeTray))
                {
                    continue;
                }

                TrayInfo tray = new TrayInfo();
                tray.ObjectId = polyline.ObjectId;
                tray.TrayId = QtoXDataHelper.GetXDataValue(polyline, QtoXDataHelper.KeyTrayId) ?? string.Empty;
                tray.SystemScope = QtoXDataHelper.GetXDataValue(polyline, QtoXDataHelper.KeySystemScope) ?? string.Empty;
                tray.VertexCount = polyline.NumberOfVertices;
                tray.Layer = polyline.Layer ?? string.Empty;
                trays.Add(tray);
            }

            return trays;
        }

        private static BlockTableRecord GetModelSpace(Transaction tr, Database db)
        {
            BlockTable blockTable = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            return (BlockTableRecord)tr.GetObject(blockTable[BlockTableRecord.ModelSpace], OpenMode.ForRead);
        }

        private static string GetBlockName(Transaction tr, BlockReference blockReference)
        {
            ObjectId blockTableRecordId = blockReference.BlockTableRecord;

            if (blockReference.IsDynamicBlock)
            {
                blockTableRecordId = blockReference.DynamicBlockTableRecord;
            }

            BlockTableRecord blockTableRecord = tr.GetObject(blockTableRecordId, OpenMode.ForRead, false) as BlockTableRecord;

            if (blockTableRecord == null)
            {
                return string.Empty;
            }

            return blockTableRecord.Name;
        }
    }
}
