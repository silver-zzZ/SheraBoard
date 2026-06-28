using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace SheraBoard.App;

public partial class ImagePreviewWindow : Window
{
    private readonly TranslateTransform _translateTransform = new();
    private readonly double _imageWidth;
    private readonly double _imageHeight;
    private double _minScale = 0.001;
    private double _scale = 1;
    private bool _isDragging;
    private System.Windows.Point _lastDragPoint;

    public ImagePreviewWindow(ImageSource imageSource, string title)
    {
        InitializeComponent();
        Title = title;
        PreviewImage.Source = imageSource;
        _imageWidth = ImageWidth(imageSource);
        _imageHeight = ImageHeight(imageSource);
        PreviewImage.Width = _imageWidth;
        PreviewImage.Height = _imageHeight;
        RenderOptions.SetBitmapScalingMode(PreviewImage, BitmapScalingMode.HighQuality);
        PreviewImage.RenderTransform = _translateTransform;

        Loaded += (_, _) => QueueFitToScreen();
        ContentRendered += (_, _) => QueueFitToScreen();
    }

    private void OverlayRoot_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var oldScale = _scale;
        var factor = e.Delta > 0 ? 1.12 : 1 / 1.12;
        _scale = Math.Clamp(_scale * factor, _minScale, 10);
        ApplyImageScale();

        var point = e.GetPosition(OverlayRoot);
        var centerX = ActualWidth / 2 + _translateTransform.X;
        var centerY = ActualHeight / 2 + _translateTransform.Y;
        var ratio = oldScale <= 0 ? 1 : _scale / oldScale;
        _translateTransform.X += (centerX - point.X) * (1 - ratio);
        _translateTransform.Y += (centerY - point.Y) * (1 - ratio);
        e.Handled = true;
    }

    private void PreviewImage_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _isDragging = true;
        _lastDragPoint = e.GetPosition(OverlayRoot);
        PreviewImage.CaptureMouse();
        e.Handled = true;
    }

    private void PreviewImage_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_isDragging)
        {
            return;
        }

        var point = e.GetPosition(OverlayRoot);
        _translateTransform.X += point.X - _lastDragPoint.X;
        _translateTransform.Y += point.Y - _lastDragPoint.Y;
        _lastDragPoint = point;
    }

    private void PreviewImage_MouseLeftButtonUp(object sender, System.Windows.Input.MouseEventArgs e)
    {
        _isDragging = false;
        PreviewImage.ReleaseMouseCapture();
    }

    private void PreviewImage_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            FitInitially();
            e.Handled = true;
        }
    }

    private void OverlayRoot_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (ReferenceEquals(e.OriginalSource, OverlayRoot))
        {
            Close();
        }
    }

    private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
        }
    }

    private void FitInitially()
    {
        if (PreviewImage.Source is null)
        {
            return;
        }

        var viewportWidth = OverlayRoot.ActualWidth > 0 ? OverlayRoot.ActualWidth : ActualWidth;
        var viewportHeight = OverlayRoot.ActualHeight > 0 ? OverlayRoot.ActualHeight : ActualHeight;
        if (viewportWidth <= 0 || viewportHeight <= 0 || _imageWidth <= 0 || _imageHeight <= 0)
        {
            return;
        }

        // Keep a quiet edge gutter like modern lightbox viewers. Do not enforce
        // a fixed minimum fit scale: very long/tall screenshots must be allowed
        // to shrink below 8%, otherwise their edges look cut off.
        var availableWidth = Math.Max(80, viewportWidth - 56);
        var availableHeight = Math.Max(80, viewportHeight - 56);
        var fitScale = Math.Min(availableWidth / _imageWidth, availableHeight / _imageHeight);
        if (double.IsNaN(fitScale) || double.IsInfinity(fitScale) || fitScale <= 0)
        {
            fitScale = 1;
        }

        _scale = Math.Min(1, fitScale);
        _minScale = Math.Max(0.0001, _scale * 0.25);
        ApplyImageScale();
        _translateTransform.X = 0;
        _translateTransform.Y = 0;
    }

    private void ApplyImageScale()
    {
        PreviewImage.Width = Math.Max(1, _imageWidth * _scale);
        PreviewImage.Height = Math.Max(1, _imageHeight * _scale);
    }

    private void QueueFitToScreen()
    {
        Dispatcher.BeginInvoke(FitInitially, DispatcherPriority.ApplicationIdle);
    }

    private static double ImageWidth(ImageSource imageSource)
    {
        return imageSource is BitmapSource bitmap && bitmap.PixelWidth > 0
            ? bitmap.PixelWidth
            : imageSource.Width;
    }

    private static double ImageHeight(ImageSource imageSource)
    {
        return imageSource is BitmapSource bitmap && bitmap.PixelHeight > 0
            ? bitmap.PixelHeight
            : imageSource.Height;
    }
}
