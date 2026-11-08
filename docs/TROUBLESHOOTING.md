# Troubleshooting

## The build fails

### `RevitAPI.dll was not found in 'C:\Program Files\Autodesk\Revit 2026\'`

The project references the API assemblies straight out of the Revit install folder — they are not
on NuGet. Fix one of these:

```powershell
.\build.ps1 -RevitVersion 2025                                   # different Revit year
.\build.ps1 -RevitApiDir "D:\Apps\Autodesk\Revit 2026\"           # non-default folder
```

Or set it once in `src\PlantDraw\PlantDraw.user.props`:

```xml
<Project>
  <PropertyGroup>
    <RevitApiDir>D:\Apps\Autodesk\Revit 2026\</RevitApiDir>
  </PropertyGroup>
</Project>
```

### `The current .NET SDK does not support targeting .NET 8.0`

Install the .NET 8 SDK. Revit 2025/2026 host .NET 8, so a .NET 8 build is required.

### Building for Revit 2024 or older

Revit 2024 and earlier host .NET Framework 4.8. Change:

```xml
<TargetFramework>net48</TargetFramework>
```

and drop `UseWPF` → keep it, WPF exists in .NET Framework too; but the `System.Text.Json`
dependency is not in-box on net48 — add the `System.Text.Json` NuGet package, or replace
`AppSettings` with a tiny manual serializer. The rest of the code is version-agnostic.

### Compiler complains about one of these (version-specific API)

The code is written for the 2025/2026 API. If a specific build disagrees, these are the only
version-sensitive spots; each is a one-line fix:

| Call | Where | If it fails |
| --- | --- | --- |
| `Pipe.Create(doc, pipeTypeId, levelId, systemTypeId, start, end)` | `SegmentFactory.CreatePipe` | check the overload order in the RevitAPI help for your year; some builds take `(doc, systemTypeId, pipeTypeId, levelId, …)` |
| `view.SketchPlane = sketchPlane` | `SegmentFactory.CreateModelLine` | if the setter is unavailable, delete that block — the curve's own sketch plane argument is what matters |
| `ObjectSnapTypes.WorkPlaneGrid` | `PadCommands`, `DirectionPadWindow` | drop the flag; the other snap flags are enough |
| `ProjectPosition.Angle` | `DrawSession.GetProjectAngleRadians` | older builds expose the rotation through `ProjectLocation` only; keep it at 0 and use the project-north frame |
| `application.Idling` on `UIControlledApplication` | `App.AttachEvents` | remove the idle refresh; the pad also refreshes on activate and after every command |

## The add-in does not load

1. **Wrong folder.** The manifest must be in
   `%APPDATA%\Autodesk\Revit\Addins\2026\PlantDraw.addin` for Revit 2026 (the year folder must
   match the Revit version).
2. **Wrong DLL path.** Open the `.addin` file: `<Assembly>` must point at an existing
   `PlantDraw.dll`.
3. **Blocked file.** Right-click the DLL → Properties → *Unblock*, or
   `Get-ChildItem $env:LOCALAPPDATA\PlantDraw -Recurse | Unblock-File`.
4. **Missing dependency.** Run `dotnet build` output through your eyes: the add-in needs only the
   .NET 8 runtime plus `RevitAPI.dll`/`RevitAPIUI.dll`, both provided by Revit.
5. **Journal diagnostics.** Start Revit with `journal` logging and check
   `%LOCALAPPDATA%\Autodesk\Revit\Autodesk Revit 2026\Journals\` for the load error.

## The ribbon tab is missing but the add-in loaded

`OnStartup` catches ribbon errors and shows them in the pad's status bar. The usual cause is a
leftover tab from a previous session (Revit keeps ribbon tabs for the lifetime of the process):
restart Revit.

## The pad opens but says the view is not usable

* `Perspective 3D views cannot host model geometry` — switch the view to orthographic:
  *View Properties → Camera → Projection = Orthographic*, or use *Default 3D View*.
* `A sheet cannot host model lines` — open a 3D view, not a sheet.
* `not a 3D view` in the read-out is only a warning: the tool also works in plan views, but the
  point of it is 3D.

## "This direction does not lie in the XY work plane"

You forced a work plane (`XY`) and then asked for a vertical run. Set *Work plane* back to
**Auto** — Auto always picks a plane that contains the direction.

## Nothing appears after drawing

* The status bar reports every failure, read it first.
* Model lines are visible in 3D views unless the category *Lines* is hidden in that view
  (*Visibility/Graphics → Model Categories → Lines*).
* In a section box, a segment outside the box is clipped: orbit or clear the section box.
* Pipes need a pipe type, a piping system type and at least one level; the pad says which is
  missing.

## The anchor is in a strange place

With nothing picked, the anchor defaults to the nearest level at the model origin. Use **Pick
start point** or **Start from selection** to place it exactly.

## The compass points the wrong way

* Frame *Project north* uses +Y in model coordinates.
* If your model is rotated, switch to *True north*; if it is still 90°/180° off, tick
  *Flip true north rotation* in Options — templates differ in which direction they store the base
  point angle.
* Frame *Screen* follows the camera, so `N` changes when you orbit. That is intended.

## Undo does nothing

**Undo last** only knows about segments this add-in created in the current session. If you closed
the document or restarted Revit, use Revit's own Undo (Ctrl+Z) instead.

## Revit warns about a "not connected" pipe

Expected: the pad creates raw segments, it does not insert fittings. Suppress the warning with
Revit's own "Don't show again", or connect the runs afterwards.

## Resetting everything

Delete `%APPDATA%\PlantDraw\settings.json`. The pad returns to its factory defaults
(mm, project north, ortho off, 1000 distance).
