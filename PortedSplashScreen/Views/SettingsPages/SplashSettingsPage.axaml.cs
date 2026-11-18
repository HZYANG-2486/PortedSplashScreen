using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Core.Abstractions.Services;
using ClassIsland.Core.Attributes;
using ClassIsland.Shared;
using FluentAvalonia.UI.Controls;
using PortedSplashScreen.Models;
using PortedSplashScreen.Services;
using PortedSplashScreen.Views.SplashScreens;

namespace PortedSplashScreen.Views.SettingsPages;

/// <summary>
/// SplashSettingsPage.xaml 的交互逻辑
/// </summary>
[SettingsPageInfo("com.ported.classisland.portedSplashScreen.settings", "PortedSplashScreen", "\ueea1", "\ueea0")]
public partial class SplashSettingsPage : SettingsPageBase
{
    public Plugin Plugin { get; }

    /// <summary>
    /// 可选的 Android Studio 内置背景素材：显示名 → 素材名。
    /// </summary>
    public static readonly Dictionary<string, string> InternalImages =
        SplashAssetProvider.AndroidStudioAssets.ToDictionary(a => a.Name, a => a.Name);

    /// <summary>
    /// 文件选择器中的视频类型。
    /// </summary>
    private static readonly FilePickerFileType VideoFileType = new("视频文件")
    {
        Patterns = ["*.mp4", "*.webm", "*.mov", "*.mkv", "*.m4v", "*.avi"]
    };

    public SplashSettingsPage(Plugin plugin)
    {
        Plugin = plugin;
        InitializeComponent();
        AsBackgroundModeBox.SelectionChanged += OnAsBackgroundModeChanged;
        PsBackgroundModeBox.SelectionChanged += OnPsBackgroundModeChanged;
        CornerRadiusExpander.IsVisible =
            SplashStyleDefaults.HasRoundedCorners(Plugin.Settings.SplashKind);
        SyncBackgroundModeBoxes();
        UpdateBackgroundPanels();
        UpdateFolderImageStatus();
        UpdateNetworkTimeoutVisibility();
        UpdateVideoDecoderVisibility();
        RefreshFfmpegStatus();
        SyncCornerRadiusBox();
        CornerRadiusBox.ValueChanged += OnCornerRadiusValueChanged;
        _ = RefreshAssetStatusAsync();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Plugin.Settings.PropertyChanged -= OnSettingsPropertyChanged;
        Plugin.Settings.PropertyChanged += OnSettingsPropertyChanged;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        Plugin.Settings.PropertyChanged -= OnSettingsPropertyChanged;
        base.OnDetachedFromVisualTree(e);
    }

