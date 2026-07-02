using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Packaging;
using System.Text;
using System.Xml;

namespace QtoWirePlugin
{
    internal enum QtoWorksheetVisibility
    {
        Visible,
        Hidden,
        VeryHidden
    }

    internal sealed class QtoWorksheetData
    {
        public string Name { get; set; }
        public object[,] Values { get; set; }
        public QtoWorksheetVisibility Visibility { get; set; }
    }

    internal static class QtoSimpleXlsxWriter
    {
        private const string SpreadsheetNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        private const string RelationshipNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        private const string OfficeDocumentRelationship = "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument";
        private const string WorksheetRelationship = "http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet";
        private const string StylesRelationship = "http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles";
        private const string WorkbookContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml";
        private const string WorksheetContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml";
        private const string StylesContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml";

        public static void WriteWorkbook(string workbookPath, IList<QtoWorksheetData> worksheets)
        {
            if (string.IsNullOrWhiteSpace(workbookPath))
            {
                throw new ArgumentException("workbookPath is required.", "workbookPath");
            }

            string fullPath = Path.GetFullPath(workbookPath);
            string directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            IList<QtoWorksheetData> safeWorksheets = worksheets == null || worksheets.Count == 0
                ? new List<QtoWorksheetData> { CreateEmptyWorksheet() }
                : worksheets;

            using (Package package = Package.Open(fullPath, FileMode.Create, FileAccess.ReadWrite))
            {
                Uri workbookUri = PackUriHelper.CreatePartUri(new Uri("/xl/workbook.xml", UriKind.Relative));
                PackagePart workbookPart = package.CreatePart(workbookUri, WorkbookContentType, CompressionOption.Maximum);
                package.CreateRelationship(workbookUri, TargetMode.Internal, OfficeDocumentRelationship);

                PackageRelationshipCollection relationshipCollection = workbookPart.GetRelationshipsByType(WorksheetRelationship);
                int relIndex = 1;
                List<string> relationshipIds = new List<string>();
                foreach (QtoWorksheetData worksheet in safeWorksheets)
                {
                    Uri sheetUri = PackUriHelper.CreatePartUri(new Uri("/xl/worksheets/sheet" + relIndex.ToString(CultureInfo.InvariantCulture) + ".xml", UriKind.Relative));
                    PackagePart sheetPart = package.CreatePart(sheetUri, WorksheetContentType, CompressionOption.Maximum);
                    string relId = "rId" + relIndex.ToString(CultureInfo.InvariantCulture);
                    workbookPart.CreateRelationship(sheetUri, TargetMode.Internal, WorksheetRelationship, relId);
                    relationshipIds.Add(relId);
                    WriteWorksheetPart(sheetPart, worksheet);
                    relIndex++;
                }

                Uri stylesUri = PackUriHelper.CreatePartUri(new Uri("/xl/styles.xml", UriKind.Relative));
                PackagePart stylesPart = package.CreatePart(stylesUri, StylesContentType, CompressionOption.Maximum);
                workbookPart.CreateRelationship(stylesUri, TargetMode.Internal, StylesRelationship, "rIdStyles");
                WriteStylesPart(stylesPart);

                WriteWorkbookPart(workbookPart, safeWorksheets, relationshipIds);
            }
        }

        private static QtoWorksheetData CreateEmptyWorksheet()
        {
            object[,] values = new object[2, 2];
            values[1, 1] = "QTO";
            return new QtoWorksheetData
            {
                Name = "QTO",
                Values = values,
                Visibility = QtoWorksheetVisibility.Visible
            };
        }

        private static void WriteWorkbookPart(PackagePart workbookPart, IList<QtoWorksheetData> worksheets, IList<string> relationshipIds)
        {
            XmlWriterSettings settings = CreateXmlSettings();
            using (XmlWriter writer = XmlWriter.Create(workbookPart.GetStream(FileMode.Create, FileAccess.Write), settings))
            {
                writer.WriteStartDocument(true);
                writer.WriteStartElement("workbook", SpreadsheetNs);
                writer.WriteAttributeString("xmlns", "r", null, RelationshipNs);
                writer.WriteStartElement("sheets", SpreadsheetNs);

                for (int i = 0; i < worksheets.Count; i++)
                {
                    writer.WriteStartElement("sheet", SpreadsheetNs);
                    writer.WriteAttributeString("name", SanitizeSheetName(worksheets[i].Name, i + 1));
                    writer.WriteAttributeString("sheetId", (i + 1).ToString(CultureInfo.InvariantCulture));
                    writer.WriteAttributeString("r", "id", RelationshipNs, relationshipIds[i]);
                    string state = ToSheetState(worksheets[i].Visibility);
                    if (!string.IsNullOrWhiteSpace(state))
                    {
                        writer.WriteAttributeString("state", state);
                    }

                    writer.WriteEndElement();
                }

                writer.WriteEndElement();
                writer.WriteEndElement();
            }
        }

