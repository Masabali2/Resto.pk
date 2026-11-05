using FluentAssertions;
using RestaurantSaaS.Domain.Entities.Orders;
using RestaurantSaaS.Domain.Enums;
using RestaurantSaaS.Domain.Exceptions;

namespace RestaurantSaaS.Domain.Tests;

public class OrderTrackingHashTests
{
    private static Order CreateTestOrder()
    {
        var item = OrderItem.CreateSnapshot(Guid.NewGuid(), Guid.NewGuid(), "Test", 10m, 1, "PKR");
        return Order.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            "ORD-TEST-1",
            "Customer",
            null,
            null,
            item.LineTotal,
            0m,
            0m,
            item.LineTotal,
            "PKR",
            null,
            OrderType.Pickup,
            new[] { item });
    }

    private static string ValidHash() => new string('a', 64);

    [Fact]
    public void SetTrackingTokenHash_WithValidHash_Succeeds()
    {
        var order = CreateTestOrder();
        var hash = ValidHash();
        order.SetTrackingTokenHash(hash);
        order.TrackingTokenHash.Should().Be(hash);
    }

    [Fact]
    public void SetTrackingTokenHash_WithNull_Throws()
    {
        var order = CreateTestOrder();
        var act = () => order.SetTrackingTokenHash(null!);
        act.Should().Throw<DomainException>().Where(e => e.Code == "ORDER_TRACKING_HASH_REQUIRED");
    }

    [Fact]
    public void SetTrackingTokenHash_WithEmpty_Throws()
    {
        var order = CreateTestOrder();
        var act = () => order.SetTrackingTokenHash("");
        act.Should().Throw<DomainException>().Where(e => e.Code == "ORDER_TRACKING_HASH_REQUIRED");
    }

    [Fact]
    public void SetTrackingTokenHash_WithWhitespace_Throws()
    {
        var order = CreateTestOrder();
        var act = () => order.SetTrackingTokenHash("   ");
        act.Should().Throw<DomainException>().Where(e => e.Code == "ORDER_TRACKING_HASH_REQUIRED");
    }

    [Fact]
    public void SetTrackingTokenHash_WithTooShort_Throws()
    {
        var order = CreateTestOrder();
        var act = () => order.SetTrackingTokenHash(new string('a', 63));
        act.Should().Throw<DomainException>().Where(e => e.Code == "ORDER_TRACKING_HASH_INVALID_LENGTH");
    }

    [Fact]
    public void SetTrackingTokenHash_WithTooLong_Throws()
    {
        var order = CreateTestOrder();
        var act = () => order.SetTrackingTokenHash(new string('a', 65));
        act.Should().Throw<DomainException>().Where(e => e.Code == "ORDER_TRACKING_HASH_INVALID_LENGTH");
    }

    [Fact]
    public void SetTrackingTokenHash_WithNonHex_Throws()
    {
        var order = CreateTestOrder();
        var act = () => order.SetTrackingTokenHash(new string('g', 64));
        act.Should().Throw<DomainException>().Where(e => e.Code == "ORDER_TRACKING_HASH_INVALID_FORMAT");
    }

    [Fact]
    public void SetTrackingTokenHash_NormalizesToLowercase()
    {
        var order = CreateTestOrder();
        var hash = new string('A', 64);
        order.SetTrackingTokenHash(hash);
        order.TrackingTokenHash.Should().Be(hash.ToLowerInvariant());
    }

    [Fact]
    public void SetTrackingTokenHash_CalledTwice_Throws()
    {
        var order = CreateTestOrder();
        order.SetTrackingTokenHash(ValidHash());
        var act = () => order.SetTrackingTokenHash(ValidHash());
        act.Should().Throw<DomainException>().Where(e => e.Code == "ORDER_TRACKING_HASH_ALREADY_SET");
    }

    [Fact]
    public void TrackingTokenHash_LegacyOrder_IsNull()
    {
        var order = CreateTestOrder();
        order.TrackingTokenHash.Should().BeNull();
    }
}
