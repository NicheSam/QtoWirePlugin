using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;

namespace QtoWirePlugin
{
    public static class MepUiHelper
    {
        public static void ShowStepMessage(string title, string body, string nextStep)
        {
            string message = title + "\n\n" + body;

            if (!string.IsNullOrWhiteSpace(nextStep))
            {
                message += "\n\n" + nextStep;
            }

            Application.ShowAlertDialog(message);
        }

        public static void WriteHeader(Editor editor, string title)
        {
            editor.WriteMessage("\n===========================================");
            editor.WriteMessage("\n" + title);
            editor.WriteMessage("\n===========================================");
        }
    }
}
