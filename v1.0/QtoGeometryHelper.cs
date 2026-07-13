using Autodesk.AutoCAD.DatabaseServices;

namespace QtoWirePlugin
{
    public static class QtoGeometryHelper
    {
        public static double GetPolylineLength(Polyline polyline)
        {
            if (polyline == null)
            {
                return 0.0;
            }

            return polyline.Length;
        }

        public static double GetMlineLength(Mline mline)
        {
            if (mline == null || mline.NumberOfVertices < 2)
            {
                return 0.0;
            }

            double length = 0.0;

            for (int i = 1; i < mline.NumberOfVertices; i++)
            {
                length += mline.VertexAt(i - 1).DistanceTo(mline.VertexAt(i));
            }

            return length;
        }

        public static double ConvertMmToM(double lengthMm)
        {
            return lengthMm / 1000.0;
        }
    }
}
