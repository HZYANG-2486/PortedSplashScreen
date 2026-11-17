using PortedSplashScreen.Services;

namespace PortedSplashScreen.Tests;

public class AnimatedImageTimingTests
{
    [Theory]
    [InlineData(0, 20)]
    [InlineData(-10, 20)]
    [InlineData(19, 20)]
    [InlineData(20, 20)]
    [InlineData(40, 40)]
    [InlineData(1000, 1000)]
    public void GetFrameDelay_把过小或无效的帧延时抬到下限(int duration, int expectedMilliseconds)
    {
        var delay = AnimatedImageTiming.GetFrameDelay(duration);

        Assert.Equal(expectedMilliseconds, delay.TotalMilliseconds, 6);
    }

    [Fact]
    public void GetFrameDelay_缺失帧延时时使用默认值()
    {
        var delay = AnimatedImageTiming.GetFrameDelay(null);

        Assert.Equal(AnimatedImageTiming.DefaultFrameDelayMilliseconds, delay.TotalMilliseconds, 6);
    }

    [Theory]
    [InlineData(0, 3, 1)]
    [InlineData(1, 3, 2)]
    [InlineData(2, 3, 0)]
    [InlineData(0, 1, 0)]
    [InlineData(-1, 3, 0)]
    [InlineData(5, 3, 0)]
    public void GetNextFrameIndex_到达末帧后回到第一帧(int current, int count, int expected)
    {
        Assert.Equal(expected, AnimatedImageTiming.GetNextFrameIndex(current, count));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(5)]
    public void GetNextFrameIndex_没有帧时返回零(int current)
    {
        Assert.Equal(0, AnimatedImageTiming.GetNextFrameIndex(current, 0));
    }
}
