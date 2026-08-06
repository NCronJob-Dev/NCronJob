# Dashboard

`NCronJob.Dashboard` is a self-hosted monitoring and control UI for NCronJob. It starts an embedded Kestrel server on its own port, so the parent application can be a worker service, console process, or web application.

The dashboard provides:

- live running jobs and bounded recent-run history, including parameters rendered as JSON;
- recurring schedules, enabled state, time zone, and next occurrence;
- an SVG dependency graph for success and faulted branches;
- run-now, enable, disable, schedule update, and parameter update controls.

The dashboard and core packages are released together and the dashboard depends on the matching core version.

The UI uses server-rendered Razor Components plus a small embedded JavaScript client. Live changes arrive over Server-Sent Events, and controls use JSON POST requests. This avoids relying on Blazor framework static assets in worker and console consumers.
