using System.Collections.Generic;

namespace QtoWirePlugin
{
    public class QtoEquipmentTypeDefinition
    {
        public string EquipmentTypeCode { get; set; }
        public string EquipmentTypeName { get; set; }
        public string SystemCode { get; set; }
        public string QuantityBasis { get; set; }
        public string DefaultMappingStatus { get; set; }
    }

    public class QtoDictionaryStore
    {
        public QtoDictionaryStore()
        {
            EquipmentTypes = new Dictionary<string, QtoEquipmentTypeDefinition>(System.StringComparer.OrdinalIgnoreCase);
            SystemCodes = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            SourceDirectory = string.Empty;
            LoadWarning = string.Empty;
        }

        public Dictionary<string, QtoEquipmentTypeDefinition> EquipmentTypes { get; private set; }
        public HashSet<string> SystemCodes { get; private set; }
        public string SourceDirectory { get; set; }
        public string LoadWarning { get; set; }

        public QtoEquipmentTypeDefinition FindEquipment(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                return null;
            }

            QtoEquipmentTypeDefinition definition;
            if (EquipmentTypes.TryGetValue(code, out definition))
            {
                return definition;
            }

            return null;
        }
    }

    public class QtoBudgetInputRow
    {
        public string SourceDwg { get; set; }
        public string ObjectHandle { get; set; }
        public string QtoType { get; set; }
        public string QtoId { get; set; }
        public string SystemCode { get; set; }
        public string EquipmentTypeCode { get; set; }
        public string EquipmentTypeName { get; set; }
        public string BlockName { get; set; }
        public string Layer { get; set; }
        public string Floor { get; set; }
        public string Area { get; set; }
        public string Space { get; set; }
        public string CableType { get; set; }
        public string ConduitType { get; set; }
        public string ConduitSize { get; set; }
        public string TraySize { get; set; }
        public string QuantityBasis { get; set; }
        public double QtoQuantity { get; set; }
        public string QtoUnit { get; set; }
        public double LengthM { get; set; }
        public string MappingStatus { get; set; }
        public string ReviewReason { get; set; }
        public string SourceRule { get; set; }
    }
}
