using System.Globalization;
using System.Net;

namespace RestaurantSaaS.Infrastructure.Services;

internal static class EmailNotificationTemplates
{
    public static (string Subject, string Body) LoginAlert(string userName) => (
        "New sign-in to your Resto.pk account",
        $"""
         <p>Hello {Escape(userName)},</p>
         <p>Your Resto.pk account was successfully signed in.</p>
         <p>If you performed this action, no further action is required. If you do not recognize this sign-in, please secure your account or contact support.</p>
         <p>Regards,<br/>Resto.pk Security Team</p>
         """);

    public static (string Subject, string Body) NewOrder(
        string restaurantName,
        string orderNumber,
        decimal totalAmount,
        string currency,
        string status,
        DateTime receivedAtUtc) => (
        $"New order received — Order #{Escape(orderNumber)}",
        $"""
         <p>Hello {Escape(restaurantName)},</p>
         <p>A new order has been received.</p>
         <p>
             Order number: {Escape(orderNumber)}<br/>
             Order total: {Escape(totalAmount.ToString("0.00", CultureInfo.InvariantCulture) + " " + currency)}<br/>
             Order status: {Escape(status)}<br/>
             Received at: {Escape(receivedAtUtc.ToString("u", CultureInfo.InvariantCulture))} UTC
         </p>
         <p>Please open your Resto.pk dashboard to review and process the order.</p>
         <p>Regards,<br/>Resto.pk</p>
         """);

    public static (string Subject, string Body)? CustomerStatusUpdate(
        string customerName,
        string orderNumber,
        string restaurantName,
        string status)
    {
        return status switch
        {
            nameof(Domain.Enums.OrderStatus.Confirmed) => (
                $"Your order #{Escape(orderNumber)} is confirmed",
                $"""
                 <p>Hello {Escape(customerName)},</p>
                 <p>Your order #{Escape(orderNumber)} from {Escape(restaurantName)} has been confirmed.</p>
                 <p>Current status: {Escape(status)}</p>
                 <p>You can check the latest order details through Resto.pk.</p>
                 <p>Regards,<br/>Resto.pk</p>
                 """),
            nameof(Domain.Enums.OrderStatus.Preparing) => (
                $"Your order #{Escape(orderNumber)} is being prepared",
                $"""
                 <p>Hello {Escape(customerName)},</p>
                 <p>Your order #{Escape(orderNumber)} is now being prepared by {Escape(restaurantName)}.</p>
                 <p>Current status: {Escape(status)}</p>
                 <p>You can check the latest order details through Resto.pk.</p>
                 <p>Regards,<br/>Resto.pk</p>
                 """),
            nameof(Domain.Enums.OrderStatus.Completed) => (
                $"Your order #{Escape(orderNumber)} is complete",
                $"""
                 <p>Hello {Escape(customerName)},</p>
                 <p>Your order #{Escape(orderNumber)} from {Escape(restaurantName)} has been marked as completed.</p>
                 <p>Thank you for ordering through Resto.pk.</p>
                 <p>Regards,<br/>Resto.pk</p>
                 """),
            nameof(Domain.Enums.OrderStatus.Cancelled) => (
                $"Your order #{Escape(orderNumber)} was cancelled",
                $"""
                 <p>Hello {Escape(customerName)},</p>
                 <p>Your order #{Escape(orderNumber)} from {Escape(restaurantName)} has been cancelled.</p>
                 <p>If you did not expect this, please contact the restaurant directly.</p>
                 <p>Regards,<br/>Resto.pk</p>
                 """),
            _ => null
        };
    }

    private static string Escape(string value) =>
        WebUtility.HtmlEncode(value ?? string.Empty);
}
