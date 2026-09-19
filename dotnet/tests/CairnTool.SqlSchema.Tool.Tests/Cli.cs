using System.CommandLine;

namespace CairnTool.SqlSchema.Tool.Tests;

/// <summary>Runs the tool in-process and captures what it wrote.</summary>
internal static class Cli {
    public sealed record Run(int ExitCode, string Out, string Error);

    public static Run Invoke(params string[] args) {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var configuration = new InvocationConfiguration { Output = stdout, Error = stderr };
        var exitCode = Program.BuildRootCommand().Parse(args).Invoke(configuration);
        return new Run(exitCode, stdout.ToString(), stderr.ToString());
    }

    public static string Root { get; } = Find();

    private static string Find() {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null) {
            if (File.Exists(Path.Combine(directory.FullName, "spec", "v1", "sql-schema.json"))) {
                return directory.FullName;
            }
            directory = directory.Parent;
        }
        throw new InvalidOperationException("Could not find the repository root.");
    }

    public static string Dacpac(string name) {
        var path = Path.Combine(Root, "artifacts", "fixtures", name, $"{name}.dacpac");
        if (!File.Exists(path)) {
            throw new FileNotFoundException($"Fixture '{name}' is not built. Run `npm run fixtures`.", path);
        }
        return path;
    }
}
