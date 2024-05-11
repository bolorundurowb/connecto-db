using connecto.server.Services;
using Microsoft.AspNetCore.Mvc;

namespace connecto.server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TablesController : ControllerBase
{
    private readonly TableService _tableService = new(Config.DbName);

    [HttpGet("")]
    public async Task<IActionResult> GetAll()
    {
        var tables = await _tableService.GetAll();
        return Ok(tables);
    }
}
