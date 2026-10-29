using RestaurantSaaS.Domain.Enums;

namespace RestaurantSaaS.Application.Common.Files;

public static class RestaurantAssetRules
{
    public const long MaxLogoSizeBytes = 1 * 1024 * 1024;
    public const long MaxImageSizeBytes = 5 * 1024 * 1024;

    private static readonly HashSet<AssetType> AllowedTypes =
    [
        AssetType.Logo,
        AssetType.DarkLogo,
        AssetType.Favicon,
        AssetType.OGBanner,
        AssetType.CoverImage,
        AssetType.Banner,
        AssetType.HeroBanner,
        AssetType.MobileLogo
    ];

    public static bool IsAllowed(AssetType assetType) => AllowedTypes.Contains(assetType);

    public static long MaxSizeFor(AssetType assetType) =>
        assetType is AssetType.Logo or AssetType.Favicon or AssetType.MobileLogo
            ? MaxLogoSizeBytes
            : MaxImageSizeBytes;
}
