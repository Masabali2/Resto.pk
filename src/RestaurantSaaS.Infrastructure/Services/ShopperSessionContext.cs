using Microsoft.AspNetCore.Http;
using RestaurantSaaS.Application.Common.Interfaces;

namespace RestaurantSaaS.Infrastructure.Services;

/// <summary>
/// Request-scoped shopper session context backed by HttpContext.Items.
/// </summary>
public sealed class ShopperSessionContext : IShopperSessionContext
{
    private const string SessionHashKey = "resto_shopper_session_hash";

    public ShopperSessionContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private readonly IHttpContextAccessor _httpContextAccessor;

    public string? SessionHash
    {
        get => _httpContextAccessor.HttpContext?.Items[SessionHashKey] as string;
        set
        {
            if (_httpContextAccessor.HttpContext is not null)
            {
                if (value is null)
                    _httpContextAccessor.HttpContext.Items.Remove(SessionHashKey);
                else
                    _httpContextAccessor.HttpContext.Items[SessionHashKey] = value;
            }
        }
    }

    public bool HasSession => SessionHash is not null;
}
