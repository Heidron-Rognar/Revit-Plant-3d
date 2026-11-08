# Using the direction pad

## Before you draw

* Use an **orthographic 3D view**. Perspective 3D views cannot host model geometry; the pad says
  so in its status bar. (`View Properties → Camera → Projection = Orthographic`.)
* The pad is modeless — leave it open, orbit the model, pick points, keep drawing.

## The three-step loop

1. **Where am I?** Pick a start point, take it from the selection, or let the pad default to the
   nearest level at the model origin.
2. **Which way?** Click a compass sector, click an axis arm, or type a bearing.
3. **How far?** Type a distance and press **Enter**.

The end point becomes the new anchor when **Chain** is on, exactly like routing in Plant 3D.

---

## The compass

```
        N
    NW      NE
  W           E
    SW      SE
        S
```

* Click a sector to select that bearing. The arrow in the middle of the pad shows the actual
  direction, including its vertical component.
* **Up +Z / Down -Z** are pure vertical runs.
* **180** flips the current bearing.
* **Level** clears the vertical component.
* Sector meaning depends on the **compass frame**:

| Frame | "North" means |
| --- | --- |
| Project north | +Y in model coordinates (Revit's project north by default) |
| True north | Project north rotated by the base point angle (`Manage → Project Location`); use *Flip true north rotation* in Options if your template stores the angle the other way round |
| Screen | Up on screen for the active 3D view — handy when the camera is not aligned to an axis |

## Axis locks — X, Y, Z

The three coloured arms in the middle of the compass are the **world axes**:

* <span>X — red</span>, Y — green, Z — blue
* click an arm to lock the direction onto that axis; click it again to unlock
* or press `X`, `Y`, `Z`
* or use the *Axis lock* radios under the compass

A lock overrides the compass: the run becomes `±X`, `±Y` or `±Z`. The sign follows the compass
bearing (pick `East`, then lock `Z` — you get nothing horizontal left, so the stored sign is
used). Press **180** to flip a locked axis.

Axis locks are always world axes, even when the compass frame is set to true north or screen.

## Ortho

`Ortho` (F8) rounds the bearing to the nearest 90° and the elevation to the nearest 0° / ±90°.
It is off by default so that the diagonal sectors work immediately. Turning ortho on is the
Plant 3D / AutoCAD "constrain to axes" behaviour; the status bar tells you when a bearing was
snapped, for example `Ortho snapped 45 deg to 90 deg`.

Typing an exact bearing or elevation switches ortho off automatically, so the angle you typed is
the angle you get.

## Exact angles

* **Bearing** box — 0–360°, clockwise from frame north (survey style: 0 = N, 90 = E).
* **Elevation** box — 0 = level, +90 = straight up, −90 = straight down, 30 = a 30° rise
  along the chosen bearing.
* **Set** applies, **Clear** removes the override and goes back to the compass.

## Distances

Type the number and press Enter (or click **Draw segment**). Accepted formats:

| You type | Result (with millimetre display) |
| --- | --- |
| `2400` | 2400 (display units) |
| `2400 mm` | 2400 mm |
| `2.4 m` | 2400 mm |
| `96 in` | 96 inches |
| `8'` | 8 feet |
| `8'-6"` | 8 ft 6 in |
| `8 ft 6 in` | 8 ft 6 in |

The three preset buttons show their value in the current display unit. Change the unit under
**Options → Units**; the current distance is converted for you.

## Draw modes

**Model line** (default) — a 3D model curve. Always works, needs no families, and is easy to
delete. Good for layout grids, routing sketches, clearance lines and reference runs.

**Pipe** — a real pipe element (`PipeType` + `Level` + first available piping system type). The
project must have a pipe type and a piping system; if not, the pad tells you and you can switch
back to model lines. Level is the closest level below the start point.

## Work planes

Revit model curves must sit in a sketch plane, so the pad builds one per segment:

| Work plane | Behaviour |
| --- | --- |
| Auto (default) | Plane that contains the segment; level runs land on XY, a vertical run on XZ or YZ, sloped runs on a plane containing the direction |
| XY | Force horizontal. Vertical or sloped directions are refused with a message |
| XZ | Force the Y-normal plane |
| YZ | Force the X-normal plane |

*Set view work plane* additionally assigns the plane to the active 3D view, so the plane you drew
on becomes the view's work plane too (Revit shows the work plane grid). Off by default because it
changes your view's state.

Sketch planes are cached per axis and offset: a chain of level runs at the same elevation creates
one plane element, and **Clear all** deletes the helper planes it created.

## Picking

* **Pick start point** — snap to geometry in the 3D view; the anchor is set exactly where you clicked.
  The same thing is available as the ribbon's **Pick Start** button, which is the fallback if Revit
  refuses to pick while a dialog is open.
* **Start from selection** — anchor from the selected element's location point, curve end, or the
  centre of its bounding box.
* **Pick end point** — free 3D drawing: pick the end point and the segment is created from the
  anchor to that point, ignoring the compass. Great for connecting to existing geometry; the
  compass toggles come back for the next run.

## Undo and clear

* **Undo last** removes the most recent segment drawn from the pad (this session).
* **Clear all** removes every segment the pad created in this document and the helper sketch
  planes.

These only touch geometry the pad created — nothing else in the model is modified.

## Recipes

**A 3 m run north, then 1.5 m up, then 2 m east (metric)**

1. `Picked start point`
2. Click `N`, type `3000`, Enter
3. Click `Up +Z`, type `1500`, Enter
4. Click `E`, type `2000`, Enter

**Route on the X axis only**

1. Turn off Chain if you want a fresh start each time.
2. Axis lock `X`, distance `1200`, Enter. Repeat — every run is exactly along world X.

**A riser from a level to the roof**

1. Pick the start point on the level.
2. Press `U` (or click `Up +Z`).
3. Type the height and press Enter.

**30° sloped run to the north-west**

1. Set frame to *Project north*.
2. Bearing `300` → Set (ortho switches off).
3. Elevation `30` → Set.
4. Distance `5000`, Enter. The read-out shows ΔE/ΔN/ΔZ before you commit.

**Screen-relative drawing from a rotated camera**

1. Orbit until the run direction is "into the screen".
2. Frame → *Screen*. Now `N` means up on screen.

## Where the settings live

`%APPDATA%\PlantDraw\settings.json` — units, frame, toggles, last distance, window size and
position. Delete the file to reset.
