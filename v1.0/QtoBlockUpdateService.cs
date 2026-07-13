using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;

namespace QtoWirePlugin
{
    public sealed class QtoBlockUpdateService
    {
        public IList<QtoBlockUpdateCandidate> Analyze(Database projectDatabase, QtoBlockCatalog catalog)
        {
            QtoLegendDwgScanResult projectScan = new QtoLegendDwgScanner().ScanDatabase(projectDatabase, projectDatabase.Filename);
            Dictionary<string, QtoBlockCatalogItem> projectItems = projectScan.Items.ToDictionary(i => i.BlockName, StringComparer.OrdinalIgnoreCase);
            List<QtoBlockUpdateCandidate> result = new List<QtoBlockUpdateCandidate>();
            foreach (QtoBlockCatalogItem standard in catalog.Items.OrderBy(i => i.DisplayName ?? i.BlockName))
            {
                QtoBlockCatalogItem current;
                projectItems.TryGetValue(standard.BlockName ?? string.Empty, out current);
                string source = ResolveSource(catalog, standard);
                string status = current == null ? QtoBlockUpdateStatus.ProjectMissing
                    : standard.IsAnonymous || standard.IsFromExternalReference ? QtoBlockUpdateStatus.Unsupported
                    : string.IsNullOrWhiteSpace(source) || !File.Exists(source) ? QtoBlockUpdateStatus.MissingSource
                    : string.Equals(current.DefinitionSignature, standard.DefinitionSignature, StringComparison.OrdinalIgnoreCase) ? QtoBlockUpdateStatus.Current
                    : QtoBlockUpdateStatus.UpdateAvailable;
                result.Add(new QtoBlockUpdateCandidate
                {
                    Selected = status == QtoBlockUpdateStatus.UpdateAvailable,
                    CatalogId = standard.CatalogId, BlockName = standard.BlockName,
                    DisplayName = string.IsNullOrWhiteSpace(standard.DisplayName) ? standard.BlockName : standard.DisplayName,
                    Status = status, SourceDwg = source, ReferenceCount = current == null ? 0 : current.ReferenceCount,
                    AttributeDifference = DescribeTags(current, standard),
                    DynamicDifference = standard.IsDynamicBlock ? "動態圖塊：更新後依同名參數保留" : "靜態圖塊",
                    QtoImpact = "保留既有實例 QTO XData 與同步 ID",
                    Detail = status == QtoBlockUpdateStatus.UpdateAvailable ? "標準定義與專案定義不同，需人工確認後更新。" : status,
                    CatalogItem = standard
                });
            }
            return result;
        }

        public QtoBlockUpdateResult Apply(Database targetDatabase, IEnumerable<QtoBlockUpdateCandidate> selected)
        {
            List<QtoBlockUpdateCandidate> candidates = selected.Where(c => c != null && c.CanUpdate).ToList();
            QtoBlockUpdateResult result = new QtoBlockUpdateResult();
            if (candidates.Count == 0) return result;
            if (string.IsNullOrWhiteSpace(targetDatabase.Filename)) throw new InvalidOperationException("請先儲存 DWG，再更新標準圖塊。");

            foreach (IGrouping<string, QtoBlockUpdateCandidate> sourceGroup in candidates.GroupBy(c => c.SourceDwg, StringComparer.OrdinalIgnoreCase))
            {
                if (!File.Exists(sourceGroup.Key)) throw new FileNotFoundException("找不到標準圖例 DWG。", sourceGroup.Key);
                Dictionary<string, List<ReferenceState>> states = CaptureReferenceStates(targetDatabase, sourceGroup.Select(c => c.BlockName));
                using (Database sourceDatabase = new Database(false, true))
                {
                    sourceDatabase.ReadDwgFile(sourceGroup.Key, FileShare.ReadWrite, true, string.Empty);
                    sourceDatabase.CloseInput(true);
                    ObjectIdCollection ids = new ObjectIdCollection();
                    using (Transaction tr = sourceDatabase.TransactionManager.StartTransaction())
                    {
                        BlockTable table = (BlockTable)tr.GetObject(sourceDatabase.BlockTableId, OpenMode.ForRead);
                        foreach (QtoBlockUpdateCandidate candidate in sourceGroup)
                        {
                            if (!table.Has(candidate.BlockName)) throw new InvalidOperationException("標準圖例缺少圖塊：" + candidate.BlockName);
                            ids.Add(table[candidate.BlockName]);
                        }
                        tr.Commit();
                    }
                    sourceDatabase.WblockCloneObjects(ids, targetDatabase.BlockTableId, new IdMapping(), DuplicateRecordCloning.Replace, false);
                }
                RestoreReferences(targetDatabase, states, result);
                result.UpdatedDefinitionCount += sourceGroup.Count();
            }
            QtoBlockUpdateReviewStore.Save(targetDatabase, result.Messages);
            return result;
        }

