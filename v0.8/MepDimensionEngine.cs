using System.Collections.Generic;
using System;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace QtoWirePlugin
{
    public static class MepDimensionEngine
    {
        public const double DefaultDimensionOffset = 600.0;
        private const double TierSpacing = 300.0;

        public static RotatedDimension CreateXDimension(MepEquipmentItem item, MepReferenceLine xReference, Database db, Transaction tr, int index, string dimStyleName)
        {
            Point3d target = item.DimensionPoint;
            Point3d foot = MepGeometryService.ProjectPointToReference(target, xReference);
            double dimY = target.Y - DefaultDimensionOffset - (index % 4) * TierSpacing;
            Point3d dimLinePoint = new Point3d((target.X + foot.X) / 2.0, dimY, 0.0);

            ObjectId dimStyleId = MepLayerHelper.GetDimStyleId(db, tr, dimStyleName);
            RotatedDimension dimension = new RotatedDimension(0.0, foot, target, dimLinePoint, string.Empty, dimStyleId);
            dimension.SetDatabaseDefaults(db);
            dimension.DimensionStyle = dimStyleId;
            return dimension;
        }

        public static RotatedDimension CreateYDimension(MepEquipmentItem item, MepReferenceLine yReference, Database db, Transaction tr, int index, string dimStyleName)
        {
            Point3d target = item.DimensionPoint;
            Point3d foot = MepGeometryService.ProjectPointToReference(target, yReference);
            double dimX = target.X + DefaultDimensionOffset + (index % 4) * TierSpacing;
            Point3d dimLinePoint = new Point3d(dimX, (target.Y + foot.Y) / 2.0, 0.0);

            ObjectId dimStyleId = MepLayerHelper.GetDimStyleId(db, tr, dimStyleName);
            RotatedDimension dimension = new RotatedDimension(System.Math.PI / 2.0, foot, target, dimLinePoint, string.Empty, dimStyleId);
            dimension.SetDatabaseDefaults(db);
            dimension.DimensionStyle = dimStyleId;
            return dimension;
        }

        public static RotatedDimension CreateHorizontalDimension(Point3d firstPoint, Point3d secondPoint, double dimensionLineY, Database db, Transaction tr, string dimStyleName)
        {
            Point3d start = new Point3d(firstPoint.X, firstPoint.Y, 0.0);
            Point3d end = new Point3d(secondPoint.X, secondPoint.Y, 0.0);
            Point3d dimensionLinePoint = new Point3d((start.X + end.X) / 2.0, dimensionLineY, 0.0);
            ObjectId dimStyleId = MepLayerHelper.GetDimStyleId(db, tr, dimStyleName);
            RotatedDimension dimension = new RotatedDimension(0.0, start, end, dimensionLinePoint, string.Empty, dimStyleId);
            dimension.SetDatabaseDefaults(db);
            dimension.DimensionStyle = dimStyleId;
            return dimension;
        }

        public static RotatedDimension CreateVerticalDimension(Point3d firstPoint, Point3d secondPoint, double dimensionLineX, Database db, Transaction tr, string dimStyleName)
        {
            Point3d start = new Point3d(firstPoint.X, firstPoint.Y, 0.0);
            Point3d end = new Point3d(secondPoint.X, secondPoint.Y, 0.0);
            Point3d dimensionLinePoint = new Point3d(dimensionLineX, (start.Y + end.Y) / 2.0, 0.0);
            ObjectId dimStyleId = MepLayerHelper.GetDimStyleId(db, tr, dimStyleName);
            RotatedDimension dimension = new RotatedDimension(Math.PI / 2.0, start, end, dimensionLinePoint, string.Empty, dimStyleId);
            dimension.SetDatabaseDefaults(db);
            dimension.DimensionStyle = dimStyleId;
            return dimension;
        }

        public static Dictionary<string, string> BuildDimensionXData(MepEquipmentItem item)
        {
            return MepAnnotationEngine.BuildXData(item, MepXDataHelper.TypeDimension);
        }
    }
}
