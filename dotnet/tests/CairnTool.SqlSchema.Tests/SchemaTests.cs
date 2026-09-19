using System.Text.Json;

namespace CairnTool.SqlSchema.Tests;

public class SchemaTests {
    [Fact]
    public void VersionIsHandOwnedAndIndependentOfThePackageVersion() {
        Schema.Version.Should().Be("1");
    }

    [Fact]
    public void IdIsTheIdentifierTheSpecificationDeclares() {
        Schema.Id.Should().Be("https://github.com/cairn-tool/sql-schema/v1/sql-schema.json");
    }

    [Fact]
    public void EmbeddedSchemaIsPresentAndParses() {
        using var document = JsonDocument.Parse(Schema.Json);
        document.RootElement.GetProperty("$id").GetString().Should().Be(Schema.Id);
    }

    [Fact]
    public void EmbeddedSchemaAgreesWithTheVersionConstant() {
        using var document = JsonDocument.Parse(Schema.Json);
        var schemaVersion = document.RootElement
            .GetProperty("properties")
            .GetProperty("schemaVersion");
        schemaVersion.GetProperty("type").GetString().Should().Be("string");
    }

    [Fact]
    public void NoDefinitionClosesItselfToUnknownProperties() {
        // Consumers must ignore what they do not recognize, which is what keeps adding a property
        // non-breaking. One `additionalProperties: false` anywhere would quietly end that.
        Schema.Json.Replace(" ", "").Should().NotContain("\"additionalProperties\":false");
    }
}
