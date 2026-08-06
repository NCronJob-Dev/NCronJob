# Controls and security

The dashboard exposes job parameters and can mutate scheduler state. It therefore binds only to loopback by default.

To require an application-specific credential, provide an authorization predicate:

```csharp
host.UseNCronJobDashboard(options =>
{
    options.Port = 5050;
    options.Authorize = context =>
        context.Request.Headers.TryGetValue("X-Dashboard-Key", out var key) &&
        CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(key.ToString()),
            Encoding.UTF8.GetBytes(configuration["DashboardKey"]!));
});
```

Return `true` only for authorized requests. Unauthorized requests receive HTTP 401. If the dashboard is exposed beyond loopback, place it behind TLS and an authenticated reverse proxy; parameters may contain sensitive values.

Set `EnableControls = false` for monitoring-only deployments. This removes controls from the UI and rejects calls through the dashboard control service.

Schedule and parameter editing requires a named job because NCronJob's runtime mutation API identifies these operations by name. Run-now and enable/disable also work for uniquely registered typed jobs.
