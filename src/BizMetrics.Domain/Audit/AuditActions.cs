namespace BizMetrics.Domain.Audit;

/// <summary>
/// Canonical action keys used in the audit log.
/// Convention: {domain}.{verb_past_tense}
/// </summary>
public static class AuditActions
{
    // Auth
    public const string UserRegistered = "user.registered";
    public const string UserLoggedIn = "user.login";
    public const string UserLoginFailed = "user.login_failed";
    public const string UserLoggedInGoogle = "user.login_google";
    public const string UserRegisteredViaGoogle = "user.registered_google";

    // Datasets
    public const string DatasetUploaded = "dataset.uploaded";
    public const string DatasetDeleted = "dataset.deleted";

    // Members
    public const string MemberInvited = "member.invited";
    public const string MemberJoined = "member.joined";
    public const string InvitationRevoked = "invitation.revoked";
    public const string MemberRoleChanged = "member.role_changed";
    public const string MemberRemoved = "member.removed";

    // Organization
    public const string OrgRenamed = "org.renamed";

    // Billing
    public const string CheckoutStarted = "billing.checkout_started";
    public const string PortalOpened = "billing.portal_opened";
    public const string SubscriptionActivated = "billing.subscription_activated";
    public const string SubscriptionUpdated = "billing.subscription_updated";
    public const string SubscriptionCanceled = "billing.subscription_canceled";
    public const string PaymentFailed = "billing.payment_failed";
}
