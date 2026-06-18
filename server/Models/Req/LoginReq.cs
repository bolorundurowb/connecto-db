using System.ComponentModel.DataAnnotations;

namespace ConnectoDb.Server.Models.Req;

public class LoginReq
{
    [Required]
    public string Username { get; set; } = null!;

    [Required]
    public string Password { get; set; } = null!;
}