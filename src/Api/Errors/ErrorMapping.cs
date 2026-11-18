using Application.Common;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Api.Errors;

/// <summary>The one place that turns an <see cref="AppError"/> into an RFC 9457 problem response.</summary>
internal static class ErrorMapping
{
    public static ProblemHttpResult ToProblem(this AppError error)
    {
        var status = error.Kind switch
        {
            ErrorKind.Validation => StatusCodes.Status400BadRequest,
            ErrorKind.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorKind.Forbidden => StatusCodes.Status403Forbidden,
            ErrorKind.NotFound => StatusCodes.Status404NotFound,
            ErrorKind.Conflict => StatusCodes.Status409Conflict,
            ErrorKind.Gone => StatusCodes.Status410Gone,
            _ => StatusCodes.Status500InternalServerError,
        };

        ProblemDetails problem = error.Errors is { } errors
            ? new HttpValidationProblemDetails(errors.ToDictionary()) { Status = status }
            : new ProblemDetails { Status = status, Detail = error.Message };
        if (error.Code is not null)
            problem.Extensions["code"] = error.Code;
        return TypedResults.Problem(problem);
    }
}
