using System;
using System.Windows.Forms;
using AcadApplication = Autodesk.AutoCAD.ApplicationServices.Application;

namespace QtoWirePlugin
{
    internal static class QtoExternalWindowHost
    {
        public static void ShowModeless(Form form)
        {
            if (form == null)
            {
                return;
            }

            try
            {
                AcadApplication.ShowModelessDialog(form);
            }
            catch (InvalidOperationException)
            {
                form.Show();
            }
        }

        public static DialogResult ShowModal(Form form, IWin32Window owner)
        {
            if (form == null)
            {
                return DialogResult.Cancel;
            }

            try
            {
                return AcadApplication.ShowModalDialog(form);
            }
            catch (InvalidOperationException)
            {
                return owner == null ? form.ShowDialog() : form.ShowDialog(owner);
            }
        }
    }
}
