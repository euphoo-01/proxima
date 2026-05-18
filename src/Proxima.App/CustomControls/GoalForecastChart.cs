using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Proxima.App.DesignSystem.Charts;
using Proxima.Core.Application.Goals;

namespace Proxima.App.CustomControls;

public sealed class GoalForecastChart : Control
{
    public static readonly StyledProperty<IReadOnlyList<GoalForecastChartPoint>?> PointsProperty =
        AvaloniaProperty.Register<GoalForecastChart, IReadOnlyList<GoalForecastChartPoint>?>(nameof(Points));

    public static readonly StyledProperty<IReadOnlyList<GoalForecastMilestone>?> MilestonesProperty =
        AvaloniaProperty.Register<GoalForecastChart, IReadOnlyList<GoalForecastMilestone>?>(nameof(Milestones));

    public static readonly StyledProperty<int> HorizonYearsProperty =
        AvaloniaProperty.Register<GoalForecastChart, int>(nameof(HorizonYears), 25);

    public static readonly StyledProperty<int> StartYearProperty =
        AvaloniaProperty.Register<GoalForecastChart, int>(nameof(StartYear), DateTimeOffset.Now.Year);

    public static readonly StyledProperty<bool> IsLoadingProperty =
        AvaloniaProperty.Register<GoalForecastChart, bool>(nameof(IsLoading));

    public static readonly StyledProperty<string?> EmptyStateTextProperty =
        AvaloniaProperty.Register<GoalForecastChart, string?>(nameof(EmptyStateText), "Добавьте цель для построения прогноза");

    public static readonly StyledProperty<string?> ErrorStateTextProperty =
        AvaloniaProperty.Register<GoalForecastChart, string?>(nameof(ErrorStateText));

    private int _hoveredMilestoneIndex = -1;

    static GoalForecastChart()
    {
        AffectsRender<GoalForecastChart>(
            PointsProperty,
            MilestonesProperty,
            HorizonYearsProperty,
            StartYearProperty,
            IsLoadingProperty,
            EmptyStateTextProperty,
            ErrorStateTextProperty);
    }

    public IReadOnlyList<GoalForecastChartPoint>? Points
    {
        get => GetValue(PointsProperty);
        set => SetValue(PointsProperty, value);
    }

    public IReadOnlyList<GoalForecastMilestone>? Milestones
    {
        get => GetValue(MilestonesProperty);
        set => SetValue(MilestonesProperty, value);
    }

    public int HorizonYears
    {
        get => GetValue(HorizonYearsProperty);
        set => SetValue(HorizonYearsProperty, value);
    }

    public int StartYear
    {
        get => GetValue(StartYearProperty);
        set => SetValue(StartYearProperty, value);
    }

    public bool IsLoading
    {
        get => GetValue(IsLoadingProperty);
        set => SetValue(IsLoadingProperty, value);
    }

    public string? EmptyStateText
    {
        get => GetValue(EmptyStateTextProperty);
        set => SetValue(EmptyStateTextProperty, value);
    }

