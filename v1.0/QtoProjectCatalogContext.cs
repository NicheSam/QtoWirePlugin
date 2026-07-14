using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;

namespace QtoWirePlugin
{
    public static class QtoProjectCatalogContext
    {
        private const string SettingsDictionaryKey = "QTO_PROJECT_SETTINGS";
        private const string CatalogPathKey = "CATALOG_PATH";
        private const string WorkbookPathKey = "WORKBOOK_PATH";
        private const string SchemaVersionKey = "SCHEMA_VERSION";

        public static string GetPreferredCatalogPath(Database database)
        {
            string configured = GetConfiguredCatalogPath(database);
            if (!string.IsNullOrWhiteSpace(configured))
            {
                return configured;
            }

            QtoBlockCatalogService service = new QtoBlockCatalogService();
            return service.GetDefaultCatalogPath(database == null ? string.Empty : database.Filename);
        }

        public static string GetConfiguredCatalogPath(Database database)
        {
            if (database == null)
            {
                return string.Empty;
            }

            string storedPath = string.Empty;
            using (Transaction transaction = database.TransactionManager.StartOpenCloseTransaction())
            {
                DBDictionary namedObjects = transaction.GetObject(database.NamedObjectsDictionaryId, OpenMode.ForRead, false) as DBDictionary;
                if (namedObjects == null || !namedObjects.Contains(SettingsDictionaryKey))
                {
                    return string.Empty;
                }

                Xrecord record = transaction.GetObject(namedObjects.GetAt(SettingsDictionaryKey), OpenMode.ForRead, false) as Xrecord;
                Dictionary<string, string> values = ReadValues(record == null ? null : record.Data);
                values.TryGetValue(CatalogPathKey, out storedPath);
            }

            return ResolveStoredPath(database, storedPath);
        }

        public static void SetCatalogPath(Database database, string catalogPath)
        {
            if (database == null)
            {
                throw new ArgumentNullException("database");
            }

            if (string.IsNullOrWhiteSpace(catalogPath))
            {
                throw new ArgumentException("Catalog path is required.", "catalogPath");
            }

            SetPathValue(database, CatalogPathKey, catalogPath);
        }

        public static string GetConfiguredWorkbookPath(Database database)
        {
            return GetPathValue(database, WorkbookPathKey);
        }

        public static void SetWorkbookPath(Database database, string workbookPath)
        {
            if (string.IsNullOrWhiteSpace(workbookPath))
            {
                throw new ArgumentException("Workbook path is required.", "workbookPath");
            }

            SetPathValue(database, WorkbookPathKey, workbookPath);
        }

        public static void ClearWorkbookPath(Database database)
        {
            SetStringValue(database, WorkbookPathKey, string.Empty);
        }

        private static string GetPathValue(Database database, string key)
        {
            if (database == null)
            {
                return string.Empty;
            }

            string storedPath = string.Empty;
            using (Transaction transaction = database.TransactionManager.StartOpenCloseTransaction())
            {
                DBDictionary namedObjects = transaction.GetObject(database.NamedObjectsDictionaryId, OpenMode.ForRead, false) as DBDictionary;
                if (namedObjects == null || !namedObjects.Contains(SettingsDictionaryKey))
                {
                    return string.Empty;
                }

                Xrecord record = transaction.GetObject(namedObjects.GetAt(SettingsDictionaryKey), OpenMode.ForRead, false) as Xrecord;
                Dictionary<string, string> values = ReadValues(record == null ? null : record.Data);
                values.TryGetValue(key, out storedPath);
            }

            return ResolveStoredPath(database, storedPath);
        }

        private static void SetPathValue(Database database, string key, string path)
        {
            if (database == null)
            {
                throw new ArgumentNullException("database");
            }

            SetStringValue(database, key, BuildStoredPath(database, path));
        }

