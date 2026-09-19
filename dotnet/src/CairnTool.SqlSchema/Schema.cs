namespace CairnTool.SqlSchema;

/// <summary>
/// Hand-owned schema version and the JSON Schema 2020-12 document.
/// Independent of the NuGet package version.
/// </summary>
public static class Schema {
    public const string Version = "1";
    public const string Id = "https://github.com/cairn-tool/sql-schema/v1/sql-schema.json";

    private const string ResourceName = "CairnTool.SqlSchema.sql-schema.json";

    public static string Json {
        get {
            var assembly = typeof(Schema).Assembly;
            using var stream = assembly.GetManifestResourceStream(ResourceName)
                ?? throw new InvalidOperationException($"Embedded {ResourceName} is missing.");
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
    }
}
