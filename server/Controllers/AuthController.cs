using connecto.server.Models.Req;
using connecto.server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace connecto.server.Controllers;

[Authorize]
[ApiController]
[Route("api/auth")]
public class AuthController(AuthService authService) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost, Route("login")]
    public async Task<IActionResult> Login([FromBody] LoginReq req)
    {
        
    }
}