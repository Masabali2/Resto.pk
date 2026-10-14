using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;
using RestaurantSaaS.Application.Common.Interfaces;
using RestaurantSaaS.Application.Common.Options;

namespace RestaurantSaaS.Infrastructure.Services;

public sealed class EmailService(
    IOptions<EmailOptions> emailOptions) : IEmailService
{
    private readonly EmailOptions _options = emailOptions.Value;

    public async Task SendAsync(
        string to,
        string subject,
        string htmlBody,
        CancellationToken cancellationToken = default)
    {
        using var message = new MailMessage
        {
            From = new MailAddress(
                _options.FromEmail,
                _options.FromName),
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true
        };

        message.To.Add(new MailAddress(to));

        using var client = new SmtpClient(
            _options.SmtpHost,
            _options.SmtpPort)
        {
            EnableSsl = true,
            UseDefaultCredentials = false,
            Credentials = new NetworkCredential(
                _options.SmtpUser,
                _options.SmtpPassword)
        };

        await client.SendMailAsync(message, cancellationToken);
    }
}