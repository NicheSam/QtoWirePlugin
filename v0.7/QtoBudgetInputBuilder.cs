using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;

namespace QtoWirePlugin
{
    public static class QtoBudgetInputBuilder
    {
        public static List<QtoBudgetInputRow> BuildRows(Transaction transaction, Database database, QtoDictionaryStore dictionary)
        {
            List<QtoBudgetInputRow> rows = new List<QtoBudgetInputRow>();
            BlockTable blockTable = (BlockTable)transaction.GetObject(database.BlockTableId, OpenMode.ForRead);
            BlockTableRecord modelSpace = (BlockTableRecord)transaction.GetObject(blockTable[BlockTableRecord.ModelSpace], OpenMode.ForRead);

            foreach (ObjectId objectId in modelSpace)
            {
                Entity entity = transaction.GetObject(objectId, OpenMode.ForRead, false) as Entity;
                if (entity == null)
                {
                    continue;
                }

                Dictionary<string, string> data = QtoXDataHelper.GetXData(entity);
                string qtoType = Get(data, QtoXDataHelper.KeyQtoType);
                if (!IsBudgetRelevantQtoType(qtoType))
                {
                    continue;
                }

                rows.Add(BuildRow(transaction, database, entity, data, dictionary));
            }

            return rows;
        }

