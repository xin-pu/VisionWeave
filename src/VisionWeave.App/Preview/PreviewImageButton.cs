using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace VisionWeave.App.Preview;

/// <summary>
/// Opens its bound preview image in a dedicated, scrollable window when clicked.
/// </summary>
public sealed class PreviewImageButton : Button
{
    /// <summary>Identifies the image shown by the full-image viewer.</summary>
    public static readonly DependencyProperty ImageSourceProperty = DependencyProperty.Register(
        nameof(ImageSource),
        typeof(BitmapSource),
        typeof(PreviewImageButton));

    /// <summary>Identifies the title shown by the full-image viewer.</summary>
    public static readonly DependencyProperty ImageTitleProperty = DependencyProperty.Register(
        nameof(ImageTitle),
        typeof(string),
        typeof(PreviewImageButton),
        new PropertyMetadata(string.Empty));

    /// <summary>Gets or sets the bitmap opened when the button is clicked.</summary>
    public BitmapSource? ImageSource
    {
        get => (BitmapSource?)GetValue(ImageSourceProperty);
        set => SetValue(ImageSourceProperty, value);
    }

    /// <summary>Gets or sets the node title shown by the viewer.</summary>
    public string ImageTitle
    {
        get => (string)GetValue(ImageTitleProperty);
        set => SetValue(ImageTitleProperty, value);
    }

    /// <inheritdoc />
    protected override void OnClick()
    {
        base.OnClick();
        if (ImageSource is { } image)
        {
            new FullImageWindow(image, ImageTitle) { Owner = Window.GetWindow(this) }.Show();
        }
    }
}
