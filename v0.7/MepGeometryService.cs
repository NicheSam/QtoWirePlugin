using System;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace QtoWirePlugin
{
    public static class MepGeometryService
    {
        private const double DirectionToleranceDegrees = 15.0;

        public static bool TryCreateReferenceLine(Entity entity, Point3d pickedPoint, out MepReferenceLine referenceLine, out string error)
        {
            referenceLine = null;
            error = string.Empty;

            Line line = entity as Line;
            if (line != null)
            {
                referenceLine = new MepReferenceLine();
                referenceLine.StartPoint = Flatten(line.StartPoint);
                referenceLine.EndPoint = Flatten(line.EndPoint);
                referenceLine.SourceObjectId = line.ObjectId;
                return HasUsableLength(referenceLine, out error);
            }

            Polyline polyline = entity as Polyline;
            if (polyline != null)
            {
                return TryCreateReferenceLineFromPolyline(polyline, pickedPoint, out referenceLine, out error);
            }

            error = "基準線目前只支援 Line 或 Polyline。";
            return false;
        }

        public static bool TryCreateBoundary(Entity entity, out MepSelectionBoundary boundary, out string error)
        {
            boundary = null;
            error = string.Empty;

            Polyline polyline = entity as Polyline;
            if (polyline == null)
            {
                error = "標註範圍必須是閉合 Polyline。";
                return false;
            }

            if (!polyline.Closed || polyline.NumberOfVertices < 3)
            {
                error = "Polyline 必須是閉合範圍。";
                return false;
            }

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

            boundary = new MepSelectionBoundary();
            boundary.SourceObjectId = polyline.ObjectId;
            boundary.Vertices = vertices;
            boundary.Extents = extents;
            return true;
        }

        public static bool IsNearVertical(MepReferenceLine referenceLine)
        {
            Vector3d direction = referenceLine.EndPoint - referenceLine.StartPoint;
            double angle = Math.Abs(Math.Atan2(direction.Y, direction.X) * 180.0 / Math.PI);
            double verticalDelta = Math.Min(Math.Abs(angle - 90.0), Math.Abs(angle - 270.0));
            return verticalDelta <= DirectionToleranceDegrees;
        }

        public static bool IsNearHorizontal(MepReferenceLine referenceLine)
        {
            Vector3d direction = referenceLine.EndPoint - referenceLine.StartPoint;
            double angle = Math.Abs(Math.Atan2(direction.Y, direction.X) * 180.0 / Math.PI);
            double horizontalDelta = Math.Min(angle, Math.Abs(angle - 180.0));
            return horizontalDelta <= DirectionToleranceDegrees;
        }

        public static Point3d ProjectPointToReference(Point3d point, MepReferenceLine referenceLine)
        {
            Point3d start = referenceLine.StartPoint;
            Point3d end = referenceLine.EndPoint;
            Vector3d vector = end - start;
            double lengthSquared = vector.DotProduct(vector);

            if (lengthSquared <= Tolerance.Global.EqualPoint)
            {
                return start;
            }

            Vector3d toPoint = point - start;
            double t = toPoint.DotProduct(vector) / lengthSquared;
            return start + vector.MultiplyBy(t);
        }

        public static bool IsPointInsideBoundary(Point3d point, MepSelectionBoundary boundary)
        {
            if (boundary == null || boundary.Vertices == null || boundary.Vertices.Count < 3)
            {
                return false;
            }

            Point3d flatPoint = Flatten(point);
            Extents3d extents = boundary.Extents;

            if (flatPoint.X < extents.MinPoint.X || flatPoint.X > extents.MaxPoint.X || flatPoint.Y < extents.MinPoint.Y || flatPoint.Y > extents.MaxPoint.Y)
            {
                return false;
            }

            bool inside = false;
            int vertexCount = boundary.Vertices.Count;

            for (int i = 0, j = vertexCount - 1; i < vertexCount; j = i++)
            {
                Point3d pi = boundary.Vertices[i];
                Point3d pj = boundary.Vertices[j];

                bool intersects = ((pi.Y > flatPoint.Y) != (pj.Y > flatPoint.Y)) &&
                    (flatPoint.X < (pj.X - pi.X) * (flatPoint.Y - pi.Y) / ((pj.Y - pi.Y) == 0.0 ? 0.0000001 : (pj.Y - pi.Y)) + pi.X);

                if (intersects)
                {
                    inside = !inside;
                }
            }

            return inside;
        }

        public static bool TryGetBlockDimensionPoint(BlockReference block, MepDimRule rule, out Point3d point, out bool usedFallback)
        {
            usedFallback = false;
            point = Flatten(block.Position);

            if (rule == null || rule.DimensionPointMode == MepDimensionPointMode.InsertionPoint)
            {
                return true;
            }

            try
            {
                Extents3d extents = block.GeometricExtents;
                Point3d min = extents.MinPoint;
                Point3d max = extents.MaxPoint;
                point = new Point3d((min.X + max.X) / 2.0, (min.Y + max.Y) / 2.0, block.Position.Z);
                return true;
            }
            catch
            {
                usedFallback = true;
                point = Flatten(block.Position);
                return true;
            }
        }

        public static Point3d Flatten(Point3d point)
        {
            return new Point3d(point.X, point.Y, 0.0);
        }

        private static bool TryCreateReferenceLineFromPolyline(Polyline polyline, Point3d pickedPoint, out MepReferenceLine referenceLine, out string error)
        {
            referenceLine = null;
            error = string.Empty;

            int segmentCount = polyline.Closed ? polyline.NumberOfVertices : polyline.NumberOfVertices - 1;
            if (segmentCount <= 0)
            {
                error = "Polyline 沒有可用線段。";
                return false;
            }

            Point3d flatPickedPoint = Flatten(pickedPoint);
            double bestDistance = double.MaxValue;
            Point3d bestStart = Point3d.Origin;
            Point3d bestEnd = Point3d.Origin;

            for (int i = 0; i < segmentCount; i++)
            {
                Point3d start = Flatten(polyline.GetPoint3dAt(i));
                Point3d end = Flatten(polyline.GetPoint3dAt((i + 1) % polyline.NumberOfVertices));
                double distance = DistancePointToSegment(flatPickedPoint, start, end);

                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestStart = start;
                    bestEnd = end;
                }
            }

            referenceLine = new MepReferenceLine();
            referenceLine.StartPoint = bestStart;
            referenceLine.EndPoint = bestEnd;
            referenceLine.SourceObjectId = polyline.ObjectId;
            return HasUsableLength(referenceLine, out error);
        }

        private static bool HasUsableLength(MepReferenceLine referenceLine, out string error)
        {
            if (referenceLine.StartPoint.DistanceTo(referenceLine.EndPoint) <= Tolerance.Global.EqualPoint)
            {
                error = "基準線長度太短。";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static double DistancePointToSegment(Point3d point, Point3d start, Point3d end)
        {
            Vector3d vector = end - start;
            double lengthSquared = vector.DotProduct(vector);

            if (lengthSquared <= Tolerance.Global.EqualPoint)
            {
                return point.DistanceTo(start);
            }

            double t = (point - start).DotProduct(vector) / lengthSquared;
            t = Math.Max(0.0, Math.Min(1.0, t));
            Point3d closest = start + vector.MultiplyBy(t);
            return point.DistanceTo(closest);
        }
    }
}
