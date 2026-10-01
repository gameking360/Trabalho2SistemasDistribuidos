using Commerce.Infrastructure.Retry;
using Microsoft.Extensions.Options;

namespace Commerce.Tests.Unit;

public sealed class RetryPolicyTests
{
    private readonly RetryPolicy _policy = new(Options.Create(new RetryOptions()));

    [Theory]
    [InlineData(1, 2)]
    [InlineData(2, 4)]
    [InlineData(3, 6)]
    [InlineData(4, 8)]
    [InlineData(5, 10)]
    public void GetDelay_AddsTwoSecondsForEachNewFailure(int failedAttempt, int expectedSeconds)
    {
        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), _policy.GetDelay(failedAttempt));
    }

    [Fact]
    public void GetDelay_AttemptLowerThanOne_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _policy.GetDelay(0));
    }

    [Fact]
    public void MaxAttempts_IsSixByDefault()
    {
        Assert.Equal(6, _policy.MaxAttempts);
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(3, false)]
    [InlineData(4, false)]
    [InlineData(5, false)]
    [InlineData(6, true)]
    public void ShouldDeadLetter_OnlyWhenTheSixthAttemptFails(int failedAttempt, bool expected)
    {
        Assert.Equal(expected, _policy.ShouldDeadLetter(failedAttempt));
    }
}
