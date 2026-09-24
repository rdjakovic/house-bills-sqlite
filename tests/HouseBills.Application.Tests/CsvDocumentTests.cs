using HouseBills.Application.Export;

namespace HouseBills.Application.Tests;

public sealed class CsvDocumentTests
{
    [Theory]
    [InlineData("plain", "plain")]
    [InlineData("a;b", "\"a;b\"")]
    [InlineData("say \"hi\"", "\"say \"\"hi\"\"\"")]
    [InlineData("two\r\nlines", "\"two\r\nlines\"")]
    [InlineData(" padded ", "\" padded \"")]
    [InlineData("1.234,56", "1.234,56")]
    [InlineData("Računi čekaju", "Računi čekaju")]
    public void AddRow_Cell_IsQuotedOnlyWhenNeeded(string cell, string expected)
    {
        new CsvDocument(";").AddRow(cell).ToString().ShouldBe(expected + "\r\n");
    }

    [Fact]
    public void AddRow_SeveralRows_JoinsWithSeparatorAndWritesNullAsEmpty()
    {
        var csv = new CsvDocument(",")
            .AddRow("Month", "Total")
            .AddRow("January", null)
            .AddBlankLine()
            .AddRow("Category", "1,234.56")
            .ToString();

        csv.ShouldBe("Month,Total\r\nJanuary,\r\n\r\nCategory,\"1,234.56\"\r\n");
    }
}