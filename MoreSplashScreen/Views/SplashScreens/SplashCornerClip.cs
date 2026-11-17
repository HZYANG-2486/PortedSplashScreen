using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace PortedSplashScreen.Views.SplashScreens;

/// <summary>
/// 让启动屏幕的根容器把子元素按圆角裁切，避免插画/背景图用方角盖住圆角。
/// </summary>
internal static class SplashCornerClip
{
    public static void Attach(Border border, double radius)
    {
        border.ClipToBounds = true;

        void Update()
        {
            var size = border.Bounds.Size;
            border.Clip = radius <= 0 || size.Width <= 0 || size.Height <= 0
                ? null
                : new RectangleGeometry(new Rect(size), radius, radius);
        }

        border.SizeChanged += (_, _) => Update();
        Update();
    }
}
