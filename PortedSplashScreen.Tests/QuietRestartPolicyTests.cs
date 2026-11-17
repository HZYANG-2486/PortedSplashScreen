using PortedSplashScreen.Services;

namespace PortedSplashScreen.Tests;

public class QuietRestartPolicyTests
{
    private static string[] Args(params string[] extra) => ["/opt/ClassIsland/ClassIsland.Desktop", .. extra];

    [Theory]
    [InlineData("-q")]
    [InlineData("--quiet")]
    [InlineData("-Q")]
    [InlineData("--QUIET")]
    public void 启用时_静默参数应跳过启动画面(string argument) =>
        Assert.True(QuietRestartPolicy.ShouldSkipSplash(true, Args(argument)));

    [Fact]
    public void 启用时_普通启动参数不应跳过() =>
        Assert.False(QuietRestartPolicy.ShouldSkipSplash(true, Args("-m", "profile")));

    [Fact]
    public void 未启用设置时_静默参数也不跳过() =>
        Assert.False(QuietRestartPolicy.ShouldSkipSplash(false, Args("-q")));

    [Fact]
    public void 只有可执行文件路径时不应跳过() =>
        Assert.False(QuietRestartPolicy.ShouldSkipSplash(true, Args()));

    [Fact]
    public void 参数仅包含静默前缀但不完全匹配时不应跳过() =>
        Assert.False(QuietRestartPolicy.ShouldSkipSplash(true, Args("--quiet-mode")));

    [Theory]
    [InlineData("-q", true)]
    [InlineData("--quiet", true)]
    [InlineData("-quiet", false)]
    [InlineData("q", false)]
    [InlineData("", false)]
    [InlineData("/q", false)]
    public void IsQuietArgument_仅识别完整的静默参数(string argument, bool expected) =>
        Assert.Equal(expected, QuietRestartPolicy.IsQuietArgument(argument));
}
