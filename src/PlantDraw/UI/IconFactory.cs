using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PlantDraw.UI
{
    /// <summary>
    /// Ribbon button icons, drawn with vector shapes at load time.
    /// Keeps the add-in free of binary resources that can go missing after a copy.
    /// </summary>
    internal static class IconFactory
    {
        private static readonly Brush DarkBrush = Freeze(new SolidColorBrush(Color.FromRgb(0x33, 0x40, 0x4D)));
        private static readonly Brush AmberBrush = Freeze(new SolidColorBrush(Color.FromRgb(0xE0, 0x7B, 0x00)));
        private static readonly Brush MutedBrush = Freeze(new SolidColorBrush(Color.FromRgb(0x66, 0x72, 0x80)));
        private static readonly Brush RedBrush = Freeze(new SolidColorBrush(Color.FromRgb(0xB0, 0x2A, 0x2A)));

        public static ImageSource CompassIcon(int size)
        {
            return Render(size, (context, side) =>
            {
                double center = side / 2.0;
                double radius = side * 0.40;
                double stroke = Math.Max(1.0, side * 0.07);

                Pen ring = new Pen(DarkBrush, stroke);
                context.DrawEllipse(null, ring, new Point(center, center), radius, radius);

                Pen tick = new Pen(MutedBrush, Math.Max(0.8, side * 0.05));
                context.DrawLine(tick, new Point(center, center - radius), new Point(center, center - (radius * 0.6)));
                context.DrawLine(tick, new Point(center, center + radius), new Point(center, center + (radius * 0.6)));
                context.DrawLine(tick, new Point(center - radius, center), new Point(center - (radius * 0.6), center));
                context.DrawLine(tick, new Point(center + radius, center), new Point(center + (radius * 0.6), center));

                Pen needle = new Pen(AmberBrush, Math.Max(1.2, side * 0.13));
                context.DrawLine(
                    needle,
                    new Point(center - (radius * 0.35), center + (radius * 0.55)),
                    new Point(center + (radius * 0.62), center - (radius * 0.62)));
            });
        }

        public static ImageSource RouteIcon(int size)
        {
            return Render(size, (context, side) =>
            {
                Pen route = new Pen(AmberBrush, Math.Max(1.2, side * 0.11));
                Point a = new Point(side * 0.18, side * 0.78);
                Point b = new Point(side * 0.18, side * 0.40);
                Point c = new Point(side * 0.78, side * 0.40);

                context.DrawLine(route, a, b);
                context.DrawLine(route, b, c);

                Brush node = DarkBrush;
                double nodeRadius = Math.Max(1.4, side * 0.10);
                context.DrawEllipse(node, null, a, nodeRadius, nodeRadius);
                context.DrawEllipse(node, null, c, nodeRadius, nodeRadius);
            });
        }

        public static ImageSource UndoIcon(int size)
        {
            return Render(size, (context, side) =>
            {
                Pen pen = new Pen(DarkBrush, Math.Max(1.2, side * 0.11));
                double left = side * 0.20;
                double right = side * 0.82;
                double middle = side * 0.52;

                context.DrawLine(pen, new Point(right, side * 0.72), new Point(left, side * 0.72));
                context.DrawLine(pen, new Point(left, side * 0.72), new Point(left, side * 0.34));
                context.DrawLine(pen, new Point(left, side * 0.34), new Point(middle, side * 0.34));
                context.DrawLine(pen, new Point(middle, side * 0.34), new Point(middle, side * 0.20));
            });
        }

        public static ImageSource ClearIcon(int size)
        {
            return Render(size, (context, side) =>
            {
                Pen pen = new Pen(RedBrush, Math.Max(1.4, side * 0.13));
                context.DrawLine(pen, new Point(side * 0.24, side * 0.24), new Point(side * 0.76, side * 0.76));
                context.DrawLine(pen, new Point(side * 0.76, side * 0.24), new Point(side * 0.24, side * 0.76));
            });
        }

        private static ImageSource Render(int size, Action<DrawingContext, double> draw)
        {
            try
            {
                DrawingVisual visual = new DrawingVisual();
                using (DrawingContext context = visual.RenderOpen())
                {
                    draw(context, size);
                }

                RenderTargetBitmap bitmap = new RenderTargetBitmap(size, size, 96.0, 96.0, PixelFormats.Pbgra32);
                bitmap.Render(visual);
                bitmap.Freeze();
                return bitmap;
            }
            catch
            {
                return null;
            }
        }

        private static Brush Freeze(Brush brush)
        {
            brush.Freeze();
            return brush;
        }
    }
}
