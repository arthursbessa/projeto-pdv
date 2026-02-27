using Pdv.Application.Services;
using Xunit;

namespace Pdv.Tests;

public sealed class SyncBackoffPolicyTests
{
    [Theory]
    [InlineData(1, 10)]
    [InlineData(2, 30)]
    [InlineData(3, 120)]
    [InlineData(4, 300)]
    [InlineData(5, 900)]
    [InlineData(6, 1800)]
    [InlineData(20, 1800)]
    public void NextDelay_ShouldRespectSchedule(int attempts, int expectedSeconds)
    {
        var delay = SyncBackoffPolicy.NextDelay(attempts);
        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), delay);
    }
}
