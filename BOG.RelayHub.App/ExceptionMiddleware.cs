using System.Net;

namespace BOG.RelayHub.App
{
    /// <summary>
    /// Ensures that a payload too large exception sends 413--not a generic 500.
    /// </summary>
    public class ExceptionMiddleware
    {
        private readonly RequestDelegate _next;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="next"></param>
        public ExceptionMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        /// <summary>
        /// Invocation
        /// </summary>
        /// <param name="httpContext"></param>
        /// <returns></returns>
        public async Task InvokeAsync(HttpContext httpContext)
        {
            try
            {
                await _next(httpContext);
            }
            catch (Exception ex)
            {
                HandleExceptionAsync(httpContext, ex);
            }
        }

        private static void HandleExceptionAsync(HttpContext context, Exception exception)
        {
            if (exception is Microsoft.AspNetCore.Http.BadHttpRequestException)
            {
                if (context.Request.Body.Length > (1024L * 1024L * 50L)) // 50M hard limit for body size
                {
                    context.Response.StatusCode = (int)HttpStatusCode.RequestEntityTooLarge;
                }
                else
                {
                    context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                }
            }
        }
    }
}
