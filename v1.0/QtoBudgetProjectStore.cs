using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;
using Autodesk.AutoCAD.DatabaseServices;

namespace QtoWirePlugin
{
    public static class QtoBudgetProjectStore
    {
        private const string RecordKey = "QTO_BUDGET_PROJECT_V1";
        private const int ChunkSize = 240;

        public static QtoBudgetProjectData Load(Database database)
        {
            if (database == null)
            {
                return new QtoBudgetProjectData { SchemaVersion = 1 };
            }

            using (Transaction tr = database.TransactionManager.StartOpenCloseTransaction())
            {
                DBDictionary nod = tr.GetObject(database.NamedObjectsDictionaryId, OpenMode.ForRead, false) as DBDictionary;
                if (nod == null || !nod.Contains(RecordKey))
                {
                    return new QtoBudgetProjectData { SchemaVersion = 1 };
                }

                Xrecord record = tr.GetObject(nod.GetAt(RecordKey), OpenMode.ForRead, false) as Xrecord;
                StringBuilder json = new StringBuilder();
                if (record != null && record.Data != null)
                {
                    foreach (TypedValue value in record.Data)
                    {
                        json.Append(Convert.ToString(value.Value) ?? string.Empty);
                    }
                }
                return Deserialize(json.ToString());
            }
        }

        public static void Save(Database database, QtoBudgetProjectData data)
        {
            if (database == null) throw new ArgumentNullException("database");
            string json = Serialize(data ?? new QtoBudgetProjectData { SchemaVersion = 1 });
            ResultBuffer buffer = new ResultBuffer();
            for (int offset = 0; offset < json.Length; offset += ChunkSize)
            {
                buffer.Add(new TypedValue((int)DxfCode.Text, json.Substring(offset, Math.Min(ChunkSize, json.Length - offset))));
            }

            using (Transaction tr = database.TransactionManager.StartTransaction())
            {
                DBDictionary nod = tr.GetObject(database.NamedObjectsDictionaryId, OpenMode.ForRead, false) as DBDictionary;
                Xrecord record;
                if (nod.Contains(RecordKey))
                {
                    record = tr.GetObject(nod.GetAt(RecordKey), OpenMode.ForWrite, false) as Xrecord;
                }
                else
                {
                    nod.UpgradeOpen();
                    record = new Xrecord();
                    nod.SetAt(RecordKey, record);
                    tr.AddNewlyCreatedDBObject(record, true);
                }
                record.Data = buffer;
                tr.Commit();
            }
        }

        public static string GetCompanyRulesPath(Database database)
        {
            string catalogPath = QtoProjectCatalogContext.GetPreferredCatalogPath(database);
            string directory = string.IsNullOrWhiteSpace(catalogPath) ? string.Empty : Path.GetDirectoryName(catalogPath);
            return string.IsNullOrWhiteSpace(directory) ? string.Empty : Path.Combine(directory, "qto_budget_company_rules.json");
        }

        public static void PromoteRule(Database database, QtoBudgetMappingRule rule)
        {
            string path = GetCompanyRulesPath(database);
            if (string.IsNullOrWhiteSpace(path)) throw new InvalidOperationException("請先設定圖塊資料庫，才能保存公司規則。");
            QtoBudgetProjectData shared = File.Exists(path) ? Deserialize(File.ReadAllText(path, Encoding.UTF8)) : new QtoBudgetProjectData { SchemaVersion = 1 };
            shared.MappingRules.RemoveAll(r => string.Equals(r.RuleId, rule.RuleId, StringComparison.OrdinalIgnoreCase));
            QtoBudgetMappingRule copy = CloneRule(rule);
            copy.Scope = "company";
            shared.MappingRules.Add(copy);
            File.WriteAllText(path, Serialize(shared), new UTF8Encoding(false));
        }

        public static IList<QtoBudgetMappingRule> LoadCompanyRules(Database database)
        {
            string path = GetCompanyRulesPath(database);
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return new List<QtoBudgetMappingRule>();
            return Deserialize(File.ReadAllText(path, Encoding.UTF8)).MappingRules;
        }

        private static QtoBudgetMappingRule CloneRule(QtoBudgetMappingRule source)
        {
            return new QtoBudgetMappingRule
            {
                RuleId = source.RuleId, SystemCode = source.SystemCode, EquipmentTypeCode = source.EquipmentTypeCode,
                CadMeasureType = source.CadMeasureType, CableType = source.CableType, ConduitType = source.ConduitType,
                Unit = source.Unit, BudgetItemId = source.BudgetItemId, QuantityRule = source.QuantityRule,
                Factor = source.Factor, FixedQuantity = source.FixedQuantity, Status = source.Status,
                Scope = source.Scope, ReviewReason = source.ReviewReason, UpdatedAt = source.UpdatedAt
            };
        }

        private static string Serialize(QtoBudgetProjectData data)
        {
            DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(QtoBudgetProjectData));
            using (MemoryStream stream = new MemoryStream())
            {
                serializer.WriteObject(stream, data);
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }

        private static QtoBudgetProjectData Deserialize(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return Normalize(new QtoBudgetProjectData { SchemaVersion = 1 });
            DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(QtoBudgetProjectData));
            using (MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
            {
                QtoBudgetProjectData data = serializer.ReadObject(stream) as QtoBudgetProjectData;
                return Normalize(data ?? new QtoBudgetProjectData { SchemaVersion = 1 });
            }
        }

        private static QtoBudgetProjectData Normalize(QtoBudgetProjectData data)
        {
            if (data.MasterItems == null) data.MasterItems = new List<QtoBudgetMasterItem>();
            if (data.MappingRules == null) data.MappingRules = new List<QtoBudgetMappingRule>();
            if (data.CompanyBindings == null) data.CompanyBindings = new List<QtoProjectBudgetBinding>();
            if (data.SchemaVersion < 1) data.SchemaVersion = 1;
            return data;
        }
    }
}
