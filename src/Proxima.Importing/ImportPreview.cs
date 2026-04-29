namespace Proxima.Importing;

public sealed record ImportPreview(
    bool Succeeded,
    string Message,
    IReadOnlyList<ImportedTransactionRow> Rows,
    bool IsPdfLimited);
