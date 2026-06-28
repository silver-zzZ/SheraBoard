namespace SheraBoard.Tests;

public sealed class IconAssetTests
{
    [Fact]
    public void AppIconPngIsLargeTransparentAsset()
    {
        var path = FindProjectFile("src", "SheraBoard.App", "Assets", "AppIcon.png");
        var bytes = File.ReadAllBytes(path);

        Assert.Equal(0x89, bytes[0]);
        Assert.Equal((byte)'P', bytes[1]);
        Assert.Equal((byte)'N', bytes[2]);
        Assert.Equal((byte)'G', bytes[3]);

        var width = ReadBigEndianInt32(bytes, 16);
        var height = ReadBigEndianInt32(bytes, 20);
        var colorType = bytes[25];

        Assert.Equal(1024, width);
        Assert.Equal(1024, height);
        Assert.Equal(6, colorType); // RGBA
    }

    [Fact]
    public void AppIconIcoContainsSmallAndLargeTaskbarSizes()
    {
        var path = FindProjectFile("src", "SheraBoard.App", "Assets", "AppIcon.ico");
        var bytes = File.ReadAllBytes(path);

        Assert.Equal(0, BitConverter.ToUInt16(bytes, 0));
        Assert.Equal(1, BitConverter.ToUInt16(bytes, 2));

        var count = BitConverter.ToUInt16(bytes, 4);
        var sizes = new HashSet<int>();
        for (var i = 0; i < count; i++)
        {
            var offset = 6 + i * 16;
            var width = bytes[offset] == 0 ? 256 : bytes[offset];
            var height = bytes[offset + 1] == 0 ? 256 : bytes[offset + 1];
            if (width == height)
            {
                sizes.Add(width);
            }
        }

        foreach (var expected in new[] { 16, 24, 32, 48, 64, 128, 256 })
        {
            Assert.Contains(expected, sizes);
        }
    }

    [Fact]
    public void AppIconIcoFillsEnoughHorizontalSpaceForTrayFlyout()
    {
        var pngPath = FindProjectFile("src", "SheraBoard.App", "Assets", "AppIcon.png");
        var bytes = File.ReadAllBytes(pngPath);

        // Regression guard for the hidden-tray flyout looking tiny: the icon
        // artwork should not be a narrow object floating in a large transparent
        // square. The PNG and derived ICO should fill most of the 1024 canvas.
        var (width, height) = ReadPngAlphaBounds(bytes);

        Assert.True(width >= 760, $"Icon visible width should be >= 760px, was {width}px.");
        Assert.True(height >= 900, $"Icon visible height should be >= 900px, was {height}px.");
    }

    [Fact]
    public void TrayIconLoadsPackIconResourceBeforeExecutableFallback()
    {
        var source = File.ReadAllText(FindProjectFile("src", "SheraBoard.App", "Services", "TrayIconService.cs"));

        var packIconIndex = source.IndexOf("pack://application:,,,/Assets/AppIcon.ico", StringComparison.Ordinal);
        var fallbackIndex = source.IndexOf("Icon.ExtractAssociatedIcon", StringComparison.Ordinal);

        Assert.True(packIconIndex >= 0);
        Assert.True(fallbackIndex > packIconIndex);
    }

    [Fact]
    public void MainHeaderUsesBalancedAppIconPresentation()
    {
        var xaml = File.ReadAllText(FindProjectFile("src", "SheraBoard.App", "MainWindow.xaml"));

        Assert.Contains("x:Name=\"HeaderAppIcon\"", xaml);
        Assert.Contains("Width=\"22\"", xaml);
        Assert.Contains("Height=\"22\"", xaml);
        Assert.DoesNotContain("Width=\"24\" Height=\"24\" Margin=\"0,0,8,0\"", xaml);
    }

    private static int ReadBigEndianInt32(byte[] bytes, int offset)
    {
        return bytes[offset] << 24 |
               bytes[offset + 1] << 16 |
               bytes[offset + 2] << 8 |
               bytes[offset + 3];
    }

    private static (int Width, int Height) ReadPngAlphaBounds(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        using var image = new System.Drawing.Bitmap(stream);
        var minX = image.Width;
        var minY = image.Height;
        var maxX = -1;
        var maxY = -1;

        for (var y = 0; y < image.Height; y++)
        {
            for (var x = 0; x < image.Width; x++)
            {
                if (image.GetPixel(x, y).A <= 8)
                {
                    continue;
                }

                minX = Math.Min(minX, x);
                minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x);
                maxY = Math.Max(maxY, y);
            }
        }

        return maxX < minX || maxY < minY
            ? (0, 0)
            : (maxX - minX + 1, maxY - minY + 1);
    }

    private static string FindProjectFile(params string[] relativeParts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(new[] { directory.FullName }.Concat(relativeParts).ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not locate {Path.Combine(relativeParts)} from {AppContext.BaseDirectory}.");
    }
}
