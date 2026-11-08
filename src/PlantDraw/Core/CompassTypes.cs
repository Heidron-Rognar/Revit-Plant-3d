using System;
using Autodesk.Revit.DB;

namespace PlantDraw.Core
{
    /// <summary>The eight compass bearings plus the two vertical directions.</summary>
    public enum CompassDirection
    {
        None = 0,
        North = 1,
        NorthEast = 2,
        East = 3,
        SouthEast = 4,
        South = 5,
        SouthWest = 6,
        West = 7,
        NorthWest = 8,
        Up = 9,
        Down = 10
    }

    /// <summary>What "north" means when the compass is resolved.</summary>
    public enum CompassFrame
    {
        /// <summary>Revit internal project north (+Y in model coordinates).</summary>
        ProjectNorth = 0,

        /// <summary>Survey / true north, taken from the project base point rotation.</summary>
        TrueNorth = 1,

        /// <summary>Screen relative: "north" is up on screen for the active 3D view.</summary>
        View = 2
    }

    /// <summary>Hard lock of the direction onto one of the three world axes.</summary>
    public enum AxisLock
    {
        None = 0,
        X = 1,
        Y = 2,
        Z = 3
    }

    /// <summary>What gets created when a segment is committed.</summary>
    public enum SegmentKind
    {
        ModelLine = 0,
        Pipe = 1
    }

    /// <summary>Work plane strategy used to host the created geometry.</summary>
    public enum PlaneMode
    {
        /// <summary>Pick a plane that contains the segment (horizontal segments land on XY).</summary>
        Auto = 0,
        XY = 1,
        XZ = 2,
        YZ = 3
    }

    public enum LengthDisplay
    {
        Millimeters = 0,
        Meters = 1,
        Inches = 2,
        FeetInches = 3
    }

    /// <summary>Everything the direction resolver needs. Plain data, no Revit calls.</summary>
    public sealed class DirectionRequest
    {
        public CompassDirection Direction { get; set; } = CompassDirection.None;

        public CompassFrame Frame { get; set; } = CompassFrame.ProjectNorth;

        public AxisLock AxisLock { get; set; } = AxisLock.None;

        /// <summary>Snap bearings to the nearest 90 degrees and elevation to the nearest axis.</summary>
        public bool Ortho { get; set; } = true;

        /// <summary>Optional survey-style bearing in degrees, clockwise from the frame north.</summary>
        public double? BearingDegrees { get; set; }

        /// <summary>Optional vertical angle in degrees (0 = level, +90 = straight up).</summary>
        public double? ElevationDegrees { get; set; }

        /// <summary>Sign used when an axis lock hits a zero component (the "flip" button).</summary>
        public int AxisSign { get; set; } = 1;
    }

    /// <summary>Result of resolving a <see cref="DirectionRequest"/> against a frame.</summary>
    public sealed class ResolvedDirection
    {
        public static readonly ResolvedDirection Invalid = new ResolvedDirection
        {
            IsValid = false,
            Vector = XYZ.BasisX,
            Label = "no direction",
            Note = "Pick a compass direction, a bearing, or an axis lock."
        };

        public bool IsValid { get; set; }

        /// <summary>Unit vector in model coordinates (Revit internal feet).</summary>
        public XYZ Vector { get; set; }

        /// <summary>Bearing measured in the active frame, clockwise from frame north.</summary>
        public double BearingDegrees { get; set; }

        /// <summary>Vertical angle in degrees.</summary>
        public double ElevationDegrees { get; set; }

        public string Label { get; set; }

        public string Note { get; set; }

        public bool IsVertical
        {
            get { return Math.Abs(ElevationDegrees) > 89.999; }
        }

        public override string ToString()
        {
            return Label;
        }
    }

    /// <summary>
    /// Horizontal basis for the compass: East and North as unit vectors in model space.
    /// Both are horizontal, so a bearing in this frame always produces a sensible direction.
    /// </summary>
    public struct FrameBasis
    {
        public XYZ East;
        public XYZ North;
        public CompassFrame Source;
        public string Description;

        public static FrameBasis World
        {
            get
            {
                return new FrameBasis
                {
                    East = XYZ.BasisX,
                    North = XYZ.BasisY,
                    Source = CompassFrame.ProjectNorth,
                    Description = "project north"
                };
            }
        }

        public bool IsValid
        {
            get { return !Vec.IsZero(East) && !Vec.IsZero(North); }
        }

        /// <summary>
        /// Frame where north is the project north rotated by the base point angle.
        /// <paramref name="angleRadians"/> follows Revit's <c>ProjectPosition.Angle</c> and is
        /// applied as a counter-clockwise rotation of project north.
        /// </summary>
        public static FrameBasis FromTrueNorth(double angleRadians, bool flip)
        {
            double a = flip ? -angleRadians : angleRadians;
            double cos = Math.Cos(a);
            double sin = Math.Sin(a);

            // Counter-clockwise rotation of +Y.
            XYZ north = new XYZ(-sin, cos, 0.0);
            XYZ east = new XYZ(cos, sin, 0.0);

            return new FrameBasis
            {
                East = Vec.Unit(east),
                North = Vec.Unit(north),
                Source = CompassFrame.TrueNorth,
                Description = string.Format("true north ({0:0.0} deg from project north)", angleRadians * 180.0 / Math.PI)
            };
        }

        /// <summary>
        /// Screen relative frame built from a 3D view orientation: "north" is up on screen and
        /// "east" is right on screen, both flattened onto the horizontal plane.
        /// </summary>
        public static FrameBasis FromView(XYZ viewRight, XYZ viewUp, XYZ viewForward)
        {
            XYZ north = Vec.HorizontalUnit(viewUp);
            if (Vec.IsZero(north, 1e-6))
            {
                // Camera is looking straight down or straight up: fall back to "into the screen".
                north = Vec.HorizontalUnit(Vec.Neg(viewForward));
            }

            XYZ east = Vec.HorizontalUnit(viewRight);
            if (Vec.IsZero(east, 1e-6))
            {
                // Right vector is vertical (top view): derive east from north.
                east = Vec.Cross(XYZ.BasisZ, north);
            }

            if (Vec.IsZero(north, 1e-6) || Vec.IsZero(east, 1e-6))
            {
                return World;
            }

            // Re-orthogonalise so the bearing maths stays exact for tilted views.
            XYZ eastOrtho = Vec.Unit(Vec.Sub(east, Vec.Mul(north, Vec.Dot(east, north))));
            if (Vec.IsZero(eastOrtho, 1e-6))
            {
                eastOrtho = Vec.Cross(XYZ.BasisZ, north);
            }

            return new FrameBasis
            {
                East = Vec.Unit(eastOrtho),
                North = Vec.Unit(north),
                Source = CompassFrame.View,
                Description = "view (screen up = north)"
            };
        }

        public XYZ ToWorld(double eastComponent, double northComponent, double upComponent)
        {
            XYZ horizontal = Vec.Add(Vec.Mul(East, eastComponent), Vec.Mul(North, northComponent));
            return Vec.Add(horizontal, new XYZ(0.0, 0.0, upComponent));
        }

        /// <summary>Projects a model-space vector into (east, north, up) components.</summary>
        public void FromWorld(XYZ vector, out double east, out double north, out double up)
        {
            east = Vec.Dot(vector, East);
            north = Vec.Dot(vector, North);
            up = vector.Z;
        }
    }
}
