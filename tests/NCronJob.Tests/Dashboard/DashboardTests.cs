using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using NCronJob.Dashboard;
using Shouldly;

namespace NCronJob.Tests.Dashboard;

public class DashboardTests
{
    private sealed record Payload(int Id, string Name);

    private sealed record Report(string Region, IReadOnlyList<string> Recipients, Payload Owner);

    private sealed class Node
    {
        public Node? Next { get; set; }

        public override string ToString() => "cyclic node";
    }

    private sealed record CyclicRecord
    {
        public CyclicRecord? Next { get; set; }
    }

    private sealed class ThrowingGetter
    {
        public string Value => throw new FormatException("broken getter");
    }

    [Theory]
    [InlineData("/logo.png", "image/png")]
    [InlineData("/_framework/blazor.web.js", "text/javascript")]
    public async Task EmbeddedAssetsAreServedWithoutHostStaticFiles(string path, string contentType)
    {
        var context = new DefaultHttpContext { Request = { Path = path }, Response = { Body = new MemoryStream() } };
        var nextCalled = false;

        await EmbeddedAssets.ServeAsync(context, _ => { nextCalled = true; return Task.CompletedTask; });

        nextCalled.ShouldBeFalse();
        context.Response.ContentType.ShouldBe(contentType);
        context.Response.Body.Length.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task UnknownPathsArePassedToTheNextMiddleware()
    {
        var context = new DefaultHttpContext { Request = { Path = "/runs" } };
        var nextCalled = false;

        await EmbeddedAssets.ServeAsync(context, _ => { nextCalled = true; return Task.CompletedTask; });

        nextCalled.ShouldBeTrue();
    }

    [Fact]
    public void ComplexParameterIsShownAsCompactPreviewAndIndentedFullJson()
    {
        var text = JsonParameter.Describe(new Report("EU", ["a@x.ch"], new Payload(1, "Ops"))).ShouldNotBeNull();

        text.Preview.ShouldBe("""{"Region":"EU","Recipients":["a@x.ch"],"Owner":{"Id":1,"Name":"Ops"}}""");
        text.Full.ShouldContain("\n");
        JsonParameter.Parse(text.Full, typeof(Report)).ShouldBeOfType<Report>().Owner.ShouldBe(new Payload(1, "Ops"));
    }

    [Fact]
    public void LongParameterPreviewIsTruncatedWhileFullJsonIsComplete()
    {
        var recipients = Enumerable.Range(0, 20).Select(i => $"user{i}@example.com").ToList();

        var text = JsonParameter.Describe(new Report("EU", recipients, new Payload(1, "Ops"))).ShouldNotBeNull();

        text.Preview.Length.ShouldBe(JsonParameter.PreviewLength);
        text.Preview.ShouldEndWith("…");
        text.Full.ShouldContain("user19@example.com");
    }

    [Fact]
    public void ParameterThatCannotBeSerializedFallsBackToItsTypeName()
    {
        var node = new Node();
        node.Next = node;

        JsonParameter.Describe(node).ShouldBe(new ParameterText("Node (not serializable to JSON)", "Node (not serializable to JSON)"));
    }

    [Fact]
    public void CyclicRecordDoesNotFallBackToItsRecursiveToString()
    {
        var cyclic = new CyclicRecord();
        cyclic.Next = cyclic;

        JsonParameter.Describe(cyclic)!.Full.ShouldBe("CyclicRecord (not serializable to JSON)");
    }

    [Fact]
    public void ThrowingGetterFallsBackInsteadOfBreakingThePage()
    {
        JsonParameter.Describe(new ThrowingGetter())!.Full.ShouldBe("ThrowingGetter (not serializable to JSON)");
    }

    [Fact]
    public void NonFiniteNumberFallsBackInsteadOfThrowing()
    {
        JsonParameter.Describe(double.NaN)!.Full.ShouldBe("Double (not serializable to JSON)");
    }

    [Fact]
    public void UnnamedDelegateRunsAreNamedAfterTheirGeneratedJobName()
    {
        var observer = new JobExecutionProgressObserver(NullLogger<JobExecutionProgressObserver>.Instance);
        using var store = new DashboardRunStore();
        store.Subscribe(observer);
        var definition = JobDefinition.CreateUntyped(name: null, () => { });

        _ = JobRun.CreateInstant(TimeProvider.System, observer.Report, definition, DateTimeOffset.UtcNow, OptionalParameter.Unspecified, CancellationToken.None);

        store.GetRuns().ShouldHaveSingleItem().JobName.ShouldBe(definition.JobFullName);
    }

    [Fact]
    public void JsonParameterDeserializesIntoConfiguredParameterType()
    {
        JsonParameter.Parse("""{ "Id": 1, "Name": "a" }""", typeof(Payload)).ShouldBe(new Payload(1, "a"));
    }

    [Fact]
    public void JsonParameterDeserializesStringParametersLikeAnyOtherType()
    {
        JsonParameter.Parse("\"hello\"", typeof(string)).ShouldBe("hello");
        JsonParameter.Parse("null", typeof(string)).ShouldBeNull();
        Should.Throw<JsonException>(() => JsonParameter.Parse("""{"a":1}""", typeof(string)));
    }

    [Fact]
    public void JsonParameterForObjectParameterUnwrapsStringsAndPassesOtherJsonAsRawText()
    {
        JsonParameter.Parse("\"hello\"", typeof(object)).ShouldBe("hello");
        JsonParameter.Parse("null", typeof(object)).ShouldBeNull();
        JsonParameter.Parse("""{"a":1}""", typeof(object)).ShouldBe("""{"a":1}""");
    }

    [Theory]
    [InlineData("""{ "Id": 1, "Name": "a", "Extra": true }""")]
    [InlineData("""{ "Id": 1 }""")]
    [InlineData("""{ "Id": 1, "Name": null }""")]
    public void JsonParameterRejectsJsonThatDoesNotFollowTheParameterType(string json)
    {
        Should.Throw<JsonException>(() => JsonParameter.Parse(json, typeof(Payload)));
    }

    [Fact]
    public void JsonParameterSchemaDescribesTheParameterType()
    {
        var schema = JsonParameter.Schema(typeof(Payload)).ShouldNotBeNull();

        schema.ShouldContain("\"Id\"");
        schema.ShouldContain("\"Name\"");
    }

    [Fact]
    public void JsonParameterSchemaIsNullWhenTheTypeHasInvalidJsonMetadata()
    {
        JsonParameter.Schema(typeof(ClashingNames)).ShouldBeNull();
    }

    private sealed class ClashingNames
    {
        [System.Text.Json.Serialization.JsonPropertyName("x")] public int A { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("x")] public int B { get; set; }
    }

    [Fact]
    public void PolymorphicParameterRoundTripsThroughItsDeclaredContract()
    {
        var json = JsonParameter.Describe(new DerivedRequest(7), typeof(BaseRequest))!.Full;

        JsonParameter.Parse(json, typeof(BaseRequest)).ShouldBe(new DerivedRequest(7));
    }

    [System.Text.Json.Serialization.JsonPolymorphic]
    [System.Text.Json.Serialization.JsonDerivedType(typeof(DerivedRequest), "derived")]
    private abstract record BaseRequest;

    private sealed record DerivedRequest(int Id) : BaseRequest;

    [Fact]
    public void JsonParameterRejectsInvalidJson()
    {
        Should.Throw<JsonException>(() => JsonParameter.Parse("hello", typeof(string)));
    }

    [Fact]
    public async Task StartupRunsAreRecordedBeforeTheDashboardIsMapped()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<Storage>();
        services.AddSingleton<IHostApplicationLifetime, MockHostApplicationLifetime>();
        services.AddNCronJob(n => n.AddJob<DummyJob>(p => p.RunAtStartup()));
        services.AddNCronJobDashboard();
        await using var provider = services.BuildServiceProvider();
        var startupJob = provider.GetRequiredService<JobRegistry>().GetAllStartupJobs().ShouldHaveSingleItem();

        await provider.GetRequiredService<StartupJobManager>().ProcessStartupJobs(TestContext.Current.CancellationToken);

        var run = provider.GetRequiredService<DashboardRunStore>().GetRuns().ShouldHaveSingleItem();
        run.TriggerType.ShouldBe(TriggerType.Startup);
        run.Current.State.ShouldBe(ExecutionState.Completed);
        run.BelongsTo(startupJob).ShouldBeTrue();
    }

