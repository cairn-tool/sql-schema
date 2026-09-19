using System.Text.Json;

namespace CairnTool.SqlSchema.Tests;

public class SerializationTests {
    private static string Write(SqlSchemaDescription document) =>
        JsonSerializer.Serialize(document, SqlSchemaJsonContext.Default.SqlSchemaDescription);

    [Fact]
    public void RoundTripsThroughTheSourceGeneratedContext() {
        var original = Documents.Minimal();

        var json = Write(original);
        var restored = JsonSerializer.Deserialize(
            json,
            SqlSchemaJsonContext.Default.SqlSchemaDescription);

        restored.Should().BeEquivalentTo(original);
    }

    [Fact]
    public void WritesCamelCaseMemberNames() {
        Write(Documents.Minimal()).Should().Contain("\"schemaVersion\"").And.Contain("\"userDefinedTypes\"");
    }

    [Fact]
    public void OmitsAnOptionalMemberRatherThanWritingItAsNull() {
        // Optional means ABSENT. A consumer round-tripping a document must not turn a member that
        // does not apply into an explicit null -- `maxLength` on a bigint is not "unknown".
        var json = Write(Documents.Minimal());
        json.Should().NotContain("\"sourceFile\"");
        json.Should().NotContain("\"temporal\"");
    }

    [Fact]
    public void WritesARequiredNullableMemberAsNull() {
        // The converse, and the distinction the specification leans on: `description` is required
        // and nullable, so it is present and null rather than omitted.
        Write(Documents.Minimal()).Should().Contain("\"description\": null");
    }

    [Fact]
    public void RefusesADocumentMissingARequiredMember() {
        const string json = """{"schemaVersion":"1"}""";

        var act = () => JsonSerializer.Deserialize(
            json,
            SqlSchemaJsonContext.Default.SqlSchemaDescription);

        act.Should().Throw<JsonException>();
    }

    [Fact]
    public void CarriesAnExtensionsBagThroughVerbatim() {
        var original = Documents.Minimal();
        var table = original.Tables[0] with {
            Extensions = new Dictionary<string, JsonElement> {
                ["sqlserver"] = JsonDocument.Parse("""{"clustered":true}""").RootElement.Clone(),
            },
        };
        var document = original with { Tables = [table] };

        var json = Write(document);

        json.Should().Contain("\"sqlserver\"").And.Contain("\"clustered\"");
    }
}
