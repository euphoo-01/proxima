namespace Proxima.Core.Application.Importing;

public sealed record ImportFileValidationResult(bool IsValid, string Message, ImportFileType? FileType);
