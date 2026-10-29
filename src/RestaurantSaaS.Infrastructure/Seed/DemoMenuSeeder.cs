using Microsoft.EntityFrameworkCore;
using RestaurantSaaS.Domain.Entities.Menus;
using RestaurantSaaS.Domain.Entities.Restaurants;
using RestaurantSaaS.Domain.Entities.Tenants;
using RestaurantSaaS.Domain.Enums;
using RestaurantSaaS.Domain.ValueObjects;
using RestaurantSaaS.Infrastructure.Persistence;

namespace RestaurantSaaS.Infrastructure.Seed;

/// <summary>
/// Development-only demo catalogue for the Menu Management screen.
///
/// The seeder owns a dedicated demo tenant/restaurant/menu (matched by stable
/// GUIDs and the unique demo slug). It never reads or mutates another
/// tenant's menu data, never overwrites records edited by a developer, and is
/// idempotent: re-running it only inserts missing demo rows. All seeded item
/// images are intentionally <c>null</c> so the UI exercises its placeholder
/// state.
/// </summary>
public static class DemoMenuSeeder
{
    public const string TenantName = "Resto.pk Demo Kitchen";
    public const string TenantSlug = "resto-pk-demo-kitchen";
    public const string RestaurantName = "Resto.pk Demo Kitchen";
    public const string MenuName = "Demo Menu — 100 Dishes";

    public static readonly Guid DemoTenantId =
        Guid.Parse("d3e0a000-0000-4000-8000-000000000001");

    public static readonly Guid DemoRestaurantId =
        Guid.Parse("d3e0a000-0000-4000-8000-000000000002");

    public static readonly Guid DemoMenuId =
        Guid.Parse("d3e0a000-0000-4000-8000-000000000003");

    private const string DemoOwnerEmail = "alimasab923@gmail.com";

