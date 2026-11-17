namespace PortedSplashScreen.Services;

/// <summary>
/// 判断本次启动是否应跳过启动画面。
/// ClassIsland 在异常崩溃恢复、内存看门狗等场景下会带 -q 静默重启。
/// </summary>
public static class QuietRestartPolicy
{
    private static readonly string[] QuietArguments = ["-q", "--quiet"];

    /// <summary>
    /// 仅在用户启用“静默重启时跳过启动画面”且命令行包含静默参数时返回 <see langword="true"/>。
    /// </summary>
    public static bool ShouldSkipSplash(bool skipEnabled, IEnumerable<string> arguments) =>
        skipEnabled && arguments.Any(IsQuietArgument);

    /// <summary>
    /// 判断单个命令行参数是否为静默启动参数（不区分大小写）。
    /// </summary>
    public static bool IsQuietArgument(string argument) =>
        QuietArguments.Any(quiet => argument.Equals(quiet, StringComparison.OrdinalIgnoreCase));
}
