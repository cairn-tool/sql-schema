namespace CairnTool.SqlSchema.Tests;

/// <summary>
/// Document fixtures, built through the generated models rather than from JSON text, so that a
/// change to a generated type breaks these at compile time rather than at assert time.
/// </summary>
internal static class Documents {
    public static SqlDataType Type(string name, string rendered) => new() {
        Name = name,
        UserDefined = false,
        Rendered = rendered,
    };

    public static SqlSchemaDescription Minimal() => new() {
        SchemaVersion = Schema.Version,
        Tool = new ToolInfo { Name = "sql-schema-tests", Version = "0.0.0" },
        Engine = new SqlEngineInfo {
            Name = "sqlserver",
            Version = null,
            TargetPlatform = "SqlAzureV12",
            Collation = null,
            CaseSensitive = false,
        },
        Source = new SqlSourceInfo {
            Kind = "dacpac",
            Name = "TestDatabase",
            Version = "1.0.0.0",
        },
        Schemas = [
            new SqlSchema { Id = "cfg", Path = ["cfg"], Description = null },
        ],
        Tables = [
            new SqlTable {
                Id = "cfg.StoreConfiguration",
                Path = ["cfg", "StoreConfiguration"],
                Description = null,
                Columns = [
                    new SqlColumn {
                        Name = "StoreConfigurationId",
                        Description = null,
                        DataType = Type("bigint", "bigint"),
                        Nullable = false,
                        Identity = new SqlIdentity { Seed = "1", Increment = "1" },
                    },
                    new SqlColumn {
                        Name = "ConfigurationData",
                        Description = null,
                        DataType = new SqlDataType {
                            Name = "varchar",
                            UserDefined = false,
                            MaxLength = true,
                            Rendered = "varchar(max)",
                        },
                        Nullable = false,
                    },
                ],
                Constraints = [
                    new SqlConstraint {
                        Name = "PK_cfg_StoreConfigurationId",
                        Kind = "primaryKey",
                        Columns = ["StoreConfigurationId"],
                    },
                ],
                Indexes = [],
            },
        ],
        Views = [],
        Routines = [],
        Sequences = [],
        UserDefinedTypes = [],
        Synonyms = [],
        Triggers = [],
    };
}
