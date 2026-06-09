using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;

namespace QtoWirePlugin
{
    public static class QtoXDataHelper
    {
        public const string RegAppName = "QTO_APP";

        public const string KeyQtoType = "QTO_TYPE";
        public const string KeyOutletId = "OUTLET_ID";
        public const string KeyJbId = "JB_ID";
        public const string KeySystem = "SYSTEM";
        public const string KeyCableType = "CABLE_TYPE";
        public const string KeyQtoError = "QTO_ERROR";
        public const string KeyQtoErrorMessage = "QTO_ERROR_MESSAGE";
        public const string KeyQtoOriginalLayer = "QTO_ORIGINAL_LAYER";
        public const string KeyRouteId = "ROUTE_ID";
        public const string KeyRouteMode = "ROUTE_MODE";
        public const string KeyRouteStatus = "ROUTE_STATUS";
        public const string KeyLengthM = "LENGTH_M";
        public const string KeyLengthSource = "LENGTH_SOURCE";
        public const string KeyTrayId = "TRAY_ID";
        public const string KeySystemScope = "SYSTEM_SCOPE";

        public const string TypeOutlet = "OUTLET";
        public const string TypeJunctionBox = "JUNCTION_BOX";
        public const string TypeWire = "WIRE";
        public const string TypeTray = "TRAY";
        public const string TypeCallout = "CALLOUT";
        public const string TypeOutletLabel = "OUTLET_LABEL";
        public const string TypeConduitSegment = "CONDUIT_SEGMENT";
        public const string TypeConduitVisual = "CONDUIT_VISUAL";

        public const string RouteModeLegacyManual = "LEGACY_MANUAL";
        public const string RouteModeTrayNetwork = "TRAY_NETWORK";
        public const string RouteStatusDraft = "DRAFT";
        public const string RouteStatusManualEdited = "MANUAL_EDITED";

        public static void RegisterAppName(Database db, Transaction tr, string appName)
        {
            if (db == null)
            {
                throw new ArgumentNullException("db");
            }

            if (tr == null)
            {
                throw new ArgumentNullException("tr");
            }

            if (string.IsNullOrWhiteSpace(appName))
            {
                throw new ArgumentException("RegAppName cannot be empty.", "appName");
            }

            RegAppTable regAppTable = (RegAppTable)tr.GetObject(db.RegAppTableId, OpenMode.ForRead);

            if (regAppTable.Has(appName))
            {
                return;
            }

            regAppTable.UpgradeOpen();

            RegAppTableRecord record = new RegAppTableRecord();
            record.Name = appName;

            regAppTable.Add(record);
            tr.AddNewlyCreatedDBObject(record, true);
        }

        public static void SetXData(Entity entity, Database db, Transaction tr, Dictionary<string, string> data)
        {
            if (entity == null)
            {
                throw new ArgumentNullException("entity");
            }

            if (data == null)
            {
                data = new Dictionary<string, string>();
            }

            RegisterAppName(db, tr, RegAppName);

            if (!entity.IsWriteEnabled)
            {
                entity.UpgradeOpen();
            }

            // XData starts with the registered app name, followed by key/value string pairs.
            List<TypedValue> values = new List<TypedValue>();
            values.Add(new TypedValue((int)DxfCode.ExtendedDataRegAppName, RegAppName));

            foreach (KeyValuePair<string, string> item in data)
            {
                if (string.IsNullOrWhiteSpace(item.Key))
                {
                    continue;
                }

                values.Add(new TypedValue((int)DxfCode.ExtendedDataAsciiString, item.Key));
                values.Add(new TypedValue((int)DxfCode.ExtendedDataAsciiString, item.Value ?? string.Empty));
            }

            entity.XData = new ResultBuffer(values.ToArray());
        }

        public static Dictionary<string, string> GetXData(Entity entity)
        {
            Dictionary<string, string> data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (entity == null)
            {
                return data;
            }

            ResultBuffer buffer = entity.GetXDataForApplication(RegAppName);

            if (buffer == null)
            {
                return data;
            }

            try
            {
                TypedValue[] values = buffer.AsArray();

                // Index 0 is the RegAppName. Remaining values are read as key/value pairs.
                for (int i = 1; i + 1 < values.Length; i += 2)
                {
                    string key = values[i].Value as string;
                    string value = values[i + 1].Value as string;

                    if (string.IsNullOrWhiteSpace(key))
                    {
                        continue;
                    }

                    data[key] = value ?? string.Empty;
                }
            }
            finally
            {
                buffer.Dispose();
            }

            return data;
        }

        public static string GetXDataValue(Entity entity, string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return null;
            }

            Dictionary<string, string> data = GetXData(entity);
            string value;

            if (data.TryGetValue(key, out value))
            {
                return value;
            }

            return null;
        }

        public static bool HasQtoType(Entity entity, string qtoType)
        {
            if (string.IsNullOrWhiteSpace(qtoType))
            {
                return false;
            }

            string value = GetXDataValue(entity, KeyQtoType);
            return string.Equals(value, qtoType, StringComparison.OrdinalIgnoreCase);
        }

        public static void UpdateXDataValue(Entity entity, Database db, Transaction tr, string key, string value)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("XData key cannot be empty.", "key");
            }

            Dictionary<string, string> data = GetXData(entity);
            data[key] = value ?? string.Empty;

            SetXData(entity, db, tr, data);
        }

        public static void RemoveXDataValues(Entity entity, Database db, Transaction tr, params string[] keys)
        {
            if (entity == null)
            {
                throw new ArgumentNullException("entity");
            }

            if (keys == null || keys.Length == 0)
            {
                return;
            }

            Dictionary<string, string> data = GetXData(entity);

            foreach (string key in keys)
            {
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                if (data.ContainsKey(key))
                {
                    data.Remove(key);
                }
            }

            SetXData(entity, db, tr, data);
        }
    }
}
