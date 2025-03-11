using Microsoft.Extensions.Time.Testing;
using Shouldly;

namespace NCronJob.Tests;

public class JobRunStatesTests
{
    private static readonly Dictionary<JobStateType, bool> AllPossibleStates = new()
        {
            { JobStateType.NotStarted , false},
            { JobStateType.Scheduled, false},
            { JobStateType.Initializing, false},
            { JobStateType.Running, false},
            { JobStateType.Retrying, false},
            { JobStateType.Completing, false},
            { JobStateType.WaitingForDependency, false},

            // Final states
            { JobStateType.Skipped, true},
            { JobStateType.Completed, true},
            { JobStateType.Faulted, true},
            { JobStateType.Cancelled, true},
            { JobStateType.Expired, true},
        };

    [Fact]
    public void TestValuesAreInSyncWithCurrentEnumValues()
    {
        var expected = Enum.GetValues<JobStateType>().ToList();
        var actual = AllPossibleStates.Keys.ToList();
        actual.ShouldBeEquivalentTo(expected);
    }

    [Theory]
    [ClassData(typeof(FinalJobStateTypeTestData))]
    internal void CompletedJobRunsCannotChangeTheirStateFurther(JobStateType value)
    {
        var howManyTimes = 0;

        var jd = JobDefinition.CreateTyped(typeof(DummyJob), null);
        var jobRun = JobRun.Create(new FakeTimeProvider(), (jr) => { howManyTimes++; }, jd);

        jobRun.CurrentState.Type.ShouldBe(JobStateType.NotStarted);
        howManyTimes.ShouldBe(1);

        var fault = new Exception();

        jobRun.NotifyStateChange(value, fault);
        jobRun.CurrentState.Type.ShouldBe(value);
        howManyTimes.ShouldBe(2);

        foreach (var state in AllPossibleStates.Keys)
        {
            jobRun.NotifyStateChange(state, fault);
            jobRun.CurrentState.Type.ShouldBe(value);
            howManyTimes.ShouldBe(2);
        }
    }

    [Theory]
    [ClassData(typeof(AllJobStateTypeTestData))]
    internal void OnlyRetryingJobRunsCanTriggerMoreThanOnceTheProgressReporter(JobStateType value)
    {
        var howManyTimes = 0;

        var jd = JobDefinition.CreateTyped(typeof(DummyJob), null);
        var jobRun = JobRun.Create(new FakeTimeProvider(), (jr) => { howManyTimes++; }, jd);

        jobRun.CurrentState.Type.ShouldBe(JobStateType.NotStarted);
        howManyTimes.ShouldBe(1);

        var fault = new Exception();

        jobRun.NotifyStateChange(value, fault);
        jobRun.CurrentState.Type.ShouldBe(value);

        var expectedNumberOfFinalInvocations = value switch
        {
            JobStateType.NotStarted => 1, // Resetting to NotStarted shouldn't do anything
            JobStateType.Retrying => 3,
            _ => 2
        };

        jobRun.NotifyStateChange(value, fault);
        jobRun.CurrentState.Type.ShouldBe(value);

        howManyTimes.ShouldBe(expectedNumberOfFinalInvocations);
    }

    internal sealed class FinalJobStateTypeTestData : TheoryData<JobStateType>
    {
        public FinalJobStateTypeTestData()
        {
            foreach (var kvp in AllPossibleStates.Where(x => x.Value))
            {
                Add(kvp.Key);
            }
        }
    }

    internal sealed class AllJobStateTypeTestData : TheoryData<JobStateType>
    {
        public AllJobStateTypeTestData()
        {
            foreach (var kvp in AllPossibleStates)
            {
                Add(kvp.Key);
            }
        }
    }

    private sealed class DummyJob : IJob
    {
        public Task RunAsync(IJobExecutionContext context, CancellationToken token)
        {
            return Task.CompletedTask;
        }
    }
}
