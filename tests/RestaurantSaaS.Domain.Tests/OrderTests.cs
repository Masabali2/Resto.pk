using FluentAssertions;
using RestaurantSaaS.Domain.Entities.Orders;
using RestaurantSaaS.Domain.Enums;
using RestaurantSaaS.Domain.Exceptions;

namespace RestaurantSaaS.Domain.Tests;

public class OrderTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid RestaurantId = Guid.NewGuid();
    private static readonly Guid MenuItemId = Guid.NewGuid();

    private static OrderItem ValidItem(
        Guid orderId,
        decimal unitPrice = 10.50m,
        int quantity = 2,
        string currency = "PKR") =>
        OrderItem.CreateSnapshot(orderId, MenuItemId, "Test Item", unitPrice, quantity, currency);

    private static Order ValidOrder(params OrderItem[] items) =>
        Order.Create(
            TenantId,
            RestaurantId,
            null,
            "ORD-100",
            "Guest Customer",
            null,
            null,
            items.Sum(i => i.LineTotal),
            0m,
            0m,
            items.Sum(i => i.LineTotal),
            "PKR",
            null,
            OrderType.Pickup,
            items);

    [Fact]
    public void Create_WhenValid_SetsFieldsAndInitialHistory()
    {
        var item = ValidItem(Guid.NewGuid());
        var order = ValidOrder(item);

        order.TenantId.Should().Be(TenantId);
        order.RestaurantId.Should().Be(RestaurantId);
        order.CustomerId.Should().BeNull();
        order.Status.Should().Be(OrderStatus.New);
        order.OrderType.Should().Be(OrderType.Pickup);
        order.PaymentMethod.Should().Be(PaymentMethod.Cash);
        order.PaymentStatus.Should().Be(PaymentStatus.Pending);
        order.Currency.Should().Be("PKR");
        order.Subtotal.Should().Be(item.LineTotal);
        order.Items.Should().HaveCount(1);
        order.StatusHistory.Should().HaveCount(1);

        var history = order.StatusHistory.Single();
        history.Status.Should().Be(OrderStatus.New);
        history.ChangedByUserId.Should().BeNull();
        history.CreatedAt.Should().Be(order.CreatedAt);
        history.Notes.Should().BeNull();
    }

    [Fact]
    public void Create_WhenGuestOrder_HistoryHasNullActor()
    {
        var item = ValidItem(Guid.NewGuid());
        var order = ValidOrder(item);

        order.CustomerId.Should().BeNull();
        order.StatusHistory.Single().ChangedByUserId.Should().BeNull();
    }

    [Fact]
    public void Create_WhenDineInOrder_SetsOrderType()
    {
        var item = ValidItem(Guid.NewGuid());
        var order = Order.Create(
            TenantId,
            RestaurantId,
            null,
            "ORD-101",
            "Dine Customer",
            null,
            null,
            item.LineTotal,
            0m,
            0m,
            item.LineTotal,
            "PKR",
            null,
            OrderType.DineIn,
            new[] { item });

        order.OrderType.Should().Be(OrderType.DineIn);
    }

    [Fact]
    public void Create_WhenDeliveryOrder_SetsOrderType()
    {
        var item = ValidItem(Guid.NewGuid());
        var order = Order.Create(
            TenantId,
            RestaurantId,
            null,
            "ORD-102",
            "Delivery Customer",
            null,
            null,
            item.LineTotal,
            0m,
            0m,
            item.LineTotal,
            "PKR",
            null,
            OrderType.Delivery,
            new[] { item });

        order.OrderType.Should().Be(OrderType.Delivery);
    }

    [Fact]
    public void Create_WhenNoItems_Throws()
    {
        var act = () => Order.Create(
            TenantId,
            RestaurantId,
            null,
            "ORD-200",
            "Guest",
            null,
            null,
            0m,
            0m,
            0m,
            0m,
            "PKR",
            null,
            OrderType.Pickup,
            null);

        act.Should().Throw<DomainException>()
            .Where(e => e.Code == "ORDER_ITEMS_REQUIRED");
    }

    [Fact]
    public void Create_WhenItemBelongsToAnotherOrder_Throws()
    {
        var itemA = ValidItem(Guid.NewGuid());
        var orderA = ValidOrder(itemA);

        var itemB = ValidItem(Guid.NewGuid());
        var orderB = ValidOrder(itemB);

        var act = () => orderB.AddItem(itemA);

        act.Should().Throw<DomainException>()
            .Where(e => e.Code == "ORDER_ITEM_ORDER_MISMATCH");
    }

    [Fact]
    public void Create_WhenItemCurrencyMismatch_Throws()
    {
        var usdItem = ValidItem(Guid.NewGuid(), currency: "USD");
        var act = () => ValidOrder(usdItem);

        act.Should().Throw<DomainException>()
            .Where(e => e.Code == "ORDER_ITEM_CURRENCY_MISMATCH");
    }

    [Fact]
    public void AddItem_WhenCurrencyMismatch_Throws()
    {
        var order = ValidOrder(ValidItem(Guid.NewGuid()));
        var usdItem = OrderItem.CreateSnapshot(order.Id, MenuItemId, "USD Item", 5m, 1, "USD");

        var act = () => order.AddItem(usdItem);

        act.Should().Throw<DomainException>()
            .Where(e => e.Code == "ORDER_ITEM_CURRENCY_MISMATCH");
    }

    [Fact]
    public void Create_WhenSubtotalMismatch_Throws()
    {
        var item = ValidItem(Guid.NewGuid());
        var act = () => Order.Create(
            TenantId,
            RestaurantId,
            null,
            "ORD-201",
            "Guest",
            null,
            null,
            item.LineTotal + 1m,
            0m,
            0m,
            item.LineTotal + 1m,
            "PKR",
            null,
            OrderType.Pickup,
            new[] { item });

        act.Should().Throw<DomainException>()
            .Where(e => e.Code == "ORDER_SUBTOTAL_MISMATCH");
    }

    [Fact]
    public void Create_WhenCorrectSubtotal_Accepted()
    {
        var item = ValidItem(Guid.NewGuid(), 10.50m, 3);
        var order = ValidOrder(item);

        order.Subtotal.Should().Be(item.LineTotal);
        order.Items.Should().ContainSingle();
    }

    [Fact]
    public void Create_WhenSubtotalOutOfRange_Throws()
    {
        var item = ValidItem(Guid.NewGuid());
        var act = () => Order.Create(
            TenantId,
            RestaurantId,
            null,
            "ORD-202",
            "Guest",
            null,
            null,
            decimal.MaxValue,
            0m,
            0m,
            decimal.MaxValue,
            "PKR",
            null,
            OrderType.Pickup,
            new[] { item });

        act.Should().Throw<DomainException>()
            .Where(e => e.Code == "ORDER_MONEY_INVALID");
    }

    [Fact]
    public void AddItem_WhenValid_AddsItemAndUpdatesSubtotal()
    {
        var item = ValidItem(Guid.NewGuid());
        var order = ValidOrder(item);

        var extra = OrderItem.CreateSnapshot(order.Id, MenuItemId, "Extra", 5m, 1, "PKR");
        var priorSubtotal = order.Subtotal;

        order.AddItem(extra);

        order.Items.Should().HaveCount(2);
        order.Subtotal.Should().Be(priorSubtotal + extra.LineTotal);
    }

    [Fact]
    public void Transition_ValidPath_ReachesTerminal()
    {
        var order = ValidOrder(ValidItem(Guid.NewGuid()));

        order.TransitionTo(OrderStatus.Confirmed);
        order.TransitionTo(OrderStatus.Preparing);
        order.TransitionTo(OrderStatus.Ready);
        order.TransitionTo(OrderStatus.Completed);

        order.Status.Should().Be(OrderStatus.Completed);
        order.IsTerminal.Should().BeTrue();
        order.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public void Transition_Cancelled_IsTerminal()
    {
        var order = ValidOrder(ValidItem(Guid.NewGuid()));

        order.TransitionTo(OrderStatus.Cancelled);

        order.Status.Should().Be(OrderStatus.Cancelled);
        order.IsTerminal.Should().BeTrue();
        order.CancelledAt.Should().NotBeNull();
    }

    [Fact]
    public void Transition_Invalid_Throws()
    {
        var order = ValidOrder(ValidItem(Guid.NewGuid()));

        var act = () => order.TransitionTo(OrderStatus.Preparing);

        act.Should().Throw<DomainException>()
            .Where(e => e.Code == "ORDER_INVALID_TRANSITION");
    }

    [Fact]
    public void Transition_FromTerminal_Throws()
    {
        var order = ValidOrder(ValidItem(Guid.NewGuid()));
        order.TransitionTo(OrderStatus.Confirmed);
        order.TransitionTo(OrderStatus.Preparing);
        order.TransitionTo(OrderStatus.Ready);
        order.TransitionTo(OrderStatus.Completed);

        var act = () => order.TransitionTo(OrderStatus.Cancelled);

        act.Should().Throw<DomainException>()
            .Where(e => e.Code == "ORDER_INVALID_TRANSITION");
    }

    [Fact]
    public void ConfirmPayment_StartsPending_BecomesPaidOnConfirm()
    {
        var order = ValidOrder(ValidItem(Guid.NewGuid()));

        order.PaymentMethod.Should().Be(PaymentMethod.Cash);
        order.PaymentStatus.Should().Be(PaymentStatus.Pending);

        order.ConfirmPayment();

        order.PaymentStatus.Should().Be(PaymentStatus.Paid);
    }

    [Fact]
    public void ConfirmPayment_Twice_RejectsInvalidTransition()
    {
        var order = ValidOrder(ValidItem(Guid.NewGuid()));
        order.ConfirmPayment();

        var act = () => order.ConfirmPayment();

        act.Should().Throw<DomainException>()
            .Where(e => e.Code == "ORDER_PAYMENT_INVALID_TRANSITION");
    }

    [Fact]
    public void ConfirmPayment_NullActor_IsAllowed()
    {
        var order = ValidOrder(ValidItem(Guid.NewGuid()));

        order.ConfirmPayment(null);

        order.PaymentStatus.Should().Be(PaymentStatus.Paid);
    }
}
