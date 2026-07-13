using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Autodesk.AutoCAD.DatabaseServices;

namespace QtoWirePlugin
{
    public static class QtoDictionaryLoader
    {
        public static QtoDictionaryStore LoadDefault(Database database)
        {
            QtoDictionaryStore store = new QtoDictionaryStore();
            string dictionaryDirectory = ResolveDictionaryDirectory(database);
            store.SourceDirectory = dictionaryDirectory ?? string.Empty;

            if (string.IsNullOrWhiteSpace(dictionaryDirectory) || !Directory.Exists(dictionaryDirectory))
            {
                store.LoadWarning = "dictionary_dir_not_found";
                QtoSystemDefaults.AddTo(store.SystemCodes);
                return store;
            }

            LoadSystemCodes(store, Path.Combine(dictionaryDirectory, "system_codes.csv"));
            if (store.SystemCodes.Count == 0)
            {
                QtoSystemDefaults.AddTo(store.SystemCodes);
            }
            LoadEquipmentTypes(store, Path.Combine(dictionaryDirectory, "equipment_types.csv"));
            return store;
        }

        private static string ResolveDictionaryDirectory(Database database)
        {
            string env = Environment.GetEnvironmentVariable("QTO_DICTIONARY_DIR");
            if (!string.IsNullOrWhiteSpace(env) && Directory.Exists(env))
            {
                return env;
            }

            if (database != null && !string.IsNullOrWhiteSpace(database.Filename))
            {
                string dwgFolder = Path.GetDirectoryName(database.Filename);
                if (!string.IsNullOrWhiteSpace(dwgFolder))
                {
                    string local = Path.Combine(dwgFolder, "m2_m4_shared_dictionary");
                    if (Directory.Exists(local))
                    {
                        return local;
                    }
                }
            }

            foreach (string bundleCandidate in GetBundleDictionaryCandidates())
            {
                if (!string.IsNullOrWhiteSpace(bundleCandidate) && Directory.Exists(bundleCandidate))
                {
                    return bundleCandidate;
                }
            }

            return string.Empty;
        }

        private static IEnumerable<string> GetBundleDictionaryCandidates()
        {
            List<string> candidates = new List<string>();
            string assemblyPath = Assembly.GetExecutingAssembly().Location;
            if (string.IsNullOrWhiteSpace(assemblyPath))
            {
                return candidates;
            }

            string assemblyDirectory = Path.GetDirectoryName(assemblyPath);
            if (string.IsNullOrWhiteSpace(assemblyDirectory))
            {
                return candidates;
            }

            candidates.Add(Path.Combine(assemblyDirectory, "m2_m4_shared_dictionary"));

            DirectoryInfo directory = new DirectoryInfo(assemblyDirectory);
            for (int i = 0; i < 3 && directory != null; i++)
            {
                candidates.Add(Path.Combine(directory.FullName, "m2_m4_shared_dictionary"));
                directory = directory.Parent;
            }

            return candidates;
        }

        private static void LoadSystemCodes(QtoDictionaryStore store, string path)
        {
            foreach (Dictionary<string, string> row in ReadCsv(path))
            {
                string code = Get(row, "system_code");
                if (!string.IsNullOrWhiteSpace(code))
                {
                    store.SystemCodes.Add(code);
                }
            }
        }

        private static void LoadEquipmentTypes(QtoDictionaryStore store, string path)
        {
            foreach (Dictionary<string, string> row in ReadCsv(path))
            {
                string code = Get(row, "equipment_type_code");
                if (string.IsNullOrWhiteSpace(code))
                {
                    continue;
                }

                QtoEquipmentTypeDefinition definition = new QtoEquipmentTypeDefinition();
                definition.EquipmentTypeCode = code;
                definition.EquipmentTypeName = Get(row, "equipment_type_name");
                definition.SystemCode = Get(row, "system_code");
                definition.QuantityBasis = Get(row, "quantity_basis");
                definition.DefaultMappingStatus = Get(row, "default_mapping_status");
                store.EquipmentTypes[code] = definition;
            }
        }

        private static IEnumerable<Dictionary<string, string>> ReadCsv(string path)
        {
            List<Dictionary<string, string>> rows = new List<Dictionary<string, string>>();
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return rows;
            }

            string[] lines = File.ReadAllLines(path, new UTF8Encoding(true));
            if (lines.Length == 0)
            {
                return rows;
            }

            List<string> headers = ParseCsvLine(lines[0]);
            for (int i = 1; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i]))
                {
                    continue;
                }

                List<string> values = ParseCsvLine(lines[i]);
                Dictionary<string, string> row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                for (int j = 0; j < headers.Count; j++)
                {
                    string value = j < values.Count ? values[j] : string.Empty;
                    row[headers[j]] = value;
                }

                rows.Add(row);
            }

            return rows;
        }

        private static List<string> ParseCsvLine(string line)
        {
            List<string> values = new List<string>();
            StringBuilder current = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char ch = line[i];
                if (ch == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if (ch == ',' && !inQuotes)
                {
                    values.Add(current.ToString());
                    current.Length = 0;
                }
                else
                {
                    current.Append(ch);
                }
            }

            values.Add(current.ToString());
            return values;
        }

        private static string Get(Dictionary<string, string> row, string key)
        {
            string value;
            if (row != null && row.TryGetValue(key, out value))
            {
                return value ?? string.Empty;
            }

            return string.Empty;
        }
    }
}
