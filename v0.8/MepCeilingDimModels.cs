using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace QtoWirePlugin
{
    public enum MepDimensionPointMode
    {
        InsertionPoint,
        BoundingBoxCenter
    }

    public enum MepDimensionMode
    {
        ChainWithBoundary,
        SpacingOnly,
        BoundaryOnly
    }

    public class MepDimensionOptions
    {
        public MepDimensionMode Mode { get; set; }
        public string DimStyleName { get; set; }
        public bool GenerateXDimensions { get; set; }
        public bool GenerateYDimensions { get; set; }
        public bool MarkUnknownBlocks { get; set; }
        public double RowTolerance { get; set; }
        public double ColumnTolerance { get; set; }
        public double InternalOffset { get; set; }
        public double BoundaryOffset { get; set; }
        public bool BoundaryTop { get; set; }
        public bool BoundaryBottom { get; set; }
        public bool BoundaryLeft { get; set; }
        public bool BoundaryRight { get; set; }
    }

    public class MepDimRule
    {
        public string BlockName { get; set; }
        public string Category { get; set; }
        public string Label { get; set; }
        public MepDimensionPointMode DimensionPointMode { get; set; }
        public bool ShowOpeningSize { get; set; }
        public string OpeningSize { get; set; }
        public bool RequiresReview { get; set; }
        public string OutputLayer { get; set; }
    }

    public class MepReferenceLine
    {
        public Point3d StartPoint { get; set; }
        public Point3d EndPoint { get; set; }
        public ObjectId SourceObjectId { get; set; }
    }

    public class MepSelectionBoundary
    {
        public ObjectId SourceObjectId { get; set; }
        public Point3dCollection Vertices { get; set; }
        public Extents3d Extents { get; set; }
    }

    public class MepEquipmentItem
    {
        public ObjectId SourceObjectId { get; set; }
        public string BlockName { get; set; }
        public Point3d InsertionPoint { get; set; }
        public Point3d DimensionPoint { get; set; }
        public MepDimRule Rule { get; set; }
        public bool UsedFallbackPoint { get; set; }
    }

    public class MepGridLine
    {
        public int Index { get; set; }
        public double Coordinate { get; set; }
        public System.Collections.Generic.List<MepEquipmentItem> Items { get; set; }
    }

    public class MepLightGrid
    {
        public System.Collections.Generic.List<MepGridLine> Rows { get; set; }
        public System.Collections.Generic.List<MepGridLine> Columns { get; set; }
        public MepGridLine RepresentativeRow { get; set; }
        public MepGridLine RepresentativeColumn { get; set; }
    }

    public enum MepPlannedDimensionOrientation
    {
        Horizontal,
        Vertical
    }

    public class MepPlannedDimension
    {
        public MepPlannedDimensionOrientation Orientation { get; set; }
        public Point3d StartPoint { get; set; }
        public Point3d EndPoint { get; set; }
        public double DimensionLineCoordinate { get; set; }
        public string LayerName { get; set; }
        public MepEquipmentItem SourceItem { get; set; }
        public string Purpose { get; set; }
    }

    public class MepDimensionPlan
    {
        public MepLightGrid Grid { get; set; }
        public System.Collections.Generic.List<MepPlannedDimension> Dimensions { get; set; }
        public string Summary { get; set; }
    }

    public class MepCeilingDimResult
    {
        public int SelectedCount { get; set; }
        public int ProcessedCount { get; set; }
        public int UnknownCount { get; set; }
        public int ReviewCount { get; set; }
        public int ErrorCount { get; set; }
        public int CreatedDimensionCount { get; set; }
        public int CreatedAnnotationCount { get; set; }
    }
}
