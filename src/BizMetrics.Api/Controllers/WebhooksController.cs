using System.Text;
using BizMetrics.Infrastructure.Billing;
using Microsoft.AspNetCore.Mvc;

namespace BizMetrics.Api.Controllers;

/// <summary>
/// Receives and processes Stripe webhook events.
/// No authentication — Stripe authenticates via the Stripe-Signature header instead.
/// </summary>
[ApiController]
[Route("api/webhooks")]
public class WebhooksController : ControllerBase
{
    private readonly BillingService _billing;
    private readonly ILogger<WebhooksController> _logger;

    public WebhooksController(BillingService billing, ILogger<WebhooksController> logger)
    {
        _billing = billing;
        _logger = logger;
    }

    /// <summary>
    /// Handles Stripe webhook delivery. Verifies the Stripe-Signature header,
    /// processes the event once (idempotent), and returns 200 to prevent retries.
    /// </summary>
    [HttpPost("stripe")]
    public async Task<IActionResult> Stripe()
    {
        // Read the raw body before any model-binding touches it.
        string rawBody;
        using (var reader = new StreamReader(HttpContext.Request.Body, Encoding.UTF8, leaveOpen: true))
            rawBody = await reader.ReadToEndAsync();

        var signature = HttpContext.Request.Headers["Stripe-Signature"].ToString();
        if (string.IsNullOrEmpty(signature))
            return BadRequest(new { error = "Missing Stripe-Signature header." });

        try
        {
            var processed = await _billing.HandleWebhookAsync(rawBody, signature);
            if (processed)
            {
                _logger.LogInformation("Stripe webhook processed.");
                return Ok();
            }
            else
            {
                _logger.LogInformation("Stripe webhook was a duplicate — skipped.");
                return Ok(new { skipped = true });
            }
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not configured"))
        {
            // Stripe keys not set — acknowledge the event so Stripe doesn't retry.
            _logger.LogWarning("Stripe webhook received but billing is not configured.");
            return Ok();
        }
        catch (InvalidOperationException ex)
        {
            // Signature mismatch or similar — tell Stripe to NOT retry (bad payload).
            _logger.LogWarning(ex, "Stripe webhook rejected: {Reason}", ex.Message);
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            // Internal error — 500 causes Stripe to retry later.
            _logger.LogError(ex, "Error processing Stripe webhook.");
            return StatusCode(500);
        }
    }
}
