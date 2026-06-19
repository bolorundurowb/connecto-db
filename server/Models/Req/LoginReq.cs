using System.ComponentModel.DataAnnotations;

namespace ConnectoDb.Server.Models.Req;

public class LoginReq
{
    [Required, MinLength(3), MaxLength(256)]
    public string Username { get; set; } = null!;

    [Required, MinLength(8), MaxLength(256)]
    public string Password { get; set; } = null!;
}