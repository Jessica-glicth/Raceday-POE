using Microsoft.AspNetCore.Mvc;

namespace RaceDay.Api.Extensions;

public static class ControllerExtensions
{
    public static ObjectResult StorageUnavailable(this ControllerBase controller)
        => controller.StatusCode(StatusCodes.Status503ServiceUnavailable, new ProblemDetails
        {
            Title = "Image storage is currently unavailable. Please try again later.",
            Status = StatusCodes.Status503ServiceUnavailable
        });
}
