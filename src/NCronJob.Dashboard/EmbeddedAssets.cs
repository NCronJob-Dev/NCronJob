using Microsoft.AspNetCore.Http;

namespace NCronJob.Dashboard;

internal static class EmbeddedAssets
{
    private static readonly Dictionary<PathString, (string Resource, string ContentType)> AssetsByPath = new()
    {
        ["/_framework/blazor.web.js"] = ("NCronJob.Dashboard.blazor.web.js", "text/javascript"),
        ["/logo.png"] = ("NCronJob.Dashboard.logo.png", "image/png"),
    };

    public static async Task ServeAsync(HttpContext context, RequestDelegate next)
    {
        if (!AssetsByPath.TryGetValue(context.Request.Path, out var asset)
            || typeof(EmbeddedAssets).Assembly.GetManifestResourceStream(asset.Resource) is not { } content)
        {
            await next(context);
            return;
        }

        await using (content)
        {
            context.Response.ContentType = asset.ContentType;
            context.Response.Headers.CacheControl = "public, max-age=3600";
            await content.CopyToAsync(context.Response.Body, context.RequestAborted);
        }
    }
}
