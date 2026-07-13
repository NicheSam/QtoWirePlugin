using System;
using System.Collections.Generic;

namespace QtoWirePlugin
{
    public static class QtoSystemDefaults
    {
        private static readonly string[] DefaultValues =
        {
            "弱電",
            "停管",
            "資訊",
            "TV",
            "CCTV",
            "BA",
            "視聽音響",
            "緊急廣播"
        };

        public static string[] GetValues()
        {
            return (string[])DefaultValues.Clone();
        }

        public static void AddTo(ICollection<string> target)
        {
            if (target == null)
            {
                return;
            }

            foreach (string value in DefaultValues)
            {
                target.Add(value);
            }
        }

        public static string[] OrderValues(IEnumerable<string> values)
        {
            HashSet<string> source = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (values != null)
            {
                foreach (string value in values)
                {
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        source.Add(value.Trim());
                    }
                }
            }

            if (source.Count == 0)
            {
                return GetValues();
            }

            List<string> ordered = new List<string>();
            foreach (string defaultValue in DefaultValues)
            {
                if (source.Remove(defaultValue))
                {
                    ordered.Add(defaultValue);
                }
            }

            List<string> customValues = new List<string>(source);
            customValues.Sort(StringComparer.OrdinalIgnoreCase);
            ordered.AddRange(customValues);
            return ordered.ToArray();
        }
    }
}