    public string? ErrorStateText
    {
        get => GetValue(ErrorStateTextProperty);
        set => SetValue(ErrorStateTextProperty, value);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        IReadOnlyList<GoalForecastChartPoint>? points = Points;
        IReadOnlyList<GoalForecastMilestone>? milestones = Milestones;
        if (points is null || points.Count == 0 || milestones is null || milestones.Count == 0)
        {
            SetHoveredMilestone(-1);
            return;
        }

        ChartLayout layout = CreateLayout(Bounds.Width, Bounds.Height);
        decimal maxValue = ResolveMaxValue(points, milestones);
        IReadOnlyList<VisualMilestone> visualMilestones = BuildVisualMilestones(milestones, maxValue, layout);
        Point pointer = e.GetPosition(this);
        int hovered = -1;
        double minDistance = 18d;

        for (int i = 0; i < visualMilestones.Count; i++)
        {
            Point marker = visualMilestones[i].Position;
            double distance = Math.Sqrt(Math.Pow(pointer.X - marker.X, 2d) + Math.Pow(pointer.Y - marker.Y, 2d));
            if (distance <= minDistance)
            {
                hovered = i;
                minDistance = distance;
            }
        }

        SetHoveredMilestone(hovered);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        SetHoveredMilestone(-1);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        if (IsLoading)
        {
            DrawStateText(context, "Загрузка прогноза...");
            return;
        }

        if (!string.IsNullOrWhiteSpace(ErrorStateText))
        {
            DrawStateText(context, ErrorStateText!);
            return;
        }

        IReadOnlyList<GoalForecastChartPoint>? points = Points;
        if (points is null || points.Count == 0)
        {
            DrawStateText(context, EmptyStateText ?? "Нет данных для прогноза");
            return;
        }

        IReadOnlyList<GoalForecastMilestone> milestones = Milestones ?? [];
        decimal maxValue = ResolveMaxValue(points, milestones);
        ChartLayout layout = CreateLayout(Bounds.Width, Bounds.Height);

        IBrush gridBrush = ResolveBrush("ProximaBrush.BorderSubtle");
        IBrush labelBrush = ResolveBrush("ProximaBrush.TextSecondary");
        IBrush mutedBrush = ResolveBrush("ProximaBrush.TextMuted");
        IBrush lineBrush = ResolveBrush("ProximaBrush.Brand");
        IBrush markerBrush = ResolveBrush("ProximaBrush.Success");
        IBrush markerBorderBrush = ResolveBrush("ProximaBrush.Surface");
        IBrush tooltipBackground = ResolveBrush("ProximaBrush.Surface");
        IBrush tooltipBorder = ResolveBrush("ProximaBrush.Border");

        IReadOnlyList<VisualMilestone> visualMilestones = BuildVisualMilestones(milestones, maxValue, layout);

        DrawGridAndYearAxis(context, layout, maxValue, gridBrush, labelBrush, mutedBrush);
        DrawProjectionArea(context, points, maxValue, layout, lineBrush);
        DrawProjectionLine(context, points, maxValue, layout, lineBrush);
        DrawMilestones(context, visualMilestones, markerBrush, markerBorderBrush, labelBrush);

        if (_hoveredMilestoneIndex >= 0 && _hoveredMilestoneIndex < visualMilestones.Count)
        {
            DrawMilestoneTooltip(context, visualMilestones[_hoveredMilestoneIndex], tooltipBackground, tooltipBorder, labelBrush, mutedBrush);
        }
    }

    private void SetHoveredMilestone(int value)
    {
        if (_hoveredMilestoneIndex == value)
        {
            return;
        }

        _hoveredMilestoneIndex = value;
        InvalidateVisual();
    }

    private static IReadOnlyList<VisualMilestone> BuildVisualMilestones(
        IReadOnlyList<GoalForecastMilestone> milestones,
        decimal maxValue,
        ChartLayout layout)
    {
        if (milestones.Count == 0)
        {
            return [];
        }

        List<VisualMilestoneDraft> drafts = new(milestones.Count);
        for (int i = 0; i < milestones.Count; i++)
        {
            GoalForecastMilestone milestone = milestones[i];
            drafts.Add(new VisualMilestoneDraft(
                i,
                milestone,
                ToPoint(milestone.MonthIndex, milestone.TargetAmount, maxValue, layout)));
        }

        VisualMilestone[] result = new VisualMilestone[milestones.Count];
        foreach (IGrouping<(int X, int Y), VisualMilestoneDraft> group in drafts.GroupBy(item => (
                     X: (int)Math.Round(item.BasePoint.X / 10d, MidpointRounding.AwayFromZero),
                     Y: (int)Math.Round(item.BasePoint.Y / 10d, MidpointRounding.AwayFromZero))))
        {
            VisualMilestoneDraft[] cluster = group
                .OrderBy(item => item.Milestone.TargetAmount)
                .ThenBy(item => item.Milestone.Title, StringComparer.CurrentCultureIgnoreCase)
                .ToArray();

            for (int i = 0; i < cluster.Length; i++)
            {
                VisualMilestoneDraft item = cluster[i];
                Point visualPoint = cluster.Length == 1
                    ? item.BasePoint
                    : OffsetClusterPoint(item.BasePoint, i, cluster.Length, layout);

                result[item.SourceIndex] = new VisualMilestone(item.Milestone, visualPoint, layout);
            }
        }

        return result;
    }

