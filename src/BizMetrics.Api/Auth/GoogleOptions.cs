namespace BizMetrics.Api.Auth;

public class GoogleOptions
{
    public const string SectionName = "Google";

    public string ClientId { get; init; } = string.Empty;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ClientId);
}
