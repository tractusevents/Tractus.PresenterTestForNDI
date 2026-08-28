using System.Drawing;
using System.Drawing.Imaging;
using Tractus.PresenterTest;
using Xunit;

namespace TractusPresenterTest.Tests;

public sealed class PresenterImageLoaderTests
{
    [Fact]
    public void CustomImageIsCenterCroppedToRequestedFrameSize()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var sourcePath = Path.Combine(directory, "square.png");
            using (var source = new Bitmap(100, 100))
            using (var graphics = Graphics.FromImage(source))
            {
                graphics.Clear(Color.DarkRed);
                source.Save(sourcePath, ImageFormat.Png);
            }

            var pixels = PresenterImageLoader.LoadBgra(1, 16, 9, sourcePath);

            Assert.Equal(16 * 9 * 4, pixels.Length);
            Assert.Equal(sourcePath, PresenterImageLoader.ResolvePath(1, sourcePath));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void ImportedImageIsNormalizedToFullHdPng()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var sourcePath = Path.Combine(directory, "portrait.bmp");
            using (var source = new Bitmap(60, 100))
            {
                source.Save(sourcePath, ImageFormat.Bmp);
            }

            var importedPath = PresenterImageLoader.ImportCustomImage(3, sourcePath, directory);
            using var imported = Image.FromFile(importedPath);

            Assert.EndsWith(".png", importedPath, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(1920, imported.Width);
            Assert.Equal(1080, imported.Height);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void MissingCustomImageFallsBackToBundledSilhouette()
    {
        var resolved = PresenterImageLoader.ResolvePath(4, @"Z:\definitely-missing\presenter.png");
        Assert.Equal(PresenterImageLoader.GetDefaultPath(4), resolved);
    }

    private static string CreateTemporaryDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "TractusPresenterTest", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
