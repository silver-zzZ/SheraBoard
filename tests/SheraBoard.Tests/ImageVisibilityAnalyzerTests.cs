using SheraBoard.Core.Imaging;

namespace SheraBoard.Tests;

public sealed class ImageVisibilityAnalyzerTests
{
    [Fact]
    public void AnalyzeBgra32TreatsTransparentRgbContentAsRenderableClipboardPreview()
    {
        var pixels = TransparentWhitePixels(24);
        for (var pixel = 0; pixel < 10; pixel++)
        {
            var offset = pixel * 4;
            pixels[offset] = 0;       // B
            pixels[offset + 1] = 255; // G
            pixels[offset + 2] = 255; // R
            pixels[offset + 3] = 0;   // A, Excel clipboard previews can arrive fully transparent.
        }

        var analysis = ImageVisibilityAnalyzer.AnalyzeBgra32(pixels);

        Assert.True(analysis.HasRenderableContent);
        Assert.True(analysis.ShouldForceOpaqueForDisplay);
    }

    [Fact]
    public void AnalyzeBgra32KeepsBlankTransparentWhiteImageHidden()
    {
        var analysis = ImageVisibilityAnalyzer.AnalyzeBgra32(TransparentWhitePixels(24));

        Assert.False(analysis.HasRenderableContent);
        Assert.False(analysis.ShouldForceOpaqueForDisplay);
    }

    [Fact]
    public void AnalyzeBgra32DoesNotForceOpaqueWhenImageAlreadyHasVisibleAlpha()
    {
        var pixels = TransparentWhitePixels(24);
        for (var pixel = 0; pixel < 10; pixel++)
        {
            var offset = pixel * 4;
            pixels[offset] = 0;
            pixels[offset + 1] = 0;
            pixels[offset + 2] = 0;
            pixels[offset + 3] = 255;
        }

        var analysis = ImageVisibilityAnalyzer.AnalyzeBgra32(pixels);

        Assert.True(analysis.HasRenderableContent);
        Assert.False(analysis.ShouldForceOpaqueForDisplay);
    }

    private static byte[] TransparentWhitePixels(int pixelCount)
    {
        var pixels = new byte[pixelCount * 4];
        for (var i = 0; i < pixels.Length; i += 4)
        {
            pixels[i] = 255;
            pixels[i + 1] = 255;
            pixels[i + 2] = 255;
            pixels[i + 3] = 0;
        }

        return pixels;
    }
}
