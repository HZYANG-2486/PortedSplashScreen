using PortedSplashScreen.Services;

namespace PortedSplashScreen.Tests;

public class SplashDurationTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(5, 5)]
    [InlineData(-3, 0)]
    [InlineData(301, 300)]
    [InlineData(double.NaN, 0)]
    [InlineData(double.PositiveInfinity, 0)]
    public void NormalizeSeconds_限制在允许范围内(double seconds, double expected)
    {
        Assert.Equal(expected, SplashDuration.NormalizeSeconds(seconds), 6);
    }

    [Fact]
    public void GetTarget_未启用时为零()
    {
        Assert.Equal(TimeSpan.Zero, SplashDuration.GetTarget(false, 5));
    }

    [Fact]
    public void GetTarget_启用时使用限制后的秒数()
    {
        Assert.Equal(TimeSpan.FromSeconds(5), SplashDuration.GetTarget(true, 5));
        Assert.Equal(TimeSpan.FromSeconds(300), SplashDuration.GetTarget(true, 999));
    }

    [Theory]
    [InlineData(2, 5, 3)]
    [InlineData(5, 5, 0)]
    [InlineData(10, 5, 0)]
    [InlineData(0, 2.5, 2.5)]
    public void GetRemaining_返回还需要等待的时间(double elapsedSeconds, double targetSeconds, double expectedSeconds)
    {
        var remaining = SplashDuration.GetRemaining(
            TimeSpan.FromSeconds(elapsedSeconds), true, targetSeconds);

        Assert.Equal(expectedSeconds, remaining.TotalSeconds, 6);
    }

    [Fact]
    public void GetRemaining_未启用时为零()
    {
        Assert.Equal(TimeSpan.Zero,
            SplashDuration.GetRemaining(TimeSpan.FromSeconds(1), false, 5));
    }

    [Fact]
    public void GetRemaining_非法秒数视为零()
    {
        Assert.Equal(TimeSpan.Zero,
            SplashDuration.GetRemaining(TimeSpan.FromSeconds(1), true, double.NaN));
    }

    [Fact]
    public void GetRemaining_超过上限时按上限等待()
    {
        var remaining = SplashDuration.GetRemaining(TimeSpan.FromSeconds(10), true, 999);

        Assert.Equal(290, remaining.TotalSeconds, 6);
    }
}
