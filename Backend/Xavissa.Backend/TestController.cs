using Microsoft.AspNetCore.Mvc;
using Xavissa.Database;

[ApiController]
[Route("api/[controller]")]
public class TestController : ControllerBase
{
    private readonly XavissaDbContext _db;
    public TestController(XavissaDbContext db) => _db = db;

    [HttpGet("ping")]
    public IActionResult Ping()
    {
        var count = _db.Products.Count();
        return Ok(new { message = "Connected to Supabase!", products = count });
    }
}
