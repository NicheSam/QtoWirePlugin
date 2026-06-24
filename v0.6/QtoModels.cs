using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace QtoWirePlugin
{
    public class OutletInfo
    {
        public ObjectId ObjectId { get; set; }
        public string OutletId { get; set; }
        public string JbId { get; set; }
        public string System { get; set; }
        public string CableType { get; set; }
        public string BlockName { get; set; }
        public Point3d Position { get; set; }
    }

    public class JunctionBoxInfo
    {
        public ObjectId ObjectId { get; set; }
        public string JbId { get; set; }
        public string System { get; set; }
        public string BlockName { get; set; }
        public Point3d Position { get; set; }
    }

    public class WireInfo
    {
        public ObjectId ObjectId { get; set; }
        public string RouteId { get; set; }
        public string OutletId { get; set; }
        public string JbId { get; set; }
        public string System { get; set; }
        public string CableType { get; set; }
        public double LengthM { get; set; }
        public string LengthSource { get; set; }
        public string Layer { get; set; }
    }

    public class CheckResult
    {
        public string ItemType { get; set; }
        public string ObjectId { get; set; }
        public string OutletId { get; set; }
        public string JbId { get; set; }
        public string System { get; set; }
        public string CableType { get; set; }
        public double LengthM { get; set; }
        public string Status { get; set; }
        public string ErrorMessage { get; set; }
    }

    public class BatchBindResult
    {
        public string OutletId { get; set; }
        public string JbId { get; set; }
        public string MatchedWireObjectId { get; set; }
        public double Distance { get; set; }
        public string Status { get; set; }
        public string ErrorMessage { get; set; }
    }

    public class JunctionBoxSummaryInfo
    {
        public string JbId { get; set; }
        public string System { get; set; }
        public string CableType { get; set; }
        public int OutletCount { get; set; }
        public int WireCount { get; set; }
        public double TotalLengthM { get; set; }
    }

    public class TrayInfo
    {
        public ObjectId ObjectId { get; set; }
        public string TrayId { get; set; }
        public string SystemScope { get; set; }
        public int VertexCount { get; set; }
        public string Layer { get; set; }
    }

    public class TrayNetworkScanResult
    {
        public int TrayCount { get; set; }
        public int NodeCount { get; set; }
        public int EdgeCount { get; set; }
        public int IsolatedTrayCount { get; set; }
        public int UnconnectedEndpointCount { get; set; }
        public int ShortSegmentCount { get; set; }
        public System.Collections.Generic.List<string[]> Rows { get; set; }
    }

    public class TrayRouteResult
    {
        public bool Success { get; set; }
        public ObjectId WireObjectId { get; set; }
        public string OutletId { get; set; }
        public string JbId { get; set; }
        public string ErrorMessage { get; set; }
        public double LengthM { get; set; }
    }
}
