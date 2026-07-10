namespace Xavissa.Backend.Security;

public class RlsContextMiddleware
{
    private readonly RequestDelegate _next;

    public RlsContextMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IRlsContextService rlsContext)
    {
        var applied = false;
        try
        {
            if (context.User.Identity?.IsAuthenticated == true)
            {
                await rlsContext.ApplyAsync(context.RequestAborted);
                applied = true;
            }

            await _next(context);
        }
        finally
        {
            if (applied)
                await rlsContext.ClearAsync(CancellationToken.None);
        }
    }
}
