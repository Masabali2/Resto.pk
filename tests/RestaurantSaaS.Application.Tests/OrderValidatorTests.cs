using FluentAssertions;
using RestaurantSaaS.Application.Features.Orders.DTOs;
using RestaurantSaaS.Application.Features.Orders.Validators;
using RestaurantSaaS.Domain.Enums;

namespace RestaurantSaaS.Application.Tests;

public class OrderValidatorTests
{
    private readonly CreateOrderRequestValidator _createOrder = new();
    private readonly OrderListQueryValidator _listQuery = new();
    private readonly UpdateOrderStatusRequestValidator _updateStatus = new();
    private readonly CancelOrderRequestValidator _cancelOrder = new();

    private static OrderItemRequest ValidItem(Guid? menuItemId = null, int quantity = 2) =>
        new(menuItemId ?? Guid.NewGuid(), quantity);

    private static CreateOrderRequest ValidRequest() =>
        new(
            OrderType.Pickup,
            "John Doe",
            "+923001234567",
            "john@example.com",
            "No onions please",
            [ValidItem()]);

    [Fact]
    public void CreateOrder_WhenValid_Passes()
    {
        var result = _createOrder.Validate(ValidRequest());

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void CreateOrder_WhenMissingCustomerName_Fails()
    {
        var request = ValidRequest() with { CustomerName = "" };

        var result = _createOrder.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateOrderRequest.CustomerName));
    }

    [Fact]
    public void CreateOrder_WhenCustomerNameTooLong_Fails()
    {
        var request = ValidRequest() with { CustomerName = new string('a', 151) };

        var result = _createOrder.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateOrderRequest.CustomerName));
    }

    [Fact]
    public void CreateOrder_WhenInvalidPhone_Fails()
    {
        var request = ValidRequest() with { CustomerPhone = "not-a-phone" };

        var result = _createOrder.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateOrderRequest.CustomerPhone));
    }

    [Fact]
    public void CreateOrder_WhenPhoneTooLong_Fails()
    {
        var request = ValidRequest() with { CustomerPhone = new string('+', 31) };

        var result = _createOrder.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateOrderRequest.CustomerPhone));
    }

    [Fact]
    public void CreateOrder_WhenInvalidEmail_Fails()
    {
        var request = ValidRequest() with { CustomerEmail = "not-an-email" };

        var result = _createOrder.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateOrderRequest.CustomerEmail));
    }

    [Fact]
    public void CreateOrder_WhenEmailTooLong_Fails()
    {
        var request = ValidRequest() with { CustomerEmail = new string('a', 250) + "@example.com" };

        var result = _createOrder.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateOrderRequest.CustomerEmail));
    }

    [Fact]
    public void CreateOrder_WhenEmptyItems_Fails()
    {
        var request = ValidRequest() with { Items = [] };

        var result = _createOrder.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateOrderRequest.Items));
    }

    [Fact]
    public void CreateOrder_WhenItemsNull_Fails()
    {
        var request = ValidRequest() with { Items = null! };

        var result = _createOrder.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateOrderRequest.Items));
    }

    [Fact]
    public void CreateOrder_WhenItemNullInsideItems_Fails()
    {
        var request = ValidRequest() with { Items = [null!] };

        var result = _createOrder.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateOrderRequest.Items));
    }

    [Fact]
    public void CreateOrder_WhenInvalidOrderType_Fails()
    {
        var request = ValidRequest() with { OrderType = (OrderType)99 };

        var result = _createOrder.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateOrderRequest.OrderType));
    }

    [Fact]
    public void CreateOrder_WhenSpecialInstructionsTooLong_Fails()
    {
        var request = ValidRequest() with { SpecialInstructions = new string('x', 2001) };

        var result = _createOrder.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateOrderRequest.SpecialInstructions));
    }

    [Fact]
    public void CreateOrder_WhenOptionalCustomerFieldsNull_Passes()
    {
        var request = ValidRequest() with { CustomerPhone = null, CustomerEmail = null, SpecialInstructions = null };

        var result = _createOrder.Validate(request);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void OrderItem_WhenValid_Passes()
    {
        var result = new OrderItemRequestValidator().Validate(ValidItem());

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void OrderItem_WhenEmptyMenuItemId_Fails()
    {
        var result = new OrderItemRequestValidator().Validate(new OrderItemRequest(Guid.Empty, 2));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(OrderItemRequest.MenuItemId));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1000)]
    public void OrderItem_WhenInvalidQuantity_Fails(int quantity)
    {
        var result = new OrderItemRequestValidator().Validate(new OrderItemRequest(Guid.NewGuid(), quantity));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(OrderItemRequest.Quantity));
    }

    [Fact]
    public void OrderListQuery_WhenValid_Passes()
    {
        var result = _listQuery.Validate(new OrderListQuery { Page = 1, PageSize = 20 });

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void OrderListQuery_WhenInvalidPage_Fails(int page)
    {
        var result = _listQuery.Validate(new OrderListQuery { Page = page, PageSize = 10 });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(OrderListQuery.Page));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void OrderListQuery_WhenInvalidPageSize_Fails(int pageSize)
    {
        var result = _listQuery.Validate(new OrderListQuery { Page = 1, PageSize = pageSize });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(OrderListQuery.PageSize));
    }

    [Fact]
    public void OrderListQuery_WhenDateFromAfterDateTo_Fails()
    {
        var result = _listQuery.Validate(new OrderListQuery
        {
            Page = 1,
            PageSize = 10,
            DateFrom = new DateTime(2026, 12, 1),
            DateTo = new DateTime(2026, 11, 1)
        });

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void OrderListQuery_WhenDateFromEqualsDateTo_Passes()
    {
        var result = _listQuery.Validate(new OrderListQuery
        {
            Page = 1,
            PageSize = 10,
            DateFrom = new DateTime(2026, 11, 1),
            DateTo = new DateTime(2026, 11, 1)
        });

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void UpdateOrderStatus_WhenValidEnum_Passes()
    {
        var result = _updateStatus.Validate(new UpdateOrderStatusRequest(OrderStatus.Confirmed, null));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void UpdateOrderStatus_WhenInvalidEnum_Fails()
    {
        var result = _updateStatus.Validate(new UpdateOrderStatusRequest((OrderStatus)99, null));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateOrderStatusRequest.Status));
    }

    [Fact]
    public void UpdateOrderStatus_WhenNotesTooLong_Fails()
    {
        var result = _updateStatus.Validate(new UpdateOrderStatusRequest(OrderStatus.Confirmed, new string('x', 1001)));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateOrderStatusRequest.Notes));
    }

    [Fact]
    public void CancelOrder_WhenReasonTooLong_Fails()
    {
        var result = _cancelOrder.Validate(new CancelOrderRequest(new string('x', 1001)));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CancelOrderRequest.Reason));
    }

    [Fact]
    public void CreateOrder_WhenDuplicateMenuItemIds_Fails()
    {
        var duplicateId = Guid.NewGuid();
        var request = ValidRequest() with
        {
            Items =
            [
                ValidItem(duplicateId, 2),
                ValidItem(duplicateId, 3)
            ]
        };

        var result = _createOrder.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateOrderRequest.Items));
    }

    [Fact]
    public void CreateOrder_WhenNonDuplicateMenuItemIds_Passes()
    {
        var request = ValidRequest() with
        {
            Items =
            [
                ValidItem(Guid.NewGuid(), 2),
                ValidItem(Guid.NewGuid(), 3)
            ]
        };

        var result = _createOrder.Validate(request);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void OrderListQuery_WhenInvalidStatusFilter_Fails()
    {
        var result = _listQuery.Validate(new OrderListQuery { Page = 1, PageSize = 10, Status = (OrderStatus)99 });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(OrderListQuery.Status));
    }

    [Fact]
    public void OrderListQuery_WhenInvalidOrderTypeFilter_Fails()
    {
        var result = _listQuery.Validate(new OrderListQuery { Page = 1, PageSize = 10, OrderType = (OrderType)99 });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(OrderListQuery.OrderType));
    }

    [Fact]
    public void OrderListQuery_WhenInvalidPaymentStatusFilter_Fails()
    {
        var result = _listQuery.Validate(new OrderListQuery { Page = 1, PageSize = 10, PaymentStatus = (PaymentStatus)99 });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(OrderListQuery.PaymentStatus));
    }

    [Fact]
    public void OrderListQuery_WhenValidEnumFilters_Passes()
    {
        var result = _listQuery.Validate(new OrderListQuery
        {
            Page = 1,
            PageSize = 10,
            Status = OrderStatus.New,
            OrderType = OrderType.Pickup,
            PaymentStatus = PaymentStatus.Pending
        });

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void OrderListQuery_WhenSearchTooLong_Fails()
    {
        var result = _listQuery.Validate(new OrderListQuery { Page = 1, PageSize = 10, Search = new string('a', 101) });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(OrderListQuery.Search));
    }

    [Fact]
    public void OrderListQuery_WhenSearchAtMaxLength_Passes()
    {
        var result = _listQuery.Validate(new OrderListQuery { Page = 1, PageSize = 10, Search = new string('a', 100) });

        result.IsValid.Should().BeTrue();
    }
}
