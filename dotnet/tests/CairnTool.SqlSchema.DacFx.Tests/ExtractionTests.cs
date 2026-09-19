using System.Text.Json;

namespace CairnTool.SqlSchema.DacFx.Tests;

/// <summary>Behaviour of the extractor that the goldens do not make obvious on their own.</summary>
public class ExtractionTests {
    private static SqlSchemaDescription Extract(string name, ExtractOptions? options = null) =>
        DacpacExtractor.Extract(Repo.Dacpac(name), options ?? Repo.Options(name));

    // ---- what the model cannot report, and must therefore omit -------------------------------

    [Fact]
    public void OmitsTheDataTypeOfAComputedColumnRatherThanInventingOne() {
        var table = Extract("datatypes").Tables.Single();
        var computed = table.Columns.Single(c => c.Name == "Persisted");

        computed.Computed!.Expression.Should().Be("([Id] * 2)");
        computed.Computed.Persisted.Should().BeTrue();
        // The model records a computed column's expression and its dependencies, and never
        // resolves the resulting type. An invented type would be a silent wrong answer.
        computed.DataType.Should().BeNull();
    }

    [Fact]
    public void OmitsIndexSortDirectionBecauseTheModelDoesNotCarryIt() {
        var table = Extract("relational").Tables.Single(t => t.Id == "items.SalesItemDetail");
        var index = table.Indexes.Single(i => i.Name == "IX_items_SalesItemDetail_Effective");

        // Declared `([EffectiveDate] DESC)`, but a compiled model exposes no ordering data at all.
        index.Columns.Single().Descending.Should().BeNull();
    }

    [Fact]
    public void DoesNotFabricateALengthForAScalarFunctionReturn() {
        var function = Extract("programmability").Routines.Single(r => r.Kind == "scalarFunction");

        // Declared NVARCHAR(80); the model hands back only the base type object.
        function.Returns!.DataType.Rendered.Should().Be("nvarchar");
    }

    // ---- the parameterization table ----------------------------------------------------------

    [Theory]
    [InlineData("Id", "int")]                       // no facets, despite the model reporting them
    [InlineData("Datetime2Default", "datetime2(7)")] // effective, not authored
    [InlineData("Decimal15", "decimal(15,0)")]       // ditto
    [InlineData("VarcharMax", "varchar(max)")]
    [InlineData("Collated", "varchar(20)")]          // collation is not part of `rendered`
    [InlineData("Account", "dt.AccountNumber")]      // a user-defined type renders as its id
    [InlineData("VariantCol", "sql_variant")]
    public void RendersDataTypesPerTheSpecification(string column, string expected) {
        var table = Extract("datatypes").Tables.Single();

        table.Columns.Single(c => c.Name == column).DataType!.Rendered.Should().Be(expected);
    }

    // ---- derived facts stay derived ----------------------------------------------------------

    [Fact]
    public void RecordsAPeriodOnceOnTheTableAndMarksTheHistoryTableNowhere() {
        var document = Extract("temporal");
        var table = document.Tables.Single(t => t.Id == "cfg.StoreConfiguration");
        var history = document.Tables.Single(t => t.Id == "cfg.StoreConfigurationHistory");

        table.Temporal!.PeriodStartColumn.Should().Be("ValidFrom");
        table.Temporal.PeriodEndColumn.Should().Be("ValidTo");
        table.Temporal.HistoryTable.Should().Be("cfg.StoreConfigurationHistory");

        // The history table carries no marker of its own: it is one because the other table says so.
        history.Temporal.Should().BeNull();
        history.Columns.Should().NotBeEmpty();
    }

    [Fact]
    public void ModelsEveryDefaultAsANamedConstraintRatherThanAColumnMember() {
        var table = Extract("temporal").Tables.Single(t => t.Id == "cfg.StoreConfiguration");

        var defaults = table.Constraints.Where(c => c.Kind == "default").ToList();
        defaults.Should().HaveCount(6);
        defaults.Should().OnlyContain(c => c.Name!.StartsWith("DF_"));
        defaults.Single(c => c.Columns.Contains("StoreStatusFlags")).Expression.Should().Be("((0))");
    }

    [Fact]
    public void ReadsForeignKeysWithBarePositionalColumnsAndBothReferentialActions() {
        var document = Extract("relational");
        var detail = document.Tables.Single(t => t.Id == "items.SalesItemDetail");
        var tag = document.Tables.Single(t => t.Id == "items.SalesItemTag");

        var toItem = detail.Constraints.Single(c => c.Name == "FK_items_SalesItemDetail_SalesItem");
        toItem.References!.Table.Should().Be("items.SalesItem");
        toItem.References.Columns.Should().Equal("SalesItemId");
        toItem.Columns.Should().Equal("SalesItemId");
        toItem.OnDelete.Should().Be("noAction");

        tag.Constraints.Single(c => c.Kind == "foreignKey").OnDelete.Should().Be("cascade");
    }

    // ---- ordering ----------------------------------------------------------------------------

    [Fact]
    public void SortsTopLevelCollectionsByIdAndConstraintsByName() {
        var document = Extract("relational");

        document.Tables.Select(t => t.Id).Should().BeInAscendingOrder(StringComparer.Ordinal);
        foreach (var table in document.Tables) {
            table.Constraints.Select(c => c.Name).Should().BeInAscendingOrder(StringComparer.Ordinal);
        }
    }

