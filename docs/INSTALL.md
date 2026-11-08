# Installing Plant Draw

## 1. Prerequisites

| Requirement | Notes |
| --- | --- |
| Autodesk Revit 2025 or 2026 | Revit is only available for Windows. |
| .NET 8 SDK | https://dotnet.microsoft.com/download/dotnet/8.0 — needed to build, not to run. |
| Revit API assemblies | Installed with Revit, at `C:\Program Files\Autodesk\Revit 2026\`. |

Check the SDK:

```powershell
dotnet --list-sdks
```

You need a line starting with `8.`.

## 2. One-command install

From the repository root:

```powershell
powershell -ExecutionPolicy Bypass -File .\install\Install-PlantDraw.ps1
```

What it does:

1. runs `dotnet build src\PlantDraw\PlantDraw.csproj -c Release -p:Platform=x64 -p:RevitVersion=2026`;
2. copies `PlantDraw.dll` (and the .pdb) to `%LOCALAPPDATA%\PlantDraw\bin\`;
3. writes `%APPDATA%\Autodesk\Revit\Addins\2026\PlantDraw.addin` pointing at that DLL.

No administrator rights, no changes to the Revit installation folder.

Useful switches:

```powershell
# Revit 2025
.\install\Install-PlantDraw.ps1 -RevitVersion 2025

# Revit installed somewhere unusual
.\install\Install-PlantDraw.ps1 -RevitApiDir "D:\Apps\Autodesk\Revit 2026\"

# rebuild already done
.\install\Install-PlantDraw.ps1 -SkipBuild

# debug build
.\install\Install-PlantDraw.ps1 -Configuration Debug
```

Then start Revit and look for the **Plant Draw** tab.

## 3. Build only

```powershell
.\build.ps1                          # Release, Revit 2026
.\build.ps1 -Configuration Debug -RevitVersion 2025
```

Output: `src\PlantDraw\bin\Release\PlantDraw.dll`.

In Visual Studio: open `PlantDraw.sln`, pick the `Release | x64` configuration, build. If Revit is
not in the default location, set the API folder once in a user property file
`src\PlantDraw\PlantDraw.user.props`:

```xml
<Project>
  <PropertyGroup>
    <RevitApiDir>D:\Apps\Autodesk\Revit 2026\</RevitApiDir>
  </PropertyGroup>
</Project>
```

## 4. Manual install

1. Build `PlantDraw.dll`.
2. Copy it anywhere permanent, for example `C:\Tools\PlantDraw\PlantDraw.dll`.
3. Copy `install\PlantDraw.addin` to `%APPDATA%\Autodesk\Revit\Addins\2026\`.
4. Open that file and replace `__ASSEMBLY__` with the full DLL path:

```xml
<AddIn Type="Application">
  <Name>Plant Draw</Name>
  <Assembly>C:\Tools\PlantDraw\PlantDraw.dll</Assembly>
  <AddInId>A6C2F0B4-9E3D-4C77-8A31-5B0E7C1D4F22</AddInId>
  <FullClassName>PlantDraw.App</FullClassName>
  <VendorId>PLDRW</VendorId>
  <VendorDescription>Plant Draw - Plant 3D style 3D direction pad for Revit</VendorDescription>
</AddIn>
```

`FullClassName` must stay `PlantDraw.App` — that is the `IExternalApplication` entry point.

> All-users install: put the manifest in
> `C:\ProgramData\Autodesk\Revit\Addins\2026\` instead. That needs admin rights.

## 5. Uninstall

```powershell
.\install\Uninstall-PlantDraw.ps1
```

Removes the manifest from the 2024–2027 add-ins folders and deletes
`%LOCALAPPDATA%\PlantDraw`. Add `-KeepFiles` to leave the binaries in place.

Manual uninstall: delete `%APPDATA%\Autodesk\Revit\Addins\2026\PlantDraw.addin` and restart Revit.

## 6. Updating

Close Revit (the DLL is locked while Revit runs), then run the installer again. Your pad
preferences in `%APPDATA%\PlantDraw\settings.json` are kept.

## 7. Unblocking a downloaded copy

If you moved the repository as a ZIP, Windows may mark the DLL as coming from another computer.
Revit then silently skips the add-in. Clear the mark:

```powershell
Get-ChildItem -Path $env:LOCALAPPDATA\PlantDraw -Recurse | Unblock-File
```
