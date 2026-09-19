# CairnTool.SqlSchema.MSBuild

Writes a [sql-schema](https://github.com/cairn-tool/sql-schema) document beside the `.dacpac`
whenever a `Microsoft.Build.Sql` project builds.

```xml
<Project DefaultTargets="Build">
  <Sdk Name="Microsoft.Build.Sql" Version="0.1.12-preview" />
  <ItemGroup>
    <PackageReference Include="CairnTool.SqlSchema.MSBuild" Version="1.*" />
  </ItemGroup>
</Project>
```

`dotnet build` now produces `sql-schema.json` next to the package it describes.

## Properties

| Property                     | Default                       |                                                    |
| ---------------------------- | ----------------------------- | -------------------------------------------------- |
| `SqlSchemaExtractEnabled`    | `true`                        | Kill switch.                                       |
| `SqlSchemaOutputPath`        | `$(TargetDir)sql-schema.json` | Where the document goes.                           |
| `SqlSchemaAnnotationsPath`   | —                             | An annotation document to merge descriptions from. |
| `SqlSchemaIncludeSchemas`    | —                             | Semicolon-separated; only these schemas.           |
| `SqlSchemaExcludeSchemas`    | —                             | Semicolon-separated; drop these schemas.           |
| `SqlSchemaIncludeReferenced` | `false`                       | Include objects from referenced packages.          |
| `SqlSchemaDatabaseName`      | `$(MSBuildProjectName)`       | Recorded in `source.name`.                         |
| `SqlSchemaFailOnDrift`       | `false`                       | Write nothing; fail if the document is stale.      |

## It runs the tool as a separate process, on purpose

`Microsoft.Build.Sql` ships its own DacFx — assembly version **17.0.0.0**, the same identity as the
one this package's payload uses, eight major file versions apart. An in-process MSBuild task would
have no way to say which one it binds to, and the answer would depend on target execution order
and on processor architecture: the SDK only uses `TaskHostFactory` when it is not on ARM, so the
same build would work on an x64 runner and fail on an Apple Silicon laptop.

Running the tool through `Exec` sidesteps the question entirely.
