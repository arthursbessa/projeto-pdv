namespace Pdv.Application.Services;

public static class SyncBackoffPolicy
{
    private static readonly TimeSpan[] Delays =
    [
        TimeSpan.FromSeconds(10),
        TimeSpan.FromSeconds(30),
        TimeSpan.FromMinutes(2),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(15),
        TimeSpan.FromMinutes(30)
    ];

    public static TimeSpan NextDelay(int attempts)
    {
        if (attempts <= 0)
        {
            return Delays[0];
        }

        var index = Math.Min(attempts - 1, Delays.Length - 1);
        return Delays[index];
    }
}
