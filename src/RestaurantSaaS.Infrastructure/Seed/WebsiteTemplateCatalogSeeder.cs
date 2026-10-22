
using Microsoft.EntityFrameworkCore;
using RestaurantSaaS.Domain.Entities.Websites;
using RestaurantSaaS.Infrastructure.Persistence;

namespace RestaurantSaaS.Infrastructure.Seed;

/// <summary>
/// A single, ready-made entry of the platform-owned website template
/// catalogue. The design configuration is DATA consumed by the generic
/// renderer — it never contains HTML, JavaScript or CSS.
/// </summary>
public sealed record WebsiteTemplatePreset(
    string Name,
    string TemplateKey,
    string Description,
    string DesignConfigurationJson);

/// <summary>
/// Idempotent catalogue seeder for the global website template catalogue.
///
/// The seeder INSERT-ONLY: a template that already exists (matched by its
/// stable <c>TemplateKey</c>) is never touched, so platform-admin edits —
/// renamed templates, retires, custom design configurations — survive a
/// re-run. Only genuinely missing templates are added.
///
/// The catalogue lives in the backend on purpose: it is the single source of
/// truth for the tenant catalogue endpoint and for the generic renderer. The
/// Angular client never keeps a second hard-coded copy.
/// </summary>
public static class WebsiteTemplateCatalogSeeder
{
    public static async Task SeedAsync(ApplicationDbContext db, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(db);

        var existingKeys = await db.WebsiteTemplates
            .AsNoTracking()
            .Select(t => t.TemplateKey)
            .ToListAsync(ct);

        var known = new HashSet<string>(existingKeys, StringComparer.OrdinalIgnoreCase);
        var added = 0;

        foreach (var preset in Presets)
        {
            if (!known.Add(preset.TemplateKey))
                continue;

            var template = WebsiteTemplate.Create(
                preset.Name,
                preset.TemplateKey,
                preset.Description);

            template.SetDesignConfiguration(preset.DesignConfigurationJson);

            db.WebsiteTemplates.Add(template);
            added++;
        }

        if (added > 0)
            await db.SaveChangesAsync(ct);
    }

