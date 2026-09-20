using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace VisionWeave.App.Preview;

/// <summary>Shows one preview bitmap at one image pixel per display pixel.</summary>
internal sealed class FullImageWindow : Window
{
    internal FullImageWindow(BitmapSource image, string nodeTitle)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(nodeTitle);
        Title = $"{nodeTitle} — Image Preview";
        Width = Math.Min(Math.Max(image.PixelWidth + 48, 640), 1400);
        Height = Math.Min(Math.Max(image.PixelHeight + 72, 480), 900);
        MinWidth = 480;
        MinHeight = 360;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = (Brush)System.Windows.Application.Current.FindResource("Brush.Background.Canvas");
        Content = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = new Image
            {
                Source = image,
                Stretch = Stretch.None,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
        };
    }
}
