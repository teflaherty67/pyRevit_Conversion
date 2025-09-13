using Autodesk.Revit.UI;
using pyRevit_Conversion.Classes;
using pyRevit_Conversion.Common;

namespace pyRevit_Conversion
{
    [Transaction(TransactionMode.Manual)]
    public class cmdSelectSheets : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            // Revit application and document variables
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document curDoc = uidoc.Document;

            // get all the sheets in the document
            List<ViewSheet> colSheets = new FilteredElementCollector(curDoc)
                .OfClass(typeof(ViewSheet))
                .Cast<ViewSheet>()
                .OrderBy(s => s.SheetNumber)
                .ToList();

            if (!colSheets.Any())
            {
                // if no sheets are found, alert the user & exit
                TaskDialog tdNoSheets = new TaskDialog("Empty");
                tdNoSheets.MainIcon = Icon.TaskDialogIconWarning;
                tdNoSheets.Title = "Select Sheets";
                tdNoSheets.TitleAutoPrefix = false;
                tdNoSheets.MainContent = "No sheets were found in the current document";
                tdNoSheets.CommonButtons = TaskDialogCommonButtons.Close;

                TaskDialogResult tdNoSheetsRes = tdNoSheets.Show();
               
                return Result.Failed;
            }

            // get the print sets from the document
            List<ViewSheetSet> colPrintSets = new FilteredElementCollector(curDoc)
                .OfClass(typeof(ViewSheetSet))
                .Cast<ViewSheetSet>()
                .OrderBy(vss => vss.Name)
                .ToList();

            // create sheet set list with "All Sheets" as the first item
            List<string> listSheetSetNames = new List<string> { "All Sheets" };

            // add the print sets to the list
            listSheetSetNames.AddRange(colPrintSets.Select(vss => vss.Name).OrderBy(name => name));

            // configure the form
            var frmConfig = new SelectFromListConfig
            {
                Title = "Select Sheets",
                ButtonText = "Select",
                ShowSheetSets = true,
                SheetSetOptions = listSheetSetNames,
                DefaultSheetSet = "All Sheets",
                ViewSheetSets = colPrintSets,
            };

            // launch the form
            var frmResult = frmSelectFromList.ShowWithResult
                (
                    items: colSheets,
                    displayNameSelector: sheet => $"{sheet.SheetNumber} - {sheet.Name}",
                    config: frmConfig
                );

            // set selection if user made a choice
            if (frmResult != null && frmResult.DialogResult && frmResult.SelectedItems.Any())
            {
                var selectedSheets = frmResult.SelectedItems.Cast<ViewSheet>().ToList();

                // get ElementIds of selected ViewSheets
                var elementIds = selectedSheets.Select(sheet => sheet.Id).ToList();

                // set the current selection
                uidoc.Selection.SetElementIds(elementIds);                
            }

            return Result.Succeeded;
        }

        internal static PushButtonData GetButtonData()
        {
            // use this method to define the properties for this command in the Revit ribbon
            string buttonInternalName = "btnCommand1";
            string buttonTitle = "Button 1";

            clsButtonData myButtonData = new clsButtonData(
                buttonInternalName,
                buttonTitle,
                MethodBase.GetCurrentMethod().DeclaringType?.FullName,
                Properties.Resources.Blue_32,
                Properties.Resources.Blue_16,
                "This is a tooltip for Button 1");

            return myButtonData.Data;
        }
    }
}
