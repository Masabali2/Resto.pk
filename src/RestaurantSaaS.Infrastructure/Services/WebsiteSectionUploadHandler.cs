using RestaurantSaaS.Application.Common.Files;
using RestaurantSaaS.Domain.Entities.Files;
using RestaurantSaaS.Domain.Exceptions;

namespace RestaurantSaaS.Infrastructure.Services;

/// <summary>
/// Associates an uploaded image with a website draft section (hero / about /
/// gallery). The image reference lives in the website draft configuration, so
/// this handler authorizes the destination only; the caller persists the
/// returned asset id through the normal draft-save flow.
/// </summary>
public sealed class WebsiteSectionUploadHandler : IUploadDestinationHandler
{
    private static readonly HashSet<string> AllowedSectionTypes =
        new(StringComparer.OrdinalIgnoreCase) { "hero", "about", "gallery" };

    public string Kind => "website-section";

    public Task ApplyAsync(
        FileMetadata file,
        UploadDestination destination,
        Guid userId,
        IUploadDestinationContext context,
        CancellationToken cancellationToken)
    {
        if (destination is not WebsiteSectionDestination d)
            throw new DomainException("UPLOAD_INVALID_DESTINATION",
                "Expected a website-section destination.");

        if (string.IsNullOrWhiteSpace(d.SectionType) ||
            !AllowedSectionTypes.Contains(d.SectionType))
            throw new DomainException("UPLOAD_INVALID_SECTION",
                $"Section type '{d.SectionType}' is not a supported image destination.");

        if (d.WebsiteId == Guid.Empty)
            throw new DomainException("UPLOAD_WEBSITE_REQUIRED", "A website is required.");

        return ValidateAccessAsync(d.WebsiteId, userId, context, cancellationToken);

        static async Task ValidateAccessAsync(
            Guid websiteId, Guid userId, IUploadDestinationContext context,
            CancellationToken ct)
        {
            var website = await context.FindWebsiteAsync(websiteId, ct);
            if (website is null)
                throw new DomainException("UPLOAD_WEBSITE_NOT_FOUND",
                    "The website could not be found or is not accessible.");

            var isMember = await context.IsActiveMemberAsync(userId, ct);
            if (!isMember)
                throw new DomainException("UPLOAD_FORBIDDEN",
                    "You are not authorized to modify this website.");
        }
    }
}
