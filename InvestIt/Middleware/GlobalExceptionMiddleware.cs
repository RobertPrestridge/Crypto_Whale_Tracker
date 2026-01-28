using System.Net;

namespace InvestIt.Middleware;

public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
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
            _logger.LogError(ex, "An unhandled exception occurred");
            await HandleExceptionAsync(context, ex);
        }
    }

    private static Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "text/html";
        context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

        var errorMessage = $@"
<!DOCTYPE html>
<html>
<head>
    <title>Error</title>
    <style>
        body {{ font-family: Arial, sans-serif; margin: 40px; }}
        .error-container {{ background-color: #f8d7da; border: 1px solid #f5c6cb; padding: 20px; border-radius: 5px; }}
        h1 {{ color: #721c24; }}
        p {{ color: #721c24; }}
    </style>
</head>
<body>
    <div class='error-container'>
        <h1>An error occurred</h1>
        <p>We're sorry, but something went wrong. The error has been logged and we'll look into it.</p>
        <p><a href='/'>Return to home page</a></p>
    </div>
</body>
</html>";

        return context.Response.WriteAsync(errorMessage);
    }
}
