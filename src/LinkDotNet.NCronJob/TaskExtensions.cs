namespace LinkDotNet.NCronJob;

internal static class TaskExtensions
{
    public static Task RunInIsolation(this Task t, IsolationLevel isolationLevel)
    {
        return isolationLevel == IsolationLevel.None ? t : Task.Run(() => t);
    }
}
