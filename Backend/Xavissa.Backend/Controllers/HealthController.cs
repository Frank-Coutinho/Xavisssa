using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xavissa.Database;

namespace Xavissa.Backend.Controllers
{
    [ApiController]
    [Route("health")]
    public class HealthController : ControllerBase
    {
        private readonly XavissaDbContext _db;

        public HealthController(XavissaDbContext db)
        {
            _db = db;
        }

        [AllowAnonymous]
        [HttpGet("connectivity")]
        public async Task<IActionResult> GetConnectivityAsync()
        {
            try
            {
                var canConnect = await _db.Database.CanConnectAsync(HttpContext.RequestAborted);
                if (!canConnect)
                {
                    return StatusCode(StatusCodes.Status503ServiceUnavailable, new
                    {
                        online = false,
                        message = "The sync database is currently unavailable.",
                    });
                }

                return Ok(new
                {
                    online = true,
                    message = "The sync database is reachable.",
                });
            }
            catch
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new
                {
                    online = false,
                    message = "The sync database is currently unavailable.",
                });
            }
        }
    }
}
