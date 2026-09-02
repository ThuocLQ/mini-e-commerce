using Microsoft.AspNetCore.Http;

namespace MicroShop.ServiceDefaults.Diagnostics;

public static class ApiProblemResults
{
    public static IResult BadRequest(string detail, string code = "BAD_REQUEST") =>
        Create(StatusCodes.Status400BadRequest, "Bad request", detail, code);

    public static IResult Unauthorized(string detail = "Authentication is required.", string code = "UNAUTHORIZED") =>
        Create(StatusCodes.Status401Unauthorized, "Unauthorized", detail, code);

    public static IResult NotFound(string detail, string code = "NOT_FOUND") =>
        Create(StatusCodes.Status404NotFound, "Not found", detail, code);

    public static IResult Conflict(string detail, string code = "CONFLICT") =>
        Create(StatusCodes.Status409Conflict, "Conflict", detail, code);

    public static IResult ServiceUnavailable(string detail, string code = "SERVICE_UNAVAILABLE") =>
        Create(StatusCodes.Status503ServiceUnavailable, "Service unavailable", detail, code);

    private static IResult Create(int statusCode, string title, string detail, string code) =>
        Results.Problem(
            statusCode: statusCode,
            title: title,
            type: $"https://microshop.dev/problems/{code.ToLowerInvariant().Replace('_', '-')}",
            detail: detail,
            extensions: new Dictionary<string, object?>
            {
                ["code"] = code,
                // Retained during the unversioned API migration for existing BFF consumers.
                ["message"] = detail,
                ["error"] = detail
            });
}