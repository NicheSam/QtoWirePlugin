using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace QtoWirePlugin
{
    public class QtoLegendDwgScanner
    {
        private const int MaxReferenceSamplesPerBlock = 20;

        public QtoLegendDwgScanResult ScanLegendDwg(string legendDwgPath)
        {
            if (string.IsNullOrWhiteSpace(legendDwgPath))
            {
                throw new ArgumentException("請先選擇圖例 DWG。", "legendDwgPath");
            }

            if (!File.Exists(legendDwgPath))
            {
                throw new FileNotFoundException("找不到圖例 DWG。", legendDwgPath);
            }

            using (Database database = new Database(false, true))
            {
                database.ReadDwgFile(legendDwgPath, FileShare.ReadWrite, true, string.Empty);
                database.CloseInput(true);
                return ScanDatabase(database, legendDwgPath);
            }
        }

        public QtoLegendDwgScanResult ScanDatabase(Database database, string sourceLegendDwg)
        {
            if (database == null)
            {
                throw new ArgumentNullException("database");
            }

            DateTime scannedAt = DateTime.Now;
            Dictionary<string, QtoBlockCatalogItem> itemsByName = new Dictionary<string, QtoBlockCatalogItem>(StringComparer.OrdinalIgnoreCase);
            int definitionCount = 0;
            int referenceCount = 0;

            using (Transaction transaction = database.TransactionManager.StartTransaction())
            {
                BlockTable blockTable = (BlockTable)transaction.GetObject(database.BlockTableId, OpenMode.ForRead);

                foreach (ObjectId blockRecordId in blockTable)
                {
                    BlockTableRecord record = transaction.GetObject(blockRecordId, OpenMode.ForRead, false) as BlockTableRecord;

                    if (!ShouldCatalogBlockDefinition(record))
                    {
                        continue;
                    }

                    QtoBlockCatalogItem item = BuildItemFromDefinition(transaction, record, sourceLegendDwg, scannedAt);
                    itemsByName[item.BlockName] = item;
                    definitionCount++;
                }

                foreach (ObjectId blockRecordId in blockTable)
                {
                    BlockTableRecord spaceRecord = transaction.GetObject(blockRecordId, OpenMode.ForRead, false) as BlockTableRecord;

                    if (spaceRecord == null || !spaceRecord.IsLayout)
                    {
                        continue;
                    }

                    foreach (ObjectId entityId in spaceRecord)
                    {
                        BlockReference blockReference = transaction.GetObject(entityId, OpenMode.ForRead, false) as BlockReference;

                        if (blockReference == null)
                        {
                            continue;
                        }

                        referenceCount++;
                        string blockName = GetEffectiveBlockName(transaction, blockReference);

                        if (string.IsNullOrWhiteSpace(blockName))
                        {
                            continue;
                        }

                        QtoBlockCatalogItem item;
                        if (!itemsByName.TryGetValue(blockName, out item))
                        {
                            item = BuildItemFromReference(transaction, blockReference, blockName, sourceLegendDwg, scannedAt);
                            itemsByName[blockName] = item;
                        }

                        AddReferenceData(transaction, item, blockReference, blockName);
                    }
                }

                foreach (QtoBlockCatalogItem item in itemsByName.Values)
                {
                    item.BlockFingerprint = BuildFingerprint(item);
                    item.TechnicalNote = "Scanned from BlockTableRecord and BlockReference data.";
                    item.RefreshStatusDisplayName();
                }

                transaction.Commit();
            }

            QtoLegendDwgScanResult result = new QtoLegendDwgScanResult();
            result.SourceLegendDwg = sourceLegendDwg;
            result.ScannedAt = scannedAt;
            result.Items = itemsByName.Values.OrderBy(item => item.BlockName, StringComparer.OrdinalIgnoreCase).ToList();
            result.DefinitionCount = definitionCount;
            result.ReferenceCount = referenceCount;
            result.UserMessage = "已掃描圖例 DWG，取得 " + result.Items.Count + " 個圖塊項目。";
            result.TechnicalMessage = "Legend DWG scanned. Source="
                + (sourceLegendDwg ?? string.Empty)
                + "; catalogItems=" + result.Items.Count
                + "; blockReferences=" + referenceCount
                + ".";

            return result;
        }

        private static bool ShouldCatalogBlockDefinition(BlockTableRecord record)
        {
            if (record == null)
            {
                return false;
            }

            if (record.IsLayout || record.IsAnonymous || record.IsDependent || record.IsFromExternalReference)
            {
                return false;
            }

            return !string.IsNullOrWhiteSpace(record.Name) && !record.Name.StartsWith("*", StringComparison.Ordinal);
        }

        private static QtoBlockCatalogItem BuildItemFromDefinition(Transaction transaction, BlockTableRecord record, string sourceLegendDwg, DateTime scannedAt)
        {
            QtoBlockCatalogItem item = new QtoBlockCatalogItem();
            item.BlockName = record.Name ?? string.Empty;
            item.DisplayName = record.Name ?? string.Empty;
            item.SourceLegendDwg = sourceLegendDwg;
            item.LastScannedAt = scannedAt;
            item.Status = QtoBlockCatalogStatus.Unconfigured;
            item.DefinitionEntityCount = CountDefinitionEntities(record);
            item.IsAnonymous = record.IsAnonymous;
            item.IsFromExternalReference = record.IsFromExternalReference;
            item.DefinitionSignature = BuildDefinitionSignature(transaction, record);
            item.AttributeTags = GetAttributeDefinitionTags(transaction, record);
            return item;
        }

        private static QtoBlockCatalogItem BuildItemFromReference(Transaction transaction, BlockReference blockReference, string blockName, string sourceLegendDwg, DateTime scannedAt)
        {
            QtoBlockCatalogItem item = new QtoBlockCatalogItem();
            item.BlockName = blockName;
            item.DisplayName = blockName;
            item.SourceLegendDwg = sourceLegendDwg;
            item.LastScannedAt = scannedAt;
            item.Status = QtoBlockCatalogStatus.Unconfigured;
            item.ReferenceCount = 0;
            item.IsDynamicBlock = blockReference.IsDynamicBlock;

            BlockTableRecord record = GetBlockTableRecord(transaction, blockReference);

            if (record != null)
            {
                item.DefinitionEntityCount = CountDefinitionEntities(record);
                item.IsAnonymous = record.IsAnonymous;
                item.IsFromExternalReference = record.IsFromExternalReference;
                item.DefinitionSignature = BuildDefinitionSignature(transaction, record);
                item.AttributeTags = GetAttributeDefinitionTags(transaction, record);
            }

            return item;
        }

        private static int CountDefinitionEntities(BlockTableRecord record)
        {
            int count = 0;

            foreach (ObjectId ignored in record)
            {
                count++;
            }

            return count;
        }

        private static List<string> GetAttributeDefinitionTags(Transaction transaction, BlockTableRecord record)
        {
            SortedSet<string> tags = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (ObjectId entityId in record)
            {
                AttributeDefinition attributeDefinition = transaction.GetObject(entityId, OpenMode.ForRead, false) as AttributeDefinition;

                if (attributeDefinition == null || string.IsNullOrWhiteSpace(attributeDefinition.Tag))
                {
                    continue;
                }

                tags.Add(attributeDefinition.Tag);
            }

            return tags.ToList();
        }

        private static string BuildDefinitionSignature(Transaction transaction, BlockTableRecord record)
        {
            List<string> parts = new List<string>();

            foreach (ObjectId entityId in record)
            {
                Entity entity = transaction.GetObject(entityId, OpenMode.ForRead, false) as Entity;

                if (entity == null)
                {
                    continue;
                }

                parts.Add(BuildEntitySignature(entity));
            }

            parts.Sort(StringComparer.OrdinalIgnoreCase);
            return string.Join("|", parts.ToArray());
        }

        private static string BuildEntitySignature(Entity entity)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append(entity.GetType().Name);
            builder.Append(':');
            builder.Append(entity.Layer ?? string.Empty);
            builder.Append(':');
            builder.Append(entity.ColorIndex.ToString(CultureInfo.InvariantCulture));
            builder.Append(':');
            builder.Append(entity.Linetype ?? string.Empty);
            builder.Append(':');
            builder.Append(GetEntityExtentsSignature(entity));
            return builder.ToString();
        }

        private static string GetEntityExtentsSignature(Entity entity)
        {
            try
            {
                Extents3d extents = entity.GeometricExtents;
                return Round(extents.MinPoint.X).ToString(CultureInfo.InvariantCulture)
                    + "," + Round(extents.MinPoint.Y).ToString(CultureInfo.InvariantCulture)
                    + "," + Round(extents.MaxPoint.X).ToString(CultureInfo.InvariantCulture)
                    + "," + Round(extents.MaxPoint.Y).ToString(CultureInfo.InvariantCulture);
            }
            catch (Autodesk.AutoCAD.Runtime.Exception)
            {
                return "no-extents";
            }
            catch (InvalidOperationException)
            {
                return "no-extents";
            }
        }

        private static void AddReferenceData(Transaction transaction, QtoBlockCatalogItem item, BlockReference blockReference, string blockName)
        {
            item.ReferenceCount++;
            item.IsDynamicBlock = item.IsDynamicBlock || blockReference.IsDynamicBlock;

            if (!string.IsNullOrWhiteSpace(blockReference.Layer) && !item.ReferenceLayers.Contains(blockReference.Layer, StringComparer.OrdinalIgnoreCase))
            {
                item.ReferenceLayers.Add(blockReference.Layer);
                item.ReferenceLayers.Sort(StringComparer.OrdinalIgnoreCase);
            }

            Dictionary<string, string> attributes = GetReferenceAttributes(transaction, blockReference);

            foreach (string tag in attributes.Keys)
            {
                if (!item.AttributeTags.Contains(tag, StringComparer.OrdinalIgnoreCase))
                {
                    item.AttributeTags.Add(tag);
                    item.AttributeTags.Sort(StringComparer.OrdinalIgnoreCase);
                }
            }

            if (item.ReferenceSamples.Count >= MaxReferenceSamplesPerBlock)
            {
                return;
            }

            Scale3d scale = blockReference.ScaleFactors;
            Point3d position = blockReference.Position;

            item.ReferenceSamples.Add(new QtoBlockReferenceSample
            {
                BlockName = blockName,
                Layer = blockReference.Layer ?? string.Empty,
                PositionX = Round(position.X),
                PositionY = Round(position.Y),
                PositionZ = Round(position.Z),
                ScaleX = Round(scale.X),
                ScaleY = Round(scale.Y),
                ScaleZ = Round(scale.Z),
                Rotation = Round(blockReference.Rotation),
                Attributes = attributes
            });
        }

        private static Dictionary<string, string> GetReferenceAttributes(Transaction transaction, BlockReference blockReference)
        {
            Dictionary<string, string> attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (ObjectId attributeId in blockReference.AttributeCollection)
            {
                AttributeReference attributeReference = transaction.GetObject(attributeId, OpenMode.ForRead, false) as AttributeReference;

                if (attributeReference == null || string.IsNullOrWhiteSpace(attributeReference.Tag))
                {
                    continue;
                }

                attributes[attributeReference.Tag] = attributeReference.TextString ?? string.Empty;
            }

            return attributes;
        }

        private static string GetEffectiveBlockName(Transaction transaction, BlockReference blockReference)
        {
            BlockTableRecord record = GetBlockTableRecord(transaction, blockReference);
            return record == null ? string.Empty : record.Name ?? string.Empty;
        }

        private static BlockTableRecord GetBlockTableRecord(Transaction transaction, BlockReference blockReference)
        {
            if (blockReference == null)
            {
                return null;
            }

            ObjectId recordId = blockReference.BlockTableRecord;

            if (blockReference.IsDynamicBlock)
            {
                recordId = blockReference.DynamicBlockTableRecord;
            }

            return transaction.GetObject(recordId, OpenMode.ForRead, false) as BlockTableRecord;
        }

        private static string BuildFingerprint(QtoBlockCatalogItem item)
        {
            StringBuilder builder = new StringBuilder();
            Append(builder, item.BlockName);
            Append(builder, item.DefinitionEntityCount.ToString(CultureInfo.InvariantCulture));
            Append(builder, item.DefinitionSignature);
            Append(builder, item.IsDynamicBlock ? "dynamic" : "static");
            Append(builder, item.IsFromExternalReference ? "xref" : "local");

            foreach (string tag in item.AttributeTags.OrderBy(value => value, StringComparer.OrdinalIgnoreCase))
            {
                Append(builder, "tag:" + tag);
            }

            foreach (string layer in item.ReferenceLayers.OrderBy(value => value, StringComparer.OrdinalIgnoreCase))
            {
                Append(builder, "layer:" + layer);
            }

            foreach (QtoBlockReferenceSample sample in item.ReferenceSamples)
            {
                Append(builder, "ref:" + sample.Layer);
                Append(builder, sample.ScaleX.ToString(CultureInfo.InvariantCulture));
                Append(builder, sample.ScaleY.ToString(CultureInfo.InvariantCulture));
                Append(builder, sample.ScaleZ.ToString(CultureInfo.InvariantCulture));
                Append(builder, sample.Rotation.ToString(CultureInfo.InvariantCulture));
            }

            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(builder.ToString()));
                return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
            }
        }

        private static void Append(StringBuilder builder, string value)
        {
            builder.Append(value ?? string.Empty);
            builder.Append('\n');
        }

        private static double Round(double value)
        {
            return Math.Round(value, 6);
        }
    }
}
