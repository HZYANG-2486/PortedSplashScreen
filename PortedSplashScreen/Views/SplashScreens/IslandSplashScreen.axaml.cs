using System.ComponentModel;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Rendering.Composition;
using ClassIsland.Core.Abstractions.Services;
using ClassIsland.Core.Abstractions.Views;
using PortedSplashScreen.Services;
using ReactiveUI;

namespace PortedSplashScreen.Views.SplashScreens;

/// <summary>
/// IslandSplashScreen.xaml 的交互逻辑
/// </summary>
public partial class IslandSplashScreen : SplashWindowBase
{
    public ISplashService SplashService { get; }
    public Plugin Plugin { get; }

    private double _lastProgress = 0.0;
    private IDisposable? _splashStatusObserver;

    private bool _canClose = false;

    public Image? SplashImage { get; }

    public IslandSplashScreen(ISplashService splashService, Plugin plugin)
    {
        SplashService = splashService;
        Plugin = plugin;
        InitializeComponent();
        var cornerRadius = Plugin.ResolveCornerRadius();
        SplashRootBorder.CornerRadius = new CornerRadius(cornerRadius);
        SplashCornerClip.Attach(SplashRootBorder, cornerRadius);
        Loaded += OnLoaded;
        Opened += (_, _) => Reposition();
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        var visualProgressBarFill = ElementComposition.GetElementVisual(ProgressBarFill);
        if (visualProgressBarFill != null)
        {
            visualProgressBarFill.Offset = visualProgressBarFill.Offset with { X = -400 };
            visualProgressBarFill.Opacity = 1.0f;
        }
    }

    void Cal(object sender, RoutedEventArgs e) => Reposition();

    private void Reposition()
    {
        // 窗口尚未显示时 Screens 可能拿不到屏幕信息，此时计算出的位置会把窗口推到屏幕外，
        // 因此这里只做能算准的定位，拿不到屏幕大小时等 Opened 后再定位。
        var screen = Screens.ScreenFromWindow(this) ?? Screens.Primary ?? Screens.All.FirstOrDefault();
        if (screen is null || screen.Bounds.Width <= 0)
        {
            return;
        }

        var windowWidth = double.IsFinite(Width) && Width > 0 ? Width : Bounds.Width;
        Position = new PixelPoint((int)((screen.Bounds.Width - windowWidth) / 2), 0);
    }

    private void SplashServiceOnProgressChanged(object? sender, double e)
    {
        _ = UpdateAnimationAsync(e);

    }

    private async Task UpdateAnimationAsync(double value, bool isFinal = false)
    {
        var visualProgressBarFill = ElementComposition.GetElementVisual(ProgressBarFill);
        if (visualProgressBarFill == null)
        {
            return;
        }
        
        var currentOffset = visualProgressBarFill.Offset;
        var compositor = visualProgressBarFill.Compositor;
        var progressAnimation = compositor.CreateVector3DKeyFrameAnimation();
        progressAnimation.InsertKeyFrame(1.0f, currentOffset with { X = -400 * (1 - value / 100) }, new CubicEaseOut());
        var duration = isFinal ? TimeSpan.FromSeconds(0.15) : TimeSpan.FromSeconds(Math.Max((value - _lastProgress ) / 8, 0.5));
        progressAnimation.Duration = duration;
        visualProgressBarFill.StartAnimation(nameof(visualProgressBarFill.Offset), progressAnimation);
        _lastProgress = value;

        await Task.Delay(duration);
    }


    private void IslandSplashScreen_OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (!_canClose)
        {
            e.Cancel = true;
        }
    }

    public override async Task StartSplash()
    {
        SplashService.ProgressChanged += SplashServiceOnProgressChanged;
        _splashStatusObserver ??= SplashService.ObservableForProperty(x => x.SplashStatus)
            .Subscribe(_ => TryRunJobs());
        await base.StartSplash();
        SplashTiming.MarkStarted();
    }

    public override async Task EndSplash()
    {
        SplashService.ProgressChanged -= SplashServiceOnProgressChanged;
        _splashStatusObserver?.Dispose();
        _splashStatusObserver = null;
        // 启动条紧贴屏幕顶部，与主界面顶栏位置重叠，启动完成后立即收起；
        // 若继续等待固定显示时长，启动条会与主界面叠在一起。
        _canClose = true;
        await base.EndSplash();
    }
}