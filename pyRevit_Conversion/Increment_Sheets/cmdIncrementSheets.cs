using pyRevit_Conversion.Classes;
using pyRevit_Conversion.Common;

namespace pyRevit_Conversion
{
    [Transaction(TransactionMode.Manual)]
    public class cmdIncrementSheets : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            // Revit application and document variables
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document curDoc = uidoc.Document;

            // get all the sheets in the document
            List<ViewSheet> allSheets = Utils.GetAndSortAllSheets(curDoc);
            
            // if no sheets are found
            if (!allSheets.Any())
            {
                // notify the user
                Utils.TaskDialogWarning("Lifestyle Design", "Increment Sheet Numbers", "No sheets were found in the current document.");

                // exit the command
                return Result.Failed;
            }

            // get the sheet sets from the document
            List<ViewSheetSet> allPrintSets = Utils.GetAndSortAllPrintSets(curDoc);            

            // create sheet set list with "All Sheets" as the first item
            List<string> listSheetSetNames = new List<string> { "All Sheets" };

            // add the print sets to the list
            listSheetSetNames.AddRange(allPrintSets.Select(vss => vss.Name).OrderBy(name => name));

            // configure the form
            var frmConfig = new SelectFromListConfig
            {
                Title = "Increment Sheet Numbers",
                ButtonText = "Increment",
                ShowSheetSets = true,
                SheetSetOptions = listSheetSetNames,
                DefaultSheetSet = "All Sheets",
                ViewSheetSets = allPrintSets,
                ShowIncrementInput = true,
                DefaultIncrementValue = "1",
                IncrementLabel = "Increment by:",
                HelpUrl = "https://lifestyle-usa-design.atlassian.net/wiki/spaces/MFS/pages/534282241/Increment+Sheets"
            };

            // launch the form
            var frmResult = frmSelectFromList.ShowWithResult
                (
                    items: allSheets,
                    displayNameSelector: sheet => $"{sheet.SheetNumber} - {sheet.Name}",
                    config: frmConfig
                );

            // process the results if user made a choice
            if (frmResult != null && frmResult.DialogResult && frmResult.SelectedItems.Any())
            {
                var selectedSheets = frmResult.SelectedItems.Cast<ViewSheet>().ToList();

                // get increment value
                if (int.TryParse(frmResult.IncrementValue, out int incrementValue) && incrementValue > 0)
                {
                    // Sort sheets in REVERSE numerical order for incrementing
                    // This prevents conflicts (A3c→A4c, then A2c→A3c, then A1c→A2c)
                    var sortedSheets = selectedSheets
                        .OrderByDescending(sheet => ExtractNumericPart(sheet.SheetNumber))
                        .ThenByDescending(sheet => sheet.SheetNumber)
                        .ToList();

                    // increment the sheet numbers
                    using (Transaction trans = new Transaction(curDoc, "Increment Sheet Numbers"))
                    {
                        trans.Start();

                        foreach (var sheet in sortedSheets)
                        {
                            try
                            {
                                string currentNumber = sheet.SheetNumber;
                                string newNumber = IncrementSheetNumber(currentNumber, incrementValue);

                                if (newNumber != currentNumber)
                                {
                                    sheet.SheetNumber = newNumber;
                                }
                            }
                            catch (Exception ex)
                            {
                                TaskDialog.Show("Warning",
                                    $"Could not increment sheet {sheet.SheetNumber}: {ex.Message}");
                            }
                        }

                        trans.Commit();
                    }

                    // Show success message
                    TaskDialog.Show("Success",
                        $"Incremented {selectedSheets.Count} sheet numbers by {incrementValue}.");
                }
                else
                {
                    TaskDialog.Show("Error", "Invalid increment value entered.");
                    return Result.Failed;
                }
            }

            return Result.Succeeded;
        }

        private string IncrementSheetNumber(string input, int incrementValue)
        {
            if (string.IsNullOrEmpty(input)) return input;

            string result = input;

            // Check if last character is a letter (suffix like 'A3s')
            string letterSuffix = "";
            string workingString = input;

            if (input.Length > 0 && char.IsLetter(input[input.Length - 1]))
            {
                letterSuffix = input.Substring(input.Length - 1);
                workingString = input.Substring(0, input.Length - 1);
            }

            // Look for number at the end of the working string
            int i = workingString.Length - 1;
            while (i >= 0 && char.IsDigit(workingString[i]))
                i--;

            if (i < workingString.Length - 1)
            {
                // Found numeric part
                string prefix = workingString.Substring(0, i + 1);
                string numericPart = workingString.Substring(i + 1);

                if (int.TryParse(numericPart, out int number))
                {
                    int newNumber = number + incrementValue;
                    if (newNumber >= 0) // Don't allow negative sheet numbers
                    {
                        // Preserve leading zeros if they existed
                        string format = numericPart.Length > 1 && numericPart.StartsWith("0")
                            ? new string('0', numericPart.Length)
                            : "";

                        string formattedNumber = format.Length > 0
                            ? newNumber.ToString(format)
                            : newNumber.ToString();

                        result = prefix + formattedNumber + letterSuffix;
                    }
                }
            }
            else
            {
                // No numeric part found, append the increment value before any letter suffix
                if (incrementValue > 0)
                    result = workingString + incrementValue.ToString() + letterSuffix;
            }

            return result;
        }

        private int ExtractNumericPart(string sheetNumber)
        {
            if (string.IsNullOrEmpty(sheetNumber)) return 0;

            // Remove letter suffix if it exists
            string workingString = sheetNumber;
            if (sheetNumber.Length > 0 && char.IsLetter(sheetNumber[sheetNumber.Length - 1]))
            {
                workingString = sheetNumber.Substring(0, sheetNumber.Length - 1);
            }

            // Find the numeric part at the end
            int i = workingString.Length - 1;
            while (i >= 0 && char.IsDigit(workingString[i]))
                i--;

            if (i < workingString.Length - 1)
            {
                string numericPart = workingString.Substring(i + 1);
                if (int.TryParse(numericPart, out int number))
                    return number;
            }

            return 0;
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
