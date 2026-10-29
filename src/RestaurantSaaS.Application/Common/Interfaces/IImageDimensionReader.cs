namespace RestaurantSaaS.Application.Common.Files;

/// <summary>
/// Reads raster image metadata from a stream header without decoding the
/// entire file. Bounded reads protect against decompression bombs and
/// oversized inputs.
/// </summary>
public interface IImageDimensionReader
{
    /// <summary>Returns (width, height) when decodable, otherwise null.</summary>
    (int Width, int Height)? ReadDimensions(Stream imageStream);

    /// <summary>
    /// Detects the content type from the file signature (magic bytes).
    /// Returns null when the signature is not a supported raster image.
    /// Stream position is restored afterwards.
    /// </summary>
    string? DetectContentType(Stream imageStream);
}
