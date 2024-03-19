namespace NCronJob.Tests;

internal sealed class CommunicationChannel
{
    public readonly TaskCompletionSource TaskCompletionSource = new();
}
