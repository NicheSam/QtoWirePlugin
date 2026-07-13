using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using AcApplication = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace QtoWirePlugin
{
    public sealed class QtoChangeWatcher : IDisposable
    {
        private readonly QtoChangeQueue queue;
        private readonly QtoCopyArrayRuleService copyArrayRuleService;
        private readonly HashSet<Document> watchedDocuments;
        private readonly HashSet<Database> watchedDatabases;
        private bool started;
        private bool disposed;

        public QtoChangeWatcher()
            : this(new QtoChangeQueue())
        {
        }

        public QtoChangeWatcher(QtoChangeQueue queue)
        {
            if (queue == null)
            {
                throw new ArgumentNullException("queue");
            }

            this.queue = queue;
            copyArrayRuleService = new QtoCopyArrayRuleService();
            watchedDocuments = new HashSet<Document>();
            watchedDatabases = new HashSet<Database>();
        }

        public QtoChangeQueue Queue
        {
            get { return queue; }
        }

        public bool IsStarted
        {
            get { return started; }
        }

        public Exception LastEventException { get; private set; }

        public event EventHandler<QtoChangeBatchEventArgs> ChangesProcessed;

        public void Start()
        {
            if (disposed || started)
            {
                return;
            }

            SafeRun(delegate
            {
                DocumentCollection documents = AcApplication.DocumentManager;
                documents.DocumentCreated += OnDocumentCreated;
                documents.DocumentToBeDestroyed += OnDocumentToBeDestroyed;

                foreach (Document document in documents)
                {
                    HookDocument(document);
                }

                started = true;
            });
        }

        public void Start(Document document)
        {
            if (disposed)
            {
                return;
            }

            if (document == null)
            {
                Start();
                return;
            }

            SafeRun(delegate
            {
                HookDocument(document);
                started = true;
            });
        }

        public void Stop()
        {
            if (disposed || !started)
            {
                return;
            }

            SafeRun(delegate
            {
                DocumentCollection documents = AcApplication.DocumentManager;
                documents.DocumentCreated -= OnDocumentCreated;
                documents.DocumentToBeDestroyed -= OnDocumentToBeDestroyed;

                foreach (Document document in new List<Document>(watchedDocuments))
                {
                    UnhookDocument(document);
                }

                foreach (Database database in new List<Database>(watchedDatabases))
                {
                    UnhookDatabase(database);
                }

                watchedDocuments.Clear();
                watchedDatabases.Clear();
                started = false;
            });
        }

        public IList<QtoCadChange> Flush()
        {
            return queue.Flush();
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            Stop();
            disposed = true;
        }

        private void HookDocument(Document document)
        {
            if (document == null || watchedDocuments.Contains(document))
            {
                return;
            }

            watchedDocuments.Add(document);

            try
            {
                document.CommandEnded += OnDocumentCommandFinished;
                document.CommandCancelled += OnDocumentCommandFinished;
                document.CommandFailed += OnDocumentCommandFinished;
            }
            catch (Exception ex)
            {
                LastEventException = ex;
            }

            HookDatabase(document.Database);
        }

        private void UnhookDocument(Document document)
        {
            if (document == null || !watchedDocuments.Contains(document))
            {
                return;
            }

            try
            {
                document.CommandEnded -= OnDocumentCommandFinished;
                document.CommandCancelled -= OnDocumentCommandFinished;
                document.CommandFailed -= OnDocumentCommandFinished;
            }
            catch (Exception ex)
            {
                LastEventException = ex;
            }

            watchedDocuments.Remove(document);
        }

        private void HookDatabase(Database database)
        {
            if (database == null || watchedDatabases.Contains(database))
            {
                return;
            }

            watchedDatabases.Add(database);
            database.ObjectAppended += OnObjectAppended;
            database.ObjectModified += OnObjectModified;
            database.ObjectErased += OnObjectErased;
        }

        private void UnhookDatabase(Database database)
        {
            if (database == null || !watchedDatabases.Contains(database))
            {
                return;
            }

            try
            {
                database.ObjectAppended -= OnObjectAppended;
                database.ObjectModified -= OnObjectModified;
                database.ObjectErased -= OnObjectErased;
            }
            catch (Exception ex)
            {
                LastEventException = ex;
            }

            watchedDatabases.Remove(database);
            QtoScopeService.InvalidateScopeCache(database);
        }

        private void OnDocumentCreated(object sender, DocumentCollectionEventArgs e)
        {
            SafeRun(delegate
            {
                HookDocument(e.Document);
            });
        }

        private void OnDocumentToBeDestroyed(object sender, DocumentCollectionEventArgs e)
        {
            SafeRun(delegate
            {
                UnhookDocument(e.Document);
                if (e.Document != null)
                {
                    UnhookDatabase(e.Document.Database);
                }
            });
        }

        private void OnDocumentCommandFinished(object sender, CommandEventArgs e)
        {
            SafeRun(delegate
            {
                Document document = sender as Document;
                IList<QtoCadChange> changes = queue.Flush();
                if (document == null || document.Database == null || changes == null || changes.Count == 0)
                {
                    return;
                }

                IList<QtoCadChange> normalizedChanges = copyArrayRuleService.ApplyRules(document.Database, changes);
                QtoScopeApplyResult scopeResult = QtoScopeService.ApplyDynamic(document.Database, normalizedChanges);
                if (scopeResult.UpdatedCount > 0 || scopeResult.ClearedCount > 0 || scopeResult.SkippedUnsupportedCount > 0)
                {
                    document.Editor.WriteMessage(
                        "\n樓層/系統範圍已動態套用：更新 " + scopeResult.UpdatedCount.ToString("0")
                        + "，清空 " + scopeResult.ClearedCount.ToString("0")
                        + "，不支援 " + scopeResult.SkippedUnsupportedCount.ToString("0") + "。");
                }

                // Scope updates can enqueue secondary ObjectModified events. The current
                // full drawing snapshot already covers them, so do not carry them into
                // the next command and create a duplicate Excel refresh.
                queue.Clear();
                EventHandler<QtoChangeBatchEventArgs> handler = ChangesProcessed;
                if (handler != null)
                {
                    handler(this, new QtoChangeBatchEventArgs(normalizedChanges));
                }
            });
        }

        private void OnObjectAppended(object sender, ObjectEventArgs e)
        {
            SafeRun(delegate
            {
                EnqueueObjectChange(sender as Database, e.DBObject, QtoCadChangeKind.Added);
            });
        }

        private void OnObjectModified(object sender, ObjectEventArgs e)
        {
            SafeRun(delegate
            {
                EnqueueObjectChange(sender as Database, e.DBObject, QtoCadChangeKind.Modified);
            });
        }

        private void OnObjectErased(object sender, ObjectErasedEventArgs e)
        {
            SafeRun(delegate
            {
                EnqueueObjectChange(
                    sender as Database,
                    e.DBObject,
                    e.Erased ? QtoCadChangeKind.Deleted : QtoCadChangeKind.Modified);
            });
        }

        private void EnqueueObjectChange(Database database, DBObject dbObject, QtoCadChangeKind kind)
        {
            if (dbObject == null)
            {
                return;
            }

            Entity entity = dbObject as Entity;
            if (entity == null)
            {
                return;
            }

            Dictionary<string, string> xdata = QtoXDataHelper.GetXData(entity);
            if (xdata.Count == 0)
            {
                return;
            }

            string qtoType = GetValueOrEmpty(xdata, QtoXDataHelper.KeyQtoType);
            string scopeKind = GetValueOrEmpty(xdata, QtoXDataHelper.KeyQtoScopeKind);
            if (string.IsNullOrWhiteSpace(qtoType) &&
                string.IsNullOrWhiteSpace(scopeKind))
            {
                return;
            }

            QtoCadChange change = new QtoCadChange();
            change.Kind = kind;
            change.ObjectId = entity.ObjectId;
            change.ObjectHandle = SafeHandle(entity);
            change.QtoType = qtoType;
            change.SyncId = GetValueOrEmpty(xdata, QtoCopyArrayRuleService.KeyQtoSyncId);
            change.DatabaseFileName = database == null ? string.Empty : database.Filename;
            change.XDataSnapshot = xdata;

            queue.Enqueue(change);
        }

        private void SafeRun(Action action)
        {
            try
            {
                action();
                LastEventException = null;
            }
            catch (Exception ex)
            {
                LastEventException = ex;
                QtoCadChange error = new QtoCadChange();
                error.Kind = QtoCadChangeKind.Error;
                error.ErrorMessage = ex.Message;
                queue.Enqueue(error);
            }
        }

        private static string SafeHandle(DBObject dbObject)
        {
            try
            {
                return dbObject.Handle.ToString();
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string GetValueOrEmpty(Dictionary<string, string> data, string key)
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
