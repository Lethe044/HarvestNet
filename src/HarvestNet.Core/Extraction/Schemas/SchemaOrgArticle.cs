namespace HarvestNet.Core.Extraction.Schemas;

/// <summary>
/// A ready-made mapping of the schema.org Article/NewsArticle/BlogPosting fields most
/// commonly published in a page's JSON-LD, for scraping news sites and blogs that publish
/// structured data without needing any HTML selectors.
/// </summary>
public sealed class SchemaOrgArticle
{
    [HarvestField("headline", Kind = SelectorKind.JsonLd)]
    public string? Headline { get; set; }

    [HarvestField("author.name", Kind = SelectorKind.JsonLd)]
    public string? Author { get; set; }

    [HarvestField("datePublished", Kind = SelectorKind.JsonLd)]
    public string? DatePublished { get; set; }

    [HarvestField("dateModified", Kind = SelectorKind.JsonLd)]
    public string? DateModified { get; set; }

    [HarvestField("description", Kind = SelectorKind.JsonLd)]
    public string? Description { get; set; }

    [HarvestField("publisher.name", Kind = SelectorKind.JsonLd)]
    public string? Publisher { get; set; }
}
