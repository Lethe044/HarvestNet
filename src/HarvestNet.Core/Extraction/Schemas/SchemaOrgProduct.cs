namespace HarvestNet.Core.Extraction.Schemas;

/// <summary>
/// A ready-made mapping of the schema.org Product fields most commonly published in a
/// page's JSON-LD. On any site that already publishes structured product data, this needs
/// no selectors at all:
/// <code>
/// var product = await new ExtractionEngine().ExtractAsync&lt;SchemaOrgProduct&gt;(html, url);
/// </code>
/// A field that the site does not publish, or publishes as something other than a plain
/// string or number (an array of images, for example), is simply left null rather than
/// causing an error.
/// </summary>
public sealed class SchemaOrgProduct
{
    [HarvestField("name", Kind = SelectorKind.JsonLd)]
    public string? Name { get; set; }

    [HarvestField("sku", Kind = SelectorKind.JsonLd)]
    public string? Sku { get; set; }

    [HarvestField("description", Kind = SelectorKind.JsonLd)]
    public string? Description { get; set; }

    [HarvestField("brand.name", Kind = SelectorKind.JsonLd)]
    public string? Brand { get; set; }

    [HarvestField("offers.price", Kind = SelectorKind.JsonLd)]
    public string? Price { get; set; }

    [HarvestField("offers.priceCurrency", Kind = SelectorKind.JsonLd)]
    public string? Currency { get; set; }

    [HarvestField("offers.availability", Kind = SelectorKind.JsonLd)]
    public string? Availability { get; set; }

    [HarvestField("image", Kind = SelectorKind.JsonLd)]
    public string? Image { get; set; }

    [HarvestField("aggregateRating.ratingValue", Kind = SelectorKind.JsonLd)]
    public string? RatingValue { get; set; }

    [HarvestField("aggregateRating.reviewCount", Kind = SelectorKind.JsonLd)]
    public string? ReviewCount { get; set; }
}
