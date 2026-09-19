using System.Text.Json;

namespace CairnTool.SqlSchema.Tool.Tests;

public class ToolTests : IDisposable {
    private readonly string scratch =
        Directory.CreateTempSubdirectory("sqlschema-tool-tests").FullName;

    public void Dispose() => Directory.Delete(scratch, recursive: true);

    private string Path(string name) => System.IO.Path.Combine(scratch, name);

    // ---- extract -------------------------------------------------------------------------------

    [Fact]
    public void ExtractWritesADocumentToStdoutByDefault() {
        var run = Cli.Invoke("extract", Cli.Dacpac("minimal"));

        run.ExitCode.Should().Be(0);
        run.Out.Should().StartWith("{").And.Contain("\"schemaVersion\"");
        using var document = JsonDocument.Parse(run.Out);
        document.RootElement.GetProperty("tool").GetProperty("name").GetString().Should().Be("sqlschema");
    }

    [Fact]
    public void ExtractWritesToTheRequestedFileAndCreatesItsDirectory() {
        var target = Path("nested/deeper/sql-schema.json");

        var run = Cli.Invoke("extract", Cli.Dacpac("minimal"), "--output", target);

        run.ExitCode.Should().Be(0);
        File.Exists(target).Should().BeTrue();
        run.Out.Should().BeEmpty("the document went to the file, not to stdout");
    }

    [Fact]
    public void ExtractFailsWithACleanMessageWhenThePackageIsMissing() {
        var run = Cli.Invoke("extract", Path("absent.dacpac"));

        run.ExitCode.Should().Be(1);
        run.Error.Should().Contain("No such file");
        // A missing input is a user error, not a crash: no stack trace reaches the terminal.
        run.Error.Should().NotContain("   at ");
    }

    [Fact]
    public void ExtractRejectsAnUnknownFormatRatherThanSubstitutingOne() {
        var run = Cli.Invoke("extract", Cli.Dacpac("minimal"), "--format", "yaml");

        run.ExitCode.Should().Be(1);
        run.Error.Should().Contain("Unknown output format 'yaml'");
    }

    // ---- --check -------------------------------------------------------------------------------

    [Fact]
    public void CheckSucceedsWhenTheFileIsCurrent() {
        var target = Path("sql-schema.json");
        Cli.Invoke("extract", Cli.Dacpac("minimal"), "-o", target).ExitCode.Should().Be(0);

        var run = Cli.Invoke("extract", Cli.Dacpac("minimal"), "-o", target, "--check");

        run.ExitCode.Should().Be(0);
    }

    [Fact]
    public void CheckReportsDriftWithItsOwnExitCodeAndWritesNothing() {
        var target = Path("sql-schema.json");
        File.WriteAllText(target, "{\"schemaVersion\":\"1\"}");

        var run = Cli.Invoke("extract", Cli.Dacpac("minimal"), "-o", target, "--check");

        // 2, not 1: drift is a distinct outcome from the command failing.
        run.ExitCode.Should().Be(2);
        run.Error.Should().Contain("out of date");
        File.ReadAllText(target).Should().Be("{\"schemaVersion\":\"1\"}", "--check must not write");
    }

    [Fact]
    public void CheckIgnoresADifferenceInTheProducingToolsVersion() {
        // The comparison is canonical, so a release of this tool does not make every committed
        // document report drift.
        var target = Path("sql-schema.json");
        Cli.Invoke("extract", Cli.Dacpac("minimal"), "-o", target);
        var document = File.ReadAllText(target).Replace("\"version\": \"1.0.0\"", "\"version\": \"9.9.9\"");
        File.WriteAllText(target, document);

        Cli.Invoke("extract", Cli.Dacpac("minimal"), "-o", target, "--check").ExitCode.Should().Be(0);
    }

    [Fact]
    public void CheckWithoutAnOutputIsAUserError() {
        var run = Cli.Invoke("extract", Cli.Dacpac("minimal"), "--check");

        run.ExitCode.Should().Be(1);
        run.Error.Should().Contain("--check needs --output");
    }

    // ---- options pass through to the extractor -------------------------------------------------

    [Fact]
    public void IncludeReferencedReachesTheExtractor() {
        var without = Cli.Invoke("extract", Cli.Dacpac("composite"));
        var with = Cli.Invoke("extract", Cli.Dacpac("composite"), "--include-referenced");

        without.Out.Should().NotContain("shared.Region");
        with.Out.Should().Contain("shared.Region").And.Contain("fromReference");
    }

