using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NCronJob.Dashboard;
using Shouldly;

namespace NCronJob.Tests.Dashboard;

public sealed class DashboardHostingTests
{
    private const string UserHeader = "X-Test-User";

    [Fact]
    public async Task ServesPagesAndAssetsUnderACustomPrefix()
    {
        await using var app = await StartAsync(app => app.MapNCronJobDashboard("/admin/jobs"));
        using var client = CreateClient(app);

        (await client.GetStringAsync(new Uri("admin/jobs/", UriKind.Relative), TestContext.Current.CancellationToken)).ShouldContain("NCronJob Dashboard");

        (await GetStatusAsync(client, "admin/jobs/_framework/blazor.web.js")).ShouldBe(HttpStatusCode.OK);
        (await GetStatusAsync(client, "admin/jobs/logo.png")).ShouldBe(HttpStatusCode.OK);
        (await NegotiateAsync(client, "admin/jobs")).ShouldBe(HttpStatusCode.OK);
        (await GetStatusAsync(client, "ncronjob/")).ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AuthorizationProtectsPagesAndTheBlazorHub()
    {
        await using var app = await StartAsync(
            app => app.MapNCronJobDashboard("/ncronjob", endpoints => endpoints.RequireAuthorization()),
            services => services.AddAuthentication(UserHeader).AddScheme<AuthenticationSchemeOptions, HeaderAuthenticationHandler>(UserHeader, null));
        using var anonymous = CreateClient(app);
        using var authenticated = CreateClient(app);
        authenticated.DefaultRequestHeaders.Add(UserHeader, "admin");

        (await GetStatusAsync(anonymous, "ncronjob/")).ShouldBe(HttpStatusCode.Unauthorized);
        (await NegotiateAsync(anonymous, "ncronjob")).ShouldBe(HttpStatusCode.Unauthorized);
        (await GetStatusAsync(authenticated, "ncronjob/")).ShouldBe(HttpStatusCode.OK);
        (await NegotiateAsync(authenticated, "ncronjob")).ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task DashboardStaysIsolatedFromTheHostsOwnBlazorApp()
    {
        await using var app = await StartAsync(app =>
        {
            app.MapNCronJobDashboard("/ncronjob");
            app.UseAntiforgery();
            app.MapRazorComponents<HostHomePage>().AddInteractiveServerRenderMode();
        });
        using var client = CreateClient(app);

        (await client.GetStringAsync(new Uri("/", UriKind.Relative), TestContext.Current.CancellationToken)).ShouldContain("host home");
        (await client.GetStringAsync(new Uri("ncronjob/", UriKind.Relative), TestContext.Current.CancellationToken)).ShouldContain("NCronJob Dashboard");
        (await NegotiateAsync(client, "")).ShouldBe(HttpStatusCode.OK);
        (await NegotiateAsync(client, "ncronjob")).ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task LocalPortGuardKeepsTheDashboardOffOtherListenersEvenWithASpoofedHostHeader()
    {
        var adminPort = 0;
        await using var app = await StartAsync(
            app => app.MapWhen(
                context => context.Connection.LocalPort == adminPort,
                admin => admin.MapNCronJobDashboard("/ncronjob")),
            urls: ["http://127.0.0.1:0", "http://127.0.0.1:0"]);
        var publicUrl = new Uri(app.Urls.First());
        var adminUrl = new Uri(app.Urls.Last());
        adminPort = adminUrl.Port;
        using var publicClient = new HttpClient { BaseAddress = publicUrl };
        using var adminClient = new HttpClient { BaseAddress = adminUrl };
        publicClient.DefaultRequestHeaders.Host = $"{adminUrl.Host}:{adminPort}";

        (await GetStatusAsync(adminClient, "ncronjob/")).ShouldBe(HttpStatusCode.OK);
        (await NegotiateAsync(adminClient, "ncronjob")).ShouldBe(HttpStatusCode.OK);
        (await GetStatusAsync(publicClient, "ncronjob/")).ShouldBe(HttpStatusCode.NotFound);
        (await NegotiateAsync(publicClient, "ncronjob")).ShouldBe(HttpStatusCode.NotFound);
    }

    private static async Task<WebApplication> StartAsync(
        Action<WebApplication> map,
        Action<IServiceCollection>? configureServices = null,
        string[]? urls = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls(urls ?? ["http://127.0.0.1:0"]);
        builder.Logging.ClearProviders();
        builder.Services.AddNCronJob((Action)(() => { }), "0 0 1 1 *");
        builder.Services.AddNCronJobDashboard();
        configureServices?.Invoke(builder.Services);
        var app = builder.Build();
        map(app);
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }

    private static HttpClient CreateClient(WebApplication app) =>
        new() { BaseAddress = new Uri(app.Urls.First() + "/") };

    private static async Task<HttpStatusCode> GetStatusAsync(HttpClient client, string path)
    {
        using var response = await client.GetAsync(new Uri(path, UriKind.Relative), TestContext.Current.CancellationToken);
        return response.StatusCode;
    }

    private static async Task<HttpStatusCode> NegotiateAsync(HttpClient client, string prefix)
    {
        var path = string.IsNullOrEmpty(prefix) ? "_blazor/negotiate?negotiateVersion=1" : $"{prefix}/_blazor/negotiate?negotiateVersion=1";
        using var response = await client.PostAsync(new Uri(path, UriKind.Relative), content: null, TestContext.Current.CancellationToken);
        return response.StatusCode;
    }

    private sealed class HeaderAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue(UserHeader, out var user))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, user.ToString())], Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
        }
    }
}

[Route("/")]
public sealed class HostHomePage : ComponentBase
{
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.AddContent(0, "host home");
    }
}