        private static void SetStringValue(Database database, string key, string value)
        {
            Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            using (Transaction transaction = database.TransactionManager.StartTransaction())
            {
                DBDictionary namedObjects = transaction.GetObject(database.NamedObjectsDictionaryId, OpenMode.ForRead, false) as DBDictionary;
                Xrecord record;

                if (namedObjects.Contains(SettingsDictionaryKey))
                {
                    record = transaction.GetObject(namedObjects.GetAt(SettingsDictionaryKey), OpenMode.ForWrite, false) as Xrecord;
                    foreach (KeyValuePair<string, string> item in ReadValues(record == null ? null : record.Data))
                    {
                        values[item.Key] = item.Value;
                    }
                }
                else
                {
                    namedObjects.UpgradeOpen();
                    record = new Xrecord();
                    namedObjects.SetAt(SettingsDictionaryKey, record);
                    transaction.AddNewlyCreatedDBObject(record, true);
                }

                values[SchemaVersionKey] = "2";
                values[key] = value ?? string.Empty;
                ResultBuffer buffer = new ResultBuffer();
                foreach (KeyValuePair<string, string> item in values.OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase))
                {
                    buffer.Add(new TypedValue((int)DxfCode.Text, item.Key));
                    buffer.Add(new TypedValue((int)DxfCode.Text, item.Value ?? string.Empty));
                }
                record.Data = buffer;
                transaction.Commit();
            }
        }

        public static QtoCatalogSnapshot LoadSnapshot(Database database)
        {
            string catalogPath = GetPreferredCatalogPath(database);
            if (string.IsNullOrWhiteSpace(catalogPath) || !File.Exists(catalogPath))
            {
                return new QtoCatalogSnapshot { LoadError = "找不到本圖面設定的 catalog：" + (catalogPath ?? string.Empty) };
            }

            try
            {
                QtoBlockCatalog catalog = new QtoBlockCatalogService().LoadCatalog(catalogPath);
                return QtoCatalogSnapshotBuilder.Build(catalog);
            }
            catch (Exception ex)
            {
                return new QtoCatalogSnapshot { LoadError = "無法讀取 catalog：" + ex.Message };
            }
        }

        private static Dictionary<string, string> ReadValues(ResultBuffer buffer)
        {
            Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (buffer == null)
            {
                return values;
            }

            string pendingKey = null;
            foreach (TypedValue value in buffer)
            {
                string text = Convert.ToString(value.Value) ?? string.Empty;
                if (pendingKey == null)
                {
                    pendingKey = text;
                }
                else
                {
                    values[pendingKey] = text;
                    pendingKey = null;
                }
            }

            return values;
        }

        private static string BuildStoredPath(Database database, string catalogPath)
        {
            string fullPath = Path.GetFullPath(catalogPath);
            string drawingDirectory = GetDrawingDirectory(database);
            if (string.IsNullOrWhiteSpace(drawingDirectory))
            {
                return fullPath;
            }

            string basePath = EnsureTrailingSeparator(Path.GetFullPath(drawingDirectory));
            Uri baseUri = new Uri(basePath, UriKind.Absolute);
            Uri targetUri = new Uri(fullPath, UriKind.Absolute);
            string relative = Uri.UnescapeDataString(baseUri.MakeRelativeUri(targetUri).ToString()).Replace('/', Path.DirectorySeparatorChar);
            return relative.StartsWith("..", StringComparison.Ordinal) ? fullPath : relative;
        }

        private static string ResolveStoredPath(Database database, string storedPath)
        {
            if (string.IsNullOrWhiteSpace(storedPath))
            {
                return string.Empty;
            }

            if (Path.IsPathRooted(storedPath))
            {
                return Path.GetFullPath(storedPath);
            }

            string drawingDirectory = GetDrawingDirectory(database);
            return string.IsNullOrWhiteSpace(drawingDirectory)
                ? Path.GetFullPath(storedPath)
                : Path.GetFullPath(Path.Combine(drawingDirectory, storedPath));
        }

        private static string GetDrawingDirectory(Database database)
        {
            if (database == null || string.IsNullOrWhiteSpace(database.Filename))
            {
                return string.Empty;
            }

            return Path.GetDirectoryName(database.Filename) ?? string.Empty;
        }

        private static string EnsureTrailingSeparator(string path)
        {
            if (string.IsNullOrEmpty(path) || path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
            {
                return path;
            }

            return path + Path.DirectorySeparatorChar;
        }
    }
}
