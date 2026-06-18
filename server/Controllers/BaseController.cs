using System.Net;
using ConnectoDb.Server.Models.Res;
using Microsoft.AspNetCore.Mvc;

namespace ConnectoDb.Server.Controllers;

[ApiController]
public abstract class BaseController : ControllerBase
{
    protected OkObjectResult Ok(string message) =>
        Ok(new GenericRes(message));

    protected BadRequestObjectResult BadRequest(string message) =>
        BadRequest(new GenericRes(message));

    protected NotFoundObjectResult NotFound(string message) =>
        NotFound(new GenericRes(message));

    protected ObjectResult Conflict(string message) =>
        StatusCode((int)HttpStatusCode.Conflict, new GenericRes(message));

    protected ObjectResult ExpectationFailed(string message) =>
        StatusCode((int)HttpStatusCode.ExpectationFailed, new GenericRes(message));

    protected CreatedResult Created<T>(T data) => Created(string.Empty, data);
}
