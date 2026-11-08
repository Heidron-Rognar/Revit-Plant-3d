using System;
using System.Globalization;
using Autodesk.Revit.DB;

namespace PlantDraw.Core
{
    /// <summary>
    /// Turns the toggle state of the direction pad into a model-space unit vector.
    /// Pure maths - no Revit calls - so the UI can preview a direction instantly.
    /// </summary>
    public static class DirectionResolver
    {
        public static ResolvedDirection Resolve(DirectionRequest request, FrameBasis frame)
        {
            if (request == null)
            {
                return ResolvedDirection.Invalid;
            }

            if (!frame.IsValid)
            {
                frame = FrameBasis.World;
            }

            bool explicitlyVertical = request.Direction == CompassDirection.Up ||
                                      request.Direction == CompassDirection.Down;

            bool hasBearing = request.BearingDegrees.HasValue;
            double bearing;
            if (hasBearing)
            {
                bearing = request.BearingDegrees.Value;
            }
            else if (!DirectionToBearing(request.Direction, out bearing))
            {
                bearing = 0.0;
                if (!explicitlyVertical && request.AxisLock == AxisLock.None)
                {
                    return ResolvedDirection.Invalid;
                }
            }

            double elevation = 0.0;
            if (request.Direction == CompassDirection.Up)
            {
                elevation = 90.0;
            }
            else if (request.Direction == CompassDirection.Down)
            {
                elevation = -90.0;
            }
            else if (request.ElevationDegrees.HasValue)
            {
                elevation = request.ElevationDegrees.Value;
            }

            string note = null;
            string axisLabel = null;

            if (request.Ortho)
            {
                double snappedBearing = Math.Round(bearing / 90.0) * 90.0;
                if (!explicitlyVertical && Math.Abs(snappedBearing - bearing) > 1e-6)
                {
                    note = string.Format(
                        CultureInfo.InvariantCulture,
                        "Ortho snapped {0:0.#} deg to {1:0} deg",
                        bearing,
                        Norm360(snappedBearing));
                }

                bearing = snappedBearing;

                double snappedElevation = Math.Round(elevation / 90.0) * 90.0;
                elevation = snappedElevation;
            }

            bearing = Norm360(bearing);

            XYZ vector = BuildVector(frame, bearing, elevation);

            if (request.AxisLock != AxisLock.None)
            {
                XYZ axis = request.AxisLock == AxisLock.X ? XYZ.BasisX
                    : request.AxisLock == AxisLock.Y ? XYZ.BasisY
                    : XYZ.BasisZ;

                double component = Vec.Dot(vector, axis);
                double sign = Math.Abs(component) < 1e-9
                    ? (request.AxisSign < 0 ? -1.0 : 1.0)
                    : Math.Sign(component);

                vector = Vec.Mul(axis, sign);

                string axisName = request.AxisLock == AxisLock.X ? "X" : request.AxisLock == AxisLock.Y ? "Y" : "Z";
                axisLabel = string.Format(CultureInfo.InvariantCulture, "{0}{1}", sign > 0 ? "+" : "-", axisName);
                note = "Locked to " + axisLabel + " (world axis)";
            }

            vector = Vec.Unit(vector);

            if (Vec.IsZero(vector))
            {
                return new ResolvedDirection
                {
                    IsValid = false,
                    Vector = XYZ.BasisX,
                    Label = "no direction",
                    Note = "The current toggles cancel each other out. Pick a direction."
                };
            }

            double east;
            double north;
            double up;
            frame.FromWorld(vector, out east, out north, out up);

            double actualBearing = Norm360(Math.Atan2(east, north) * 180.0 / Math.PI);
            double actualElevation = Math.Asin(Math.Max(-1.0, Math.Min(1.0, up))) * 180.0 / Math.PI;

            return new ResolvedDirection
            {
                IsValid = true,
                Vector = vector,
                BearingDegrees = actualBearing,
                ElevationDegrees = actualElevation,
                Label = axisLabel != null
                    ? "Locked " + axisLabel
                    : Describe(east, north, up, actualElevation, request.Frame == CompassFrame.ProjectNorth),
                Note = note
            };
        }