    [Fact]
    public void DependencyTreeFollowsChainsAndStopsAtCycles()
    {
        var services = new ServiceCollection();
        services.AddNCronJob(n => n
            .AddJob<DummyJob>(p => p.WithCronExpression(Cron.AtEveryMinute))
            .ExecuteWhen(success: s => s.RunJob<AnotherDummyJob>(), faulted: f => f.RunJob<ExceptionJob>())
            .AddJob<AnotherDummyJob>()
            .ExecuteWhen(success: s => s.RunJob<DummyJob>()));
        using var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<JobRegistry>();
        var root = registry.FindFirstRootJobDefinition(typeof(DummyJob)).ShouldNotBeNull();

        var dependents = DependencyTree.DependentsOf(registry, root);

        dependents.Select(d => (d.JobName, d.RunsOnFault)).ShouldBe(
            [(typeof(AnotherDummyJob).FullName!, false), (typeof(ExceptionJob).FullName!, true)]);
        var cycle = dependents[0].Dependents.ShouldHaveSingleItem();
        cycle.JobName.ShouldBe(typeof(DummyJob).FullName);
        cycle.Dependents.ShouldBeEmpty();
    }

    [Fact]
    public void RunStoreRecordsStatesTriggerAndParameter()
    {
        var observer = new JobExecutionProgressObserver(NullLogger<JobExecutionProgressObserver>.Instance);
        using var store = new DashboardRunStore();
        store.Subscribe(observer);

        var run = CreateInstantRun(observer, OptionalParameter.FromValue("p"));
        run.NotifyStateChange(JobStateType.Running);
        run.NotifyStateChange(JobStateType.Completed);

        var record = store.GetRuns().ShouldHaveSingleItem();
        record.Parameter.ShouldBe(new ParameterText("\"p\"", "\"p\""));
        record.States.Select(s => s.State).ShouldBe([ExecutionState.NotStarted, ExecutionState.Running, ExecutionState.Completed]);
        record.IsFinished.ShouldBeTrue();
        record.TriggerType.ShouldBe(TriggerType.Instant);
    }

