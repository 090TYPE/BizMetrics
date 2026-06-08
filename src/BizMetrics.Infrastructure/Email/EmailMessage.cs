namespace BizMetrics.Infrastructure.Email;

public record EmailMessage(string To, string Subject, string Body);
