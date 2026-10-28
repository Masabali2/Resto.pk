using System.Buffers.Binary;
using RestaurantSaaS.Application.Common.Files;
using RestaurantSaaS.Application.Common.Interfaces;

namespace RestaurantSaaS.Infrastructure.Services;

/// <summary>
/// Reads raster image metadata from the stream header for JPEG, PNG and WebP.
/// Only the first few kilobytes are read; the stream is left positioned for
/// the caller. Unsupported or truncated inputs return null.
/// </summary>
public sealed class ImageDimensionReader : IImageDimensionReader
{
    private const int HeaderBufferSize = 8192;

    public (int Width, int Height)? ReadDimensions(Stream imageStream)
    {
        if (imageStream is null || !imageStream.CanRead)
            return null;

        long originalPosition = imageStream.Position;
        try
        {
            var buffer = ReadHeader(imageStream);
            if (buffer.Length < 16)
                return null;

            if (IsJpeg(buffer))
                return ReadJpegDimensions(buffer);

            if (IsPng(buffer))
                return ReadPngDimensions(buffer);

            if (IsWebp(buffer))
                return ReadWebpDimensions(buffer);

            return null;
        }
        finally
        {
            imageStream.Position = originalPosition;
        }
    }

    public string? DetectContentType(Stream imageStream)
    {
        if (imageStream is null || !imageStream.CanRead)
            return null;

        long originalPosition = imageStream.Position;
        try
        {
            var buffer = ReadHeader(imageStream);
            if (buffer.Length < 12) return null;

            if (IsJpeg(buffer)) return "image/jpeg";
            if (IsPng(buffer)) return "image/png";
            if (IsWebp(buffer)) return "image/webp";
            return null;
        }
        finally
        {
            imageStream.Position = originalPosition;
        }
    }

    private static byte[] ReadHeader(Stream stream)
    {
        var buffer = new byte[HeaderBufferSize];
        int read = stream.Read(buffer, 0, buffer.Length);
        return read > 0 ? buffer.AsSpan(0, read).ToArray() : Array.Empty<byte>();
    }

    private static bool IsJpeg(byte[] b) =>
        b.Length >= 2 && b[0] == 0xFF && b[1] == 0xD8;

    private static bool IsPng(byte[] b) =>
        b.Length >= 8 &&
        b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47 &&
        b[4] == 0x0D && b[5] == 0x0A && b[6] == 0x1A && b[7] == 0x0A;

    private static bool IsWebp(byte[] b) =>
        b.Length >= 12 &&
        b[0] == 0x52 && b[1] == 0x49 && b[2] == 0x46 && b[3] == 0x46 && // RIFF
        b[8] == 0x57 && b[9] == 0x45 && b[10] == 0x42 && b[11] == 0x50; // WEBP

    private static (int Width, int Height)? ReadPngDimensions(byte[] b)
    {
        if (b.Length < 24) return null;
        if (b[12] != 0x49 || b[13] != 0x48 || b[14] != 0x44 || b[15] != 0x52)
            return null;
        int width = BinaryPrimitives.ReadInt32BigEndian(b.AsSpan(16));
        int height = BinaryPrimitives.ReadInt32BigEndian(b.AsSpan(20));
        return Normalize(width, height);
    }

    private static (int Width, int Height)? ReadJpegDimensions(byte[] b)
    {
        int i = 2;
        while (i + 7 < b.Length)
        {
            if (b[i] != 0xFF) { i++; continue; }
            int marker = b[i + 1];
            if (marker >= 0xC0 && marker <= 0xCF &&
                marker != 0xC4 && marker != 0xC8 && marker != 0xCC)
            {
                if (i + 9 > b.Length) return null;
                int height = (b[i + 5] << 8) | b[i + 6];
                int width = (b[i + 7] << 8) | b[i + 8];
                return Normalize(width, height);
            }
            if (marker != 0xD8 && marker != 0xD9 && marker >= 0xD0 && marker <= 0xD9)
            {
                i += 2;
                continue;
            }
            if (i + 4 > b.Length) return null;
            int segLen = (b[i + 2] << 8) | b[i + 3];
            i += 2 + segLen;
        }
        return null;
    }

    private static (int Width, int Height)? ReadWebpDimensions(byte[] b)
    {
        if (b.Length < 31) return null;
        string fourCC = System.Text.Encoding.ASCII.GetString(b, 12, 4);
        if (fourCC == "VP8 ")
        {
            int width = b[26] | (b[27] << 8);
            int height = b[28] | (b[29] << 8);
            return Normalize(width, height);
        }
        if (fourCC == "VP8L")
        {
            int hdr0 = b[21];
            int width = (hdr0 & 0x3F) | (b[22] << 6) | ((b[23] & 0x0F) << 14);
            int height = ((b[23] >> 4) & 0x0F) | (b[24] << 4) | (b[25] << 12);
            return Normalize(width, height);
        }
        if (fourCC == "VP8X")
        {
            int width = b[24] | (b[25] << 8) | (b[26] << 16);
            int height = b[27] | (b[28] << 8) | (b[29] << 16);
            return Normalize(width, height);
        }
        return null;
    }

    private static (int Width, int Height)? Normalize(int width, int height) =>
        width > 0 && height > 0 ? (width, height) : null;
}
