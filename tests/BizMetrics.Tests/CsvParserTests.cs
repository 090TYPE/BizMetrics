using BizMetrics.Infrastructure.Processing;
using Xunit;

namespace BizMetrics.Tests;

public class CsvParserTests
{
    private static CsvParseResult ParseText(string csv) =>
        CsvParser.Parse(new StringReader(csv));

    [Fact]
    public void Parses_headers_and_rows()
    {
        var result = ParseText("date,product,amount\n2026-01-01,Widget,12.50\n2026-01-02,Gadget,7\n");

        Assert.Equal(new[] { "date", "product", "amount" }, result.Columns);
        Assert.Equal(2, result.Rows.Count);
        Assert.Equal("Widget", result.Rows[0]["product"]);
        Assert.Equal("7", result.Rows[1]["amount"]);
    }

    [Fact]
    public void Handles_quoted_fields_with_commas()
    {
        var result = ParseText("name,note\n\"Acme, Inc.\",\"hello, world\"\n");

        Assert.Single(result.Rows);
        Assert.Equal("Acme, Inc.", result.Rows[0]["name"]);
        Assert.Equal("hello, world", result.Rows[0]["note"]);
    }

    [Fact]
    public void Names_blank_headers()
    {
        var result = ParseText("a,,c\n1,2,3\n");
        Assert.Equal(new[] { "a", "column_2", "c" }, result.Columns);
        Assert.Equal("2", result.Rows[0]["column_2"]);
    }

    [Fact]
    public void Empty_input_yields_no_columns_or_rows()
    {
        var result = ParseText("");
        Assert.Empty(result.Columns);
        Assert.Empty(result.Rows);
    }

    [Fact]
    public void Header_only_yields_columns_but_no_rows()
    {
        var result = ParseText("x,y,z\n");
        Assert.Equal(3, result.Columns.Count);
        Assert.Empty(result.Rows);
    }
}
