using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RestaurantSaaS.Domain.Entities.Carts;
using RestaurantSaaS.Infrastructure.Persistence;
using RestaurantSaaS.Infrastructure.Services;

namespace RestaurantSaaS.Infrastructure.Tests;

public class CartConcurrencyTests
{
    [Fact]
    public void Cart_RowVersion_IsConfiguredAsConcurrencyToken()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        using var context = new ApplicationDbContext(options, null!, null!);
        var entityType = context.Model.FindEntityType(typeof(Cart))!;
        var property = entityType.FindProperty(nameof(Cart.RowVersion))!;
        property.IsConcurrencyToken.Should().BeTrue();
        property.ValueGenerated.Should().Be(Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.OnAddOrUpdate);
    }
}
