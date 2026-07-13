using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Packaging;
using System.Xml;

namespace QtoWirePlugin
{
    internal static class QtoSimpleXlsxReader
    {
        private const string SpreadsheetNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        private const string RelationshipNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        private const string PackageRelationshipNs = "http://schemas.openxmlformats.org/package/2006/relationships";

        public static IList<string> GetSheetNames(string workbookPath)
        {
            List<string> names = new List<string>();
            if (string.IsNullOrWhiteSpace(workbookPath) || !File.Exists(workbookPath))
            {
                return names;
            }

            using (Package package = Package.Open(workbookPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                Uri workbookUri = PackUriHelper.CreatePartUri(new Uri("/xl/workbook.xml", UriKind.Relative));
                if (!package.PartExists(workbookUri))
                {
                    return names;
                }

                XmlDocument document = LoadXml(package.GetPart(workbookUri));
                XmlNamespaceManager ns = CreateNamespaceManager(document);
                foreach (XmlNode sheet in document.SelectNodes("/ss:workbook/ss:sheets/ss:sheet", ns))
                {
                    XmlAttribute name = sheet.Attributes["name"];
                    if (name != null && !string.IsNullOrWhiteSpace(name.Value))
                    {
                        names.Add(name.Value);
                    }
                }
            }

            return names;
        }

        public static SortedDictionary<int, Dictionary<int, string>> ReadRawRows(string workbookPath, string sheetName)
        {
            SortedDictionary<int, Dictionary<int, string>> result = new SortedDictionary<int, Dictionary<int, string>>();
            if (string.IsNullOrWhiteSpace(workbookPath) || !File.Exists(workbookPath))
            {
                return result;
            }

            using (Package package = Package.Open(workbookPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                Uri worksheetUri = FindWorksheetUri(package, sheetName);
                if (worksheetUri == null)
                {
                    return result;
                }

                foreach (KeyValuePair<int, Dictionary<int, string>> row in ReadWorksheetRowsStreaming(package.GetPart(worksheetUri), ReadSharedStrings(package), 50))
                {
                    if (row.Value.Count > 0)
                    {
                        result[row.Key] = row.Value;
                    }
                }
            }

            return result;
        }

        private static SortedDictionary<int, Dictionary<int, string>> ReadWorksheetRowsStreaming(PackagePart worksheetPart, IList<string> sharedStrings, int emptyLimit)
        {
            SortedDictionary<int, Dictionary<int, string>> rows = new SortedDictionary<int, Dictionary<int, string>>();
            int emptyRun = 0;
            bool sawContent = false;
            XmlReaderSettings settings = new XmlReaderSettings { IgnoreComments = true, IgnoreWhitespace = true };
            using (Stream stream = worksheetPart.GetStream(FileMode.Open, FileAccess.Read))
            using (XmlReader reader = XmlReader.Create(stream, settings))
            {
                while (reader.Read())
                {
                    if (reader.NodeType != XmlNodeType.Element || reader.LocalName != "row")
                    {
                        continue;
                    }

                    string rowText = reader.ReadOuterXml();
                    XmlDocument rowDocument = new XmlDocument();
                    rowDocument.LoadXml(rowText);
                    XmlNamespaceManager ns = CreateNamespaceManager(rowDocument);
                    XmlElement rowElement = rowDocument.DocumentElement;
                    int rowIndex;
                    if (rowElement == null || !int.TryParse(rowElement.GetAttribute("r"), NumberStyles.Integer, CultureInfo.InvariantCulture, out rowIndex))
                    {
                        continue;
                    }

                    Dictionary<int, string> cells = new Dictionary<int, string>();
                    foreach (XmlNode cellNode in rowElement.SelectNodes("ss:c", ns))
                    {
                        XmlAttribute reference = cellNode.Attributes["r"];
                        int column = reference == null ? 0 : ParseColumnIndex(reference.Value);
                        if (column <= 0) continue;
                        string value = ReadCellValue(cellNode, ns, sharedStrings);
                        if (!string.IsNullOrWhiteSpace(value)) cells[column] = value;
                    }

                    if (cells.Count > 0)
                    {
                        rows[rowIndex] = cells;
                        emptyRun = 0;
                        sawContent = true;
                    }
                    else if (sawContent && ++emptyRun >= emptyLimit)
                    {
                        break;
                    }
                }
            }
            return rows;
        }

        public static IList<Dictionary<string, string>> ReadTable(string workbookPath, string sheetName)
        {
            List<Dictionary<string, string>> result = new List<Dictionary<string, string>>();
            if (string.IsNullOrWhiteSpace(workbookPath) || string.IsNullOrWhiteSpace(sheetName) || !File.Exists(workbookPath))
            {
                return result;
            }

            try
            {
                using (Package package = Package.Open(workbookPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    List<string> sharedStrings = ReadSharedStrings(package);
                    Uri worksheetUri = FindWorksheetUri(package, sheetName);
                    if (worksheetUri == null)
                    {
                        return result;
                    }

                    PackagePart worksheetPart = package.GetPart(worksheetUri);
                    Dictionary<int, Dictionary<int, string>> rows = ReadWorksheetRows(worksheetPart, sharedStrings);
                    Dictionary<int, string> headerRow;
                    if (!rows.TryGetValue(1, out headerRow))
                    {
                        return result;
                    }

                    Dictionary<int, string> headers = new Dictionary<int, string>();
                    foreach (KeyValuePair<int, string> pair in headerRow)
                    {
                        if (!string.IsNullOrWhiteSpace(pair.Value))
                        {
                            headers[pair.Key] = pair.Value.Trim();
                        }
                    }

                    foreach (KeyValuePair<int, Dictionary<int, string>> rowPair in rows)
                    {
                        if (rowPair.Key == 1)
                        {
                            continue;
                        }

                        Dictionary<string, string> row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        bool hasValue = false;
                        foreach (KeyValuePair<int, string> header in headers)
                        {
                            string value;
                            rowPair.Value.TryGetValue(header.Key, out value);
                            row[header.Value] = value ?? string.Empty;
                            if (!string.IsNullOrWhiteSpace(value))
                            {
                                hasValue = true;
                            }
                        }

                        if (hasValue)
                        {
                            result.Add(row);
                        }
                    }
                }
            }
            catch
            {
                return result;
            }

            return result;
        }

        private static Uri FindWorksheetUri(Package package, string sheetName)
        {
            Uri workbookUri = PackUriHelper.CreatePartUri(new Uri("/xl/workbook.xml", UriKind.Relative));
            if (!package.PartExists(workbookUri))
            {
                return null;
            }

            PackagePart workbookPart = package.GetPart(workbookUri);
            Dictionary<string, string> relTargets = ReadWorkbookRelationships(package);

            XmlDocument document = LoadXml(workbookPart);
            XmlNamespaceManager ns = CreateNamespaceManager(document);
            XmlNodeList sheets = document.SelectNodes("/ss:workbook/ss:sheets/ss:sheet", ns);
            foreach (XmlNode sheet in sheets)
            {
                XmlAttribute nameAttribute = sheet.Attributes["name"];
                XmlAttribute relAttribute = sheet.Attributes["id", RelationshipNs];
                if (nameAttribute == null || relAttribute == null)
                {
                    continue;
                }

                if (!string.Equals(nameAttribute.Value, sheetName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string target;
                if (!relTargets.TryGetValue(relAttribute.Value, out target))
                {
                    return null;
                }

                if (!target.StartsWith("/", StringComparison.Ordinal))
                {
                    target = "/xl/" + target.TrimStart('/');
                }

                return PackUriHelper.CreatePartUri(new Uri(target, UriKind.Relative));
            }

            return null;
        }

        private static Dictionary<string, string> ReadWorkbookRelationships(Package package)
        {
            Dictionary<string, string> relTargets = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            Uri relsUri = PackUriHelper.CreatePartUri(new Uri("/xl/_rels/workbook.xml.rels", UriKind.Relative));
            if (!package.PartExists(relsUri))
            {
                return relTargets;
            }

            XmlDocument document = LoadXml(package.GetPart(relsUri));
            XmlNamespaceManager ns = new XmlNamespaceManager(document.NameTable);
            ns.AddNamespace("pr", PackageRelationshipNs);
            XmlNodeList relationships = document.SelectNodes("/pr:Relationships/pr:Relationship", ns);
            foreach (XmlNode relationship in relationships)
            {
                XmlAttribute id = relationship.Attributes["Id"];
                XmlAttribute target = relationship.Attributes["Target"];
                if (id == null || target == null)
                {
                    continue;
                }

                relTargets[id.Value] = target.Value;
            }

            return relTargets;
        }

        private static List<string> ReadSharedStrings(Package package)
        {
            List<string> sharedStrings = new List<string>();
            Uri uri = PackUriHelper.CreatePartUri(new Uri("/xl/sharedStrings.xml", UriKind.Relative));
            if (!package.PartExists(uri))
            {
                return sharedStrings;
            }

            XmlDocument document = LoadXml(package.GetPart(uri));
            XmlNamespaceManager ns = CreateNamespaceManager(document);
            XmlNodeList nodes = document.SelectNodes("/ss:sst/ss:si", ns);
            foreach (XmlNode item in nodes)
            {
                XmlNodeList textNodes = item.SelectNodes(".//ss:t", ns);
                string value = string.Empty;
                foreach (XmlNode textNode in textNodes)
                {
                    value += textNode.InnerText;
                }
                sharedStrings.Add(value);
            }

            return sharedStrings;
        }

        private static Dictionary<int, Dictionary<int, string>> ReadWorksheetRows(PackagePart worksheetPart, IList<string> sharedStrings)
        {
            Dictionary<int, Dictionary<int, string>> rows = new Dictionary<int, Dictionary<int, string>>();
            XmlDocument document = LoadXml(worksheetPart);
            XmlNamespaceManager ns = CreateNamespaceManager(document);
            XmlNodeList rowNodes = document.SelectNodes("/ss:worksheet/ss:sheetData/ss:row", ns);
            foreach (XmlNode rowNode in rowNodes)
            {
                XmlAttribute rowAttribute = rowNode.Attributes["r"];
                int rowIndex;
                if (rowAttribute == null || !int.TryParse(rowAttribute.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out rowIndex))
                {
                    continue;
                }

                Dictionary<int, string> cells = new Dictionary<int, string>();
                XmlNodeList cellNodes = rowNode.SelectNodes("ss:c", ns);
                foreach (XmlNode cellNode in cellNodes)
                {
                    XmlAttribute refAttribute = cellNode.Attributes["r"];
                    if (refAttribute == null)
                    {
                        continue;
                    }

                    int columnIndex = ParseColumnIndex(refAttribute.Value);
                    if (columnIndex <= 0)
                    {
                        continue;
                    }

                    cells[columnIndex] = ReadCellValue(cellNode, ns, sharedStrings);
                }

                rows[rowIndex] = cells;
            }

            return rows;
        }

        private static string ReadCellValue(XmlNode cellNode, XmlNamespaceManager ns, IList<string> sharedStrings)
        {
            XmlAttribute typeAttribute = cellNode.Attributes["t"];
            string type = typeAttribute == null ? string.Empty : typeAttribute.Value;

            if (string.Equals(type, "inlineStr", StringComparison.OrdinalIgnoreCase))
            {
                XmlNode textNode = cellNode.SelectSingleNode("ss:is/ss:t", ns);
                return textNode == null ? string.Empty : textNode.InnerText;
            }

            XmlNode valueNode = cellNode.SelectSingleNode("ss:v", ns);
            string rawValue = valueNode == null ? string.Empty : valueNode.InnerText;
            if (string.Equals(type, "s", StringComparison.OrdinalIgnoreCase))
            {
                int index;
                if (int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out index)
                    && index >= 0
                    && index < sharedStrings.Count)
                {
                    return sharedStrings[index];
                }
            }

            return rawValue;
        }

        private static XmlDocument LoadXml(PackagePart part)
        {
            XmlDocument document = new XmlDocument();
            using (Stream stream = part.GetStream(FileMode.Open, FileAccess.Read))
            {
                document.Load(stream);
            }
            return document;
        }

        private static XmlNamespaceManager CreateNamespaceManager(XmlDocument document)
        {
            XmlNamespaceManager ns = new XmlNamespaceManager(document.NameTable);
            ns.AddNamespace("ss", SpreadsheetNs);
            ns.AddNamespace("r", RelationshipNs);
            return ns;
        }

        private static int ParseColumnIndex(string cellReference)
        {
            int result = 0;
            foreach (char ch in cellReference)
            {
                if (ch >= 'A' && ch <= 'Z')
                {
                    result = result * 26 + (ch - 'A' + 1);
                }
                else if (ch >= 'a' && ch <= 'z')
                {
                    result = result * 26 + (ch - 'a' + 1);
                }
                else
                {
                    break;
                }
            }

            return result;
        }
    }
}
