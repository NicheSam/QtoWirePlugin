using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using Autodesk.AutoCAD.DatabaseServices;

namespace QtoWirePlugin
{
    public static class QtoCompanyBudgetProfileStore
    {
        public const string FileName = "qto_company_budget_profile.json";

        public static string GetProfilePath(Database database)
        {
            string catalogPath = QtoProjectCatalogContext.GetPreferredCatalogPath(database);
            string directory = string.IsNullOrWhiteSpace(catalogPath) ? string.Empty : Path.GetDirectoryName(catalogPath);
            if (!string.IsNullOrWhiteSpace(catalogPath) && File.Exists(catalogPath))
            {
                try
                {
                    QtoBlockCatalog catalog = new QtoBlockCatalogService().LoadCatalog(catalogPath);
                    if (catalog != null && !string.IsNullOrWhiteSpace(catalog.SourceLegendDwg))
                    {
                        string legendDirectory = Path.GetDirectoryName(catalog.SourceLegendDwg);
                        if (!string.IsNullOrWhiteSpace(legendDirectory)) directory = legendDirectory;
                    }
                }
                catch
                {
                }
            }

            return string.IsNullOrWhiteSpace(directory) ? string.Empty : Path.Combine(directory, FileName);
        }

        public static QtoCompanyBudgetProfile Load(Database database)
        {
            string path = GetProfilePath(database);
            return Load(path);
        }

        public static QtoCompanyBudgetProfile Load(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;
            using (FileStream stream = File.OpenRead(path))
            {
                DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(QtoCompanyBudgetProfile));
                QtoCompanyBudgetProfile profile = serializer.ReadObject(stream) as QtoCompanyBudgetProfile;
                return Normalize(profile);
            }
        }

        public static QtoCompanyBudgetProfile CreateFromProject(QtoBudgetProjectData project)
        {
            if (project == null || project.MasterItems == null || project.MasterItems.Count == 0)
            {
                throw new InvalidOperationException("請先匯入預算格式，再建立公司預算基準。");
            }

            QtoCompanyBudgetProfile profile = new QtoCompanyBudgetProfile
            {
                ProfileId = "CBP-" + Guid.NewGuid().ToString("N"),
                Version = 1,
                BaselineWorkbookPath = project.SourceWorkbookPath ?? string.Empty,
                BaselineWorkbookFingerprint = ComputeFileFingerprint(project.SourceWorkbookPath),
                UpdatedAt = DateTime.UtcNow
            };

            Dictionary<string, QtoBudgetMasterItem> byId = project.MasterItems
                .Where(i => i != null && !string.IsNullOrWhiteSpace(i.BudgetItemId))
                .ToDictionary(i => i.BudgetItemId, StringComparer.OrdinalIgnoreCase);
            Dictionary<string, int> occurrenceBySignature = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (QtoBudgetMasterItem item in project.MasterItems
                .Where(i => i != null && string.Equals(i.RowType, QtoBudgetRowType.Detail, StringComparison.OrdinalIgnoreCase))
                .OrderBy(i => i.SortOrder))
            {
                string hierarchy = BuildHierarchyPath(item, byId);
                string signature = BuildSignature(item.SourceSheet, hierarchy, item.ItemName, item.Unit);
                int occurrence;
                occurrenceBySignature.TryGetValue(signature, out occurrence);
                occurrenceBySignature[signature] = occurrence + 1;
                profile.Items.Add(new QtoCompanyBudgetItem
                {
                    CompanyBudgetItemId = "CBI-" + HashText(signature + "|" + occurrence.ToString(CultureInfo.InvariantCulture)).Substring(0, 20),
                    CanonicalName = item.ItemName ?? string.Empty,
                    Unit = item.Unit ?? string.Empty,
                    HierarchyPath = hierarchy,
                    SourceSheetHint = item.SourceSheet ?? string.Empty,
                    CanonicalSignature = signature,
                    OccurrenceIndex = occurrence,
                    Status = "active"
                });
            }

            return profile;
        }

        public static void PreserveItemMetadata(QtoCompanyBudgetProfile previous, QtoCompanyBudgetProfile replacement)
        {
            if (previous == null || replacement == null) return;
            if (replacement.Items == null) replacement.Items = new List<QtoCompanyBudgetItem>();
            if (replacement.MappingRules == null) replacement.MappingRules = new List<QtoBudgetMappingRule>();
            Dictionary<string, QtoCompanyBudgetItem> existingItems = (previous.Items ?? new List<QtoCompanyBudgetItem>())
                .Where(i => i != null && !string.IsNullOrWhiteSpace(i.CompanyBudgetItemId))
                .GroupBy(i => i.CompanyBudgetItemId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
            HashSet<string> replacementIds = new HashSet<string>(replacement.Items
                .Where(i => i != null && !string.IsNullOrWhiteSpace(i.CompanyBudgetItemId))
                .Select(i => i.CompanyBudgetItemId), StringComparer.OrdinalIgnoreCase);
            foreach (QtoCompanyBudgetItem item in replacement.Items.Where(i => i != null))
            {
                QtoCompanyBudgetItem existing;
                if (existingItems.TryGetValue(item.CompanyBudgetItemId ?? string.Empty, out existing))
                {
                    item.Aliases = new List<string>(existing.Aliases ?? new List<string>());
                }
            }
            foreach (QtoBudgetMappingRule rule in (previous.MappingRules ?? new List<QtoBudgetMappingRule>())
                .Where(r => r != null && replacementIds.Contains(r.CompanyBudgetItemId ?? string.Empty)))
            {
                replacement.MappingRules.Add(rule);
            }
        }

        public static void Save(Database database, QtoCompanyBudgetProfile profile, int expectedVersion)
        {
            string path = GetProfilePath(database);
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new InvalidOperationException("請先設定公司標準圖塊庫，才能保存公司預算規則。");
            }

            Save(path, profile, expectedVersion);
        }