    private static readonly IReadOnlyList<CategorySeed> Categories =
    [
        new("Starters & Snacks", 0,
        [
            ("Chicken Samosa", 120m, "Crisp pastry parcels filled with spiced chicken and served with tamarind chutney."),
            ("Vegetable Spring Rolls", 180m, "Golden rolls packed with cabbage, carrot and spring onion."),
            ("Chicken Wings", 650m, "Oven-baked wings tossed in a smoky chilli glaze."),
            ("Loaded Fries", 550m, "Crispy fries topped with cheese sauce, jalapeños and herbs."),
            ("Chicken Tenders", 690m, "Buttermilk-marinated tenders with a crunchy golden crust."),
            ("Mozzarella Sticks", 620m, "Stretchy mozzarella in a herbed breadcrumb coating."),
            ("Fish Fingers", 750m, "Boneless fish strips with a light lemon-pepper crumb."),
            ("Hummus with Pita", 480m, "Creamy chickpea hummus, olive oil and warm pita bread."),
            ("Garlic Bread", 320m, "Toasted baguette with garlic butter and parsley."),
            ("Stuffed Mushrooms", 590m, "Button mushrooms filled with herbed cream cheese.")
        ]),
        new("Soups & Salads", 1,
        [
            ("Chicken Corn Soup", 350m, "Silky chicken and sweetcorn soup finished with egg ribbons."),
            ("Hot & Sour Soup", 380m, "Tangy broth with chicken, vegetables and a gentle chilli kick."),
            ("Thai Chicken Soup", 420m, "Lemongrass, coconut milk and tender chicken in a fragrant broth."),
            ("Beef Nihari Soup", 450m, "Slow-cooked beef broth with traditional nihari spices."),
            ("Cream of Mushroom", 390m, "Velvety mushroom soup with a touch of cream."),
            ("Garden Salad", 300m, "Seasonal greens, cucumber, tomato and a light vinaigrette."),
            ("Caesar Salad", 520m, "Romaine, parmesan, croutons and creamy Caesar dressing."),
            ("Greek Salad", 560m, "Cucumber, tomato, olives and feta with oregano oil."),
            ("Chicken Tikka Salad", 620m, "Grilled tikka strips over crisp leaves with mint yoghurt."),
            ("Fruit Chaat", 280m, "Chilled fruit with chaat masala and fresh lime.")
        ]),
        new("BBQ & Grill", 2,
        [
            ("Chicken Tikka", 480m, "Charred chicken pieces marinated in yoghurt and tandoori spices."),
            ("Chicken Malai Boti", 620m, "Creamy malai marinade with mild green chilli."),
            ("Beef Seekh Kebab", 550m, "Hand-minced beef skewers with fresh coriander."),
            ("Chicken Seekh Kebab", 500m, "Juicy chicken kebabs with a smoky finish."),
            ("Lamb Chops", 1450m, "Tender chops grilled with black pepper and rosemary."),
            ("Chicken Boti", 580m, "Boneless chicken cubes grilled over an open flame."),
            ("Beef Bihari Kebab", 650m, "Thin beef strips in a rich Bihari marinade."),
            ("Fish Tikka", 890m, "Spiced fish fillets with lemon and ajwain."),
            ("Grilled Chicken Half", 950m, "Half chicken grilled with a house spice rub."),
            ("Mixed Grill Platter", 1850m, "Tikka, seekh kebab, boti and lamb chops for sharing.")
        ]),
        new("Karahi & Handi", 3,
        [
            ("Chicken Karahi Half", 1250m, "Chicken cooked with tomatoes, ginger and green chilli."),
            ("Chicken Karahi Full", 2300m, "A full karahi for sharing, finished with fresh coriander."),
            ("Mutton Karahi Half", 1850m, "Tender mutton in a glossy tomato and spice base."),
            ("Mutton Karahi Full", 3400m, "Slow-cooked mutton karahi for a family table."),
            ("Chicken White Karahi", 1450m, "Creamy white karahi with yoghurt and black pepper."),
            ("Chicken Handi", 1150m, "Chicken simmered in a rich, buttery handi gravy."),
            ("Mutton Handi", 1900m, "Mutton slow-cooked with caramelised onion and spices."),
            ("Chicken Achari Karahi", 1350m, "Tangy pickling spices with chicken and tomatoes."),
            ("Beef Karahi", 1600m, "Beef strips cooked in a bold tomato and ginger masala."),
            ("Butter Chicken Handi", 1400m, "Silky tomato-butter gravy with tender chicken.")
        ]),
        new("Curries & Daal", 4,
        [
            ("Chicken Curry", 950m, "Home-style chicken curry with a fragrant onion base."),
            ("Mutton Curry", 1500m, "Slow-cooked mutton in a deep, spiced gravy."),
            ("Chicken Korma", 1050m, "Mild korma with yoghurt, almonds and whole spices."),
            ("Beef Kofta Curry", 1150m, "Spiced beef koftas simmered in tomato gravy."),
            ("Daal Makhani", 550m, "Black lentils cooked overnight with butter and cream."),
            ("Daal Fry", 380m, "Yellow lentils tempered with cumin, garlic and chilli."),
            ("Chana Masala", 480m, "Chickpeas in a tangy onion-tomato masala."),
            ("Aloo Gobi", 520m, "Potato and cauliflower with turmeric and cumin."),
            ("Palak Paneer", 680m, "Paneer cubes in a smooth spinach gravy."),
            ("Mixed Vegetable Curry", 600m, "Seasonal vegetables in a lightly spiced curry.")
        ]),
        new("Rice & Biryani", 5,
        [
            ("Chicken Biryani", 650m, "Layered basmati rice with spiced chicken and saffron."),
            ("Mutton Biryani", 950m, "Aromatic mutton biryani with fried onion and yoghurt."),
            ("Beef Pulao", 700m, "Beef and basmati rice cooked in a savoury stock."),
            ("Chicken Pulao", 620m, "Fragrant chicken pulao with whole spices."),
            ("Vegetable Biryani", 520m, "Basmati rice with vegetables, mint and biryani masala."),
            ("Egg Fried Rice", 450m, "Wok-tossed rice with egg, spring onion and soy."),
            ("Chicken Fried Rice", 580m, "Chicken fried rice with crisp vegetables."),
            ("Plain Rice", 250m, "Steamed long-grain basmati rice."),
            ("Kabuli Pulao", 850m, "Sweet-savoury pulao with lamb, carrot and raisins."),
            ("Prawn Biryani", 1100m, "Prawns layered with saffron rice and herbs.")
        ]),
        new("Breads & Roti", 6,
        [
            ("Tandoori Roti", 40m, "Whole-wheat flatbread baked in the tandoor."),
            ("Butter Naan", 120m, "Soft naan brushed with melted butter."),
            ("Garlic Naan", 180m, "Naan topped with garlic and fresh coriander."),
            ("Cheese Naan", 220m, "Naan filled with a molten cheese blend."),
            ("Kulcha", 150m, "Pillowy leavened flatbread with a crisp base."),
            ("Paratha", 100m, "Flaky layered whole-wheat paratha."),
            ("Aloo Paratha", 180m, "Paratha stuffed with spiced mashed potato."),
            ("Puri", 60m, "Light, golden deep-fried bread."),
            ("Roghni Naan", 160m, "Enriched naan with sesame and a glossy finish."),
            ("Sesame Naan", 170m, "Naan scattered with toasted sesame seeds.")
        ]),
        new("Fast Food", 7,
        [
            ("Zinger Burger", 650m, "Crispy chicken fillet, lettuce and mayo in a soft bun."),
            ("Beef Burger", 550m, "Grilled beef patty with cheese, onion and house sauce."),
            ("Chicken Cheese Burger", 720m, "Crumbed chicken with a double cheese layer."),
            ("Chicken Wrap", 480m, "Grilled chicken, salad and garlic sauce in a warm wrap."),
            ("Beef Wrap", 520m, "Spiced beef strips with peppers and yoghurt sauce."),
            ("Club Sandwich", 580m, "Triple-decker chicken, egg and salad sandwich."),
            ("Chicken Shawarma", 350m, "Shaved chicken, pickles and garlic sauce in pita."),
            ("Beef Shawarma", 400m, "Marinated beef shawarma with tahini and salad."),
            ("Margherita Pizza", 950m, "Tomato, mozzarella and basil on a thin crust."),
            ("Chicken Fajita Pizza", 1250m, "Chicken, peppers, onion and mozzarella.")
        ]),
        new("Desserts", 8,
        [
            ("Gulab Jamun", 250m, "Warm milk dumplings soaked in rose-cardamom syrup."),
            ("Ras Malai", 320m, "Soft cheese discs in chilled saffron milk."),
            ("Gajar Halwa", 350m, "Carrot halwa slow-cooked with milk and ghee."),
            ("Kheer", 280m, "Creamy rice pudding with cardamom and pistachio."),
            ("Chocolate Lava Cake", 450m, "Warm chocolate cake with a molten centre."),
            ("Cheesecake Slice", 520m, "Baked vanilla cheesecake on a biscuit base."),
            ("Tiramisu", 550m, "Coffee-soaked sponge with mascarpone cream."),
            ("Falooda", 380m, "Rose syrup, vermicelli, basil seeds and ice cream."),
            ("Kulfi", 200m, "Traditional slow-churned milk ice cream."),
            ("Brownie with Ice Cream", 480m, "Fudgy brownie served with vanilla ice cream.")
        ]),
        new("Beverages", 9,
        [
            ("Masala Chai", 120m, "Strong tea brewed with milk, cardamom and ginger."),
            ("Green Tea", 100m, "Light jasmine green tea served hot."),
            ("Fresh Lime", 180m, "Lime, soda and a pinch of black salt."),
            ("Mint Margarita", 250m, "Blended mint, lime and ice for a cooling finish."),
            ("Mango Lassi", 280m, "Yoghurt blended with ripe mango and honey."),
            ("Sweet Lassi", 240m, "Chilled sweetened yoghurt drink."),
            ("Cold Coffee", 350m, "Blended coffee with milk and a scoop of ice cream."),
            ("Soft Drink", 100m, "Chilled canned soft drink of your choice."),
            ("Mineral Water", 60m, "500 ml sealed mineral water."),
            ("Fresh Orange Juice", 300m, "Freshly squeezed orange juice, no added sugar.")
        ])
    ];

