using System.Text.Json;
using System.Text.Json.Nodes;

namespace CairnTool.SqlSchema.DacFx;

/// <summary>
/// Builds the per-engine <c>extensions</c> bag.
/// </summary>
/// <remarks>
/// Keys are inserted in sorted order deliberately. Canonicalization sorts the engine names, but it
/// does not descend into a bag — the bag is open by design and its contents are not the
/// specification's to order — so if the producer emitted them in an arbitrary order the document
/// would not be byte-reproducible, and the golden gate would be worthless.
/// </remarks>
internal sealed class ExtensionsBuilder {
    private readonly SortedDictionary<string, JsonNode?> values = new(StringComparer.Ordinal);

    public ExtensionsBuilder Set(string key, bool value) {
        if (value) values[key] = JsonValue.Create(value);
        return this;
    }

    public ExtensionsBuilder Set(string key, bool value, bool whenDefault) {
        if (value != whenDefault) values[key] = JsonValue.Create(value);
        return this;
    }

    public ExtensionsBuilder Set(string key, int value, int whenDefault) {
        if (value != whenDefault) values[key] = JsonValue.Create(value);
        return this;
    }

    public ExtensionsBuilder Set(string key, string? value) {
        if (!string.IsNullOrEmpty(value)) values[key] = JsonValue.Create(value);
        return this;
    }

    /// <summary>Returns the bag, or null when nothing was recorded. Never an empty object.</summary>
    public IReadOnlyDictionary<string, JsonElement>? Build(string engine = "sqlserver") {
        if (values.Count == 0) return null;
        var node = new JsonObject();
        foreach (var pair in values) node[pair.Key] = pair.Value;
        using var document = JsonDocument.Parse(node.ToJsonString());
        return new Dictionary<string, JsonElement> { [engine] = document.RootElement.Clone() };
    }
}
