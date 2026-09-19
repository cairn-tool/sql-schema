using System.Text;
using Microsoft.SqlServer.Dac.Model;

namespace CairnTool.SqlSchema.DacFx;

/// <summary>
/// The specification's <c>id</c> algorithm. It is implemented here rather than taken from
/// <see cref="ObjectIdentifier.ToString"/> deliberately: that method's quoting rules are DacFx's,
/// and the specification requires <c>id</c> to be a pure function of <c>path</c> so that two
/// producers cannot render the same object differently.
/// </summary>
internal static class Identifiers {
    private static bool IsBare(string segment) {
        if (segment.Length == 0) return false;
        foreach (var c in segment) {
            var ok = char.IsAsciiLetterOrDigit(c) || c is '_' or '@' or '#' or '$';
            if (!ok) return false;
        }
        return true;
    }

    public static string Join(IReadOnlyList<string> path) {
        var builder = new StringBuilder();
        for (var i = 0; i < path.Count; i++) {
            if (i > 0) builder.Append('.');
            var segment = path[i];
            if (IsBare(segment)) {
                builder.Append(segment);
            } else {
                builder.Append('[').Append(segment.Replace("]", "]]", StringComparison.Ordinal)).Append(']');
            }
        }
        return builder.ToString();
    }

    public static IReadOnlyList<string> Path(TSqlObject o) => [.. o.Name.Parts];

    public static string Id(TSqlObject o) => Join(Path(o));

    /// <summary>The last segment: a nested member's bare name, or a top-level object's own name.</summary>
    public static string Name(TSqlObject o) => o.Name.Parts[^1];

    /// <summary>The address the annotation sidecar is keyed by.</summary>
    public static string Member(string ownerId, string name) => $"{ownerId}.{name}";
}
