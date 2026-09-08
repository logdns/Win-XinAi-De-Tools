using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using SkiaSharp;
using Windows.UI.ViewManagement;

namespace PortManager.Controls;

/// <summary>DPI-aware Skia chart, rendered only when data, size, or theme changes.</summary>
public sealed class RuleDistributionChart : UserControl
{
    private readonly Image _image = new();
    private readonly AccessibilitySettings _accessibility = new();
    private readonly UISettings _colors = new();
    private string? _renderKey;
    private int _inbound;
    private int _outbound;
    private XamlRoot? _root;

    public RuleDistributionChart()
    {
        Content = _image;
        IsHitTestVisible = false;
        Loaded += (_, _) =>
        {
            _root = XamlRoot;
            _root.Changed += RootChanged;
            _colors.ColorValuesChanged += SystemColorsChanged;
            Render();
        };
        Unloaded += (_, _) =>
        {
            if (_root is not null) _root.Changed -= RootChanged;
            _colors.ColorValuesChanged -= SystemColorsChanged;
            _root = null;
            _image.Source = null;
            _renderKey = null;
        };
        SizeChanged += (_, _) => Render();
        ActualThemeChanged += (_, _) => Render();
    }

    public void SetCounts(int inbound, int outbound)
    {
        _inbound = inbound;
        _outbound = outbound;
        Render();
    }

    private void RootChanged(XamlRoot sender, XamlRootChangedEventArgs args) => Render();

    private void SystemColorsChanged(UISettings sender, object args) => DispatcherQueue.TryEnqueue(() =>
    {
        _renderKey = null;
        Render();
    });

    private void Render()
    {
        if (_root is null || ActualWidth <= 0 || ActualHeight <= 0 || !_root.IsHostVisible) return;
        var scale = _root.RasterizationScale;
        var width = Math.Clamp((int)Math.Ceiling(ActualWidth * scale), 1, 2048);
        var height = Math.Clamp((int)Math.Ceiling(ActualHeight * scale), 1, 1024);
        var key = $"{width}:{height}:{ActualTheme}:{_accessibility.HighContrast}:{_inbound}:{_outbound}";
        if (key == _renderKey) return;
        using var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul));
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        canvas.Scale(width / (float)ActualWidth, height / (float)ActualHeight);
        var w = (float)ActualWidth;
        var h = (float)ActualHeight;
        var radius = Math.Min(h * .37f, w * .22f);
        var center = new SKPoint(w * .5f, h * .5f);
        var dark = ActualTheme == ElementTheme.Dark;
        var systemForeground = _colors.GetColorValue(UIColorType.Foreground);
        var contrastColor = new SKColor(systemForeground.R, systemForeground.G, systemForeground.B);
        using var paint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 16 };
        paint.Color = dark ? new SKColor(65, 70, 80) : new SKColor(222, 228, 237);
        if (_accessibility.HighContrast) paint.Color = contrastColor;
        canvas.DrawCircle(center, radius, paint);
        var total = (long)_inbound + _outbound;
        var bounds = new SKRect(center.X - radius, center.Y - radius, center.X + radius, center.Y + radius);
        if (total > 0)
        {
            var sweep = 360f * _inbound / total;
            paint.Color = dark ? new SKColor(113, 190, 255) : new SKColor(0, 103, 192);
            if (_accessibility.HighContrast) paint.Color = contrastColor;
            canvas.DrawArc(bounds, -90, sweep, false, paint);
            paint.Color = dark ? new SKColor(100, 220, 190) : new SKColor(0, 128, 104);
            if (_accessibility.HighContrast) paint.Color = contrastColor;
            canvas.DrawArc(bounds, -90 + sweep, 360 - sweep, false, paint);
        }
        // Shield glyph is geometry so it remains sharp without a font dependency.
        using var shield = new SKPath();
        shield.MoveTo(center.X, center.Y - 21);
        shield.LineTo(center.X + 18, center.Y - 13);
        shield.LineTo(center.X + 16, center.Y + 9);
        shield.QuadTo(center.X + 10, center.Y + 21, center.X, center.Y + 26);
        shield.QuadTo(center.X - 10, center.Y + 21, center.X - 16, center.Y + 9);
        shield.LineTo(center.X - 18, center.Y - 13);
        shield.Close();
        paint.Color = dark ? new SKColor(215, 227, 243) : new SKColor(40, 59, 85);
        if (_accessibility.HighContrast) paint.Color = contrastColor;
        paint.StrokeWidth = 2.5f;
        canvas.DrawPath(shield, paint);
        canvas.Flush();

        var pixels = new byte[bitmap.ByteCount];
        Marshal.Copy(bitmap.GetPixels(), pixels, 0, pixels.Length);
        var output = new WriteableBitmap(width, height);
        using (var stream = output.PixelBuffer.AsStream()) stream.Write(pixels);
        output.Invalidate();
        _image.Source = output;
        _renderKey = key;
        App.LogStartup($"Skia chart rendered: {width}x{height}.");
    }
}