    public static IReadOnlyList<CategorySeed> DemoCategories => Categories;

    public static async Task<int> SeedAsync(
        ApplicationDbContext db,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(db);

        db.DisableTenantFilters = true;

        var tenant = await db.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t =>
                t.Id == DemoTenantId || t.Slug == TenantSlug, ct);

        if (tenant is not null && tenant.Id != DemoTenantId)
        {
            // The demo slug is already used by a different tenant. Never
            // attach demo data to an existing arbitrary workspace.
            return -1;
        }

        if (tenant is null)
        {
            tenant = Tenant.Create(TenantName, TenantSlug);
            tenant.Id = DemoTenantId;
            tenant.ChangeStatus(TenantStatus.Active);
            db.Tenants.Add(tenant);
            await db.SaveChangesAsync(ct);
        }

        var restaurant = await db.Restaurants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.Id == DemoRestaurantId, ct);

        if (restaurant is null)
        {
            restaurant = Restaurant.Create(
                DemoTenantId,
                RestaurantName,
                Address.Create(
                    "12 Gulberg Main Boulevard",
                    "Lahore",
                    "Pakistan",
                    postalCode: "54660"),
                description: "Demo workspace with a complete 100-item menu.",
                timeZone: "Asia/Karachi");

            restaurant.Id = DemoRestaurantId;
            restaurant.ChangeStatus(RestaurantStatus.Active);
            db.Restaurants.Add(restaurant);
            await db.SaveChangesAsync(ct);
        }

        var menu = await db.Menus
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(m => m.Id == DemoMenuId, ct);

        if (menu is null)
        {
            menu = Menu.Create(
                DemoTenantId,
                DemoRestaurantId,
                MenuName,
                "A realistic demo menu used to exercise pagination, filtering and image uploads.");
            menu.Id = DemoMenuId;
            db.Menus.Add(menu);
            await db.SaveChangesAsync(ct);
        }

        var existingCategoryIds = await db.MenuCategories
            .IgnoreQueryFilters()
            .Where(c => c.MenuId == DemoMenuId)
            .Select(c => c.Id)
            .ToHashSetAsync(ct);

        var categoryIdByIndex = new Guid[Categories.Count];

        for (var i = 0; i < Categories.Count; i++)
        {
            var definition = Categories[i];
            categoryIdByIndex[i] = CategoryId(i);

            if (existingCategoryIds.Contains(categoryIdByIndex[i]))
                continue;

            var category = MenuCategory.Create(
                DemoMenuId,
                definition.Name,
                definition.Description,
                definition.DisplayOrder);
            category.Id = categoryIdByIndex[i];
            db.MenuCategories.Add(category);
        }

        await db.SaveChangesAsync(ct);

        if (menu.Status == MenuStatus.Draft)
        {
            menu.Publish(Categories.Count);
            await db.SaveChangesAsync(ct);
        }

        var existingItemIds = await db.MenuItems
            .IgnoreQueryFilters()
            .Where(i => i.Category.MenuId == DemoMenuId)
            .Select(i => i.Id)
            .ToHashSetAsync(ct);

        var itemIndex = 1;

        foreach (var definition in Categories)
        {
            var categoryId = CategoryId(definition.DisplayOrder);

            for (var order = 0; order < definition.Items.Count; order++)
            {
                var itemDefinition = definition.Items[order];
                var itemId = ItemId(itemIndex++);

                if (existingItemIds.Contains(itemId))
                    continue;

                var item = MenuItem.Create(
                    categoryId,
                    itemDefinition.Name,
                    Money.New(itemDefinition.Price, "PKR"),
                    itemDefinition.Description,
                    imageFileId: null,
                    displayOrder: order);
                item.Id = itemId;
                db.MenuItems.Add(item);
            }
        }

        await db.SaveChangesAsync(ct);

        await EnsureDemoOwnerMembershipAsync(db, ct);

        return await db.MenuItems
            .IgnoreQueryFilters()
            .CountAsync(i => i.Category.MenuId == DemoMenuId, ct);
    }

    private static async Task EnsureDemoOwnerMembershipAsync(
        ApplicationDbContext db,
        CancellationToken ct)
    {
        var normalizedEmail = DemoOwnerEmail.ToUpperInvariant();

        var owner = await db.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail, ct);

        if (owner is null)
            return;

        var ownerRoleId = await db.Roles
            .IgnoreQueryFilters()
            .Where(r => r.NormalizedName == "OWNER")
            .Select(r => (Guid?)r.Id)
            .FirstOrDefaultAsync(ct);

        if (ownerRoleId is null)
            return;

        var hasMembership = await db.StaffMemberships
            .IgnoreQueryFilters()
            .AnyAsync(m =>
                m.TenantId == DemoTenantId &&
                m.UserId == owner.Id &&
                m.Status == MembershipStatus.Active, ct);

        if (hasMembership)
            return;

        db.StaffMemberships.Add(
            StaffMembership.Create(DemoTenantId, owner.Id, ownerRoleId.Value));

        await db.SaveChangesAsync(ct);
    }

    private static Guid CategoryId(int index) =>
        Guid.Parse($"d3e0c000-0000-4000-8000-{index + 1:D12}");

    private static Guid ItemId(int index) =>
        Guid.Parse($"d3e0b000-0000-4000-8000-{index:D12}");

    public sealed record CategorySeed(
        string Name,
        int DisplayOrder,
        IReadOnlyList<(string Name, decimal Price, string Description)> Items)
    {
        public string? Description => null;
    }
}