        public static QtoBudgetInputRow BuildRow(Transaction transaction, Database database, Entity entity, Dictionary<string, string> data, QtoDictionaryStore dictionary)
        {
            if (data == null)
            {
                data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            string qtoType = Get(data, QtoXDataHelper.KeyQtoType);
            string equipmentCode = FirstNonEmpty(Get(data, QtoXDataHelper.KeyEquipmentTypeCode), InferEquipmentTypeCode(qtoType, entity, data, transaction));
            QtoEquipmentTypeDefinition equipment = dictionary == null ? null : dictionary.FindEquipment(equipmentCode);
            string systemCode = FirstNonEmpty(Get(data, QtoXDataHelper.KeySystemCode), equipment == null ? string.Empty : equipment.SystemCode, NormalizeSystemCode(Get(data, QtoXDataHelper.KeySystem)));
            string quantityBasis = FirstNonEmpty(Get(data, QtoXDataHelper.KeyQuantityBasis), equipment == null ? string.Empty : equipment.QuantityBasis, InferQuantityBasis(qtoType));
            double lengthM = GetLengthM(entity, data);

            QtoBudgetInputRow row = new QtoBudgetInputRow();
            row.SourceDwg = database == null ? string.Empty : database.Filename;
            row.ObjectHandle = entity.ObjectId.Handle.ToString();
            row.QtoType = qtoType;
            row.QtoId = FirstNonEmpty(
                Get(data, QtoXDataHelper.KeyQtoId),
                Get(data, QtoXDataHelper.KeyOutletId),
                Get(data, QtoXDataHelper.KeyJbId),
                Get(data, QtoXDataHelper.KeyRouteId),
                Get(data, QtoXDataHelper.KeyTrayId),
                entity.ObjectId.Handle.ToString());
            row.SystemCode = systemCode;
            row.EquipmentTypeCode = equipmentCode;
            row.EquipmentTypeName = equipment == null ? string.Empty : equipment.EquipmentTypeName;
            row.BlockName = GetBlockName(transaction, entity);
            row.Layer = entity.Layer ?? string.Empty;
            row.Floor = Get(data, QtoXDataHelper.KeyFloor);
            row.Area = Get(data, QtoXDataHelper.KeyArea);
            row.Space = Get(data, QtoXDataHelper.KeySpace);
            row.CableType = Get(data, QtoXDataHelper.KeyCableType);
            row.ConduitType = Get(data, QtoXDataHelper.KeyConduitType);
            row.ConduitSize = Get(data, QtoXDataHelper.KeyConduitSize);
            row.TraySize = Get(data, QtoXDataHelper.KeyTraySize);
            row.QuantityBasis = quantityBasis;
            row.QtoQuantity = GetQuantity(quantityBasis, lengthM);
            row.QtoUnit = FirstNonEmpty(Get(data, QtoXDataHelper.KeyQtoUnit), InferUnit(quantityBasis));
            row.LengthM = lengthM;
            row.MappingStatus = FirstNonEmpty(Get(data, QtoXDataHelper.KeyMappingStatus), equipment == null ? string.Empty : equipment.DefaultMappingStatus, "candidate");
            row.SourceRule = FirstNonEmpty(Get(data, QtoXDataHelper.KeySourceRule), "qto_v0_6_inference");
            row.ReviewReason = BuildReviewReason(row, data, dictionary);
            if (!string.IsNullOrWhiteSpace(row.ReviewReason) && string.Equals(row.MappingStatus, "candidate", StringComparison.OrdinalIgnoreCase))
            {
                row.MappingStatus = "needs_review";
            }

            return row;
        }

        public static List<string[]> ToCsvRows(List<QtoBudgetInputRow> rows)
        {
            return ToCsvRows(rows, QtoBudgetExportOptions.CreateDefault());
        }

        public static List<string[]> ToCsvRows(List<QtoBudgetInputRow> rows, QtoBudgetExportOptions options)
        {
            List<string[]> csv = new List<string[]>();
            if (options == null)
            {
                options = QtoBudgetExportOptions.CreateDefault();
            }

            List<string> headers = new List<string>();
            if (options.IncludeObjectIdentity)
            {
                headers.Add("來源DWG");
                headers.Add("物件識別碼");
                headers.Add("圖塊名稱");
                headers.Add("圖層");
            }

            if (options.IncludeCadQuantity)
            {
                headers.Add("CAD計量型態代碼");
                headers.Add("CAD計量型態");
                headers.Add("QTO編號");
                headers.Add("數量依據代碼");
                headers.Add("數量依據");
                headers.Add("QTO數量");
                headers.Add("單位代碼");
                headers.Add("單位");
                headers.Add("長度(m)");
            }

            if (options.IncludeSystemEquipment)
            {
                headers.Add("系統代碼");
                headers.Add("設備類型代碼");
                headers.Add("設備類型名稱");
            }

            if (options.IncludeLocationRouting)
            {
                headers.Add("樓層");
                headers.Add("區域");
                headers.Add("空間");
                headers.Add("線材類型");
                headers.Add("管線類型");
                headers.Add("管線尺寸");
                headers.Add("線槽尺寸");
            }

            if (options.IncludeReviewStatus)
            {
                headers.Add("對應狀態代碼");
                headers.Add("對應狀態");
                headers.Add("待確認原因");
                headers.Add("資料來源規則");
            }

            csv.Add(headers.ToArray());

            if (rows == null)
            {
                return csv;
            }

            foreach (QtoBudgetInputRow row in rows)
            {
                List<string> values = new List<string>();
                if (options.IncludeObjectIdentity)
                {
                    values.Add(row.SourceDwg ?? string.Empty);
                    values.Add(row.ObjectHandle ?? string.Empty);
                    values.Add(row.BlockName ?? string.Empty);
                    values.Add(row.Layer ?? string.Empty);
                }

                if (options.IncludeCadQuantity)
                {
                    values.Add(row.QtoType ?? string.Empty);
                    values.Add(ToChineseQtoType(row.QtoType));
                    values.Add(row.QtoId ?? string.Empty);
                    values.Add(row.QuantityBasis ?? string.Empty);
                    values.Add(ToChineseQuantityBasis(row.QuantityBasis));
                    values.Add(row.QtoQuantity.ToString("0.###"));
                    values.Add(row.QtoUnit ?? string.Empty);
                    values.Add(ToChineseUnit(row.QtoUnit));
                    values.Add(row.LengthM.ToString("0.###"));
                }

                if (options.IncludeSystemEquipment)
                {
                    values.Add(row.SystemCode ?? string.Empty);
                    values.Add(row.EquipmentTypeCode ?? string.Empty);
                    values.Add(row.EquipmentTypeName ?? string.Empty);
                }

                if (options.IncludeLocationRouting)
                {
                    values.Add(row.Floor ?? string.Empty);
                    values.Add(row.Area ?? string.Empty);
                    values.Add(row.Space ?? string.Empty);
                    values.Add(row.CableType ?? string.Empty);
                    values.Add(row.ConduitType ?? string.Empty);
                    values.Add(row.ConduitSize ?? string.Empty);
                    values.Add(row.TraySize ?? string.Empty);
                }

                if (options.IncludeReviewStatus)
                {
                    values.Add(row.MappingStatus ?? string.Empty);
                    values.Add(ToChineseMappingStatus(row.MappingStatus));
                    values.Add(row.ReviewReason ?? string.Empty);
                    values.Add(row.SourceRule ?? string.Empty);
                }

                csv.Add(values.ToArray());
            }

            return csv;
        }

        private static string ToChineseQtoType(string qtoType)
        {
            if (string.Equals(qtoType, QtoXDataHelper.TypeOutlet, StringComparison.OrdinalIgnoreCase))
            {
                return "出線口";
            }

            if (string.Equals(qtoType, QtoXDataHelper.TypeJunctionBox, StringComparison.OrdinalIgnoreCase))
            {
                return "箱體/接線箱";
            }

            if (string.Equals(qtoType, QtoXDataHelper.TypeWire, StringComparison.OrdinalIgnoreCase))
            {
                return "配線";
            }

            if (string.Equals(qtoType, QtoXDataHelper.TypeConduitSegment, StringComparison.OrdinalIgnoreCase))
            {
                return "管段";
            }

            if (string.Equals(qtoType, QtoXDataHelper.TypeTray, StringComparison.OrdinalIgnoreCase))
            {
                return "線槽";
            }

            if (string.Equals(qtoType, QtoXDataHelper.TypeDevice, StringComparison.OrdinalIgnoreCase))
            {
                return "設備";
            }

            if (string.Equals(qtoType, QtoXDataHelper.TypePanel, StringComparison.OrdinalIgnoreCase))
            {
                return "盤箱/設備盤";
            }

            return qtoType ?? string.Empty;
        }

        private static string ToChineseQuantityBasis(string quantityBasis)
        {
            if (string.Equals(quantityBasis, "point_count", StringComparison.OrdinalIgnoreCase))
            {
                return "點位數量";
            }

            if (string.Equals(quantityBasis, "device_count", StringComparison.OrdinalIgnoreCase))
            {
                return "設備數量";
            }

            if (string.Equals(quantityBasis, "wire_length_m", StringComparison.OrdinalIgnoreCase))
            {
                return "配線長度";
            }

            if (string.Equals(quantityBasis, "conduit_length_m", StringComparison.OrdinalIgnoreCase))
            {
                return "管段長度";
            }

            if (string.Equals(quantityBasis, "tray_length_m", StringComparison.OrdinalIgnoreCase))
            {
                return "線槽長度";
            }

            return quantityBasis ?? string.Empty;
        }

        private static string ToChineseUnit(string unit)
        {
            if (string.Equals(unit, "point", StringComparison.OrdinalIgnoreCase))
            {
                return "點";
            }

            if (string.Equals(unit, "set", StringComparison.OrdinalIgnoreCase))
            {
                return "式";
            }

            if (string.Equals(unit, "m", StringComparison.OrdinalIgnoreCase))
            {
                return "公尺";
            }

            return unit ?? string.Empty;
        }

        private static string ToChineseMappingStatus(string mappingStatus)
        {
            if (string.Equals(mappingStatus, "candidate", StringComparison.OrdinalIgnoreCase))
            {
                return "候選對應";
            }

            if (string.Equals(mappingStatus, "needs_review", StringComparison.OrdinalIgnoreCase))
            {
                return "需人工確認";
            }

            if (string.Equals(mappingStatus, "blocked", StringComparison.OrdinalIgnoreCase))
            {
                return "已阻擋";
            }

            if (string.Equals(mappingStatus, "confirmed", StringComparison.OrdinalIgnoreCase))
            {
                return "已確認";
            }

            return mappingStatus ?? string.Empty;
        }

        private static bool IsBudgetRelevantQtoType(string qtoType)
        {
            return string.Equals(qtoType, QtoXDataHelper.TypeOutlet, StringComparison.OrdinalIgnoreCase)
                || string.Equals(qtoType, QtoXDataHelper.TypeJunctionBox, StringComparison.OrdinalIgnoreCase)
                || string.Equals(qtoType, QtoXDataHelper.TypeWire, StringComparison.OrdinalIgnoreCase)
                || string.Equals(qtoType, QtoXDataHelper.TypeConduitSegment, StringComparison.OrdinalIgnoreCase)
                || string.Equals(qtoType, QtoXDataHelper.TypeTray, StringComparison.OrdinalIgnoreCase)
                || string.Equals(qtoType, QtoXDataHelper.TypeDevice, StringComparison.OrdinalIgnoreCase)
                || string.Equals(qtoType, QtoXDataHelper.TypePanel, StringComparison.OrdinalIgnoreCase);
        }

        private static string InferEquipmentTypeCode(string qtoType, Entity entity, Dictionary<string, string> data, Transaction transaction)
        {
            string text = (GetBlockName(transaction, entity) + " " + Get(data, QtoXDataHelper.KeySystem) + " " + Get(data, QtoXDataHelper.KeyCableType)).ToUpperInvariant();

            if (string.Equals(qtoType, QtoXDataHelper.TypeWire, StringComparison.OrdinalIgnoreCase))
            {
                return "ELV_CABLE";
            }

            if (string.Equals(qtoType, QtoXDataHelper.TypeConduitSegment, StringComparison.OrdinalIgnoreCase))
            {
                return "ELV_CONDUIT";
            }

            if (text.IndexOf("CCTV", StringComparison.OrdinalIgnoreCase) >= 0 || text.IndexOf("CAM", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "CCTV_CAMERA";
            }

            if (text.IndexOf("ACS", StringComparison.OrdinalIgnoreCase) >= 0 || text.IndexOf("CARD", StringComparison.OrdinalIgnoreCase) >= 0 || text.IndexOf("READER", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "ACS_CARD_READER";
            }

            if (text.IndexOf("PA", StringComparison.OrdinalIgnoreCase) >= 0 || text.IndexOf("SPEAKER", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "PA_SPEAKER";
            }

            return string.Empty;
        }

        private static string NormalizeSystemCode(string system)
        {
            if (string.IsNullOrWhiteSpace(system))
            {
                return string.Empty;
            }

            string value = system.Trim().ToUpperInvariant();
            if (value == "DATA")
            {
                return "LAN";
            }

            if (value == "CAM")
            {
                return "CCTV";
            }

            return value;
        }

        private static string InferQuantityBasis(string qtoType)
        {
            if (string.Equals(qtoType, QtoXDataHelper.TypeWire, StringComparison.OrdinalIgnoreCase))
            {
                return "wire_length_m";
            }

            if (string.Equals(qtoType, QtoXDataHelper.TypeConduitSegment, StringComparison.OrdinalIgnoreCase))
            {
                return "conduit_length_m";
            }

            if (string.Equals(qtoType, QtoXDataHelper.TypeTray, StringComparison.OrdinalIgnoreCase))
            {
                return "tray_length_m";
            }

            if (string.Equals(qtoType, QtoXDataHelper.TypeOutlet, StringComparison.OrdinalIgnoreCase))
            {
                return "point_count";
            }

            return "device_count";
        }

        private static string InferUnit(string quantityBasis)
        {
            if (string.Equals(quantityBasis, "wire_length_m", StringComparison.OrdinalIgnoreCase)
                || string.Equals(quantityBasis, "conduit_length_m", StringComparison.OrdinalIgnoreCase)
                || string.Equals(quantityBasis, "tray_length_m", StringComparison.OrdinalIgnoreCase))
            {
                return "m";
            }

            if (string.Equals(quantityBasis, "point_count", StringComparison.OrdinalIgnoreCase))
            {
                return "point";
            }

            return "set";
        }

        private static double GetQuantity(string quantityBasis, double lengthM)
        {
            if (string.Equals(quantityBasis, "wire_length_m", StringComparison.OrdinalIgnoreCase)
                || string.Equals(quantityBasis, "conduit_length_m", StringComparison.OrdinalIgnoreCase)
                || string.Equals(quantityBasis, "tray_length_m", StringComparison.OrdinalIgnoreCase))
            {
                return lengthM;
            }

            return 1.0;
        }

        private static double GetLengthM(Entity entity, Dictionary<string, string> data)
        {
            double value;
            if (double.TryParse(Get(data, QtoXDataHelper.KeyLengthM), out value))
            {
                return value;
            }

            Polyline polyline = entity as Polyline;
            if (polyline != null)
            {
                return QtoGeometryHelper.ConvertMmToM(QtoGeometryHelper.GetPolylineLength(polyline));
            }

            return 0.0;
        }

        private static string BuildReviewReason(QtoBudgetInputRow row, Dictionary<string, string> data, QtoDictionaryStore dictionary)
        {
            List<string> reasons = new List<string>();
            string existingReason = Get(data, QtoXDataHelper.KeyReviewReason);
            if (!string.IsNullOrWhiteSpace(existingReason))
            {
                reasons.Add(existingReason);
            }

            if (string.IsNullOrWhiteSpace(row.SystemCode))
            {
                reasons.Add("missing_system_code");
            }
            else if (dictionary != null && dictionary.SystemCodes.Count > 0 && !dictionary.SystemCodes.Contains(row.SystemCode))
            {
                reasons.Add("unknown_system_code");
            }

            if (string.IsNullOrWhiteSpace(row.EquipmentTypeCode))
            {
                reasons.Add("missing_equipment_type_code");
            }
            else if (dictionary != null && dictionary.EquipmentTypes.Count > 0 && dictionary.FindEquipment(row.EquipmentTypeCode) == null)
            {
                reasons.Add("unknown_equipment_type_code");
            }

            if (string.IsNullOrWhiteSpace(row.QuantityBasis))
            {
                reasons.Add("missing_quantity_basis");
            }

            if (string.Equals(row.QtoType, QtoXDataHelper.TypeOutlet, StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(Get(data, QtoXDataHelper.KeyJbId)))
                {
                    reasons.Add("missing_jb_id");
                }

                if (string.IsNullOrWhiteSpace(row.CableType))
                {
                    reasons.Add("missing_cable_type");
                }
            }

            if (string.Equals(row.QuantityBasis, "wire_length_m", StringComparison.OrdinalIgnoreCase) && row.LengthM <= 0.0)
            {
                reasons.Add("missing_length_m");
            }

            return string.Join("; ", reasons.ToArray());
        }

        private static string Get(Dictionary<string, string> data, string key)
        {
            string value;
            if (data != null && data.TryGetValue(key, out value))
            {
                return value ?? string.Empty;
            }

            return string.Empty;
        }

        private static string FirstNonEmpty(params string[] values)
        {
            if (values == null)
            {
                return string.Empty;
            }

            foreach (string value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value.Trim();
                }
            }

            return string.Empty;
        }

        private static string GetBlockName(Transaction transaction, Entity entity)
        {
            BlockReference blockReference = entity as BlockReference;
            if (blockReference == null)
            {
                return string.Empty;
            }

            ObjectId blockTableRecordId = blockReference.IsDynamicBlock ? blockReference.DynamicBlockTableRecord : blockReference.BlockTableRecord;
            BlockTableRecord record = transaction.GetObject(blockTableRecordId, OpenMode.ForRead, false) as BlockTableRecord;
            return record == null ? string.Empty : record.Name;
        }
    }
}
