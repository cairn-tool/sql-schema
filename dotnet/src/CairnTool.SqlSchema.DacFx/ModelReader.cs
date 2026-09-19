using Microsoft.SqlServer.Dac.Model;

using DacIndex = Microsoft.SqlServer.Dac.Model.Index;
using DacSchema = Microsoft.SqlServer.Dac.Model.Schema;
using SchemaObject = CairnTool.SqlSchema.SqlSchema;

namespace CairnTool.SqlSchema.DacFx;

/// <summary>Walks a loaded model and builds the document. One instance per extraction.</summary>
internal sealed class ModelReader(TSqlModel model, ExtractOptions options) {
    private readonly DacQueryScopes scope = options.IncludeReferenced
        // UserDefined alone is this package's own objects. SameDatabase adds the ones that arrived
        // from a referenced package -- and nothing else: the built-in and system scopes stay off,
        // so `sys` and `INFORMATION_SCHEMA` never appear.
        ? DacQueryScopes.UserDefined | DacQueryScopes.SameDatabase
        : DacQueryScopes.UserDefined;

    private HashSet<string> ownObjectIds = [];

    /// <summary>Reads a script-valued property. These are not strings and GetProperty&lt;string&gt; throws.</summary>
    private static string? Script(TSqlObject o, ModelPropertyClass property) =>
        Prop(o, property)?.ToString();

    // ⚠️ Reading the model defensively is not caution, it is correctness. Two distinct failures
    // are normal rather than exceptional here:
    //
    //   * GetProperty<T> throws NullReferenceException when a property is simply unset on an
    //     element -- a primary key has no FillFactor unless one was declared. The non-generic
    //     overload returns null instead, so nothing here uses the generic one.
    //   * The same logical concept is several model types. A table's column is `Column`; a table
    //     type's column is `TableTypeColumn`, and it does not support `IsHidden` at all. Asking
    //     throws DacModelException rather than returning null.
    //
    // Everything below funnels through Prop and Refs so that both are handled in one place.
    private static object? Prop(TSqlObject o, ModelPropertyClass p) {
        try {
            return o.GetProperty(p);
        } catch (Microsoft.SqlServer.Dac.Model.DacModelException) {
            return null;
        }
    }

    private static IEnumerable<TSqlObject> Refs(TSqlObject o, ModelRelationshipClass r) {
        try {
            return o.GetReferenced(r);
        } catch (Microsoft.SqlServer.Dac.Model.DacModelException) {
            return [];
        }
    }

    private static bool Flag(TSqlObject o, ModelPropertyClass p) => Prop(o, p) as bool? ?? false;

    private static int Number(TSqlObject o, ModelPropertyClass p) => Prop(o, p) as int? ?? 0;

    private static string? Text(TSqlObject o, ModelPropertyClass p) => Prop(o, p) as string;

    private IEnumerable<TSqlObject> Objects(ModelTypeClass type) =>
        model.GetObjects(scope, type).Where(Included);

