using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ClassIsland.Core.Abstractions.Services;
using ClassIsland.Core.Abstractions.Views;
using PortedSplashScreen.Services;

namespace PortedSplashScreen.Views.SplashScreens;

public partial class AllsSplashScreen : SplashWindowBase
{
    public ISplashService SplashService { get; }
    
    public AllsSplashScreen(ISplashService splashService)
    {
        SplashService = splashService;
        InitializeComponent();
    }

    public override async Task StartSplash()
    {
        await base.StartSplash();
        SplashTiming.MarkStarted();
    }

    public override async Task EndSplash()
    {
        await SplashTiming.WaitForFixedDurationAsync();
        await base.EndSplash();
    }
}