        public static void Save(string path, QtoCompanyBudgetProfile profile, int expectedVersion)
        {
            if (profile == null) throw new ArgumentNullException("profile");
            string directory = Path.GetDirectoryName(path);
            if (string.IsNullOrWhiteSpace(directory)) throw new InvalidOperationException("公司規則路徑不完整。");
            if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);

            QtoCompanyBudgetProfile current = Load(path);
            int currentVersion = current == null ? 0 : current.Version;
            if (expectedVersion >= 0 && currentVersion != expectedVersion)
            {
                throw new InvalidOperationException("公司規則已被其他使用者更新，請重新載入後再發布。");
            }

            profile.SchemaVersion = 2;
            profile.Version = Math.Max(1, currentVersion + 1);
            profile.UpdatedAt = DateTime.UtcNow;
            Normalize(profile);

            string tempPath = path + ".tmp." + Guid.NewGuid().ToString("N");
            string backupPath = path + ".bak";
            try
            {
                Write(tempPath, profile);
                if (File.Exists(path))
                {
                    try
                    {
                        File.Replace(tempPath, path, backupPath, true);
                    }
                    catch (PlatformNotSupportedException)
                    {
                        File.Copy(path, backupPath, true);
                        File.Copy(tempPath, path, true);
                    }
                    catch (IOException)
                    {
                        File.Copy(path, backupPath, true);
                        File.Copy(tempPath, path, true);
                    }
                }
                else
                {
                    File.Move(tempPath, path);
                }
            }
            finally
            {
                if (File.Exists(tempPath)) File.Delete(tempPath);
            }
        }

        public static string BuildSignature(string sheet, string hierarchy, string name, string unit)
        {
            return string.Join("|", NormalizeText(sheet), NormalizeText(hierarchy), NormalizeText(name), NormalizeText(unit));
        }

        public static string BuildHierarchyPath(QtoBudgetMasterItem item, IDictionary<string, QtoBudgetMasterItem> byId)
        {
            List<string> names = new List<string>();
            HashSet<string> visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string parentId = item == null ? string.Empty : item.ParentItemId;
            while (!string.IsNullOrWhiteSpace(parentId) && visited.Add(parentId))
            {
                QtoBudgetMasterItem parent;
                if (byId == null || !byId.TryGetValue(parentId, out parent) || parent == null) break;
                if (!string.IsNullOrWhiteSpace(parent.ItemName)) names.Add(parent.ItemName.Trim());
                parentId = parent.ParentItemId;
            }
            names.Reverse();
            return string.Join(" / ", names);
        }

        public static string NormalizeText(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            StringBuilder result = new StringBuilder();
            bool previousSpace = false;
            foreach (char character in value.Trim())
            {
                bool isSpace = char.IsWhiteSpace(character);
                if (isSpace)
                {
                    if (!previousSpace) result.Append(' ');
                }
                else
                {
                    result.Append(char.ToUpperInvariant(character));
                }
                previousSpace = isSpace;
            }
            return result.ToString();
        }

        private static QtoCompanyBudgetProfile Normalize(QtoCompanyBudgetProfile profile)
        {
            if (profile == null) return null;
            if (profile.Items == null) profile.Items = new List<QtoCompanyBudgetItem>();
            if (profile.MappingRules == null) profile.MappingRules = new List<QtoBudgetMappingRule>();
            foreach (QtoCompanyBudgetItem item in profile.Items)
            {
                if (item.Aliases == null) item.Aliases = new List<string>();
                if (string.IsNullOrWhiteSpace(item.Status)) item.Status = "active";
            }
            return profile;
        }

        private static void Write(string path, QtoCompanyBudgetProfile profile)
        {
            DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(QtoCompanyBudgetProfile));
            using (FileStream stream = File.Create(path))
            using (XmlDictionaryWriter writer = JsonReaderWriterFactory.CreateJsonWriter(stream, Encoding.UTF8, true, true, "  "))
            {
                serializer.WriteObject(writer, profile);
            }
        }

        private static string ComputeFileFingerprint(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return string.Empty;
            using (SHA256 sha = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
            {
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty);
            }
        }

        private static string HashText(string value)
        {
            using (SHA256 sha = SHA256.Create())
            {
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty))).Replace("-", string.Empty);
            }
        }
    }
}