    [Fact]
    public void KeepsColumnsInDeclaredOrderBecauseTheirOrderIsSemantic() {
        var table = Extract("minimal").Tables.Single();

        table.Columns.Select(c => c.Name).Should().Equal("SettingId", "Name");
    }

    // ---- extensions --------------------------------------------------------------------------

    [Fact]
    public void PutsClusteredInTheEngineBagRatherThanTheCore() {
        var table = Extract("minimal").Tables.Single();
        var key = table.Constraints.Single(c => c.Kind == "primaryKey");

        key.Extensions.Should().ContainKey("sqlserver");
        key.Extensions!["sqlserver"].GetProperty("clustered").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public void OmitsAnEmptyExtensionsBagRatherThanWritingAnEmptyObject() {
        var table = Extract("minimal").Tables.Single();

        table.Columns.Should().OnlyContain(c => c.Extensions == null);
    }

    // ---- composite models --------------------------------------------------------------------

    [Fact]
    public void ExcludesObjectsFromAReferencedPackageByDefault() {
        var document = Extract("composite");

        document.Tables.Select(t => t.Id).Should().Equal("local.Branch");
    }

    [Fact]
    public void IncludesAndAttributesThemWhenAsked() {
        var document = Extract("composite", Repo.Options("composite") with { IncludeReferenced = true });

        document.Tables.Select(t => t.Id).Should().Contain("shared.Region");
        var referenced = document.Tables.Single(t => t.Id == "shared.Region");
        referenced.Extensions!["sqlserver"].GetProperty("fromReference").GetString().Should().Be("shared");

        // An object of this package's own is not attributed.
        document.Tables.Single(t => t.Id == "local.Branch")
            .Extensions?.ContainsKey("sqlserver").Should().NotBe(true);
    }

    // ---- schema filtering ----------------------------------------------------------------------

    [Fact]
    public void AppliesIncludeAndExcludeSchemaFilters() {
        var only = Extract("relational", Repo.Options("relational") with { IncludeSchemas = ["items"] });
        only.Tables.Should().NotBeEmpty();

        var none = Extract("relational", Repo.Options("relational") with { ExcludeSchemas = ["items"] });
        none.Tables.Should().BeEmpty();
        none.Schemas.Should().BeEmpty();
    }

    // ---- annotations -------------------------------------------------------------------------

    [Fact]
    public void MergesDescriptionsFromTheSidecar() {
        var document = Extract("annotated");
        var table = document.Tables.Single();

        document.Schemas.Single().Description.Should()
            .Be("Configuration owned by the platform rather than by a store.");
        table.Description.Should().Be("Per-store status, one row per store.");
        table.Columns.Single(c => c.Name == "StoreStatusFlags").Description.Should()
            .Be("Bit flags: 1 ingesting, 2 forecasting, 4 scheduling.");
        table.Columns.Single(c => c.Name == "StoreStatusId").Description.Should().BeNull();
    }

    [Fact]
    public void RejectsAnAnnotationThatNamesSomethingTheDocumentDoesNotContain() {
        var stale = new SqlAnnotations {
            SchemaVersion = "1",
            Descriptions = new Dictionary<string, string> {
                ["cfg.StoreStatus"] = "fine",
                ["cfg.StoreStatus.DroppedLastQuarter"] = "stale",
            },
        };

        var act = () => Extract("annotated", Repo.Options("annotated") with { Annotations = stale });

        // A stale annotation naming a dropped column is exactly the failure the sidecar exists to
        // prevent, so it is an error rather than a silent no-op.
        act.Should().Throw<AnnotationMismatchException>()
            .Which.UnmatchedKeys.Should().Equal("cfg.StoreStatus.DroppedLastQuarter");
    }

    [Fact]
    public void LeavesEveryDescriptionNullWithNoSidecar() {
        var document = Extract("annotated", Repo.Options("annotated") with { Annotations = null });

        document.Tables.Single().Description.Should().BeNull();
        document.Tables.Single().Columns.Should().OnlyContain(c => c.Description == null);
    }

    // ---- the document validates against its own schema ---------------------------------------

    [Fact]
    public void ProducesDocumentsThatDeserializeBackThroughTheModel() {
        foreach (var name in Repo.Cases) {
            var json = JsonSerializer.Serialize(
                Extract(name), SqlSchemaJsonContext.Default.SqlSchemaDescription);
            var restored = JsonSerializer.Deserialize(
                json, SqlSchemaJsonContext.Default.SqlSchemaDescription);
            restored.Should().NotBeNull($"{name} must round-trip through the published model");
        }
    }

    [Fact]
    public void CanonicalFormIsNotItselfRoundTrippable() {
        // Worth pinning, because it is surprising and it is deliberate. Canonicalization removes
        // tool.version so that a golden survives a release, and `version` is required on ToolInfo
        // -- so the canonical form is a comparison format, not a wire format. A consumer that
        // needs to read a document back must keep the serialized one.
        var canonical = Repo.Canonical(Extract("minimal"));

        var act = () => JsonSerializer.Deserialize(
            canonical, SqlSchemaJsonContext.Default.SqlSchemaDescription);

        act.Should().Throw<JsonException>().WithMessage("*version*");
    }
}
