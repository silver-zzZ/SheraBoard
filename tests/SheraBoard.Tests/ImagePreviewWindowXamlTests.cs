namespace SheraBoard.Tests;

public sealed class ImagePreviewWindowXamlTests
{
    [Fact]
    public void PreviewImageUsesFillStretchSoWheelZoomScalesInsteadOfCropping()
    {
        var xaml = File.ReadAllText(FindProjectFile("src", "SheraBoard.App", "ImagePreviewWindow.xaml"));

        Assert.Contains("x:Name=\"PreviewImage\"", xaml);
        Assert.Contains("Stretch=\"Fill\"", xaml);
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