    public static IReadOnlyList<WebsiteTemplatePreset> Presets { get; } =
    [
        new WebsiteTemplatePreset(
            "Spice Garden",
            "spice-garden",
            "Warm desi hospitality with editorial serif headings and a generous, food-first layout.",
            """
            {
              "schemaVersion": 1,
              "category": "South Asian / Desi",
              "tags": ["desi", "family-dining", "traditional"],
              "featured": true,
              "displayOrder": 1,
              "design": {
                "palette": {
                  "background": "#FFF8EE",
                  "surface": "#FFFFFF",
                  "text": "#24352A",
                  "mutedText": "#667368",
                  "primary": "#24563A",
                  "accent": "#D99027",
                  "border": "#E9DFCE"
                },
                "typography": {
                  "headingFont": "serif-editorial",
                  "bodyFont": "sans-humanist"
                },
                "layout": {
                  "header": "centered",
                  "hero": "split",
                  "menu": "featured-cards",
                  "gallery": "masonry",
                  "footer": "multi-column",
                  "buttons": "solid",
                  "corners": "soft"
                },
                "sections": [
                  { "type": "header", "enabled": true, "order": 1 },
                  { "type": "hero", "enabled": true, "order": 2 },
                  { "type": "menu", "enabled": true, "order": 3 },
                  { "type": "about", "enabled": true, "order": 4 },
                  { "type": "gallery", "enabled": true, "order": 5 },
                  { "type": "contact", "enabled": true, "order": 6 },
                  { "type": "footer", "enabled": true, "order": 7 }
                ]
              },
              "previewContent": {
                "eyebrow": "Authentic flavours, warmly served",
                "heroTitle": "A Taste of Tradition",
                "heroDescription": "Discover comforting dishes prepared with fresh ingredients and slow-cooked gravies.",
                "primaryActionLabel": "Explore Our Menu",
                "sampleDishes": [
                  { "name": "Signature Karahi", "price": "PKR 1,650" },
                  { "name": "Smoky Chicken Tikka", "price": "PKR 950" },
                  { "name": "Daal Makhani", "price": "PKR 780" }
                ]
              }
            }
            """),
        new WebsiteTemplatePreset(
            "Ember & Oak",
            "ember-oak",
            "Fine dining with a cinematic, image-led hero and calm premium spacing.",
            """
            {
              "schemaVersion": 1,
              "category": "Fine dining",
              "tags": ["fine-dining", "premium", "tasting-menu"],
              "featured": true,
              "displayOrder": 2,
              "design": {
                "palette": {
                  "background": "#F7F2EA",
                  "surface": "#FFFFFF",
                  "text": "#1C1B1A",
                  "mutedText": "#6B6560",
                  "primary": "#2E2A27",
                  "accent": "#B0672A",
                  "border": "#E3D9CB"
                },
                "typography": {
                  "headingFont": "serif-editorial",
                  "bodyFont": "sans-modern"
                },
                "layout": {
                  "header": "centered",
                  "hero": "cinematic",
                  "menu": "editorial-list",
                  "gallery": "strip",
                  "footer": "simple",
                  "buttons": "outline",
                  "corners": "sharp"
                },
                "sections": [
                  { "type": "header", "enabled": true, "order": 1 },
                  { "type": "hero", "enabled": true, "order": 2 },
                  { "type": "about", "enabled": true, "order": 3 },
                  { "type": "menu", "enabled": true, "order": 4 },
                  { "type": "gallery", "enabled": true, "order": 5 },
                  { "type": "contact", "enabled": true, "order": 6 },
                  { "type": "footer", "enabled": true, "order": 7 }
                ]
              },
              "previewContent": {
                "eyebrow": "An evening, composed course by course",
                "heroTitle": "Ember, Oak & Patience",
                "heroDescription": "Seasonal plates cooked over live fire and served in an unhurried dining room.",
                "primaryActionLabel": "Reserve a Table",
                "sampleDishes": [
                  { "name": "Charred Octopus, Smoked Paprika", "price": "PKR 3,200" },
                  { "name": "Dry-Aged Ribeye, Bone Marrow", "price": "PKR 5,400" },
                  { "name": "Burnt Honey Panna Cotta", "price": "PKR 1,100" }
                ]
              }
            }
            """),
        new WebsiteTemplatePreset(
            "Urban Slice",
            "urban-slice",
            "Bold, fast-casual design built around a prominent ordering action and compact menu cards.",
            """
            {
              "schemaVersion": 1,
              "category": "Pizza / Fast casual",
              "tags": ["pizza", "fast-casual", "delivery"],
              "featured": true,
              "displayOrder": 3,
              "design": {
                "palette": {
                  "background": "#FAF7F2",
                  "surface": "#FFFFFF",
                  "text": "#22252A",
                  "mutedText": "#636B73",
                  "primary": "#D7263D",
                  "accent": "#F5A524",
                  "border": "#E6DFD6"
                },
                "typography": {
                  "headingFont": "sans-bold",
                  "bodyFont": "sans-modern"
                },
                "layout": {
                  "header": "split",
                  "hero": "image-led",
                  "menu": "compact-cards",
                  "gallery": "grid",
                  "footer": "multi-column",
                  "buttons": "pill",
                  "corners": "sharp"
                },
                "sections": [
                  { "type": "header", "enabled": true, "order": 1 },
                  { "type": "hero", "enabled": true, "order": 2 },
                  { "type": "menu", "enabled": true, "order": 3 },
                  { "type": "about", "enabled": true, "order": 4 },
                  { "type": "gallery", "enabled": true, "order": 5 },
                  { "type": "contact", "enabled": true, "order": 6 },
                  { "type": "footer", "enabled": true, "order": 7 }
                ]
              },
              "previewContent": {
                "eyebrow": "Sliced, stacked and shipped hot",
                "heroTitle": "Big Slices. Zero Fuss.",
                "heroDescription": "Wood-fired bases, generous toppings and a checkout that takes seconds.",
                "primaryActionLabel": "Order Now",
                "sampleDishes": [
                  { "name": "Margherita Sourdough", "price": "PKR 1,250" },
                  { "name": "Spicy Pepperoni Stack", "price": "PKR 1,650" },
                  { "name": "Loaded Garlic Fries", "price": "PKR 550" }
                ]
              }
            }
            """),
        new WebsiteTemplatePreset(
            "Coastal Table",
            "coastal-table",
            "Airy coastal design with a centered hero and spacious, unhurried menu presentation.",
            """
            {
              "schemaVersion": 1,
              "category": "Seafood / Coastal",
              "tags": ["seafood", "coastal", "fresh"],
              "featured": false,
              "displayOrder": 4,
              "design": {
                "palette": {
                  "background": "#F4FAF9",
                  "surface": "#FFFFFF",
                  "text": "#123B3E",
                  "mutedText": "#5B7578",
                  "primary": "#0E6F73",
                  "accent": "#2E9C8F",
                  "border": "#D3E6E4"
                },
                "typography": {
                  "headingFont": "serif-editorial",
                  "bodyFont": "sans-humanist"
                },
                "layout": {
                  "header": "centered",
                  "hero": "centered",
                  "menu": "list-rows",
                  "gallery": "grid",
                  "footer": "multi-column",
                  "buttons": "outline",
                  "corners": "rounded"
                },
                "sections": [
                  { "type": "header", "enabled": true, "order": 1 },
                  { "type": "hero", "enabled": true, "order": 2 },
                  { "type": "menu", "enabled": true, "order": 3 },
                  { "type": "about", "enabled": true, "order": 4 },
                  { "type": "gallery", "enabled": true, "order": 5 },
                  { "type": "contact", "enabled": true, "order": 6 },
                  { "type": "footer", "enabled": true, "order": 7 }
                ]
              },
              "previewContent": {
                "eyebrow": "Landed this morning",
                "heroTitle": "The Coast, Served Daily",
                "heroDescription": "Line-caught fish, cold shellfish and simple preparations by the water.",
                "primaryActionLabel": "View Today's Catch",
                "sampleDishes": [
                  { "name": "Grilled King Fish", "price": "PKR 2,450" },
                  { "name": "Garlic Prawns", "price": "PKR 1,890" },
                  { "name": "Lemon Seafood Chowder", "price": "PKR 950" }
                ]
              }
            }
            """),
        new WebsiteTemplatePreset(
            "Bloom Café",
            "bloom-cafe",
            "A soft, elegant café design with blush tones, rounded cards and editorial typography.",
            """
            {
              "schemaVersion": 1,
              "category": "Café / Bakery",
              "tags": ["cafe", "bakery", "coffee"],
              "featured": true,
              "displayOrder": 5,
              "design": {
                "palette": {
                  "background": "#FDF5F7",
                  "surface": "#FFFFFF",
                  "text": "#3A2620",
                  "mutedText": "#7C635A",
                  "primary": "#B5647C",
                  "accent": "#D99027",
                  "border": "#EFDCE0"
                },
                "typography": {
                  "headingFont": "serif-traditional",
                  "bodyFont": "serif-editorial"
                },
                "layout": {
                  "header": "left",
                  "hero": "split",
                  "menu": "featured-cards",
                  "gallery": "carousel",
                  "footer": "centered",
                  "buttons": "pill",
                  "corners": "rounded"
                },
                "sections": [
                  { "type": "header", "enabled": true, "order": 1 },
                  { "type": "hero", "enabled": true, "order": 2 },
                  { "type": "menu", "enabled": true, "order": 3 },
                  { "type": "about", "enabled": true, "order": 4 },
                  { "type": "gallery", "enabled": true, "order": 5 },
                  { "type": "contact", "enabled": true, "order": 6 },
                  { "type": "footer", "enabled": true, "order": 7 }
                ]
              },
              "previewContent": {
                "eyebrow": "Roasted in-house, baked before dawn",
                "heroTitle": "Slow Mornings Start Here",
                "heroDescription": "Single-origin coffee, laminated pastries and a table waiting for you.",
                "primaryActionLabel": "See the Counter",
                "sampleDishes": [
                  { "name": "Flat White", "price": "PKR 650" },
                  { "name": "Almond Croissant", "price": "PKR 520" },
                  { "name": "Sourdough Focaccia", "price": "PKR 480" }
                ]
              }
            }
            """),
        new WebsiteTemplatePreset(
            "Olive Grove",
            "olive-grove",
            "Mediterranean naturalism with editorial split sections and a balanced gallery.",
            """
            {
              "schemaVersion": 1,
              "category": "Mediterranean",
              "tags": ["mediterranean", "mezze", "natural-wine"],
              "featured": false,
              "displayOrder": 6,
              "design": {
                "palette": {
                  "background": "#F6F3E9",
                  "surface": "#FFFDF8",
                  "text": "#2F3524",
                  "mutedText": "#6E7461",
                  "primary": "#5B6B3A",
                  "accent": "#C2703D",
                  "border": "#DFDACA"
                },
                "typography": {
                  "headingFont": "serif-traditional",
                  "bodyFont": "sans-humanist"
                },
                "layout": {
                  "header": "left",
                  "hero": "split",
                  "menu": "grid-cards",
                  "gallery": "masonry",
                  "footer": "multi-column",
                  "buttons": "solid",
                  "corners": "soft"
                },
                "sections": [
                  { "type": "header", "enabled": true, "order": 1 },
                  { "type": "hero", "enabled": true, "order": 2 },
                  { "type": "about", "enabled": true, "order": 3 },
                  { "type": "menu", "enabled": true, "order": 4 },
                  { "type": "gallery", "enabled": true, "order": 5 },
                  { "type": "contact", "enabled": true, "order": 6 },
                  { "type": "footer", "enabled": true, "order": 7 }
                ]
              },
              "previewContent": {
                "eyebrow": "Pressed olives, stone fruit, warm bread",
                "heroTitle": "A Long Table Under the Trees",
                "heroDescription": "Mezze to share, grilled over vine cuttings, with a short and honest wine list.",
                "primaryActionLabel": "Explore the Mezze",
                "sampleDishes": [
                  { "name": "Hummus & Warm Flatbread", "price": "PKR 850" },
                  { "name": "Grilled Lamb Kofta", "price": "PKR 1,750" },
                  { "name": "Baked Feta & Peppers", "price": "PKR 1,100" }
                ]
              }
            }
            """),
        new WebsiteTemplatePreset(
            "Midnight Sushi",
            "midnight-sushi",
            "Minimal Japanese precision with a restrained palette and a refined, quiet menu layout.",
            """
            {
              "schemaVersion": 1,
              "category": "Japanese / Premium",
              "tags": ["sushi", "japanese", "omakase"],
              "featured": true,
              "displayOrder": 7,
              "design": {
                "palette": {
                  "background": "#101114",
                  "surface": "#181A1E",
                  "text": "#F6F3EC",
                  "mutedText": "#A6A9B0",
                  "primary": "#F6F3EC",
                  "accent": "#C8321E",
                  "border": "#2A2D33"
                },
                "typography": {
                  "headingFont": "display-wide",
                  "bodyFont": "sans-modern"
                },
                "layout": {
                  "header": "left",
                  "hero": "cinematic",
                  "menu": "editorial-list",
                  "gallery": "strip",
                  "footer": "simple",
                  "buttons": "outline",
                  "corners": "sharp"
                },
                "sections": [
                  { "type": "header", "enabled": true, "order": 1 },
                  { "type": "hero", "enabled": true, "order": 2 },
                  { "type": "menu", "enabled": true, "order": 3 },
                  { "type": "about", "enabled": true, "order": 4 },
                  { "type": "gallery", "enabled": true, "order": 5 },
                  { "type": "contact", "enabled": true, "order": 6 },
                  { "type": "footer", "enabled": true, "order": 7 }
                ]
              },
              "previewContent": {
                "eyebrow": "Twelve seats, one counter",
                "heroTitle": "Kaiseki, After Hours",
                "heroDescription": "A quiet counter, a short omakase and rice seasoned to the minute.",
                "primaryActionLabel": "Book a Seat",
                "sampleDishes": [
                  { "name": "Omakase nigiri set", "price": "PKR 9,500" },
                  { "name": "Chu-toro", "price": "PKR 2,400" },
                  { "name": "Miso grilled black cod", "price": "PKR 3,100" }
                ]
              }
            }
            """),
        new WebsiteTemplatePreset(
            "Heritage Kitchen",
            "heritage-kitchen",
            "Classic editorial warmth with a modern, accessible navigation for a traditional kitchen.",
            """
            {
              "schemaVersion": 1,
              "category": "Traditional / BBQ",
              "tags": ["bbq", "traditional", "charcoal"],
              "featured": false,
              "displayOrder": 8,
              "design": {
                "palette": {
                  "background": "#F7F0E0",
                  "surface": "#FFFBF2",
                  "text": "#2E1F1B",
                  "mutedText": "#75635A",
                  "primary": "#6B1F2B",
                  "accent": "#C9A227",
                  "border": "#E2D4B8"
                },
                "typography": {
                  "headingFont": "serif-traditional",
                  "bodyFont": "serif-editorial"
                },
                "layout": {
                  "header": "centered",
                  "hero": "split",
                  "menu": "featured-cards",
                  "gallery": "grid",
                  "footer": "multi-column",
                  "buttons": "solid",
                  "corners": "soft"
                },
                "sections": [
                  { "type": "header", "enabled": true, "order": 1 },
                  { "type": "hero", "enabled": true, "order": 2 },
                  { "type": "menu", "enabled": true, "order": 3 },
                  { "type": "about", "enabled": true, "order": 4 },
                  { "type": "gallery", "enabled": true, "order": 5 },
                  { "type": "contact", "enabled": true, "order": 6 },
                  { "type": "footer", "enabled": true, "order": 7 }
                ]
              },
              "previewContent": {
                "eyebrow": "Marinated overnight, smoked all day",
                "heroTitle": "The Grill House Tradition",
                "heroDescription": "Seekh kebabs, charcoal tikka and slow-cooked curry served on a courtyard table.",
                "primaryActionLabel": "Taste the Grill",
                "sampleDishes": [
                  { "name": "Seekh Kebab Platter", "price": "PKR 1,450" },
                  { "name": "Charcoal Beef Tikka", "price": "PKR 1,250" },
                  { "name": "Chicken Karahi", "price": "PKR 1,700" }
                ]
              }
            }
            """),
        new WebsiteTemplatePreset(
            "Green Fork",
            "green-fork",
            "A clean, plant-forward grid with clear dietary labelling where the data exists.",
            """
            {
              "schemaVersion": 1,
              "category": "Healthy / Plant-forward",
              "tags": ["healthy", "vegan", "salads"],
              "featured": false,
              "displayOrder": 9,
              "design": {
                "palette": {
                  "background": "#FBFCF8",
                  "surface": "#FFFFFF",
                  "text": "#1D3328",
                  "mutedText": "#5F7268",
                  "primary": "#1F4D3A",
                  "accent": "#7FB800",
                  "border": "#DCE8DE"
                },
                "typography": {
                  "headingFont": "sans-modern",
                  "bodyFont": "sans-humanist"
                },
                "layout": {
                  "header": "split",
                  "hero": "split",
                  "menu": "grid-cards",
                  "gallery": "grid",
                  "footer": "multi-column",
                  "buttons": "pill",
                  "corners": "rounded"
                },
                "sections": [
                  { "type": "header", "enabled": true, "order": 1 },
                  { "type": "hero", "enabled": true, "order": 2 },
                  { "type": "menu", "enabled": true, "order": 3 },
                  { "type": "about", "enabled": true, "order": 4 },
                  { "type": "gallery", "enabled": true, "order": 5 },
                  { "type": "contact", "enabled": true, "order": 6 },
                  { "type": "footer", "enabled": true, "order": 7 }
                ]
              },
              "previewContent": {
                "eyebrow": "Bowls, greens and grains",
                "heroTitle": "Eat Well, Feel Lighter",
                "heroDescription": "Seasonal bowls assembled to order, with vegan and gluten-free options marked clearly.",
                "primaryActionLabel": "Browse the Bowls",
                "sampleDishes": [
                  { "name": "Charred Corn & Black Bean Bowl", "price": "PKR 890" },
                  { "name": "Miso Aubergine Poke", "price": "PKR 1,150" },
                  { "name": "Green Tahini Salad", "price": "PKR 750" }
                ]
              }
            }
            """),
        new WebsiteTemplatePreset(
            "Street Feast",
            "street-feast",
            "Energetic, order-first design with asymmetric sections and unapologetically bold headings.",
            """
            {
              "schemaVersion": 1,
              "category": "Street food / Casual",
              "tags": ["street-food", "casual", "late-night"],
              "featured": false,
              "displayOrder": 10,
              "design": {
                "palette": {
                  "background": "#FFF8F0",
                  "surface": "#FFFFFF",
                  "text": "#14110F",
                  "mutedText": "#5E564F",
                  "primary": "#FF5A1F",
                  "accent": "#14110F",
                  "border": "#F0DFCE"
                },
                "typography": {
                  "headingFont": "display-wide",
                  "bodyFont": "sans-modern"
                },
                "layout": {
                  "header": "split",
                  "hero": "asymmetric",
                  "menu": "list-rows",
                  "gallery": "strip",
                  "footer": "simple",
                  "buttons": "pill",
                  "corners": "sharp"
                },
                "sections": [
                  { "type": "header", "enabled": true, "order": 1 },
                  { "type": "hero", "enabled": true, "order": 2 },
                  { "type": "menu", "enabled": true, "order": 3 },
                  { "type": "gallery", "enabled": true, "order": 4 },
                  { "type": "about", "enabled": true, "order": 5 },
                  { "type": "contact", "enabled": true, "order": 6 },
                  { "type": "footer", "enabled": true, "order": 7 }
                ]
              },
              "previewContent": {
                "eyebrow": "Open till the last order",
                "heroTitle": "Street Food, Done Loud",
                "heroDescription": "Grilled on the corner, wrapped hot and handed over before it cools.",
                "primaryActionLabel": "Order for Pickup",
                "sampleDishes": [
                  { "name": "Chicken Shawarma Roll", "price": "PKR 650" },
                  { "name": "Loaded Fries", "price": "PKR 450" },
                  { "name": "Malai Boti Wrap", "price": "PKR 780" }
                ]
              }
            }
            """)
    ];
}
