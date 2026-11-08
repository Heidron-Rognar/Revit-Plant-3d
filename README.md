# Plant Draw for Revit

A Plant 3D style **3D direction pad** for Autodesk Revit 2025 / 2026. Draw in the 3D view by
picking a direction from a compass ring — N, NE, E, SE, S, SW, W, NW, up and down — or by
locking the run onto the **X / Y / Z** axis, then type the distance and press Enter. Segments
are created as model lines or as real pipes.

```
Plant Draw  ->  3D Direction Pad
```

## Why

Plant 3D lets you route with a compass and ortho locks: pick a direction, type a distance,
keep going. Revit's 3D view has no such thing — you get a work plane and no directional
constraints. This add-in adds that direction pad on top of Revit.

## What you get

| Feature | Detail |
| --- | --- |
| Compass ring | 8 clickable sectors (N, NE, E, SE, S, SW, W, NW) plus Up / Down |
| Axis locks | X, Y, Z world-axis toggles (click the gizmo arms, press X / Y / Z, or use the radios) |
| Ortho | F8-style lock to 90° steps |
| Exact angles | Bearing box (0–360°, survey style) and elevation box (−90° … +90°) |
| Distance entry | Millimetres, metres, inches or feet-and-inches, plus 3 one-click presets |
| Draw modes | 3D **model lines** or real **pipes** (pipe type + level + system resolved automatically) |
| Compass frames | Project north, true north (uses the base point rotation), or screen-relative |
| Chains | Keeps drawing from the end of the last segment, Plant 3D style |
| Picking | Pick a start point / end point anywhere in the 3D view with Revit snapping |
| Live read-out | Next point, ΔE/ΔN/ΔZ, total run length, active view |
| Undo | Undo last / clear everything the pad created |
| Persistence | All toggles, units, distance and the window position are remembered |

## Requirements

* Windows (Revit is Windows only).
* Autodesk Revit **2025** or **2026** (both run on .NET 8).
* .NET 8 SDK — only needed to build.
* Revit's own `RevitAPI.dll` / `RevitAPIUI.dll` (installed with Revit) — the project references
  them from `C:\Program Files\Autodesk\Revit 2026\`.

## Install

```powershell
git clone https://github.com/Heidron-Rognar/Revit-Plant-3d.git
cd Revit-Plant-3d
powershell -ExecutionPolicy Bypass -File .\install\Install-PlantDraw.ps1
```

The script builds the add-in and installs it per-user (no admin rights):

* binaries → `%LOCALAPPDATA%\PlantDraw\bin\PlantDraw.dll`
* manifest → `%APPDATA%\Autodesk\Revit\Addins\2026\PlantDraw.addin`

Then start Revit. A **Plant Draw** tab appears in the ribbon with a **3D Direction Pad** button.

Uninstall with `.\install\Uninstall-PlantDraw.ps1`.

Full details, including manual install and building in Visual Studio: [docs/INSTALL.md](docs/INSTALL.md).

## Use it

1. Open your model and switch to an **orthographic 3D view** (*View tab → 3D View → Default 3D View*).
2. Ribbon → **Plant Draw → 3D Direction Pad**.
3. Click **Pick start point** and snap to the geometry where the run begins.
4. Click a compass sector (or press `N`, `E`, `S`, `W`, `U`, `D`). Use the **X / Y / Z** toggles to
   lock onto a world axis.
5. Type the distance and press **Enter**.

The pad stays open while you orbit, pick and draw. Each new segment starts where the last one
ended, so you can walk a run: `N 3000` → `Up 1500` → `E 2000`.

### Keyboard

| Key | Action |
| --- | --- |
| `N` `E` `S` `W` | Compass bearing |
| `U` / `D` | Straight up / straight down |
| `X` `Y` `Z` | Toggle the axis lock |
| `F8` | Toggle ortho |
| `Enter` | Draw the current segment |

Full walkthrough, unit formats and recipes: [docs/USAGE.md](docs/USAGE.md).

## How it works

* The pad is a **modeless WPF window**. It never calls the Revit API directly: every request is
  queued and executed through an `ExternalEvent`, which is the only safe way to touch Revit from
  a modeless window.
* Each segment needs a **sketch plane**, because Revit model curves must live in one. A plane
  containing the segment is derived from the direction (level runs land on XY, vertical runs on
  XZ/YZ), and planes are cached per axis and offset so a long horizontal chain creates one plane,
  not one per click.
* Directions are resolved from the toggles by `DirectionResolver`, pure maths over the active
  compass frame, so the pad can preview a direction without a round trip to Revit.
* A warning-swallowing `IFailuresPreprocessor` keeps routing sessions alive when Revit raises
  benign warnings mid-draw.

More detail: [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md).

## Repository layout

```
PlantDraw.sln
build.ps1                        build only
install/
  PlantDraw.addin                manifest template
  Install-PlantDraw.ps1          build + per-user install
  Uninstall-PlantDraw.ps1
src/PlantDraw/
  App.cs                         IExternalApplication: ribbon, events
  RibbonBuilder.cs               ribbon tab and buttons
  Globals.cs
  Commands/PadCommands.cs        pad, pick start, undo, clear
  Core/
    CompassTypes.cs              enums, direction request, frame basis
    DirectionResolver.cs         toggles -> unit vector
    DrawSession.cs               per-document anchor, history, frames
    SegmentFactory.cs            model line / pipe creation
    WorkPlaneFactory.cs          sketch planes for 3D segments
    LengthUnits.cs               mm / m / inch / ft-in parsing and formatting
    Tx.cs                        transactions + warning sink
    RevitTaskBridge.cs           ExternalEvent queue
    PlantDrawRuntime.cs          settings, sessions, status bus
    AppSettings.cs               %AppData% JSON preferences
    Vec.cs, ViewChecks.cs
  UI/
    DirectionPadWindow.xaml/.cs  the pad
    CompassPad.cs                custom-drawn compass + X/Y/Z gizmo
    IconFactory.cs               ribbon icons drawn at runtime
    DirectionPadHost.cs          window lifetime
docs/
  INSTALL.md  USAGE.md  ARCHITECTURE.md  TROUBLESHOOTING.md
preview/
  plant-draw-preview.html        clickable HTML mock-up of the pad (no Revit needed)
  tests/check-preview.mjs        Node harness: runs the mock-up's maths and interactions
```

## Try the UI without Revit

`preview/plant-draw-preview.html` is a standalone, clickable mock-up of the pad — same compass,
same toggles, same read-outs — so you can feel the interaction in a browser before building.
It needs no server: open the file directly.

Its direction maths is verified in Node (no dependencies):

```bash
node preview/tests/check-preview.mjs
```

## Status

Written against the Revit 2025 / 2026 API and shipped as source. The repository contains no
compiled binary: you build it on a machine that has Revit installed (the API assemblies are only
available there). See [docs/TROUBLESHOOTING.md](docs/TROUBLESHOOTING.md) if the build complains —
it lists the few version-sensitive API calls and their fixes.
