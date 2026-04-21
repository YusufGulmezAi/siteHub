using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using SiteHub.Contracts.Common;
using SiteHub.Domain.Common;

namespace SiteHub.Infrastructure.Middleware;

/// <summary>
/// Global exception handler middleware.
///
/// Tüm unhandled exception'ları yakalar, tip'ine göre uygun HTTP status
/// koduyla ApiResponse&lt;object&gt; formatında döner.
///
/// Kullanım:
/// <code>
/// app.UseMiddleware&lt;GlobalExceptionHandlerMiddleware&gt;();
/// </code>
///
/// UseExceptionHandler yerine custom middleware tercih etme sebebi:
/// - Domain exception'larını özel mapleyebilmek
/// - ApiError.Errors gibi structured data döndürebilmek
/// - TraceId ile log correlation sağlamak
/// </summary>
public sealed class GlobalExceptionHandlerMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlerMiddleware> _logger;

    public GlobalExceptionHandlerMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionHandlerMiddleware> logger)
    {
        _next = next;
        _logger = logger;
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
        var traceId = context.TraceIdentifier;

        var (statusCode, apiError) = exception switch
        {
            ValidationException ve => (
                StatusCodes.Status400BadRequest,
                new ApiError
                {
                    Code = ve.Code,
                    Message = ve.Message,
                    Errors = ve.Errors.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
                    TraceId = traceId
                }),

            NotFoundException nfe => (
                StatusCodes.Status404NotFound,
                new ApiError
                {
                    Code = nfe.Code,
                    Message = nfe.Message,
                    TraceId = traceId
                }),

            ForbiddenException fe => (
                StatusCodes.Status403Forbidden,
                new ApiError
                {
                    Code = fe.Code,
                    Message = fe.Message,
                    TraceId = traceId
                }),

            InvalidStateException ise => (
                StatusCodes.Status409Conflict,
                new ApiError
                {
                    Code = ise.Code,
                    Message = ise.Message,
                    TraceId = traceId
                }),

            BusinessRuleViolationException brv => (
                StatusCodes.Status422UnprocessableEntity,
                new ApiError
                {
                    Code = brv.Code,
                    Message = brv.Message,
                    TraceId = traceId
                }),

            DomainException de => (
                StatusCodes.Status400BadRequest,
                new ApiError
                {
                    Code = de.Code,
                    Message = de.Message,
                    TraceId = traceId
                }),

            // Bilinmeyen exception'lar — log ve generic hata
            _ => (
                StatusCodes.Status500InternalServerError,
                new ApiError
                {
                    Code = ApiErrorCodes.InternalError,
                    Message = "Beklenmeyen bir hata oluştu. Lütfen destek ekibine başvurun.",
                    TraceId = traceId
                })
        };

        // Domain exception'ları warning (iş kuralı ihlali beklenir).
        // Diğer exception'lar error (gerçek hata).
        if (exception is DomainException)
        {
            _logger.LogWarning(exception,
                "Domain exception yakalandı: {Code} — TraceId: {TraceId}",
                apiError.Code, traceId);
        }
        else
        {
            _logger.LogError(exception,
                "Unhandled exception — TraceId: {TraceId}", traceId);
        }

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";

        var response = new ApiResponse<object?>
        {
            Success = false,
            Data = null,
            Error = apiError
        };

        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await context.Response.WriteAsync(json);
    }
}

/// <summary>
/// Middleware extension for easy registration.
/// </summary>
public static class GlobalExceptionHandlerMiddlewareExtensions
{
    public static IApplicationBuilder UseGlobalExceptionHandler(
        this IApplicationBuilder app)
    {
        return app.UseMiddleware<GlobalExceptionHandlerMiddleware>();
    }
}