        private static Dictionary<string, List<ReferenceState>> CaptureReferenceStates(Database database, IEnumerable<string> blockNames)
        {
            HashSet<string> names = new HashSet<string>(blockNames, StringComparer.OrdinalIgnoreCase);
            Dictionary<string, List<ReferenceState>> states = new Dictionary<string, List<ReferenceState>>(StringComparer.OrdinalIgnoreCase);
            using (Transaction tr = database.TransactionManager.StartTransaction())
            {
                BlockTable table = (BlockTable)tr.GetObject(database.BlockTableId, OpenMode.ForRead);
                foreach (ObjectId recordId in table)
                {
                    BlockTableRecord space = tr.GetObject(recordId, OpenMode.ForRead, false) as BlockTableRecord;
                    if (space == null || !space.IsLayout) continue;
                    foreach (ObjectId entityId in space)
                    {
                        BlockReference reference = tr.GetObject(entityId, OpenMode.ForRead, false) as BlockReference;
                        if (reference == null) continue;
                        BlockTableRecord definition = tr.GetObject(reference.IsDynamicBlock ? reference.DynamicBlockTableRecord : reference.BlockTableRecord, OpenMode.ForRead, false) as BlockTableRecord;
                        string name = definition == null ? string.Empty : definition.Name;
                        if (!names.Contains(name)) continue;
                        List<ReferenceState> list;
                        if (!states.TryGetValue(name, out list)) { list = new List<ReferenceState>(); states[name] = list; }
                        ReferenceState state = new ReferenceState { ObjectId = entityId };
                        foreach (ObjectId attributeId in reference.AttributeCollection)
                        {
                            AttributeReference attribute = tr.GetObject(attributeId, OpenMode.ForRead, false) as AttributeReference;
                            if (attribute != null) state.Attributes[attribute.Tag ?? string.Empty] = attribute.TextString ?? string.Empty;
                        }
                        if (reference.IsDynamicBlock)
                        {
                            foreach (DynamicBlockReferenceProperty property in reference.DynamicBlockReferencePropertyCollection)
                                if (!property.ReadOnly) state.DynamicValues[property.PropertyName ?? string.Empty] = property.Value;
                        }
                        list.Add(state);
                    }
                }
                tr.Commit();
            }
            return states;
        }

        private static void RestoreReferences(Database database, Dictionary<string, List<ReferenceState>> states, QtoBlockUpdateResult result)
        {
            using (Transaction tr = database.TransactionManager.StartTransaction())
            {
                foreach (List<ReferenceState> list in states.Values)
                foreach (ReferenceState state in list)
                {
                    BlockReference reference = tr.GetObject(state.ObjectId, OpenMode.ForWrite, false) as BlockReference;
                    if (reference == null) continue;
                    BlockTableRecord definition = tr.GetObject(reference.IsDynamicBlock ? reference.DynamicBlockTableRecord : reference.BlockTableRecord, OpenMode.ForRead, false) as BlockTableRecord;
                    Dictionary<string, AttributeReference> existing = new Dictionary<string, AttributeReference>(StringComparer.OrdinalIgnoreCase);
                    foreach (ObjectId id in reference.AttributeCollection)
                    {
                        AttributeReference attribute = tr.GetObject(id, OpenMode.ForWrite, false) as AttributeReference;
                        if (attribute != null) existing[attribute.Tag ?? string.Empty] = attribute;
                    }
                    HashSet<string> standardTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (ObjectId id in definition)
                    {
                        AttributeDefinition attributeDefinition = tr.GetObject(id, OpenMode.ForRead, false) as AttributeDefinition;
                        if (attributeDefinition == null || attributeDefinition.Constant) continue;
                        standardTags.Add(attributeDefinition.Tag ?? string.Empty);
                        AttributeReference attribute;
                        string saved;
                        if (existing.TryGetValue(attributeDefinition.Tag ?? string.Empty, out attribute))
                        {
                            state.Attributes.TryGetValue(attributeDefinition.Tag ?? string.Empty, out saved);
                            attribute.SetAttributeFromBlock(attributeDefinition, reference.BlockTransform);
                            if (saved != null) attribute.TextString = saved;
                        }
                        else
                        {
                            attribute = new AttributeReference();
                            attribute.SetAttributeFromBlock(attributeDefinition, reference.BlockTransform);
                            reference.AttributeCollection.AppendAttribute(attribute);
                            tr.AddNewlyCreatedDBObject(attribute, true);
                        }
                    }
                    foreach (string removed in state.Attributes.Keys.Where(k => !standardTags.Contains(k)))
                    {
                        result.ReviewCount++;
                        result.Messages.Add("屬性標籤已不在標準圖塊中，保留既有實例值供人工確認：" + removed);
                    }
                    if (reference.IsDynamicBlock)
                    {
                        foreach (DynamicBlockReferenceProperty property in reference.DynamicBlockReferencePropertyCollection)
                        {
                            object value;
                            if (!property.ReadOnly && state.DynamicValues.TryGetValue(property.PropertyName ?? string.Empty, out value))
                            {
                                try { property.Value = value; } catch { result.ReviewCount++; }
                            }
                        }
                    }
                    reference.RecordGraphicsModified(true);
                    result.UpdatedReferenceCount++;
                }
                tr.Commit();
            }
        }

        private static string ResolveSource(QtoBlockCatalog catalog, QtoBlockCatalogItem item)
        {
            if (!string.IsNullOrWhiteSpace(item.SourceLegendDwg)) return item.SourceLegendDwg;
            return catalog == null ? string.Empty : catalog.SourceLegendDwg;
        }

        private static string DescribeTags(QtoBlockCatalogItem current, QtoBlockCatalogItem standard)
        {
            if (current == null) return "專案沒有此圖塊";
            IEnumerable<string> added = standard.AttributeTags.Except(current.AttributeTags, StringComparer.OrdinalIgnoreCase);
            IEnumerable<string> removed = current.AttributeTags.Except(standard.AttributeTags, StringComparer.OrdinalIgnoreCase);
            return "新增：" + string.Join(",", added) + "；移除：" + string.Join(",", removed);
        }

        private sealed class ReferenceState
        {
            public ReferenceState() { Attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase); DynamicValues = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase); }
            public ObjectId ObjectId { get; set; }
            public Dictionary<string, string> Attributes { get; private set; }
            public Dictionary<string, object> DynamicValues { get; private set; }
        }
    }
}
