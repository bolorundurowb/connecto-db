namespace ConnectoDb.Server.Models.Req;

public class LoginReq
{
    public string Username { get; set; } = null!;

    public string Password { get; set; } = null!;
}