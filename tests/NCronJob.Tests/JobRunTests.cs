using Microsoft.Extensions.Time.Testing;

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
        List<JobStateType> expected = Enum.GetValues<JobStateType>().ToList();
        List<JobStateType> actual = AllPossibleStates.Keys.ToList();
        Assert.Equal(expected, actual);
    }

    [Theory]
    [ClassData(typeof(FinalJobStateTypeTestData))]
    internal void CompletedJobRunsCannotChangeTheirStateFurther(JobStateType value)
    {
        int howManyTimes = 0;

        JobDefinition jd = new JobDefinition(typeof(DummyJob), null, null, null);
        var jobRun = JobRun.Create(new FakeTimeProvider(), (jr) => { howManyTimes++; }, jd);
    
        Assert.Equal(JobStateType.NotStarted, jobRun.CurrentState.Type);
        Assert.Equal(1, howManyTimes);

        var fault = new Exception();

        jobRun.NotifyStateChange(value, fault);
        Assert.Equal(value, jobRun.CurrentState.Type);
        Assert.Equal(2, howManyTimes);

        foreach (JobStateType state in AllPossibleStates.Keys)
        {
            jobRun.NotifyStateChange(state, fault);
            Assert.Equal(value, jobRun.CurrentState.Type);
            Assert.Equal(2, howManyTimes);
        }
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

    private sealed class DummyJob : IJob
    {
        public Task RunAsync(IJobExecutionContext context, CancellationToken token)
        {
            return Task.CompletedTask;
        }
    }
}
