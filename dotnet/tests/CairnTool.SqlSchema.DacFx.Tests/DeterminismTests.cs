namespace CairnTool.SqlSchema.DacFx.Tests;

/// <summary>
/// An extraction must be byte-reproducible.
/// </summary>
/// <remarks>
/// This is not a nicety. If an unordered enumeration leaks the model's internal ordering into the
/// document then every consumer's committed file churns on every build, a diff stops being
/// readable, and the golden gate is worthless — it would fail for reasons that have nothing to do
/// with the schema.
/// </remarks>
public class DeterminismTests {
    public static TheoryData<string> Cases() {
        var data = new TheoryData<string>();
        foreach (var name in Repo.Cases) data.Add(name);
        return data;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void ExtractingTwiceProducesTheSameBytes(string name) {
        var first = Repo.Canonical(DacpacExtractor.Extract(Repo.Dacpac(name), Repo.Options(name)));
        var second = Repo.Canonical(DacpacExtractor.Extract(Repo.Dacpac(name), Repo.Options(name)));

        second.Should().Be(first);
    }

    [Fact]
    public void CarriesNoTimestampOrOtherRunVaryingValue() {
        var json = Repo.Canonical(DacpacExtractor.Extract(Repo.Dacpac("temporal"), Repo.Options("temporal")));

        // A dacpac's own Origin.xml holds a build timestamp and checksums. None of it may reach
        // the document, or two builds of the same sources would disagree.
        json.Should().NotContain("generatedAt");
        json.Should().NotContain(DateTime.UtcNow.Year.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }
}
