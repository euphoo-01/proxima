using Proxima.Importing;
using System.Text;

namespace Proxima.Importing.Tests;

internal static class Program
{
    private static void Main()
    {
        string? assemblyName = typeof(ImportingAssemblyMarker).Assembly.GetName().Name;
        Assert(assemblyName == "Proxima.Importing", "Importing assembly name must be Proxima.Importing.");
        CsvParser_ParsesValidRows().GetAwaiter().GetResult();
        CsvParser_FlagsInvalidDate();
        FileValidator_RejectsUnsupportedExtension();
        Console.WriteLine("Proxima.Importing.Tests passed.");
    }

    private static async Task CsvParser_ParsesValidRows()
    {
        string directory = Path.Combine(Path.GetTempPath(), "proxima-import-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "demo.csv");
        string csv = "date,ticker,name,type,quantity,price,currency,fee,broker,tag\n2026-01-01,AAPL,Apple,Buy,2,100,USD,1,Broker A,tech";
        await File.WriteAllTextAsync(path, csv, Encoding.UTF8).ConfigureAwait(false);

        ImportPreview preview = await new CsvImportParser().ParseAsync(path, CancellationToken.None).ConfigureAwait(false);
        Assert(preview.Succeeded, "CSV parser should succeed for valid file.");
        Assert(preview.Rows.Count == 1, "CSV parser should map one row.");
        Assert(preview.Rows[0].Status == ImportRowStatus.Valid, "Valid row should not be flagged.");
    }

    private static void CsvParser_FlagsInvalidDate()
    {
        string directory = Path.Combine(Path.GetTempPath(), "proxima-import-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "broken.csv");
        string csv = "date,ticker,name,type,quantity,price,currency,fee\nbad-date,AAPL,Apple,Buy,2,100,USD,1";
        File.WriteAllText(path, csv, Encoding.UTF8);

        ImportPreview preview = new CsvImportParser().ParseAsync(path, CancellationToken.None).GetAwaiter().GetResult();
        Assert(preview.Rows.Count == 1, "CSV parser should return one row.");
        Assert(preview.Rows[0].Status == ImportRowStatus.Invalid, "Invalid date should be flagged.");
    }

    private static void FileValidator_RejectsUnsupportedExtension()
    {
        ImportFileValidationResult result = new ImportFileValidator(1024).Validate("report.xlsx", 128);
        Assert(!result.IsValid, "Unsupported extension must be rejected.");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
