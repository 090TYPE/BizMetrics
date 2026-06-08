using System.ComponentModel.DataAnnotations;
using BizMetrics.Domain.Entities;

namespace BizMetrics.Api.Contracts;

public record CreateInvitationRequest(
    [Required, EmailAddress] string Email,
    [Required] OrgRole Role);

public record InvitationDto(
    Guid Id,
    string Email,
    string Role,
    string Status,
    DateTime ExpiresAt,
    DateTime CreatedAt);

public record AcceptInvitationRequest([Required] string Token);

/// <summary>Anonymous preview shown on the accept page before the user signs in.</summary>
public record InvitationPreviewDto(
    string OrganizationName,
    string Email,
    string Role,
    bool Redeemable);