    private void OnSettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Settings.SplashKind))
        {
            // 切换样式时恢复该样式在原仓库中的默认圆角
            Plugin.Settings.CornerRadius = -1;
            SyncCornerRadiusBox();
            CornerRadiusExpander.IsVisible =
                SplashStyleDefaults.HasRoundedCorners(Plugin.Settings.SplashKind);
            SyncBackgroundModeBoxes();
            UpdateBackgroundPanels();
            UpdateFolderImageStatus();
            UpdateNetworkTimeoutVisibility();
            UpdateVideoDecoderVisibility();
            RefreshFfmpegStatus();
        }

        if (e.PropertyName == nameof(Settings.FfmpegPath))
        {
            RefreshFfmpegStatus();
        }

        if (e.PropertyName is nameof(Settings.SplashKind)
            or nameof(Settings.AndroidStudioBackgroundSource)
            or nameof(Settings.CustomAndroidStudioSplashImageKind)
            or nameof(Settings.CustomAndroidStudioSplashImagePath)
            or nameof(Settings.PhotoshopBackgroundSource)
            or nameof(Settings.CustomPhotoshopSplashImagePath)
            or nameof(Settings.AllowRemoteAssetDownload))
        {
            SyncBackgroundModeBoxes();
            UpdateBackgroundPanels();
            UpdateVideoDecoderVisibility();
            _ = RefreshAssetStatusAsync();
        }

        if (e.PropertyName is nameof(Settings.CustomImageFolderPath)
            or nameof(Settings.ImageFolderMode))
        {
            UpdateFolderImageStatus();
        }
    }

    /// <summary>
    /// 按当前样式与背景来源显示对应的设置面板。
    /// </summary>
    private void UpdateBackgroundPanels()
    {
        var asSource = Plugin.Settings.AndroidStudioBackgroundSource;
        var psSource = Plugin.Settings.PhotoshopBackgroundSource;

        AsBuiltInPanel.IsVisible = asSource == 0;
        AsCustomPanel.IsVisible = asSource == 1;
        AsFolderPanel.IsVisible = asSource == 2;

        PsCustomPanel.IsVisible = psSource == 1;
        PsFolderPanel.IsVisible = psSource == 2;
    }

    /// <summary>
    /// 网络获取超时只对需要网络背景的样式显示。
    /// </summary>
    private void UpdateNetworkTimeoutVisibility() =>
        NetworkTimeoutExpander.IsVisible = Plugin.Settings.SplashKind is 1 or 7;

    /// <summary>
    /// 视频解码器设置对所有支持自定义背景的样式显示，避免切到自定义后找不到下载入口。
    /// </summary>
    private void UpdateVideoDecoderVisibility() =>
        VideoDecoderExpander.IsVisible = Plugin.Settings.SplashKind is 1 or 7;

    /// <summary>
    /// 刷新 FFmpeg 查找结果。
    /// </summary>
    private void RefreshFfmpegStatus()
    {
        var path = FfmpegProvider.Resolve(Plugin.Settings.FfmpegPath);
        FfmpegStatusText.Text = path != null
            ? $"已找到 FFmpeg：{path}"
            : "未找到 FFmpeg。视频背景将无法播放，请下载解码器或指定已安装的 FFmpeg 路径（插件不包含解码器）。";
    }

    private void UpdateFolderImageStatus()
    {
        var text = GetFolderImageStatusText();
        AsFolderImageStatusText.Text = text;
        PsFolderImageStatusText.Text = text;
    }

    private string GetFolderImageStatusText()
    {
        if (!Plugin.IsFolderBackgroundActive)
        {
            return "未选择文件夹轮播，将使用上面的背景设置。";
        }

        var images = ImageFolderProvider.GetImages(Plugin.Settings.CustomImageFolderPath);
        if (images.Count == 0)
        {
            return "文件夹不存在或其中没有支持的图片或视频（png/jpg/jpeg/webp/bmp/gif/mp4/webm/mov/mkv）。";
        }

        var current = Plugin.FolderSplashImagePath;
        return current != null && File.Exists(current)
            ? $"找到 {images.Count} 个文件；本次启动使用：{Path.GetFileName(current)}。"
            : $"找到 {images.Count} 个文件；重启应用后开始使用。";
    }

    private SplashAsset? GetSelectedAsset() => Plugin.Settings.SplashKind switch
    {
        1 => Plugin.ResolveAndroidStudioAsset(),
        7 => Plugin.Settings.PhotoshopBackgroundSource == 0 ? SplashAssetProvider.PhotoshopAsset : null,
        _ => null
    };

    private bool _syncingCornerRadius;

    /// <summary>
    /// 把实际生效的圆角（自定义值或当前样式默认值）显示到输入框。
    /// </summary>
    private void SyncCornerRadiusBox()
    {
        _syncingCornerRadius = true;
        CornerRadiusBox.Value = Plugin.ResolveCornerRadius();
        _syncingCornerRadius = false;
    }

    private void OnCornerRadiusValueChanged(object? sender, NumberBoxValueChangedEventArgs e)
    {
        if (_syncingCornerRadius || double.IsNaN(e.NewValue))
        {
            return;
        }

        Plugin.Settings.CornerRadius = e.NewValue;
    }

    /// <summary>
    /// 复用真实启动屏幕的界面内容打开静态预览，方便在设置后直接观察样式。
    /// </summary>
    private void OnPreviewSplashClick(object? sender, RoutedEventArgs e)
    {
        var splashService = IAppHost.GetService<ISplashService>();
        Window? splash = Plugin.Settings.SplashKind switch
        {
            1 => new AndroidStudioSplashScreen(splashService, Plugin),
            2 => new Microsoft365SplashScreen(splashService),
            3 => new OfficeLegacySplashScreen(splashService),
            6 => new IslandSplashScreen(splashService, Plugin),
            7 => new AdobePhotoshopSplashScreen(splashService, Plugin),
            8 => new AllsSplashScreen(splashService),
            _ => null
        };

        if (splash?.Content is not Control content)
        {
            PreviewHintText.Text = "“应用默认”启动屏幕由 ClassIsland 自身绘制，暂不支持预览。";
            return;
        }

        splash.Content = null;
        var width = double.IsFinite(splash.Width) && splash.Width > 0 ? splash.Width : 900;
        var height = double.IsFinite(splash.Height) && splash.Height > 0 ? splash.Height : 506;
        var preview = new Window
        {
            Title = "启动画面预览",
            Width = Math.Clamp(width, 320, 1600),
            Height = Math.Clamp(height, 80, 900),
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = splash.Background,
            ShowInTaskbar = false,
            RequestedThemeVariant = splash.RequestedThemeVariant
        };

        // 复制窗口级的继承属性，避免文字颜色等在原窗口上定义的样式丢失
        preview.SetValue(TextElement.ForegroundProperty, splash.GetValue(TextElement.ForegroundProperty));

        IReadOnlyList<string> folderImages = Plugin.IsFolderBackgroundActive
            ? ImageFolderProvider.GetImages(Plugin.Settings.CustomImageFolderPath)
            : Array.Empty<string>();
        DispatcherTimer? carouselTimer = null;
        if (folderImages.Count > 0)
        {
            // 底部切换栏需要额外的固定高度，避免挤压启动画面本身
            preview.Height = Math.Min(preview.Height + 48, 900);
            var grid = new Grid { RowDefinitions = new RowDefinitions("*,Auto") };
            Grid.SetRow(content, 0);
            grid.Children.Add(content);

            var counter = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
            var previousButton = new Button { Content = "上一张" };
            var nextButton = new Button { Content = "下一张" };
            var bar = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Spacing = 8,
                Margin = new Thickness(0, 8)
            };
            bar.Children.Add(previousButton);
            bar.Children.Add(counter);
            bar.Children.Add(nextButton);
            Grid.SetRow(bar, 1);
            grid.Children.Add(bar);
            preview.Content = grid;

            var index = 0;
            void Apply(int target)
            {
                index = CarouselStepper.Move(target, folderImages.Count, 0);
                var applied = splash switch
                {
                    AndroidStudioSplashScreen androidStudio => androidStudio.ApplyPreviewImage(folderImages[index]),
                    AdobePhotoshopSplashScreen photoshop => photoshop.ApplyPreviewImage(folderImages[index]),
                    _ => false
                };
                counter.Text = $"{index + 1} / {folderImages.Count}" + (applied ? "" : "（无法预览该文件）");
            }

            previousButton.Click += (_, _) => Apply(CarouselStepper.Move(index, folderImages.Count, -1));
            nextButton.Click += (_, _) => Apply(CarouselStepper.Move(index, folderImages.Count, 1));
            Apply(0);

            // 自动轮播与真实启动的“每次启动换一张”节奏无关，仅用于在预览里展示切换效果
            if (folderImages.Count > 1)
            {
                carouselTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
                carouselTimer.Tick += (_, _) => Apply(CarouselStepper.Move(index, folderImages.Count, 1));
                carouselTimer.Start();
            }

            PreviewHintText.Text = folderImages.Count > 1
                ? $"正在轮播预览：共 {folderImages.Count} 张，每 3 秒自动切换，也可用下方按钮手动切换。"
                : "文件夹内只有 1 张可用的背景，切换方式不会产生可见变化。";
        }
        else
        {
            preview.Content = content;
            PreviewHintText.Text = Plugin.IsFolderBackgroundActive
                ? "背景文件夹中没有可用的图片或视频。"
                : "正在显示静态预览，关闭预览窗口即可返回。";
        }

        // 预览用的是真实启动画面实例，但它从未显示过，不会触发自身的 Closed，
        // 需要在预览关闭时手动释放动图/视频播放器与事件订阅。
        preview.Closed += (_, _) =>
        {
            carouselTimer?.Stop();
            ReleaseSplashInstance(splash);
        };

        if (TopLevel.GetTopLevel(this) is Window owner)
        {
            _ = preview.ShowDialog(owner);
        }
        else
        {
            preview.Show();
        }
    }

    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private CancellationTokenSource? _ffmpegDownloadCts;
    private bool _syncingBackgroundMode;

    /// <summary>
    /// 释放预览使用的启动画面实例持有的资源（动图/视频播放器、事件订阅）。
    /// </summary>
    private static void ReleaseSplashInstance(Window splash)
    {
        switch (splash)
        {
            case AndroidStudioSplashScreen androidStudioSplash:
                androidStudioSplash.ReleaseSplashAssets();
                break;
            case AdobePhotoshopSplashScreen photoshopSplash:
                photoshopSplash.ReleaseSplashAssets();
                break;
            case Microsoft365SplashScreen microsoft365Splash:
                microsoft365Splash.ReleaseSplashAssets();
                break;
            case OfficeLegacySplashScreen officeLegacySplash:
                officeLegacySplash.ReleaseSplashAssets();
                break;
        }
    }

    /// <summary>
    /// 把设置中的背景来源同步到胶囊选择器。
    /// </summary>
    private void SyncBackgroundModeBoxes()
    {
        _syncingBackgroundMode = true;
        AsBackgroundModeBox.SelectedIndex =
            Math.Clamp(Plugin.Settings.AndroidStudioBackgroundSource, 0, 2);
        PsBackgroundModeBox.SelectedIndex =
            Math.Clamp(Plugin.Settings.PhotoshopBackgroundSource, 0, 2);
        _syncingBackgroundMode = false;
    }

    private void OnAsBackgroundModeChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_syncingBackgroundMode || e.AddedItems.Count == 0)
        {
            return;
        }

        var index = AsBackgroundModeBox.Items.IndexOf(e.AddedItems[0]!);
        if (index < 0)
        {
            return;
        }

        Plugin.Settings.AndroidStudioBackgroundSource = index;
        UpdateBackgroundPanels();
        UpdateFolderImageStatus();
        _ = RefreshAssetStatusAsync();
    }

    private void OnPsBackgroundModeChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_syncingBackgroundMode || e.AddedItems.Count == 0)
        {
            return;
        }

        var index = PsBackgroundModeBox.Items.IndexOf(e.AddedItems[0]!);
        if (index < 0)
        {
            return;
        }

        Plugin.Settings.PhotoshopBackgroundSource = index;
        UpdateBackgroundPanels();
        UpdateFolderImageStatus();
        _ = RefreshAssetStatusAsync();
    }

    private async Task RefreshAssetStatusAsync()
    {
        // 切换选型可能连续触发多次，只保留一次检查/下载
        if (!await _refreshLock.WaitAsync(0))
        {
            return;
        }

        try
        {
            var (ready, total) = SplashAssetProvider.GetCacheStatus();

            if (Plugin.Settings.SplashKind == 7 &&
                Plugin.Settings.PhotoshopBackgroundSource == 1 &&
                !Plugin.IsCustomPhotoshopImageAvailable)
            {
                AssetStatusText.Text = "已启用自定义 Photoshop 背景，但图片文件不存在，启动时将回退到内置背景。";
                return;
            }

            var asset = GetSelectedAsset();

            if (asset == null)
            {
                AssetStatusText.Text = $"当前启动屏幕不需要内置背景素材。已缓存 {ready}/{total} 张。";
                return;
            }

            if (SplashAssetProvider.GetCachedPath(asset) != null)
            {
                AssetStatusText.Text = $"背景素材“{asset.Name}”已缓存并通过 SHA-256 校验（已缓存 {ready}/{total} 张）。";
                return;
            }

            if (!Plugin.Settings.AllowRemoteAssetDownload)
            {
                AssetStatusText.Text =
                    $"背景素材“{asset.Name}”未缓存，且已关闭自动下载，启动时将显示占位背景（已缓存 {ready}/{total} 张）。";
                return;
            }

            // 选型时等待下载完成，避免重启应用时素材还没下载好
            AssetStatusText.Text = $"正在从原版仓库下载背景素材“{asset.Name}”，请稍候…";
            IsEnabled = false;
            string? path;
            try
            {
                path = await SplashAssetProvider.EnsureAsync(asset, true);
            }
            finally
            {
                IsEnabled = true;
            }

            AssetStatusText.Text = path != null
                ? $"背景素材“{asset.Name}”下载完成并通过 SHA-256 校验，重启应用后即可使用。"
                : $"背景素材“{asset.Name}”下载失败，启动时将显示占位背景。";
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private async void OnCheckAssetsClick(object? sender, RoutedEventArgs e)
    {
        await RefreshAssetStatusAsync();
    }

    private async void OnBrowsePhotoshopImageClick(object? sender, RoutedEventArgs e)
    {
        var storageProvider = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storageProvider == null)
        {
            return;
        }

        var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "选择 Adobe Photoshop 启动屏幕背景图片或视频",
            AllowMultiple = false,
            FileTypeFilter = [FilePickerFileTypes.ImageAll, VideoFileType]
        });

        if (files.Count > 0)
        {
            Plugin.Settings.CustomPhotoshopSplashImagePath =
                files[0].TryGetLocalPath() ?? files[0].Path.LocalPath;
        }
    }

    private async void OnBrowseAndroidStudioImageClick(object? sender, RoutedEventArgs e)
    {
        var storageProvider = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storageProvider == null)
        {
            return;
        }

        var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "选择 Android Studio 启动屏幕背景图片或视频",
            AllowMultiple = false,
            FileTypeFilter = [FilePickerFileTypes.ImageAll, VideoFileType]
        });

        if (files.Count > 0)
        {
            Plugin.Settings.CustomAndroidStudioSplashImagePath =
                files[0].TryGetLocalPath() ?? files[0].Path.LocalPath;
        }
    }

    private async void OnBrowseFolderClick(object? sender, RoutedEventArgs e)
    {
        var storageProvider = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storageProvider == null)
        {
            return;
        }

        var folders = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "选择存放启动背景图片的文件夹",
            AllowMultiple = false
        });

        if (folders.Count > 0)
        {
            Plugin.Settings.CustomImageFolderPath =
                folders[0].TryGetLocalPath() ?? folders[0].Path.LocalPath;
        }
    }

    private async void OnBrowseFfmpegClick(object? sender, RoutedEventArgs e)
    {
        var storageProvider = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storageProvider == null)
        {
            return;
        }

        var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "选择 FFmpeg 可执行文件",
            AllowMultiple = false
        });

        if (files.Count > 0)
        {
            Plugin.Settings.FfmpegPath = files[0].TryGetLocalPath() ?? files[0].Path.LocalPath;
        }
    }

    private async void OnDownloadFfmpegClick(object? sender, RoutedEventArgs e)
    {
        if (_ffmpegDownloadCts != null)
        {
            return;
        }

        var source = FfmpegProvider.GetDownloadSource();
        if (source == null)
        {
            FfmpegStatusText.Text = "当前平台暂不支持自动下载 FFmpeg，请手动安装后在“FFmpeg 路径”中指定。";
            return;
        }

        var cts = new CancellationTokenSource();
        _ffmpegDownloadCts = cts;
        FfmpegDownloadButton.IsEnabled = false;
        FfmpegCancelButton.IsVisible = true;
        FfmpegProgressBar.IsVisible = true;
        FfmpegProgressBar.IsIndeterminate = false;
        FfmpegProgressBar.Value = 0;
        FfmpegStatusText.Text = "正在下载 FFmpeg，请稍候…";

        try
        {
            var progress = new Progress<double>(value =>
            {
                FfmpegProgressBar.Value = value;
                FfmpegStatusText.Text = $"正在下载 FFmpeg… {value:0}%";
            });

            var path = await FfmpegProvider.DownloadAsync(progress, cts.Token);
            if (path != null)
            {
                FfmpegProgressBar.Value = 100;
                FfmpegStatusText.Text = $"FFmpeg 已下载到：{path}";
                RefreshFfmpegStatus();
            }
            else
            {
                FfmpegStatusText.Text = "下载或解压 FFmpeg 失败，请检查网络后重试，或手动指定已安装的 FFmpeg 路径。";
            }
        }
        catch (OperationCanceledException)
        {
            FfmpegStatusText.Text = "已取消下载 FFmpeg。";
        }
        finally
        {
            _ffmpegDownloadCts = null;
            cts.Dispose();
            FfmpegCancelButton.IsVisible = false;
            FfmpegDownloadButton.IsEnabled = true;
            FfmpegProgressBar.IsVisible = false;
        }
    }

    private void OnCancelFfmpegClick(object? sender, RoutedEventArgs e)
    {
        _ffmpegDownloadCts?.Cancel();
        FfmpegStatusText.Text = "正在取消下载…";
    }
}
