using Microsoft.AspNetCore.Mvc;
using Xavissa.Database;

[ApiController]
[Route("api/[controller]")]
public class TestController : ControllerBase
{
    private readonly XavissaDbContext _db;
    private readonly IWebHostEnvironment _environment;
    public TestController(XavissaDbContext db, IWebHostEnvironment environment)
    {
        _db = db;
        _environment = environment;
    }

    [HttpGet("ping")]
    public IActionResult Ping()
    {
        if (!_environment.IsDevelopment())
            return NotFound();

        var count = _db.Products.Count();
        return Ok(new { message = "Connected to Supabase!", products = count });
    }
}
