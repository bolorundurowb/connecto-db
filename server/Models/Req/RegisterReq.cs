using System.ComponentModel.DataAnnotations;

namespace ConnectoDb.Server.Models.Req;

public class RegisterReq
{
    [Required]
    public string Username { get; set; } = null!;

    [Required]
    public string Password { get; set; } = null!;

    public string? FirstName { get; set; }

    public string? LastName { get; set; }
}