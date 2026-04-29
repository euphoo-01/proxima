namespace Proxima.Importing;

public sealed record ImportFileValidationResult(bool IsValid, string Message, ImportFileType? FileType);