    [Fact]
    public void RunStoreEvictsOnlyTheOldestFinishedRunsAndKeepsActiveAndStartupRuns()
    {
        var observer = new JobExecutionProgressObserver(NullLogger<JobExecutionProgressObserver>.Instance);
        using var store = new DashboardRunStore();
        store.Subscribe(observer);
        var oldestFinished = CreateInstantRun(observer, OptionalParameter.Unspecified);
        oldestFinished.NotifyStateChange(JobStateType.Completed);
        var active = CreateInstantRun(observer, OptionalParameter.Unspecified);
        var startup = JobRun.CreateStartupJob(TimeProvider.System, observer.Report, JobDefinition.CreateTyped(typeof(DummyJob), parameter: null));
        startup.NotifyStateChange(JobStateType.Completed);

        for (var i = 0; i < DashboardRunStore.Capacity; i++)
        {
            CreateInstantRun(observer, OptionalParameter.Unspecified).NotifyStateChange(JobStateType.Completed);
        }

        var runs = store.GetRuns();
        runs.Count.ShouldBe(DashboardRunStore.Capacity + 2);
        runs.ShouldNotContain(r => r.RunId == oldestFinished.JobRunId);
        runs.ShouldContain(r => r.RunId == active.JobRunId);
        runs.ShouldContain(r => r.RunId == startup.JobRunId);
    }

    [Fact]
    public void StartupRunsMatchOnlyTheirOwnRegistrationEvenWhenTypeAndNameAreEqual()
    {
        var observer = new JobExecutionProgressObserver(NullLogger<JobExecutionProgressObserver>.Instance);
        using var store = new DashboardRunStore();
        store.Subscribe(observer);
        var succeeding = JobDefinition.CreateTyped(typeof(DummyJob), parameter: "a");
        var failing = JobDefinition.CreateTyped(typeof(DummyJob), parameter: "b");

        JobRun.CreateStartupJob(TimeProvider.System, observer.Report, succeeding).NotifyStateChange(JobStateType.Completed);
        JobRun.CreateStartupJob(TimeProvider.System, observer.Report, failing).NotifyStateChange(JobStateType.Faulted, new InvalidOperationException());

        var runs = store.GetRuns();
        runs.Single(r => r.BelongsTo(succeeding)).Current.State.ShouldBe(ExecutionState.Completed);
        runs.Single(r => r.BelongsTo(failing)).Current.State.ShouldBe(ExecutionState.Faulted);
    }

    private static JobRun CreateInstantRun(JobExecutionProgressObserver observer, OptionalParameter parameter) =>
        JobRun.CreateInstant(
            TimeProvider.System,
            observer.Report,
            JobDefinition.CreateTyped(typeof(DummyJob), parameter: null),
            DateTimeOffset.UtcNow,
            parameter,
            CancellationToken.None);
}
