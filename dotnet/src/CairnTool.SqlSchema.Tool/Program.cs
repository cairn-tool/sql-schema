using System.CommandLine;
using System.Text.Json;
using CairnTool.CliSchema.SystemCommandLine;
using CairnTool.SqlSchema.DacFx;
using Json.Schema;

namespace CairnTool.SqlSchema.Tool;

public static class Program {
    internal const int Ok = 0;
    internal const int Failed = 1;
    internal const int Invalid = 2;

    /// <summary>
    /// Compiled once. <c>JsonSchema.FromText</c> registers the document by its <c>$id</c> in a
    /// process-wide registry, so calling it a second time throws "Overwriting registered schemas
    /// is not permitted." A one-shot process never notices; anything that validates twice does.
    /// </summary>
    private static readonly Lazy<JsonSchema> ValidationSchema =
        new(() => JsonSchema.FromText(Schema.Json));

    public static int Main(string[] args) => BuildRootCommand().Parse(args).Invoke();

    internal static RootCommand BuildRootCommand() {
        var root = new RootCommand("Extract a portable schema document from a SQL Server .dacpac");
        root.Subcommands.Add(BuildExtractCommand());
        root.Subcommands.Add(BuildValidateCommand());
        root.AddDescribeCommand(Contracts.Build(), new DescribeCommandOptions {
            ToolName = "sqlschema",
            ToolVersion = ToolVersion(),
        });
        return root;
    }

    private static string ToolVersion() =>
        typeof(Program).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";

    // ---- extract -------------------------------------------------------------------------------

    private static Command BuildExtractCommand() {
        var command = new Command("extract", "Read a .dacpac and write a sql-schema document");

        var dacpac = new Argument<string>("dacpac") {
            Description = "Path to the .dacpac to read",
        };
        var output = new Option<string?>("--output", "-o") {
            Description = "Where to write the document. Omitted, it goes to stdout",
        };
        var annotations = new Option<string?>("--annotations") {
            Description = "An annotation document whose descriptions are merged into the result",
        };
        var includeSchemas = new Option<string[]>("--schema") {
            Description = "Only this schema. Repeatable",
            AllowMultipleArgumentsPerToken = true,
        };
        var excludeSchemas = new Option<string[]>("--exclude-schema") {
            Description = "Drop this schema. Repeatable",
            AllowMultipleArgumentsPerToken = true,
        };
        var includeReferenced = new Option<bool>("--include-referenced") {
            Description = "Include objects from referenced packages, each marked with its origin",
        };
        var databaseName = new Option<string?>("--database-name") {
            Description = "Name recorded in source.name. Defaults to the package's own",
        };
        var check = new Option<bool>("--check") {
            Description = "Write nothing; exit 2 if --output differs from what would be written",
        };
        var format = new Option<string>("--format") {
            Description = "Output format: json",
            DefaultValueFactory = _ => "json",
        };
        format.HelpName = "fmt";

        command.Arguments.Add(dacpac);
        foreach (var option in new Option[] {
            output, annotations, includeSchemas, excludeSchemas, includeReferenced,
            databaseName, check, format,
        }) {
            command.Options.Add(option);
        }

        command.SetAction(parse => {
            var write = parse.InvocationConfiguration.Output;
            var error = parse.InvocationConfiguration.Error;

            var chosenFormat = parse.GetValue(format) ?? "json";
            if (chosenFormat != "json") {
                // A wrong --format is a hard error, never a silent substitution.
                error.WriteLine($"Unknown output format '{chosenFormat}'. Supported: json.");
                return Failed;
            }

            var path = parse.GetValue(dacpac)!;
            if (!File.Exists(path)) {
                error.WriteLine($"No such file: {path}");
                return Failed;
            }

            var outputPath = parse.GetValue(output);
            var wantsCheck = parse.GetValue(check);
            if (wantsCheck && outputPath is null) {
                error.WriteLine("--check needs --output: there is nothing to compare against.");
                return Failed;
            }

            var annotationsPath = parse.GetValue(annotations);
            SqlSchemaDescription document;
            try {
                document = DacpacExtractor.Extract(path, new ExtractOptions {
                    GeneratorName = "sqlschema",
                    GeneratorVersion = ToolVersion(),
                    DatabaseName = parse.GetValue(databaseName),
                    IncludeSchemas = parse.GetValue(includeSchemas) ?? [],
                    ExcludeSchemas = parse.GetValue(excludeSchemas) ?? [],
                    IncludeReferenced = parse.GetValue(includeReferenced),
                    Annotations = annotationsPath is null
                        ? null
                        : Documents.ReadAnnotations(annotationsPath),
                });
            } catch (AnnotationMismatchException mismatch) {
                error.WriteLine(mismatch.Message);
                return Failed;
            } catch (Exception failure) when (failure is IOException or InvalidOperationException
                                                or JsonException or ArgumentException) {
                error.WriteLine($"Could not read {path}: {failure.Message}");
                return Failed;
            }

            var json = Documents.Serialize(document);

            if (wantsCheck) {
                if (Documents.Matches(outputPath!, json)) return Ok;
                error.WriteLine($"{outputPath} is out of date. Re-run without --check to rewrite it.");
                return Invalid;
            }

            if (outputPath is null) {
                write.Write(json);
                return Ok;
            }

            var directory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(outputPath, json);
            return Ok;
        });

        return command;
    }

    // ---- validate ------------------------------------------------------------------------------

    private static Command BuildValidateCommand() {
        var command = new Command("validate", "Validate a document against the sql-schema JSON Schema");

        var documentArgument = new Argument<string>("document") {
            Description = "Path to the document to validate",
        };
        var format = new Option<string>("--format") {
            Description = "Output format: json, text",
            DefaultValueFactory = _ => "text",
        };
        format.HelpName = "fmt";

        command.Arguments.Add(documentArgument);
        command.Options.Add(format);

        command.SetAction(parse => {
            var write = parse.InvocationConfiguration.Output;
            var error = parse.InvocationConfiguration.Error;

            var chosenFormat = parse.GetValue(format) ?? "text";
            if (chosenFormat is not ("text" or "json")) {
                error.WriteLine($"Unknown output format '{chosenFormat}'. Supported: json, text.");
                return Failed;
            }

            var path = parse.GetValue(documentArgument)!;
            if (!File.Exists(path)) {
                error.WriteLine($"No such file: {path}");
                return Failed;
            }

            JsonDocument instance;
            try {
                instance = JsonDocument.Parse(File.ReadAllText(path));
            } catch (JsonException failure) {
                error.WriteLine($"{path} is not JSON: {failure.Message}");
                return Failed;
            }

            using (instance) {
                var result = ValidationSchema.Value.Evaluate(instance.RootElement, new EvaluationOptions {
                    OutputFormat = OutputFormat.List,
                });

                if (chosenFormat == "json") {
                    write.Write(JsonSerializer.Serialize(
                        result, new JsonSerializerOptions { WriteIndented = true }) + "\n");
                    return result.IsValid ? Ok : Invalid;
                }

                if (result.IsValid) {
                    write.WriteLine($"{path} is a valid sql-schema document.");
                    return Ok;
                }

                write.WriteLine($"{path} is not a valid sql-schema document:");
                var details = result.Details ?? [];
                foreach (var detail in details.Where(d => d.Errors is { Count: > 0 })) {
                    foreach (var problem in detail.Errors!) {
                        write.WriteLine($"  {detail.InstanceLocation} {problem.Value}");
                    }
                }
                return Invalid;
            }
        });

        return command;
    }
}