    private bool Included(TSqlObject o) {
        var parts = o.Name.Parts;
        if (parts.Count == 0) return false;
        var schema = parts[0];
        if (options.IncludeSchemas.Count > 0
            && !options.IncludeSchemas.Contains(schema, StringComparer.OrdinalIgnoreCase)) {
            return false;
        }
        return !options.ExcludeSchemas.Contains(schema, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Attributes an object that arrived from a referenced package.</summary>
    private ExtensionsBuilder Provenance(TSqlObject o, ExtensionsBuilder builder) {
        if (options.IncludeReferenced && !ownObjectIds.Contains(Identifiers.Id(o))) {
            builder.Set("fromReference", o.Name.Parts[0]);
        }
        return builder;
    }

    public SqlSchemaDescription Read(string? packageName, string? packageVersion) {
        ownObjectIds = options.IncludeReferenced
            ? [.. model.GetObjects(DacQueryScopes.UserDefined).Select(Identifiers.Id)]
            : [];

        var byId = (IEnumerable<TSqlObject> source) => source.OrderBy(Identifiers.Id, StringComparer.Ordinal);

        return new SqlSchemaDescription {
            SchemaVersion = Schema.Version,
            Tool = new ToolInfo { Name = options.GeneratorName, Version = options.GeneratorVersion },
            Engine = ReadEngine(),
            Source = new SqlSourceInfo {
                Kind = "dacpac",
                Name = options.DatabaseName ?? packageName,
                Version = packageVersion,
            },
            Schemas = [.. byId(Objects(DacSchema.TypeClass)).Select(ReadSchema)],
            Tables = [.. byId(Objects(Table.TypeClass)).Select(ReadTable)],
            Views = [.. byId(Objects(View.TypeClass)).Select(ReadView)],
            Routines = [.. ReadRoutines()],
            Sequences = [.. byId(Objects(Sequence.TypeClass)).Select(ReadSequence)],
            UserDefinedTypes = [.. ReadUserDefinedTypes()],
            Synonyms = [.. byId(Objects(Synonym.TypeClass)).Select(ReadSynonym)],
            Triggers = [.. byId(Objects(DmlTrigger.TypeClass)).Select(ReadTrigger)],
        };
    }

    private SqlEngineInfo ReadEngine() {
        // The model's DatabaseOptions carries the collation by name, e.g.
        // SQL_Latin1_General_CP1_CI_AS -- so there is no need to reconstruct one from a locale
        // identifier, and no need to leave the member null.
        var options = model.GetObjects(DacQueryScopes.All, ModelSchema.DatabaseOptions).FirstOrDefault();
        var collation = options is null ? null : Text(options, DatabaseOptions.Collation);

        // Derived by probing the comparer rather than by parsing "_CS_" out of the name, because
        // the comparer is what the model itself compares identifiers with.
        var caseSensitive = !model.CollationComparer.Equals("a", "A");

        return new SqlEngineInfo {
            Name = "sqlserver",
            Version = model.EngineVersion.ToString(System.Globalization.CultureInfo.InvariantCulture),
            TargetPlatform = model.Version.ToString(),
            Collation = string.IsNullOrEmpty(collation) ? null : collation,
            CaseSensitive = caseSensitive,
        };
    }

    private SchemaObject ReadSchema(TSqlObject o) => new() {
        Id = Identifiers.Id(o),
        Path = Identifiers.Path(o),
        Description = null,
        Extensions = Provenance(o, new ExtensionsBuilder()).Build(),
    };

    // ---- tables ------------------------------------------------------------------------------

    private SqlTable ReadTable(TSqlObject table) {
        var id = Identifiers.Id(table);
        var extensions = new ExtensionsBuilder()
            .Set("memoryOptimized", Flag(table, Table.MemoryOptimized))
            .Set("changeTrackingEnabled", Flag(table, Table.ChangeTrackingEnabled));
        Provenance(table, extensions);

        var history = table.GetReferenced(Table.TemporalSystemVersioningHistoryTable).FirstOrDefault();
        SqlTemporal? temporal = null;
        if (history is not null) {
            var columns = table.GetReferenced(Table.Columns).ToList();
            var start = columns.FirstOrDefault(c => Number(c, Column.GeneratedAlwaysType) == 1);
            var end = columns.FirstOrDefault(c => Number(c, Column.GeneratedAlwaysType) == 2);
            if (start is not null && end is not null) {
                temporal = new SqlTemporal {
                    Kind = "systemVersioned",
                    PeriodStartColumn = Identifiers.Name(start),
                    PeriodEndColumn = Identifiers.Name(end),
                    HistoryTable = Identifiers.Id(history),
                };
            }
        }

        return new SqlTable {
            Id = id,
            Path = Identifiers.Path(table),
            Description = null,
            Columns = [.. table.GetReferenced(Table.Columns).Select(ReadColumn)],
            Constraints = [.. ConstraintsOf(id)],
            Indexes = [.. IndexesOf(id)],
            Temporal = temporal,
            Extensions = extensions.Build(),
        };
    }

    private SqlColumn ReadColumn(TSqlObject column) {
        var extensions = new ExtensionsBuilder()
            .Set("hidden", Flag(column, Column.IsHidden))
            .Set("sparse", Flag(column, Column.Sparse))
            .Set("rowGuidCol", Flag(column, Column.IsRowGuidCol))
            .Set("isFileStream", Flag(column, Column.IsFileStream))
            .Set("maskingFunction", Text(column, Column.MaskingFunction));

        SqlIdentity? identity = null;
        if (Flag(column, Column.IsIdentity)) {
            identity = new SqlIdentity {
                Seed = Text(column, Column.IdentitySeed) ?? "1",
                Increment = Text(column, Column.IdentityIncrement) ?? "1",
                Extensions = new ExtensionsBuilder()
                    .Set("notForReplication", Flag(column, Column.IsIdentityNotForReplication))
                    .Build(),
            };
        }

        SqlComputedColumn? computed = null;
        var expression = Script(column, Column.Expression);
        if (!string.IsNullOrEmpty(expression)) {
            computed = new SqlComputedColumn {
                Expression = expression,
                Persisted = Flag(column, Column.Persisted),
            };
        }

        // A computed column has an empty DataType relationship: the model records the expression
        // and its dependencies, and never resolves the resulting type. Omitting the member is the
        // only honest answer -- inventing one would be a silent wrong answer, which is worse than
        // an absent one. Its nullability comes from PersistedNullable, and a computed expression
        // is assumed nullable when the model reports neither.
        var declaredType = Refs(column, Column.DataType).Any()
            ? DataTypes.OfColumn(column)
            : null;
        var nullable = Prop(column, Column.Nullable) as bool?
            ?? Prop(column, Column.PersistedNullable) as bool?
            ?? true;

        return new SqlColumn {
            Name = Identifiers.Name(column),
            Description = null,
            DataType = declaredType,
            Nullable = nullable,
            Identity = identity,
            Computed = computed,
            Extensions = extensions.Build(),
        };
    }

    // ---- constraints and indexes -------------------------------------------------------------

    private static readonly string[] ReferentialActions =
        ["noAction", "cascade", "setNull", "setDefault"];

    private static string Action(int value) =>
        value >= 0 && value < ReferentialActions.Length ? ReferentialActions[value] : "noAction";

    private IEnumerable<SqlConstraint> ConstraintsOf(string tableId) {
        var found = new List<SqlConstraint>();

        foreach (var pk in Objects(PrimaryKeyConstraint.TypeClass)) {
            if (HostId(pk, PrimaryKeyConstraint.Host) != tableId) continue;
            found.Add(Key(pk, "primaryKey",
                pk.GetReferenced(PrimaryKeyConstraint.Columns),
                Flag(pk, PrimaryKeyConstraint.Clustered),
                Number(pk, PrimaryKeyConstraint.FillFactor)));
        }
        foreach (var uq in Objects(UniqueConstraint.TypeClass)) {
            if (HostId(uq, UniqueConstraint.Host) != tableId) continue;
            found.Add(Key(uq, "unique",
                uq.GetReferenced(UniqueConstraint.Columns),
                Flag(uq, UniqueConstraint.Clustered),
                Number(uq, UniqueConstraint.FillFactor)));
        }
        foreach (var fk in Objects(ForeignKeyConstraint.TypeClass)) {
            if (HostId(fk, ForeignKeyConstraint.Host) != tableId) continue;
            var target = fk.GetReferenced(ForeignKeyConstraint.ForeignTable).FirstOrDefault();
            found.Add(new SqlConstraint {
                Name = Identifiers.Name(fk),
                Kind = "foreignKey",
                Columns = [.. fk.GetReferenced(ForeignKeyConstraint.Columns).Select(Identifiers.Name)],
                References = target is null ? null : new SqlForeignKeyReference {
                    Table = Identifiers.Id(target),
                    Columns = [.. fk.GetReferenced(ForeignKeyConstraint.ForeignColumns).Select(Identifiers.Name)],
                },
                OnDelete = Action(Number(fk, ForeignKeyConstraint.DeleteAction)),
                OnUpdate = Action(Number(fk, ForeignKeyConstraint.UpdateAction)),
                Extensions = new ExtensionsBuilder()
                    .Set("notForReplication", Flag(fk, ForeignKeyConstraint.NotForReplication))
                    .Build(),
            });
        }
        foreach (var ck in Objects(CheckConstraint.TypeClass)) {
            if (HostId(ck, CheckConstraint.Host) != tableId) continue;
            found.Add(new SqlConstraint {
                Name = Identifiers.Name(ck),
                Kind = "check",
                Columns = [],
                Expression = Script(ck, CheckConstraint.Expression),
                Extensions = new ExtensionsBuilder()
                    .Set("notForReplication", Flag(ck, CheckConstraint.NotForReplication))
                    .Build(),
            });
        }
        foreach (var df in Objects(DefaultConstraint.TypeClass)) {
            if (HostId(df, DefaultConstraint.Host) != tableId) continue;
            var column = df.GetReferenced(DefaultConstraint.TargetColumn).FirstOrDefault();
            found.Add(new SqlConstraint {
                Name = Identifiers.Name(df),
                Kind = "default",
                Columns = column is null ? [] : [Identifiers.Name(column)],
                Expression = Script(df, DefaultConstraint.Expression),
            });
        }

        // Name order, unnamed last. The specification fixes this because a constraint list has no
        // semantic order and an arbitrary one would churn the document between runs.
        return found
            .OrderBy(c => c.Name is null)
            .ThenBy(c => c.Name, StringComparer.Ordinal);
    }

    private static SqlConstraint Key(
        TSqlObject o, string kind, IEnumerable<TSqlObject> columns, bool clustered, int fillFactor) =>
        new() {
            Name = Identifiers.Name(o),
            Kind = kind,
            Columns = [.. columns.Select(Identifiers.Name)],
            Extensions = new ExtensionsBuilder()
                .Set("clustered", clustered)
                .Set("fillFactor", fillFactor, whenDefault: 0)
                .Build(),
        };

    private string? HostId(TSqlObject o, ModelRelationshipClass host) {
        var owner = o.GetReferenced(host).FirstOrDefault();
        return owner is null ? null : Identifiers.Id(owner);
    }

    private IEnumerable<SqlIndex> IndexesOf(string tableId) {
        var found = new List<SqlIndex>();
        foreach (var index in Objects(DacIndex.TypeClass)) {
            var owner = index.GetReferenced(DacIndex.IndexedObject).FirstOrDefault();
            if (owner is null || Identifiers.Id(owner) != tableId) continue;
            found.Add(new SqlIndex {
                Name = Identifiers.Name(index),
                Unique = Flag(index, DacIndex.Unique),
                // No `descending`: the public model exposes an index's columns as a plain
                // relationship with no ordering data, so the specification requires it be omitted
                // rather than asserted false.
                Columns = [.. index.GetReferenced(DacIndex.Columns)
                    .Select(c => new SqlIndexColumn { Name = Identifiers.Name(c) })],
                IncludedColumns = index.GetReferenced(DacIndex.IncludedColumns)
                    .Select(Identifiers.Name).ToList() is { Count: > 0 } included ? included : null,
                Filter = Script(index, DacIndex.FilterPredicate),
                Extensions = new ExtensionsBuilder()
                    .Set("clustered", Flag(index, DacIndex.Clustered))
                    .Set("fillFactor", Number(index, DacIndex.FillFactor), whenDefault: 0)
                    .Build(),
            });
        }
        return found
            .OrderBy(i => i.Name is null)
            .ThenBy(i => i.Name, StringComparer.Ordinal);
    }

    // ---- programmable objects ----------------------------------------------------------------

    private SqlView ReadView(TSqlObject view) => new() {
        Id = Identifiers.Id(view),
        Path = Identifiers.Path(view),
        Description = null,
        // Null means "could not resolve"; an empty list means "there are none". A view whose
        // columns the model did not resolve gets null, never an empty list.
        Columns = view.GetReferenced(View.Columns).Select(ReadResultColumn).ToList() is { Count: > 0 } columns
            ? columns
            : null,
        Extensions = new ExtensionsBuilder()
            .Set("schemaBinding", Flag(view, View.WithSchemaBinding))
            .Set("withEncryption", Flag(view, View.WithEncryption))
            .Build(),
    };

    private static SqlResultColumn ReadResultColumn(TSqlObject column) => new() {
        Name = Identifiers.Name(column),
        Description = null,
        DataType = DataTypes.OfColumn(column),
        Nullable = Flag(column, Column.Nullable),
    };

    private IEnumerable<SqlRoutine> ReadRoutines() {
        var found = new List<SqlRoutine>();

        foreach (var p in Objects(Procedure.TypeClass)) {
            found.Add(new SqlRoutine {
                Id = Identifiers.Id(p),
                Path = Identifiers.Path(p),
                Kind = "procedure",
                Description = null,
                Parameters = [.. p.GetReferenced(Procedure.Parameters).Select(ReadParameter)],
                Returns = null,
                // Always empty. A procedure's result set is not in the model at any fidelity, and
                // a producer must not guess at one.
                ResultColumns = [],
                Extensions = new ExtensionsBuilder()
                    .Set("schemaBinding", Flag(p, Procedure.WithSchemaBinding))
                    .Set("nativeCompilation", Flag(p, Procedure.WithNativeCompilation))
                    .Build(),
            });
        }

        foreach (var f in Objects(ScalarFunction.TypeClass)) {
            var returnType = f.GetReferenced(ScalarFunction.ReturnType).FirstOrDefault();
            found.Add(new SqlRoutine {
                Id = Identifiers.Id(f),
                Path = Identifiers.Path(f),
                Kind = "scalarFunction",
                Description = null,
                Parameters = [.. f.GetReferenced(ScalarFunction.Parameters).Select(ReadParameter)],
                // ReturnType hands back the DataType object itself, not a column, so it is read
                // directly. Its declared facets are not exposed anywhere on the model -- a
                // function returning NVARCHAR(80) reports only `nvarchar` -- so the base type is
                // recorded without inventing a length.
                Returns = returnType is null ? null : new SqlRoutineReturn {
                    DataType = DataTypes.Read(
                        returnType, isMax: false, length: 0, precision: 0, scale: 0,
                        collation: null, facetsKnown: false),
                    // The model does not report a scalar function's return nullability, and every
                    // scalar function may return NULL, so this is true rather than guessed.
                    Nullable = true,
                },
                ResultColumns = [],
                Extensions = new ExtensionsBuilder()
                    .Set("schemaBinding", Flag(f, ScalarFunction.WithSchemaBinding))
                    .Set("returnsNullOnNullInput", Flag(f, ScalarFunction.ReturnsNullOnNullInput))
                    .Build(),
            });
        }

        foreach (var f in Objects(TableValuedFunction.TypeClass)) {
            found.Add(new SqlRoutine {
                Id = Identifiers.Id(f),
                Path = Identifiers.Path(f),
                Kind = "tableValuedFunction",
                Description = null,
                Parameters = [.. f.GetReferenced(TableValuedFunction.Parameters).Select(ReadParameter)],
                Returns = null,
                ResultColumns = [.. f.GetReferenced(TableValuedFunction.Columns).Select(ReadResultColumn)],
                Extensions = new ExtensionsBuilder()
                    .Set("schemaBinding", Flag(f, TableValuedFunction.WithSchemaBinding))
                    .Build(),
            });
        }

        return found.OrderBy(r => r.Id, StringComparer.Ordinal);
    }

    private static SqlParameter ReadParameter(TSqlObject parameter) => new() {
        Name = Identifiers.Name(parameter),
        DataType = DataTypes.OfParameter(parameter),
        Nullable = Flag(parameter, Parameter.IsNullable),
        Mode = Flag(parameter, Parameter.IsOutput) ? "inOut" : "in",
        ReadOnly = Flag(parameter, Parameter.ReadOnly) ? true : null,
        DefaultExpression = Script(parameter, Parameter.DefaultExpression),
    };

    private SqlSequence ReadSequence(TSqlObject sequence) {
        var noMin = Flag(sequence, Sequence.NoMinValue);
        var noMax = Flag(sequence, Sequence.NoMaxValue);
        var cached = Flag(sequence, Sequence.IsCached);
        var cacheSize = Number(sequence, Sequence.CacheSize);
        return new SqlSequence {
            Id = Identifiers.Id(sequence),
            Path = Identifiers.Path(sequence),
            Description = null,
            DataType = DataTypes.Read(
                Refs(sequence, Sequence.DataType).FirstOrDefault(),
                isMax: false, length: 0, precision: 0, scale: 0,
                collation: null, facetsKnown: false),
            // Strings, because a sequence bound exceeds a 32-bit integer -- and DacFx reports them
            // as strings for the same reason.
            StartValue = Text(sequence, Sequence.StartValue) ?? "1",
            Increment = Text(sequence, Sequence.IncrementValue) ?? "1",
            MinValue = noMin ? null : Text(sequence, Sequence.MinValue),
            MaxValue = noMax ? null : Text(sequence, Sequence.MaxValue),
            Cycle = Flag(sequence, Sequence.IsCycling),
            Cache = cached,
            CacheSize = cached && cacheSize > 0 ? cacheSize : null,
            Extensions = Provenance(sequence, new ExtensionsBuilder()).Build(),
        };
    }

    private IEnumerable<SqlUserDefinedType> ReadUserDefinedTypes() {
        var found = new List<SqlUserDefinedType>();

        // An alias type's type class is DataType, not UserDefinedType -- which is the CLR one.
        foreach (var alias in Objects(Microsoft.SqlServer.Dac.Model.DataType.TypeClass)) {
            found.Add(new SqlUserDefinedType {
                Id = Identifiers.Id(alias),
                Path = Identifiers.Path(alias),
                Kind = "alias",
                Description = null,
                // Uddt-prefixed, because the DataType class covers both the built-in types and
                // the user-defined alias types that live in the same model namespace.
                DataType = DataTypes.Read(
                    alias.GetReferenced(Microsoft.SqlServer.Dac.Model.DataType.Type).FirstOrDefault(),
                    Flag(alias, Microsoft.SqlServer.Dac.Model.DataType.UddtIsMax),
                    Number(alias, Microsoft.SqlServer.Dac.Model.DataType.UddtLength),
                    Number(alias, Microsoft.SqlServer.Dac.Model.DataType.UddtPrecision),
                    Number(alias, Microsoft.SqlServer.Dac.Model.DataType.UddtScale),
                    collation: null),
                Nullable = Flag(alias, Microsoft.SqlServer.Dac.Model.DataType.UddtNullable),
                Extensions = Provenance(alias, new ExtensionsBuilder()).Build(),
            });
        }

        foreach (var tableType in Objects(TableType.TypeClass)) {
            found.Add(new SqlUserDefinedType {
                Id = Identifiers.Id(tableType),
                Path = Identifiers.Path(tableType),
                Kind = "table",
                Description = null,
                Columns = [.. tableType.GetReferenced(TableType.Columns).Select(ReadColumn)],
                Constraints = [],
                Extensions = new ExtensionsBuilder()
                    .Set("memoryOptimized", Flag(tableType, TableType.MemoryOptimized))
                    .Build(),
            });
        }

        return found.OrderBy(t => t.Id, StringComparer.Ordinal);
    }

    private SqlSynonym ReadSynonym(TSqlObject synonym) {
        var target = synonym.GetReferenced(Synonym.ForObject).FirstOrDefault();
        return new SqlSynonym {
            Id = Identifiers.Id(synonym),
            Path = Identifiers.Path(synonym),
            Description = null,
            Target = Text(synonym, Synonym.ForObjectName) ?? "",
            TargetPath = target is null ? null : Identifiers.Path(target),
            Extensions = Provenance(synonym, new ExtensionsBuilder()).Build(),
        };
    }

    private SqlTrigger ReadTrigger(TSqlObject trigger) {
        var on = trigger.GetReferenced(DmlTrigger.TriggerObject).FirstOrDefault();
        var events = new List<string>();
        if (Flag(trigger, DmlTrigger.IsInsertTrigger)) events.Add("insert");
        if (Flag(trigger, DmlTrigger.IsUpdateTrigger)) events.Add("update");
        if (Flag(trigger, DmlTrigger.IsDeleteTrigger)) events.Add("delete");

        // TriggerType: Unknown=0, For=1, After=2, InsteadOf=3. FOR is a T-SQL synonym for AFTER,
        // so both map to `after`; `before` is not a SQL Server timing and appears only for other
        // engines.
        var timing = Number(trigger, DmlTrigger.TriggerType) == 3 ? "insteadOf" : "after";

        return new SqlTrigger {
            Id = Identifiers.Id(trigger),
            Path = Identifiers.Path(trigger),
            Kind = "dml",
            Description = null,
            Table = on is null ? null : Identifiers.Id(on),
            Timing = timing,
            Events = events,
            Disabled = Flag(trigger, DmlTrigger.Disabled),
            Extensions = Provenance(trigger, new ExtensionsBuilder()).Build(),
        };
    }
}
