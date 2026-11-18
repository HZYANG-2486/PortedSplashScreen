using Avalonia;
using Avalonia.Animation;
using Avalonia.Styling;

namespace PortedSplashScreen.Services;

/// <summary>
/// 用一个几乎察觉不到的无限动画让合成器持续产生新帧。
/// 合成与重绘由渲染线程推进，因此 UI 线程被启动流程占用时，
/// 只要位图像素被后台线程更新，画面仍有机会刷新。
/// </summary>
public static class CompositorKeepAlive
{
    /// <summary>
    /// 在指定控件上启动保活动画。
    /// </summary>
    public static void Start(Visual target)
    {
        var animation = new Animation
        {
            Duration = TimeSpan.FromMilliseconds(100),
            IterationCount = IterationCount.Infinite,
            Children =
            {
                new KeyFrame
                {
                    Cue = new Cue(0),
                    Setters = { new Setter(Visual.OpacityProperty, 1d) }
                },
                new KeyFrame
                {
                    Cue = new Cue(1),
                    Setters = { new Setter(Visual.OpacityProperty, 0.999d) }
                }
            }
        };

        _ = animation.RunAsync(target);
    }
}
