using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Autodesk.AutoCAD.DatabaseServices;

namespace QtoWirePlugin
{
    public static class QtoSyncRowBuilder
    {
        public static QtoSyncRow BuildFromEntity(Entity entity, Transaction tr, string sourceDwg)
        {
            if (entity == null)
            {
                throw new ArgumentNullException("entity");
            }

            Dictionary<string, string> xdata = QtoXDataHelper.GetXData(entity);
            string rawXData = ToRawXData(xdata);

            QtoSyncRow row = new QtoSyncRow();
            row.SyncId = GetValue(xdata, QtoXDataHelper.KeyQtoSyncId);
            row.SourceDwg = sourceDwg ?? string.Empty;
            row.ObjectHandle = entity.Handle.ToString();
            row.CadMeasureType = GetValue(xdata, QtoXDataHelper.KeyQtoType);
            row.QtoNumber = GetValue(xdata, QtoXDataHelper.KeyQtoId);
            row.SystemCode = Coalesce(GetValue(xdata, QtoXDataHelper.KeySystemCode), GetValue(xdata, QtoXDataHelper.KeySystem));
            row.EquipmentTypeCode = GetValue(xdata, QtoXDataHelper.KeyEquipmentTypeCode);
            row.EquipmentTypeName = string.Empty;
            row.BlockName = GetBlockName(entity, tr);
            row.Layer = entity.Layer ?? string.Empty;
            row.Floor = GetValue(xdata, QtoXDataHelper.KeyFloor);
            row.Area = GetValue(xdata, QtoXDataHelper.KeyArea);
            row.Space = GetValue(xdata, QtoXDataHelper.KeySpace);
            row.CableType = GetValue(xdata, QtoXDataHelper.KeyCableType);
            row.ConduitType = GetValue(xdata, QtoXDataHelper.KeyConduitType);
            row.ConduitSize = GetValue(xdata, QtoXDataHelper.KeyConduitSize);
            row.QuantityBasis = GetQuantityBasis(entity, xdata);
            row.Quantity = GetQuantity(entity, xdata);
            row.Unit = GetUnit(entity, xdata);
            row.LengthM = GetLengthM(entity, xdata);
            row.SyncStatus = Coalesce(GetValue(xdata, QtoXDataHelper.KeyQtoSyncStatus), QtoSyncStatus.Active);
            row.ReviewReason = GetValue(xdata, QtoXDataHelper.KeyReviewReason);
            row.LastSyncAt = GetValue(xdata, QtoXDataHelper.KeyQtoLastSyncAt);
            row.LastCadModifiedAt = GetValue(xdata, QtoXDataHelper.KeyQtoLastModifiedAt);
            row.LastExcelModifiedAt = string.Empty;
            row.CatalogId = GetValue(xdata, QtoXDataHelper.KeyQtoSourceCatalogId);
            row.RawXData = rawXData;
            row.Fingerprint = BuildFingerprint(entity, row, rawXData);
            row.Remark = string.Empty;

            return row;
        }

        public static QtoSyncRow EnsureAndBuildFromEntity(Entity entity, Database db, Transaction tr, string sourceDwg)
        {
            QtoSyncIdService.EnsureEntitySyncId(entity, db, tr);
            return BuildFromEntity(entity, tr, sourceDwg);
        }

        public static string ToUtcTimestamp(DateTime utcDateTime)
        {
            return utcDateTime.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
        }

        private static string GetQuantityBasis(Entity entity, Dictionary<string, string> xdata)
        {
            string basis = GetValue(xdata, QtoXDataHelper.KeyQuantityBasis);
            if (!string.IsNullOrWhiteSpace(basis))
            {
                return basis;
            }

            if (entity is Polyline)
            {
                return "length";
            }

            return "count";
        }

        private static double GetQuantity(Entity entity, Dictionary<string, string> xdata)
        {
            string basis = GetQuantityBasis(entity, xdata);

            if (string.Equals(basis, "length", StringComparison.OrdinalIgnoreCase))
            {
                return GetLengthM(entity, xdata);
            }

            return 1.0;
        }

        private static string GetUnit(Entity entity, Dictionary<string, string> xdata)
        {
            string unit = GetValue(xdata, QtoXDataHelper.KeyQtoUnit);
            if (!string.IsNullOrWhiteSpace(unit))
            {
                return unit;
            }

            return entity is Polyline ? "m" : "ea";
        }

        private static double GetLengthM(Entity entity, Dictionary<string, string> xdata)
        {
            double lengthM;
            if (TryParseInvariant(GetValue(xdata, QtoXDataHelper.KeyLengthM), out lengthM))
            {
                return lengthM;
            }

            Polyline polyline = entity as Polyline;
            if (polyline != null)
            {
                return polyline.Length / 1000.0;
            }

            return 0.0;
        }

        private static string GetBlockName(Entity entity, Transaction tr)
        {
            BlockReference blockReference = entity as BlockReference;
            if (blockReference == null || tr == null)
            {
                return string.Empty;
            }

            ObjectId blockTableRecordId = blockReference.BlockTableRecord;

            if (blockReference.IsDynamicBlock)
            {
                blockTableRecordId = blockReference.DynamicBlockTableRecord;
            }

            BlockTableRecord blockTableRecord = tr.GetObject(blockTableRecordId, OpenMode.ForRead, false) as BlockTableRecord;
            return blockTableRecord == null ? string.Empty : blockTableRecord.Name;
        }

        private static string ToRawXData(Dictionary<string, string> xdata)
        {
            if (xdata == null || xdata.Count == 0)
            {
                return string.Empty;
            }

            List<string> keys = new List<string>(xdata.Keys);
            keys.Sort(StringComparer.OrdinalIgnoreCase);

            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < keys.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append(";");
                }

                string key = keys[i];
                builder.Append(key);
                builder.Append("=");
                builder.Append(xdata[key] ?? string.Empty);
            }

            return builder.ToString();
        }

        private static string BuildFingerprint(Entity entity, QtoSyncRow row, string rawXData)
        {
            string text = string.Join("|", new[]
            {
                row.SyncId ?? string.Empty,
                row.ObjectHandle ?? string.Empty,
                row.CadMeasureType ?? string.Empty,
                row.BlockName ?? string.Empty,
                row.Layer ?? string.Empty,
                row.Quantity.ToString("0.########", CultureInfo.InvariantCulture),
                row.LengthM.ToString("0.########", CultureInfo.InvariantCulture),
                rawXData ?? string.Empty
            });

            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(text));
                StringBuilder builder = new StringBuilder(hash.Length * 2);

                for (int i = 0; i < hash.Length; i++)
                {
                    builder.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
                }

                return builder.ToString();
            }
        }

        private static string GetValue(Dictionary<string, string> data, string key)
        {
            if (data == null || string.IsNullOrWhiteSpace(key))
            {
                return string.Empty;
            }

            string value;
            return data.TryGetValue(key, out value) ? value ?? string.Empty : string.Empty;
        }

        private static string Coalesce(string first, string second)
        {
            return string.IsNullOrWhiteSpace(first) ? second ?? string.Empty : first;
        }

        private static bool TryParseInvariant(string value, out double result)
        {
            return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
        }
    }
}
