using System.ComponentModel.DataAnnotations;

namespace BizMetrics.Api.Contracts;

public record RegisterRequest(
    [property: Required, EmailAddress] string Email,
    [property: Required, MinLength(8)] string Password,
    [property: Required] string FullName,
    [property: Required] string OrganizationName);

public record LoginRequest(
    [property: Required, EmailAddress] string Email,
    [property: Required] string Password);

public record RefreshRequest([property: Required] string RefreshToken);

public record AuthResponse(
    string AccessToken,
    string RefreshToken,
    Guid UserId,
    Guid? OrganizationId,
    string? Role);
