using Proxima.Domain.Transactions;

namespace Proxima.Importing;

public sealed class CsvImportParser : IImportParser
{
    public ImportFileType SupportedFileType => ImportFileType.Csv;

    public async Task<ImportPreview> ParseAsync(string filePath, CancellationToken cancellationToken)
    {
        string content = await File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(content))
        {
            return new ImportPreview(false, "CSV файл пустой.", [], false);
        }

        string[] lines = content
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(static line => line.Trim())
            .Where(static line => !string.IsNullOrWhiteSpace(line))
            .ToArray();
        if (lines.Length <= 1)
        {
            return new ImportPreview(false, "CSV не содержит строк данных.", [], false);
        }

        char separator = lines[0].Contains(';') ? ';' : ',';
        List<ImportedTransactionRow> rows = [];
        for (int index = 1; index < lines.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string[] cells = lines[index].Split(separator).Select(static item => item.Trim()).ToArray();
            rows.Add(ParseRow(index + 1, cells));
        }

        return new ImportPreview(true, string.Empty, rows, false);
    }

    private static ImportedTransactionRow ParseRow(int rowNumber, string[] cells)
    {
        if (cells.Length < 8)
        {
            return Invalid(rowNumber, "Недостаточно колонок.");
        }

        if (!DateTimeOffset.TryParse(cells[0], out DateTimeOffset tradeDate))
        {
            return Invalid(rowNumber, "Некорректная дата.");
        }

        string ticker = cells[1];
        string name = cells[2];
        if (!TryParseTransactionType(cells[3], out TransactionType transactionType))
        {
            return Suspicious(rowNumber, tradeDate, ticker, name, "Неподдерживаемый тип транзакции.");
        }

        if (!decimal.TryParse(cells[4], out decimal quantity))
        {
            return Invalid(rowNumber, "Некорректное количество.");
        }

        if (!decimal.TryParse(cells[5], out decimal price))
        {
            return Invalid(rowNumber, "Некорректная цена.");
        }

        if (!decimal.TryParse(cells[7], out decimal fees))
        {
            return Invalid(rowNumber, "Некорректная комиссия.");
        }

        string currency = cells[6];
        string? broker = cells.Length > 8 ? NormalizeOptional(cells[8]) : null;
        string? tag = cells.Length > 9 ? NormalizeOptional(cells[9]) : null;
        decimal grossAmount = quantity * price;

        ImportRowStatus status = ImportRowStatus.Valid;
        string? statusReason = null;
        if (quantity == 0 || price == 0)
        {
            status = ImportRowStatus.Suspicious;
            statusReason = "Нулевая цена или количество.";
        }

        return new ImportedTransactionRow(
            rowNumber,
            tradeDate,
            ticker,
            name,
            transactionType,
            quantity,
            price,
            grossAmount,
            fees,
            currency,
            broker,
            tag,
            status,
            statusReason);
    }

    private static ImportedTransactionRow Invalid(int rowNumber, string reason)
    {
        return new ImportedTransactionRow(
            rowNumber,
            DateTimeOffset.UtcNow,
            string.Empty,
            string.Empty,
            TransactionType.Buy,
            0,
            0,
            0,
            0,
            string.Empty,
            null,
            null,
            ImportRowStatus.Invalid,
            reason);
    }

    private static ImportedTransactionRow Suspicious(int rowNumber, DateTimeOffset tradeDate, string ticker, string name, string reason)
    {
        return new ImportedTransactionRow(
            rowNumber,
            tradeDate,
            ticker,
            name,
            TransactionType.Buy,
            0,
            0,
            0,
            0,
            "USD",
            null,
            null,
            ImportRowStatus.Suspicious,
            reason);
    }

    private static bool TryParseTransactionType(string raw, out TransactionType type)
    {
        if (Enum.TryParse(raw, ignoreCase: true, out type))
        {
            return true;
        }

        return raw.ToLowerInvariant() switch
        {
            "buy" => Assign(TransactionType.Buy, out type),
            "sell" => Assign(TransactionType.Sell, out type),
            "dividend" => Assign(TransactionType.Dividend, out type),
            "fee" => Assign(TransactionType.Fee, out type),
            "tax" => Assign(TransactionType.Tax, out type),
            "deposit" => Assign(TransactionType.Deposit, out type),
            "withdrawal" => Assign(TransactionType.Withdrawal, out type),
            "transfer" => Assign(TransactionType.Transfer, out type),
            "split" => Assign(TransactionType.Split, out type),
            "airdrop" => Assign(TransactionType.Airdrop, out type),
            "stakingreward" => Assign(TransactionType.StakingReward, out type),
            _ => false,
        };
    }

    private static bool Assign(TransactionType value, out TransactionType type)
    {
        type = value;
        return true;
    }

    private static string? NormalizeOptional(string raw)
    {
        return string.IsNullOrWhiteSpace(raw) ? null : raw.Trim();
    }
}