        private static XYZ BuildVector(FrameBasis frame, double bearingDegrees, double elevationDegrees)
        {
            double b = bearingDegrees * Math.PI / 180.0;
            double e = elevationDegrees * Math.PI / 180.0;

            double horizontal = Math.Cos(e);
            XYZ planar = frame.ToWorld(Math.Sin(b) * horizontal, Math.Cos(b) * horizontal, Math.Sin(e));
            return Vec.Unit(planar);
        }

        public static double Norm360(double degrees)
        {
            double value = degrees % 360.0;
            if (value < 0.0)
            {
                value += 360.0;
            }

            return value;
        }

        /// <summary>Bearing (degrees clockwise from frame north) for one of the eight compass points.</summary>
        public static bool DirectionToBearing(CompassDirection direction, out double bearingDegrees)
        {
            switch (direction)
            {
                case CompassDirection.North: bearingDegrees = 0.0; return true;
                case CompassDirection.NorthEast: bearingDegrees = 45.0; return true;
                case CompassDirection.East: bearingDegrees = 90.0; return true;
                case CompassDirection.SouthEast: bearingDegrees = 135.0; return true;
                case CompassDirection.South: bearingDegrees = 180.0; return true;
                case CompassDirection.SouthWest: bearingDegrees = 225.0; return true;
                case CompassDirection.West: bearingDegrees = 270.0; return true;
                case CompassDirection.NorthWest: bearingDegrees = 315.0; return true;
                case CompassDirection.Up: bearingDegrees = 0.0; return true;
                case CompassDirection.Down: bearingDegrees = 0.0; return true;
                default: bearingDegrees = 0.0; return false;
            }
        }

        /// <summary>Reverse of <see cref="DirectionToBearing"/> for the eight planar directions.</summary>
        public static CompassDirection BearingToDirection(double bearingDegrees)
        {
            int index = (int)Math.Round(Norm360(bearingDegrees) / 45.0) % 8;
            switch (index)
            {
                case 0: return CompassDirection.North;
                case 1: return CompassDirection.NorthEast;
                case 2: return CompassDirection.East;
                case 3: return CompassDirection.SouthEast;
                case 4: return CompassDirection.South;
                case 5: return CompassDirection.SouthWest;
                case 6: return CompassDirection.West;
                default: return CompassDirection.NorthWest;
            }
        }

        public static string DirectionName(CompassDirection direction)
        {
            switch (direction)
            {
                case CompassDirection.North: return "North";
                case CompassDirection.NorthEast: return "North-East";
                case CompassDirection.East: return "East";
                case CompassDirection.SouthEast: return "South-East";
                case CompassDirection.South: return "South";
                case CompassDirection.SouthWest: return "South-West";
                case CompassDirection.West: return "West";
                case CompassDirection.NorthWest: return "North-West";
                case CompassDirection.Up: return "Up";
                case CompassDirection.Down: return "Down";
                default: return "none";
            }
        }

        private static string Describe(double east, double north, double up, double elevation, bool worldFrame)
        {
            if (Math.Abs(up) > 0.9999)
            {
                return up > 0 ? (worldFrame ? "Up (+Z)" : "Up") : (worldFrame ? "Down (-Z)" : "Down");
            }

            string planar;
            double absEast = Math.Abs(east);
            double absNorth = Math.Abs(north);

            if (absEast < 1e-4)
            {
                planar = north > 0 ? (worldFrame ? "North (+Y)" : "North") : (worldFrame ? "South (-Y)" : "South");
            }
            else if (absNorth < 1e-4)
            {
                planar = east > 0 ? (worldFrame ? "East (+X)" : "East") : (worldFrame ? "West (-X)" : "West");
            }
            else
            {
                string ns = north > 0 ? "N" : "S";
                string ew = east > 0 ? "E" : "W";
                double fromNorth = Math.Atan2(absEast, absNorth) * 180.0 / Math.PI;
                planar = string.Format(CultureInfo.InvariantCulture, "{0} {1:0.#}deg {2}", ns, fromNorth, ew);
            }

            if (Math.Abs(elevation) > 0.05)
            {
                planar = string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}, {1:0.#}deg {2}",
                    planar,
                    Math.Abs(elevation),
                    elevation > 0 ? "up" : "down");
            }

            return planar;
        }
    }
}
