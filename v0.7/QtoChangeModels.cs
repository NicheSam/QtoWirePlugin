using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;

namespace QtoWirePlugin
{
    public enum QtoCadChangeKind
    {
        Added,
        Modified,
        Deleted,
        Missing,
        CopiedOrPasted,
        ArrayItem,
        Error
    }

    public sealed class QtoCadChange
    {
        public QtoCadChange()
        {
            UtcTime = DateTime.UtcNow;
            XDataSnapshot = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        public QtoCadChangeKind Kind { get; set; }
        public ObjectId ObjectId { get; set; }
        public string ObjectHandle { get; set; }
        public string QtoType { get; set; }
        public string SyncId { get; set; }
        public string SourceSyncId { get; set; }
        public string DatabaseFileName { get; set; }
        public DateTime UtcTime { get; set; }
        public Dictionary<string, string> XDataSnapshot { get; set; }
        public string ErrorMessage { get; set; }

        public QtoCadChange Clone()
        {
            QtoCadChange clone = new QtoCadChange();
            clone.Kind = Kind;
            clone.ObjectId = ObjectId;
            clone.ObjectHandle = ObjectHandle;
            clone.QtoType = QtoType;
            clone.SyncId = SyncId;
            clone.SourceSyncId = SourceSyncId;
            clone.DatabaseFileName = DatabaseFileName;
            clone.UtcTime = UtcTime;
            clone.ErrorMessage = ErrorMessage;
            clone.XDataSnapshot = new Dictionary<string, string>(
                XDataSnapshot ?? new Dictionary<string, string>(),
                StringComparer.OrdinalIgnoreCase);
            return clone;
        }
    }

    public sealed class QtoChangeBatchEventArgs : EventArgs
    {
        public QtoChangeBatchEventArgs(IList<QtoCadChange> changes)
        {
            Changes = changes ?? new List<QtoCadChange>();
            UtcTime = DateTime.UtcNow;
        }

        public IList<QtoCadChange> Changes { get; private set; }
        public DateTime UtcTime { get; private set; }
    }
}
