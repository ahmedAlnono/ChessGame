using System.Net;
using System.Text.Json;
using ChessAPI.Models.DTOs;

namespace ChessAPI.Middleware;

public class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    public ExceptionMiddleware(
        RequestDelegate next, 
        ILogger<ExceptionMiddleware> logger,
        IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        _logger.LogError(exception, "An unhandled exception occurred");

        var response = context.Response;
        response.ContentType = "application/json";

        var errorResponse = new ErrorMessageDto
        {
            Code = GetErrorCode(exception),
            Message = GetErrorMessage(exception),
            Details = _environment.IsDevelopment() ? new
            {
                exception.StackTrace,
                exception.Source,
                InnerException = exception.InnerException?.Message
            } : null
        };

        response.StatusCode = (int)GetStatusCode(exception);

        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        var result = JsonSerializer.Serialize(errorResponse, jsonOptions);
        await response.WriteAsync(result);
    }

    private static HttpStatusCode GetStatusCode(Exception exception) => exception switch
    {
        UnauthorizedAccessException => HttpStatusCode.Unauthorized,
        ArgumentException => HttpStatusCode.BadRequest,
        InvalidOperationException => HttpStatusCode.BadRequest,
        KeyNotFoundException => HttpStatusCode.NotFound,
        TimeoutException => HttpStatusCode.RequestTimeout,
        NotImplementedException => HttpStatusCode.NotImplemented,
        _ => HttpStatusCode.InternalServerError
    };

    private static string GetErrorCode(Exception exception) => exception switch
    {
        UnauthorizedAccessException => "UNAUTHORIZED",
        ArgumentException => "INVALID_ARGUMENT",
        InvalidOperationException => "INVALID_OPERATION",
        KeyNotFoundException => "NOT_FOUND",
        TimeoutException => "TIMEOUT",
        NotImplementedException => "NOT_IMPLEMENTED",
        _ => "INTERNAL_SERVER_ERROR"
    };

    private static string GetErrorMessage(Exception exception) => exception switch
    {
        UnauthorizedAccessException => "You are not authorized to perform this action.",
        ArgumentException => exception.Message,
        InvalidOperationException => exception.Message,
        KeyNotFoundException => "The requested resource was not found.",
        TimeoutException => "The operation timed out.",
        NotImplementedException => "This feature is not yet implemented.",
        _ => "An unexpected error occurred. Please try again later."
    };
}