using FluentAssertions;
using RestaurantSaaS.Domain.Entities.Orders;
using RestaurantSaaS.Domain.Enums;
using RestaurantSaaS.Domain.Exceptions;

namespace RestaurantSaaS.Domain.Tests;

public class OrderTypeTests
{
    [Fact]
    public void ValidTypes_AreDefined()
    {
        Enum.IsDefined<OrderType>(OrderType.Pickup).Should().BeTrue();
        Enum.IsDefined<OrderType>(OrderType.DineIn).Should().BeTrue();
        Enum.IsDefined<OrderType>(OrderType.Delivery).Should().BeTrue();
    }

    [Fact]
    public void InvalidValue_IsNotDefined()
    {
        Enum.IsDefined<OrderType>((OrderType)99).Should().BeFalse();
    }

    [Fact]
    public void Create_WhenInvalidOrderType_Throws()
    {
        var item = OrderItem.CreateSnapshot(Guid.NewGuid(), Guid.NewGuid(), "Item", 10m, 1, "PKR");

        var act = () => Order.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            "ORD-1",
            "Customer",
            null,
            null,
            item.LineTotal,
            0m,
            0m,
            item.LineTotal,
            "PKR",
            null,
            (OrderType)99,
            new[] { item });

        act.Should().Throw<DomainException>()
            .Where(e => e.Code == "ORDER_TYPE_INVALID");
    }
}
