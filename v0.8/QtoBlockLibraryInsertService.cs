using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace QtoWirePlugin
{
    public enum QtoBlockInsertionBaseMode
    {
        Origin,
        Center
    }

    public class QtoBlockInsertResult
    {
        public bool Success { get; set; }
        public ObjectId InsertedObjectId { get; set; }
        public string UserMessage { get; set; }
        public string TechnicalMessage { get; set; }
    }

    public class QtoBlockLibraryInsertService
    {
        public QtoBlockInsertResult InsertCatalogBlock(Database database, QtoBlockCatalog catalog, QtoBlockCatalogItem item, Point3d insertionPoint, QtoBlockInsertionBaseMode insertionBaseMode)
        {
            if (database == null)
            {
                throw new ArgumentNullException("database");
            }

            if (catalog == null)
            {
                throw new ArgumentNullException("catalog");
            }

            if (item == null)
            {
                throw new ArgumentNullException("item");
            }

            if (string.IsNullOrWhiteSpace(item.BlockName))
            {
                throw new InvalidOperationException("圖塊資料缺少 BlockName，無法插入。");
            }

            string libraryDwgPath = ResolveLibraryDwgPath(catalog, item);
            if (string.IsNullOrWhiteSpace(libraryDwgPath) || !File.Exists(libraryDwgPath))
            {
                throw new FileNotFoundException("找不到圖例 DWG，請先重建圖塊庫或補上來源路徑。", libraryDwgPath ?? string.Empty);
            }

            ObjectId blockDefinitionId = EnsureBlockDefinition(database, libraryDwgPath, item.BlockName);
            ObjectId insertedId;

            using (Transaction transaction = database.TransactionManager.StartTransaction())
            {
                BlockTable blockTable = (BlockTable)transaction.GetObject(database.BlockTableId, OpenMode.ForRead);
                BlockTableRecord modelSpace = (BlockTableRecord)transaction.GetObject(blockTable[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                BlockTableRecord definition = (BlockTableRecord)transaction.GetObject(blockDefinitionId, OpenMode.ForRead);

                Point3d blockPosition = ResolveBlockPosition(transaction, definition, insertionPoint, insertionBaseMode);
                BlockReference blockReference = new BlockReference(blockPosition, blockDefinitionId);
                string targetLayer = ResolveLayerName(item);
                QtoLayerHelper.EnsureLayerExists(database, transaction, targetLayer);
                blockReference.Layer = targetLayer;

                insertedId = modelSpace.AppendEntity(blockReference);
                transaction.AddNewlyCreatedDBObject(blockReference, true);

                AddDefaultAttributes(transaction, blockReference, definition);
                QtoXDataHelper.SetXData(blockReference, database, transaction, BuildXData(item));

                transaction.Commit();
            }

            return new QtoBlockInsertResult
            {
                Success = true,
                InsertedObjectId = insertedId,
                UserMessage = "已插入標準圖塊：" + GetDisplayName(item),
                TechnicalMessage = "Inserted catalog block. CatalogId=" + (item.CatalogId ?? string.Empty) + "; BlockName=" + item.BlockName + "; insertionBase=" + insertionBaseMode + "."
            };
        }

        private static Point3d ResolveBlockPosition(Transaction transaction, BlockTableRecord definition, Point3d targetPoint, QtoBlockInsertionBaseMode insertionBaseMode)
        {
            if (insertionBaseMode != QtoBlockInsertionBaseMode.Center)
            {
                return targetPoint;
            }

            Extents3d? extents = GetDefinitionExtents(transaction, definition);
            if (!extents.HasValue)
            {
                return targetPoint;
            }

            Point3d min = extents.Value.MinPoint;
            Point3d max = extents.Value.MaxPoint;
            double centerX = (min.X + max.X) / 2.0;
            double centerY = (min.Y + max.Y) / 2.0;
            double centerZ = (min.Z + max.Z) / 2.0;
            return new Point3d(targetPoint.X - centerX, targetPoint.Y - centerY, targetPoint.Z - centerZ);
        }

        private static Extents3d? GetDefinitionExtents(Transaction transaction, BlockTableRecord definition)
        {
            Extents3d? merged = null;

            foreach (ObjectId objectId in definition)
            {
                Entity entity = transaction.GetObject(objectId, OpenMode.ForRead, false) as Entity;
                if (entity == null)
                {
                    continue;
                }

                try
                {
                    Extents3d entityExtents = entity.GeometricExtents;
                    if (!merged.HasValue)
                    {
                        merged = entityExtents;
                    }
                    else
                    {
                        Extents3d value = merged.Value;
                        value.AddExtents(entityExtents);
                        merged = value;
                    }
                }
                catch (Autodesk.AutoCAD.Runtime.Exception)
                {
                }
                catch (InvalidOperationException)
                {
                }
            }

            return merged;
        }

        private static ObjectId EnsureBlockDefinition(Database targetDatabase, string sourceDwgPath, string blockName)
        {
            using (Transaction transaction = targetDatabase.TransactionManager.StartTransaction())
            {
                BlockTable blockTable = (BlockTable)transaction.GetObject(targetDatabase.BlockTableId, OpenMode.ForRead);
                if (blockTable.Has(blockName))
                {
                    ObjectId existingId = blockTable[blockName];
                    transaction.Commit();
                    return existingId;
                }

                transaction.Commit();
            }

            using (Database sourceDatabase = new Database(false, true))
            {
                sourceDatabase.ReadDwgFile(sourceDwgPath, FileShare.ReadWrite, true, string.Empty);
                sourceDatabase.CloseInput(true);

                ObjectId sourceBlockId;
                using (Transaction sourceTransaction = sourceDatabase.TransactionManager.StartTransaction())
                {
                    BlockTable sourceBlockTable = (BlockTable)sourceTransaction.GetObject(sourceDatabase.BlockTableId, OpenMode.ForRead);
                    if (!sourceBlockTable.Has(blockName))
                    {
                        throw new InvalidOperationException("圖例 DWG 中找不到圖塊：" + blockName);
                    }

                    sourceBlockId = sourceBlockTable[blockName];
                    sourceTransaction.Commit();
                }

                ObjectIdCollection sourceIds = new ObjectIdCollection();
                sourceIds.Add(sourceBlockId);
                IdMapping mapping = new IdMapping();
                sourceDatabase.WblockCloneObjects(sourceIds, targetDatabase.BlockTableId, mapping, DuplicateRecordCloning.Replace, false);
            }

            using (Transaction transaction = targetDatabase.TransactionManager.StartTransaction())
            {
                BlockTable blockTable = (BlockTable)transaction.GetObject(targetDatabase.BlockTableId, OpenMode.ForRead);
                ObjectId importedId = blockTable[blockName];
                transaction.Commit();
                return importedId;
            }
        }

        private static void AddDefaultAttributes(Transaction transaction, BlockReference blockReference, BlockTableRecord definition)
        {
            foreach (ObjectId objectId in definition)
            {
                AttributeDefinition attributeDefinition = transaction.GetObject(objectId, OpenMode.ForRead, false) as AttributeDefinition;
                if (attributeDefinition == null || attributeDefinition.Constant)
                {
                    continue;
                }

                AttributeReference attributeReference = new AttributeReference();
                attributeReference.SetAttributeFromBlock(attributeDefinition, blockReference.BlockTransform);
                attributeReference.TextString = attributeDefinition.TextString ?? string.Empty;
                blockReference.AttributeCollection.AppendAttribute(attributeReference);
                transaction.AddNewlyCreatedDBObject(attributeReference, true);
            }
        }

        private static Dictionary<string, string> BuildXData(QtoBlockCatalogItem item)
        {
            Dictionary<string, string> data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            SetIfNotEmpty(data, QtoXDataHelper.KeyQtoType, item.QtoType);
            SetIfNotEmpty(data, QtoXDataHelper.KeySystemCode, item.SystemCode);
            SetIfNotEmpty(data, QtoXDataHelper.KeySystem, item.SystemCode);
            SetIfNotEmpty(data, QtoXDataHelper.KeyEquipmentTypeCode, item.EquipmentTypeCode);
            SetIfNotEmpty(data, QtoXDataHelper.KeyQuantityBasis, item.QuantityBasis);
            SetIfNotEmpty(data, QtoXDataHelper.KeyQtoUnit, item.Unit);
            SetIfNotEmpty(data, QtoXDataHelper.KeyCableType, item.DefaultCableType);
            SetIfNotEmpty(data, QtoXDataHelper.KeyConduitType, item.DefaultConduitType);
            SetIfNotEmpty(data, QtoXDataHelper.KeyConduitSize, item.DefaultConduitSize);
            SetIfNotEmpty(data, QtoXDataHelper.KeyBudgetItemKey, item.BudgetItemKey);
            SetIfNotEmpty(data, QtoXDataHelper.KeyQtoSourceCatalogId, item.CatalogId);
            SetIfNotEmpty(data, QtoXDataHelper.KeyQtoCatalogVersion, item.Version);
            data[QtoXDataHelper.KeyQtoSyncId] = QtoSyncIdService.CreateSyncId();
            data[QtoXDataHelper.KeyQtoSyncStatus] = QtoSyncStatus.Active;
            data[QtoXDataHelper.KeyQtoLastModifiedAt] = DateTime.UtcNow.ToString("o");
            data[QtoXDataHelper.KeySourceRule] = "catalog_insert";
            return data;
        }

        private static void SetIfNotEmpty(Dictionary<string, string> data, string key, string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                data[key] = value.Trim();
            }
        }

        private static string ResolveLibraryDwgPath(QtoBlockCatalog catalog, QtoBlockCatalogItem item)
        {
            if (!string.IsNullOrWhiteSpace(item.SourceLegendDwg))
            {
                return item.SourceLegendDwg;
            }

            return catalog.SourceLegendDwg ?? string.Empty;
        }

        private static string ResolveLayerName(QtoBlockCatalogItem item)
        {
            if (!string.IsNullOrWhiteSpace(item.DefaultLayer))
            {
                return item.DefaultLayer.Trim();
            }

            if (item.ReferenceLayers != null)
            {
                string firstLayer = item.ReferenceLayers.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
                if (!string.IsNullOrWhiteSpace(firstLayer))
                {
                    return firstLayer.Trim();
                }
            }

            if (!string.IsNullOrWhiteSpace(item.SystemCode))
            {
                return "QTO_" + item.SystemCode.Trim().ToUpperInvariant();
            }

            return "QTO_BLOCK";
        }

        private static string GetDisplayName(QtoBlockCatalogItem item)
        {
            if (!string.IsNullOrWhiteSpace(item.DisplayName))
            {
                return item.DisplayName;
            }

            return item.BlockName ?? string.Empty;
        }
    }
}
