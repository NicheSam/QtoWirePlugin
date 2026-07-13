using System;
using System.Drawing;
using System.Windows.Forms;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Windows;

namespace QtoWirePlugin
{
    internal static class QtoPropertyPaletteHost
    {
        private static PaletteSet paletteSet;
        private static QtoPropertyPanelForm panelForm;
        private static Document trackedDocument;
        private static Editor trackedEditor;
        private static bool documentEventsAttached;
        private const int SelectionRefreshDelayMilliseconds = 200;
        private static System.Windows.Forms.Timer selectionRefreshTimer;
        private static Document pendingRefreshDocument;
        private static bool refreshPending;
        private static bool refreshInProgress;

        public static void Show()
        {
            Document document = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;

            EnsurePalette(document);
            AttachDocumentEvents();
            AttachEditor(document);

            paletteSet.Visible = true;
            RefreshSelection(document);
        }

        private static void EnsurePalette(Document document)
        {
            if (paletteSet != null)
            {
                return;
            }

            QtoDictionaryStore dictionary = QtoDictionaryLoader.LoadDefault(document == null ? null : document.Database);

            panelForm = new QtoPropertyPanelForm(dictionary);
            panelForm.Dock = DockStyle.Fill;

            paletteSet = new PaletteSet("QTO 屬性");
            paletteSet.Style = PaletteSetStyles.ShowAutoHideButton
                | PaletteSetStyles.ShowCloseButton
                | PaletteSetStyles.ShowPropertiesMenu;
            paletteSet.DockEnabled = DockSides.Left | DockSides.Right;
            paletteSet.Size = new Size(560, 820);
            paletteSet.Add("屬性", panelForm);
        }

        private static void AttachDocumentEvents()
        {
            if (documentEventsAttached)
            {
                return;
            }

            Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.DocumentActivated += DocumentManagerDocumentActivated;
            documentEventsAttached = true;
        }

        private static void EnsureRefreshTimer()
        {
            if (selectionRefreshTimer != null)
            {
                return;
            }

            selectionRefreshTimer = new System.Windows.Forms.Timer();
            selectionRefreshTimer.Interval = SelectionRefreshDelayMilliseconds;
            selectionRefreshTimer.Tick += SelectionRefreshTimerTick;
        }

        private static void AttachEditor(Document document)
        {
            if (document == null)
            {
                DetachEditor();
                return;
            }

            if (ReferenceEquals(trackedDocument, document))
            {
                return;
            }

            DetachEditor();

            trackedDocument = document;
            trackedEditor = document.Editor;
            trackedEditor.SelectionAdded += EditorSelectionAdded;
            trackedEditor.SelectionRemoved += EditorSelectionRemoved;
        }

        private static void DetachEditor()
        {
            if (trackedEditor != null)
            {
                trackedEditor.SelectionAdded -= EditorSelectionAdded;
                trackedEditor.SelectionRemoved -= EditorSelectionRemoved;
            }

            trackedEditor = null;
            trackedDocument = null;
            ClearPendingRefresh();
        }

        private static void DocumentManagerDocumentActivated(object sender, DocumentCollectionEventArgs e)
        {
            AttachEditor(e == null ? null : e.Document);
            RefreshSelection(e == null ? null : e.Document);
        }

        private static void EditorSelectionAdded(object sender, SelectionAddedEventArgs e)
        {
            RefreshSelection(trackedDocument);
        }

        private static void EditorSelectionRemoved(object sender, SelectionRemovedEventArgs e)
        {
            RefreshSelection(trackedDocument);
        }

        private static void RefreshSelection(Document document)
        {
            if (!IsPaletteVisible())
            {
                ClearPendingRefresh();
                return;
            }

            pendingRefreshDocument = document;
            refreshPending = true;
            RestartRefreshTimer();
        }

        private static void SelectionRefreshTimerTick(object sender, EventArgs e)
        {
            if (selectionRefreshTimer != null)
            {
                selectionRefreshTimer.Stop();
            }

            if (!refreshPending)
            {
                return;
            }

            if (!IsPaletteVisible())
            {
                ClearPendingRefresh();
                return;
            }

            if (refreshInProgress)
            {
                RestartRefreshTimer();
                return;
            }

            Document document = pendingRefreshDocument;
            Document activeDocument = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            if (document == null || !ReferenceEquals(document, activeDocument))
            {
                document = activeDocument;
            }

            pendingRefreshDocument = null;
            refreshPending = false;

            refreshInProgress = true;
            try
            {
                RefreshPanel(document);
            }
            finally
            {
                refreshInProgress = false;
                if (refreshPending)
                {
                    RestartRefreshTimer();
                }
            }
        }

        private static bool IsPaletteVisible()
        {
            return paletteSet != null
                && panelForm != null
                && !panelForm.IsDisposed
                && paletteSet.Visible;
        }

        private static void RestartRefreshTimer()
        {
            EnsureRefreshTimer();
            selectionRefreshTimer.Stop();
            selectionRefreshTimer.Interval = SelectionRefreshDelayMilliseconds;
            selectionRefreshTimer.Start();
        }

        private static void ClearPendingRefresh()
        {
            pendingRefreshDocument = null;
            refreshPending = false;
            if (selectionRefreshTimer != null)
            {
                selectionRefreshTimer.Stop();
            }
        }

        private static void RefreshPanel(Document document)
        {
            try
            {
                if (panelForm.ShouldDeferAutoRefresh())
                {
                    return;
                }

                panelForm.LoadSelectionFromEditor(document);
            }
            catch (Exception ex)
            {
                WriteRefreshError(document, ex);
            }
        }

        private static void WriteRefreshError(Document document, Exception ex)
        {
            if (document == null || document.Editor == null)
            {
                return;
            }

            document.Editor.WriteMessage("\nQTO 屬性面板更新略過：" + ex.Message);
        }
    }
}
