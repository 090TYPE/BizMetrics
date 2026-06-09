using System.ComponentModel.DataAnnotations;

namespace BizMetrics.Api.Contracts;

// Validation attributes target the constructor parameters (no `property:` prefix):
// ASP.NET Core model validation reads them from the parameter on positional records.
public record RegisterRequest(
    [Required, EmailAddress] string Email,
    [Required, MinLength(8)] string Password,
    [Required] string FullName,
    [Required] string OrganizationName);

public record LoginRequest(
    [Required, EmailAddress] string Email,
    [Required] string Password);

public record RefreshRequest([Required] string RefreshToken);

public record GoogleLoginRequest([Required] string IdToken);

public record AuthResponse(
    string AccessToken,
    string RefreshToken,
    Guid UserId,
    Guid? OrganizationId,
    string? Role);
