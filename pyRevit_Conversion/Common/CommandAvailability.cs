﻿namespace pyRevit_Conversion.Common
{
    internal class CommandAvailability : IExternalCommandAvailability
    {
        public bool IsCommandAvailable(UIApplication applicationData, CategorySet selectedCategories)
        {
            bool result = false;
            UIDocument activeDoc = applicationData.ActiveUIDocument;
            if (activeDoc != null && activeDoc.Document != null)
            {
                result = true;
            }

            return result;
        }
    }
}
