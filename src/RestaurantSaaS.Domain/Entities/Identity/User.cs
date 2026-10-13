
using Microsoft.AspNetCore.Identity;

namespace RestaurantSaaS.Domain.Entities.Identity;

public class User : IdentityUser<Guid>
{
    public string? FirstName { get; set; }

    public string? LastName { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? LastLoginAt { get; set; }

    public string FullName =>
        string.IsNullOrWhiteSpace(FirstName) &&
        string.IsNullOrWhiteSpace(LastName)
            ? UserName ?? string.Empty
            : $"{FirstName} {LastName}".Trim();
}

