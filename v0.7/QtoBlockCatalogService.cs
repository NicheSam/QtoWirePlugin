using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Xml;

namespace QtoWirePlugin
{
    public class QtoBlockCatalogService
    {
        public const string DefaultCatalogFileName = "qto_block_catalog.json";

        private readonly QtoLegendDwgScanner scanner;

        public QtoBlockCatalogService()
            : this(new QtoLegendDwgScanner())
        {
        }

        public QtoBlockCatalogService(QtoLegendDwgScanner scanner)
        {
            this.scanner = scanner ?? new QtoLegendDwgScanner();
        }

        public string GetDefaultCatalogPath(string projectDwgPath)
        {
            if (string.IsNullOrWhiteSpace(projectDwgPath))
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), DefaultCatalogFileName);
            }

            string directory = Path.GetDirectoryName(projectDwgPath);

            if (string.IsNullOrWhiteSpace(directory))
            {
                directory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            }

            string projectName = Path.GetFileNameWithoutExtension(projectDwgPath);

            if (string.IsNullOrWhiteSpace(projectName))
            {
                return Path.Combine(directory, DefaultCatalogFileName);
            }

            return Path.Combine(directory, projectName + ".qto_catalog.json");
        }

        public QtoBlockCatalog LoadCatalog(string catalogPath)
        {
            if (string.IsNullOrWhiteSpace(catalogPath) || !File.Exists(catalogPath))
            {
                return CreateEmptyCatalog();
            }

            using (FileStream stream = File.OpenRead(catalogPath))
            {
                DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(QtoBlockCatalog));
                QtoBlockCatalog catalog = serializer.ReadObject(stream) as QtoBlockCatalog;
                return NormalizeCatalog(catalog);
            }
        }

        public void SaveCatalog(QtoBlockCatalog catalog, string catalogPath)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException("catalog");
            }

            if (string.IsNullOrWhiteSpace(catalogPath))
            {
                throw new ArgumentException("Catalog path is required.", "catalogPath");
            }

            string directory = Path.GetDirectoryName(catalogPath);

            if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            NormalizeCatalog(catalog);
            catalog.LastSavedAt = DateTime.Now;

            DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(QtoBlockCatalog));
            using (FileStream stream = File.Create(catalogPath))
            using (XmlDictionaryWriter writer = JsonReaderWriterFactory.CreateJsonWriter(stream, Encoding.UTF8, true, true, "  "))
            {
                serializer.WriteObject(writer, catalog);
            }
        }

        public QtoBlockCatalogOperationResult RescanLegendAndSave(string legendDwgPath, string catalogPath)
        {
            QtoBlockCatalog catalog = LoadCatalog(catalogPath);
            QtoLegendDwgScanResult scanResult = scanner.ScanLegendDwg(legendDwgPath);
            QtoBlockCatalogOperationResult result = MergeScanResult(catalog, scanResult);
            result.CatalogPath = catalogPath;

            result.Success = true;
            result.UserMessage = BuildUserMessage(result);
            result.TechnicalMessage = BuildTechnicalMessage(result, scanResult);
            AddCatalogMessage(result.Catalog, result.UserMessage, result.TechnicalMessage);
            SaveCatalog(result.Catalog, catalogPath);

            return result;
        }

        public QtoBlockCatalogOperationResult MergeScanResult(QtoBlockCatalog catalog, QtoLegendDwgScanResult scanResult)
        {
            if (scanResult == null)
            {
                throw new ArgumentNullException("scanResult");
            }

            QtoBlockCatalog normalizedCatalog = NormalizeCatalog(catalog);
            DateTime now = scanResult.ScannedAt == DateTime.MinValue ? DateTime.Now : scanResult.ScannedAt;

            normalizedCatalog.SourceLegendDwg = scanResult.SourceLegendDwg;
            normalizedCatalog.LastScannedAt = now;

            QtoBlockCatalogOperationResult result = new QtoBlockCatalogOperationResult();
            result.Catalog = normalizedCatalog;

            Dictionary<string, List<QtoBlockCatalogItem>> existingByName = normalizedCatalog.Items
                .Where(item => !string.IsNullOrWhiteSpace(item.BlockName))
                .GroupBy(item => item.BlockName, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);

            HashSet<string> scannedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (QtoBlockCatalogItem scannedItem in scanResult.Items)
            {
                if (scannedItem == null || string.IsNullOrWhiteSpace(scannedItem.BlockName))
                {
                    continue;
                }

                scannedNames.Add(scannedItem.BlockName);

                List<QtoBlockCatalogItem> matches;
                if (!existingByName.TryGetValue(scannedItem.BlockName, out matches) || matches.Count == 0)
                {
                    scannedItem.CatalogId = CreateCatalogId(scannedItem.BlockName);
                    scannedItem.Status = QtoBlockCatalogStatus.Unconfigured;
                    scannedItem.LastUpdatedAt = now;
                    scannedItem.RefreshStatusDisplayName();
                    normalizedCatalog.Items.Add(scannedItem);
                    existingByName[scannedItem.BlockName] = new List<QtoBlockCatalogItem> { scannedItem };
                    result.NewItemCount++;
                    continue;
                }

                if (matches.Count > 1)
                {
                    foreach (QtoBlockCatalogItem duplicate in matches)
                    {
                        duplicate.Status = QtoBlockCatalogStatus.Conflict;
                        duplicate.LastScannedAt = now;
                        duplicate.TechnicalNote = "Duplicate catalog items share the same BlockName.";
                        duplicate.RefreshStatusDisplayName();
                    }

                    result.ConflictItemCount++;
                    continue;
                }

                QtoBlockCatalogItem existing = matches[0];
                string previousFingerprint = existing.BlockFingerprint ?? string.Empty;
                string scannedFingerprint = scannedItem.BlockFingerprint ?? string.Empty;
                bool fingerprintChanged = !string.IsNullOrWhiteSpace(previousFingerprint)
                    && !string.Equals(previousFingerprint, scannedFingerprint, StringComparison.OrdinalIgnoreCase);

                CopyScannedFields(existing, scannedItem, now);

                if (fingerprintChanged)
                {
                    if (string.Equals(existing.Status, QtoBlockCatalogStatus.Deprecated, StringComparison.OrdinalIgnoreCase))
                    {
                        existing.Status = QtoBlockCatalogStatus.Conflict;
                        result.ConflictItemCount++;
                    }
                    else
                    {
                        existing.Status = QtoBlockCatalogStatus.Updated;
                        result.UpdatedItemCount++;
                    }
                }
                else if (string.Equals(existing.Status, QtoBlockCatalogStatus.Missing, StringComparison.OrdinalIgnoreCase))
                {
                    existing.Status = QtoBlockCatalogStatus.Updated;
                    result.UpdatedItemCount++;
                }

                existing.RefreshStatusDisplayName();
            }

            foreach (QtoBlockCatalogItem existing in normalizedCatalog.Items)
            {
                if (existing == null || string.IsNullOrWhiteSpace(existing.BlockName))
                {
                    continue;
                }

                if (scannedNames.Contains(existing.BlockName))
                {
                    continue;
                }

                if (string.Equals(existing.Status, QtoBlockCatalogStatus.Deprecated, StringComparison.OrdinalIgnoreCase))
                {
                    existing.RefreshStatusDisplayName();
                    continue;
                }

                existing.Status = QtoBlockCatalogStatus.Missing;
                existing.LastScannedAt = now;
                existing.TechnicalNote = "Block definition was not found in the latest legend DWG scan.";
                existing.RefreshStatusDisplayName();
                result.MissingItemCount++;
            }

            result.Success = true;
            result.UserMessage = BuildUserMessage(result);
            result.TechnicalMessage = BuildTechnicalMessage(result, scanResult);
            return result;
        }

        private static QtoBlockCatalog CreateEmptyCatalog()
        {
            return new QtoBlockCatalog();
        }

        private static QtoBlockCatalog NormalizeCatalog(QtoBlockCatalog catalog)
        {
            if (catalog == null)
            {
                catalog = CreateEmptyCatalog();
            }

            if (string.IsNullOrWhiteSpace(catalog.SchemaVersion))
            {
                catalog.SchemaVersion = "0.1";
            }

            if (catalog.Items == null)
            {
                catalog.Items = new List<QtoBlockCatalogItem>();
            }

            if (catalog.Messages == null)
            {
                catalog.Messages = new List<QtoBlockCatalogMessage>();
            }

            foreach (QtoBlockCatalogItem item in catalog.Items)
            {
                if (item == null)
                {
                    continue;
                }

                if (item.AliasNames == null)
                {
                    item.AliasNames = new List<string>();
                }

                if (item.AttributeTags == null)
                {
                    item.AttributeTags = new List<string>();
                }

                if (item.ReferenceLayers == null)
                {
                    item.ReferenceLayers = new List<string>();
                }

                if (item.ReferenceSamples == null)
                {
                    item.ReferenceSamples = new List<QtoBlockReferenceSample>();
                }

                if (string.IsNullOrWhiteSpace(item.CatalogId) && !string.IsNullOrWhiteSpace(item.BlockName))
                {
                    item.CatalogId = CreateCatalogId(item.BlockName);
                }

                item.RefreshStatusDisplayName();
            }

            return catalog;
        }

        private static void CopyScannedFields(QtoBlockCatalogItem target, QtoBlockCatalogItem source, DateTime now)
        {
            target.SourceLegendDwg = source.SourceLegendDwg;
            target.BlockFingerprint = source.BlockFingerprint;
            target.LastScannedAt = now;
            target.DefinitionEntityCount = source.DefinitionEntityCount;
            target.ReferenceCount = source.ReferenceCount;
            target.IsDynamicBlock = source.IsDynamicBlock;
            target.IsFromExternalReference = source.IsFromExternalReference;
            target.IsAnonymous = source.IsAnonymous;
            target.DefinitionSignature = source.DefinitionSignature;
            target.AttributeTags = source.AttributeTags ?? new List<string>();
            target.ReferenceLayers = source.ReferenceLayers ?? new List<string>();
            target.ReferenceSamples = source.ReferenceSamples ?? new List<QtoBlockReferenceSample>();
        }

        private static string CreateCatalogId(string blockName)
        {
            string normalized = string.IsNullOrWhiteSpace(blockName) ? "block" : blockName.Trim();
            StringBuilder builder = new StringBuilder();

            foreach (char character in normalized)
            {
                if (char.IsLetterOrDigit(character))
                {
                    builder.Append(char.ToLowerInvariant(character));
                }
                else
                {
                    builder.Append('_');
                }
            }

            return "blk_" + builder.ToString().Trim('_');
        }

        private static void AddCatalogMessage(QtoBlockCatalog catalog, string userMessage, string technicalMessage)
        {
            if (catalog.Messages == null)
            {
                catalog.Messages = new List<QtoBlockCatalogMessage>();
            }

            catalog.Messages.Add(new QtoBlockCatalogMessage
            {
                UserMessage = userMessage,
                TechnicalMessage = technicalMessage,
                CreatedAt = DateTime.Now
            });
        }

        private static string BuildUserMessage(QtoBlockCatalogOperationResult result)
        {
            List<string> parts = new List<string>();

            if (result.NewItemCount > 0)
            {
                parts.Add("發現 " + result.NewItemCount + " 個新圖塊，請補齊基本分類後再納入同步。");
            }

            if (result.UpdatedItemCount > 0)
            {
                parts.Add(result.UpdatedItemCount + " 個圖塊內容有更新，請確認是否套用到專案。");
            }

            if (result.MissingItemCount > 0)
            {
                parts.Add(result.MissingItemCount + " 個圖塊已不在圖例 DWG 中，已標記為找不到來源。");
            }

            if (result.ConflictItemCount > 0)
            {
                parts.Add(result.ConflictItemCount + " 個圖塊資料有衝突，請人工確認。");
            }

            if (parts.Count == 0)
            {
                parts.Add("圖塊資料庫已更新，未發現需要處理的新狀態。");
            }

            return string.Join(" ", parts.ToArray());
        }

        private static string BuildTechnicalMessage(QtoBlockCatalogOperationResult result, QtoLegendDwgScanResult scanResult)
        {
            return "Legend DWG scan merged. Source="
                + (scanResult.SourceLegendDwg ?? string.Empty)
                + "; definitions=" + scanResult.DefinitionCount
                + "; references=" + scanResult.ReferenceCount
                + "; new=" + result.NewItemCount
                + "; updated=" + result.UpdatedItemCount
                + "; missing=" + result.MissingItemCount
                + "; conflicts=" + result.ConflictItemCount
                + ".";
        }
    }
}
