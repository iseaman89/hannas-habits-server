using System.Text.Json;
using FluentValidation;
using HannasHabits.Application.Common.Exceptions;
using HannasHabits.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace HannasHabits.WebApi.ExceptionHandling;

/// <summary>
/// Single place that translates exceptions into RFC 9457 <see cref="ProblemDetails"/> responses,
/// so controllers and handlers can simply throw.
/// </summary>
public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly IProblemDetailsService _problemDetailsService;
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(IProblemDetailsService problemDetailsService, ILogger<GlobalExceptionHandler> logger)
    {
        _problemDetailsService = problemDetailsService;
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var problem = CreateProblemDetails(exception);
        var status = problem.Status ?? StatusCodes.Status500InternalServerError;

        if (status >= StatusCodes.Status500InternalServerError)
            _logger.LogError(exception, "Unhandled exception while processing {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);
        else
            _logger.LogInformation("{ExceptionType} -> {StatusCode}: {Message}", exception.GetType().Name, status, exception.Message);

        httpContext.Response.StatusCode = status;

        // The status code is decided, so the exception counts as handled even if the client
        // does not accept JSON and no body can be written.
        await _problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = problem
        });

        return true;
    }

    private static ProblemDetails CreateProblemDetails(Exception exception) => exception switch
    {
        ValidationException validation => new ValidationProblemDetails(GroupErrors(validation))
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "One or more validation errors occurred."
        },
        DomainException => Problem(StatusCodes.Status400BadRequest, "A business rule was violated.", exception.Message),
        NotFoundException => Problem(StatusCodes.Status404NotFound, "The resource was not found.", exception.Message),
        ConflictException => Problem(StatusCodes.Status409Conflict, "The request conflicts with the current state.", exception.Message),
        ForbiddenException => Problem(StatusCodes.Status403Forbidden, "Access is forbidden.", exception.Message),
        UnauthorizedAccessException => Problem(StatusCodes.Status401Unauthorized, "Authentication is required.", detail: null),
        // Never leak internals of unexpected errors to the client.
        _ => Problem(StatusCodes.Status500InternalServerError, "An unexpected error occurred.", detail: null)
    };

    private static ProblemDetails Problem(int status, string title, string? detail) => new()
    {
        Status = status,
        Title = title,
        Detail = detail
    };

    // Keys are camelCased like the JSON properties of the request, so a client can map them to form fields.
    private static Dictionary<string, string[]> GroupErrors(ValidationException exception) => exception.Errors
        .GroupBy(failure => JsonNamingPolicy.CamelCase.ConvertName(failure.PropertyName))
        .ToDictionary(
            group => group.Key,
            group => group.Select(failure => failure.ErrorMessage).Distinct().ToArray());
}
