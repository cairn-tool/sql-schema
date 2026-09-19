using System.Text.Json;
using System.Text.Json.Nodes;

namespace CairnTool.SqlSchema.Tests;

public class CanonicalizerTests {
    private static JsonNode Parse(string json) =>
        JsonNode.Parse(json) ?? throw new InvalidOperationException("not JSON");

    private static JsonNode Document() => Parse(
        JsonSerializer.Serialize(
            Documents.Minimal(),
            SqlSchemaJsonContext.Default.SqlSchemaDescription));

    [Fact]
    public void DropsToolVersionBecauseBinariesReportTheirOwn() {
        var result = SqlSchemaCanonicalizer.Canonicalize(Document())!.AsObject();

        result["tool"]!.AsObject().ContainsKey("version").Should().BeFalse();
        result["tool"]!["name"]!.GetValue<string>().Should().Be("sql-schema-tests");
    }

    [Fact]
    public void OrdersKeysAsTheSpecificationDeclaresThem() {
        var scrambled = Parse("""{"triggers":[],"schemaVersion":"1","tool":{"name":"t"}}""");

        var result = SqlSchemaCanonicalizer.Canonicalize(scrambled)!.AsObject();

        result.Select(pair => pair.Key).Should().Equal("schemaVersion", "tool", "triggers");
    }

    [Fact]
    public void OrdersNestedObjectsTooAtEveryDepth() {
        var scrambled = Parse(
            """
            {
              "tables": [
                {
                  "columns": [
                    {
                      "nullable": true,
                      "name": "C",
                      "description": null,
                      "dataType": { "rendered": "int", "name": "int", "userDefined": false }
                    }
                  ],
                  "path": ["s", "T"],
                  "id": "s.T",
                  "description": null,
                  "constraints": [],
                  "indexes": []
                }
              ]
            }
            """);

        var table = SqlSchemaCanonicalizer.Canonicalize(scrambled)!["tables"]![0]!.AsObject();

        table.Select(pair => pair.Key).Should()
            .Equal("id", "path", "description", "columns", "constraints", "indexes");
        table["columns"]![0]!.AsObject().Select(pair => pair.Key).Should()
            .Equal("name", "description", "dataType", "nullable");
    }

    [Fact]
    public void KeepsAnUnrecognizedPropertySortedAfterTheDeclaredOnes() {
        var withExtra = Parse("""{"zebra":1,"schemaVersion":"1","alpha":2}""");

        var result = SqlSchemaCanonicalizer.Canonicalize(withExtra)!.AsObject();

        result.Select(pair => pair.Key).Should().Equal("schemaVersion", "alpha", "zebra");
    }

    [Fact]
    public void IsStableWhenEveryObjectArrivesWithItsKeysReversed() {
        var document = Document();
        var reversed = Reverse(document);

        SqlSchemaCanonicalizer.CanonicalizeJson(reversed)
            .Should().Be(SqlSchemaCanonicalizer.CanonicalizeJson(document));
    }

    [Fact]
    public void SerializesWithTwoSpaceIndentAndATrailingNewline() {
        var json = SqlSchemaCanonicalizer.CanonicalizeJson(Document());

        json.Should().EndWith("\n");
        json.Should().Contain("\n  \"tool\": {");
        json.Should().NotContain("\r\n");
    }

    [Fact]
    public void DoesNotEscapeCharactersThatJavaScriptLeavesAlone() {
        // The default System.Text.Json encoder escapes <, >, &, + and every non-ASCII rune. The
        // npm package's canonicalizer does not, and the two outputs are compared byte for byte.
        var document = Parse("""{"schemaVersion":"1 < 2 & 3 > 0 + é"}""");

        SqlSchemaCanonicalizer.CanonicalizeJson(document)
            .Should().Contain("1 < 2 & 3 > 0 + é");
    }

    private static JsonNode? Reverse(JsonNode? node) {
        if (node is JsonObject obj) {
            var reversed = new JsonObject();
            foreach (var pair in obj.Reverse()) {
                reversed[pair.Key] = Reverse(pair.Value?.DeepClone());
            }
            return reversed;
        }

        if (node is JsonArray array) {
            var copy = new JsonArray();
            foreach (var entry in array) {
                copy.Add(Reverse(entry?.DeepClone()));
            }
            return copy;
        }

        return node?.DeepClone();
    }
}