    [Fact]
    public void SchemaFiltersReachTheExtractor() {
        var run = Cli.Invoke("extract", Cli.Dacpac("relational"), "--exclude-schema", "items");

        using var document = JsonDocument.Parse(run.Out);
        document.RootElement.GetProperty("tables").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public void AnnotationsReachTheExtractorAndAStaleOneIsReported() {
        var good = System.IO.Path.Combine(
            Cli.Root, "spec", "fixtures", "annotated", "annotations.json");
        var run = Cli.Invoke("extract", Cli.Dacpac("annotated"), "--annotations", good);
        run.ExitCode.Should().Be(0);
        run.Out.Should().Contain("Bit flags");

        var stale = Path("stale.json");
        File.WriteAllText(stale,
            "{\"schemaVersion\":\"1\",\"descriptions\":{\"cfg.Nope\":\"gone\"}}");

        var bad = Cli.Invoke("extract", Cli.Dacpac("annotated"), "--annotations", stale);
        bad.ExitCode.Should().Be(1);
        bad.Error.Should().Contain("cfg.Nope");
    }

    // ---- validate ------------------------------------------------------------------------------

    [Fact]
    public void ValidateAcceptsADocumentTheToolItselfProduced() {
        var target = Path("sql-schema.json");
        var extract = Cli.Invoke("extract", Cli.Dacpac("programmability"), "-o", target);
        extract.ExitCode.Should().Be(0, extract.Error);

        var run = Cli.Invoke("validate", target);

        run.ExitCode.Should().Be(0, $"stdout=<{run.Out}> stderr=<{run.Error}> exists={File.Exists(target)}");
        run.Out.Should().Contain("is a valid sql-schema document");
    }

    [Fact]
    public void ValidateReportsWhatIsWrongAndExitsTwo() {
        var target = Path("bad.json");
        File.WriteAllText(target, "{\"schemaVersion\":\"1\"}");

        var run = Cli.Invoke("validate", target);

        run.ExitCode.Should().Be(2);
        run.Out.Should().Contain("is not a valid sql-schema document");
        run.Out.Should().Contain("tables");
    }

    [Fact]
    public void ValidateRejectsSomethingThatIsNotJsonAtAll() {
        var target = Path("not.json");
        File.WriteAllText(target, "this is not json");

        var run = Cli.Invoke("validate", target);

        run.ExitCode.Should().Be(1);
        run.Error.Should().Contain("is not JSON");
    }

    // ---- describe ------------------------------------------------------------------------------

    [Fact]
    public void DescribeEmitsAContractForEveryCommand() {
        var run = Cli.Invoke("describe", "--format", "json");

        run.ExitCode.Should().Be(0);
        using var document = JsonDocument.Parse(run.Out);
        var ids = document.RootElement.GetProperty("commands")
            .EnumerateArray().Select(c => c.GetProperty("id").GetString()).ToList();
        ids.Should().Contain(["extract", "validate", "describe"]);
    }

    [Fact]
    public void DescribeMatchesItsGolden() {
        var run = Cli.Invoke("describe", "--format", "json");
        run.ExitCode.Should().Be(0);

        // Kept apart from spec/conformance, which holds sql-schema documents. This one is a
        // cli-schema document, and `npm run conformance` validates it against that repository's
        // schema resolved from the installed package rather than a vendored copy -- so a major
        // release of cli-schema that changes this payload is caught here.
        var directory = System.IO.Path.Combine(Cli.Root, "artifacts", "cli");
        Directory.CreateDirectory(directory);
        File.WriteAllText(System.IO.Path.Combine(directory, "describe.json"), run.Out);

        var golden = System.IO.Path.Combine(Cli.Root, "spec", "cli", "describe.json");
        File.Exists(golden).Should().BeTrue(
            "spec/cli/describe.json is missing. Run `npm run goldens` to write it.");

        // The tool's own version is in the payload and changes every release, so it is normalized
        // out -- the same reason canonicalization drops tool.version from a sql-schema document.
        static string WithoutVersion(string json) =>
            System.Text.RegularExpressions.Regex.Replace(
                json, "\"version\": \"[^\"]*\"", "\"version\": \"0.0.0\"");

        WithoutVersion(run.Out).Should().Be(WithoutVersion(File.ReadAllText(golden)));
    }

    [Fact]
    public void EveryCommandDeclaresAContractRatherThanBeingUndeclared() {
        // A command with no registry entry is reported as `undeclared` rather than rejected, which
        // is the right behaviour and also the way a new command quietly ships undocumented.
        var run = Cli.Invoke("describe", "--format", "json");

        using var document = JsonDocument.Parse(run.Out);
        foreach (var command in document.RootElement.GetProperty("commands").EnumerateArray()) {
            command.GetProperty("stability").GetString()
                .Should().NotBe("undeclared",
                    $"'{command.GetProperty("id").GetString()}' has no entry in Contracts");
        }
    }
}
