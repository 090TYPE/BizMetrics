namespace BizMetrics.Domain.Entities;

/// <summary>A tenant-scoped collection of saved analytics widgets.</summary>
public class Dashboard : ITenantEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }

    public string Name { get; set; } = string.Empty;
    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Widget> Widgets { get; set; } = new List<Widget>();
}

/// <summary>
/// A saved chart: an analytics query (stored as JSON) against a dataset, plus how
/// to render it. The query is re-run on demand so widgets reflect current data.
/// </summary>
public class Widget : ITenantEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }

    public Guid DashboardId { get; set; }
    public Dashboard Dashboard { get; set; } = null!;

    public Guid DatasetId { get; set; }

    public string Title { get; set; } = string.Empty;
    public string ChartType { get; set; } = "bar"; // bar | line | pie
    public string QueryJson { get; set; } = "{}";
    public int Position { get; set; }
}