    private static Point OffsetClusterPoint(Point point, int index, int count, ChartLayout layout)
    {
        double center = (count - 1) / 2d;
        double xOffset = (index - center) * 14d;
        double yOffset = index % 2 == 0 ? -5d : 5d;
        double x = Math.Clamp(point.X + xOffset, layout.Left + 8, layout.Right - 8);
        double y = Math.Clamp(point.Y + yOffset, layout.Top + 8, layout.Bottom - 8);
        return new Point(x, y);
    }

    private void DrawProjectionArea(
        DrawingContext context,
        IReadOnlyList<GoalForecastChartPoint> points,
        decimal maxValue,
        ChartLayout layout,
        IBrush lineBrush)
    {
        if (points.Count < 2)
        {
            return;
        }

        StreamGeometry geometry = new();
        using (StreamGeometryContext area = geometry.Open())
        {
            Point first = ToPoint(points[0].MonthIndex, points[0].Amount, maxValue, layout);
            area.BeginFigure(new Point(first.X, layout.Bottom), true);
            area.LineTo(first);

            for (int i = 1; i < points.Count; i++)
            {
                area.LineTo(ToPoint(points[i].MonthIndex, points[i].Amount, maxValue, layout));
            }

            Point last = ToPoint(points[^1].MonthIndex, points[^1].Amount, maxValue, layout);
            area.LineTo(new Point(last.X, layout.Bottom));
            area.LineTo(new Point(first.X, layout.Bottom));
            area.EndFigure(true);
        }

        context.DrawGeometry(CreateProjectionAreaBrush(lineBrush), null, geometry);
    }

    private void DrawProjectionLine(
        DrawingContext context,
        IReadOnlyList<GoalForecastChartPoint> points,
        decimal maxValue,
        ChartLayout layout,
        IBrush lineBrush)
    {
        if (points.Count == 1)
        {
            Point single = ToPoint(points[0].MonthIndex, points[0].Amount, maxValue, layout);
            context.DrawEllipse(lineBrush, null, single, 4, 4);
            return;
        }

        StreamGeometry geometry = new();
        using StreamGeometryContext line = geometry.Open();
        Point first = ToPoint(points[0].MonthIndex, points[0].Amount, maxValue, layout);
        line.BeginFigure(first, false);

        for (int i = 1; i < points.Count; i++)
        {
            line.LineTo(ToPoint(points[i].MonthIndex, points[i].Amount, maxValue, layout));
        }

        context.DrawGeometry(null, new Pen(lineBrush, 2.2), geometry);
    }

    private void DrawMilestones(
        DrawingContext context,
        IReadOnlyList<VisualMilestone> visualMilestones,
        IBrush markerBrush,
        IBrush markerBorderBrush,
        IBrush labelBrush)
    {
        for (int i = 0; i < visualMilestones.Count; i++)
        {
            VisualMilestone visual = visualMilestones[i];
            Point marker = visual.Position;
            double radius = i == _hoveredMilestoneIndex ? 8 : 7;

            context.DrawEllipse(markerBorderBrush, null, marker, radius + 3, radius + 3);
            context.DrawEllipse(markerBrush, null, marker, radius, radius);

            string label = CompactLabel(visual.Milestone.Title, 16);
            Size size = MeasureText(label, ProximaChartTheme.AxisLabelFontSize);
            double labelX = Math.Clamp(marker.X - size.Width / 2d, visual.Layout.Left, visual.Layout.Right - size.Width);
            double labelY = Math.Clamp(marker.Y + 10, visual.Layout.Top + 2, visual.Layout.Bottom - 20);
            DrawText(context, label, labelBrush, labelX, labelY, ProximaChartTheme.AxisLabelFontSize, FontWeight.Bold);
        }
    }

