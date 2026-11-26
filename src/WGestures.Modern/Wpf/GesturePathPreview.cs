using System.Windows;
using System.Windows.Media;

namespace WGestures.Modern;

/// <summary>Small, normalized path glyph rendered entirely with WPF drawing primitives.</summary>
public sealed class GesturePathPreview : FrameworkElement
{
    public static readonly DependencyProperty PathTextProperty = DependencyProperty.Register(nameof(PathText), typeof(string),
        typeof(GesturePathPreview), new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty StrokeProperty = DependencyProperty.Register(nameof(Stroke), typeof(Brush),
        typeof(GesturePathPreview), new FrameworkPropertyMetadata(Brushes.DodgerBlue, FrameworkPropertyMetadataOptions.AffectsRender));
    public string PathText { get => (string)GetValue(PathTextProperty); set => SetValue(PathTextProperty, value); }
    public Brush Stroke { get => (Brush)GetValue(StrokeProperty); set => SetValue(StrokeProperty, value); }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        if (ActualWidth <= 12 || ActualHeight <= 12) return;
        const string arrows = "↑↗→↘↓↙←↖";
        var points = new List<Point> { new(0,0) };
        foreach (var symbol in (PathText ?? "").Take(32))
        {
            int i = arrows.IndexOf(symbol);
            if (i < 0) continue;
            double angle = i * Math.PI / 4;
            var last = points[^1];
            points.Add(new Point(last.X + Math.Sin(angle), last.Y - Math.Cos(angle)));
        }
        double minX = points.Min(q => q.X), maxX = points.Max(q => q.X);
        double minY = points.Min(q => q.Y), maxY = points.Max(q => q.Y);
        double scale = Math.Min((ActualWidth - 16) / Math.Max(maxX - minX, 1),
            (ActualHeight - 16) / Math.Max(maxY - minY, 1));
        Point Map(Point q) => new(ActualWidth / 2 + (q.X - (minX + maxX) / 2) * scale,
            ActualHeight / 2 + (q.Y - (minY + maxY) / 2) * scale);
        var pen = new Pen(Stroke, 2.6) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
        for (int i = 1; i < points.Count; i++) dc.DrawLine(pen, Map(points[i - 1]), Map(points[i]));
        dc.DrawEllipse(null, pen, Map(points[0]), 2.5, 2.5);
        if (points.Count > 1) dc.DrawEllipse(Stroke, null, Map(points[^1]), 3.1, 3.1);
    }
}
