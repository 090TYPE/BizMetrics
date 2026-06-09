using BizMetrics.Api.Auth;
using BizMetrics.Domain.Entities;
using BizMetrics.Infrastructure.Persistence;
using BizMetrics.Infrastructure.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BizMetrics.Api.Controllers;

/// <summary>
/// Read-only view of the organization's audit trail.
/// </summary>
[ApiController]
[Authorize(Policy = Policies.RequireAdmin)]
[Route("api/audit")]
public class AuditController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;

    public AuditController(AppDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public record AuditEntryDto(
        Guid Id,
        Guid? UserId,
        string Action,
        string EntityType,
        string? EntityId,
        string? Details,
        string? IpAddress,
        DateTime CreatedAt);

    /// <summary>
    /// Returns a paged, reverse-chronological audit trail for the current organization.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<object>> List(
        int skip = 0,
        int take = 50,
        string? action = null,
        string? entityType = null)
    {
        take = Math.Clamp(take, 1, 200);

        var orgId = _tenant.OrganizationId;
        if (orgId is null) return Forbid();

        var query = _db.AuditEntries
            .Where(e => e.OrganizationId == orgId);

        if (!string.IsNullOrWhiteSpace(action))
            query = query.Where(e => e.Action == action);

        if (!string.IsNullOrWhiteSpace(entityType))
            query = query.Where(e => e.EntityType == entityType);

        var total = await query.CountAsync();

        var entries = await query
            .OrderByDescending(e => e.CreatedAt)
            .Skip(skip)
            .Take(take)
            .Select(e => new AuditEntryDto(
                e.Id, e.UserId, e.Action, e.EntityType,
                e.EntityId, e.Details, e.IpAddress, e.CreatedAt))
            .ToListAsync();

        return Ok(new { total, entries });
    }
}
