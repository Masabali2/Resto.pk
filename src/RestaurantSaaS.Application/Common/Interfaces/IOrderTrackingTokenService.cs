namespace RestaurantSaaS.Application.Common.Interfaces;

public interface IOrderTrackingTokenService
{
    (string RawToken, string Hash) Generate();
    string Hash(string rawToken);
}
