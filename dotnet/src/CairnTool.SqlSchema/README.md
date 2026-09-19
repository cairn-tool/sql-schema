# CairnTool.SqlSchema

Model types, the JSON Schema document, and the canonicalizer for the
[sql-schema](https://github.com/cairn-tool/sql-schema) format.

This package reads and writes documents. It does not produce one from a database — that is
`CairnTool.SqlSchema.DacFx`, which depends on DacFx and is therefore neither trimmable nor
AOT-compatible. This one is both, so an application that only consumes documents pays nothing for
the extractor it does not use.
