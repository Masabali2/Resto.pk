using Microsoft.EntityFrameworkCore;
using RestaurantSaaS.Domain.Entities.Orders;
using RestaurantSaaS.Domain.Enums;
using RestaurantSaaS.Infrastructure.Persistence;

namespace RestaurantSaaS.Infrastructure.Seed;

public static class DemoOrderSeeder
{
    private const string OrderNumberPrefix = "DEMO-ORD-";

    private static readonly DemoOrderDefinition[] Orders =
    [
        new("Ayesha Khan", "03001234567", OrderStatus.New, OrderType.Delivery, [0, 3], [1, 2], false, "Please call on arrival."),
        new("Usman Ahmed", "03011234567", OrderStatus.New, OrderType.Pickup, [1], [2], false, null),
        new("Sara Malik", "03021234567", OrderStatus.Confirmed, OrderType.DineIn, [2, 4], [1, 1], true, "Window seating if available."),
        new("Hamza Iqbal", "03031234567", OrderStatus.Confirmed, OrderType.Delivery, [5, 6], [1, 2], false, null),
        new("Fatima Noor", "03041234567", OrderStatus.Preparing, OrderType.Pickup, [7, 8], [2, 1], true, "No cutlery required."),
        new("Bilal Raza", "03051234567", OrderStatus.Preparing, OrderType.Delivery, [9, 10], [1, 1], false, "Extra napkins, please."),
        new("Hira Shah", "03061234567", OrderStatus.Ready, OrderType.Pickup, [0, 11], [1, 1], true, null),
        new("Danish Ali", "03071234567", OrderStatus.Completed, OrderType.DineIn, [1, 2], [1, 1], true, null),
        new("Maham Tariq", "03081234567", OrderStatus.Completed, OrderType.Delivery, [3, 5], [2, 1], true, "Leave at the reception."),
        new("Zain Abbas", "03091234567", OrderStatus.Cancelled, OrderType.Pickup, [4], [1], false, "Customer requested cancellation.")
    ];

    public static async Task<int> SeedAsync(
        ApplicationDbContext db,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(db);

        db.DisableTenantFilters = true;

        var menuItems = await db.MenuItems
            .IgnoreQueryFilters()
            .Where(item =>
                item.Category.MenuId == DemoMenuSeeder.DemoMenuId &&
                item.IsActive &&
                item.IsAvailable)
            .OrderBy(item => item.Name)
            .Take(12)
            .ToListAsync(ct);

        if (menuItems.Count < 12)
        {
            throw new InvalidOperationException(
                "The demo menu must contain at least 12 available items before demo orders can be seeded.");
        }

        var existingOrderNumbers = await db.Orders
            .IgnoreQueryFilters()
            .Where(order => order.TenantId == DemoMenuSeeder.DemoTenantId &&
                            order.RestaurantId == DemoMenuSeeder.DemoRestaurantId &&
                            order.OrderNumber.StartsWith(OrderNumberPrefix))
            .Select(order => order.OrderNumber)
            .ToHashSetAsync(ct);

        var inserted = 0;

        for (var index = 0; index < Orders.Length; index++)
        {
            var orderNumber = $"{OrderNumberPrefix}{index + 1:D4}";

            if (existingOrderNumbers.Contains(orderNumber))
            {
                continue;
            }

            var definition = Orders[index];
            var orderItems = new List<OrderItem>(definition.ItemIndexes.Length);
            decimal subtotal = 0m;

            for (var itemIndex = 0; itemIndex < definition.ItemIndexes.Length; itemIndex++)
            {
                var menuItem = menuItems[definition.ItemIndexes[itemIndex]];
                var quantity = definition.Quantities[itemIndex];

                var orderItem = OrderItem.CreateSnapshot(
                    Guid.NewGuid(),
                    menuItem.Id,
                    menuItem.Name,
                    menuItem.Price,
                    quantity,
                    menuItem.Currency);

                orderItems.Add(orderItem);
                subtotal += orderItem.LineTotal;
            }

            var order = Order.Create(
                DemoMenuSeeder.DemoTenantId,
                DemoMenuSeeder.DemoRestaurantId,
                customerId: null,
                orderNumber,
                definition.CustomerName,
                definition.CustomerPhone,
                customerEmail: null,
                subtotal,
                discountAmount: 0m,
                taxAmount: 0m,
                totalAmount: subtotal,
                currency: "PKR",
                definition.SpecialInstructions,
                definition.OrderType,
                orderItems);

            foreach (var status in GetStatusPath(definition.Status))
            {
                order.TransitionTo(
                    status,
                    notes: "Development demo order seed.",
                    utcNow: DateTime.UtcNow);
            }

            if (definition.IsPaid)
            {
                order.ConfirmPayment(utcNow: DateTime.UtcNow);
            }

            db.Orders.Add(order);
            inserted++;
        }

        if (inserted > 0)
        {
            await db.SaveChangesAsync(ct);
        }

        return inserted;
    }

    private static IEnumerable<OrderStatus> GetStatusPath(OrderStatus targetStatus)
    {
        return targetStatus switch
        {
            OrderStatus.New => [],
            OrderStatus.Confirmed => [OrderStatus.Confirmed],
            OrderStatus.Preparing => [OrderStatus.Confirmed, OrderStatus.Preparing],
            OrderStatus.Ready => [OrderStatus.Confirmed, OrderStatus.Preparing, OrderStatus.Ready],
            OrderStatus.Completed => [OrderStatus.Confirmed, OrderStatus.Preparing, OrderStatus.Ready, OrderStatus.Completed],
            OrderStatus.Cancelled => [OrderStatus.Cancelled],
            _ => throw new ArgumentOutOfRangeException(nameof(targetStatus))
        };
    }

    private sealed record DemoOrderDefinition(
        string CustomerName,
        string CustomerPhone,
        OrderStatus Status,
        OrderType OrderType,
        int[] ItemIndexes,
        int[] Quantities,
        bool IsPaid,
        string? SpecialInstructions);
}
