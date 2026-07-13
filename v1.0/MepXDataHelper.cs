using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;

namespace QtoWirePlugin
{
    public static class MepXDataHelper
    {
        public const string RegAppName = "MEP_DIM_APP";

        public const string KeyMepType = "MEP_TYPE";
        public const string KeyBlockName = "MEP_BLOCK_NAME";
        public const string KeyRuleId = "MEP_RULE_ID";
        public const string KeyCategory = "MEP_CATEGORY";
        public const string KeyReviewRequired = "MEP_REVIEW_REQUIRED";
        public const string KeySourceObjectId = "MEP_SOURCE_OBJECT_ID";

        public const string TypeDimension = "DIMENSION";
        public const string TypeTag = "TAG";
        public const string TypeOpeningNote = "OPENING_NOTE";
        public const string TypeUnknown = "UNKNOWN";

        public static void SetXData(Entity entity, Database db, Transaction tr, Dictionary<string, string> data)
        {
            if (entity == null)
            {
                throw new ArgumentNullException("entity");
            }

            RegisterAppName(db, tr);

            if (!entity.IsWriteEnabled)
            {
                entity.UpgradeOpen();
            }

            List<TypedValue> values = new List<TypedValue>();
            values.Add(new TypedValue((int)DxfCode.ExtendedDataRegAppName, RegAppName));

            if (data != null)
            {
                foreach (KeyValuePair<string, string> item in data)
                {
                    if (string.IsNullOrWhiteSpace(item.Key))
                    {
                        continue;
                    }

                    values.Add(new TypedValue((int)DxfCode.ExtendedDataAsciiString, item.Key));
                    values.Add(new TypedValue((int)DxfCode.ExtendedDataAsciiString, item.Value ?? string.Empty));
                }
            }

            entity.XData = new ResultBuffer(values.ToArray());
        }

        private static void RegisterAppName(Database db, Transaction tr)
        {
            RegAppTable table = (RegAppTable)tr.GetObject(db.RegAppTableId, OpenMode.ForRead);

            if (table.Has(RegAppName))
            {
                return;
            }

            table.UpgradeOpen();
            RegAppTableRecord record = new RegAppTableRecord();
            record.Name = RegAppName;
            table.Add(record);
            tr.AddNewlyCreatedDBObject(record, true);
        }
    }
}
