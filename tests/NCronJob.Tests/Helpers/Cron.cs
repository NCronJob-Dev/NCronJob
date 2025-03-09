namespace NCronJob.Tests;

public static class Cron
{
    public const string AtEverySecond = "* * * * * *";

    public const string AtEveryMinute = "* * * * *";
    public const string AtEvery2ndMinute = "*/2 * * * *";

    public const string AtMinute0 = "0 * * * *";
    public const string AtMinute2 = "2 * * * *";
    public const string AtMinute5 = "5 * * * *";

    public const string AtEveryJanuaryTheFirst = "0 0 1 1 *";

    public const string Never = "* * 31 2 *";
}
