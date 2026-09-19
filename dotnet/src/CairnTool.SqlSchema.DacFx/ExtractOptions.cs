namespace CairnTool.SqlSchema.DacFx;

/// <summary>Controls what an extraction reads and what it stamps into the document.</summary>
public sealed record ExtractOptions {
    /// <summary>Name recorded as the producing tool. Defaults to this assembly's name.</summary>
    public string GeneratorName { get; init; } = "CairnTool.SqlSchema.DacFx";

    /// <summary>
    /// Version recorded as the producing tool's. Dropped by canonicalization, so a conformance
    /// golden does not have to be rewritten every release.
    /// </summary>
    public string GeneratorVersion { get; init; } = "0.0.0";

    /// <summary>Database name recorded in <c>source.name</c>. Defaults to the package's own.</summary>
    public string? DatabaseName { get; init; }

    /// <summary>Schemas to keep. Empty keeps every schema.</summary>
    public IReadOnlyCollection<string> IncludeSchemas { get; init; } = [];

    /// <summary>Schemas to drop, applied after <see cref="IncludeSchemas"/>.</summary>
    public IReadOnlyCollection<string> ExcludeSchemas { get; init; } = [];

    /// <summary>
    /// Include objects that originated in a referenced package. Off by default: the document's job
    /// is to describe <em>this</em> database, and an unmarked merged document makes it impossible
    /// to tell where a table actually lives. Included objects are attributed with
    /// <c>extensions.sqlserver.fromReference</c>.
    /// </summary>
    public bool IncludeReferenced { get; init; }

    /// <summary>Descriptions to merge over the extracted document, keyed by object address.</summary>
    public SqlAnnotations? Annotations { get; init; }
}
