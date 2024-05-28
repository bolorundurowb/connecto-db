using connecto.server.Models.Req;
using connecto.server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace connecto.server.Controllers;

[Authorize]
[Route("api/auth")]
public class AuthController(AuthService authService) : BaseController
{
    [AllowAnonymous]
    [HttpPost, Route("login")]
    public async Task<IActionResult> Login([FromBody] LoginReq req)
    {
        var user = await authService.FindByUsername(req.Username);

        if (user is null || !user.CheckPasswordMatch(req.Password))
            return BadRequest("User account or password invalid");

        var res = await authService.Login(user);
        return Ok(res);
    }
    
    [AllowAnonymous]
    [HttpPost, Route("register")]
    public async Task<IActionResult> Register([FromBody] RegisterReq req)
    {
        var user = await authService.FindByUsername(req.Username);

        if (user is not null)
            return Conflict("User account already exists");

        user = await authService.Create(req);
        var res = await authService.Login(user);
        return Ok(res);
    }
}
