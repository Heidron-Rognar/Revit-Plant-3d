using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace PlantDraw.Core
{
    /// <summary>
    /// Entry and display of lengths. Revit stores internal units in feet, the user types
    /// millimetres / metres / inches / feet-and-inches.
    /// </summary>
    public static class LengthUnits
    {
        private const double FeetToMm = 304.8;
        private const double FeetToInch = 12.0;

        private static readonly Regex MetricPattern = new Regex(
            @"^(?<num>[-+]?[0-9]*\.?[0-9]+)\s*(?<unit>mm|millimetre|millimetres|millimeter|millimeters|cm|centimetre|centimetres|centimeter|centimeters|m|metre|metres|meter|meters)?$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private static readonly Regex ImperialPattern = new Regex(
            @"^(?<neg>[-+])?\s*(?:(?<ft>[0-9]*\.?[0-9]+)\s*(?:'|ft|foot|feet|'))?\s*[-]?\s*(?:(?<inch>[0-9]*\.?[0-9]+)\s*(?:""|in|inch|inches))?$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        public static string UnitSuffix(LengthDisplay display)
        {
            switch (display)
            {
                case LengthDisplay.Meters: return "m";
                case LengthDisplay.Inches: return "in";
                case LengthDisplay.FeetInches: return "ft";
                default: return "mm";
            }
        }

        /// <summary>Formats a length (in feet) for the read-outs.</summary>
        public static string Format(double feet, LengthDisplay display)
        {
            switch (display)
            {
                case LengthDisplay.Meters:
                    return string.Format(CultureInfo.InvariantCulture, "{0:0.###} m", feet * FeetToMm / 1000.0);
                case LengthDisplay.Inches:
                    return string.Format(CultureInfo.InvariantCulture, "{0:0.##} in", feet * FeetToInch);
                case LengthDisplay.FeetInches:
                    return FormatFeetInches(feet);
                default:
                    return string.Format(CultureInfo.InvariantCulture, "{0:0.#} mm", feet * FeetToMm);
            }
        }

        public static string FormatNumber(double feet, LengthDisplay display)
        {
            switch (display)
            {
                case LengthDisplay.Meters:
                    return string.Format(CultureInfo.InvariantCulture, "{0:0.###}", feet * FeetToMm / 1000.0);
                case LengthDisplay.Inches:
                    return string.Format(CultureInfo.InvariantCulture, "{0:0.##}", feet * FeetToInch);
                case LengthDisplay.FeetInches:
                    return FormatFeetInches(feet);
                default:
                    return string.Format(CultureInfo.InvariantCulture, "{0:0.#}", feet * FeetToMm);
            }
        }

        public static string FormatFeetInches(double feet)
        {
            bool negative = feet < 0;
            double totalInches = Math.Abs(feet) * FeetToInch;
            double round = Math.Round(totalInches * 16.0) / 16.0;
            int wholeFeet = (int)Math.Floor(round / 12.0);
            double remainder = round - (wholeFeet * 12.0);

            string text;
            if (Math.Abs(remainder) < 0.01)
            {
                text = string.Format(CultureInfo.InvariantCulture, "{0}'-0\"", wholeFeet);
            }
            else
            {
                text = string.Format(CultureInfo.InvariantCulture, "{0}'-{1:0.##}\"", wholeFeet, remainder);
            }

            return negative ? "-" + text : text;
        }

        /// <summary>
        /// Parses a length typed by the user into feet.
        /// Accepts "2400", "2400 mm", "2.4m", "96in", "8'", "8'-6\"", "8 ft 6 in".
        /// A bare number is interpreted in <paramref name="display"/> units.
        /// </summary>
        public static bool TryParse(string text, LengthDisplay display, out double feet)
        {
            feet = 0.0;
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            string value = text.Trim().Replace('\u2212', '-').Replace(",", ".").Replace("\u00A0", " ");

            Match imperial = ImperialPattern.Match(value);
            if (imperial.Success && (value.IndexOf('\'') >= 0 || value.IndexOf('"') >= 0 ||
                                     value.IndexOf("ft", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                     value.IndexOf("in", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                double parsed = 0.0;
                bool any = false;

                Group feetGroup = imperial.Groups["ft"];
                if (feetGroup.Success && TryNumber(feetGroup.Value, out double ftValue))
                {
                    parsed += ftValue * 12.0;
                    any = true;
                }

                Group inchGroup = imperial.Groups["inch"];
                if (inchGroup.Success && TryNumber(inchGroup.Value, out double inValue))
                {
                    parsed += inValue;
                    any = true;
                }

                if (!any)
                {
                    return false;
                }

                if (imperial.Groups["neg"].Value == "-")
                {
                    parsed = -parsed;
                }

                feet = parsed / FeetToInch;
                return true;
            }

            Match metric = MetricPattern.Match(value.Replace(" ", string.Empty));
            if (!metric.Success || !TryNumber(metric.Groups["num"].Value, out double number))
            {
                return false;
            }

            string unit = metric.Groups["unit"].Value.ToLowerInvariant();
            switch (unit)
            {
                case "mm":
                case "millimeter":
                case "millimeters":
                case "millimetre":
                case "millimetres":
                    feet = number / FeetToMm;
                    return true;
                case "cm":
                case "centimeter":
                case "centimeters":
                case "centimetre":
                case "centimetres":
                    feet = number * 10.0 / FeetToMm;
                    return true;
                case "m":
                case "meter":
                case "meters":
                case "metre":
                case "metres":
                    feet = number * 1000.0 / FeetToMm;
                    return true;
                default:
                    switch (display)
                    {
                        case LengthDisplay.Meters:
                            feet = number * 1000.0 / FeetToMm;
                            return true;
                        case LengthDisplay.Inches:
                            feet = number / FeetToInch;
                            return true;
                        case LengthDisplay.FeetInches:
                            feet = number;
                            return true;
                        default:
                            feet = number / FeetToMm;
                            return true;
                    }
            }
        }

        private static bool TryNumber(string text, out double value)
        {
            return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        /// <summary>Handy one-click distances, in the requested display unit.</summary>
        public static double[] QuickValues(LengthDisplay display)
        {
            switch (display)
            {
                case LengthDisplay.Meters: return new[] { 1.0, 2.0, 5.0 };
                case LengthDisplay.Inches: return new[] { 12.0, 24.0, 48.0 };
                case LengthDisplay.FeetInches: return new[] { 1.0, 5.0, 10.0 };
                default: return new[] { 500.0, 1000.0, 2000.0 };
            }
        }
    }
}
