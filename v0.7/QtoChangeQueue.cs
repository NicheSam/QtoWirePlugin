using System;
using System.Collections.Generic;
using System.Threading;

namespace QtoWirePlugin
{
    public sealed class QtoChangeQueue : IDisposable
    {
        private const int MinDebounceMilliseconds = 500;
        private const int MaxDebounceMilliseconds = 1500;

        private readonly object gate = new object();
        private readonly Dictionary<string, QtoCadChange> pendingChanges;
        private readonly Timer debounceTimer;
        private int debounceMilliseconds;
        private bool disposed;

        public QtoChangeQueue()
            : this(1000)
        {
        }

        public QtoChangeQueue(int debounceMilliseconds)
        {
            pendingChanges = new Dictionary<string, QtoCadChange>(StringComparer.OrdinalIgnoreCase);
            this.debounceMilliseconds = ClampDebounce(debounceMilliseconds);
            debounceTimer = new Timer(OnDebounceElapsed, null, Timeout.Infinite, Timeout.Infinite);
        }

        public event EventHandler<QtoChangeBatchEventArgs> BatchReady;

        public int DebounceMilliseconds
        {
            get { return debounceMilliseconds; }
            set { debounceMilliseconds = ClampDebounce(value); }
        }

        public int PendingCount
        {
            get
            {
                lock (gate)
                {
                    return pendingChanges.Count;
                }
            }
        }

        public Exception LastDispatchException { get; private set; }

        public void Enqueue(QtoCadChange change)
        {
            if (change == null || disposed)
            {
                return;
            }

            QtoCadChange normalized = change.Clone();
            if (normalized.UtcTime == DateTime.MinValue)
            {
                normalized.UtcTime = DateTime.UtcNow;
            }

            string key = GetMergeKey(normalized);

            lock (gate)
            {
                QtoCadChange existing;
                if (pendingChanges.TryGetValue(key, out existing))
                {
                    pendingChanges[key] = Merge(existing, normalized);
                }
                else
                {
                    pendingChanges[key] = normalized;
                }

                RestartTimerNoThrow();
            }
        }

        public IList<QtoCadChange> Flush()
        {
            List<QtoCadChange> changes = Drain();
            if (changes.Count > 0)
            {
                RaiseBatchReady(changes);
            }

            return changes;
        }

        public void Clear()
        {
            lock (gate)
            {
                pendingChanges.Clear();
                StopTimerNoThrow();
            }
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            Clear();
            debounceTimer.Dispose();
        }

        private static int ClampDebounce(int value)
        {
            if (value < MinDebounceMilliseconds)
            {
                return MinDebounceMilliseconds;
            }

            if (value > MaxDebounceMilliseconds)
            {
                return MaxDebounceMilliseconds;
            }

            return value;
        }

        private static string GetMergeKey(QtoCadChange change)
        {
            if (!string.IsNullOrWhiteSpace(change.DatabaseFileName) &&
                !string.IsNullOrWhiteSpace(change.ObjectHandle))
            {
                return change.DatabaseFileName + "|" + change.ObjectHandle;
            }

            if (!string.IsNullOrWhiteSpace(change.ObjectHandle))
            {
                return change.ObjectHandle;
            }

            if (!change.ObjectId.IsNull)
            {
                return change.ObjectId.ToString();
            }

            if (!string.IsNullOrWhiteSpace(change.SyncId))
            {
                return "sync|" + change.SyncId;
            }

            return "event|" + Guid.NewGuid().ToString("N");
        }

        private static QtoCadChange Merge(QtoCadChange existing, QtoCadChange incoming)
        {
            QtoCadChange merged = incoming.Clone();

            if (incoming.Kind == QtoCadChangeKind.Deleted ||
                incoming.Kind == QtoCadChangeKind.Missing ||
                incoming.Kind == QtoCadChangeKind.Error)
            {
                if (merged.XDataSnapshot.Count == 0 && existing.XDataSnapshot != null)
                {
                    merged.XDataSnapshot = new Dictionary<string, string>(
                        existing.XDataSnapshot,
                        StringComparer.OrdinalIgnoreCase);
                }

                if (string.IsNullOrWhiteSpace(merged.SyncId))
                {
                    merged.SyncId = existing.SyncId;
                }

                if (string.IsNullOrWhiteSpace(merged.QtoType))
                {
                    merged.QtoType = existing.QtoType;
                }

                return merged;
            }

            if (existing.Kind == QtoCadChangeKind.Deleted ||
                existing.Kind == QtoCadChangeKind.Missing ||
                existing.Kind == QtoCadChangeKind.Error)
            {
                return existing.Clone();
            }

            if (existing.Kind == QtoCadChangeKind.Added &&
                incoming.Kind == QtoCadChangeKind.Modified)
            {
                merged.Kind = QtoCadChangeKind.Added;
            }

            if (string.IsNullOrWhiteSpace(merged.SyncId))
            {
                merged.SyncId = existing.SyncId;
            }

            if (string.IsNullOrWhiteSpace(merged.QtoType))
            {
                merged.QtoType = existing.QtoType;
            }

            return merged;
        }

        private List<QtoCadChange> Drain()
        {
            lock (gate)
            {
                StopTimerNoThrow();

                List<QtoCadChange> changes = new List<QtoCadChange>();
                foreach (QtoCadChange change in pendingChanges.Values)
                {
                    changes.Add(change.Clone());
                }

                pendingChanges.Clear();
                return changes;
            }
        }

        private void OnDebounceElapsed(object state)
        {
            if (disposed)
            {
                return;
            }

            Flush();
        }

        private void RaiseBatchReady(IList<QtoCadChange> changes)
        {
            EventHandler<QtoChangeBatchEventArgs> handler = BatchReady;
            if (handler == null)
            {
                return;
            }

            try
            {
                handler(this, new QtoChangeBatchEventArgs(changes));
                LastDispatchException = null;
            }
            catch (Exception ex)
            {
                LastDispatchException = ex;
            }
        }

        private void RestartTimerNoThrow()
        {
            try
            {
                debounceTimer.Change(debounceMilliseconds, Timeout.Infinite);
            }
            catch (ObjectDisposedException)
            {
            }
        }

        private void StopTimerNoThrow()
        {
            try
            {
                debounceTimer.Change(Timeout.Infinite, Timeout.Infinite);
            }
            catch (ObjectDisposedException)
            {
            }
        }
    }
}
