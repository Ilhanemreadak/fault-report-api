using FluentValidation;
using LotusCode.Application.Common;
using LotusCode.Application.Exceptions;
using System.Net;
using System.Text.Json;

namespace LotusCode.Api.Middleware
{
    /// <summary>
    /// Handles all unhandled exceptions globally and converts them into standardized API responses.
    /// </summary>
    public sealed class GlobalExceptionMiddleware
    {
        private readonly RequestDelegate next;
        private readonly ILogger<GlobalExceptionMiddleware> logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="GlobalExceptionMiddleware"/> class.
        /// </summary>
        public GlobalExceptionMiddleware(
            RequestDelegate next,
            ILogger<GlobalExceptionMiddleware> logger)
        {
            this.next = next;
            this.logger = logger;
        }

        /// <summary>
        /// Invokes the next middleware and catches unhandled exceptions for standardized API responses.
        /// </summary>
        /// <param name="context">The current HTTP context.</param>
        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await this.next(context);
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Unhandled exception occurred.");

                await HandleExceptionAsync(context, ex);
            }
        }

        private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            var statusCode = exception switch
            {
                ValidationException => (int)HttpStatusCode.BadRequest,
                UnauthorizedException => (int)HttpStatusCode.Unauthorized,
                ForbiddenException => (int)HttpStatusCode.Forbidden,
                NotFoundException => (int)HttpStatusCode.NotFound,
                BusinessRuleException => 422,
                StatusTransitionException => 422,
                _ => (int)HttpStatusCode.InternalServerError
            };

            var message = statusCode == (int)HttpStatusCode.InternalServerError
                ? "An unexpected error occurred."
                : exception.Message;

            var errors = exception switch
            {
                ValidationException validationException => validationException.Errors
                    .Select(x => x.ErrorMessage)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct()
                    .ToArray(),
                BusinessRuleException => [exception.Message],
                StatusTransitionException => [exception.Message],
                _ when statusCode == (int)HttpStatusCode.InternalServerError
                    => ["Please contact support if the issue persists."],
                _ => Array.Empty<string>()
            };

            var response = new ApiResponse<object>
            {
                Success = false,
                Data = null,
                Message = message,
                Errors = errors
            };

            context.Response.ContentType = "application/json";
            context.Response.StatusCode = statusCode;

            var json = JsonSerializer.Serialize(response);

            await context.Response.WriteAsync(json);
        }
    }
}
