using System.ComponentModel.DataAnnotations;
using BizMetrics.Infrastructure.Analytics;

namespace BizMetrics.Api.Contracts;

public record CreateDashboardRequest([Required, MinLength(1)] string Name);

public record DashboardSummaryDto(Guid Id, string Name, int WidgetCount, DateTime CreatedAt);

public record AddWidgetRequest(
    [Required] Guid DatasetId,
    [Required, MinLength(1)] string Title,
    [Required] string ChartType,
    [Required] AnalyticsQuery Query);

public record WidgetDto(
    Guid Id, Guid DatasetId, string Title, string ChartType, AnalyticsQuery Query, int Position);

/// <summary>A widget plus its freshly computed data, for rendering a dashboard.</summary>
public record WidgetDataDto(
    Guid Id, Guid DatasetId, string Title, string ChartType, int Position,
    string Label, IReadOnlyList<AnalyticsPoint> Points, IReadOnlyList<string> Insights);

public record DashboardDataDto(Guid Id, string Name, IReadOnlyList<WidgetDataDto> Widgets);
