using Microsoft.SqlServer.Dac.Model;

namespace CairnTool.SqlSchema.DacFx;

/// <summary>
/// Builds a <see cref="SqlDataType"/> from what the model reports, applying the specification's
/// parameterization table.
/// </summary>
/// <remarks>
/// The table is not a formality. The model reports a precision and a scale for every column
/// regardless of type: an <c>int</c> comes back with precision 10 and a <c>datetime2</c> with
/// precision 0 and scale 7. Emitting what the model says would produce <c>int(10)</c> and
/// <c>datetime2(0,7)</c>. Which members a type carries is decided here, by type, and nowhere else.
/// </remarks>
internal static class DataTypes {
    // See ModelReader: GetProperty<T> throws when a property is unset on an element, which is the
    // normal case for most of these. The non-generic overload returns null.
    private static object? Prop(TSqlObject o, ModelPropertyClass p) {
        try {
            return o.GetProperty(p);
        } catch (Microsoft.SqlServer.Dac.Model.DacModelException) {
            return null;
        }
    }

    private static bool Flag(TSqlObject o, ModelPropertyClass p) => Prop(o, p) as bool? ?? false;

    private static int Number(TSqlObject o, ModelPropertyClass p) => Prop(o, p) as int? ?? 0;

    private static string? Text(TSqlObject o, ModelPropertyClass p) => Prop(o, p) as string;

    private enum Shape { None, Length, PrecisionScale, PrecisionOnly, ScaleOnly }

    private static Shape ShapeOf(string name) => name switch {
        "char" or "varchar" or "nchar" or "nvarchar" or "binary" or "varbinary" => Shape.Length,
        "decimal" or "numeric" => Shape.PrecisionScale,
        "float" => Shape.PrecisionOnly,
        "datetime2" or "datetimeoffset" or "time" => Shape.ScaleOnly,
        _ => Shape.None,
    };

    /// <summary>The specification's <c>rendered</c> algorithm. Collation is not part of it.</summary>
    public static string Render(SqlDataType type) {
        if (type.MaxLength == true) return $"{type.Name}(max)";
        if (type.Length is { } length) return $"{type.Name}({length})";
        if (type.Precision is { } p && type.Scale is { } s) return $"{type.Name}({p},{s})";
        if (type.Precision is { } only) return $"{type.Name}({only})";
        if (type.Scale is { } scale) return $"{type.Name}({scale})";
        return type.Name;
    }

    /// <param name="facetsKnown">
    /// False when the caller has no facets to give, as opposed to facets that are genuinely zero.
    /// A scalar function's return type is the case that matters: the model hands back the base
    /// type object and exposes its declared length nowhere, so passing zeros would render
    /// <c>nvarchar(0)</c> -- a value that is not merely unhelpful but wrong.
    /// </param>
    public static SqlDataType Read(
        TSqlObject? declared,
        bool isMax,
        int length,
        int precision,
        int scale,
        string? collation,
        bool facetsKnown = true) {
        // A built-in type has a one-part name; a user-defined one is schema-qualified. That is the
        // discriminator, and it is also what decides whether `name` holds a base type or an id.
        var parts = declared?.Name.Parts;
        // Callers hand this either a column's referenced type or a type object read straight off a
        // relationship. Both are DataType objects; nothing else is a legal argument.
        var userDefined = parts is { Count: > 1 };
        var name = parts is null || parts.Count == 0
            ? "sql_variant"
            : userDefined
                ? Identifiers.Join([.. parts])
                : parts[^1].ToLowerInvariant();

        var shape = userDefined || !facetsKnown ? Shape.None : ShapeOf(name);

        var type = new SqlDataType {
            Name = name,
            UserDefined = userDefined,
            MaxLength = shape == Shape.Length && isMax ? true : null,
            Length = shape == Shape.Length && !isMax ? length : null,
            Precision = shape is Shape.PrecisionScale or Shape.PrecisionOnly ? precision : null,
            Scale = shape is Shape.PrecisionScale or Shape.ScaleOnly ? scale : null,
            Collation = string.IsNullOrEmpty(collation) ? null : collation,
            Rendered = "",
        };

        return type with { Rendered = Render(type) };
    }

    private static TSqlObject? Referenced(TSqlObject o, ModelRelationshipClass r) {
        try {
            return o.GetReferenced(r).FirstOrDefault();
        } catch (Microsoft.SqlServer.Dac.Model.DacModelException) {
            return null;
        }
    }

    public static SqlDataType OfColumn(TSqlObject column) => Read(
        Referenced(column, Column.DataType),
        Flag(column, Column.IsMax),
        Number(column, Column.Length),
        Number(column, Column.Precision),
        Number(column, Column.Scale),
        Text(column, Column.Collation));

    public static SqlDataType OfParameter(TSqlObject parameter) => Read(
        Referenced(parameter, Parameter.DataType),
        Flag(parameter, Parameter.IsMax),
        Number(parameter, Parameter.Length),
        Number(parameter, Parameter.Precision),
        Number(parameter, Parameter.Scale),
        collation: null);
}
