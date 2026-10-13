
using Microsoft.AspNetCore.Identity;

namespace RestaurantSaaS.Domain.Entities.Identity;

public class Role : IdentityRole<Guid>
{
    public bool IsPlatformRole { get; set; }

    public string? Description { get; set; }
}

