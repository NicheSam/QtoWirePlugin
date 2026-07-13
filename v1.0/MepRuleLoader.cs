using System;
using System.Collections.Generic;

namespace QtoWirePlugin
{
    public static class MepRuleLoader
    {
        private static readonly List<MepDimRule> BuiltInRules = CreateBuiltInRules();

        public static IList<MepDimRule> GetBuiltInRules()
        {
            return new List<MepDimRule>(BuiltInRules);
        }

        public static MepDimRule FindRule(string blockName)
        {
            if (string.IsNullOrWhiteSpace(blockName))
            {
                return null;
            }

            foreach (MepDimRule rule in BuiltInRules)
            {
                if (string.Equals(rule.BlockName, blockName, StringComparison.OrdinalIgnoreCase))
                {
                    return rule;
                }
            }

            return null;
        }

        private static List<MepDimRule> CreateBuiltInRules()
        {
            List<MepDimRule> rules = new List<MepDimRule>();

            rules.Add(CreateRule("PANEL_LIGHT_600x600", "平板燈", "L1 方形平板燈", MepDimensionPointMode.BoundingBoxCenter, "開孔依型錄確認", true, MepLayerHelper.TagLayerName));
            rules.Add(CreateRule("SMOKE_DETECTOR", "偵煙", "SD 偵煙", MepDimensionPointMode.InsertionPoint, string.Empty, false, MepLayerHelper.TagLayerName));
            rules.Add(CreateRule("SPRINKLER", "灑水頭", "SP 灑水頭", MepDimensionPointMode.InsertionPoint, string.Empty, true, MepLayerHelper.TagLayerName));
            rules.Add(CreateRule("ACCESS_PANEL_600x600", "維修孔", "AP 600x600", MepDimensionPointMode.BoundingBoxCenter, "開孔 600x600", true, MepLayerHelper.AccessPanelLayerName));
            rules.Add(CreateRule("ELV_OUTLET", "弱電出線口", "弱電出線口", MepDimensionPointMode.InsertionPoint, string.Empty, false, MepLayerHelper.TagLayerName));
            rules.Add(CreateRule("EXHAUST_GRILLE", "排煙口", "排煙口", MepDimensionPointMode.BoundingBoxCenter, "開孔依空調確認", true, MepLayerHelper.OpeningNoteLayerName));
            rules.Add(CreateRule("AIR_DIFFUSER", "空調風口", "空調風口", MepDimensionPointMode.BoundingBoxCenter, "開孔依空調確認", true, MepLayerHelper.OpeningNoteLayerName));

            return rules;
        }

        private static MepDimRule CreateRule(string blockName, string category, string label, MepDimensionPointMode pointMode, string openingSize, bool requiresReview, string outputLayer)
        {
            MepDimRule rule = new MepDimRule();
            rule.BlockName = blockName;
            rule.Category = category;
            rule.Label = label;
            rule.DimensionPointMode = pointMode;
            rule.ShowOpeningSize = !string.IsNullOrWhiteSpace(openingSize);
            rule.OpeningSize = openingSize ?? string.Empty;
            rule.RequiresReview = requiresReview;
            rule.OutputLayer = outputLayer;
            return rule;
        }
    }
}
