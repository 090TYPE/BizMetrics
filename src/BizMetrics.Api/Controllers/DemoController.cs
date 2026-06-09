using BizMetrics.Api.Demo;
using BizMetrics.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;

namespace BizMetrics.Api.Controllers;

/// <summary>
/// Demo seeding endpoint.
/// Available in Development and when DEMO_SEED env var is "true".
/// Returns the demo credentials so callers know how to log in.
/// </summary>
[ApiController]
[Route("api/demo")]
public class DemoController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;
    private readonly IWebHostEnvironment _env;

    public DemoController(AppDbContext db, IConfiguration config, IWebHostEnvironment env)
    {
        _db = db;
        _config = config;
        _env = env;
    }

    /// <summary>
    /// Seeds demo data and returns the demo login credentials.
    /// Idempotent — safe to call repeatedly.
    /// </summary>
    [HttpPost("seed")]
    public async Task<ActionResult<object>> Seed()
    {
        var demoEnabled = _env.IsDevelopment()
            || string.Equals(_config["DEMO_SEED"], "true", StringComparison.OrdinalIgnoreCase);

        if (!demoEnabled)
            return NotFound();

        await DemoSeeder.SeedAsync(_db);

        return Ok(new
        {
            message = "Demo data seeded.",
            email = DemoSeeder.DemoEmail,
            password = DemoSeeder.DemoPassword
        });
    }
}
