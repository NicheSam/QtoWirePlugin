using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;

namespace QtoWirePlugin
{
    public class QtoPropertyPanelForm : UserControl
    {
        private readonly ComboBox qtoTypeComboBox;
        private readonly ComboBox systemCodeComboBox;
        private readonly ComboBox equipmentTypeComboBox;
        private readonly TextBox objectHandleTextBox;
        private readonly TextBox blockNameTextBox;
        private readonly TextBox layerTextBox;
        private readonly TextBox qtoIdTextBox;
        private readonly TextBox outletIdTextBox;
        private readonly TextBox jbIdTextBox;
        private readonly TextBox cableTypeTextBox;
        private readonly TextBox floorTextBox;
        private readonly TextBox areaTextBox;
        private readonly TextBox spaceTextBox;
        private readonly TextBox quantityBasisTextBox;
        private readonly TextBox qtoUnitTextBox;
        private readonly ComboBox mappingStatusComboBox;
        private readonly TextBox reviewReasonTextBox;
        private readonly TextBox diagnosticsTextBox;
        private readonly ToolTip toolTip;
        private readonly QtoDictionaryStore dictionary;
        private ObjectId currentObjectId;
        private ObjectId[] currentSelectionObjectIds;
        private bool isMultipleSelection;

        public QtoPropertyPanelForm(QtoDictionaryStore dictionary)
        {
            this.dictionary = dictionary ?? new QtoDictionaryStore();
            currentSelectionObjectIds = new ObjectId[0];
            Text = "QTO v1.0 Beta 屬性面板";
            Width = 390;
            Height = 820;
            MinimumSize = new Size(340, 560);
            BackColor = Color.FromArgb(238, 240, 243);

            FlowLayoutPanel actionPanel = new FlowLayoutPanel();
            actionPanel.Dock = DockStyle.Top;
            actionPanel.Height = 78;
            actionPanel.Padding = new Padding(8, 7, 8, 5);
            actionPanel.BackColor = Color.FromArgb(238, 240, 243);
            actionPanel.WrapContents = true;

            actionPanel.Controls.Add(CreateButton("挑選", 8, 8, 70, 28, PickButtonClick));
            actionPanel.Controls.Add(CreateButton("儲存", 84, 8, 70, 28, SaveButtonClick));
            actionPanel.Controls.Add(CreateButton("更新", 160, 8, 70, 28, RefreshButtonClick));
            Button exportButton = CreateButton("匯出預算CSV", 236, 8, 104, 28, ExportButtonClick);
            actionPanel.Controls.Add(exportButton);
            actionPanel.Controls.Add(CreateButton("未標註", 8, 42, 70, 28, SelectUntaggedButtonClick));
            actionPanel.Controls.Add(CreateButton("選同類", 84, 42, 70, 28, SelectSameButtonClick));
            toolTip = new ToolTip();
            toolTip.SetToolTip(exportButton, "匯出 QTO 預算前置資料：圖面來源、物件識別、系統、設備類型、位置、配線、數量、對應狀態與待確認原因。");

            TableLayoutPanel typePanel = new TableLayoutPanel();
            typePanel.Dock = DockStyle.Top;
            typePanel.Height = 78;
            typePanel.Padding = new Padding(8, 6, 8, 6);
            typePanel.BackColor = Color.FromArgb(248, 249, 251);
            typePanel.ColumnCount = 2;
            typePanel.RowCount = 2;
            typePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70));
            typePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            typePanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            typePanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));

            Label typeLabel = CreatePropertyLabel("設備類型");
            typeLabel.Dock = DockStyle.Fill;
            typeLabel.Margin = Padding.Empty;
            typePanel.Controls.Add(typeLabel, 0, 0);

            equipmentTypeComboBox = new ComboBox();
            equipmentTypeComboBox.Dock = DockStyle.Fill;
            equipmentTypeComboBox.DropDownStyle = ComboBoxStyle.DropDown;
            equipmentTypeComboBox.Margin = new Padding(0, 4, 0, 4);
            equipmentTypeComboBox.Items.AddRange(GetEquipmentDisplays());
            equipmentTypeComboBox.SelectedIndexChanged += EquipmentTypeComboBoxSelectedIndexChanged;
            typePanel.Controls.Add(equipmentTypeComboBox, 1, 0);
            toolTip.SetToolTip(equipmentTypeComboBox, "設備類型是預算對應的主要分類，可複製或重新命名後再人工確認。");

            FlowLayoutPanel typeButtonPanel = new FlowLayoutPanel();
            typeButtonPanel.Dock = DockStyle.Fill;
            typeButtonPanel.Margin = Padding.Empty;
            typeButtonPanel.Padding = Padding.Empty;
            typeButtonPanel.WrapContents = false;
            typeButtonPanel.Controls.Add(CreateButton("複製類型", 348, 7, 78, 28, DuplicateTypeButtonClick));
            typeButtonPanel.Controls.Add(CreateButton("重新命名", 432, 7, 82, 28, RenameTypeButtonClick));

            Label instanceLabel = CreatePropertyLabel("實例屬性");
            instanceLabel.Dock = DockStyle.Fill;
            instanceLabel.Margin = Padding.Empty;
            typePanel.Controls.Add(instanceLabel, 0, 1);
            typePanel.Controls.Add(typeButtonPanel, 1, 1);

            Panel diagnosticsPanel = new Panel();
            diagnosticsPanel.Dock = DockStyle.Bottom;
            diagnosticsPanel.Height = 150;
            diagnosticsPanel.Padding = new Padding(8, 6, 8, 8);
            diagnosticsPanel.BackColor = Color.FromArgb(238, 240, 243);

            Label diagnosticsLabel = CreateCategoryLabel("檢查結果");
            diagnosticsLabel.Dock = DockStyle.Top;
            diagnosticsLabel.Height = 26;
            diagnosticsPanel.Controls.Add(diagnosticsLabel);

            diagnosticsTextBox = new TextBox();
            diagnosticsTextBox.Dock = DockStyle.Fill;
            diagnosticsTextBox.Multiline = true;
            diagnosticsTextBox.ScrollBars = ScrollBars.Vertical;
            diagnosticsTextBox.ReadOnly = true;
            diagnosticsTextBox.BorderStyle = BorderStyle.FixedSingle;
            diagnosticsPanel.Controls.Add(diagnosticsTextBox);

            Panel propertyHost = new Panel();
            propertyHost.Dock = DockStyle.Fill;
            propertyHost.AutoScroll = true;
            propertyHost.Padding = new Padding(8, 8, 8, 8);
            propertyHost.BackColor = Color.FromArgb(238, 240, 243);

            TableLayoutPanel propertyTable = new TableLayoutPanel();
            propertyTable.Dock = DockStyle.Top;
            propertyTable.AutoSize = true;
            propertyTable.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            propertyTable.BackColor = Color.FromArgb(238, 240, 243);
            propertyTable.CellBorderStyle = TableLayoutPanelCellBorderStyle.Single;
            propertyTable.ColumnCount = 2;
            propertyTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38));
            propertyTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62));
            propertyTable.GrowStyle = TableLayoutPanelGrowStyle.AddRows;
            propertyTable.Margin = Padding.Empty;
            propertyHost.Controls.Add(propertyTable);

            Controls.Add(propertyHost);
            Controls.Add(diagnosticsPanel);
            Controls.Add(typePanel);
            Controls.Add(actionPanel);

            AddCategoryRow(propertyTable, "目前選取");
            objectHandleTextBox = AddGridText(propertyTable, "物件識別碼", true);
            blockNameTextBox = AddGridText(propertyTable, "圖塊名稱", true);
            layerTextBox = AddGridText(propertyTable, "圖層", true);

            AddCategoryRow(propertyTable, "系統與設備");
            qtoTypeComboBox = AddGridCombo(propertyTable, "CAD計量型態", new string[]
            {
                ToQtoTypeDisplay(QtoXDataHelper.TypeOutlet),
                ToQtoTypeDisplay(QtoXDataHelper.TypeJunctionBox),
                ToQtoTypeDisplay(QtoXDataHelper.TypeWire),
                ToQtoTypeDisplay(QtoXDataHelper.TypeConduitSegment),
                ToQtoTypeDisplay(QtoXDataHelper.TypeTray),
                ToQtoTypeDisplay(QtoXDataHelper.TypeDevice),
                ToQtoTypeDisplay(QtoXDataHelper.TypePanel)
            });
            systemCodeComboBox = AddGridCombo(propertyTable, "系統代碼", GetSystemCodes());
            toolTip.SetToolTip(qtoTypeComboBox, "CAD計量型態只描述圖面物件的計量方式，例如出線口、箱體、配線或管段，不等於預算品項。");
            toolTip.SetToolTip(systemCodeComboBox, "系統代碼用來區分弱電、停管、資訊、TV、CCTV、BA、視聽音響與緊急廣播。");

            AddCategoryRow(propertyTable, "識別資料");
            qtoIdTextBox = AddGridText(propertyTable, "QTO 編號", false);
            outletIdTextBox = AddGridText(propertyTable, "出線口編號", false);
            jbIdTextBox = AddGridText(propertyTable, "箱體編號", false);

            AddCategoryRow(propertyTable, "位置與配線");
            cableTypeTextBox = AddGridText(propertyTable, "線材類型", false);
            floorTextBox = AddGridText(propertyTable, "樓層", false);
            areaTextBox = AddGridText(propertyTable, "區域", false);
            spaceTextBox = AddGridText(propertyTable, "空間", false);

            AddCategoryRow(propertyTable, "數量與預算");
            quantityBasisTextBox = AddGridText(propertyTable, "數量依據", false);
            qtoUnitTextBox = AddGridText(propertyTable, "單位", false);
            mappingStatusComboBox = AddGridCombo(propertyTable, "對應狀態", new string[] { "候選對應", "需人工確認", "已阻擋", "已確認" });
            reviewReasonTextBox = AddGridText(propertyTable, "待確認原因", false);

            ShowDictionaryStatus();
        }

        private void PickButtonClick(object sender, EventArgs e)
        {
            Document document = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            if (document == null)
            {
                return;
            }

            PromptEntityOptions options = new PromptEntityOptions("\n請選取要檢視的 QTO 圖塊或線條：");
            PromptEntityResult result = document.Editor.GetEntity(options);
            if (result.Status != PromptStatus.OK)
            {
                return;
            }

            LoadObjectId(document, result.ObjectId);
        }

        private void RefreshButtonClick(object sender, EventArgs e)
        {
            Document document = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            LoadSelectionFromEditor(document);
        }

        private void SaveButtonClick(object sender, EventArgs e)
        {
            if (isMultipleSelection)
            {
                SaveBatchProperties();
                return;
            }

            if (currentObjectId.IsNull)
            {
                diagnosticsTextBox.Text = "尚未選取物件。";
                return;
            }

            Document document = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            if (document == null)
            {
                return;
            }

            using (document.LockDocument())
            using (Transaction transaction = document.Database.TransactionManager.StartTransaction())
            {
                Entity entity = transaction.GetObject(currentObjectId, OpenMode.ForWrite, false) as Entity;
                if (entity == null)
                {
                    diagnosticsTextBox.Text = "選取物件不是可寫入 QTO 屬性的 Entity。";
                    return;
                }

                Dictionary<string, string> data = QtoXDataHelper.GetXData(entity);
                Put(data, QtoXDataHelper.KeyQtoType, ToQtoTypeCode(qtoTypeComboBox.Text));
                Put(data, QtoXDataHelper.KeySystemCode, systemCodeComboBox.Text);
                Put(data, QtoXDataHelper.KeyEquipmentTypeCode, ToEquipmentTypeCode(equipmentTypeComboBox.Text));
                Put(data, QtoXDataHelper.KeyQtoId, qtoIdTextBox.Text);
                Put(data, QtoXDataHelper.KeyOutletId, outletIdTextBox.Text);
                Put(data, QtoXDataHelper.KeyJbId, jbIdTextBox.Text);
                Put(data, QtoXDataHelper.KeyCableType, cableTypeTextBox.Text);
                Put(data, QtoXDataHelper.KeyFloor, floorTextBox.Text);
                Put(data, QtoXDataHelper.KeyArea, areaTextBox.Text);
                Put(data, QtoXDataHelper.KeySpace, spaceTextBox.Text);
                Put(data, QtoXDataHelper.KeyQuantityBasis, ToQuantityBasisCode(quantityBasisTextBox.Text));
                Put(data, QtoXDataHelper.KeyQtoUnit, ToUnitCode(qtoUnitTextBox.Text));
                Put(data, QtoXDataHelper.KeyMappingStatus, ToMappingStatusCode(mappingStatusComboBox.Text));
                Put(data, QtoXDataHelper.KeyReviewReason, reviewReasonTextBox.Text);
                Put(data, QtoXDataHelper.KeySourceRule, "qto_property_panel_v0_6");
                QtoXDataHelper.SetXData(entity, document.Database, transaction, data);
                transaction.Commit();
            }

            LoadObjectId(document, currentObjectId);
        }

        private void SaveBatchProperties()
        {
            if (currentSelectionObjectIds == null || currentSelectionObjectIds.Length == 0)
            {
                diagnosticsTextBox.Text = "目前沒有可批次寫入的選取物件。";
                return;
            }

            Dictionary<string, string> batchValues = BuildBatchValues();
            if (batchValues.Count == 0)
            {
                diagnosticsTextBox.Text = "請先輸入要批次套用的欄位。空白欄位不會覆蓋既有資料。";
                return;
            }

            Document document = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            if (document == null)
            {
                diagnosticsTextBox.Text = "目前沒有作用中的 CAD 文件。";
                return;
            }

            int updatedCount = 0;
            int skippedCount = 0;
            using (document.LockDocument())
            using (Transaction transaction = document.Database.TransactionManager.StartTransaction())
            {
                foreach (ObjectId objectId in currentSelectionObjectIds)
                {
                    if (objectId.IsNull)
                    {
                        skippedCount++;
                        continue;
                    }

                    Entity entity = transaction.GetObject(objectId, OpenMode.ForWrite, false) as Entity;
                    if (entity == null)
                    {
                        skippedCount++;
                        continue;
                    }

                    Dictionary<string, string> data = QtoXDataHelper.GetXData(entity);
                    foreach (KeyValuePair<string, string> item in batchValues)
                    {
                        Put(data, item.Key, item.Value);
                    }

                    Put(data, QtoXDataHelper.KeySourceRule, "qto_property_panel_batch_v0_6");
                    QtoXDataHelper.SetXData(entity, document.Database, transaction, data);
                    updatedCount++;
                }

                transaction.Commit();
            }

            diagnosticsTextBox.Text = "批次寫入完成：" + updatedCount.ToString() + " 個物件。"
                + (skippedCount > 0 ? Environment.NewLine + "略過：" + skippedCount.ToString() + " 個非可寫入物件。" : string.Empty)
                + Environment.NewLine + "已套用欄位：" + string.Join("、", GetBatchValueNames(batchValues).ToArray());
        }

        private Dictionary<string, string> BuildBatchValues()
        {
            Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            AddBatchValue(values, QtoXDataHelper.KeyEquipmentTypeCode, ToEquipmentTypeCode(equipmentTypeComboBox.Text));
            AddBatchValue(values, QtoXDataHelper.KeyQtoType, ToQtoTypeCode(qtoTypeComboBox.Text));
            AddBatchValue(values, QtoXDataHelper.KeySystemCode, systemCodeComboBox.Text);
            AddBatchValue(values, QtoXDataHelper.KeyCableType, cableTypeTextBox.Text);
            AddBatchValue(values, QtoXDataHelper.KeyFloor, floorTextBox.Text);
            AddBatchValue(values, QtoXDataHelper.KeyArea, areaTextBox.Text);
            AddBatchValue(values, QtoXDataHelper.KeySpace, spaceTextBox.Text);
            return values;
        }

        private static void AddBatchValue(Dictionary<string, string> values, string key, string value)
        {
            if (values == null || string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            values[key] = value.Trim();
        }

        private static List<string> GetBatchValueNames(Dictionary<string, string> values)
        {
            List<string> names = new List<string>();
            if (values.ContainsKey(QtoXDataHelper.KeyEquipmentTypeCode))
            {
                names.Add("設備類型");
            }

            if (values.ContainsKey(QtoXDataHelper.KeyQtoType))
            {
                names.Add("CAD計量型態");
            }

            if (values.ContainsKey(QtoXDataHelper.KeySystemCode))
            {
                names.Add("系統代碼");
            }

            if (values.ContainsKey(QtoXDataHelper.KeyCableType))
            {
                names.Add("線材類型");
            }

            if (values.ContainsKey(QtoXDataHelper.KeyFloor))
            {
                names.Add("樓層");
            }

            if (values.ContainsKey(QtoXDataHelper.KeyArea))
            {
                names.Add("區域");
            }

            if (values.ContainsKey(QtoXDataHelper.KeySpace))
            {
                names.Add("空間");
            }

            return names;
        }

        private void ExportButtonClick(object sender, EventArgs e)
        {
            Commands command = new Commands();
            command.QtoExportBudgetInput();
        }

        private void SelectUntaggedButtonClick(object sender, EventArgs e)
        {
            Document document = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            if (document == null)
            {
                return;
            }

            document.SendStringToExecute("QTO_SELECT_UNTAGGED ", true, false, false);
        }

        private void SelectSameButtonClick(object sender, EventArgs e)
        {
            Document document = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            if (document == null)
            {
                return;
            }

            document.SendStringToExecute("QTO_SELECT_SAME_QTO ", true, false, false);
        }

        private void DuplicateTypeButtonClick(object sender, EventArgs e)
        {
            string currentCode = ToEquipmentTypeCode(equipmentTypeComboBox.Text);
            if (string.IsNullOrWhiteSpace(currentCode))
            {
                diagnosticsTextBox.Text = "請先選擇或輸入設備類型。";
                return;
            }

            string newCode = PromptForText("複製類型", "新類型代碼", currentCode + "_COPY");
            if (string.IsNullOrWhiteSpace(newCode))
            {
                return;
            }

            SetEquipmentTypeText(newCode.Trim());
            mappingStatusComboBox.Text = ToMappingStatusDisplay("needs_review");
            AppendReviewReason("由「" + currentCode + "」複製為「" + newCode.Trim() + "」，需確認字典與預算對應。");
            diagnosticsTextBox.Text = "已建立目前物件的新類型代碼：" + newCode.Trim();
        }

        private void RenameTypeButtonClick(object sender, EventArgs e)
        {
            string currentCode = ToEquipmentTypeCode(equipmentTypeComboBox.Text);
            string newCode = PromptForText("重新命名類型", "類型代碼", currentCode);
            if (string.IsNullOrWhiteSpace(newCode) || string.Equals(currentCode, newCode.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            SetEquipmentTypeText(newCode.Trim());
            mappingStatusComboBox.Text = ToMappingStatusDisplay("needs_review");
            AppendReviewReason("設備類型由「" + currentCode + "」更名為「" + newCode.Trim() + "」，需確認字典與預算對應。");
            diagnosticsTextBox.Text = "已更新目前物件的類型代碼：" + newCode.Trim();
        }

        public void LoadSelectionFromEditor(Document document)
        {
            if (document == null)
            {
                ClearSelectionMessage("目前沒有作用中的 CAD 文件。");
                return;
            }

            PromptSelectionResult selectionResult = document.Editor.SelectImplied();
            if (selectionResult.Status != PromptStatus.OK || selectionResult.Value == null)
            {
                ClearSelectionMessage("目前沒有選取物件。");
                return;
            }

            List<ObjectId> objectIds = new List<ObjectId>();
            foreach (SelectedObject selectedObject in selectionResult.Value)
            {
                if (selectedObject != null && !selectedObject.ObjectId.IsNull)
                {
                    objectIds.Add(selectedObject.ObjectId);
                }
            }

            if (objectIds.Count == 0)
            {
                ClearSelectionMessage("目前沒有選取物件。");
                return;
            }

            if (objectIds.Count == 1)
            {
                EnsureSelectionIdentity(document, objectIds.ToArray());
                LoadObjectId(document, objectIds[0]);
                return;
            }

            EnsureSelectionIdentity(document, objectIds.ToArray());
            LoadMultipleSelection(objectIds.ToArray());
        }

        public void LoadObjectId(ObjectId objectId)
        {
            Document document = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            LoadObjectId(document, objectId);
        }

        public void LoadObjectId(Document document, ObjectId objectId)
        {
            if (objectId.IsNull)
            {
                ClearSelectionMessage("目前沒有選取物件。");
                return;
            }

            if (document == null)
            {
                ClearSelectionMessage("目前沒有作用中的 CAD 文件。");
                return;
            }

            currentObjectId = objectId;
            currentSelectionObjectIds = new ObjectId[] { objectId };
            isMultipleSelection = false;
            EnsureSelectionIdentity(document, currentSelectionObjectIds);
            LoadCurrentObject(document);
        }

        public void ClearSelectionMessage(string message)
        {
            currentObjectId = ObjectId.Null;
            currentSelectionObjectIds = new ObjectId[0];
            isMultipleSelection = false;
            ClearPropertyFields();
            diagnosticsTextBox.Text = string.IsNullOrWhiteSpace(message) ? "目前沒有選取物件。" : message;
        }

        public ObjectId[] GetCurrentSelectionObjectIds()
        {
            ObjectId[] objectIds = new ObjectId[currentSelectionObjectIds.Length];
            Array.Copy(currentSelectionObjectIds, objectIds, currentSelectionObjectIds.Length);
            return objectIds;
        }

        public bool ShouldDeferAutoRefresh()
        {
            return ContainsFocus;
        }

        public string[] GetBatchEditableXDataKeys()
        {
            return new string[0];
        }

        private void LoadMultipleSelection(ObjectId[] objectIds)
        {
            currentObjectId = ObjectId.Null;
            currentSelectionObjectIds = objectIds ?? new ObjectId[0];
            isMultipleSelection = true;
            ClearPropertyFields();
            objectHandleTextBox.Text = "多重選取：" + currentSelectionObjectIds.Length.ToString() + " 個物件";
            diagnosticsTextBox.Text = "多重選取：" + currentSelectionObjectIds.Length.ToString() + " 個物件" + Environment.NewLine
                + "可批次設定：設備類型、CAD計量型態、系統代碼、線材類型、樓層、區域、空間。" + Environment.NewLine
                + "按「儲存」後，只會寫入已填值的欄位；空白欄位不覆蓋原資料。";
        }

        private void ClearPropertyFields()
        {
            objectHandleTextBox.Text = string.Empty;
            blockNameTextBox.Text = string.Empty;
            layerTextBox.Text = string.Empty;
            qtoTypeComboBox.Text = string.Empty;
            systemCodeComboBox.Text = string.Empty;
            SetEquipmentTypeText(string.Empty);
            qtoIdTextBox.Text = string.Empty;
            outletIdTextBox.Text = string.Empty;
            jbIdTextBox.Text = string.Empty;
            cableTypeTextBox.Text = string.Empty;
            floorTextBox.Text = string.Empty;
            areaTextBox.Text = string.Empty;
            spaceTextBox.Text = string.Empty;
            quantityBasisTextBox.Text = string.Empty;
            qtoUnitTextBox.Text = string.Empty;
            mappingStatusComboBox.Text = string.Empty;
            reviewReasonTextBox.Text = string.Empty;
        }

        private void EnsureSelectionIdentity(Document document, ObjectId[] objectIds)
        {
            if (document == null || document.Database == null || objectIds == null || objectIds.Length == 0)
            {
                return;
            }

            try
            {
                using (Transaction transaction = document.Database.TransactionManager.StartTransaction())
                {
                    if (!SelectionNeedsIdentityRepair(transaction, objectIds))
                    {
                        transaction.Commit();
                        return;
                    }

                    QtoSyncIdRepairResult repairResult = QtoSyncIdService.RepairMissingAndDuplicateSyncIds(document.Database, transaction);
                    transaction.Commit();

                    if (repairResult.DuplicateRepairedCount > 0 || repairResult.MissingCreatedCount > 0)
                    {
                        diagnosticsTextBox.Text = "已修正複製物件的唯一識別；複製來的 QTO 編號需重新確認。";
                    }
                }
            }
            catch (Exception ex)
            {
                diagnosticsTextBox.Text = "檢查複製物件識別時發生錯誤：" + ex.Message;
            }
        }

        private static bool SelectionNeedsIdentityRepair(Transaction transaction, ObjectId[] objectIds)
        {
            foreach (ObjectId objectId in objectIds)
            {
                if (objectId.IsNull || objectId.IsErased)
                {
                    continue;
                }

                Entity entity = transaction.GetObject(objectId, OpenMode.ForRead, false) as Entity;
                if (!QtoSyncIdService.IsQtoEntity(entity))
                {
                    continue;
                }

                string syncId = QtoSyncIdService.GetSyncId(entity);
                if (string.IsNullOrWhiteSpace(syncId))
                {
                    return true;
                }

                if (CountSyncId(transaction, entity.Database, syncId) > 1)
                {
                    return true;
                }
            }

            return false;
        }

        private static int CountSyncId(Transaction transaction, Database database, string syncId)
        {
            if (database == null || string.IsNullOrWhiteSpace(syncId))
            {
                return 0;
            }

            int count = 0;
            BlockTable blockTable = (BlockTable)transaction.GetObject(database.BlockTableId, OpenMode.ForRead);
            BlockTableRecord modelSpace = (BlockTableRecord)transaction.GetObject(blockTable[BlockTableRecord.ModelSpace], OpenMode.ForRead);
            foreach (ObjectId objectId in modelSpace)
            {
                Entity entity = transaction.GetObject(objectId, OpenMode.ForRead, false) as Entity;
                if (!QtoSyncIdService.IsQtoEntity(entity))
                {
                    continue;
                }

                if (string.Equals(QtoSyncIdService.GetSyncId(entity), syncId, StringComparison.OrdinalIgnoreCase))
                {
                    count++;
                }
            }

            return count;
        }

        private void LoadCurrentObject(Document document)
        {
            if (currentObjectId.IsNull)
            {
                diagnosticsTextBox.Text = "尚未選取物件。";
                return;
            }

            if (document == null)
            {
                ClearSelectionMessage("目前沒有作用中的 CAD 文件。");
                return;
            }

            try
            {
                using (Transaction transaction = document.Database.TransactionManager.StartTransaction())
                {
                    Entity entity = transaction.GetObject(currentObjectId, OpenMode.ForRead, false) as Entity;
                    if (entity == null)
                    {
                        ClearPropertyFields();
                        diagnosticsTextBox.Text = "選取物件不是可讀取 QTO 屬性的 Entity。";
                        transaction.Commit();
                        return;
                    }

                    Dictionary<string, string> data = QtoXDataHelper.GetXData(entity);
                    QtoBudgetInputRow row = QtoBudgetInputBuilder.BuildRow(transaction, document.Database, entity, data, dictionary);
                    objectHandleTextBox.Text = row.ObjectHandle;
                    blockNameTextBox.Text = row.BlockName;
                    layerTextBox.Text = row.Layer;
                    qtoTypeComboBox.Text = ToQtoTypeDisplay(row.QtoType);
                    systemCodeComboBox.Text = row.SystemCode;
                    SetEquipmentTypeText(row.EquipmentTypeCode);
                    qtoIdTextBox.Text = row.QtoId;
                    outletIdTextBox.Text = Get(data, QtoXDataHelper.KeyOutletId);
                    jbIdTextBox.Text = Get(data, QtoXDataHelper.KeyJbId);
                    cableTypeTextBox.Text = row.CableType;
                    floorTextBox.Text = row.Floor;
                    areaTextBox.Text = row.Area;
                    spaceTextBox.Text = row.Space;
                    quantityBasisTextBox.Text = ToQuantityBasisDisplay(row.QuantityBasis);
                    qtoUnitTextBox.Text = ToUnitDisplay(row.QtoUnit);
                    mappingStatusComboBox.Text = ToMappingStatusDisplay(row.MappingStatus);
                    reviewReasonTextBox.Text = row.ReviewReason;
                    diagnosticsTextBox.Text = BuildDiagnostics(row);
                    transaction.Commit();
                }
            }
            catch (Exception ex)
            {
                ClearPropertyFields();
                diagnosticsTextBox.Text = "無法讀取目前選取物件：" + ex.Message;
            }
        }

        private string BuildDiagnostics(QtoBudgetInputRow row)
        {
            List<string> lines = new List<string>();
            lines.Add("物件類型：" + ToQtoTypeDisplay(row.QtoType));
            lines.Add("數量：" + row.QtoQuantity.ToString("0.###") + " " + ToUnitDisplay(row.QtoUnit));
            lines.Add("長度(m)：" + row.LengthM.ToString("0.###"));
            lines.Add("對應狀態：" + ToMappingStatusDisplay(row.MappingStatus));
            if (string.IsNullOrWhiteSpace(row.ReviewReason))
            {
                lines.Add("檢查：OK");
            }
            else
            {
                lines.Add("待確認：" + row.ReviewReason);
            }

            lines.Add("字典：" + (string.IsNullOrWhiteSpace(dictionary.LoadWarning) ? dictionary.SourceDirectory : dictionary.LoadWarning));
            return string.Join(Environment.NewLine, lines.ToArray());
        }

        private void EquipmentTypeComboBoxSelectedIndexChanged(object sender, EventArgs e)
        {
            QtoEquipmentTypeDefinition definition = dictionary.FindEquipment(ToEquipmentTypeCode(equipmentTypeComboBox.Text));
            if (definition == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(systemCodeComboBox.Text))
            {
                systemCodeComboBox.Text = definition.SystemCode;
            }

            quantityBasisTextBox.Text = ToQuantityBasisDisplay(definition.QuantityBasis);
            if (string.IsNullOrWhiteSpace(mappingStatusComboBox.Text))
            {
                mappingStatusComboBox.Text = ToMappingStatusDisplay(definition.DefaultMappingStatus);
            }
        }

        private void ShowDictionaryStatus()
        {
            diagnosticsTextBox.Text = string.IsNullOrWhiteSpace(dictionary.LoadWarning)
                ? "字典已載入：" + dictionary.SourceDirectory
                : "字典警告：" + dictionary.LoadWarning;
        }

        private void SetEquipmentTypeText(string value)
        {
            string text = value == null ? string.Empty : value.Trim();
            string display = ToEquipmentTypeDisplay(text);
            if (!string.IsNullOrWhiteSpace(display) && equipmentTypeComboBox.FindStringExact(display) < 0)
            {
                equipmentTypeComboBox.Items.Add(display);
            }

            equipmentTypeComboBox.Text = display;
        }

        private void AppendReviewReason(string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(reviewReasonTextBox.Text))
            {
                reviewReasonTextBox.Text = reason;
                return;
            }

            if (reviewReasonTextBox.Text.IndexOf(reason, StringComparison.OrdinalIgnoreCase) < 0)
            {
                reviewReasonTextBox.Text = reviewReasonTextBox.Text.TrimEnd() + "；" + reason;
            }
        }

        private static string PromptForText(string title, string label, string initialValue)
        {
            using (Form prompt = new Form())
            using (Label promptLabel = new Label())
            using (TextBox input = new TextBox())
            using (Button okButton = new Button())
            using (Button cancelButton = new Button())
            {
                prompt.Text = title;
                prompt.StartPosition = FormStartPosition.CenterParent;
                prompt.FormBorderStyle = FormBorderStyle.FixedDialog;
                prompt.MinimizeBox = false;
                prompt.MaximizeBox = false;
                prompt.ClientSize = new Size(360, 118);

                promptLabel.Text = label;
                promptLabel.Location = new Point(14, 16);
                promptLabel.AutoSize = true;

                input.Text = initialValue ?? string.Empty;
                input.Location = new Point(96, 12);
                input.Size = new Size(246, 25);

                okButton.Text = "確定";
                okButton.DialogResult = DialogResult.OK;
                okButton.Location = new Point(186, 72);
                okButton.Size = new Size(74, 28);

                cancelButton.Text = "取消";
                cancelButton.DialogResult = DialogResult.Cancel;
                cancelButton.Location = new Point(268, 72);
                cancelButton.Size = new Size(74, 28);

                prompt.Controls.Add(promptLabel);
                prompt.Controls.Add(input);
                prompt.Controls.Add(okButton);
                prompt.Controls.Add(cancelButton);
                prompt.AcceptButton = okButton;
                prompt.CancelButton = cancelButton;

                return prompt.ShowDialog() == DialogResult.OK ? input.Text.Trim() : string.Empty;
            }
        }

        private static Label CreateCategoryLabel(string text)
        {
            Label label = new Label();
            label.Text = text;
            label.BackColor = Color.FromArgb(214, 219, 226);
            label.ForeColor = Color.FromArgb(25, 35, 50);
            label.Font = new System.Drawing.Font(SystemFonts.MessageBoxFont.FontFamily, 9.0f, FontStyle.Bold);
            label.Padding = new Padding(8, 4, 4, 4);
            label.Margin = Padding.Empty;
            return label;
        }

        private static Label CreatePropertyLabel(string text)
        {
            Label label = new Label();
            label.Text = text;
            label.ForeColor = Color.FromArgb(45, 55, 72);
            label.TextAlign = ContentAlignment.MiddleLeft;
            label.AutoEllipsis = true;
            return label;
        }

        private static void AddCategoryRow(TableLayoutPanel table, string title)
        {
            Label label = CreateCategoryLabel(title);
            label.Dock = DockStyle.Fill;
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            table.Controls.Add(label, 0, table.RowCount);
            table.SetColumnSpan(label, 2);
            table.RowCount += 1;
        }

        private static TextBox AddGridText(TableLayoutPanel table, string label, bool readOnly)
        {
            TextBox textBox = new TextBox();
            textBox.BorderStyle = BorderStyle.None;
            textBox.Dock = DockStyle.Fill;
            textBox.Margin = Padding.Empty;
            textBox.ReadOnly = readOnly;
            textBox.BackColor = readOnly ? Color.FromArgb(229, 232, 236) : Color.White;
            AddGridControl(table, label, textBox);
            return textBox;
        }

        private static ComboBox AddGridCombo(TableLayoutPanel table, string label, string[] values)
        {
            ComboBox comboBox = new ComboBox();
            comboBox.Dock = DockStyle.Fill;
            comboBox.DropDownStyle = ComboBoxStyle.DropDown;
            comboBox.Margin = Padding.Empty;
            if (values != null)
            {
                comboBox.Items.AddRange(values);
            }

            AddGridControl(table, label, comboBox);
            return comboBox;
        }

        private static void AddGridControl(TableLayoutPanel table, string label, Control control)
        {
            Label controlLabel = CreatePropertyLabel(label);
            controlLabel.Dock = DockStyle.Fill;
            controlLabel.BackColor = Color.FromArgb(238, 240, 243);
            controlLabel.Padding = new Padding(8, 4, 4, 4);
            controlLabel.Margin = Padding.Empty;

            Panel valuePanel = new Panel();
            valuePanel.Dock = DockStyle.Fill;
            valuePanel.Padding = new Padding(4, 4, 4, 4);
            valuePanel.BackColor = Color.White;
            valuePanel.Margin = Padding.Empty;
            valuePanel.Controls.Add(control);

            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            table.Controls.Add(controlLabel, 0, table.RowCount);
            table.Controls.Add(valuePanel, 1, table.RowCount);
            table.RowCount += 1;
        }

        private void AddSectionHeader(string text, int x, int y)
        {
            Label label = new Label();
            label.Text = text;
            label.Font = new System.Drawing.Font(SystemFonts.MessageBoxFont.FontFamily, 10.0f, FontStyle.Bold);
            label.ForeColor = Color.FromArgb(55, 70, 90);
            label.Location = new Point(x, y);
            label.AutoSize = true;
            Controls.Add(label);

            Panel line = new Panel();
            line.Location = new Point(x + 92, y + 9);
            line.Size = new Size(620, 1);
            line.BackColor = Color.FromArgb(200, 205, 210);
            Controls.Add(line);
        }

        private TextBox AddReadOnlyText(string label, int x, int y)
        {
            TextBox textBox = AddText(label, x, y);
            textBox.ReadOnly = true;
            textBox.BackColor = Color.FromArgb(235, 238, 242);
            return textBox;
        }

        private TextBox AddText(string label, int x, int y)
        {
            Label controlLabel = new Label();
            controlLabel.Text = label;
            controlLabel.Location = new Point(x, y);
            controlLabel.AutoSize = true;
            controlLabel.ForeColor = Color.FromArgb(75, 82, 92);
            Controls.Add(controlLabel);

            TextBox textBox = new TextBox();
            textBox.Location = new Point(x, y + 22);
            textBox.Size = new Size(220, 25);
            textBox.BorderStyle = BorderStyle.FixedSingle;
            Controls.Add(textBox);
            return textBox;
        }

        private TextBox AddWideText(string label, int x, int y, int width)
        {
            Label controlLabel = new Label();
            controlLabel.Text = label;
            controlLabel.Location = new Point(x, y);
            controlLabel.AutoSize = true;
            controlLabel.ForeColor = Color.FromArgb(75, 82, 92);
            Controls.Add(controlLabel);

            TextBox textBox = new TextBox();
            textBox.Location = new Point(x, y + 22);
            textBox.Size = new Size(width, 25);
            textBox.BorderStyle = BorderStyle.FixedSingle;
            Controls.Add(textBox);
            return textBox;
        }

        private ComboBox AddCombo(string label, int x, int y, string[] values)
        {
            Label controlLabel = new Label();
            controlLabel.Text = label;
            controlLabel.Location = new Point(x, y);
            controlLabel.AutoSize = true;
            controlLabel.ForeColor = Color.FromArgb(75, 82, 92);
            Controls.Add(controlLabel);

            ComboBox comboBox = new ComboBox();
            comboBox.Location = new Point(x, y + 22);
            comboBox.Size = new Size(220, 25);
            comboBox.DropDownStyle = ComboBoxStyle.DropDown;
            if (values != null)
            {
                comboBox.Items.AddRange(values);
            }

            Controls.Add(comboBox);
            return comboBox;
        }

        private static Button CreateButton(string text, int x, int y, int width, int height, EventHandler handler)
        {
            Button button = new Button();
            button.Text = text;
            button.Location = new Point(x, y);
            button.Size = new Size(width, height);
            button.Margin = new Padding(3, 0, 3, 0);
            button.Click += handler;
            return button;
        }

        private static string ToQtoTypeDisplay(string code)
        {
            string value = code == null ? string.Empty : code.Trim();
            if (string.Equals(value, QtoXDataHelper.TypeOutlet, StringComparison.OrdinalIgnoreCase))
            {
                return "出線口 (OUTLET)";
            }

            if (string.Equals(value, QtoXDataHelper.TypeJunctionBox, StringComparison.OrdinalIgnoreCase))
            {
                return "箱體/接線箱 (JUNCTION_BOX)";
            }

            if (string.Equals(value, QtoXDataHelper.TypeWire, StringComparison.OrdinalIgnoreCase))
            {
                return "配線 (WIRE)";
            }

            if (string.Equals(value, QtoXDataHelper.TypeConduitSegment, StringComparison.OrdinalIgnoreCase))
            {
                return "管段 (CONDUIT_SEGMENT)";
            }

            if (string.Equals(value, QtoXDataHelper.TypeTray, StringComparison.OrdinalIgnoreCase))
            {
                return "線槽 (TRAY)";
            }

            if (string.Equals(value, QtoXDataHelper.TypeDevice, StringComparison.OrdinalIgnoreCase))
            {
                return "設備 (DEVICE)";
            }

            if (string.Equals(value, QtoXDataHelper.TypePanel, StringComparison.OrdinalIgnoreCase))
            {
                return "盤箱/設備盤 (PANEL)";
            }

            return value;
        }

        private static string ToQtoTypeCode(string display)
        {
            string value = display == null ? string.Empty : display.Trim();
            int open = value.LastIndexOf('(');
            int close = value.LastIndexOf(')');
            if (open >= 0 && close > open)
            {
                return value.Substring(open + 1, close - open - 1).Trim();
            }

            if (value == "出線口")
            {
                return QtoXDataHelper.TypeOutlet;
            }

            if (value == "箱體")
            {
                return QtoXDataHelper.TypeJunctionBox;
            }

            if (value == "配線")
            {
                return QtoXDataHelper.TypeWire;
            }

            if (value == "管段")
            {
                return QtoXDataHelper.TypeConduitSegment;
            }

            if (value == "線槽")
            {
                return QtoXDataHelper.TypeTray;
            }

            if (value == "設備")
            {
                return QtoXDataHelper.TypeDevice;
            }

            if (value == "盤箱")
            {
                return QtoXDataHelper.TypePanel;
            }

            return value;
        }

        private static string ToMappingStatusDisplay(string code)
        {
            string value = code == null ? string.Empty : code.Trim();
            if (string.Equals(value, "candidate", StringComparison.OrdinalIgnoreCase))
            {
                return "候選對應";
            }

            if (string.Equals(value, "needs_review", StringComparison.OrdinalIgnoreCase))
            {
                return "需人工確認";
            }

            if (string.Equals(value, "blocked", StringComparison.OrdinalIgnoreCase))
            {
                return "已阻擋";
            }

            if (string.Equals(value, "confirmed", StringComparison.OrdinalIgnoreCase))
            {
                return "已確認";
            }

            return value;
        }

        private static string ToMappingStatusCode(string display)
        {
            string value = display == null ? string.Empty : display.Trim();
            if (value == "候選對應")
            {
                return "candidate";
            }

            if (value == "需人工確認")
            {
                return "needs_review";
            }

            if (value == "已阻擋")
            {
                return "blocked";
            }

            if (value == "已確認")
            {
                return "confirmed";
            }

            return value;
        }

        private static string ToQuantityBasisDisplay(string code)
        {
            string value = code == null ? string.Empty : code.Trim();
            if (string.Equals(value, "point_count", StringComparison.OrdinalIgnoreCase))
            {
                return "點位數量";
            }

            if (string.Equals(value, "device_count", StringComparison.OrdinalIgnoreCase))
            {
                return "設備數量";
            }

            if (string.Equals(value, "wire_length_m", StringComparison.OrdinalIgnoreCase))
            {
                return "配線長度";
            }

            if (string.Equals(value, "conduit_length_m", StringComparison.OrdinalIgnoreCase))
            {
                return "管段長度";
            }

            if (string.Equals(value, "tray_length_m", StringComparison.OrdinalIgnoreCase))
            {
                return "線槽長度";
            }

            return value;
        }

        private static string ToQuantityBasisCode(string display)
        {
            string value = display == null ? string.Empty : display.Trim();
            if (value == "點位數量")
            {
                return "point_count";
            }

            if (value == "設備數量")
            {
                return "device_count";
            }

            if (value == "配線長度")
            {
                return "wire_length_m";
            }

            if (value == "管段長度")
            {
                return "conduit_length_m";
            }

            if (value == "線槽長度")
            {
                return "tray_length_m";
            }

            return value;
        }

        private static string ToUnitDisplay(string code)
        {
            string value = code == null ? string.Empty : code.Trim();
            if (string.Equals(value, "point", StringComparison.OrdinalIgnoreCase))
            {
                return "點";
            }

            if (string.Equals(value, "set", StringComparison.OrdinalIgnoreCase))
            {
                return "式";
            }

            if (string.Equals(value, "m", StringComparison.OrdinalIgnoreCase))
            {
                return "公尺";
            }

            return value;
        }

        private static string ToUnitCode(string display)
        {
            string value = display == null ? string.Empty : display.Trim();
            if (value == "點")
            {
                return "point";
            }

            if (value == "式")
            {
                return "set";
            }

            if (value == "公尺")
            {
                return "m";
            }

            return value;
        }

        private string ToEquipmentTypeDisplay(string code)
        {
            string value = code == null ? string.Empty : code.Trim();
            QtoEquipmentTypeDefinition definition = dictionary.FindEquipment(value);
            if (definition == null || string.IsNullOrWhiteSpace(definition.EquipmentTypeName))
            {
                return value;
            }

            return definition.EquipmentTypeName.Trim() + " (" + definition.EquipmentTypeCode + ")";
        }

        private static string ToEquipmentTypeCode(string display)
        {
            string value = display == null ? string.Empty : display.Trim();
            int open = value.LastIndexOf('(');
            int close = value.LastIndexOf(')');
            if (open >= 0 && close > open)
            {
                return value.Substring(open + 1, close - open - 1).Trim();
            }

            return value;
        }

        private string[] GetSystemCodes()
        {
            return QtoSystemDefaults.OrderValues(dictionary.SystemCodes);
        }

        private string[] GetEquipmentDisplays()
        {
            List<string> values = new List<string>();
            foreach (string code in dictionary.EquipmentTypes.Keys)
            {
                values.Add(ToEquipmentTypeDisplay(code));
            }

            values.Sort(StringComparer.OrdinalIgnoreCase);
            return values.ToArray();
        }

        private static string Get(Dictionary<string, string> data, string key)
        {
            string value;
            if (data != null && data.TryGetValue(key, out value))
            {
                return value ?? string.Empty;
            }

            return string.Empty;
        }

        private static void Put(Dictionary<string, string> data, string key, string value)
        {
            if (data == null || string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            data[key] = value == null ? string.Empty : value.Trim();
        }
    }
}
