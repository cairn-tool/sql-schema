namespace CairnTool.SqlSchema.DacFx.Tests;

/// <summary>
/// Extracts every fixture, drops it for the cross-language runner, and asserts it still matches
/// its golden.
/// </summary>
/// <remarks>
/// <b>The gate is that a golden still matches with no expected.json edited.</b> A golden that needs
/// changing means the document format moved, which is a bug in the change rather than an intended
/// outcome — <c>git diff spec/conformance/</c> must come back empty.
/// </remarks>
public class ConformanceTests {
    public static TheoryData<string> Cases() {
        var data = new TheoryData<string>();
        foreach (var name in Repo.Cases) data.Add(name);
        return data;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void MatchesItsGolden(string name) {
        var document = DacpacExtractor.Extract(Repo.Dacpac(name), Repo.Options(name));

        // The artifact is the document as written -- complete and schema-valid -- because the
        // other language validates it before reconstructing it. The golden is the canonical form,
        // which has tool.version removed and therefore does not validate.
        Repo.WriteArtifact(name, Repo.Serialize(document));

        var golden = Repo.Golden(name);
        golden.Should().NotBeNull(
            $"spec/conformance/{name}/expected.json is missing. Run `npm run goldens` to write it, "
            + "then review the diff before committing it.");

        // Both sides go through canonicalization, which drops tool.version. A golden is therefore
        // a complete, schema-valid document that survives a release of the producer that wrote it.
        SqlSchemaCanonicalizer.CanonicalizeJson(golden!).Should().Be(Repo.Canonical(document));
    }
}
