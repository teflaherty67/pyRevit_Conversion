using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using pyRevit_Conversion.Classes;
using System;
using System.Collections.Generic;
using System.Linq;

namespace pyRevit_Conversion
{
    // Base class for shared sheet modification functionality
    public abstract class BaseSheetNumberModifier : IExternalCommand
    {
        protected abstract string OperationType { get; }
        protected abstract string DialogTitle { get; }
        protected abstract string DefaultValue { get; }

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;

            try
            {
                // Get all sheets
                var allSheets = new FilteredElementCollector(doc)
                    .OfClass(typeof(ViewSheet))
                    .Cast<ViewSheet>()
                    .OrderBy(sheet => sheet.SheetNumber)
                    .ToList();

                if (!allSheets.Any())
                {
                    TaskDialog.Show("No Sheets", "No sheets found in the document.");
                    return Result.Cancelled;
                }

                // Get sheet sets
                var sheetSets = GetSheetSets(doc);
                var sheetSetOptions = new List<string> { "All Sheets" };
                sheetSetOptions.AddRange(sheetSets.Keys.OrderBy(x => x));

                // Configure form with increment value input
                var config = new SelectFromListConfig
                {
                    Title = DialogTitle,
                    ButtonText = OperationType,
                    ShowSheetSets = true,
                    SheetSetOptions = sheetSetOptions,
                    DefaultSheetSet = "All Sheets",
                    ViewSheetSets = sheetSets.Values.SelectMany(x => x).Cast<ViewSheetSet>().ToList(),
                    ShowIncrementInput = true,  // This would need to be added to your config
                    DefaultIncrementValue = DefaultValue
                };

                // Show selection form
                var result = frmSelectFromList.ShowWithResult(allSheets,
                    sheet => $"{sheet.SheetNumber} - {sheet.Name}", config);

                if (result?.DialogResult == true && result.SelectedItems.Any())
                {
                    // Get increment value (you'll need to add this to your form result)
                    var incrementValue = result.IncrementValue ?? DefaultValue;

                    if (int.TryParse(incrementValue, out int shift))
                    {
                        var selectedSheets = result.SelectedItems.Cast<ViewSheet>().ToList();
                        ModifySheetNumbers(doc, selectedSheets, shift);

                        TaskDialog.Show("Success",
                            $"{OperationType}ed {selectedSheets.Count} sheet numbers by {Math.Abs(shift)}.");
                    }
                    else
                    {
                        TaskDialog.Show("Error", "Invalid increment value entered.");
                        return Result.Failed;
                    }
                }

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = $"Error in {OperationType} Sheet Numbers: {ex.Message}";
                return Result.Failed;
            }
        }

        private Dictionary<string, List<ViewSheetSet>> GetSheetSets(Document doc)
        {
            var sheetSets = new Dictionary<string, List<ViewSheetSet>>();

            try
            {
                var viewSheetSets = new FilteredElementCollector(doc)
                    .OfClass(typeof(ViewSheetSet))
                    .Cast<ViewSheetSet>()
                    .ToList();

                foreach (var sheetSet in viewSheetSets)
                {
                    var sheets = sheetSet.Views.OfType<ViewSheet>().ToList();
                    if (sheets.Any())
                    {
                        sheetSets[sheetSet.Name] = new List<ViewSheetSet> { sheetSet };
                    }
                }
            }
            catch (Exception)
            {
                // Handle error silently, continue with empty sheet sets
            }

            return sheetSets;
        }

        private void ModifySheetNumbers(Document doc, List<ViewSheet> sheets, int shift)
        {
            using (Transaction trans = new Transaction(doc, $"{OperationType} Sheet Numbers"))
            {
                trans.Start();

                foreach (var sheet in sheets)
                {
                    try
                    {
                        string currentNumber = sheet.SheetNumber;
                        string newNumber = ModifySheetNumber(currentNumber, shift);

                        if (newNumber != currentNumber)
                        {
                            sheet.SheetNumber = newNumber;
                        }
                    }
                    catch (Exception ex)
                    {
                        TaskDialog.Show("Warning",
                            $"Could not modify sheet {sheet.SheetNumber}: {ex.Message}");
                    }
                }

                trans.Commit();
            }
        }

        private string ModifySheetNumber(string input, int value)
        {
            if (string.IsNullOrEmpty(input)) return input;

            // Apply operation type
            int shift = (OperationType == "Decrement") ? -value : value;

            string result = input;

            // Look for number at the end of the string
            int i = input.Length - 1;
            while (i >= 0 && char.IsDigit(input[i]))
                i--;

            if (i < input.Length - 1)
            {
                // Found numeric suffix
                string prefix = input.Substring(0, i + 1);
                string numericPart = input.Substring(i + 1);

                if (int.TryParse(numericPart, out int number))
                {
                    int newNumber = number + shift;
                    if (newNumber >= 0) // Don't allow negative sheet numbers
                    {
                        // Preserve leading zeros
                        string format = new string('0', numericPart.Length);
                        result = prefix + newNumber.ToString(format);
                    }
                }
            }
            else
            {
                // No numeric suffix found, append the value if incrementing
                if (OperationType == "Increment" && value > 0)
                    result = input + value.ToString();
            }

            return result;
        }
    }

    // Increment Sheet Numbers Command
    [Transaction(TransactionMode.Manual)]
    public class cmdIncrementSheetNumbers : BaseSheetNumberModifier
    {
        protected override string OperationType => "Increment";
        protected override string DialogTitle => "Increment Sheet Numbers";
        protected override string DefaultValue => "1";

        internal static PushButtonData GetButtonData()
        {
            string buttonInternalName = "btnIncrementSheets";
            string buttonTitle = "Increment\nSheets";

            var myButtonData = new clsButtonData(
                buttonInternalName,
                buttonTitle,
                MethodBase.GetCurrentMethod().DeclaringType?.FullName,
                Properties.Resources.Blue_32,
                Properties.Resources.Blue_16,
                "Increment selected sheet numbers");

            return myButtonData.Data;
        }
    }

    // Decrement Sheet Numbers Command  
    [Transaction(TransactionMode.Manual)]
    public class cmdDecrementSheetNumbers : BaseSheetNumberModifier
    {
        protected override string OperationType => "Decrement";
        protected override string DialogTitle => "Decrement Sheet Numbers";
        protected override string DefaultValue => "1";

        internal static PushButtonData GetButtonData()
        {
            string buttonInternalName = "btnDecrementSheets";
            string buttonTitle = "Decrement\nSheets";

            var myButtonData = new clsButtonData(
                buttonInternalName,
                buttonTitle,
                MethodBase.GetCurrentMethod().DeclaringType?.FullName,
                Properties.Resources.Red_32,
                Properties.Resources.Red_16,
                "Decrement selected sheet numbers");

            return myButtonData.Data;
        }
    }
}