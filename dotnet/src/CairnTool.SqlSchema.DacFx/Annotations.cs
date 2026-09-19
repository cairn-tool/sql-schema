namespace CairnTool.SqlSchema.DacFx;

/// <summary>Thrown when an annotation names something the document does not contain.</summary>
public sealed class AnnotationMismatchException(IReadOnlyList<string> unmatchedKeys)
    : Exception(BuildMessage(unmatchedKeys)) {
    public IReadOnlyList<string> UnmatchedKeys { get; } = unmatchedKeys;

    private static string BuildMessage(IReadOnlyList<string> keys) =>
        $"The annotation file names {keys.Count} object(s) that are not in the document: "
        + string.Join(", ", keys)
        + ". A stale annotation naming a dropped column is exactly the failure the file exists to "
        + "prevent, so this is an error rather than a warning.";
}
