using System.Security.Cryptography;
using RestaurantSaaS.Application.Common.Interfaces;

namespace RestaurantSaaS.Infrastructure.Services;

public sealed class OrderNumberGenerator : IOrderNumberGenerator
{
    public string Generate()
    {
        var now = DateTime.UtcNow;
        var bytes = RandomNumberGenerator.GetBytes(8);
        var suffix = Convert.ToHexString(bytes).ToUpperInvariant();
        var number = $"ORD-{now:yyyyMMdd}-{suffix}";
        if (number.Length > 50)
            number = number[..50];
        return number;
    }
}
