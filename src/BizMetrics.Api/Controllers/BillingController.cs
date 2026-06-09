using System.Security.Claims;
using BizMetrics.Api.Auth;
using BizMetrics.Api.Contracts;
using BizMetrics.Domain.Entities;
using BizMetrics.Infrastructure.Billing;
using BizMetrics.Infrastructure.Persistence;
using BizMetrics.Infrastructure.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BizMetrics.Api.Controllers;

/// <summary>
/// Billing: current status, Stripe Checkout, and Customer Portal.
/// </summary>
[ApiController]
[Authorize]
[Route("api/billing")]
public class BillingController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly BillingService _billing;
    private readonly ITenantContext _tenant;
    private readonly StripeOptions _stripe;

    public BillingController(
        AppDbContext db,
        BillingService billing,
        ITenantContext tenant,
        IOptions<StripeOptions> stripe)
    {
        _db = db;
        _billing = billing;
        _tenant = tenant;
        _stripe = stripe.Value;
    }

    /// <summary>Returns the current plan, subscription status, trial countdown, and usage.</summary>
    [HttpGet]
    public async Task<ActionResult<BillingStatusResponse>> Status()
    {
        var orgId = _tenant.OrganizationId;
        if (orgId is null) return Forbid();

        var org = await _db.Organizations
            .Include(o => o.Plan)
            .FirstOrDefaultAsync(o => o.Id == orgId);
        if (org is null) return NotFound();

        // Fallback plan when the org doesn't have one assigned yet (free trial)
        var plan = org.Plan ?? new Plan
        {
            Name = "Free", MaxUsers = 2, MaxDatasets = 3,
            MaxRows = 10_000, PriceMonthly = 0
        };

        var datasetsUsed = await _db.Datasets.CountAsync();
        var membersUsed = await _db.Memberships
            .CountAsync(m => m.OrganizationId == orgId && m.Status == MembershipStatus.Active);

        int? trialDaysLeft = null;
        if (org.SubscriptionStatus == SubscriptionStatus.Trialing && org.TrialEndsAt.HasValue)
            trialDaysLeft = Math.Max(0, (int)(org.TrialEndsAt.Value - DateTime.UtcNow).TotalDays);

        return Ok(new BillingStatusResponse(
            plan.Name,
            plan.PriceMonthly,
            org.SubscriptionStatus.ToString(),
            org.TrialEndsAt,
            trialDaysLeft,
            !string.IsNullOrEmpty(org.StripeCustomerId),
            _stripe.IsConfigured,
            new BillingUsage(datasetsUsed, plan.MaxDatasets, membersUsed, plan.MaxUsers)
        ));
    }

    /// <summary>Creates a Stripe Checkout session and returns the redirect URL.</summary>
    [HttpPost("checkout")]
    public async Task<ActionResult<object>> Checkout(CheckoutRequest req)
    {
        var orgId = _tenant.OrganizationId;
        if (orgId is null) return Forbid();

        var userEmail = User.FindFirstValue(ClaimTypes.Email) ?? string.Empty;

        try
        {
            var url = await _billing.CreateCheckoutSessionAsync(
                orgId.Value, req.Plan, userEmail, req.SuccessUrl, req.CancelUrl);
            return Ok(new { url });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not configured"))
        {
            return StatusCode(503, new { error = "Billing is not configured on this instance." });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Creates a Stripe Customer Portal session and returns the redirect URL.</summary>
    [HttpPost("portal")]
    public async Task<ActionResult<object>> Portal(PortalRequest req)
    {
        var orgId = _tenant.OrganizationId;
        if (orgId is null) return Forbid();

        try
        {
            var url = await _billing.CreatePortalSessionAsync(orgId.Value, req.ReturnUrl);
            return Ok(new { url });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not configured"))
        {
            return StatusCode(503, new { error = "Billing is not configured on this instance." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
