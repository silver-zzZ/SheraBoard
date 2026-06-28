using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Drawing;
using System.Drawing.Imaging;
using SheraBoard.Core.Capture;

namespace SheraBoard.App.Services;

public sealed class ClipboardReader
{
    public ClipboardCapture? TryRead()
    {
        try
        {
            var dataObject = System.Windows.Clipboard.GetDataObject();
            if (dataObject is null)
            {
                return null;
            }

            var files = ReadFileDrop(dataObject);
            var text = ReadString(dataObject, System.Windows.DataFormats.UnicodeText);
            var html = ReadString(dataObject, System.Windows.DataFormats.Html);
            var rtf = ReadString(dataObject, System.Windows.DataFormats.Rtf);
            var (imageBytes, imageWidth, imageHeight) = ReadImage(dataObject);

            return new ClipboardCapture(text, html, rtf, imageBytes, imageWidth, imageHeight, files);
        }
        catch (ExternalException)
        {
            return null;
        }
    }

    private static string? ReadString(System.Windows.IDataObject dataObject, string format)
    {
        return dataObject.GetDataPresent(format) ? dataObject.GetData(format) as string : null;
    }

    private static IReadOnlyList<string> ReadFileDrop(System.Windows.IDataObject dataObject)
    {
        if (!dataObject.GetDataPresent(System.Windows.DataFormats.FileDrop))
        {
            return [];
        }

        return dataObject.GetData(System.Windows.DataFormats.FileDrop) is string[] paths ? paths : [];
    }

    private static (byte[]? Bytes, int? Width, int? Height) ReadImage(System.Windows.IDataObject dataObject)
    {
        var directPng = ReadPngBytes(dataObject);
        if (directPng.Bytes is not null)
        {
            return directPng;
        }

        try
        {
            if (System.Windows.Clipboard.ContainsImage())
            {
                var image = System.Windows.Clipboard.GetImage();
                if (image is not null)
                {
                    return EncodeBitmapSource(image);
                }
            }
        }
        catch (ExternalException)
        {
        }

        var bitmapObject = dataObject.GetDataPresent(System.Windows.DataFormats.Bitmap, autoConvert: true)
            ? dataObject.GetData(System.Windows.DataFormats.Bitmap, autoConvert: true)
            : null;
        return bitmapObject switch
        {
            BitmapSource bitmapSource => EncodeBitmapSource(bitmapSource),
            Bitmap bitmap => EncodeDrawingBitmap(bitmap),
            _ => (null, null, null)
        };
    }

    private static (byte[]? Bytes, int? Width, int? Height) ReadPngBytes(System.Windows.IDataObject dataObject)
    {
        foreach (var format in new[] { "PNG", "image/png" })
        {
            if (!dataObject.GetDataPresent(format, autoConvert: true))
            {
                continue;
            }

            var data = dataObject.GetData(format, autoConvert: true);
            var bytes = data switch
            {
                byte[] byteArray => byteArray,
                MemoryStream memoryStream => memoryStream.ToArray(),
                Stream stream => ReadAllBytes(stream),
                _ => null
            };
            if (bytes is { Length: > 0 })
            {
                var dimensions = TryReadPngDimensions(bytes);
                return (bytes, dimensions.Width, dimensions.Height);
            }
        }

        return (null, null, null);
    }

    private static (byte[] Bytes, int Width, int Height) EncodeBitmapSource(BitmapSource image)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(image));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return (stream.ToArray(), image.PixelWidth, image.PixelHeight);
    }

    private static (byte[] Bytes, int Width, int Height) EncodeDrawingBitmap(Bitmap bitmap)
    {
        using var stream = new MemoryStream();
        bitmap.Save(stream, ImageFormat.Png);
        return (stream.ToArray(), bitmap.Width, bitmap.Height);
    }

    private static byte[] ReadAllBytes(Stream stream)
    {
        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        using var memoryStream = new MemoryStream();
        stream.CopyTo(memoryStream);
        return memoryStream.ToArray();
    }

    private static (int? Width, int? Height) TryReadPngDimensions(byte[] pngBytes)
    {
        try
        {
            using var stream = new MemoryStream(pngBytes);
            var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            var frame = decoder.Frames.FirstOrDefault();
            return frame is null ? (null, null) : (frame.PixelWidth, frame.PixelHeight);
        }
        catch
        {
            return (null, null);
        }
    }
}
