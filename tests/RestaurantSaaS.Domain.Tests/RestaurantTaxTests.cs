using FluentAssertions;
using RestaurantSaaS.Domain.Entities.Restaurants;
using RestaurantSaaS.Domain.Exceptions;
using RestaurantSaaS.Domain.ValueObjects;

namespace RestaurantSaaS.Domain.Tests;

public class RestaurantTaxTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Address Address = Address.Create("123 Food Street", "Karachi", "Pakistan");

    [Fact]
    public void Create_WithValidTaxRate_SetsTaxRate()
    {
        var restaurant = Restaurant.Create(TenantId, "Dhaba", Address, taxRate: 16m);

        restaurant.TaxRate.Should().Be(16m);
    }

    [Fact]
    public void Create_WithoutTaxRate_DoesNotHardcodeGst()
    {
        var restaurant = Restaurant.Create(TenantId, "Dhaba", Address);

        restaurant.TaxRate.Should().Be(0m);
    }

    [Fact]
    public void Create_WhenNegativeTax_Throws()
    {
        var act = () => Restaurant.Create(TenantId, "Dhaba", Address, taxRate: -1m);

        act.Should().Throw<DomainException>()
            .Where(e => e.Code == "RESTAURANT_TAX_RATE_OUT_OF_RANGE");
    }

    [Fact]
    public void Create_WhenTaxAboveMaximum_Throws()
    {
        var act = () => Restaurant.Create(TenantId, "Dhaba", Address, taxRate: 100.0001m);

        act.Should().Throw<DomainException>()
            .Where(e => e.Code == "RESTAURANT_TAX_RATE_OUT_OF_RANGE");
    }

    [Fact]
    public void Create_WhenTaxTooManyDecimals_Throws()
    {
        var act = () => Restaurant.Create(TenantId, "Dhaba", Address, taxRate: 10.12345m);

        act.Should().Throw<DomainException>()
            .Where(e => e.Code == "RESTAURANT_TAX_RATE_PRECISION");
    }

    [Fact]
    public void SetTaxRate_WhenValid_Updates()
    {
        var restaurant = Restaurant.Create(TenantId, "Dhaba", Address, taxRate: 16m);

        restaurant.SetTaxRate(8.5m);

        restaurant.TaxRate.Should().Be(8.5m);
        restaurant.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void SetTaxRate_WhenSameValue_DoesNotUpdate()
    {
        var restaurant = Restaurant.Create(TenantId, "Dhaba", Address, taxRate: 16m);

        restaurant.SetTaxRate(16m);

        restaurant.TaxRate.Should().Be(16m);
        restaurant.UpdatedAt.Should().BeNull();
    }

    [Fact]
    public void SetTaxRate_WhenInvalid_Throws()
    {
        var restaurant = Restaurant.Create(TenantId, "Dhaba", Address);

        var act = () => restaurant.SetTaxRate(-5m);

        act.Should().Throw<DomainException>()
            .Where(e => e.Code == "RESTAURANT_TAX_RATE_OUT_OF_RANGE");
    }
}
