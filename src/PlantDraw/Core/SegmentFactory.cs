using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;

namespace PlantDraw.Core
{
    public sealed class SegmentResult
    {
        public ElementId ElementId { get; set; } = ElementId.InvalidElementId;
        public ElementId SketchPlaneId { get; set; } = ElementId.InvalidElementId;
        public double Length { get; set; }
        public string Kind { get; set; }
    }

    /// <summary>Creates the geometry for one committed segment.</summary>
    public static class SegmentFactory
    {
        /// <summary>Revit refuses to build geometry shorter than roughly this.</summary>
        public const double MinimumLength = 1e-3;

        public static SegmentResult Create(
            Document document,
            XYZ start,
            XYZ end,
            SegmentKind kind,
            PlaneMode planeMode,
            IDictionary<string, ElementId> planeCache,
            bool pinViewWorkPlane,
            View view)
        {
            double length = Vec.Distance(start, end);
            if (length < MinimumLength)
            {
                throw new InvalidOperationException("The segment is too short to build. Increase the distance.");
            }

            switch (kind)
            {
                case SegmentKind.Pipe:
                    return CreatePipe(document, start, end, length);
                default:
                    return CreateModelLine(document, start, end, length, planeMode, planeCache, pinViewWorkPlane, view);
            }
        }

        private static SegmentResult CreateModelLine(
            Document document,
            XYZ start,
            XYZ end,
            double length,
            PlaneMode planeMode,
            IDictionary<string, ElementId> planeCache,
            bool pinViewWorkPlane,
            View view)
        {
            XYZ direction = Vec.Unit(Vec.Sub(end, start));

            bool created;
            SketchPlane sketchPlane = WorkPlaneFactory.Resolve(document, start, direction, planeMode, planeCache, out created);

            if (pinViewWorkPlane && view != null && view.IsValidObject)
            {
                try
                {
                    view.SketchPlane = sketchPlane;
                }
                catch
                {
                    // Not every view accepts a work plane; the curve itself is what matters.
                }
            }

            Line line = Line.CreateBound(start, end);
            ModelCurve curve = document.Create.NewModelCurve(line, sketchPlane);

            return new SegmentResult
            {
                ElementId = curve.Id,
                SketchPlaneId = created ? sketchPlane.Id : ElementId.InvalidElementId,
                Length = length,
                Kind = "model line"
            };
        }

        private static SegmentResult CreatePipe(Document document, XYZ start, XYZ end, double length)
        {
            PipeType pipeType = new FilteredElementCollector(document)
                .OfClass(typeof(PipeType))
                .Cast<PipeType>()
                .FirstOrDefault();

            if (pipeType == null)
            {
                throw new InvalidOperationException(
                    "This project has no pipe type. Load a piping system (or switch the draw mode to Model line).");
            }

            PipingSystemType systemType = new FilteredElementCollector(document)
                .OfClass(typeof(PipingSystemType))
                .Cast<PipingSystemType>()
                .FirstOrDefault();

            if (systemType == null)
            {
                throw new InvalidOperationException(
                    "This project has no piping system type. Add one under Manage > MEP Settings (or switch to Model line).");
            }

            Level level = FindLevel(document, start);
            if (level == null)
            {
                throw new InvalidOperationException("This project has no levels, so a pipe cannot be placed.");
            }

            Pipe pipe = Pipe.Create(document, pipeType.Id, level.Id, systemType.Id, start, end);

            return new SegmentResult
            {
                ElementId = pipe.Id,
                SketchPlaneId = ElementId.InvalidElementId,
                Length = length,
                Kind = "pipe"
            };
        }

        public static Level FindLevel(Document document, XYZ point)
        {
            List<Level> levels = new FilteredElementCollector(document)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .OrderBy(level => level.ProjectElevation)
                .ToList();

            if (levels.Count == 0)
            {
                return null;
            }

            Level best = levels[0];
            foreach (Level level in levels)
            {
                if (level.ProjectElevation <= point.Z + 1e-6)
                {
                    best = level;
                }
            }

            return best;
        }

        public static string DescribePoint(XYZ point, LengthDisplay display)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "E {0}  N {1}  Z {2}",
                LengthUnits.FormatNumber(point.X, display),
                LengthUnits.FormatNumber(point.Y, display),
                LengthUnits.FormatNumber(point.Z, display));
        }
    }
}
