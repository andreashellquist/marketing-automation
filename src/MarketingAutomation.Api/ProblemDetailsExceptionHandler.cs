using FluentValidation;
using MarketingAutomation.SharedKernel.Application;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace MarketingAutomation.Api;

/// <summary>Maps domain and validation exceptions to RFC 7807 ProblemDetails responses.</summary>
public sealed class ProblemDetailsExceptionHandler(IProblemDetailsService problemDetailsService)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        var (status, title, errors) = exception switch
        {
            ValidationException ve => (
                StatusCodes.Status400BadRequest,
                "Validation failed",
                ve.Errors.GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray())),
            NotFoundException => (StatusCodes.Status404NotFound, exception.Message, null),
            DomainConflictException => (StatusCodes.Status409Conflict, exception.Message, null),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred.", null),
        };

        context.Response.StatusCode = status;

        var problemDetails = new ProblemDetails { Status = status, Title = title };
        if (errors is not null) problemDetails.Extensions["errors"] = errors;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            Exception = exception,
            ProblemDetails = problemDetails,
        });
    }
}