    private void DrawMilestoneTooltip(
        DrawingContext context,
        VisualMilestone visualMilestone,
        IBrush background,
        IBrush border,
        IBrush textBrush,
        IBrush mutedBrush)
    {
        GoalForecastMilestone milestone = visualMilestone.Milestone;
        Point marker = visualMilestone.Position;
        double boxWidth = 188;
        double boxHeight = 70;
        double boxX = Math.Clamp(marker.X - boxWidth / 2d, 8, Bounds.Width - boxWidth - 8);
        double boxY = marker.Y - boxHeight - 14;
        if (boxY < 8)
        {
            boxY = marker.Y + 18;
        }

        Rect rect = new(boxX, boxY, boxWidth, boxHeight);
        context.DrawRectangle(background, new Pen(border, 1), rect, 12, 12);

        DrawText(context, CompactLabel(milestone.Title, 22), textBrush, boxX + 14, boxY + 12, 12, FontWeight.Bold);
        DrawText(context, $"Достижение: {milestone.EstimatedYear}", mutedBrush, boxX + 14, boxY + 32, 11, FontWeight.Medium);
        DrawText(context, FormatMoney(milestone.TargetAmount, milestone.Currency), textBrush, boxX + 14, boxY + 49, 11, FontWeight.Bold);
    }

    private void DrawGridAndYearAxis(
        DrawingContext context,
        ChartLayout layout,
        decimal maxValue,
        IBrush gridBrush,
        IBrush labelBrush,
        IBrush mutedBrush)
    {
        Pen gridPen = new(gridBrush, 1);

        const int horizontalLines = 4;
        for (int i = 0; i <= horizontalLines; i++)
        {
            double t = i / (double)horizontalLines;
            double y = layout.Top + layout.Height * t;
            context.DrawLine(gridPen, new Point(layout.Left, y), new Point(layout.Right, y));
        }

        int yearStep = ResolveYearStep(HorizonYears);
        for (int yearOffset = 0; yearOffset <= HorizonYears; yearOffset += yearStep)
        {
            double x = layout.Left + layout.Width * yearOffset / Math.Max(1, HorizonYears);
            context.DrawLine(gridPen, new Point(x, layout.Bottom), new Point(x, layout.Bottom + 4));

            string year = (StartYear + yearOffset).ToString(System.Globalization.CultureInfo.InvariantCulture);
            Size size = MeasureText(year, ProximaChartTheme.AxisLabelFontSize);
            DrawText(context, year, labelBrush, x - size.Width / 2d, layout.Bottom + 22, ProximaChartTheme.AxisLabelFontSize, FontWeight.Bold);
        }

        DrawText(context, FormatMoney(maxValue, "USD"), mutedBrush, 0, layout.Top - 4, 10, FontWeight.Medium);
        DrawText(context, "0", mutedBrush, 7, layout.Bottom - 12, 10, FontWeight.Medium);
    }

    private ChartLayout CreateLayout(double width, double height)
    {
        const double left = 31;
        const double right = 31;
        const double top = 22;
        const double bottom = 44;

        return new ChartLayout(
            left,
            top,
            Math.Max(left + 8, width - right),
            Math.Max(top + 8, height - bottom),
            Math.Max(1, HorizonYears * 12));
    }

    private static Point ToPoint(int monthIndex, decimal value, decimal maxValue, ChartLayout layout)
    {
        double x = layout.Left + layout.Width * Math.Clamp(monthIndex, 0, layout.TotalMonths) / layout.TotalMonths;
        double normalized = maxValue <= 0m ? 0d : Math.Clamp((double)(value / maxValue), 0d, 1d);
        double y = layout.Bottom - layout.Height * normalized;
        return new Point(x, y);
    }

