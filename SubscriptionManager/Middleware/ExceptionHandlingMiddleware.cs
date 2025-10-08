using System;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using SubscriptionManager.Models.Domain;
using SubscriptionManager.Services.Interfaces;

namespace SubscriptionManager.Middleware
{
    public class ExceptionHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionHandlingMiddleware> _logger;
        private readonly IChannelProducer<LogMessage> _logProducer;

        public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger, IChannelProducer<LogMessage> logProducer)
        {
            _next = next;
            _logger = logger;
            _logProducer = logProducer;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (OperationCanceledException)
            {
                // Request aborted, no need to log as error
                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception for {Path}", context.Request.Path);
                _logProducer.TryWrite(new LogMessage
                {
                    Action = "Exception",
                    Message = $"{ex.GetType().Name}: {ex.Message}"
                });

                if (IsApiRequest(context))
                {
                    await WriteProblemDetailsAsync(context, ex);
                }
                else
                {
                    context.Response.Redirect("/Home/Error");
                }
            }
        }

        private static bool IsApiRequest(HttpContext ctx)
        {
            return ctx.Request.Path.StartsWithSegments("/api")
                   || ctx.Request.Headers.TryGetValue("Accept", out var accept)
                   && accept.Any(v => v.Contains("application/json", StringComparison.OrdinalIgnoreCase));
        }

        private static async Task WriteProblemDetailsAsync(HttpContext ctx, Exception ex)
        {
            ctx.Response.ContentType = "application/problem+json";
            ctx.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

            var problem = new
            {
                type = "about:blank",
                title = "An unexpected error occurred.",
                status = ctx.Response.StatusCode,
                detail = ex.Message,
                traceId = ctx.TraceIdentifier
            };
            var json = JsonSerializer.Serialize(problem, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            await ctx.Response.WriteAsync(json);
        }
    }
}