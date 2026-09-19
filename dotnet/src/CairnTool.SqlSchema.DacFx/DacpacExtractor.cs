using Microsoft.SqlServer.Dac;
using Microsoft.SqlServer.Dac.Model;

using DacIndex = Microsoft.SqlServer.Dac.Model.Index;
using DacSchema = Microsoft.SqlServer.Dac.Model.Schema;
// `SqlSchema` alone would bind to the enclosing CairnTool.SqlSchema namespace from inside this
// child namespace, so the record is aliased rather than named directly.
using SchemaObject = CairnTool.SqlSchema.SqlSchema;

namespace CairnTool.SqlSchema.DacFx;

/// <summary>Reads a <c>.dacpac</c> and produces a sql-schema document. No database is contacted.</summary>
public static class DacpacExtractor {
    public static SqlSchemaDescription Extract(string dacpacPath, ExtractOptions? options = null) {
        ArgumentException.ThrowIfNullOrEmpty(dacpacPath);
        options ??= new ExtractOptions();

        string? packageName;
        string? packageVersion;
        using (var package = DacPackage.Load(dacpacPath)) {
            packageName = package.Name;
            packageVersion = package.Version?.ToString();
        }

        using var model = new TSqlModel(dacpacPath);
        var reader = new ModelReader(model, options);
        var document = reader.Read(packageName, packageVersion);
        return Annotate(document, options.Annotations);
    }

    /// <summary>
    /// Merges descriptions over an extracted document. Last step, after ordering is decided, so an
    /// annotation can never change the document's structure.
    /// </summary>
    private static SqlSchemaDescription Annotate(
        SqlSchemaDescription document,
        SqlAnnotations? annotations) {
        if (annotations is null || annotations.Descriptions.Count == 0) return document;

        var used = new HashSet<string>(StringComparer.Ordinal);
        string? Take(string address) {
            if (!annotations.Descriptions.TryGetValue(address, out var text)) return null;
            used.Add(address);
            return text;
        }

        var schemas = document.Schemas
            .Select(s => s with { Description = Take(s.Id) ?? s.Description })
            .ToList();

        var tables = document.Tables.Select(table => table with {
            Description = Take(table.Id) ?? table.Description,
            Columns = [.. table.Columns.Select(column => column with {
                Description = Take(Identifiers.Member(table.Id, column.Name)) ?? column.Description,
            })],
        }).ToList();

        var views = document.Views.Select(view => view with {
            Description = Take(view.Id) ?? view.Description,
        }).ToList();

        var routines = document.Routines.Select(routine => routine with {
            Description = Take(routine.Id) ?? routine.Description,
        }).ToList();

        var sequences = document.Sequences
            .Select(s => s with { Description = Take(s.Id) ?? s.Description }).ToList();
        var types = document.UserDefinedTypes
            .Select(t => t with { Description = Take(t.Id) ?? t.Description }).ToList();
        var synonyms = document.Synonyms
            .Select(s => s with { Description = Take(s.Id) ?? s.Description }).ToList();
        var triggers = document.Triggers
            .Select(t => t with { Description = Take(t.Id) ?? t.Description }).ToList();

        var unmatched = annotations.Descriptions.Keys
            .Where(key => !used.Contains(key))
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToList();
        if (unmatched.Count > 0) throw new AnnotationMismatchException(unmatched);

        return document with {
            Schemas = schemas,
            Tables = tables,
            Views = views,
            Routines = routines,
            Sequences = sequences,
            UserDefinedTypes = types,
            Synonyms = synonyms,
            Triggers = triggers,
        };
    }
}
