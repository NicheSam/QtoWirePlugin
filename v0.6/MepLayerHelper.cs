using System;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;

namespace QtoWirePlugin
{
    public static class MepLayerHelper
    {
        public const string DimXLayerName = "MEP-DIM-X";
        public const string DimYLayerName = "MEP-DIM-Y";
        public const string TagLayerName = "MEP-TAG";
        public const string OpeningNoteLayerName = "MEP-OPENING-NOTE";
        public const string UnknownLayerName = "MEP-UNKNOWN";
        public const string HoldAreaLayerName = "MEP-HOLD-AREA";
        public const string AccessPanelLayerName = "MEP-ACCESS-PANEL";
        public const string ReferenceLayerName = "MEP-REFERENCE";

        public const string TextStyleName = "MEP-DIM-TEXT";
        public const string DimStyleName = "MEP-DIM";

        public static void EnsureMepEnvironment(Database db, Transaction tr)
        {
            EnsureLayer(db, tr, DimXLayerName, 3);
            EnsureLayer(db, tr, DimYLayerName, 4);
            EnsureLayer(db, tr, TagLayerName, 2);
            EnsureLayer(db, tr, OpeningNoteLayerName, 30);
            EnsureLayer(db, tr, UnknownLayerName, 1);
            EnsureLayer(db, tr, HoldAreaLayerName, 6);
            EnsureLayer(db, tr, AccessPanelLayerName, 5);
            EnsureLayer(db, tr, ReferenceLayerName, 8);
            EnsureTextStyle(db, tr);
            EnsureDimStyle(db, tr);
        }

        public static ObjectId GetTextStyleId(Database db, Transaction tr)
        {
            TextStyleTable table = (TextStyleTable)tr.GetObject(db.TextStyleTableId, OpenMode.ForRead);
            if (table.Has(TextStyleName))
            {
                return table[TextStyleName];
            }

            return db.Textstyle;
        }

        public static ObjectId GetDimStyleId(Database db, Transaction tr)
        {
            return GetDimStyleId(db, tr, null);
        }

        public static ObjectId GetDimStyleId(Database db, Transaction tr, string dimStyleName)
        {
            DimStyleTable table = (DimStyleTable)tr.GetObject(db.DimStyleTableId, OpenMode.ForRead);

            if (!string.IsNullOrWhiteSpace(dimStyleName) && table.Has(dimStyleName))
            {
                return table[dimStyleName];
            }

            if (table.Has(DimStyleName))
            {
                return table[DimStyleName];
            }

            return db.Dimstyle;
        }

        public static string GetCurrentDimStyleName(Database db, Transaction tr)
        {
            DimStyleTableRecord record = (DimStyleTableRecord)tr.GetObject(db.Dimstyle, OpenMode.ForRead);
            return record.Name;
        }

        public static void MoveEntityToLayer(Entity entity, Database db, Transaction tr, string layerName)
        {
            if (entity == null)
            {
                throw new ArgumentNullException("entity");
            }

            EnsureLayer(db, tr, layerName, 7);

            if (!entity.IsWriteEnabled)
            {
                entity.UpgradeOpen();
            }

            entity.Layer = layerName;
        }

        private static void EnsureLayer(Database db, Transaction tr, string layerName, short colorIndex)
        {
            LayerTable table = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);

            if (!table.Has(layerName))
            {
                table.UpgradeOpen();
                LayerTableRecord record = new LayerTableRecord();
                record.Name = layerName;
                record.Color = Color.FromColorIndex(ColorMethod.ByAci, colorIndex);
                table.Add(record);
                tr.AddNewlyCreatedDBObject(record, true);
            }

            LayerTableRecord layer = (LayerTableRecord)tr.GetObject(table[layerName], OpenMode.ForWrite);
            layer.IsOff = false;
            layer.IsFrozen = false;
            layer.IsLocked = false;
            layer.Color = Color.FromColorIndex(ColorMethod.ByAci, colorIndex);
        }

        private static void EnsureTextStyle(Database db, Transaction tr)
        {
            TextStyleTable table = (TextStyleTable)tr.GetObject(db.TextStyleTableId, OpenMode.ForRead);

            if (table.Has(TextStyleName))
            {
                return;
            }

            table.UpgradeOpen();
            TextStyleTableRecord current = (TextStyleTableRecord)tr.GetObject(db.Textstyle, OpenMode.ForRead);
            TextStyleTableRecord record = new TextStyleTableRecord();
            record.CopyFrom(current);
            record.Name = TextStyleName;
            table.Add(record);
            tr.AddNewlyCreatedDBObject(record, true);
        }

        private static void EnsureDimStyle(Database db, Transaction tr)
        {
            DimStyleTable table = (DimStyleTable)tr.GetObject(db.DimStyleTableId, OpenMode.ForRead);

            if (table.Has(DimStyleName))
            {
                return;
            }

            table.UpgradeOpen();
            DimStyleTableRecord current = (DimStyleTableRecord)tr.GetObject(db.Dimstyle, OpenMode.ForRead);
            DimStyleTableRecord record = new DimStyleTableRecord();
            record.CopyFrom(current);
            record.Name = DimStyleName;
            record.Dimtxt = 250.0;
            record.Dimasz = 180.0;
            table.Add(record);
            tr.AddNewlyCreatedDBObject(record, true);
        }
    }
}
