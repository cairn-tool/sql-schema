# CairnTool.SqlSchema.Tool

```bash
dotnet tool install -g CairnTool.SqlSchema.Tool
sqlschema extract MyDatabase.dacpac --output sql-schema.json
```

Turns a SQL Server `.dacpac` into a [sql-schema](https://github.com/cairn-tool/sql-schema) document,
offline, with no connection to a database.

| Command               |                                                                             |
| --------------------- | --------------------------------------------------------------------------- |
| `extract <dacpac>`    | Read a package and write a document. `--check` compares instead of writing. |
| `validate <document>` | Validate a document against the embedded JSON Schema.                       |
| `describe`            | This tool's own contract, as a machine-readable CLI description.            |

`describe` exists because this tool is the first external consumer of
[cli-schema](https://github.com/cairn-tool/cli-schema): `sqlschema describe --format json` emits a
document that repository specifies, so the tool's reference documentation is generated rather than
written by hand and left to rot.

## Exit codes

| Code |                                                                                       |
| ---- | ------------------------------------------------------------------------------------- |
| 0    | Success.                                                                              |
| 1    | The operation failed — the package could not be read, or an annotation named nothing. |
| 2    | The document is invalid, or `--check` found it differs from what would be written.    |
