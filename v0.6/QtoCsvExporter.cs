using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Autodesk.AutoCAD.DatabaseServices;

namespace QtoWirePlugin
{
    public static class QtoCsvExporter
    {
        public static string ExportOutlets(List<OutletInfo> outlets, Database db)
        {
            return ExportOutlets(outlets, db, null);
        }

        public static string ExportOutlets(List<OutletInfo> outlets, Database db, string outputPath)
        {
            if (outlets == null)
            {
                outlets = new List<OutletInfo>();
            }

            if (string.IsNullOrWhiteSpace(outputPath))
            {
                outputPath = BuildDefaultOutputPath(db, "QTO_OUTLETS_");
            }

            List<string[]> rows = new List<string[]>();
            rows.Add(new string[]
            {
                "出線口編號",
                "結線箱編號",
                "系統",
                "線材類型",
                "X座標",
                "Y座標",
                "Z座標",
                "圖塊名稱",
                "狀態",
                "錯誤訊息"
            });

            foreach (OutletInfo outlet in outlets)
            {
                string status = "OK";
                string errorMessage = BuildOutletErrorMessage(outlet);

                if (!string.IsNullOrWhiteSpace(errorMessage))
                {
                    status = "ERROR";
                }

                rows.Add(new string[]
                {
                    outlet.OutletId ?? string.Empty,
                    outlet.JbId ?? string.Empty,
                    outlet.System ?? string.Empty,
                    outlet.CableType ?? string.Empty,
                    outlet.Position.X.ToString("0.###"),
                    outlet.Position.Y.ToString("0.###"),
                    outlet.Position.Z.ToString("0.###"),
                    outlet.BlockName ?? string.Empty,
                    ToChineseStatus(status),
                    ToChineseErrorMessage(errorMessage)
                });
            }

            WriteCsv(outputPath, rows);
            return outputPath;
        }

        public static string ExportWireRecalc(List<WireInfo> wires, Database db)
        {
            return ExportWireRecalc(wires, db, null);
        }

        public static string ExportWireRecalc(List<WireInfo> wires, Database db, string outputPath)
        {
            if (wires == null)
            {
                wires = new List<WireInfo>();
            }

            if (string.IsNullOrWhiteSpace(outputPath))
            {
                outputPath = BuildDefaultOutputPath(db, "QTO_WIRE_RECALC_");
            }

            List<string[]> rows = new List<string[]>();
            rows.Add(new string[]
            {
                "出線口編號",
                "結線箱編號",
                "系統",
                "線材類型",
                "配線物件ID",
                "長度_公尺",
                "狀態",
                "錯誤訊息"
            });

            foreach (WireInfo wire in wires)
            {
                string status = "OK";
                string errorMessage = BuildWireErrorMessage(wire);

                if (!string.IsNullOrWhiteSpace(errorMessage))
                {
                    status = "ERROR";
                }

                rows.Add(new string[]
                {
                    wire.OutletId ?? string.Empty,
                    wire.JbId ?? string.Empty,
                    wire.System ?? string.Empty,
                    wire.CableType ?? string.Empty,
                    wire.ObjectId.ToString(),
                    wire.LengthM.ToString("0.000"),
                    ToChineseStatus(status),
                    ToChineseErrorMessage(errorMessage)
                });
            }

            WriteCsv(outputPath, rows);
            return outputPath;
        }

        public static string ExportWireCheck(List<CheckResult> results, Database db)
        {
            return ExportWireCheck(results, db, null);
        }

        public static string ExportWireCheck(List<CheckResult> results, Database db, string outputPath)
        {
            if (results == null)
            {
                results = new List<CheckResult>();
            }

            if (string.IsNullOrWhiteSpace(outputPath))
            {
                outputPath = BuildDefaultOutputPath(db, "QTO_WIRE_CHECK_");
            }

            List<string[]> rows = new List<string[]>();
            rows.Add(new string[]
            {
                "項目類型",
                "物件ID",
                "出線口編號",
                "結線箱編號",
                "系統",
                "線材類型",
                "長度_公尺",
                "狀態",
                "錯誤訊息"
            });

            foreach (CheckResult result in results)
            {
                rows.Add(new string[]
                {
                    ToChineseItemType(result.ItemType),
                    result.ObjectId ?? string.Empty,
                    result.OutletId ?? string.Empty,
                    result.JbId ?? string.Empty,
                    result.System ?? string.Empty,
                    result.CableType ?? string.Empty,
                    result.LengthM.ToString("0.000"),
                    ToChineseStatus(result.Status),
                    ToChineseErrorMessage(result.ErrorMessage)
                });
            }

            WriteCsv(outputPath, rows);
            return outputPath;
        }

