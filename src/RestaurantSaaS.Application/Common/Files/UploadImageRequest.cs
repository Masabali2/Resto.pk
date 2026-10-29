using Microsoft.AspNetCore.Http;

namespace RestaurantSaaS.Application.Common.Files;

/// <summary>Multipart upload request: one image plus a JSON destination descriptor.</summary>
public sealed class UploadImageRequest
{
    public IFormFile? File { get; set; }
    public string? Destination { get; set; }
}
