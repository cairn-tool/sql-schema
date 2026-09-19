using CairnTool.CliSchema;

namespace CairnTool.SqlSchema.Tool;

/// <summary>
/// What this tool declares about itself: the facts a CLI framework cannot know.
/// </summary>
/// <remarks>
/// A command with no entry here is reported as <c>undeclared</c> rather than rejected, so a
/// missing row is a visible gap and not a crash. The conformance golden for
/// <c>describe --format json</c> is what stops a command being added without one.
/// </remarks>
internal static class Contracts {
    private static readonly ExitCodeMeaning[] Standard = [
        new(0, "Success."),
        new(1, "The operation failed."),
        new(2, "The document is invalid, or --check found a difference."),
    ];

    public static ContractRegistry Build() =>
        new ContractRegistry()
            .Add("extract", new CommandContract(
                Formats: ["json"],
                DefaultFormat: "json",
                FormatConfigurable: false,
                OutputSchema: Schema.Id,
                ExitCodes: Standard,
                Stream: new CommandStream("stdout"),
                Writes: true,
                Stability: "stable"))
            .Add("validate", new CommandContract(
                Formats: ["json", "text"],
                DefaultFormat: "text",
                FormatConfigurable: false,
                OutputSchema: null,
                ExitCodes: Standard,
                Stream: new CommandStream("stdout") { Findings = "stdout" },
                Writes: false,
                Stability: "stable"))
            .Add("describe", new CommandContract(
                Formats: ["llm", "human", "json"],
                DefaultFormat: "llm",
                FormatConfigurable: false,
                OutputSchema: "https://github.com/cairn-tool/cli-schema/v1/cli-schema.json",
                ExitCodes: Standard,
                Stream: new CommandStream("stdout"),
                Writes: false,
                Stability: "stable"));
}