        private static void WriteWorksheetPart(PackagePart sheetPart, QtoWorksheetData worksheet)
        {
            object[,] values = worksheet == null ? null : worksheet.Values;
            int rowCount = values == null ? 1 : Math.Max(1, values.GetLength(0) - 1);
            int columnCount = values == null ? 1 : Math.Max(1, values.GetLength(1) - 1);

            XmlWriterSettings settings = CreateXmlSettings();
            using (XmlWriter writer = XmlWriter.Create(sheetPart.GetStream(FileMode.Create, FileAccess.Write), settings))
            {
                writer.WriteStartDocument(true);
                writer.WriteStartElement("worksheet", SpreadsheetNs);
                writer.WriteStartElement("sheetData", SpreadsheetNs);

                for (int row = 1; row <= rowCount; row++)
                {
                    writer.WriteStartElement("row", SpreadsheetNs);
                    writer.WriteAttributeString("r", row.ToString(CultureInfo.InvariantCulture));

                    for (int column = 1; column <= columnCount; column++)
                    {
                        object value = values == null ? null : values[row, column];
                        if (value == null)
                        {
                            continue;
                        }

                        WriteCell(writer, row, column, value, row == 1);
                    }

                    writer.WriteEndElement();
                }

                writer.WriteEndElement();
                writer.WriteEndElement();
            }
        }

        private static void WriteCell(XmlWriter writer, int row, int column, object value, bool header)
        {
            writer.WriteStartElement("c", SpreadsheetNs);
            writer.WriteAttributeString("r", ColumnName(column) + row.ToString(CultureInfo.InvariantCulture));
            if (header)
            {
                writer.WriteAttributeString("s", "1");
            }

            string formulaValue = value as string;
            if (!string.IsNullOrWhiteSpace(formulaValue) && formulaValue.StartsWith("=", StringComparison.Ordinal))
            {
                writer.WriteElementString("f", SpreadsheetNs, formulaValue.Substring(1));
            }
            else
            {
                double numericValue;
                if (TryGetNumber(value, out numericValue))
                {
                    writer.WriteElementString("v", SpreadsheetNs, numericValue.ToString("0.##########", CultureInfo.InvariantCulture));
                }
                else
                {
                    writer.WriteAttributeString("t", "inlineStr");
                    writer.WriteStartElement("is", SpreadsheetNs);
                    writer.WriteElementString("t", SpreadsheetNs, SanitizeXmlText(Convert.ToString(value, CultureInfo.InvariantCulture)));
                    writer.WriteEndElement();
                }
            }

            writer.WriteEndElement();
        }

        private static void WriteStylesPart(PackagePart stylesPart)
        {
            XmlWriterSettings settings = CreateXmlSettings();
            using (XmlWriter writer = XmlWriter.Create(stylesPart.GetStream(FileMode.Create, FileAccess.Write), settings))
            {
                writer.WriteStartDocument(true);
                writer.WriteStartElement("styleSheet", SpreadsheetNs);
                writer.WriteRaw("<fonts count=\"2\"><font><sz val=\"11\"/><name val=\"Calibri\"/></font><font><b/><sz val=\"11\"/><name val=\"Calibri\"/></font></fonts>");
                writer.WriteRaw("<fills count=\"1\"><fill><patternFill patternType=\"none\"/></fill></fills>");
                writer.WriteRaw("<borders count=\"1\"><border><left/><right/><top/><bottom/><diagonal/></border></borders>");
                writer.WriteRaw("<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>");
                writer.WriteRaw("<cellXfs count=\"2\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/><xf numFmtId=\"0\" fontId=\"1\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyFont=\"1\"/></cellXfs>");
                writer.WriteEndElement();
            }
        }

        private static XmlWriterSettings CreateXmlSettings()
        {
            return new XmlWriterSettings
            {
                Encoding = new UTF8Encoding(false),
                Indent = false
            };
        }

        private static bool TryGetNumber(object value, out double number)
        {
            number = 0;
            if (value is byte || value is short || value is int || value is long || value is float || value is double || value is decimal)
            {
                number = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                return !double.IsNaN(number) && !double.IsInfinity(number);
            }

            return false;
        }

        private static string ColumnName(int column)
        {
            string name = string.Empty;
            int value = column;
            while (value > 0)
            {
                value--;
                name = (char)('A' + (value % 26)) + name;
                value /= 26;
            }

            return name;
        }

        private static string SanitizeSheetName(string name, int index)
        {
            string value = string.IsNullOrWhiteSpace(name) ? "Sheet" + index.ToString(CultureInfo.InvariantCulture) : name;
            char[] invalid = new[] { '\\', '/', '?', '*', '[', ']', ':' };
            foreach (char item in invalid)
            {
                value = value.Replace(item, '_');
            }

            return value.Length > 31 ? value.Substring(0, 31) : value;
        }

        private static string ToSheetState(QtoWorksheetVisibility visibility)
        {
            if (visibility == QtoWorksheetVisibility.Hidden)
            {
                return "hidden";
            }

            if (visibility == QtoWorksheetVisibility.VeryHidden)
            {
                return "veryHidden";
            }

            return string.Empty;
        }

        private static string SanitizeXmlText(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            StringBuilder builder = new StringBuilder(text.Length);
            foreach (char c in text)
            {
                if (XmlConvert.IsXmlChar(c))
                {
                    builder.Append(c);
                }
            }

            return builder.ToString();
        }
    }
}
