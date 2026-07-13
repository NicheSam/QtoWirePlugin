using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace QtoWirePlugin
{
    public sealed class QtoScopeApplyResult
    {
        public int ScopeCount { get; set; }
        public int ScannedQtoCount { get; set; }
        public int UpdatedCount { get; set; }
        public int ClearedCount { get; set; }
        public int UnchangedCount { get; set; }
        public int SkippedNoQtoCount { get; set; }
        public int SkippedUnsupportedCount { get; set; }
        public int ErrorCount { get; set; }
        public string ErrorMessage { get; set; }

        public string ToUserSummary()
        {
            return "範圍框：" + ScopeCount.ToString("0", CultureInfo.InvariantCulture)
                + "\r\n掃描 QTO 物件：" + ScannedQtoCount.ToString("0", CultureInfo.InvariantCulture)
                + "\r\n更新：" + UpdatedCount.ToString("0", CultureInfo.InvariantCulture)
                + "\r\n清空：" + ClearedCount.ToString("0", CultureInfo.InvariantCulture)
                + "\r\n不變：" + UnchangedCount.ToString("0", CultureInfo.InvariantCulture)
                + "\r\n跳過無 QTO：" + SkippedNoQtoCount.ToString("0", CultureInfo.InvariantCulture)
                + "\r\n跳過不支援曲線：" + SkippedUnsupportedCount.ToString("0", CultureInfo.InvariantCulture)
                + (ErrorCount > 0 ? "\r\n錯誤：" + ErrorCount.ToString("0", CultureInfo.InvariantCulture) + "\r\n" + ErrorMessage : string.Empty);
        }
    }

    public sealed class QtoScopeDefinition
    {
        public ObjectId ObjectId { get; set; }
        public string Kind { get; set; }
        public string Value { get; set; }
        public string Name { get; set; }
        public long PriorityTicks { get; set; }
        public Point3dCollection Vertices { get; set; }
        public Extents3d Extents { get; set; }
    }

    public static class QtoScopeService
    {
        private static bool dynamicEnabled = true;
        private static readonly object scopeCacheGate = new object();
        private static readonly Dictionary<Database, List<QtoScopeDefinition>> scopeCache = new Dictionary<Database, List<QtoScopeDefinition>>();

        public static bool DynamicEnabled
        {
            get { return dynamicEnabled; }
            set { dynamicEnabled = value; }
        }

        public static bool IsScopeEntity(Entity entity)
        {
            if (entity == null)
            {
                return false;
            }

            string kind = QtoXDataHelper.GetXDataValue(entity, QtoXDataHelper.KeyQtoScopeKind);
            return IsScopeKind(kind);
        }

        public static bool HasScopeChange(IEnumerable<QtoCadChange> changes)
        {
            if (changes == null)
            {
                return false;
            }

            foreach (QtoCadChange change in changes)
            {
                if (change == null || change.XDataSnapshot == null)
                {
                    continue;
                }

                string kind;
                if (change.XDataSnapshot.TryGetValue(QtoXDataHelper.KeyQtoScopeKind, out kind) && IsScopeKind(kind))
                {
                    return true;
                }
            }

            return false;
        }

        public static QtoScopeApplyResult DefineScope(Database database, ObjectId polylineId, string kind, string value, string name)
        {
            QtoScopeApplyResult result = new QtoScopeApplyResult();
            if (database == null || polylineId.IsNull)
            {
                result.ErrorCount = 1;
                result.ErrorMessage = "未指定有效的範圍框。";
                return result;
            }

            using (Transaction tr = database.TransactionManager.StartTransaction())
            {
                Polyline polyline = tr.GetObject(polylineId, OpenMode.ForWrite, false) as Polyline;
                if (!IsValidScopePolyline(polyline))
                {
                    result.ErrorCount = 1;
                    result.ErrorMessage = "範圍框必須是閉合 Polyline，且至少有 3 個頂點。";
                    tr.Commit();
                    return result;
                }

                Dictionary<string, string> data = QtoXDataHelper.GetXData(polyline);
                data[QtoXDataHelper.KeyQtoScopeKind] = NormalizeScopeKind(kind);
                data[QtoXDataHelper.KeyQtoScopeValue] = value ?? string.Empty;
                data[QtoXDataHelper.KeyQtoScopeName] = string.IsNullOrWhiteSpace(name) ? BuildDefaultScopeName(kind, value) : name.Trim();
                data[QtoXDataHelper.KeyQtoScopePriorityTicks] = DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture);
                QtoXDataHelper.SetXData(polyline, database, tr, data);
                tr.Commit();
            }

            InvalidateScopeCache(database);

            result.ScopeCount = 1;
            return result;
        }

        public static QtoScopeApplyResult ClearScopes(Database database, IList<ObjectId> scopeIds)
        {
            QtoScopeApplyResult result = new QtoScopeApplyResult();
            if (database == null || scopeIds == null || scopeIds.Count == 0)
            {
                return result;
            }

            bool clearedFloor = false;
            bool clearedSystem = false;
            using (Transaction tr = database.TransactionManager.StartTransaction())
            {
                foreach (ObjectId objectId in scopeIds)
                {
                    Entity entity = tr.GetObject(objectId, OpenMode.ForWrite, false) as Entity;
                    if (entity == null || !IsScopeEntity(entity))
                    {
                        continue;
                    }

                    Dictionary<string, string> data = QtoXDataHelper.GetXData(entity);
                    string kind = GetValue(data, QtoXDataHelper.KeyQtoScopeKind);
                    clearedFloor = clearedFloor || string.Equals(kind, QtoXDataHelper.ScopeKindFloor, StringComparison.OrdinalIgnoreCase);
                    clearedSystem = clearedSystem || string.Equals(kind, QtoXDataHelper.ScopeKindSystem, StringComparison.OrdinalIgnoreCase);
                    data.Remove(QtoXDataHelper.KeyQtoScopeKind);
                    data.Remove(QtoXDataHelper.KeyQtoScopeValue);
                    data.Remove(QtoXDataHelper.KeyQtoScopeName);
                    data.Remove(QtoXDataHelper.KeyQtoScopePriorityTicks);
                    QtoXDataHelper.SetXData(entity, database, tr, data);
                    result.ScopeCount++;
                }

                tr.Commit();
            }

            InvalidateScopeCache(database);

            QtoScopeApplyResult refresh = ApplyScopes(database, null, null, false, clearedFloor, clearedSystem);
            result.ScannedQtoCount = refresh.ScannedQtoCount;
            result.UpdatedCount = refresh.UpdatedCount;
            result.ClearedCount = refresh.ClearedCount;
            result.UnchangedCount = refresh.UnchangedCount;
            result.SkippedNoQtoCount = refresh.SkippedNoQtoCount;
            result.SkippedUnsupportedCount = refresh.SkippedUnsupportedCount;
            result.ErrorCount = refresh.ErrorCount;
            result.ErrorMessage = refresh.ErrorMessage;

            return result;
        }

        public static QtoScopeApplyResult ApplySelectedScopes(Database database, IList<ObjectId> scopeIds, bool previewOnly)
        {
            return ApplyScopes(database, scopeIds, null, previewOnly, false, false);
        }

        public static QtoScopeApplyResult ApplyAllScopes(Database database, bool previewOnly)
        {
            InvalidateScopeCache(database);
            return ApplyScopes(database, null, null, previewOnly, false, false);
        }

        public static IList<QtoScopeDefinition> GetScopes(Database database)
        {
            if (database == null) return new List<QtoScopeDefinition>();
            using (Transaction tr = database.TransactionManager.StartOpenCloseTransaction())
            {
                BlockTable table = (BlockTable)tr.GetObject(database.BlockTableId, OpenMode.ForRead);
                BlockTableRecord modelSpace = (BlockTableRecord)tr.GetObject(table[BlockTableRecord.ModelSpace], OpenMode.ForRead);
                return LoadScopesFromModelSpaceCached(database, tr, modelSpace).ToList();
            }
        }

        public static QtoScopeApplyResult ApplyDynamic(Database database, IEnumerable<QtoCadChange> changes)
        {
            if (!DynamicEnabled)
            {
                return new QtoScopeApplyResult();
            }

            List<QtoCadChange> changeList = changes == null ? new List<QtoCadChange>() : changes.Where(change => change != null).ToList();
            List<ObjectId> changedObjectIds = new List<ObjectId>();
            bool hasScopeChange = HasScopeChange(changeList);
            if (hasScopeChange)
            {
                bool changedFloor = changeList.Any(change => IsSnapshotKind(change, QtoXDataHelper.ScopeKindFloor));
                bool changedSystem = changeList.Any(change => IsSnapshotKind(change, QtoXDataHelper.ScopeKindSystem));
                InvalidateScopeCache(database);
                return ApplyScopes(database, null, null, false, changedFloor, changedSystem);
            }

            if (changeList.Count > 0)
            {
                foreach (QtoCadChange change in changeList)
                {
                    if (change == null || change.Kind == QtoCadChangeKind.Deleted || change.Kind == QtoCadChangeKind.Error)
                    {
                        continue;
                    }

                    if (!change.ObjectId.IsNull && !string.IsNullOrWhiteSpace(change.QtoType))
                    {
                        changedObjectIds.Add(change.ObjectId);
                    }
                }
            }

            if (changedObjectIds.Count == 0)
            {
                return new QtoScopeApplyResult();
            }

            return ApplyScopes(database, null, changedObjectIds, false, false, false);
        }

        private static QtoScopeApplyResult ApplyScopes(Database database, IList<ObjectId> explicitScopeIds, IList<ObjectId> explicitTargetIds, bool previewOnly, bool forceManageFloor, bool forceManageSystem)
        {
            QtoScopeApplyResult result = new QtoScopeApplyResult();
            if (database == null)
            {
                result.ErrorCount = 1;
                result.ErrorMessage = "沒有可用的資料庫。";
                return result;
            }

            using (Transaction tr = database.TransactionManager.StartTransaction())
            {
                BlockTableRecord modelSpace = GetModelSpace(database, tr);
                bool isSelectedScopeApply = explicitScopeIds != null && explicitScopeIds.Count > 0;
                List<QtoScopeDefinition> scopes = isSelectedScopeApply
                    ? LoadScopes(database, tr, explicitScopeIds)
                    : LoadScopesFromModelSpaceCached(database, tr, modelSpace);

                result.ScopeCount = scopes.Count;
                bool manageFloor = forceManageFloor || scopes.Any(scope => string.Equals(scope.Kind, QtoXDataHelper.ScopeKindFloor, StringComparison.OrdinalIgnoreCase));
                bool manageSystem = forceManageSystem || scopes.Any(scope => string.Equals(scope.Kind, QtoXDataHelper.ScopeKindSystem, StringComparison.OrdinalIgnoreCase));
                if (!manageFloor && !manageSystem)
                {
                    tr.Commit();
                    return result;
                }

                HashSet<ObjectId> explicitTargets = explicitTargetIds == null
                    ? null
                    : new HashSet<ObjectId>(explicitTargetIds);

                foreach (ObjectId objectId in modelSpace)
                {
                    if (explicitTargets != null && !explicitTargets.Contains(objectId))
                    {
                        continue;
                    }

                    Entity entity = tr.GetObject(objectId, OpenMode.ForRead, false) as Entity;
                    if (entity == null || IsScopeEntity(entity))
                    {
                        continue;
                    }

                    Dictionary<string, string> data = QtoXDataHelper.GetXData(entity);
                    string qtoType = GetValue(data, QtoXDataHelper.KeyQtoType);
                    if (string.IsNullOrWhiteSpace(qtoType))
                    {
                        result.SkippedNoQtoCount++;
                        continue;
                    }

                    result.ScannedQtoCount++;

                    bool changed = false;
                    if (manageFloor)
                    {
                        bool unsupported;
                        QtoScopeDefinition floorScope = FindWinningScope(entity, scopes, QtoXDataHelper.ScopeKindFloor, out unsupported);
                        if (unsupported)
                        {
                            result.SkippedUnsupportedCount++;
                            continue;
                        }

                        if (floorScope != null)
                        {
                            changed = ApplyValue(data, QtoXDataHelper.KeyFloor, floorScope.Value, result);
                        }
                        else if (!isSelectedScopeApply)
                        {
                            changed = ApplyValue(data, QtoXDataHelper.KeyFloor, string.Empty, result);
                        }
                    }

                    if (manageSystem)
                    {
                        bool unsupported;
                        QtoScopeDefinition systemScope = FindWinningScope(entity, scopes, QtoXDataHelper.ScopeKindSystem, out unsupported);
                        if (unsupported)
                        {
                            result.SkippedUnsupportedCount++;
                            continue;
                        }

                        if (systemScope != null)
                        {
                            changed = ApplyValue(data, QtoXDataHelper.KeySystemCode, systemScope.Value, result) || changed;
                        }
                        else if (!isSelectedScopeApply)
                        {
                            changed = ApplyValue(data, QtoXDataHelper.KeySystemCode, string.Empty, result) || changed;
                        }
                    }

                    if (changed)
                    {
                        data[QtoXDataHelper.KeyQtoLastModifiedAt] = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
                        if (!previewOnly)
                        {
                            if (!entity.IsWriteEnabled)
                            {
                                entity.UpgradeOpen();
                            }

                            QtoXDataHelper.SetXData(entity, database, tr, data);
                        }
                    }
                    else
                    {
                        result.UnchangedCount++;
                    }
                }

                tr.Commit();
            }

                return result;
        }

        private static bool IsSnapshotKind(QtoCadChange change, string kind)
        {
            if (change == null || change.XDataSnapshot == null) return false;
            string value;
            return change.XDataSnapshot.TryGetValue(QtoXDataHelper.KeyQtoScopeKind, out value)
                && string.Equals(value, kind, StringComparison.OrdinalIgnoreCase);
        }

        private static bool ApplyValue(Dictionary<string, string> data, string key, string targetValue, QtoScopeApplyResult result)
        {
            string current = GetValue(data, key);
            targetValue = targetValue ?? string.Empty;
            if (string.Equals(current ?? string.Empty, targetValue, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(targetValue))
            {
                data[key] = string.Empty;
                result.ClearedCount++;
            }
            else
            {
                data[key] = targetValue;
                result.UpdatedCount++;
            }

            return true;
        }

        private static QtoScopeDefinition FindWinningScope(Entity entity, IEnumerable<QtoScopeDefinition> scopes, string kind, out bool unsupported)
        {
            unsupported = false;
            QtoScopeDefinition winner = null;

            foreach (QtoScopeDefinition scope in scopes)
            {
                if (!string.Equals(scope.Kind, kind, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                bool inside;
                if (!TryIsEntityInsideScope(entity, scope, out inside))
                {
                    unsupported = true;
                    return null;
                }

                if (!inside)
                {
                    continue;
                }

                if (winner == null || scope.PriorityTicks >= winner.PriorityTicks)
                {
                    winner = scope;
                }
            }

            return winner;
        }

        private static bool TryIsEntityInsideScope(Entity entity, QtoScopeDefinition scope, out bool inside)
        {
            inside = false;

            BlockReference block = entity as BlockReference;
            if (block != null)
            {
                inside = IsPointInsideBoundary(Flatten(block.Position), scope);
                return true;
            }

            Line line = entity as Line;
            if (line != null)
            {
                inside = IsPointInsideBoundary(Flatten(line.StartPoint), scope)
                    && IsPointInsideBoundary(Flatten(line.EndPoint), scope);
                return true;
            }

            Polyline polyline = entity as Polyline;
            if (polyline != null)
            {
                inside = IsPolylineFullyInsideScope(polyline, scope);
                return true;
            }

            return !(entity is Curve);
        }

        private static bool IsPolylineFullyInsideScope(Polyline polyline, QtoScopeDefinition scope)
        {
            if (polyline == null || polyline.NumberOfVertices == 0)
            {
                return false;
            }

            for (int i = 0; i < polyline.NumberOfVertices; i++)
            {
                if (!IsPointInsideBoundary(Flatten(polyline.GetPoint3dAt(i)), scope))
                {
                    return false;
                }
            }

            for (int i = 0; i < polyline.NumberOfVertices - 1; i++)
            {
                Point3d start = Flatten(polyline.GetPoint3dAt(i));
                Point3d end = Flatten(polyline.GetPoint3dAt(i + 1));
                Point3d mid = new Point3d((start.X + end.X) / 2.0, (start.Y + end.Y) / 2.0, 0.0);
                if (!IsPointInsideBoundary(mid, scope))
                {
                    return false;
                }
            }

            if (polyline.Closed && polyline.NumberOfVertices > 2)
            {
                Point3d start = Flatten(polyline.GetPoint3dAt(polyline.NumberOfVertices - 1));
                Point3d end = Flatten(polyline.GetPoint3dAt(0));
                Point3d mid = new Point3d((start.X + end.X) / 2.0, (start.Y + end.Y) / 2.0, 0.0);
                if (!IsPointInsideBoundary(mid, scope))
                {
                    return false;
                }
            }

            return true;
        }

        private static List<QtoScopeDefinition> LoadScopesFromModelSpace(Transaction tr, BlockTableRecord modelSpace)
        {
            List<ObjectId> ids = new List<ObjectId>();
            foreach (ObjectId objectId in modelSpace)
            {
                ids.Add(objectId);
            }

            return LoadScopes(null, tr, ids);
        }

        private static List<QtoScopeDefinition> LoadScopesFromModelSpaceCached(Database database, Transaction tr, BlockTableRecord modelSpace)
        {
            if (database != null)
            {
                lock (scopeCacheGate)
                {
                    List<QtoScopeDefinition> cached;
                    if (scopeCache.TryGetValue(database, out cached))
                    {
                        return cached;
                    }
                }
            }

            List<QtoScopeDefinition> scopes = LoadScopesFromModelSpace(tr, modelSpace);
            if (database != null)
            {
                lock (scopeCacheGate)
                {
                    scopeCache[database] = scopes;
                }
            }

            return scopes;
        }

        public static void InvalidateScopeCache(Database database)
        {
            if (database == null)
            {
                return;
            }

            lock (scopeCacheGate)
            {
                scopeCache.Remove(database);
            }
        }

        private static List<QtoScopeDefinition> LoadScopes(Database database, Transaction tr, IList<ObjectId> objectIds)
        {
            List<QtoScopeDefinition> scopes = new List<QtoScopeDefinition>();
            if (objectIds == null)
            {
                return scopes;
            }

            foreach (ObjectId objectId in objectIds)
            {
                Polyline polyline = tr.GetObject(objectId, OpenMode.ForRead, false) as Polyline;
                if (!IsValidScopePolyline(polyline))
                {
                    continue;
                }

                Dictionary<string, string> data = QtoXDataHelper.GetXData(polyline);
                string kind = NormalizeScopeKind(GetValue(data, QtoXDataHelper.KeyQtoScopeKind));
                string value = GetValue(data, QtoXDataHelper.KeyQtoScopeValue);
                if (!IsScopeKind(kind) || string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                QtoScopeDefinition scope = new QtoScopeDefinition();
                scope.ObjectId = objectId;
                scope.Kind = kind;
                scope.Value = value;
                scope.Name = GetValue(data, QtoXDataHelper.KeyQtoScopeName);
                scope.PriorityTicks = ParseLong(GetValue(data, QtoXDataHelper.KeyQtoScopePriorityTicks));
                PopulateBoundary(polyline, scope);
                scopes.Add(scope);
            }

            scopes.Sort(delegate (QtoScopeDefinition left, QtoScopeDefinition right)
            {
                int kindCompare = string.Compare(left.Kind, right.Kind, StringComparison.OrdinalIgnoreCase);
                if (kindCompare != 0)
                {
                    return kindCompare;
                }

                return left.PriorityTicks.CompareTo(right.PriorityTicks);
            });

            return scopes;
        }

        private static void PopulateBoundary(Polyline polyline, QtoScopeDefinition scope)
        {
            Point3dCollection vertices = new Point3dCollection();
            Extents3d extents = new Extents3d();

            for (int i = 0; i < polyline.NumberOfVertices; i++)
            {
                Point3d point = Flatten(polyline.GetPoint3dAt(i));
                vertices.Add(point);
                if (i == 0)
                {
                    extents = new Extents3d(point, point);
                }
                else
                {
                    extents.AddPoint(point);
                }
            }

            scope.Vertices = vertices;
            scope.Extents = extents;
        }

        private static bool IsPointInsideBoundary(Point3d point, QtoScopeDefinition scope)
        {
            if (scope == null || scope.Vertices == null || scope.Vertices.Count < 3)
            {
                return false;
            }

            Point3d flatPoint = Flatten(point);
            Extents3d extents = scope.Extents;

            if (flatPoint.X < extents.MinPoint.X || flatPoint.X > extents.MaxPoint.X || flatPoint.Y < extents.MinPoint.Y || flatPoint.Y > extents.MaxPoint.Y)
            {
                return false;
            }

            bool inside = false;
            int vertexCount = scope.Vertices.Count;

            for (int i = 0, j = vertexCount - 1; i < vertexCount; j = i++)
            {
                Point3d pi = scope.Vertices[i];
                Point3d pj = scope.Vertices[j];

                bool intersects = ((pi.Y > flatPoint.Y) != (pj.Y > flatPoint.Y)) &&
                    (flatPoint.X < (pj.X - pi.X) * (flatPoint.Y - pi.Y) / ((pj.Y - pi.Y) == 0.0 ? 0.0000001 : (pj.Y - pi.Y)) + pi.X);

                if (intersects)
                {
                    inside = !inside;
                }
            }

            return inside;
        }

        private static BlockTableRecord GetModelSpace(Database database, Transaction tr)
        {
            BlockTable blockTable = (BlockTable)tr.GetObject(database.BlockTableId, OpenMode.ForRead);
            return (BlockTableRecord)tr.GetObject(blockTable[BlockTableRecord.ModelSpace], OpenMode.ForRead);
        }

        private static bool IsValidScopePolyline(Polyline polyline)
        {
            return polyline != null && polyline.Closed && polyline.NumberOfVertices >= 3;
        }

        private static bool IsScopeKind(string kind)
        {
            return string.Equals(kind, QtoXDataHelper.ScopeKindFloor, StringComparison.OrdinalIgnoreCase)
                || string.Equals(kind, QtoXDataHelper.ScopeKindSystem, StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeScopeKind(string kind)
        {
            if (string.Equals(kind, QtoXDataHelper.ScopeKindSystem, StringComparison.OrdinalIgnoreCase))
            {
                return QtoXDataHelper.ScopeKindSystem;
            }

            if (string.Equals(kind, QtoXDataHelper.ScopeKindFloor, StringComparison.OrdinalIgnoreCase))
            {
                return QtoXDataHelper.ScopeKindFloor;
            }

            return string.Empty;
        }

        private static string BuildDefaultScopeName(string kind, string value)
        {
            if (string.Equals(kind, QtoXDataHelper.ScopeKindSystem, StringComparison.OrdinalIgnoreCase))
            {
                return "系統框 " + (value ?? string.Empty);
            }

            return "樓層框 " + (value ?? string.Empty);
        }

        private static Point3d Flatten(Point3d point)
        {
            return new Point3d(point.X, point.Y, 0.0);
        }

        private static long ParseLong(string value)
        {
            long result;
            if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result))
            {
                return result;
            }

            return 0;
        }

        private static string GetValue(Dictionary<string, string> data, string key)
        {
            string value;
            if (data != null && data.TryGetValue(key, out value))
            {
                return value ?? string.Empty;
            }

            return string.Empty;
        }
    }
}