        public static string ExportBatchBindReport(List<BatchBindResult> results, Database db, string outputPath)
        {
            if (results == null)
            {
                results = new List<BatchBindResult>();
            }

            if (string.IsNullOrWhiteSpace(outputPath))
            {
                outputPath = BuildDefaultOutputPath(db, "QTO_BATCH_BIND_WIRE_");
            }

            List<string[]> rows = new List<string[]>();
            rows.Add(new string[]
            {
                "出線口編號",
                "結線箱編號",
                "配對配線物件ID",
                "距離_mm",
                "狀態",
                "錯誤訊息"
            });

            foreach (BatchBindResult result in results)
            {
                rows.Add(new string[]
                {
                    result.OutletId ?? string.Empty,
                    result.JbId ?? string.Empty,
                    result.MatchedWireObjectId ?? string.Empty,
                    result.Distance.ToString("0.###"),
                    ToChineseStatus(result.Status),
                    ToChineseErrorMessage(result.ErrorMessage)
                });
            }

            WriteCsv(outputPath, rows);
            return outputPath;
        }

        public static string ExportJunctionBoxSummary(List<JunctionBoxSummaryInfo> summaries, Database db, string outputPath)
        {
            if (summaries == null)
            {
                summaries = new List<JunctionBoxSummaryInfo>();
            }

            if (string.IsNullOrWhiteSpace(outputPath))
            {
                outputPath = BuildDefaultOutputPath(db, "QTO_JB_SUMMARY_");
            }

            List<string[]> rows = new List<string[]>();
            rows.Add(new string[]
            {
                "結線箱編號",
                "系統",
                "線材類型",
                "出線口數量",
                "配線數量",
                "總長度_公尺"
            });

            foreach (JunctionBoxSummaryInfo summary in summaries)
            {
                rows.Add(new string[]
                {
                    summary.JbId ?? string.Empty,
                    summary.System ?? string.Empty,
                    summary.CableType ?? string.Empty,
                    summary.OutletCount.ToString(),
                    summary.WireCount.ToString(),
                    summary.TotalLengthM.ToString("0.000")
                });
            }

            WriteCsv(outputPath, rows);
            return outputPath;
        }

        public static string ExportJunctionBoxDetailSummary(List<JunctionBoxSummaryInfo> summaries, List<WireInfo> wires, Database db, string outputPath)
        {
            if (summaries == null)
            {
                summaries = new List<JunctionBoxSummaryInfo>();
            }

            if (wires == null)
            {
                wires = new List<WireInfo>();
            }

            if (string.IsNullOrWhiteSpace(outputPath))
            {
                outputPath = BuildDefaultOutputPath(db, "QTO_JB_SUMMARY_");
            }

            List<string[]> rows = new List<string[]>();
            rows.Add(new string[]
            {
                "\u8cc7\u6599\u985e\u578b",
                "\u7d50\u7dda\u7bb1\u7de8\u865f",
                "\u51fa\u7dda\u53e3\u7de8\u865f",
                "\u7cfb\u7d71",
                "\u7dda\u6750\u985e\u578b",
                "\u914d\u7dda\u7269\u4ef6ID",
                "\u9577\u5ea6_\u516c\u5c3a",
                "\u51fa\u7dda\u53e3\u6578\u91cf",
                "\u914d\u7dda\u6578\u91cf",
                "\u7d50\u7dda\u7bb1\u7e3d\u9577\u5ea6_\u516c\u5c3a",
                "\u5099\u8a3b"
            });

            wires.Sort(CompareWireForReport);

            foreach (WireInfo wire in wires)
            {
                rows.Add(new string[]
                {
                    "\u660e\u7d30",
                    wire.JbId ?? string.Empty,
                    wire.OutletId ?? string.Empty,
                    wire.System ?? string.Empty,
                    wire.CableType ?? string.Empty,
                    wire.ObjectId.ToString(),
                    wire.LengthM.ToString("0.000"),
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty
                });
            }

            if (wires.Count > 0 && summaries.Count > 0)
            {
                rows.Add(new string[]
                {
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty
                });
            }

            foreach (JunctionBoxSummaryInfo summary in summaries)
            {
                rows.Add(new string[]
                {
                    "\u5c0f\u8a08",
                    summary.JbId ?? string.Empty,
                    string.Empty,
                    summary.System ?? string.Empty,
                    summary.CableType ?? string.Empty,
                    string.Empty,
                    string.Empty,
                    summary.OutletCount.ToString(),
                    summary.WireCount.ToString(),
                    summary.TotalLengthM.ToString("0.000"),
                    "\u8a72\u7d50\u7dda\u7bb1\u7d71\u8a08"
                });
            }

            WriteCsv(outputPath, rows);
            return outputPath;
        }

        private static int CompareWireForReport(WireInfo left, WireInfo right)
        {
            int jbCompare = string.Compare(left.JbId, right.JbId, StringComparison.OrdinalIgnoreCase);

            if (jbCompare != 0)
            {
                return jbCompare;
            }

            return string.Compare(left.OutletId, right.OutletId, StringComparison.OrdinalIgnoreCase);
        }

