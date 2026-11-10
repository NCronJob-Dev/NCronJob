namespace NCronJobSample;

public sealed record ReportRequest(string Region, IReadOnlyList<string> Recipients, ReportOptions Options);

public sealed record ReportOptions(bool IncludeCharts, string Format);
