using Microsoft.AspNetCore.Mvc;

namespace PhotoLibApi.Controllers
{
    /// <summary>
    /// Provides a simple endpoint for checking whether the server is running.
    /// Useful for monitoring, diagnostics, and health checks.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class HealthController : ControllerBase
    {
        /// <summary>
        /// Returns a basic health status and the current server time (UTC).
        /// </summary>
        /// <remarks>
        /// This endpoint does not require authentication and is intended
        /// for external tools, uptime checkers, and quick manual testing.
        /// </remarks>
        /// <response code="200">
        /// An object containing:
        /// - <c>status</c>: always "ok"
        /// - <c>time</c>: current UTC timestamp
        /// </response>
        [HttpGet]
        public IActionResult Get() =>
            Ok(new { status = "ok", time = System.DateTime.UtcNow });
    }
}
