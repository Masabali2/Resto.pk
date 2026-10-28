
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
/// Missing templates (matched by their stable <c>TemplateKey</c>) are inserted.
/// Existing templates keep every platform-admin edit — name, description,
/// active flag and all design content — while the manifest-managed image
/// fields (<c>assets</c>, <c>heroAssetId</c>, <c>aboutAssetId</c> and the
/// ordered <c>galleryAssetIds</c>) are reconciled from the checked-in
/// manifests. This is what repairs a row that was seeded before its images
/// existed without overwriting reviewed copy or styling.
///
/// A row whose stored configuration is empty or unreadable is never touched,
/// and a configuration is only written when its text actually changes, so
/// repeated seeding performs no database writes.
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

        var existing = await db.WebsiteTemplates.ToListAsync(ct);

        var byKey = existing.ToDictionary(
            template => template.TemplateKey,
            StringComparer.OrdinalIgnoreCase);

        var changed = 0;

        foreach (var preset in Presets)
        {
            if (!byKey.TryGetValue(preset.TemplateKey, out var template))
            {
                template = WebsiteTemplate.Create(
                    preset.Name,
                    preset.TemplateKey,
                    preset.Description);

                template.SetDesignConfiguration(preset.DesignConfigurationJson);

                db.WebsiteTemplates.Add(template);
                byKey.Add(preset.TemplateKey, template);
                changed++;
                continue;
            }

            var manifest = WebsiteTemplateAssetManifestRegistry.Find(preset.TemplateKey);

            if (manifest is null)
                continue;

            var reconciled = WebsiteTemplateAssetConfigurationBuilder.Reconcile(
                template.DesignConfigurationJson,
                manifest);

            if (reconciled is null ||
                string.Equals(reconciled, template.DesignConfigurationJson, StringComparison.Ordinal))
            {
                continue;
            }

            template.SetDesignConfiguration(reconciled);
            changed++;
        }

        if (changed > 0)
            await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Creates a preset through the one generic image builder. The stable
    /// template key selects the optional manifest; the base document stays
    /// authoritative for every other property. Templates without a manifest
    /// are passed through unchanged.
    /// </summary>
    private static WebsiteTemplatePreset Preset(
        string name,
        string templateKey,
        string description,
        string baseDesignConfiguration) =>
        new(
            name,
            templateKey,
            description,
            WebsiteTemplateAssetConfigurationBuilder.Apply(templateKey, baseDesignConfiguration));

    /// <summary>
    /// Authoritative Spice Garden design settings and preview copy. The image
    /// registry and the hero/about/gallery references are added by
    /// <see cref="WebsiteTemplateAssetConfigurationBuilder"/> from the
    /// checked-in manifest, so filenames and URLs never live in the Angular
    /// client.
    /// </summary>
    private const string SpiceGardenBaseDesignConfiguration = """
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
              "accent": "#D9A441",
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
        """;

    public static IReadOnlyList<WebsiteTemplatePreset> Presets { get; } =
    [
        Preset(
            "Spice Garden",
            "spice-garden",
            "Forest-green, warm-ivory and antique-gold hospitality inspired by premium Pakistani dining, with editorial serif headings and food-first storytelling.",
            SpiceGardenBaseDesignConfiguration),
        Preset(
            "Ember & Oak",
            "ember-oak",
            "A cinematic fine-dining identity with charcoal, champagne and burnt-bronze tones, elegant typography and generous premium spacing.",
            """
            {
              "schemaVersion": 1,
              "category": "Fine dining",
              "tags": ["fine-dining", "premium", "tasting-menu"],
              "featured": true,
              "displayOrder": 2,
              "design": {
                "palette": {
                  "background": "#F5EFE5",
                  "surface": "#FFFCF7",
                  "text": "#252321",
                  "mutedText": "#716A62",
                  "primary": "#252321",
                  "accent": "#B87543",
                  "border": "#E5D9C9"
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
        Preset(
            "Urban Slice",
            "urban-slice",
            "A high-energy pizza identity with tomato-red, golden-yellow and warm-white tones, bold typography and an order-first menu.",
            """
            {
              "schemaVersion": 1,
              "category": "Pizza / Fast casual",
              "tags": ["pizza", "fast-casual", "delivery"],
              "featured": true,
              "displayOrder": 3,
              "design": {
                "palette": {
                  "background": "#FFF9F0",
                  "surface": "#FFFFFF",
                  "text": "#272329",
                  "mutedText": "#6B6265",
                  "primary": "#D7263D",
                  "accent": "#FFC857",
                  "border": "#F0E2D7"
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
        Preset(
            "Coastal Table",
            "coastal-table",
            "A fresh coastal identity with deep teal, seafoam and white, airy spacing and a calm seafood-led presentation.",
            """
            {
              "schemaVersion": 1,
              "category": "Seafood / Coastal",
              "tags": ["seafood", "coastal", "fresh"],
              "featured": false,
              "displayOrder": 4,
              "design": {
                "palette": {
                  "background": "#F3FAF8",
                  "surface": "#FFFFFF",
                  "text": "#153B3D",
                  "mutedText": "#587779",
                  "primary": "#126B70",
                  "accent": "#C9E8DF",
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
        Preset(
            "Bloom Café",
            "bloom-cafe",
            "A boutique café identity with dusty rose, cream and espresso-brown tones, elegant editorial typography and soft card styling.",
            """
            {
              "schemaVersion": 1,
              "category": "Café / Bakery",
              "tags": ["cafe", "bakery", "coffee"],
              "featured": true,
              "displayOrder": 5,
              "design": {
                "palette": {
                  "background": "#FFF4F0",
                  "surface": "#FFFFFF",
                  "text": "#49312C",
                  "mutedText": "#806A64",
                  "primary": "#B5647C",
                  "accent": "#D9A441",
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
        Preset(
            "Olive Grove",
            "olive-grove",
            "A sun-washed Mediterranean identity with olive green, terracotta and natural ivory, organic warmth and editorial split sections.",
            """
            {
              "schemaVersion": 1,
              "category": "Mediterranean",
              "tags": ["mediterranean", "mezze", "natural-wine"],
              "featured": false,
              "displayOrder": 6,
              "design": {
                "palette": {
                  "background": "#F7F2E8",
                  "surface": "#FFFDF8",
                  "text": "#30382A",
                  "mutedText": "#6E7461",
                  "primary": "#667548",
                  "accent": "#C87952",
                  "border": "#E3DCCB"
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
        Preset(
            "Midnight Sushi",
            "midnight-sushi",
            "A premium Japanese identity with obsidian black, soft ivory and vermilion, restrained typography and a refined, quiet menu.",
            """
            {
              "schemaVersion": 1,
              "category": "Japanese / Premium",
              "tags": ["sushi", "japanese", "omakase"],
              "featured": true,
              "displayOrder": 7,
              "design": {
                "palette": {
                  "background": "#111318",
                  "surface": "#1C1E23",
                  "text": "#F6F3EC",
                  "mutedText": "#A6A9B0",
                  "primary": "#F6F3EC",
                  "accent": "#C8321E",
                  "border": "#30333A"
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
        Preset(
            "Heritage Kitchen",
            "heritage-kitchen",
            "A traditional grill-house identity with deep maroon, antique gold and parchment, combining heritage warmth with clear modern navigation.",
            """
            {
              "schemaVersion": 1,
              "category": "Traditional / BBQ",
              "tags": ["bbq", "traditional", "charcoal"],
              "featured": false,
              "displayOrder": 8,
              "design": {
                "palette": {
                  "background": "#F8F0DF",
                  "surface": "#FFFBF2",
                  "text": "#35211F",
                  "mutedText": "#75635A",
                  "primary": "#762C38",
                  "accent": "#D1AF55",
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
        Preset(
            "Green Fork",
            "green-fork",
            "A fresh plant-forward identity with botanical green, lime and white, bright menu cards and clear dietary labelling where data exists.",
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
                  "primary": "#24543C",
                  "accent": "#A6C95B",
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
        Preset(
            "Street Feast",
            "street-feast",
            "A vivid street-food identity with vibrant orange, ink black and warm cream, expressive typography and a prominent ordering experience.",
            """
            {
              "schemaVersion": 1,
              "category": "Street food / Casual",
              "tags": ["street-food", "casual", "late-night"],
              "featured": false,
              "displayOrder": 10,
              "design": {
                "palette": {
                  "background": "#FFF5EA",
                  "surface": "#FFFFFF",
                  "text": "#191512",
                  "mutedText": "#655B53",
                  "primary": "#F05A24",
                  "accent": "#191512",
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
