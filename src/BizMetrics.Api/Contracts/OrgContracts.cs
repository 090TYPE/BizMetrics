using System.ComponentModel.DataAnnotations;
using BizMetrics.Domain.Entities;

namespace BizMetrics.Api.Contracts;

public record OrganizationDto(
    Guid Id,
    string Name,
    string Slug,
    string SubscriptionStatus,
    DateTime? TrialEndsAt,
    string? PlanName);

public record UpdateOrganizationRequest([Required, MinLength(1)] string Name);

public record MemberDto(
    Guid UserId,
    string Email,
    string FullName,
    string Role,
    string Status,
    DateTime JoinedAt);

public record ChangeRoleRequest([Required] OrgRole Role);

/// <summary>One organization the current user belongs to — feeds the org switcher.</summary>
public record MyOrganizationDto(Guid Id, string Name, string Slug, string Role);
