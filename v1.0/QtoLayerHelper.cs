using System;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;

namespace QtoWirePlugin
{
    public static class QtoLayerHelper
    {
        public const string WireLayerName = "QTO_WIRE";
        public const string CalloutLayerName = "QTO_CALLOUT";
        public const string OutletLabelLayerName = "QTO_OUTLET_LABEL";
        public const string ConduitLayerName = "QTO_CONDUIT";

        public static void EnsureLayerExists(Database db, Transaction tr, string layerName)
        {
            if (db == null)
            {
                throw new ArgumentNullException("db");
            }

            if (tr == null)
            {
                throw new ArgumentNullException("tr");
            }

            if (string.IsNullOrWhiteSpace(layerName))
            {
                throw new ArgumentException("Layer name cannot be empty.", "layerName");
            }

            LayerTable layerTable = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);

            if (layerTable.Has(layerName))
            {
                return;
            }

            layerTable.UpgradeOpen();

            // Phase 1 only ensures the layer exists. Color and linetype stay default.
            LayerTableRecord record = new LayerTableRecord();
            record.Name = layerName;

            layerTable.Add(record);
            tr.AddNewlyCreatedDBObject(record, true);
        }

        public static void MoveEntityToLayer(Entity entity, Database db, Transaction tr, string layerName)
        {
            if (entity == null)
            {
                throw new ArgumentNullException("entity");
            }

            EnsureLayerExists(db, tr, layerName);

            if (!entity.IsWriteEnabled)
            {
                entity.UpgradeOpen();
            }

            entity.Layer = layerName;
        }

        public static void EnsureLayerVisible(Database db, Transaction tr, string layerName)
        {
            if (db == null)
            {
                throw new ArgumentNullException("db");
            }

            if (tr == null)
            {
                throw new ArgumentNullException("tr");
            }

            if (string.IsNullOrWhiteSpace(layerName))
            {
                throw new ArgumentException("Layer name cannot be empty.", "layerName");
            }

            EnsureLayerExists(db, tr, layerName);

            LayerTable layerTable = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);

            if (!layerTable.Has(layerName))
            {
                return;
            }

            LayerTableRecord layer = (LayerTableRecord)tr.GetObject(layerTable[layerName], OpenMode.ForWrite);
            layer.IsOff = false;
            layer.IsFrozen = false;
            layer.IsLocked = false;

            if (string.Equals(layerName, ConduitLayerName, StringComparison.OrdinalIgnoreCase))
            {
                layer.Color = Color.FromColorIndex(ColorMethod.ByAci, 5);
            }
        }
    }
}
