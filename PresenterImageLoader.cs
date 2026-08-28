using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace Tractus.PresenterTest;

public static class PresenterImageLoader
{
    public static string GetDefaultPath(int number) => Path.Combine(
        AppContext.BaseDirectory, "assets", "presenters-centered", $"presenter-{number}.png");

    public static string ResolvePath(int number, string? customPath = null) =>
        !string.IsNullOrWhiteSpace(customPath) && File.Exists(customPath)
            ? customPath
            : GetDefaultPath(number);

    public static string ImportCustomImage(int number, string sourcePath, string? storageDirectory = null)
    {
        var directory = storageDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Tractus", "Presenter Test for NDI", "images");
        Directory.CreateDirectory(directory);
        var destination = Path.Combine(directory, $"presenter-{number}-{DateTime.UtcNow:yyyyMMddHHmmssfff}.png");
        var temporary = destination + ".tmp";

        using (var source = new Bitmap(sourcePath))
        {
            ApplyExifOrientation(source);
            using var normalized = ScaleAndCrop(source, VideoSpec.Width, VideoSpec.Height);
            normalized.Save(temporary, ImageFormat.Png);
        }
        File.Move(temporary, destination, true);
        return destination;
    }

    public static byte[] LoadBgra(int number, int width, int height, string? customPath = null)
    {
        var path = ResolvePath(number, customPath);
        if (!File.Exists(path))
            throw new FileNotFoundException($"Presenter image {number} was not found.", path);

        using var source = new Bitmap(path);
        using var scaled = ScaleAndCrop(source, width, height);

        var pixels = new byte[width * height * 4];
        var bounds = new Rectangle(0, 0, width, height);
        var data = scaled.LockBits(bounds, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            var rowBytes = width * 4;
            for (var y = 0; y < height; y++)
                Marshal.Copy(data.Scan0 + y * data.Stride, pixels, y * rowBytes, rowBytes);
        }
        finally
        {
            scaled.UnlockBits(data);
        }
        return pixels;
    }

    private static Bitmap ScaleAndCrop(Image source, int width, int height)
    {
        var scaled = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        scaled.SetResolution(96, 96);
        using var graphics = Graphics.FromImage(scaled);
        graphics.CompositingMode = CompositingMode.SourceCopy;
        graphics.CompositingQuality = CompositingQuality.HighQuality;
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        graphics.SmoothingMode = SmoothingMode.HighQuality;
        var scale = Math.Max((double)width / source.Width, (double)height / source.Height);
        var drawWidth = source.Width * scale;
        var drawHeight = source.Height * scale;
        var destination = new RectangleF(
            (float)((width - drawWidth) / 2),
            (float)((height - drawHeight) / 2),
            (float)drawWidth,
            (float)drawHeight);
        graphics.DrawImage(source, destination);
        return scaled;
    }

    private static void ApplyExifOrientation(Image image)
    {
        const int orientationId = 0x0112;
        if (!image.PropertyIdList.Contains(orientationId)) return;
        var orientationData = image.GetPropertyItem(orientationId)?.Value;
        if (orientationData is not { Length: > 0 }) return;
        var orientation = orientationData[0];
        var transform = orientation switch
        {
            2 => RotateFlipType.RotateNoneFlipX,
            3 => RotateFlipType.Rotate180FlipNone,
            4 => RotateFlipType.Rotate180FlipX,
            5 => RotateFlipType.Rotate90FlipX,
            6 => RotateFlipType.Rotate90FlipNone,
            7 => RotateFlipType.Rotate270FlipX,
            8 => RotateFlipType.Rotate270FlipNone,
            _ => RotateFlipType.RotateNoneFlipNone
        };
        if (transform != RotateFlipType.RotateNoneFlipNone) image.RotateFlip(transform);
    }

    public static void WritePreviewBitmap(string path, byte[] bgra, int width, int height)
    {
        var rowBytes = width * 3;
        var padding = (4 - rowBytes % 4) % 4;
        var imageSize = (rowBytes + padding) * height;
        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream);
        writer.Write((ushort)0x4d42);
        writer.Write(54 + imageSize);
        writer.Write(0);
        writer.Write(54);
        writer.Write(40);
        writer.Write(width);
        writer.Write(height);
        writer.Write((ushort)1);
        writer.Write((ushort)24);
        writer.Write(0);
        writer.Write(imageSize);
        writer.Write(2835);
        writer.Write(2835);
        writer.Write(0);
        writer.Write(0);
        var pad = new byte[padding];
        for (var y = height - 1; y >= 0; y--)
        {
            for (var x = 0; x < width; x++)
            {
                var offset = (y * width + x) * 4;
                writer.Write(bgra[offset]);
                writer.Write(bgra[offset + 1]);
                writer.Write(bgra[offset + 2]);
            }
            writer.Write(pad);
        }
    }
}
