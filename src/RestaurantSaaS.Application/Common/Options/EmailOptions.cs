using System.ComponentModel.DataAnnotations;

namespace RestaurantSaaS.Application.Common.Options;

public sealed class EmailOptions
{
    public const string SectionName = "Email";

    [Required]
    public string SmtpHost { get; set; } = string.Empty;

    [Range(1, 65535)]
    public int SmtpPort { get; set; } = 587;

    [Required]
    public string SmtpUser { get; set; } = string.Empty;

    [Required]
    public string SmtpPassword { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string FromEmail { get; set; } = string.Empty;

    [Required]
    public string FromName { get; set; } = "Resto.pk";
}   