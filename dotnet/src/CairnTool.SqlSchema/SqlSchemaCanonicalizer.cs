using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CairnTool.SqlSchema;

/// <summary>
/// Puts a document into canonical form: keys in the order the specification declares them, and
/// <c>tool.version</c> removed.
/// </summary>
/// <remarks>
/// This is the C# twin of the npm package's <c>canonicalize</c>, and the two must agree byte for
/// byte -- a conformance golden is compared through it. They agree because neither hard-codes a
/// key-order table: both read the order out of the same JSON Schema document, which is the one
/// thing that cannot drift from the specification.
/// <para>
/// It lives in this package rather than in the extractor because the extractor is what writes a
/// document, and it cannot call into Node to do it.
/// </para>
/// </remarks>
public static class SqlSchemaCanonicalizer {
    private const string DefsPrefix = "#/$defs/";

    private static readonly JsonObject SchemaDocument =
        JsonNode.Parse(Schema.Json)?.AsObject()
        ?? throw new InvalidOperationException("The embedded schema is not a JSON object.");

    private static readonly JsonObject Defs = SchemaDocument["$defs"]?.AsObject() ?? [];

    private static readonly JsonSerializerOptions WriteOptions = new() {
        WriteIndented = true,
        // JavaScript's JSON.stringify escapes only what JSON requires. System.Text.Json's default
        // encoder additionally escapes characters that would be unsafe in an HTML context -- <, >,
        // &, + and every non-ASCII rune. A document canonicalized here has to be byte-identical to
        // one canonicalized by the npm package, so the relaxed encoder is required, not preferred.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static JsonObject? Resolve(JsonNode? node) {
        if (node is not JsonObject obj) return null;
        if (obj["$ref"] is JsonValue value
            && value.TryGetValue(out string? reference)
            && reference is not null
            && reference.StartsWith(DefsPrefix, StringComparison.Ordinal)) {
            return Resolve(Defs[reference[DefsPrefix.Length..]]);
        }
        return obj;
    }

    private static JsonNode? Order(JsonNode? value, JsonNode? schemaNode) {
        var node = Resolve(schemaNode);

        if (value is JsonArray array) {
            var items = node?["items"];
            var ordered = new JsonArray();
            foreach (var entry in array) {
                ordered.Add(Order(entry?.DeepClone(), items));
            }
            return ordered;
        }

        if (value is not JsonObject source) return value?.DeepClone();

        var properties = node?["properties"] as JsonObject;
        var result = new JsonObject();

        // Declared properties first, in schema order.
        if (properties is not null) {
            foreach (var property in properties) {
                if (source.TryGetPropertyValue(property.Key, out var child)) {
                    result[property.Key] = Order(child?.DeepClone(), property.Value);
                }
            }
        }

        // Anything undeclared keeps its place after them, name-sorted. The format requires
        // consumers to ignore what they do not recognize; dropping it here would make
        // canonicalization lossy, and leaving it unsorted would make it unstable. Ordinal, to match
        // JavaScript's Array.prototype.sort, which compares UTF-16 code units.
        var extra = source
            .Where(pair => properties is null || !properties.ContainsKey(pair.Key))
            .Select(pair => pair.Key)
            .OrderBy(key => key, StringComparer.Ordinal);
        foreach (var key in extra) {
            result[key] = source[key]?.DeepClone();
        }

        return result;
    }

    /// <summary>Returns the document with keys in spec order and <c>tool.version</c> removed.</summary>
    public static JsonNode? Canonicalize(JsonNode? document) {
        var ordered = Order(document?.DeepClone(), SchemaDocument);
        if (ordered is JsonObject obj && obj["tool"] is JsonObject tool) {
            tool.Remove("version");
        }
        return ordered;
    }

    /// <summary>
    /// The canonical serialization: two-space indent, <c>\n</c> newlines, trailing newline.
    /// </summary>
    public static string CanonicalizeJson(JsonNode? document) {
        var json = Canonicalize(document)?.ToJsonString(WriteOptions) ?? "null";
        // System.Text.Json writes Environment.NewLine-free output already, but normalize anyway:
        // a document written on Windows must compare equal to one written on Linux.
        return json.Replace("\r\n", "\n", StringComparison.Ordinal) + "\n";
    }

    /// <summary>Canonicalizes a document given as JSON text.</summary>
    public static string CanonicalizeJson(string json) => CanonicalizeJson(JsonNode.Parse(json));
}
