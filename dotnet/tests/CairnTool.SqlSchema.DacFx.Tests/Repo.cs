using System.Text.Json;

namespace CairnTool.SqlSchema.DacFx.Tests;

/// <summary>Locates the repository from the test binary, and reads and writes its fixtures.</summary>
internal static class Repo {
    /// <summary>Every conformance case. The fixture dacpac and the golden share this name.</summary>
    public static readonly string[] Cases = [
        "annotated",
        "composite",
        "datatypes",
        "legacy",
        "minimal",
        "programmability",
        "relational",
        "temporal",
    ];

    public static string Root { get; } = Find();

    private static string Find() {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null) {
            if (File.Exists(Path.Combine(directory.FullName, "spec", "v1", "sql-schema.json"))) {
                return directory.FullName;
            }
            directory = directory.Parent;
        }
        throw new InvalidOperationException("Could not find the repository root from the test binary.");
    }

    public static string Dacpac(string name) {
        var path = Path.Combine(Root, "artifacts", "fixtures", name, $"{name}.dacpac");
        if (!File.Exists(path)) {
            throw new FileNotFoundException(
                $"The fixture dacpac for '{name}' has not been built. Run `npm run fixtures`.", path);
        }
        return path;
    }

    public static string GoldenPath(string name) =>
        Path.Combine(Root, "spec", "conformance", name, "expected.json");

    public static string? Golden(string name) {
        var path = GoldenPath(name);
        return File.Exists(path) ? File.ReadAllText(path) : null;
    }

    /// <summary>
    /// Drops the document where the cross-language conformance runner reads it. The npm test suite
    /// reconstructs what lands here, and `npm run conformance` diffs the two.
    /// </summary>
    public static void WriteArtifact(string name, string json) {
        var directory = Environment.GetEnvironmentVariable("SQL_SCHEMA_ARTIFACTS")
            ?? Path.Combine(Root, "artifacts", "dotnet");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, $"{name}.json"), json);
    }

    /// <summary>The document as written: a real, complete, schema-valid document.</summary>
    public static string Serialize(SqlSchemaDescription document) =>
        JsonSerializer.Serialize(document, SqlSchemaJsonContext.Default.SqlSchemaDescription);

    /// <summary>
    /// The comparison form. Canonicalization removes tool.version, so this is what a golden holds
    /// and what two documents are diffed through — but it is not a document a consumer can read
    /// back, which is why the artifact written for the other language is the serialized form.
    /// </summary>
    public static string Canonical(SqlSchemaDescription document) =>
        SqlSchemaCanonicalizer.CanonicalizeJson(Serialize(document));

    public static ExtractOptions Options(string name) => new() {
        GeneratorName = "sqlschema",
        GeneratorVersion = "0.0.0",
        Annotations = LoadAnnotations(name),
    };

    private static SqlAnnotations? LoadAnnotations(string name) {
        var path = Path.Combine(Root, "spec", "fixtures", name, "annotations.json");
        if (!File.Exists(path)) return null;
        return JsonSerializer.Deserialize(
            File.ReadAllText(path), SqlSchemaJsonContext.Default.SqlAnnotations);
    }
}
