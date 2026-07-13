using System.Collections.Generic;
using System.Windows.Forms;

namespace QtoWirePlugin
{
    internal static class QtoDialogService
    {
        public static void ShowReview(IEnumerable<QtoReviewItem> reviewItems)
        {
            QtoExternalWindowHost.ShowModeless(new QtoReviewForm(reviewItems));
        }

        public static void ShowRepair(IEnumerable<QtoRepairPlanItem> repairItems, IWin32Window owner)
        {
            using (QtoRepairForm form = new QtoRepairForm(repairItems))
            {
                QtoExternalWindowHost.ShowModal(form, owner);
            }
        }

        public static void ShowSettings(IWin32Window owner)
        {
            using (QtoSettingsForm form = new QtoSettingsForm())
            {
                QtoExternalWindowHost.ShowModal(form, owner);
            }
        }

        public static bool EditBudgetLayout(
            QtoBudgetLayoutSettings settings,
            IWin32Window owner,
            out QtoBudgetLayoutSettings result)
        {
            using (QtoBudgetLayoutSettingsForm form = new QtoBudgetLayoutSettingsForm(settings))
            {
                if (QtoExternalWindowHost.ShowModal(form, owner) == DialogResult.OK && form.ResultSettings != null)
                {
                    result = form.ResultSettings;
                    return true;
                }
            }

            result = settings;
            return false;
        }
    }
}
