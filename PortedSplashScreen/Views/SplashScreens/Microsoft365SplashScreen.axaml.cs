using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using ClassIsland.Core;
using ClassIsland.Core.Abstractions.Services;
using ClassIsland.Core.Abstractions.Views;
using PortedSplashScreen.Services;
using ReactiveUI;

namespace PortedSplashScreen.Views.SplashScreens;

/// <summary>
/// Microsoft365SplashScreen.xaml 的交互逻辑
/// </summary>
public partial class Microsoft365SplashScreen : SplashWindowBase
{
    public ISplashService SplashService { get; }

    private bool _closed;
    private IDisposable? _splashStatusObserver;

    public Microsoft365SplashScreen(ISplashService splashService)
    {
        SplashService = splashService;
        SplashService.SplashEnded += SplashServiceOnSplashEnded;
        InitializeComponent();
        var cornerRadius = Plugin.Instance?.ResolveCornerRadius() ?? 8;
        SplashRootBorder.CornerRadius = new CornerRadius(cornerRadius);
        SplashCornerClip.Attach(SplashRootBorder, cornerRadius);
    }

    private void SplashServiceOnSplashEnded(object? sender, EventArgs e)
    {
        SplashService.SplashEnded -= SplashServiceOnSplashEnded;
        if (!_closed)
        {
            Dispatcher.UIThread.InvokeAsync(Close);
        }
    }
        

    private void ButtonMinimize_OnClick(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void ButtonExit_OnClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void Microsoft365SplashScreen_OnClosed(object? sender, EventArgs e)
    {
        _closed = true;
        SplashService.SplashEnded -= SplashServiceOnSplashEnded;
        _splashStatusObserver?.Dispose();
        _splashStatusObserver = null;
    }

    /// <summary>
    /// 释放事件订阅。设置页预览不会显示真实窗口，关闭预览时需要手动解除。
    /// </summary>
    internal void ReleaseSplashAssets()
    {
        SplashService.SplashEnded -= SplashServiceOnSplashEnded;
        _splashStatusObserver?.Dispose();
        _splashStatusObserver = null;
    }
        
    public override async Task StartSplash()
    {
        _splashStatusObserver ??= SplashService.ObservableForProperty(x => x.SplashStatus)
            .Subscribe(_ => TryRunJobs());
        await base.StartSplash();
        SplashTiming.MarkStarted();
    }

    public override async Task EndSplash()
    {
        _splashStatusObserver?.Dispose();
        _splashStatusObserver = null;
        await SplashTiming.WaitForFixedDurationAsync();
        await base.EndSplash();
    }
}