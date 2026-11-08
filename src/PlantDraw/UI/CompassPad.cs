using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using PlantDraw.Core;

namespace PlantDraw.UI
{
    public delegate void DirectionPickedHandler(object sender, CompassDirection direction);

    public delegate void AxisLockRequestedHandler(object sender, AxisLock axis);

    /// <summary>
    /// Plant 3D style direction pad: an eight sector compass ring plus a clickable
    /// isometric X/Y/Z gizmo in the middle. Clicking a ring sector picks a bearing,
    /// clicking a gizmo axis locks the direction to that world axis.
    /// </summary>
    public sealed class CompassPad : FrameworkElement
    {
        public static readonly DependencyProperty DirectionProperty = DependencyProperty.Register(
            nameof(Direction),
            typeof(CompassDirection),
            typeof(CompassPad),
            new FrameworkPropertyMetadata(CompassDirection.None, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty AxisLockProperty = DependencyProperty.Register(
            nameof(AxisLock),
            typeof(AxisLock),
            typeof(CompassPad),
            new FrameworkPropertyMetadata(AxisLock.None, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty EastProperty = DependencyProperty.Register(
            nameof(East),
            typeof(double),
            typeof(CompassPad),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty NorthProperty = DependencyProperty.Register(
            nameof(North),
            typeof(double),
            typeof(CompassPad),
            new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty UpProperty = DependencyProperty.Register(
            nameof(Up),
            typeof(double),
            typeof(CompassPad),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

        private static readonly Brush DiscBrush = Frozen(new LinearGradientBrush(
            Color.FromRgb(0xFB, 0xFC, 0xFD), Color.FromRgb(0xE6, 0xEB, 0xF1), 90.0));

        private static readonly Brush SectorBrush = Frozen(new SolidColorBrush(Color.FromRgb(0xF3, 0xF6, 0xF9)));
        private static readonly Brush SectorHoverBrush = Frozen(new SolidColorBrush(Color.FromRgb(0xFF, 0xEE, 0xD2)));
        private static readonly Brush SectorActiveBrush = Frozen(new SolidColorBrush(Color.FromRgb(0xFF, 0xC9, 0x6B)));
        private static readonly Brush LabelBrush = Frozen(new SolidColorBrush(Color.FromRgb(0x39, 0x45, 0x52)));
        private static readonly Brush LabelActiveBrush = Frozen(new SolidColorBrush(Color.FromRgb(0x5B, 0x3A, 0x00)));
        private static readonly Brush HintBrush = Frozen(new SolidColorBrush(Color.FromRgb(0x7A, 0x86, 0x93)));

        private static readonly Pen RingPen = FrozenPen(new Pen(Frozen(new SolidColorBrush(Color.FromRgb(0xB9, 0xC3, 0xCE))), 1.2));
        private static readonly Pen SectorPen = FrozenPen(new Pen(Frozen(new SolidColorBrush(Color.FromRgb(0xD5, 0xDD, 0xE5))), 1.0));
        private static readonly Pen ArrowPen = FrozenPen(new Pen(Frozen(new SolidColorBrush(Color.FromRgb(0xE0, 0x7B, 0x00))), 3.0));
        private static readonly Pen ArrowGlowPen = FrozenPen(new Pen(Frozen(new SolidColorBrush(Color.FromArgb(0x40, 0xE0, 0x7B, 0x00))), 8.0));

        private static readonly Pen AxisXPen = FrozenPen(new Pen(Frozen(new SolidColorBrush(Color.FromRgb(0xD6, 0x45, 0x45))), 2.0));
        private static readonly Pen AxisYPen = FrozenPen(new Pen(Frozen(new SolidColorBrush(Color.FromRgb(0x3C, 0x9A, 0x5F))), 2.0));
        private static readonly Pen AxisZPen = FrozenPen(new Pen(Frozen(new SolidColorBrush(Color.FromRgb(0x2F, 0x6F, 0xD0))), 2.0));
        private static readonly Pen AxisLockedPen = FrozenPen(new Pen(Frozen(new SolidColorBrush(Color.FromRgb(0x22, 0x2A, 0x33))), 4.0));
        private static readonly Pen AxisGhostPen = FrozenPen(new Pen(Frozen(new SolidColorBrush(Color.FromArgb(0x55, 0x88, 0x94, 0xA2))), 1.0));

        private static readonly Brush AxisXBrush = Frozen(new SolidColorBrush(Color.FromRgb(0xD6, 0x45, 0x45)));
        private static readonly Brush AxisYBrush = Frozen(new SolidColorBrush(Color.FromRgb(0x3C, 0x9A, 0x5F)));
        private static readonly Brush AxisZBrush = Frozen(new SolidColorBrush(Color.FromRgb(0x2F, 0x6F, 0xD0)));

        // Isometric projection of the world axes onto the 2D pad.
        private static readonly Point IsoX = new Point(0.866, 0.5);
        private static readonly Point IsoY = new Point(-0.866, 0.5);
        private static readonly Point IsoZ = new Point(0.0, -1.0);

        private static readonly Typeface RegularTypeface = new Typeface(
            new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

        private static readonly Typeface BoldTypeface = new Typeface(
            new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);

        private static readonly CompassDirection[] RingDirections =
        {
            CompassDirection.North,
            CompassDirection.NorthEast,
            CompassDirection.East,
            CompassDirection.SouthEast,
            CompassDirection.South,
            CompassDirection.SouthWest,
            CompassDirection.West,
            CompassDirection.NorthWest
        };

        private static readonly string[] RingLabels = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };

        private int _hoverSector = -1;
        private AxisLock _hoverAxis = AxisLock.None;

        public CompassPad()
        {
            SnapsToDevicePixels = true;
            Cursor = Cursors.Hand;
        }

        public event DirectionPickedHandler DirectionPicked;

        public event AxisLockRequestedHandler AxisLockRequested;

        public CompassDirection Direction
        {
            get { return (CompassDirection)GetValue(DirectionProperty); }
            set { SetValue(DirectionProperty, value); }
        }

        public AxisLock AxisLock
        {
            get { return (AxisLock)GetValue(AxisLockProperty); }
            set { SetValue(AxisLockProperty, value); }
        }

        /// <summary>East component of the resolved direction, in the active compass frame.</summary>
        public double East
        {
            get { return (double)GetValue(EastProperty); }
            set { SetValue(EastProperty, value); }
        }

        public double North
        {
            get { return (double)GetValue(NorthProperty); }
            set { SetValue(NorthProperty, value); }
        }

        public double Up
        {
            get { return (double)GetValue(UpProperty); }
            set { SetValue(UpProperty, value); }
        }

        protected override void OnRender(DrawingContext dc)
        {
            double width = ActualWidth;
            double height = ActualHeight;
            if (width < 60.0 || height < 60.0)
            {
                return;
            }

            double cx = width / 2.0;
            double cy = height / 2.0;
            double outer = (Math.Min(width, height) / 2.0) - 6.0;
            double inner = outer * 0.63;
            Point center = new Point(cx, cy);

            dc.DrawEllipse(DiscBrush, null, center, outer, outer);

            // Compass ring sectors.
            for (int i = 0; i < RingDirections.Length; i++)
            {
                double start = (i * 45.0) - 22.5;
                double end = (i * 45.0) + 22.5;

                Brush fill = _hoverSector == i ? SectorHoverBrush : SectorBrush;
                if (Direction == RingDirections[i])
                {
                    fill = SectorActiveBrush;
                }

                Geometry geometry = SectorGeometry(cx, cy, inner + 2.0, outer - 1.0, start, end, 10);
                dc.DrawGeometry(fill, SectorPen, geometry);

                bool active = Direction == RingDirections[i];
                Brush labelBrush = active ? LabelActiveBrush : (_hoverSector == i ? LabelActiveBrush : LabelBrush);
                Typeface typeface = active || i % 2 == 0 ? BoldTypeface : RegularTypeface;

                double labelRadius = (inner + outer) / 2.0;
                Point labelPoint = PointOn(cx, cy, labelRadius, i * 45.0);
                DrawCenteredText(dc, RingLabels[i], labelPoint, Math.Max(10.0, outer * 0.11), labelBrush, typeface);
            }

            dc.DrawEllipse(null, RingPen, center, outer, outer);
            dc.DrawEllipse(null, RingPen, center, inner, inner);

            DrawAxisGizmo(dc, cx, cy, inner);
            DrawDirectionArrow(dc, cx, cy, inner);

            if (Direction == CompassDirection.None && AxisLock == AxisLock.None)
            {
                DrawCenteredText(
                    dc,
                    "pick",
                    new Point(cx, cy + (inner * 0.72)),
                    Math.Max(9.0, outer * 0.085),
                    HintBrush,
                    RegularTypeface);
            }
        }

        private void DrawAxisGizmo(DrawingContext dc, double cx, double cy, double inner)
        {
            double length = inner * 0.82;
            Point origin = new Point(cx, cy);

            Point xEnd = new Point(cx + (IsoX.X * length), cy + (IsoX.Y * length));
            Point yEnd = new Point(cx + (IsoY.X * length), cy + (IsoY.Y * length));
            Point zEnd = new Point(cx + (IsoZ.X * length), cy + (IsoZ.Y * length));

            dc.DrawLine(AxisGhostPen, new Point(cx - (IsoX.X * length * 0.55), cy - (IsoX.Y * length * 0.55)), xEnd);
            dc.DrawLine(AxisGhostPen, new Point(cx - (IsoY.X * length * 0.55), cy - (IsoY.Y * length * 0.55)), yEnd);
            dc.DrawLine(AxisGhostPen, new Point(cx - (IsoZ.X * length * 0.55), cy - (IsoZ.Y * length * 0.55)), zEnd);

            double fontSize = Math.Max(9.5, inner * 0.17);
            DrawAxis(dc, origin, xEnd, "X", AxisXPen, AxisXBrush, AxisLock.X, _hoverAxis == AxisLock.X, fontSize);
            DrawAxis(dc, origin, yEnd, "Y", AxisYPen, AxisYBrush, AxisLock.Y, _hoverAxis == AxisLock.Y, fontSize);
            DrawAxis(dc, origin, zEnd, "Z", AxisZPen, AxisZBrush, AxisLock.Z, _hoverAxis == AxisLock.Z, fontSize);

            dc.DrawEllipse(Brushes.White, RingPen, origin, 3.0, 3.0);
        }

        private void DrawAxis(
            DrawingContext dc,
            Point origin,
            Point end,
            string label,
            Pen pen,
            Brush brush,
            AxisLock axis,
            bool hovered,
            double fontSize)
        {
            bool locked = AxisLock == axis;
            Pen usePen = locked ? AxisLockedPen : (hovered ? new Pen(brush, 3.5) : pen);

            dc.DrawLine(usePen, origin, end);

            Point labelPoint = new Point(
                origin.X + ((end.X - origin.X) * 1.13),
                origin.Y + ((end.Y - origin.Y) * 1.13));

            DrawCenteredText(
                dc,
                label,
                labelPoint,
                fontSize,
                locked ? Brushes.Black : brush,
                BoldTypeface);
        }

        private void DrawDirectionArrow(DrawingContext dc, double cx, double cy, double inner)
        {
            Point projected = Project3D(East, North, Up);
            double magnitude = Math.Sqrt((projected.X * projected.X) + (projected.Y * projected.Y));
            if (magnitude < 1e-6)
            {
                return;
            }

            double scale = (inner * 0.95) / magnitude;
            Point tip = new Point(cx + (projected.X * scale), cy + (projected.Y * scale));
            Point origin = new Point(cx, cy);

            dc.DrawLine(ArrowGlowPen, origin, tip);
            dc.DrawLine(ArrowPen, origin, tip);
            dc.DrawEllipse(ArrowPen.Brush, null, tip, 4.0, 4.0);
        }

        private static Point Project3D(double east, double north, double up)
        {
            double x = (east * IsoX.X) + (north * IsoY.X) + (up * IsoZ.X);
            double y = (east * IsoX.Y) + (north * IsoY.Y) + (up * IsoZ.Y);
            return new Point(x, y);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            Point point = e.GetPosition(this);
            double cx = ActualWidth / 2.0;
            double cy = ActualHeight / 2.0;
            double outer = (Math.Min(ActualWidth, ActualHeight) / 2.0) - 6.0;
            double inner = outer * 0.63;

            AxisLock axis;
            int sector = -1;
            if (!TryHitAxis(point, cx, cy, inner, out axis))
            {
                axis = AxisLock.None;
                TryHitSector(point, cx, cy, inner, outer, out sector);
            }

            if (axis != _hoverAxis || sector != _hoverSector)
            {
                _hoverAxis = axis;
                _hoverSector = sector;
                InvalidateVisual();
            }
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            base.OnMouseLeave(e);

            if (_hoverAxis != AxisLock.None || _hoverSector >= 0)
            {
                _hoverAxis = AxisLock.None;
                _hoverSector = -1;
                InvalidateVisual();
            }
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);

            Point point = e.GetPosition(this);
            double cx = ActualWidth / 2.0;
            double cy = ActualHeight / 2.0;
            double outer = (Math.Min(ActualWidth, ActualHeight) / 2.0) - 6.0;
            double inner = outer * 0.63;

            AxisLock axis;
            if (TryHitAxis(point, cx, cy, inner, out axis))
            {
                AxisLockRequestedHandler axisHandler = AxisLockRequested;
                if (axisHandler != null)
                {
                    axisHandler(this, axis);
                }

                e.Handled = true;
                return;
            }

            int sector;
            if (TryHitSector(point, cx, cy, inner, outer, out sector) && sector >= 0)
            {
                DirectionPickedHandler handler = DirectionPicked;
                if (handler != null)
                {
                    handler(this, RingDirections[sector]);
                }

                e.Handled = true;
            }
        }

        private static bool TryHitAxis(Point point, double cx, double cy, double inner, out AxisLock axis)
        {
            axis = AxisLock.None;

            double length = inner * 0.82;
            double bestDistance = 11.0;
            AxisLock best = AxisLock.None;

            CheckAxis(point, cx, cy, IsoX, length, AxisLock.X, ref best, ref bestDistance);
            CheckAxis(point, cx, cy, IsoY, length, AxisLock.Y, ref best, ref bestDistance);
            CheckAxis(point, cx, cy, IsoZ, length, AxisLock.Z, ref best, ref bestDistance);

            axis = best;
            return best != AxisLock.None;
        }

        private static void CheckAxis(
            Point point,
            double cx,
            double cy,
            Point direction,
            double length,
            AxisLock axis,
            ref AxisLock best,
            ref double bestDistance)
        {
            double dx = point.X - cx;
            double dy = point.Y - cy;
            double along = (dx * direction.X) + (dy * direction.Y);
            if (along < 2.0 || along > length * 1.2)
            {
                return;
            }

            double perpendicular = Math.Abs((dx * direction.Y) - (dy * direction.X));
            if (perpendicular < bestDistance)
            {
                bestDistance = perpendicular;
                best = axis;
            }
        }

        private static bool TryHitSector(Point point, double cx, double cy, double inner, double outer, out int sector)
        {
            sector = -1;

            double dx = point.X - cx;
            double dy = point.Y - cy;
            double distance = Math.Sqrt((dx * dx) + (dy * dy));

            if (distance < inner || distance > outer + 4.0)
            {
                return false;
            }

            double bearing = Math.Atan2(dx, -dy) * 180.0 / Math.PI;
            if (bearing < 0.0)
            {
                bearing += 360.0;
            }

            sector = (int)Math.Round(bearing / 45.0) % 8;
            return true;
        }

        private void DrawCenteredText(
            DrawingContext dc,
            string text,
            Point center,
            double fontSize,
            Brush brush,
            Typeface typeface)
        {
            FormattedText formatted = new FormattedText(
                text,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                typeface,
                fontSize,
                brush,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);

            dc.DrawText(formatted, new Point(center.X - (formatted.Width / 2.0), center.Y - (formatted.Height / 2.0)));
        }

        private static Point PointOn(double cx, double cy, double radius, double bearingDegrees)
        {
            double theta = (bearingDegrees - 90.0) * Math.PI / 180.0;
            return new Point(cx + (radius * Math.Cos(theta)), cy + (radius * Math.Sin(theta)));
        }

        private static Geometry SectorGeometry(
            double cx,
            double cy,
            double r1,
            double r2,
            double startDeg,
            double endDeg,
            int steps)
        {
            List<Point> points = new List<Point>();

            for (int i = 0; i <= steps; i++)
            {
                double angle = startDeg + (((endDeg - startDeg) * i) / steps);
                points.Add(PointOn(cx, cy, r2, angle));
            }

            for (int i = steps; i >= 0; i--)
            {
                double angle = startDeg + (((endDeg - startDeg) * i) / steps);
                points.Add(PointOn(cx, cy, r1, angle));
            }

            StreamGeometry geometry = new StreamGeometry();
            using (StreamGeometryContext context = geometry.Open())
            {
                context.BeginFigure(points[0], true, true);
                for (int i = 1; i < points.Count; i++)
                {
                    context.LineTo(points[i], true, false);
                }
            }

            geometry.Freeze();
            return geometry;
        }

        private static Brush Frozen(Brush brush)
        {
            brush.Freeze();
            return brush;
        }

        private static Pen FrozenPen(Pen pen)
        {
            pen.Freeze();
            return pen;
        }
    }
}
