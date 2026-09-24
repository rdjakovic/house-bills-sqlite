using HouseBills.Application.Export;

namespace HouseBills.Application.Tests;

public sealed class CsvReaderTests
{
    [Fact]
    public void Parse_QuotedCells_KeepsSeparatorsQuotesAndLineBreaks()
    {
        var rows = CsvReader.Parse("Name;Notes\r\n\"EPS; main\";\"say \"\"hi\"\"\r\nnext line\"\r\n", ";");

        rows.Count.ShouldBe(2);
        rows[1].ShouldBe(["EPS; main", "say \"hi\"\r\nnext line"]);
    }

    [Fact]
    public void Parse_LfLineEndingsAndEmptyCells_ReturnsEveryCell()
    {
        var rows = CsvReader.Parse("a,b,c\n1,,3\n,,", ",");

        rows.ShouldBe([["a", "b", "c"], ["1", "", "3"], ["", "", ""]]);
    }

    [Fact]
    public void Parse_WrittenByCsvDocument_ReadsTheSameCells()
    {
        var text = new CsvDocument(",").AddRow("Rent, flat", " padded ", null, "Računi").ToString();

        CsvReader.Parse(text, ",").ShouldHaveSingleItem().ShouldBe(["Rent, flat", " padded ", "", "Računi"]);
    }

    [Theory]
    [InlineData("Name;Notes\r\nx;y", ",", ";")]
    [InlineData("Name,Notes\r\nx,y", ";", ",")]
    [InlineData("Name\tNotes", ";", "\t")]
    [InlineData("Name\r\nEPS", ";", ";")]
    [InlineData("\"A;B\",C\r\n", ";", ",")]
    public void DetectSeparator_HeaderLine_PicksTheSeparatorItUses(string text, string preferred, string expected)
    {
        CsvReader.DetectSeparator(text, preferred).ShouldBe(expected);
    }
}