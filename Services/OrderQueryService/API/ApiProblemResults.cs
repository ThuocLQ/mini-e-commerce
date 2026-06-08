namespace OrderQueryService.API;

internal static class ApiProblemResults
{
    public static IResult NotFound(HttpContext context, string detail)
    {
        return Results.Problem(
            title: "Resource not found",
            detail: detail,
            statusCode: StatusCodes.Status404NotFound,
            type: "https://microshop.local/problems/not-found",
            instance: context.Request.Path);
    }

    public static IResult BadRequest(HttpContext context, string detail)
    {
        return Results.Problem(
            title: "Bad request",
            detail: detail,
            statusCode: StatusCodes.Status400BadRequest,
            type: "https://microshop.local/problems/bad-request",
            instance: context.Request.Path);
    }

    public static IResult ValidationProblem(
        HttpContext context,
        IDictionary<string, string[]> errors)
    {
        return Results.ValidationProblem(
            errors,
            title: "Validation failed",
            detail: "One or more validation errors occurred.",
            type: "https://microshop.local/problems/validation",
            instance: context.Request.Path);
    }

    public static IResult ServiceUnavailable(HttpContext context, string detail)
    {
        return Results.Problem(
            title: "Service unavailable",
            detail: detail,
            statusCode: StatusCodes.Status503ServiceUnavailable,
            type: "https://microshop.local/problems/service-unavailable",
            instance: context.Request.Path);
    }

    public static IResult InternalServerError(HttpContext context, string detail)
    {
        return Results.Problem(
            title: "Internal server error",
            detail: detail,
            statusCode: StatusCodes.Status500InternalServerError,
            type: "https://microshop.local/problems/internal-server-error",
            instance: context.Request.Path);
    }
}
