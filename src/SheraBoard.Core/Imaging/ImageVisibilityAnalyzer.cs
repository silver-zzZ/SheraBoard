namespace SheraBoard.Core.Imaging;

public readonly record struct ImageVisibilityAnalysis(
    bool HasRenderableContent,
    bool ShouldForceOpaqueForDisplay);

public static class ImageVisibilityAnalyzer
{
    private const int BytesPerPixel = 4;
    private const int AlphaVisibleThreshold = 8;
    private const int WhiteThreshold = 245;
    private const int MinimumContentPixels = 8;

    public static ImageVisibilityAnalysis AnalyzeBgra32(ReadOnlySpan<byte> pixels)
    {
        var pixelCount = pixels.Length / BytesPerPixel;
        if (pixelCount <= 0)
        {
            return new ImageVisibilityAnalysis(false, false);
        }

        var visibleAlphaPixels = 0;
        var rgbContentPixels = 0;
        byte minBlue = byte.MaxValue;
        byte minGreen = byte.MaxValue;
        byte minRed = byte.MaxValue;
        byte maxBlue = byte.MinValue;
        byte maxGreen = byte.MinValue;
        byte maxRed = byte.MinValue;

        for (var i = 0; i + 3 < pixels.Length; i += BytesPerPixel)
        {
            var blue = pixels[i];
            var green = pixels[i + 1];
            var red = pixels[i + 2];
            var alpha = pixels[i + 3];

            if (alpha > AlphaVisibleThreshold)
            {
                visibleAlphaPixels++;
            }

            if (red < WhiteThreshold || green < WhiteThreshold || blue < WhiteThreshold)
            {
                rgbContentPixels++;
            }

            minBlue = Math.Min(minBlue, blue);
            minGreen = Math.Min(minGreen, green);
            minRed = Math.Min(minRed, red);
            maxBlue = Math.Max(maxBlue, blue);
            maxGreen = Math.Max(maxGreen, green);
            maxRed = Math.Max(maxRed, red);
        }

        if (visibleAlphaPixels >= MinimumContentPixels)
        {
            return new ImageVisibilityAnalysis(true, false);
        }

        var hasTransparentRgbContent =
            rgbContentPixels >= MinimumContentPixels &&
            (rgbContentPixels <= pixelCount - MinimumContentPixels ||
             maxBlue - minBlue > 2 ||
             maxGreen - minGreen > 2 ||
             maxRed - minRed > 2);

        return new ImageVisibilityAnalysis(
            hasTransparentRgbContent,
            ShouldForceOpaqueForDisplay: hasTransparentRgbContent);
    }
}
