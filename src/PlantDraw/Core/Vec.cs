using System;
using Autodesk.Revit.DB;

namespace PlantDraw.Core
{
    /// <summary>
    /// Small, side-effect free vector helpers.
    ///
    /// Everything in this add-in goes through these helpers instead of the mutating
    /// members on <see cref="XYZ"/> (Normalize/Negate mutate in place, which is a very
    /// easy way to corrupt a cached point). All helpers allocate a new XYZ.
    /// </summary>
    public static class Vec
    {
        public const double Tolerance = 1e-9;

        public static XYZ New(double x, double y, double z)
        {
            return new XYZ(x, y, z);
        }

        public static XYZ Add(XYZ a, XYZ b)
        {
            return new XYZ(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        }

        public static XYZ Sub(XYZ a, XYZ b)
        {
            return new XYZ(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        }

        public static XYZ Mul(XYZ a, double scalar)
        {
            return new XYZ(a.X * scalar, a.Y * scalar, a.Z * scalar);
        }

        public static XYZ Neg(XYZ a)
        {
            return new XYZ(-a.X, -a.Y, -a.Z);
        }

        public static double Dot(XYZ a, XYZ b)
        {
            return (a.X * b.X) + (a.Y * b.Y) + (a.Z * b.Z);
        }

        public static XYZ Cross(XYZ a, XYZ b)
        {
            return new XYZ(
                (a.Y * b.Z) - (a.Z * b.Y),
                (a.Z * b.X) - (a.X * b.Z),
                (a.X * b.Y) - (a.Y * b.X));
        }

        public static double Length(XYZ a)
        {
            return Math.Sqrt((a.X * a.X) + (a.Y * a.Y) + (a.Z * a.Z));
        }

        public static double Distance(XYZ a, XYZ b)
        {
            return Length(Sub(a, b));
        }

        public static bool IsZero(XYZ a, double tolerance = 1e-9)
        {
            return Math.Abs(a.X) <= tolerance && Math.Abs(a.Y) <= tolerance && Math.Abs(a.Z) <= tolerance;
        }

        /// <summary>Returns a unit vector, or XYZ.Zero when the input has no length.</summary>
        public static XYZ Unit(XYZ a)
        {
            double length = Length(a);
            if (length <= 1e-12)
            {
                return XYZ.Zero;
            }

            return new XYZ(a.X / length, a.Y / length, a.Z / length);
        }

        /// <summary>Component of <paramref name="a"/> that lies in the horizontal (XY) plane, normalised.</summary>
        public static XYZ HorizontalUnit(XYZ a)
        {
            return Unit(new XYZ(a.X, a.Y, 0.0));
        }

        public static XYZ Axis(int index)
        {
            switch (index)
            {
                case 0: return XYZ.BasisX;
                case 1: return XYZ.BasisY;
                default: return XYZ.BasisZ;
            }
        }

        public static string AxisName(int index)
        {
            switch (index)
            {
                case 0: return "X";
                case 1: return "Y";
                default: return "Z";
            }
        }
    }
}