    private static decimal ResolveMaxValue(IReadOnlyList<GoalForecastChartPoint> points, IReadOnlyList<GoalForecastMilestone> milestones)
    {
        decimal max = points.Count == 0 ? 0m : points.Max(point => point.Amount);
        if (milestones.Count > 0)
        {
            max = Math.Max(max, milestones.Max(item => item.TargetAmount));
        }

        if (max <= 0m)
        {
            return 1m;
        }

        return max * 1.12m;
    }

    private static int ResolveYearStep(int horizonYears)
    {
        return horizonYears switch
        {
            <= 10 => 1,
            <= 25 => 5,
            <= 40 => 10,
            _ => 10,
        };
    }

    private static string CompactLabel(string value, int maxLength)
    {
        string normalized = string.IsNullOrWhiteSpace(value) ? "Цель" : value.Trim();
        if (normalized.Length <= maxLength)
        {
            return normalized;
        }

        return normalized[..Math.Max(1, maxLength - 1)].TrimEnd() + "…";
    }

    private static string FormatMoney(decimal value, string currency)
    {
        string prefix = string.Equals(currency, "USD", StringComparison.OrdinalIgnoreCase) ? "$" : string.Empty;
        string suffix = string.Equals(currency, "USD", StringComparison.OrdinalIgnoreCase) ? string.Empty : $" {currency}";

        if (Math.Abs(value) >= 1_000_000m)
        {
            return $"{prefix}{value / 1_000_000m:0.##}M{suffix}";
        }

        if (Math.Abs(value) >= 1_000m)
        {
            return $"{prefix}{value / 1_000m:0.#}K{suffix}";
        }

        return $"{prefix}{value:0}{suffix}";
    }

    private static IBrush CreateProjectionAreaBrush(IBrush lineBrush)
    {
        Color baseColor = Colors.SteelBlue;
        if (lineBrush is ISolidColorBrush solidBrush)
        {
            baseColor = solidBrush.Color;
        }

        return new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0.5, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0.5, 1, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(Color.FromArgb(52, baseColor.R, baseColor.G, baseColor.B), 0),
                new GradientStop(Color.FromArgb(22, baseColor.R, baseColor.G, baseColor.B), 0.48),
                new GradientStop(Color.FromArgb(0, baseColor.R, baseColor.G, baseColor.B), 1),
            },
        };
    }

    private IBrush ResolveBrush(string key)
    {
        return ProximaChartTheme.ResolveBrush(this, key);
    }

    private static Size MeasureText(string text, double size)
    {
        FormattedText formatted = new(
            text,
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            Typeface.Default,
            size,
            Brushes.Black);
        return new Size(formatted.Width, formatted.Height);
    }

    private static void DrawText(DrawingContext context, string text, IBrush brush, double x, double y, double size, FontWeight weight)
    {
        FormattedText formatted = new(
            text,
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface(Typeface.Default.FontFamily, FontStyle.Normal, weight),
            size,
            brush);
        context.DrawText(formatted, new Point(x, y));
    }

    private static void DrawStateText(DrawingContext context, string text)
    {
        FormattedText formatted = new(
            text,
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            Typeface.Default,
            13,
            Brushes.Gray);
        context.DrawText(formatted, new Point(18, 18));
    }

    private readonly record struct VisualMilestoneDraft(int SourceIndex, GoalForecastMilestone Milestone, Point BasePoint);

    private readonly record struct VisualMilestone(GoalForecastMilestone Milestone, Point Position, ChartLayout Layout);

    private readonly record struct ChartLayout(double Left, double Top, double Right, double Bottom, int TotalMonths)
    {
        public double Width => Right - Left;

        public double Height => Bottom - Top;
    }
}
