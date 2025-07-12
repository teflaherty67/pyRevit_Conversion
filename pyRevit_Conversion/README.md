﻿This template supports Revit versions 2020 through 2026.
In order to build your code to Revit 2025, you must install the .Net 8 SDK. 
To do so, go to the following link and download the .Net 8 SDK: 
	https://dotnet.microsoft.com/download/dotnet/8.0

The template copies built code to a sub-folder in the Revit Addins folder.
The sub-folder is named after the add-in assembly name and contains the add-in manifest file and the add-in assembly.
The .addin file is automatically updated with the correct path to the add-in assembly.

Template Change log
3.0 - Added support for Revit 2025
3.1 
3.2 
3.3 - Added R20 build config, fixed error in Command2.cs, added ButtonDataClass
3.4 - Added CopyLocalLockFileAssemblies property to .csproj file
3.5 - Added support for Revit 2026