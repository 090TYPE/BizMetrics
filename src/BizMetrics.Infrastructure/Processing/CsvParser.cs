using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;

namespace BizMetrics.Infrastructure.Processing;

public record CsvParseResult(List<string> Columns, List<Dictionary<string, string?>> Rows);

/// <summary>
/// Parses a CSV stream into ordered headers and a list of column→value maps.
/// Pure and side-effect free so it can be unit-tested without storage or a DB.
/// </summary>
public static class CsvParser
{
    public static CsvParseResult Parse(Stream stream)
    {
        using var reader = new StreamReader(stream);
        return Parse(reader);
    }

    public static CsvParseResult Parse(TextReader reader)
    {
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            TrimOptions = TrimOptions.Trim,
            MissingFieldFound = null,
            BadDataFound = null,
        };
        using var csv = new CsvReader(reader, config);

        if (!csv.Read() || !csv.ReadHeader())
            return new CsvParseResult(new(), new());

        var headers = (csv.HeaderRecord ?? [])
            .Select((h, i) => string.IsNullOrWhiteSpace(h) ? $"column_{i + 1}" : h.Trim())
            .ToList();

        var rows = new List<Dictionary<string, string?>>();
        while (csv.Read())
        {
            var row = new Dictionary<string, string?>(headers.Count);
            for (var i = 0; i < headers.Count; i++)
                row[headers[i]] = csv.TryGetField<string>(i, out var value) ? value : null;
            rows.Add(row);
        }

        return new CsvParseResult(headers, rows);
    }
}