        public static string BuildDefaultOutputPath(Database db, string prefix)
        {
            string folder = GetDefaultExportFolder(db);
            string fileName = prefix + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".csv";
            return Path.Combine(folder, fileName);
        }

        public static void WriteCsvFile(string outputPath, List<string[]> rows)
        {
            WriteCsv(outputPath, rows);
        }

        private static string GetDefaultExportFolder(Database db)
        {
            if (db != null && !string.IsNullOrWhiteSpace(db.Filename))
            {
                string dwgFolder = Path.GetDirectoryName(db.Filename);

                if (!string.IsNullOrWhiteSpace(dwgFolder) && Directory.Exists(dwgFolder))
                {
                    return dwgFolder;
                }
            }

            return Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        }

        private static void WriteCsv(string outputPath, List<string[]> rows)
        {
            using (StreamWriter writer = new StreamWriter(outputPath, false, new UTF8Encoding(true)))
            {
                foreach (string[] row in rows)
                {
                    writer.WriteLine(BuildCsvLine(row));
                }
            }
        }

        private static string BuildOutletErrorMessage(OutletInfo outlet)
        {
            List<string> errors = new List<string>();

            if (string.IsNullOrWhiteSpace(outlet.OutletId))
            {
                errors.Add("Missing OUTLET_ID");
            }

            if (string.IsNullOrWhiteSpace(outlet.JbId))
            {
                errors.Add("Missing JB_ID");
            }

            return string.Join("; ", errors.ToArray());
        }

        private static string BuildWireErrorMessage(WireInfo wire)
        {
            List<string> errors = new List<string>();

            if (string.IsNullOrWhiteSpace(wire.OutletId))
            {
                errors.Add("Missing OUTLET_ID");
            }

            if (string.IsNullOrWhiteSpace(wire.JbId))
            {
                errors.Add("Missing JB_ID");
            }

            if (wire.LengthM <= 0.0)
            {
                errors.Add("Length is zero");
            }

            return string.Join("; ", errors.ToArray());
        }

        private static string BuildCsvLine(string[] values)
        {
            List<string> escapedValues = new List<string>();

            foreach (string value in values)
            {
                escapedValues.Add(EscapeCsv(value));
            }

            return string.Join(",", escapedValues.ToArray());
        }

        private static string ToChineseStatus(string status)
        {
            if (string.Equals(status, "OK", StringComparison.OrdinalIgnoreCase))
            {
                return "正常";
            }

            if (string.Equals(status, "ERROR", StringComparison.OrdinalIgnoreCase))
            {
                return "錯誤";
            }

            return status ?? string.Empty;
        }

        private static string ToChineseItemType(string itemType)
        {
            if (string.Equals(itemType, "OUTLET", StringComparison.OrdinalIgnoreCase))
            {
                return "出線口";
            }

            if (string.Equals(itemType, "JUNCTION_BOX", StringComparison.OrdinalIgnoreCase))
            {
                return "結線箱";
            }

            if (string.Equals(itemType, "WIRE", StringComparison.OrdinalIgnoreCase))
            {
                return "配線";
            }

            return itemType ?? string.Empty;
        }

        private static string ToChineseErrorMessage(string errorMessage)
        {
            if (string.IsNullOrWhiteSpace(errorMessage))
            {
                return string.Empty;
            }

            string translated = errorMessage;
            translated = translated.Replace("Missing OUTLET_ID", "缺少出線口編號");
            translated = translated.Replace("Missing JB_ID", "缺少結線箱編號");
            translated = translated.Replace("Length is zero", "長度為 0");
            translated = translated.Replace("Invalid XData", "XData 資料異常");
            translated = translated.Replace("Missing wire", "缺少配線");
            translated = translated.Replace("Multiple wires for OUTLET_ID", "同一出線口有多條配線");
            translated = translated.Replace("OUTLET_ID does not exist", "配線綁定的出線口不存在");
            translated = translated.Replace("JB_ID does not exist", "配線綁定的結線箱不存在");
            translated = translated.Replace("Wire is not on QTO_WIRE layer", "配線不在 QTO_WIRE 圖層");
            return translated;
        }

        private static string EscapeCsv(string value)
        {
            if (value == null)
            {
                value = string.Empty;
            }

            bool mustQuote = value.IndexOf(',') >= 0 || value.IndexOf('"') >= 0 || value.IndexOf('\r') >= 0 || value.IndexOf('\n') >= 0;

            if (value.IndexOf('"') >= 0)
            {
                value = value.Replace("\"", "\"\"");
            }

            if (mustQuote)
            {
                return "\"" + value + "\"";
            }

            return value;
        }
    }
}
