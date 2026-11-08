# Architecture

## Layering

```
RibbonBuilder / App               ribbon, document + idling events
        │
Commands/PadCommands              ribbon entry points (IExternalCommand)
        │
UI/DirectionPadHost ── UI/DirectionPadWindow (WPF, modeless)
        │                          │
        │                          ├── UI/CompassPad (custom drawing, no Revit)
        │                          └── Core/DirectionResolver (pure maths)
        │
Core/PlantDrawRuntime ──── Core/RevitTaskBridge (IExternalEventHandler)
        │
Core/PlantDrawRuntime ── Core/DrawSession (per document)
                              │
                              ├── Core/WorkPlaneFactory   (sketch planes)
                              ├── Core/SegmentFactory     (model line / pipe)
                              ├── Core/Tx                 (transaction + warnings)
                              └── Core/LengthUnits, Vec, ViewChecks
```

Nothing in `Core` depends on `UI`. The only WPF types in `Core` are none — it is pure Revit API
plus maths, which keeps it unit-testable.

## Threading: why the pad is safe

A modeless WPF window runs outside any Revit API context. Calling the API from there throws or
corrupts state. The only supported pattern is:

1. the window enqueues an `Action<UIApplication>` in `RevitTaskBridge`;
2. `PlantDrawRuntime.Post` calls `ExternalEvent.Raise()`;
3. Revit calls `RevitTaskBridge.Execute` on its own main thread, inside a valid API context;
4. the queued actions run in order, each wrapped in try/catch so one failure cannot wedge the queue.

Every read (active view, anchor, project north) and every write (picking, transactions) goes
through that path. The window itself only ever mutates its own cached copy of the state
(`_frame`, `_anchor`, read-outs), which is refreshed:

* when the window is shown or activated,
* after each command the pad runs,
* on Revit's `Idling` event, throttled to ~0.8 s, so orbiting or switching views updates the pad.

`DirectionResolver` needs no Revit call at all, so direction previews are instant: the window
caches the compass frame (project north angle / camera axes) and re-resolves locally.

## Direction resolution

`DirectionRequest` carries the toggles; `FrameBasis` is a horizontal basis (East/North unit
vectors in model space) plus a description. Resolution:

1. take the bearing from the compass sector or the bearing override;
2. take the elevation from Up/Down or the elevation override;
3. if ortho, snap bearing to 90° steps and elevation to 0 / ±90°;
4. build the horizontal vector in the frame, tilt it by the elevation;
5. if an axis lock is set, project onto that world axis and take the sign from the remaining
   component (or from the stored flip sign when the component vanished);
6. normalise and report bearing, elevation and a human label.

Because the frame is always horizontal (the view's up vector is flattened onto XY), a bearing can
never accidentally produce a tilted, unexpected direction — a camera looking straight down still
gives a usable compass.

## Work planes

`Document.Create.NewModelCurve(curve, sketchPlane)` requires the curve to lie inside the sketch
plane. `WorkPlaneFactory.ResolveNormal` picks the plane normal:

* horizontal direction (|dz| ≈ 0) → normal Z (the natural XY routing plane),
* direction along X → normal X, along Y → normal Y (XZ / YZ planes),
* anything else → a normal perpendicular to the direction.

Sketch planes are cached in `DrawSession.PlaneCache` keyed by `axis|offset`, so a chain of level
segments at one elevation reuses a single plane element. `SketchPlane.Create(document, Plane)`
with a raw plane creates an unbound plane — invisible in the model, and deleted again by
**Clear all**.

## Segment creation

`SegmentFactory` handles the two modes:

* **Model line**: resolve a sketch plane, optionally assign it to the view, then
  `NewModelCurve(Line.CreateBound(start, end), plane)`.
* **Pipe**: first `PipeType`, first `PipingSystemType`, nearest `Level` below the start point,
  then `Pipe.Create(...)`.

Both run inside `Tx.Run`, which installs a `WarningSink` (`IFailuresPreprocessor`) that deletes
warnings so a benign "elements are not connected" style message cannot abort the drawing loop.
Errors still abort and are surfaced in the pad's status bar.

## Session state

`DrawSession` holds, per document:

* the current anchor and whether it is set,
* the list of created segments (`ElementId`, length, kind) for undo,
* the sketch plane cache,
* the project north angle reader and frame builder,
* the total run length.

Sessions are created lazily in `PlantDrawRuntime` and dropped on `DocumentClosed`.

## Deliberate non-goals

* **No live rubber-band preview in model space.** That needs `DirectContext3D` (custom render
  loop, vertex buffers, GPU state). It is a nice second step, but it is also the most
  version-sensitive part of the API, so the pad previews direction in its own compass and shows
  the exact next point / ΔE ΔN ΔZ instead.
* **No fitting insertion.** The pad creates raw segments; connect them with Revit's own tools, or
  extend `SegmentFactory` with a placement step.
* **No in-canvas HUD in the Revit view.** Everything lives in the pad window so it works
  identically in every Revit version.

## Extending

| Want | Change |
| --- | --- |
| Duct / conduit / cable tray | add a case to `SegmentKind` and `SegmentFactory.Create` |
| 16-way bearings | change the ring arrays in `CompassPad` and the 45° step in `BearingToDirection` |
| Snap to analysed pipe ends | pre-filter the anchor in `DrawSession.EnsureAnchor` |
| Custom line style per run | set `ModelCurve.LineStyle` in `SegmentFactory` |
| Live 3D preview | implement `IDirectContext3DServer`, register it with the `DirectContext3DService`, feed it the pending segment from `DirectionPadWindow` |
