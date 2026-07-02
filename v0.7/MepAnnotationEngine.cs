using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace QtoWirePlugin
{
    public static class MepAnnotationEngine
    {
        public const double DefaultTextHeight = 250.0;

        public static DBText CreateTag(MepEquipmentItem item, Database db, Transaction tr, int index)
        {
            Point3d point = item.DimensionPoint + new Vector3d(300.0, 300.0 + (index % 3) * 120.0, 0.0);
            string text = item.Rule != null ? item.Rule.Label : "UNKNOWN " + item.BlockName;

            DBText tag = CreateText(point, text, db, tr);
            return tag;
        }

        public static DBText CreateOpeningNote(MepEquipmentItem item, Database db, Transaction tr, int index)
        {
            Point3d point = item.DimensionPoint + new Vector3d(300.0, -280.0 - (index % 3) * 120.0, 0.0);
            string text = item.Rule.OpeningSize;

            if (item.Rule.RequiresReview)
            {
                text += " / 需覆核";
            }

            return CreateText(point, text, db, tr);
        }

        public static DBText CreateUnknownText(string blockName, Point3d point, Database db, Transaction tr)
        {
            return CreateText(point + new Vector3d(300.0, 300.0, 0.0), "UNKNOWN: " + blockName, db, tr);
        }

        public static Dictionary<string, string> BuildXData(MepEquipmentItem item, string mepType)
        {
            Dictionary<string, string> data = new Dictionary<string, string>();
            data[MepXDataHelper.KeyMepType] = mepType;
            data[MepXDataHelper.KeyBlockName] = item.BlockName ?? string.Empty;
            data[MepXDataHelper.KeySourceObjectId] = item.SourceObjectId.Handle.ToString();

            if (item.Rule != null)
            {
                data[MepXDataHelper.KeyRuleId] = item.Rule.BlockName ?? string.Empty;
                data[MepXDataHelper.KeyCategory] = item.Rule.Category ?? string.Empty;
                data[MepXDataHelper.KeyReviewRequired] = item.Rule.RequiresReview ? "TRUE" : "FALSE";
            }
            else
            {
                data[MepXDataHelper.KeyRuleId] = "UNKNOWN";
                data[MepXDataHelper.KeyCategory] = "UNKNOWN";
                data[MepXDataHelper.KeyReviewRequired] = "TRUE";
            }

            return data;
        }

        private static DBText CreateText(Point3d position, string text, Database db, Transaction tr)
        {
            DBText dbText = new DBText();
            dbText.SetDatabaseDefaults(db);
            dbText.Position = position;
            dbText.Height = DefaultTextHeight;
            dbText.TextString = text ?? string.Empty;
            dbText.TextStyleId = MepLayerHelper.GetTextStyleId(db, tr);
            return dbText;
        }
    }
}
