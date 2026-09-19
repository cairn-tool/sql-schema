using System.Text.Json;

namespace CairnTool.SqlSchema.Tool;

/// <summary>Reading and writing the documents the tool deals in.</summary>
internal static class Documents {
    public static string Serialize(SqlSchemaDescription document) =>
        JsonSerializer.Serialize(document, SqlSchemaJsonContext.Default.SqlSchemaDescription) + "\n";

    public static SqlAnnotations ReadAnnotations(string path) {
        var annotations = JsonSerializer.Deserialize(
            File.ReadAllText(path), SqlSchemaJsonContext.Default.SqlAnnotations);
        return annotations
            ?? throw new InvalidOperationException($"{path} is not an annotation document.");
    }

    /// <summary>
    /// True when the file already holds this document. Compared through canonicalization, so a
    /// difference in the producing tool's version is not reported as drift.
    /// </summary>
    public static bool Matches(string path, string candidate) {
        if (!File.Exists(path)) return false;
        try {
            return SqlSchemaCanonicalizer.CanonicalizeJson(File.ReadAllText(path))
                == SqlSchemaCanonicalizer.CanonicalizeJson(candidate);
        } catch (JsonException) {
            return false;
        }
    }
}
