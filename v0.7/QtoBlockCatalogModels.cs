using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace QtoWirePlugin
{
    public static class QtoBlockCatalogStatus
    {
        public const string Active = "active";
        public const string Unconfigured = "unconfigured";
        public const string Deprecated = "deprecated";
        public const string Missing = "missing";
        public const string Updated = "updated";
        public const string Conflict = "conflict";

        public static bool IsKnown(string status)
        {
            return string.Equals(status, Active, StringComparison.OrdinalIgnoreCase)
                || string.Equals(status, Unconfigured, StringComparison.OrdinalIgnoreCase)
                || string.Equals(status, Deprecated, StringComparison.OrdinalIgnoreCase)
                || string.Equals(status, Missing, StringComparison.OrdinalIgnoreCase)
                || string.Equals(status, Updated, StringComparison.OrdinalIgnoreCase)
                || string.Equals(status, Conflict, StringComparison.OrdinalIgnoreCase);
        }

        public static string Normalize(string status)
        {
            if (string.IsNullOrWhiteSpace(status))
            {
                return Unconfigured;
            }

            string normalized = status.Trim().ToLowerInvariant();
            return IsKnown(normalized) ? normalized : Unconfigured;
        }

        public static string GetDisplayName(string status)
        {
            switch (Normalize(status))
            {
                case Active:
                    return "使用中";
                case Deprecated:
                    return "已停用";
                case Missing:
                    return "找不到來源";
                case Updated:
                    return "有更新";
                case Conflict:
                    return "有衝突";
                default:
                    return "未設定";
            }
        }
    }

    [DataContract]
    public class QtoBlockCatalog
    {
        public QtoBlockCatalog()
        {
            SchemaVersion = "0.1";
            Items = new List<QtoBlockCatalogItem>();
            Messages = new List<QtoBlockCatalogMessage>();
            CreatedAt = DateTime.Now;
            LastSavedAt = DateTime.MinValue;
            LastScannedAt = DateTime.MinValue;
        }

        [DataMember(Order = 1)]
        public string SchemaVersion { get; set; }

        [DataMember(Order = 2)]
        public string SourceLegendDwg { get; set; }

        [DataMember(Order = 3)]
        public DateTime CreatedAt { get; set; }

        [DataMember(Order = 4)]
        public DateTime LastScannedAt { get; set; }

        [DataMember(Order = 5)]
        public DateTime LastSavedAt { get; set; }

        [DataMember(Order = 6)]
        public List<QtoBlockCatalogItem> Items { get; set; }

        [DataMember(Order = 7)]
        public List<QtoBlockCatalogMessage> Messages { get; set; }
    }

    [DataContract]
    public class QtoBlockCatalogItem
    {
        public QtoBlockCatalogItem()
        {
            AliasNames = new List<string>();
            AttributeTags = new List<string>();
            ReferenceLayers = new List<string>();
            ReferenceSamples = new List<QtoBlockReferenceSample>();
            Status = QtoBlockCatalogStatus.Unconfigured;
            Version = "1";
            LastScannedAt = DateTime.MinValue;
            LastUpdatedAt = DateTime.MinValue;
        }

        [DataMember(Order = 1)]
        public string CatalogId { get; set; }

        [DataMember(Order = 2)]
        public string BlockName { get; set; }

        [DataMember(Order = 3)]
        public string DisplayName { get; set; }

        [DataMember(Order = 4)]
        public string SystemCode { get; set; }

        [DataMember(Order = 5)]
        public string EquipmentTypeCode { get; set; }

        [DataMember(Order = 6)]
        public string QtoType { get; set; }

        [DataMember(Order = 7)]
        public string QuantityBasis { get; set; }

        [DataMember(Order = 8)]
        public string Unit { get; set; }

        [DataMember(Order = 9)]
        public string DefaultCableType { get; set; }

        [DataMember(Order = 10)]
        public string DefaultConduitType { get; set; }

        [DataMember(Order = 11)]
        public string DefaultConduitSize { get; set; }

        [DataMember(Order = 12)]
        public string Status { get; set; }

        [DataMember(Order = 13)]
        public string StatusDisplayName { get; set; }

        [DataMember(Order = 14)]
        public string Version { get; set; }

        [DataMember(Order = 15)]
        public string SourceLegendDwg { get; set; }

        [DataMember(Order = 16)]
        public string BlockFingerprint { get; set; }

        [DataMember(Order = 17)]
        public DateTime LastScannedAt { get; set; }

        [DataMember(Order = 18)]
        public DateTime LastUpdatedAt { get; set; }

        [DataMember(Order = 19)]
        public string Remark { get; set; }

        [DataMember(Order = 20)]
        public List<string> AliasNames { get; set; }

        [DataMember(Order = 21)]
        public string ReplacementBlockName { get; set; }

        [DataMember(Order = 22)]
        public string PreviewImagePath { get; set; }

        [DataMember(Order = 23)]
        public int DefinitionEntityCount { get; set; }

        [DataMember(Order = 24)]
        public int ReferenceCount { get; set; }

        [DataMember(Order = 25)]
        public bool IsDynamicBlock { get; set; }

        [DataMember(Order = 26)]
        public bool IsFromExternalReference { get; set; }

        [DataMember(Order = 27)]
        public bool IsAnonymous { get; set; }

        [DataMember(Order = 28)]
        public List<string> AttributeTags { get; set; }

        [DataMember(Order = 29)]
        public List<string> ReferenceLayers { get; set; }

        [DataMember(Order = 30)]
        public List<QtoBlockReferenceSample> ReferenceSamples { get; set; }

        [DataMember(Order = 31)]
        public string DefinitionSignature { get; set; }

        [DataMember(Order = 32)]
        public string TechnicalNote { get; set; }

        public void RefreshStatusDisplayName()
        {
            Status = QtoBlockCatalogStatus.Normalize(Status);
            StatusDisplayName = QtoBlockCatalogStatus.GetDisplayName(Status);
        }
    }

    [DataContract]
    public class QtoBlockReferenceSample
    {
        [DataMember(Order = 1)]
        public string BlockName { get; set; }

        [DataMember(Order = 2)]
        public string Layer { get; set; }

        [DataMember(Order = 3)]
        public double PositionX { get; set; }

        [DataMember(Order = 4)]
        public double PositionY { get; set; }

        [DataMember(Order = 5)]
        public double PositionZ { get; set; }

        [DataMember(Order = 6)]
        public double ScaleX { get; set; }

        [DataMember(Order = 7)]
        public double ScaleY { get; set; }

        [DataMember(Order = 8)]
        public double ScaleZ { get; set; }

        [DataMember(Order = 9)]
        public double Rotation { get; set; }

        [DataMember(Order = 10)]
        public Dictionary<string, string> Attributes { get; set; }
    }

    [DataContract]
    public class QtoBlockCatalogMessage
    {
        [DataMember(Order = 1)]
        public string UserMessage { get; set; }

        [DataMember(Order = 2)]
        public string TechnicalMessage { get; set; }

        [DataMember(Order = 3)]
        public DateTime CreatedAt { get; set; }
    }

    public class QtoLegendDwgScanResult
    {
        public QtoLegendDwgScanResult()
        {
            Items = new List<QtoBlockCatalogItem>();
            UserMessage = string.Empty;
            TechnicalMessage = string.Empty;
        }

        public string SourceLegendDwg { get; set; }
        public DateTime ScannedAt { get; set; }
        public List<QtoBlockCatalogItem> Items { get; set; }
        public int DefinitionCount { get; set; }
        public int ReferenceCount { get; set; }
        public string UserMessage { get; set; }
        public string TechnicalMessage { get; set; }
    }

    public class QtoBlockCatalogOperationResult
    {
        public QtoBlockCatalogOperationResult()
        {
            Catalog = new QtoBlockCatalog();
            UserMessage = string.Empty;
            TechnicalMessage = string.Empty;
        }

        public bool Success { get; set; }
        public QtoBlockCatalog Catalog { get; set; }
        public string CatalogPath { get; set; }
        public string UserMessage { get; set; }
        public string TechnicalMessage { get; set; }
        public int NewItemCount { get; set; }
        public int UpdatedItemCount { get; set; }
        public int MissingItemCount { get; set; }
        public int ConflictItemCount { get; set; }
    }
}
