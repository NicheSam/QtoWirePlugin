using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace QtoWirePlugin
{
    public class QtoTrayNetwork
    {
        private readonly List<TrayNode> nodes = new List<TrayNode>();
        private readonly List<TrayEdge> edges = new List<TrayEdge>();
        private readonly Dictionary<int, List<TrayEdge>> edgesByNode = new Dictionary<int, List<TrayEdge>>();
        private readonly Dictionary<string, int> edgeKeySet = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        public static QtoTrayNetwork Build(Transaction transaction, Database database, double tolerance, TrayNetworkScanResult scanResult)
        {
            QtoTrayNetwork network = new QtoTrayNetwork();
            BlockTable blockTable = (BlockTable)transaction.GetObject(database.BlockTableId, OpenMode.ForRead);
            BlockTableRecord modelSpace = (BlockTableRecord)transaction.GetObject(blockTable[BlockTableRecord.ModelSpace], OpenMode.ForRead);
            Dictionary<string, List<int>> trayNodeIds = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
            int trayCount = 0;
            int shortSegmentCount = 0;

            foreach (ObjectId objectId in modelSpace)
            {
                Polyline polyline = transaction.GetObject(objectId, OpenMode.ForRead, false) as Polyline;

                if (polyline == null || !QtoXDataHelper.HasQtoType(polyline, QtoXDataHelper.TypeTray))
                {
                    continue;
                }

                trayCount++;
                string trayId = QtoXDataHelper.GetXDataValue(polyline, QtoXDataHelper.KeyTrayId) ?? objectId.ToString();
                List<int> nodeIds = new List<int>();

                for (int i = 0; i < polyline.NumberOfVertices; i++)
                {
                    int nodeId = network.GetOrAddNode(polyline.GetPoint3dAt(i), tolerance);
                    network.nodes[nodeId].TrayIds[trayId] = true;
                    nodeIds.Add(nodeId);
                }

                for (int i = 0; i + 1 < nodeIds.Count; i++)
                {
                    double length = network.nodes[nodeIds[i]].Point.DistanceTo(network.nodes[nodeIds[i + 1]].Point);

                    if (length <= Math.Max(1.0, tolerance * 0.1))
                    {
                        shortSegmentCount++;
                    }

                    network.AddEdge(nodeIds[i], nodeIds[i + 1], length, trayId);
                }

                trayNodeIds[trayId] = nodeIds;
            }

            if (scanResult != null)
            {
                scanResult.TrayCount = trayCount;
                scanResult.NodeCount = network.nodes.Count;
                scanResult.EdgeCount = network.edges.Count;
                scanResult.ShortSegmentCount = shortSegmentCount;
                FillScanDiagnostics(network, trayNodeIds, tolerance, scanResult);
            }

            return network;
        }

        public bool TryFindPath(Point3d startPoint, Point3d endPoint, out List<Point3d> routePoints, out string errorMessage)
        {
            routePoints = new List<Point3d>();
            errorMessage = string.Empty;

            if (nodes.Count == 0 || edges.Count == 0)
            {
                errorMessage = "No TRAY network. Please mark and scan trays first.";
                return false;
            }

            int startNode = FindNearestNode(startPoint);
            int endNode = FindNearestNode(endPoint);
            List<int> path = RunDijkstra(startNode, endNode);

            if (path.Count == 0)
            {
                errorMessage = "No connected TRAY path between outlet and junction box.";
                return false;
            }

            routePoints.Add(startPoint);

            foreach (int nodeId in path)
            {
                Point3d point = nodes[nodeId].Point;

                if (routePoints[routePoints.Count - 1].DistanceTo(point) > 0.0001)
                {
                    routePoints.Add(point);
                }
            }

            if (routePoints[routePoints.Count - 1].DistanceTo(endPoint) > 0.0001)
            {
                routePoints.Add(endPoint);
            }

            return true;
        }

        private static void FillScanDiagnostics(QtoTrayNetwork network, Dictionary<string, List<int>> trayNodeIds, double tolerance, TrayNetworkScanResult result)
        {
            result.Rows = new List<string[]>();
            result.Rows.Add(new string[] { "項目類型", "線槽編號", "狀態", "訊息" });

            foreach (KeyValuePair<string, List<int>> tray in trayNodeIds)
            {
                bool connectedToAnotherTray = false;

                foreach (int nodeId in tray.Value)
                {
                    if (network.nodes[nodeId].TrayIds.Count > 1)
                    {
                        connectedToAnotherTray = true;
                        break;
                    }
                }

                if (!connectedToAnotherTray)
                {
                    result.IsolatedTrayCount++;
                    result.Rows.Add(new string[] { "線槽", tray.Key, "警告", "孤立線槽" });
                }

                if (tray.Value.Count > 0)
                {
                    int firstNode = tray.Value[0];
                    int lastNode = tray.Value[tray.Value.Count - 1];

                    if (network.nodes[firstNode].TrayIds.Count == 1)
                    {
                        result.UnconnectedEndpointCount++;
                        result.Rows.Add(new string[] { "端點", tray.Key, "警告", "起點未在容許距離 " + tolerance.ToString("0.###") + " mm 內連接其他線槽" });
                    }

                    if (lastNode != firstNode && network.nodes[lastNode].TrayIds.Count == 1)
                    {
                        result.UnconnectedEndpointCount++;
                        result.Rows.Add(new string[] { "端點", tray.Key, "警告", "終點未在容許距離 " + tolerance.ToString("0.###") + " mm 內連接其他線槽" });
                    }
                }
            }

            if (result.ShortSegmentCount > 0)
            {
                result.Rows.Add(new string[] { "線段", string.Empty, "警告", "短線段數量：" + result.ShortSegmentCount.ToString() });
            }
        }

        private int GetOrAddNode(Point3d point, double tolerance)
        {
            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i].Point.DistanceTo(point) <= tolerance)
                {
                    return i;
                }
            }

            TrayNode node = new TrayNode();
            node.Id = nodes.Count;
            node.Point = point;
            nodes.Add(node);
            return node.Id;
        }

        private void AddEdge(int fromNodeId, int toNodeId, double length, string trayId)
        {
            if (fromNodeId == toNodeId)
            {
                return;
            }

            int min = Math.Min(fromNodeId, toNodeId);
            int max = Math.Max(fromNodeId, toNodeId);
            string key = min.ToString() + "-" + max.ToString();

            if (edgeKeySet.ContainsKey(key))
            {
                return;
            }

            TrayEdge edge = new TrayEdge();
            edge.FromNodeId = fromNodeId;
            edge.ToNodeId = toNodeId;
            edge.Length = length;
            edge.TrayId = trayId ?? string.Empty;
            edges.Add(edge);
            edgeKeySet[key] = edges.Count - 1;
            AddEdgeToNode(fromNodeId, edge);
            AddEdgeToNode(toNodeId, edge);
        }

        private void AddEdgeToNode(int nodeId, TrayEdge edge)
        {
            List<TrayEdge> list;

            if (!edgesByNode.TryGetValue(nodeId, out list))
            {
                list = new List<TrayEdge>();
                edgesByNode[nodeId] = list;
            }

            list.Add(edge);
        }

        private int FindNearestNode(Point3d point)
        {
            int nearest = -1;
            double distance = double.MaxValue;

            for (int i = 0; i < nodes.Count; i++)
            {
                double candidate = nodes[i].Point.DistanceTo(point);

                if (candidate < distance)
                {
                    distance = candidate;
                    nearest = i;
                }
            }

            return nearest;
        }

        private List<int> RunDijkstra(int startNodeId, int endNodeId)
        {
            Dictionary<int, double> distanceByNode = new Dictionary<int, double>();
            Dictionary<int, int> previousByNode = new Dictionary<int, int>();
            Dictionary<int, bool> visited = new Dictionary<int, bool>();

            foreach (TrayNode node in nodes)
            {
                distanceByNode[node.Id] = double.MaxValue;
            }

            distanceByNode[startNodeId] = 0.0;

            while (visited.Count < nodes.Count)
            {
                int current = GetNearestUnvisitedNode(distanceByNode, visited);

                if (current < 0 || current == endNodeId)
                {
                    break;
                }

                visited[current] = true;
                List<TrayEdge> nodeEdges;

                if (!edgesByNode.TryGetValue(current, out nodeEdges))
                {
                    continue;
                }

                foreach (TrayEdge edge in nodeEdges)
                {
                    int next = edge.GetOtherNodeId(current);

                    if (visited.ContainsKey(next))
                    {
                        continue;
                    }

                    double candidate = distanceByNode[current] + edge.Length;

                    if (candidate < distanceByNode[next])
                    {
                        distanceByNode[next] = candidate;
                        previousByNode[next] = current;
                    }
                }
            }

            if (!previousByNode.ContainsKey(endNodeId) && startNodeId != endNodeId)
            {
                return new List<int>();
            }

            List<int> path = new List<int>();
            int cursor = endNodeId;
            path.Add(cursor);

            while (cursor != startNodeId)
            {
                int previous;

                if (!previousByNode.TryGetValue(cursor, out previous))
                {
                    return new List<int>();
                }

                cursor = previous;
                path.Add(cursor);
            }

            path.Reverse();
            return path;
        }

        private static int GetNearestUnvisitedNode(Dictionary<int, double> distanceByNode, Dictionary<int, bool> visited)
        {
            int nodeId = -1;
            double best = double.MaxValue;

            foreach (KeyValuePair<int, double> item in distanceByNode)
            {
                if (!visited.ContainsKey(item.Key) && item.Value < best)
                {
                    best = item.Value;
                    nodeId = item.Key;
                }
            }

            return nodeId;
        }
    }

    public class TrayNode
    {
        public TrayNode()
        {
            TrayIds = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        }

        public int Id { get; set; }
        public Point3d Point { get; set; }
        public Dictionary<string, bool> TrayIds { get; private set; }
    }

    public class TrayEdge
    {
        public int FromNodeId { get; set; }
        public int ToNodeId { get; set; }
        public double Length { get; set; }
        public string TrayId { get; set; }

        public int GetOtherNodeId(int nodeId)
        {
            return nodeId == FromNodeId ? ToNodeId : FromNodeId;
        }
    }
}
