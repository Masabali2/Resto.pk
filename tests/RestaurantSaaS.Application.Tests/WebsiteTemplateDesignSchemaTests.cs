using FluentAssertions;
using RestaurantSaaS.Application.Common;

namespace RestaurantSaaS.Application.Tests;

public class WebsiteTemplateDesignSchemaTests
{
    private const string ValidDesign = """
    {
      "schemaVersion": 1,
      "category": "South Asian / Desi",
      "tags": ["desi", "family-dining"],
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
        "typography": { "headingFont": "serif-editorial", "bodyFont": "sans-modern" },
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
          { "type": "hero", "enabled": true, "order": 1 },
          { "type": "menu", "enabled": true, "order": 2 }
        ]
      },
      "previewContent": {
        "eyebrow": "Authentic flavours",
        "heroTitle": "A Taste of Tradition",
        "heroDescription": "Comforting dishes prepared with fresh ingredients.",
        "primaryActionLabel": "Explore Our Menu",
        "sampleDishes": [
          { "name": "Signature Karahi", "price": "PKR 1,650" }
        ]
      }
    }
    """;

    [Fact]
    public void Validate_CompleteDocument_ReturnsNull()
    {
        WebsiteTemplateDesignSchema.Validate(ValidDesign).Should().BeNull();
    }

    [Fact]
    public void Validate_Null_IsAccepted()
    {
        WebsiteTemplateDesignSchema.Validate(null).Should().BeNull();
    }

    [Fact]
    public void Validate_MalformedJson_IsRejected()
    {
        var error = WebsiteTemplateDesignSchema.Validate("{ \"schemaVersion\": 1, ");

        error.Should().NotBeNull();
        error!.Should().Contain("well-formed JSON");
    }

    [Fact]
    public void Validate_NonObjectRoot_IsRejected()
    {
        WebsiteTemplateDesignSchema.Validate("[]").Should().NotBeNull();
    }

    [Theory]
    [InlineData(2)]
    [InlineData(0)]
    public void Validate_UnsupportedSchemaVersion_IsRejected(int version)
    {
        var error = WebsiteTemplateDesignSchema.Validate($"{{\"schemaVersion\":{version}}}");

        error.Should().NotBeNull();
        error!.Should().Contain("schemaVersion");
    }

    [Fact]
    public void Validate_InvalidColour_IsRejected()
    {
        var error = WebsiteTemplateDesignSchema.Validate("""
        {
          "schemaVersion": 1,
          "design": { "palette": { "primary": "green" } }
        }
        """);

        error.Should().NotBeNull();
        error!.Should().Contain("primary");
    }

    [Fact]
    public void Validate_UnknownPaletteColour_IsRejected()
    {
        var error = WebsiteTemplateDesignSchema.Validate("""
        {
          "schemaVersion": 1,
          "design": { "palette": { "neon": "#FFFFFF" } }
        }
        """);

        error.Should().NotBeNull();
        error!.Should().Contain("neon");
    }

    [Fact]
    public void Validate_UnsupportedLayoutVariant_IsRejected()
    {
        var error = WebsiteTemplateDesignSchema.Validate("""
        {
          "schemaVersion": 1,
          "design": { "layout": { "hero": "holographic" } }
        }
        """);

        error.Should().NotBeNull();
        error!.Should().Contain("hero");
    }

    [Fact]
    public void Validate_UnsupportedTypographyToken_IsRejected()
    {
        var error = WebsiteTemplateDesignSchema.Validate("""
        {
          "schemaVersion": 1,
          "design": { "typography": { "headingFont": "comic-neue" } }
        }
        """);

        error.Should().NotBeNull();
        error!.Should().Contain("headingFont");
    }

    [Fact]
    public void Validate_UnsupportedSectionType_IsRejected()
    {
        var error = WebsiteTemplateDesignSchema.Validate("""
        {
          "schemaVersion": 1,
          "design": { "sections": [ { "type": "carousel-of-carousels", "order": 1 } ] }
        }
        """);

        error.Should().NotBeNull();
        error!.Should().Contain("carousel-of-carousels");
    }

    [Fact]
    public void Validate_UnknownTopLevelProperty_IsRejected()
    {
        var error = WebsiteTemplateDesignSchema.Validate("""
        {
          "schemaVersion": 1,
          "customCss": "body { display: none; }"
        }
        """);

        error.Should().NotBeNull();
        error!.Should().Contain("customCss");
    }

    [Fact]
    public void Validate_ExecutableContentInPreviewCopy_IsRejected()
    {
        var error = WebsiteTemplateDesignSchema.Validate("""
        {
          "schemaVersion": 1,
          "previewContent": { "heroTitle": "<script>alert(1)</script>" }
        }
        """);

        error.Should().NotBeNull();
        error!.Should().Contain("heroTitle");
    }

    [Fact]
    public void Validate_JavaScriptUrlInPreviewCopy_IsRejected()
    {
        var error = WebsiteTemplateDesignSchema.Validate("""
        {
          "schemaVersion": 1,
          "previewContent": { "heroTitle": "javascript:alert(1)" }
        }
        """);

        error.Should().NotBeNull();
    }

    [Fact]
    public void Validate_TagWithIllegalCharacters_IsRejected()
    {
        var error = WebsiteTemplateDesignSchema.Validate("""
        {
          "schemaVersion": 1,
          "tags": ["desi food!"]
        }
        """);

        error.Should().NotBeNull();
        error!.Should().Contain("Tags");
    }

    [Fact]
    public void Validate_TooManySections_IsRejected()
    {
        var sections = string.Join(
            ",",
            Enumerable.Range(0, 13)
                .Select(i => $"{{\"type\":\"hero\",\"order\":{i}}}"));

        var error = WebsiteTemplateDesignSchema.Validate(
            $"{{\"schemaVersion\":1,\"design\":{{\"sections\":[{sections}]}}}}");

        error.Should().NotBeNull();
        error!.Should().Contain("sections");
    }
}
