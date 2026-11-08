using System;
using System.Collections.Generic;
using System.Globalization;
using Autodesk.Revit.DB;

namespace PlantDraw.Core
{
    /// <summary>
    /// Builds the sketch plane that hosts each segment.
    ///
    /// Revit model curves must lie inside a sketch plane, so the plane is derived from the
    /// segment direction: horizontal runs land on an XY plane (the natural routing plane),
    /// vertical runs on XZ/YZ. Sketch planes are cached per (axis, offset) so a long chain of
    /// horizontal segments only ever creates one plane per elevation.
    /// </summary>
    public static class WorkPlaneFactory
    {
        /// <summary>Normal of the plane that will host a segment running along <paramref name="direction"/>.</summary>
        public static XYZ ResolveNormal(XYZ direction, PlaneMode mode)
        {
            switch (mode)
            {
                case PlaneMode.XY:
                    return XYZ.BasisZ;
                case PlaneMode.XZ:
                    return XYZ.BasisY;
                case PlaneMode.YZ:
                    return XYZ.BasisX;
            }

            XYZ dir = Vec.Unit(direction);

            // Prefer the XY plane (normal Z) for level runs - that is what people expect.
            if (Math.Abs(dir.Z) < 1e-9)
            {
                return XYZ.BasisZ;
            }

            if (Math.Abs(dir.X) < 1e-9)
            {
                return XYZ.BasisX;
            }

            if (Math.Abs(dir.Y) < 1e-9)
            {
                return XYZ.BasisY;
            }

            // Sloped or skewed run: any plane that contains the direction will do.
            XYZ normal = Vec.Cross(dir, XYZ.BasisZ);
            if (Vec.IsZero(normal, 1e-9))
            {
                normal = Vec.Cross(dir, XYZ.BasisX);
            }

            return Vec.Unit(normal);
        }

        public static string AxisKey(XYZ normal)
        {
            if (Math.Abs(Math.Abs(normal.Z) - 1.0) < 1e-9)
            {
                return "Z";
            }

            if (Math.Abs(Math.Abs(normal.X) - 1.0) < 1e-9)
            {
                return "X";
            }

            if (Math.Abs(Math.Abs(normal.Y) - 1.0) < 1e-9)
            {
                return "Y";
            }

            return null;
        }

        /// <summary>
        /// Returns a sketch plane that contains the segment start -> direction.
        /// <paramref name="created"/> tells the caller whether a new element was added
        /// (so it can be cleaned up again by Undo / Clear).
        /// </summary>
        public static SketchPlane Resolve(
            Document document,
            XYZ start,
            XYZ direction,
            PlaneMode mode,
            IDictionary<string, ElementId> cache,
            out bool created)
        {
            created = false;

            XYZ normal = ResolveNormal(direction, mode);

            if (mode != PlaneMode.Auto && Math.Abs(Vec.Dot(Vec.Unit(direction), normal)) > 1e-6)
            {
                throw new InvalidOperationException(string.Format(
                    CultureInfo.InvariantCulture,
                    "This direction does not lie in the {0} work plane. Pick 'Auto' for the work plane, or set the axis lock.",
                    mode == PlaneMode.XY ? "XY" : mode == PlaneMode.XZ ? "XZ" : "YZ"));
            }

            string axisKey = AxisKey(normal);
            string cacheKey = null;
            if (axisKey != null && cache != null)
            {
                double offset = Vec.Dot(start, normal);
                cacheKey = string.Format(CultureInfo.InvariantCulture, "{0}|{1:0.####}", axisKey, offset);

                if (cache.TryGetValue(cacheKey, out ElementId cachedId) && cachedId != ElementId.InvalidElementId)
                {
                    if (document.GetElement(cachedId) is SketchPlane cached && cached.IsValidObject)
                    {
                        return cached;
                    }

                    cache.Remove(cacheKey);
                }
            }

            Plane plane = Plane.CreateByNormalAndOrigin(normal, start);
            SketchPlane sketchPlane = SketchPlane.Create(document, plane);
            created = true;

            if (cacheKey != null && cache != null)
            {
                cache[cacheKey] = sketchPlane.Id;
            }

            return sketchPlane;
        }
    }
}
