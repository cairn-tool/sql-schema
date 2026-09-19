# CairnTool.SqlSchema.DacFx

Produces a [sql-schema](https://github.com/cairn-tool/sql-schema) document from a SQL Server
`.dacpac`, offline, with no connection to a database.

```csharp
var document = DacpacExtractor.Extract("MyDatabase.dacpac", new ExtractOptions {
    GeneratorName = "my-tool",
    GeneratorVersion = "1.0.0",
});
```

This package depends on DacFx and is therefore **neither trimmable nor AOT-compatible**. If you
only read documents, reference `CairnTool.SqlSchema` instead — it is both, and it is the whole
reason these are two packages.
